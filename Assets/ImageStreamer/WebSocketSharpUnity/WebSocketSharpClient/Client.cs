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

    public delegate void OnMessageCallback(object sender, MessageEventArgs e);
    public event OnMessageCallback onMessageCallback;

    public delegate void OnErrorCallback(object sender, ErrorEventArgs e);
    public event OnErrorCallback onErrorCallback;

    public delegate void OnOpenCallback(object sender, EventArgs e);
    public event OnOpenCallback onOpenCallback;

    public delegate void OnCloseCallback(object sender, CloseEventArgs e);
    public event OnCloseCallback onCloseCallback;

    public void InitClient()
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
            onMessageCallback?.Invoke(sender, e);
        }
    }

    private void OnOpen(object sender, EventArgs e)
    {
        Debug.Log("Client connected to " + _address);
        onOpenCallback?.Invoke(sender, e);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Debug.Log("Client error: " + e.Message);
        onErrorCallback?.Invoke(sender, e);
    }

    private void OnClose(object sender, CloseEventArgs e)
    {
        Debug.Log("Client closed with reason: " + e.Reason);
        onCloseCallback?.Invoke(sender, e);
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