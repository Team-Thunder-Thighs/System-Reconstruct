using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using extOSC;
using UnityEngine.UI;

public class OSC_Grab : MonoBehaviour
{
    public string grabAddress = "/grabbing";
    [SerializeField] private GameObject closedHand;
    [SerializeField] private GameObject openHand;
    private bool holdingLastFrame = false;
    private bool grabbing = false;

    // Start is called before the first frame update
    void Start()
    {
        // OSCManager.Instance.OSCReceiver.Bind(grabAddress, OnReceiveGrabbing);
    }

    void OnReceiveGrabbing(OSCMessage message)
    {
        if (message.Values[0].IntValue > 0)
        {
            grabbing = true;
        } else
        {
            grabbing = false;

        }
            
    }

    // Update is called once per frame
    void Update()
    {
        if (holdingLastFrame && !grabbing)
        {
            openHand.SetActive(true);
            closedHand.SetActive(false);
        } else if(!holdingLastFrame && grabbing)
        {
            closedHand.SetActive(true);
            openHand.SetActive(false);
        }
        holdingLastFrame = grabbing;
    }
}
