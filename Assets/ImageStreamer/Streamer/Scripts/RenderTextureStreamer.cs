using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class RenderTextureStreamer : MonoBehaviour
{
    public enum StreamingMode
    {
        DISK, 
        SOCKET,
        NONE
    }

    [Header("Streaming Config")]
    [SerializeField] StreamingMode _streamingMode = StreamingMode.DISK;
    [SerializeField] private bool _processFrames = true;
    [SerializeField] private bool _useApplicationPersistentDataPath = true;
    [SerializeField] private List<Camera> _cameras;
    [SerializeField] private UnityEvent _onGotCameraFrame;
    [SerializeField] private Vector2 _cameraResolution;

    [Header("Disk Streaming Config")]
    [SerializeField] private string _frameSaveFolder = "StreamedFrames1";

    [Header("Socket Streaming Config")]
    [SerializeField] private Server _server;

    [Header("Optimization Config")]
    [SerializeField] private int _processFrameDelay = 0;
    [SerializeField] private bool _processFramesAsynchronously = true;
    [SerializeField] private bool _iterateCameras = false;

    private int _currentCameraIndex = 0;
    private static string _savePath;
    private List<int> _cameraProcessedFrameCount = new List<int>();

    private delegate void OnStartupStreamingMode();
    private OnStartupStreamingMode _onStartupStreamingMode;

    private delegate void OnFrameProcessed(byte[] data, Camera camera);
    private OnFrameProcessed _onFrameProcessed;

    private delegate void OnFrameProcessedAsync(byte[] data, Camera camera);
    private OnFrameProcessedAsync _onFrameProcessedAsync;

    public static string SavePath { get => _savePath + "/"; }

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (_useApplicationPersistentDataPath)
            _savePath = System.IO.Path.Combine(Application.persistentDataPath, _frameSaveFolder);
        else
            _savePath = _frameSaveFolder;

        switch (_streamingMode)
        {
            case StreamingMode.DISK:
                _onStartupStreamingMode += SetupCameras;
                _onStartupStreamingMode += CreateFrameFolder;
                _onFrameProcessedAsync += DiskFrameProcessedAsync;
                _onFrameProcessed += DiskFrameProcessed;
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                break;
            case StreamingMode.SOCKET:
            _onStartupStreamingMode += SetupCameras;
                _onStartupStreamingMode += _server.InitServer;
                _onFrameProcessedAsync += SocketFrameProcessedAsync;
                _onFrameProcessed += SocketFrameProcessed;
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                break;
            case StreamingMode.NONE:
                break;
            default:
                break;
        }

        _onStartupStreamingMode?.Invoke();
    }

    private void CreateFrameFolder()
    {
        if (!System.IO.Directory.Exists(_savePath))
            System.IO.Directory.CreateDirectory(_savePath);
        
        foreach (Camera camera in _cameras)
        {
            string cameraPath = System.IO.Path.Combine(_savePath, camera.name);
            if (!System.IO.Directory.Exists(cameraPath))
                System.IO.Directory.CreateDirectory(cameraPath);
        }

        Debug.Log("Created folder: " + _savePath);
    }

    private void DeleteFrameFolder()
    {
        if (System.IO.Directory.Exists(_savePath))
            System.IO.Directory.Delete(_savePath, true);
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
            _cameras[_currentCameraIndex].gameObject.SetActive(true);
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!_processFrames)
            return;

        int cameraIdx = _cameras.IndexOf(camera);
        if (cameraIdx == -1)
            return;
            
        if (_iterateCameras)
        {
            if (_currentCameraIndex != cameraIdx)
                return;

            _cameras[_currentCameraIndex].gameObject.SetActive(false);
            _currentCameraIndex++;
            
            if (_currentCameraIndex >= _cameras.Count)
                _currentCameraIndex = 0;
            
            _cameras[_currentCameraIndex].gameObject.SetActive(true);
        }

        if (_cameraProcessedFrameCount[cameraIdx] < _processFrameDelay)
        {
            _cameraProcessedFrameCount[cameraIdx]++;
            return;
        }

        _cameraProcessedFrameCount[cameraIdx] = 0;

        if (_processFramesAsynchronously)
            ImageUtils.RenderTexture2ArrayAsync(camera.targetTexture, TextureFormat.RGBA32, (bytes) => _onFrameProcessedAsync?.Invoke(bytes, camera));
        else 
            ImageUtils.RenderTexture2PNG(camera.targetTexture, TextureFormat.RGBA32, (bytes) => _onFrameProcessed?.Invoke(bytes, camera));

        _onGotCameraFrame?.Invoke();
    }

    private void DiskFrameProcessedAsync(byte[] png, Camera camera)
    {
        string path = GetFramePath(camera.name);
        RenderTexture rt = camera.targetTexture;
        ImageUtils.SaveArrayAsPNGToDiskAsync(png, rt.graphicsFormat, (uint)rt.width, (uint)rt.height, path);
    }

    private void DiskFrameProcessed(byte[] png, Camera camera)
    {
        string path = GetFramePath(camera.name);
        ImageUtils.SavePNG2Disk(png, path);
    }

    private void SocketFrameProcessedAsync(byte[] png, Camera camera)
    {
        string fileName = camera.name + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + ".png";
        RenderTexture rt = camera.targetTexture;
        ImageUtils.SendArrayAsPNGToSocketAsync(png, rt.graphicsFormat, (uint)rt.width, (uint)rt.height, _server, fileName, "/Image");
    }

    private void SocketFrameProcessed(byte[] png, Camera camera)
    {
        string fileName = camera.name + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + ".png";
        SocketSendForm form = new SocketSendForm()
        {
            name = fileName,
            fileBytes = png
        };

        byte[] data = form.ToBytes();
        _server.SendPNG(data, "/Image");
    }

    private string GetFramePath(string cameraName)
    {
        string cameraPath = System.IO.Path.Combine(_savePath, cameraName);
        if (!System.IO.Directory.Exists(cameraPath))
            return "";
        
        string framePath = System.IO.Path.Combine(cameraPath, $"{System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")}.png");
        return framePath;
    }

    private void OnDestroy()
    {
        ImageUtils.StopThreads();
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }
}

[System.Serializable]
public class SocketSendForm
{
    public string name;
    public byte[] fileBytes;

    public byte[] ToBytes()
    {
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        byte[] data = new byte[nameBytes.Length + fileBytes.Length + sizeof(int)];
        System.Buffer.BlockCopy(nameBytes, 0, data, 0, nameBytes.Length);
        System.Buffer.BlockCopy(fileBytes, 0, data, nameBytes.Length, fileBytes.Length);
        System.Buffer.BlockCopy(System.BitConverter.GetBytes(nameBytes.Length), 0, data, nameBytes.Length + fileBytes.Length, sizeof(int));
        return data;
    }

    public void FromBytes(byte[] data)
    {
        int nameLength = System.BitConverter.ToInt32(data, data.Length - sizeof(int));
        name = System.Text.Encoding.UTF8.GetString(data, 0, nameLength);
        fileBytes = new byte[data.Length - nameLength - sizeof(int)];
        System.Buffer.BlockCopy(data, nameLength, fileBytes, 0, fileBytes.Length);
    }
}