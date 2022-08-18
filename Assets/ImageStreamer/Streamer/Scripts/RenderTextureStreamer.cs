using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using WebSocketSharp;
using Unity.Collections;
using WebSocketSharp.Server;

public class RenderTextureStreamer : DataStreamer
{
    [Header("Camera Config")]
    [SerializeField] private List<Camera> _cameras;
    [SerializeField] private UnityEvent _onGotCameraFrame;
    [SerializeField] private Vector2 _cameraResolution;

    [Header("Camera Optimization Config")]
    [SerializeField] private bool _iterateCameras = false;
    [SerializeField] private int _howManyCanProcess = 1;

    private int _howManyProcessed = 0;
    private int _currentCameraIndex = 0;
    private List<int> _cameraProcessedFrameCount = new List<int>();

    protected delegate void OnStreamProcessed<T>(T data, Camera camera);
    protected delegate void OnStreamProcessedAsync<T>(T data, Camera camera);

    private OnStreamProcessed<byte[]> _onStreamProcessed;
    private OnStreamProcessedAsync<byte[]> _onStreamProcessedAsyncByte;
    private OnStreamProcessedAsync<NativeArray<byte>> _onStreamProcessedAsyncNativeArray;

    private delegate void OnFrameProcessed(byte[] data, Camera camera);
    private OnFrameProcessed _onFrameProcessed;

    protected override void SetupSocketMode()
    {
        base.SetupSocketMode();
        _onStartupStreamingMode += SetupCameras;
        _onStreamProcessedAsyncNativeArray += SocketStreamProcessedAsync;
        _onStreamProcessed += SocketStreamProcessed;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    protected override void SetupDiskMode()
    {
        base.SetupDiskMode();
        _onStartupStreamingMode += SetupCameras;
        _onStartupStreamingMode += CreateCamerasFolder;
        _onStreamProcessedAsyncNativeArray += DiskStreamProcessedAsync;
        _onStreamProcessed += DiskStreamProcessed;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void CreateCamerasFolder()
    {
        foreach (Camera camera in _cameras)
        {
            string cameraPath = System.IO.Path.Combine(_savePath, camera.name);
            if (!System.IO.Directory.Exists(cameraPath))
                System.IO.Directory.CreateDirectory(cameraPath);
        }
    }

    private void SetupCameras()
    {
        if (_cameraResolution.x == 0 || _cameraResolution.y == 0)
        {
            Debug.LogError("Camera resolution is not set");
            return;
        }

        foreach (var camera in _cameras)
        {
            camera.targetTexture = new RenderTexture((int)_cameraResolution.x, (int)_cameraResolution.y, 24);
            _cameraProcessedFrameCount.Add(0);

            if (_iterateCameras)
                camera.gameObject.SetActive(false);
        }

        if (_iterateCameras)
        {
            if (_howManyCanProcess < 1 || _howManyCanProcess > _cameras.Count)
            {
                Debug.LogWarning("How many cameras can process is not valid, setting it to 1");
                _howManyCanProcess = 1;
            }

            for (int i = 0 ; i < _howManyCanProcess; i++)
                _cameras[i].gameObject.SetActive(true);
        }
    }

    protected override void Stream<T>(T obj)
    {
        Camera camera = obj as Camera;
        if (!_runStreamer)
            return;
        
        int cameraIdx = _cameras.IndexOf(camera);
        if (cameraIdx == -1)
            return;
        
        if (_cameraProcessedFrameCount[cameraIdx] < _processStreamDelay)
        {
            _cameraProcessedFrameCount[cameraIdx]++;
            return;
        }

        if (_iterateCameras)
        {
            camera.gameObject.SetActive(false);
            
            _howManyProcessed++;
            if (_howManyProcessed >= _howManyCanProcess)
            {
                _howManyProcessed = 0;
                int calc = _currentCameraIndex + _howManyCanProcess;

                for (int i = 0; i < _howManyCanProcess; i++)
                    _cameras[(calc + i) % _cameras.Count].gameObject.SetActive(true);
                
                _currentCameraIndex = calc % _cameras.Count;
            }
        }

        _cameraProcessedFrameCount[cameraIdx] = 0;

        if (_processStreamAsynchronously)
            ImageUtils.RenderTexture2NativeArrayAsync(camera.targetTexture, TextureFormat.RGBA32, (bytes) => _onStreamProcessedAsyncNativeArray?.Invoke(bytes, camera));
        else
            ImageUtils.RenderTexture2PNG(camera.targetTexture, TextureFormat.RGBA32, (bytes) => _onStreamProcessed?.Invoke(bytes, camera));

        _onGotCameraFrame?.Invoke();
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        Stream<Camera>(camera);
    }

    protected void DiskStreamProcessed(byte[] png, Camera camera)
    {
        string path = GetFramePath(camera.name);
        ImageUtils.SavePNG2Disk(png, path);
    }

    protected void DiskStreamProcessedAsync(NativeArray<byte> png, Camera camera)
    {
        string path = GetFramePath(camera.name);
        RenderTexture rt = camera.targetTexture;
        ImageUtils.SaveNativeByteArrayAsPNGToDiskAsync(png, rt.graphicsFormat, (uint)rt.width, (uint)rt.height, path);
    }

    protected void SocketStreamProcessed(byte[] png, Camera camera)
    {
        string fileName = camera.name + "/" + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + ".png";
        SocketSendForm form = new SocketSendForm()
        {
            name = fileName,
            fileBytes = png
        };

        byte[] data = form.ToBytes();
        SessionManager.Broadcast(data);
    }

    protected void SocketStreamProcessedAsync(NativeArray<byte> png, Camera camera)
    {
        string fileName = camera.name + "/" + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + ".png";
        RenderTexture rt = camera.targetTexture;
        ImageUtils.SendNativeByteArrayAsPNGToSocketAsync(png, rt.graphicsFormat, (uint)rt.width, (uint)rt.height, SessionManager, fileName);
    }

    public override void OnSocketGotData(object sender, MessageEventArgs e)
    {
        base.OnSocketGotData(sender, e);
        ImageUtils.SaveSocketFormAsPNG2DiskAsync(e.RawData, _savePath);
    }

    private string GetFramePath(string cameraName)
    {
        string cameraPath = System.IO.Path.Combine(_savePath, cameraName);
        if (!System.IO.Directory.Exists(cameraPath))
            return "";
        
        string framePath = System.IO.Path.Combine(cameraPath, $"{System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")}.png");
        return framePath;
    }

    protected override void OnDestroy()
    {
        ImageUtils.StopAllThreads();
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }
}