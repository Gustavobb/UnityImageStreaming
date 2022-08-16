// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.Events;
// using UnityEngine.Rendering;
// using UnityEngine.Experimental.Rendering;
// using System.Threading;

// public class Streamer : MonoBehaviour
// {
//     public enum StreamingMode
//     {
//         DISK, 
//         SOCKET
//     }

//     [SerializeField] StreamingMode _streamingMode = StreamingMode.DISK;
//     [SerializeField] private List<Camera> _cameras;
//     [SerializeField] private UnityEvent _onGotCameraFrame;
//     [SerializeField] private Vector2 _cameraResolution;
//     [SerializeField] private string _frameSaveFolder = "StreamedFrames1";
//     [SerializeField] private int _processFrameDelay = 0;
//     [SerializeField] private bool _processFramesAsynchronously = true;
//     [SerializeField] private bool _iterateCameras = false;
//     [SerializeField] private bool _processFrames = true;

//     private int _currentCameraIndex = 0;
//     private string _savePath;
//     private List<Thread> _threadPool = new List<Thread>();
//     private List<int> _cameraProcessedFrameCount = new List<int>();

//     private delegate void OnStartupStreamingMode();
//     private OnStartupStreamingMode _onStartupStreamingMode;

//     private delegate void OnFrameProcessed(FrameDataForm form);
//     private OnFrameProcessed _onFrameProcessed;

//     struct FrameDataForm
//     {
//         private byte[] _frameData;
//         private string _cameraName;
//         private RenderTexture _frameTexture;

//         public byte[] FrameData { get => _frameData; set => _frameData = value; }
//         public string CameraName { get => _cameraName; set => _cameraName = value; }
//         public RenderTexture FrameTexture { get => _frameTexture; set => _frameTexture = value; }
//     }

//     struct SavePNGForm
//     {
//         private byte[] _data;
//         private string _savePath;
//         private GraphicsFormat _format;
//         private uint _width;
//         private uint _height;

//         public byte[] Data { get => _data; set => _data = value; }
//         public string SavePath { get => _savePath; set => _savePath = value; }
//         public GraphicsFormat Format { get => _format; set => _format = value; }
//         public uint Width { get => _width; set => _width = (uint)value; }
//         public uint Height { get => _height; set => _height = (uint)value; }
//     }

//     private void Awake()
//     {
//         Init();
//     }

//     private void Start()
//     {
//         RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
//     }

//     private void Init()
//     {
//         switch (_streamingMode)
//         {
//             case StreamingMode.DISK:
//                 _onStartupStreamingMode += CreateFrameFolder;
//                 _onFrameProcessed += CreateDiskSavePNGForm;
//                 break;
//             case StreamingMode.SOCKET:
//                 break;
//             default:
//                 break;
//         }

//         SetupCameras();
//         _onStartupStreamingMode?.Invoke();
//     }

//     private void CreateFrameFolder()
//     {
//         _savePath = System.IO.Path.Combine(Application.persistentDataPath, _frameSaveFolder);
//         if (!System.IO.Directory.Exists(_savePath))
//             System.IO.Directory.CreateDirectory(_savePath);
        
//         foreach (Camera camera in _cameras)
//         {
//             string cameraPath = System.IO.Path.Combine(_savePath, camera.name);
//             if (!System.IO.Directory.Exists(cameraPath))
//                 System.IO.Directory.CreateDirectory(cameraPath);
//         }

//         Debug.Log("Created folder: " + _savePath);
//     }

//     private void DeleteFrameFolder()
//     {
//         if (System.IO.Directory.Exists(_savePath))
//             System.IO.Directory.Delete(_savePath, true);
//     }

//     private void SetupCameras()
//     {
//         foreach (var camera in _cameras)
//         {
//             camera.targetTexture = new RenderTexture((int)_cameraResolution.x, (int)_cameraResolution.y, 24);
//             _cameraProcessedFrameCount.Add(0);

//             if (_iterateCameras)
//                 camera.gameObject.SetActive(false);
//         }

//         if (_iterateCameras)
//             _cameras[_currentCameraIndex].gameObject.SetActive(true);
//     }

//     private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
//     {
//         if (!_processFrames)
//             return;

//         int cameraIdx = _cameras.IndexOf(camera);
//         if (cameraIdx == -1)
//             return;
            
//         if (_iterateCameras)
//         {
//             if (_currentCameraIndex != cameraIdx)
//                 return;

//             _cameras[_currentCameraIndex].gameObject.SetActive(false);
//             _currentCameraIndex++;
            
//             if (_currentCameraIndex >= _cameras.Count)
//                 _currentCameraIndex = 0;
            
//             _cameras[_currentCameraIndex].gameObject.SetActive(true);
//         }

//         if (_cameraProcessedFrameCount[cameraIdx] < _processFrameDelay)
//         {
//             _cameraProcessedFrameCount[cameraIdx]++;
//             return;
//         }

//         _cameraProcessedFrameCount[cameraIdx] = 0;
//         ConvertRenderTexture2PNG(camera);
//         _onGotCameraFrame?.Invoke();
//     }

//     private void ConvertRenderTexture2PNG(Camera camera)
//     {
//         FrameDataForm form = new FrameDataForm()
//         {
//             CameraName = camera.name,
//             FrameTexture = camera.targetTexture
//         };
        
//         if (!_processFramesAsynchronously)
//         {
//             Texture2D texture = RenderTexture2Texture2D(camera.targetTexture);
//             form.FrameData = texture.GetRawTextureData();
//             _onFrameProcessed?.Invoke(form);
//             return;
//         }
        
//         RenderTexture2Texture2DAsync(camera.targetTexture, form);
//     }

//     private Texture2D RenderTexture2Texture2D(RenderTexture renderTexture)
//     {
//         RenderTexture.active = renderTexture;
//         Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
//         texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
//         texture.Apply();
//         RenderTexture.active = null;
//         return texture;
//     }

//     private void RenderTexture2Texture2DAsync(RenderTexture renderTexture, FrameDataForm form)
//     {
//         AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32, req =>
//         {
//             if (req.hasError)
//             {
//                 Debug.LogError("Error while reading render texture.");
//                 return;
//             }

//             if (renderTexture == null)
//                 return;

//             form.FrameData = req.GetData<byte>().ToArray();
//             _onFrameProcessed?.Invoke(form);
//         });
//     }

//     private void CreateDiskSavePNGForm(FrameDataForm form)
//     {
//         string cameraPath = System.IO.Path.Combine(_savePath, form.CameraName);
//         if (!System.IO.Directory.Exists(cameraPath))
//             return;

//         Debug.Log("Saving frame to: " + cameraPath);
//         SavePNGForm pngForm = new SavePNGForm
//         {
//             Data = form.FrameData,
//             SavePath = cameraPath,
//             Format = form.FrameTexture.graphicsFormat,
//             Width = (uint)form.FrameTexture.width,
//             Height = (uint)form.FrameTexture.height
//         };

//         SaveTexture2D2Disk(pngForm);
//     }
    
//     private void SaveTexture2D2Disk(SavePNGForm form)
//     {
//         Thread thread = new Thread(() =>
//         {
//             byte[] png = ImageConversion.EncodeArrayToPNG(form.Data, form.Format, form.Width, form.Height);
//             string framePath = System.IO.Path.Combine(form.SavePath, $"{System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")}.png");
//             System.IO.File.WriteAllBytes(framePath, png);
//         });
        
//         _threadPool.Add(thread);
//         thread.Start();
//     }

//     private void StopThreads()
//     {
//         foreach (var thread in _threadPool)
//             thread.Abort();
//     }

//     private void OnDestroy()
//     {
//         StopThreads();
//         RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
//     }
// }
