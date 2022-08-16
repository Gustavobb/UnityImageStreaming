using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Threading;

public class ImageUtils
{
    public delegate void OnFinishBytes(byte[] bytes);
    public delegate void OnFinishTexture(Texture2D texture);
    public delegate void OnFinishRenderTexture(RenderTexture texture);
    private static List<Thread> _threadPool = new List<Thread>();

    public static void RenderTexture2Texture2D(RenderTexture renderTexture, TextureFormat format, OnFinishTexture onFinishTexture)
    {
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, format, false);
        RenderTexture.active = renderTexture;
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = null;
        onFinishTexture?.Invoke(texture2D);
    }

    public static void Texture2D2RenderTexture(Texture2D texture, OnFinishRenderTexture onFinishRenderTexture)
    {
        RenderTexture renderTexture = new RenderTexture(texture.width, texture.height, 24, texture.graphicsFormat);
        renderTexture.Create();
        RenderTexture.active = renderTexture;
        Graphics.Blit(texture, renderTexture);
        RenderTexture.active = null;
        onFinishRenderTexture?.Invoke(renderTexture);
    }

    public static void RenderTexture2PNG(RenderTexture renderTexture, TextureFormat format, OnFinishBytes onFinishBytes)
    {
        OnFinishTexture onFinishTexture = (texture) => Texture2D2PNG(texture, onFinishBytes);
        RenderTexture2Texture2D(renderTexture, format, onFinishTexture);
    }

    public static void RenderTexture2Texture2DAsync(RenderTexture renderTexture, TextureFormat format, OnFinishTexture onFinishTexture)
    {
        AsyncGPUReadback.Request(renderTexture, 0, format, req =>
        {
            if (!AssertGPUReadback(req, renderTexture))
                return;
            
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, format, false);
            texture.LoadRawTextureData(req.GetData<byte>().ToArray());
            texture.Apply();
            onFinishTexture?.Invoke(texture);
        });
    }

    public static void RenderTexture2PNGAsync(RenderTexture renderTexture, TextureFormat format, OnFinishBytes onFinishBytes)
    {
        AsyncGPUReadback.Request(renderTexture, 0, format, req =>
        {
            if (!AssertGPUReadback(req, renderTexture))
                return;

            byte [] bytes = req.GetData<byte>().ToArray();
            bytes = ImageConversion.EncodeArrayToPNG(bytes, renderTexture.graphicsFormat, (uint) renderTexture.width, (uint) renderTexture.height);
            onFinishBytes?.Invoke(bytes);
        });
    }

    public static void RenderTexture2ArrayAsync(RenderTexture renderTexture, TextureFormat format, OnFinishBytes onFinishBytes)
    {
        AsyncGPUReadback.Request(renderTexture, 0, format, req =>
        {
            if (!AssertGPUReadback(req, renderTexture))
                return;

            byte [] bytes = req.GetData<byte>().ToArray();
            onFinishBytes?.Invoke(bytes);
        });
    }

    private static bool AssertGPUReadback(AsyncGPUReadbackRequest req, RenderTexture renderTexture)
    {
        if (req.hasError)
        {
            Debug.LogError("Error while reading render texture.");
            return false;
        }
        
        if (renderTexture == null)
            return false;
        
        return true;
    }

    public static void Array2PNG(byte[] bytes, GraphicsFormat format, uint width, uint height, OnFinishBytes onFinishBytes)
    {
        byte[] png = ImageConversion.EncodeArrayToPNG(bytes, format, width, height);
        onFinishBytes?.Invoke(png);
    }

    public static void Texture2D2PNG(Texture2D texture, OnFinishBytes onFinishBytes)
    {
        byte[] bytes = ImageConversion.EncodeArrayToPNG(texture.GetRawTextureData(), texture.graphicsFormat, (uint) texture.width, (uint) texture.height);
        onFinishBytes?.Invoke(bytes);
    }

    public static void PNG2Texture2D(byte[] bytes, OnFinishTexture onFinishTexture)
    {
        Texture2D texture = new Texture2D(0, 0);
        texture.LoadRawTextureData(bytes);
        texture.Apply();
        onFinishTexture?.Invoke(texture);
    }

    public static void PNG2RenderTexture(byte[] bytes, OnFinishRenderTexture onFinishRenderTexture)
    {
        OnFinishTexture onFinishTexture = (texture) => Texture2D2RenderTexture(texture, onFinishRenderTexture);
        PNG2Texture2D(bytes, onFinishTexture);
    }

    public static void SaveArrayAsPNGToDiskAsync(byte[] bytes, GraphicsFormat format, uint width, uint height, string filePath)
    {
        if (bytes == null)
            return;

        Thread thread = new Thread(() => 
        {
            byte[] png = ImageConversion.EncodeArrayToPNG(bytes, format, width, height);
            System.IO.File.WriteAllBytes(filePath, png);
        });
        
        _threadPool.Add(thread);
        thread.Start();
    }

    public static void SaveArrayAsPNGToDisk(byte[] bytes, GraphicsFormat format, uint width, uint height, string filePath)
    {
        byte[] png = ImageConversion.EncodeArrayToPNG(bytes, format, width, height);
        System.IO.File.WriteAllBytes(filePath, png);
    }

    public static void SavePNG2DiskAsync(byte[] png, string path)
    {
        if (png == null)
        return;

        Thread thread = new Thread(() =>
        {
            Debug.Log("Saving frame to: " + path);
            System.IO.File.WriteAllBytes(path, png);
        });
        
        _threadPool.Add(thread);
        thread.Start();
    }

    public static void SavePNG2Disk(byte[] png, string path)
    {
        if (png == null)
            return;

        Debug.Log("Saving frame to: " + path);
        System.IO.File.WriteAllBytes(path, png);
    }

    public static void StopThreads()
    {
        foreach (var thread in _threadPool)
            thread.Abort();
    }
}
