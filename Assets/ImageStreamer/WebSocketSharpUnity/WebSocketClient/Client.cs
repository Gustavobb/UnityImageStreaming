using System;
using UnityEngine;
using WebSocketSharp;

public class Client : MonoBehaviour
{
    private WebSocket _ws;
    [SerializeField] private int _port = 4649;
    [SerializeField] private string _host = "localhost";
    [SerializeField] private string _service = "Laputa";
    [SerializeField] private Texture2D _texture;
    private string _address { get { return "ws://" + _host + ":" + _port + "/" + _service; } }

    private byte[] _bytes;

    private void Start()
    {
        _bytes = ImageConversion.EncodeArrayToPNG(_texture.GetRawTextureData(), _texture.graphicsFormat, (uint)_texture.width, (uint)_texture.height);
        InitClient();
    }

    private void InitClient()
    {
        _ws = new WebSocket(_address);
        _ws.Connect();
        _ws.OnMessage += (sender, e) => Debug.Log(_service + " says: " + e.Data);
        Console.ReadKey(true);
        Debug.Log("Client connected to " + _address);
    }

    private void OnDestroy()
    {
        _ws.Close ();
    }

    private void Update()
    {
        if(_ws == null)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            print("Sending... " + _bytes.Length);
            _ws.Send(_bytes);
        }
    }
}