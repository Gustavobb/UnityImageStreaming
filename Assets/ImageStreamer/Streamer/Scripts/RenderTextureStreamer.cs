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
        SOCKET
    }

    [SerializeField] StreamingMode _streamingMode = StreamingMode.DISK;
    [SerializeField] private List<Camera> _cameras;
    [SerializeField] private UnityEvent _onGotCameraFrame;
    [SerializeField] private Vector2 _cameraResolution;
    [SerializeField] private string _frameSaveFolder = "StreamedFrames1";
    [SerializeField] private int _processFrameDelay = 0;
    [SerializeField] private bool _processFramesAsynchronously = true;
    [SerializeField] private bool _iterateCameras = false;
    [SerializeField] private bool _processFrames = true;

    private int _currentCameraIndex = 0;
    private string _savePath;
    private List<int> _cameraProcessedFrameCount = new List<int>();

    private delegate void OnStartupStreamingMode();
    private OnStartupStreamingMode _onStartupStreamingMode;

    private delegate void OnFrameProcessed(byte[] data, Camera camera);
    private OnFrameProcessed _onFrameProcessed;

    private delegate void OnFrameProcessedAsync(byte[] data, Camera camera);
    private OnFrameProcessedAsync _onFrameProcessedAsync;

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void Init()
    {
        switch (_streamingMode)
        {
            case StreamingMode.DISK:
                _onStartupStreamingMode += CreateFrameFolder;
                _onFrameProcessedAsync += DiskFrameProcessedAsync;
                _onFrameProcessed += DiskFrameProcessed;
                break;
            case StreamingMode.SOCKET:
                break;
            default:
                break;
        }

        SetupCameras();
        _onStartupStreamingMode?.Invoke();
    }

    private void CreateFrameFolder()
    {
        _savePath = System.IO.Path.Combine(Application.persistentDataPath, _frameSaveFolder);
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
        ImageUtils.SaveArrayAsPNGToDiskAsync(png, camera.targetTexture.graphicsFormat, (uint)camera.targetTexture.width, (uint)camera.targetTexture.height, path);
    }

    private void DiskFrameProcessed(byte[] png, Camera camera)
    {
        string path = GetFramePath(camera.name);
        ImageUtils.SavePNG2Disk(png, path);
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
