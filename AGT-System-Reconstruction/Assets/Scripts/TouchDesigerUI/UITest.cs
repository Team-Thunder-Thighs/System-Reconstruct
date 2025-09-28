using extOSC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UITest : MonoBehaviour
{
    public string addressX = "/rectangle/center/x";
    public string addressY = "/rectangle/center/y";

    private float centerX;
    private float centerY;

    void Start()
    {
       
        // OSCManager.Instance.OSCReceiver.Bind(addressX, OnReceiveX);
        // OSCManager.Instance.OSCReceiver.Bind(addressY, OnReceiveY);
    }

    void OnReceiveX(OSCMessage message)
    {
        Debug.Log("centerx");
        centerX = message.Values[0].FloatValue;
    }

    void OnReceiveY(OSCMessage message)
    {
        centerY = message.Values[0].FloatValue;
    }

    public float GetCenterX()
    {
        return centerX; 
    }

    public float GetCenterY()
    {
        return centerY;
    }

}
