using System;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

public class ImageSocketBehaviour : WebSocketBehavior
{
    protected override void OnMessage(MessageEventArgs e)
    {
        Debug.Log("ImageSocketBehaviour got: " + e.RawData.Length);
        // string msg = e.Data == "BALUS"
        //         ? "Are you kidding?"
        //         : "I'm not available now.";

        // Send(msg);
    }
}
