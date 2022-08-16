using System;
using UnityEngine;
using UnityEngine.Events;
using WebSocketSharp;

public class Client : MonoBehaviour
{
    private WebSocket _ws;
    [SerializeField] private int _port = 4649;
    [SerializeField] private string _host = "localhost";
    [SerializeField] private string _service = "Image";
    private string _address { get { return "ws://" + _host + ":" + _port + "/" + _service; } }

    private void Start()
    {
        InitClient();
    }

    private void InitClient()
    {
        // create a new WebSocket and connect to the server
        _ws = new WebSocket(_address);
        _ws.Connect();

        // subscribe to the events
        _ws.OnOpen += OnOpen;
        _ws.OnMessage += OnMessage;
        _ws.OnError += OnError;
        _ws.OnClose += OnClose;
    }

    private void OnMessage(object sender, MessageEventArgs e)
    {
        if (e.IsBinary)
        {
            Debug.Log("Client got: " + e.RawData.Length);
            ImageUtils.SaveArray2PNG2DiskAsync(e.RawData, RenderTextureStreamer.SavePath);
        }
    }

    private void OnOpen(object sender, EventArgs e)
    {
        Debug.Log("Client connected to " + _address);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Debug.Log("Client error: " + e.Message);
    }

    private void OnClose(object sender, CloseEventArgs e)
    {
        Debug.Log("Client closed with reason: " + e.Reason);
    }

    public void SendData(byte[] data)
    {
        _ws.Send(data);
    }
    
    private void OnDestroy()
    {
        _ws.Close();
    }
}