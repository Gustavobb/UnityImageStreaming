using System;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

public class Server : MonoBehaviour
{
    private WebSocketServer _wsServer;
    [SerializeField] private int _port = 4649;
    [SerializeField] private string _host = "localhost";
    private string _address { get { return "ws://" + _host + ":" + _port; } }

    public int Port { get { return _port; } }
    public string Host { get { return _host; } }
    public string Address { get { return _address; } }

    private void Awake()
    {
        InitServer();
    }

    private void InitServer()
    {
        _wsServer = new WebSocketServer(_address);
        _wsServer.AddWebSocketService<Laputa>("/Laputa");
        _wsServer.AddWebSocketService<ImageSocketBehaviour>("/Image");
        _wsServer.Start();
        Console.ReadKey(true);
        Debug.Log("Server started at " + _address);
    }

    private void OnDestroy()
    {
        _wsServer.Stop();
    }
}

public class Laputa : WebSocketBehavior
{
    protected override void OnMessage(MessageEventArgs e)
    {
        Debug.Log("Laputa got: " + e.Data);
        string msg = e.Data == "BALUS"
                ? "Are you kidding?"
                : "I'm not available now.";

        Send(msg);
    }
}