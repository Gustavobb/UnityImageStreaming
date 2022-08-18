using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using WebSocketSharp;
using Unity.Collections;
using WebSocketSharp.Server;

public class DataStreamer : MonoBehaviour
{
    public enum StreamingMode
    {
        DISK, 
        SOCKET,
        NONE
    }

    [Header("Streaming Config")]
    [SerializeField] protected StreamingMode _streamingMode = StreamingMode.DISK;
    [SerializeField] protected bool _runStreamer = true;
    [SerializeField] protected bool _useApplicationPersistentDataPath = true;
    [SerializeField] protected bool _initStreamerAutomatically= false;

    [Header("Socket Config")]
    [SerializeField] protected bool _hasToGetStreamFromClient = false;
    [SerializeField] protected string _service = "Service";
    [SerializeField] protected Client _client;
    [SerializeField] protected Server _server;
    
    [Header("Disk Config")]
    [SerializeField] protected string _streamSaveFolder = "Streamed";

    [Header("Optimization Config")]
    [SerializeField] protected bool _processStreamAsynchronously = true;
    [SerializeField] protected int _processStreamDelay = 0;

    protected static string _savePath;

    protected delegate void OnStartupStreamingMode();
    protected OnStartupStreamingMode _onStartupStreamingMode;

    public string Service { get { return "/" + _service; } }
    public WebSocketSessionManager SessionManager;
    public static string SavePath { get => _savePath + "/"; }

    protected virtual void Awake()
    {
        if (_initStreamerAutomatically)
            Init();
    }

    protected virtual void Start()
    {
        if (_initStreamerAutomatically && _hasToGetStreamFromClient)
            _client.InitClient();
    }

    protected virtual void Init()
    {
        _onStartupStreamingMode += CreateStreamFolder;
        switch (_streamingMode)
        {
            case StreamingMode.DISK:
                SetupDiskMode();
                break;
            case StreamingMode.SOCKET:
                _server.InitServer();
                SetupSocketMode();
                break;
            case StreamingMode.NONE:
                break;
            default:
                break;
        }

        if (_hasToGetStreamFromClient)
            _client.onMessageCallback += OnSocketGotData;
        
        _onStartupStreamingMode?.Invoke();
    }

    protected virtual void SetupSocketMode()
    {
        SessionManager = _server.WsServer.WebSocketServices[Service].Sessions;
    }

    protected virtual void SetupDiskMode()
    {
    }

    protected virtual void CreateStreamFolder()
    {
        if (_useApplicationPersistentDataPath)
            _savePath = System.IO.Path.Combine(Application.persistentDataPath, _streamSaveFolder);
        else
            _savePath = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("USERPROFILE"), _streamSaveFolder);

        if (!System.IO.Directory.Exists(_savePath))
            System.IO.Directory.CreateDirectory(_savePath);
        
        Debug.Log("Created folder: " + _savePath);
    }

    protected virtual void DeleteStreamFolder()
    {
        if (System.IO.Directory.Exists(_savePath))
            System.IO.Directory.Delete(_savePath, true);
    }

    protected virtual void Stream<T>(T obj)
    {
        if (!_runStreamer)
            return;
    }

    public virtual void OnSocketGotData(object sender, MessageEventArgs e)
    {
        if (_savePath == null)
            CreateStreamFolder();
    }

    protected virtual void OnDestroy()
    {
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