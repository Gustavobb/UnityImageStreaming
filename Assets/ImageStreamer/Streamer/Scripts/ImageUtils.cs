using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Threading;
using WebSocketSharp.Server;

public class ImageUtils
{
    public delegate void OnFinishBytes(byte[] bytes);
    public delegate void OnFinishNativeBytes(NativeArray<byte> bytes);
    public delegate void OnFinishTexture(Texture2D texture);
    public delegate void OnFinishRenderTexture(RenderTexture texture);
    private static List<ThreadData> _threadPool = new List<ThreadData>();

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

    public static void RenderTexture2NativeArrayAsync(RenderTexture renderTexture, TextureFormat format, OnFinishNativeBytes onFinishNativeBytes)
    {
        AsyncGPUReadback.Request(renderTexture, 0, format, req =>
        {
            if (!AssertGPUReadback(req, renderTexture))
                return;

            NativeArray<byte> nativeBytes = req.GetData<byte>();
            onFinishNativeBytes?.Invoke(nativeBytes);
        });
    }

    public static void RenderTexture2ArrayAsync(RenderTexture renderTexture, TextureFormat format, OnFinishBytes onFinishBytes)
    {
        AsyncGPUReadback.Request(renderTexture, 0, format, req =>
        {
            if (!AssertGPUReadback(req, renderTexture))
                return;

            byte[] bytes = req.GetData<byte>().ToArray();
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

        System.Threading.ParameterizedThreadStart threadFunction = obj =>
        {
            byte[] png = ImageConversion.EncodeArrayToPNG(bytes, format, width, height);
            System.IO.File.WriteAllBytes(filePath, png);
        };

        HandleThreadPool(threadFunction);
    }

    public static void SaveNativeByteArrayAsPNGToDiskAsync(NativeArray<byte> bytes, GraphicsFormat format, uint width, uint height, string filePath)
    {
       if (bytes.Length == 0)
            return;

        System.Threading.ParameterizedThreadStart threadFunction = obj =>
        {
            byte[] png;
            if (_threadPool[(int) obj].bytes == null)
            {
                png = new byte[bytes.Length];
                _threadPool[(int) obj].bytes = png;
            }
            else
                png = _threadPool[(int) obj].bytes;

            bytes.CopyTo(png);
            png = ImageConversion.EncodeArrayToPNG(png, format, width, height);
            System.IO.File.WriteAllBytes(filePath, png);
        };

        HandleThreadPool(threadFunction);
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

        System.Threading.ParameterizedThreadStart threadFunction = obj =>
        {
            System.IO.File.WriteAllBytes(path, png);
        };
        
        HandleThreadPool(threadFunction);
    }

    public static void SavePNG2Disk(byte[] png, string path)
    {
        if (png == null)
            return;

        System.IO.File.WriteAllBytes(path, png);
    }

    public static void SendArrayAsPNGToSocket(byte[] bytes, GraphicsFormat format, uint width, uint height, WebSocketSessionManager server, string fileName)
    {
        OnFinishBytes onFinishBytes = (png) => 
        {
            SocketSendForm form = new SocketSendForm()
            {
                name = fileName,
                fileBytes = png
            };
            byte[] data = form.ToBytes();
            server.BroadcastAsync(data,() => {});
        };

        Array2PNG(bytes, format, width, height, onFinishBytes);
    }

    public static void SendArrayAsPNGToSocketAsync(byte[] bytes, GraphicsFormat format, uint width, uint height, WebSocketSessionManager server, string fileName)
    {
        if (bytes == null)
            return;

        System.Threading.ParameterizedThreadStart threadFunction = obj =>
        {
            byte[] png = ImageConversion.EncodeArrayToPNG(bytes, format, width, height);

            SocketSendForm form = new SocketSendForm()
            {
                name = fileName,
                fileBytes = png
            };

            byte[] data = form.ToBytes();
            server.Broadcast(data);
        };
        
        HandleThreadPool(threadFunction);
    }

    public static void SendNativeByteArrayAsPNGToSocketAsync(NativeArray<byte> bytes, GraphicsFormat format, uint width, uint height, WebSocketSessionManager server, string fileName)
    {
        if (bytes.Length == 0)
            return;

        System.Threading.ParameterizedThreadStart threadFunction = obj =>
        {
            byte[] png;
            if (_threadPool[(int) obj].bytes == null)
            {
                png = new byte[bytes.Length];
                _threadPool[(int) obj].bytes = png;
            }
            else
                png = _threadPool[(int) obj].bytes;

            bytes.CopyTo(png);
            png = ImageConversion.EncodeArrayToPNG(png, format, width, height);

            SocketSendForm form = new SocketSendForm()
            {
                name = fileName,
                fileBytes = png
            };

            byte[] data = form.ToBytes();
            server.Broadcast(data);
        };

        HandleThreadPool(threadFunction);
    }

    public static void SaveSocketFormAsPNG2DiskAsync(byte[] data, string path)
    {
        if (data == null)
            return;

        System.Threading.ParameterizedThreadStart threadFunction = obj =>
        {
            SocketSendForm form = new SocketSendForm();
            form.FromBytes(data);
            
            string filePath = CreateFolderAndReturnPath(form.name, path);
            System.IO.File.WriteAllBytes(filePath, form.fileBytes);
        };
        
        HandleThreadPool(threadFunction);
    }

    public static void SaveSocketFormAsPNG2Disk(byte[] data, string path)
    {
        if (data == null)
            return;

        SocketSendForm form = new SocketSendForm();
        form.FromBytes(data);

        string filePath = CreateFolderAndReturnPath(form.name, path);
        System.IO.File.WriteAllBytes(filePath, form.fileBytes);
    }

    private static string CreateFolderAndReturnPath(string fileName, string path)
    {
        string[] paths = fileName.Split('/');
        string folder = System.IO.Path.Combine(path, paths[0]);

        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);
        
        return System.IO.Path.Combine(folder, paths[1]);
    }

    private static void HandleThreadPool(System.Threading.ParameterizedThreadStart threadFunction)
    {
        Thread thread;

        for (int i = 0; i < _threadPool.Count; i++)
        {
            if (!_threadPool[i].thread.IsAlive)
            {
                thread = _threadPool[i].thread;
                thread.Abort();
                thread = new Thread(threadFunction);
                thread.Start(i);
                _threadPool[i].thread = thread;
                return;
            }
        }

        thread = new Thread(threadFunction);
        ThreadData newThreadData = new ThreadData()
        {
            thread = thread,
            bytes = null
        };
        _threadPool.Add(newThreadData);
        thread.Start(_threadPool.Count - 1);
    }

    public static void StopAllThreads()
    {
        foreach (ThreadData obj in _threadPool)
            obj.thread.Abort();
    }
}

class ThreadData
{
    public Thread thread;
    public byte[] bytes;
}