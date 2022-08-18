using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using WebSocketSharp;
using Unity.Collections;
using WebSocketSharp.Server;

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
    [SerializeField] private bool _startStreamerAutomatically= false;
    [SerializeField] private List<Camera> _cameras;
    [SerializeField] private UnityEvent _onGotCameraFrame;
    [SerializeField] private Vector2 _cameraResolution;

    [Header("Socket Config")]
    [SerializeField] private bool _hasToGetFrameFromClient = false;
    [SerializeField] private string _service = "Image";
    [SerializeField] private Client _client;
    [SerializeField] private Server _server;
    
    [Header("Disk Config")]
    [SerializeField] private string _frameSaveFolder = "StreamedFrames1";

    [Header("Optimization Config")]
    [SerializeField] private bool _processFramesAsynchronously = true;
    [SerializeField] private bool _iterateCameras = false;
    [SerializeField] private int _howManyCanProcess = 1;
    [SerializeField] private int _processFrameDelay = 0;

    private int _howManyProcessed = 0;
    private int _currentCameraIndex = 0;
    private static string _savePath;
    private List<int> _cameraProcessedFrameCount = new List<int>();

    private delegate void OnStartupStreamingMode();
    private OnStartupStreamingMode _onStartupStreamingMode;

    private delegate void OnFrameProcessed(byte[] data, Camera camera);
    private OnFrameProcessed _onFrameProcessed;

    private delegate void OnFrameProcessedAsync<T>(T data, Camera camera);
    private OnFrameProcessedAsync<byte[]> _onFrameProcessedAsyncByte;
    private OnFrameProcessedAsync<NativeArray<byte>> _onFrameProcessedAsyncNativeArray;
    public string Service { get { return "/" + _service; } }
    public WebSocketSessionManager SessionManager { get { return _server.WsServer.WebSocketServices[Service].Sessions; } }

    public static string SavePath { get => _savePath + "/"; }

    private void Awake()
    {
        if (_startStreamerAutomatically)
            Init();
    }

    void Start()
    {
        if (_startStreamerAutomatically && _hasToGetFrameFromClient)
            _client.InitClient();
    }

    private void Init()
    {
        switch (_streamingMode)
        {
            case StreamingMode.DISK:
                _onStartupStreamingMode += CreateFrameFolder;
                _onStartupStreamingMode += SetupCameras;
                _onStartupStreamingMode += CreateCamerasFolder;
                _onFrameProcessedAsyncNativeArray += DiskFrameProcessedAsync;
                _onFrameProcessed += DiskFrameProcessed;
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                break;
            case StreamingMode.SOCKET:
                _onStartupStreamingMode += CreateFrameFolder;
                _onStartupStreamingMode += SetupCameras;
                _onStartupStreamingMode += _server.InitServer;
                _onFrameProcessedAsyncNativeArray += SocketFrameProcessedAsync;
                _onFrameProcessed += SocketFrameProcessed;
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                break;
            case StreamingMode.NONE:
                break;
            default:
                break;
        }

        if (_hasToGetFrameFromClient)
            _client.onMessageCallback += OnSocketGotData;
        
        if (_iterateCameras && (_howManyCanProcess < 1 || _howManyCanProcess > _cameras.Count))
        {
            Debug.LogWarning("How many cameras can process is not valid, setting it to 1");
            _howManyCanProcess = 1;
        }

        _onStartupStreamingMode?.Invoke();
    }

    private void CreateFrameFolder()
    {
        if (_useApplicationPersistentDataPath)
            _savePath = System.IO.Path.Combine(Application.persistentDataPath, _frameSaveFolder);
        else
            _savePath = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("USERPROFILE"), _frameSaveFolder);

        if (!System.IO.Directory.Exists(_savePath))
            System.IO.Directory.CreateDirectory(_savePath);
        
        Debug.Log("Created folder: " + _savePath);
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
        {
            for (int i = 0 ; i < _howManyCanProcess; i++)
                _cameras[i].gameObject.SetActive(true);
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!_processFrames)
            return;

        int cameraIdx = _cameras.IndexOf(camera);
        if (cameraIdx == -1)
            return;
        
        if (_cameraProcessedFrameCount[cameraIdx] < _processFrameDelay)
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

        if (_processFramesAsynchronously)
            ImageUtils.RenderTexture2NativeArrayAsync(camera.targetTexture, TextureFormat.RGBA32, (bytes) => _onFrameProcessedAsyncNativeArray?.Invoke(bytes, camera));
        else
            ImageUtils.RenderTexture2PNG(camera.targetTexture, TextureFormat.RGBA32, (bytes) => _onFrameProcessed?.Invoke(bytes, camera));

        _onGotCameraFrame?.Invoke();
    }

    private void DiskFrameProcessedAsync(NativeArray<byte> png, Camera camera)
    {
        string path = GetFramePath(camera.name);
        RenderTexture rt = camera.targetTexture;
        ImageUtils.SaveNativeByteArrayAsPNGToDiskAsync(png, rt.graphicsFormat, (uint)rt.width, (uint)rt.height, path);
    }

    private void DiskFrameProcessed(byte[] png, Camera camera)
    {
        string path = GetFramePath(camera.name);
        ImageUtils.SavePNG2Disk(png, path);
    }

    private void SocketFrameProcessedAsync(NativeArray<byte> png, Camera camera)
    {
        string fileName = camera.name + "/" + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + ".png";
        RenderTexture rt = camera.targetTexture;
        ImageUtils.SendNativeByteArrayAsPNGToSocketAsync(png, rt.graphicsFormat, (uint)rt.width, (uint)rt.height, SessionManager, fileName);
    }

    private void SocketFrameProcessed(byte[] png, Camera camera)
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

    public void OnSocketGotData(object sender, MessageEventArgs e)
    {
        if (_savePath == null)
            CreateFrameFolder();

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

    private void OnDestroy()
    {
        ImageUtils.StopAllThreads();
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