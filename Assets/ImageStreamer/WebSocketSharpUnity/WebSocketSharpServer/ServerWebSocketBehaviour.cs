using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

public class ServerWebSocketBehaviour : WebSocketBehavior
{
    protected override void OnOpen()
    {
        Debug.Log("Server connected");
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        Debug.Log("Server got: " + e.RawData.Length);
    }

    protected override void OnError(ErrorEventArgs e)
    {
        Debug.Log("Server error: " + e.Message);
    }

    protected override void OnClose(CloseEventArgs e)
    {
        Debug.Log("Server closed with reason: " + e.Reason);
    }
}
