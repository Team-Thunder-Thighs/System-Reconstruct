using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using uOSC;

public class GameManager : MonoBehaviour
{

    [SerializeField] bool grab_interaction = false;
    [SerializeField] bool reach_interaction = false;
    [SerializeField] bool headturn_interaction = false;

    // Track previous states to detect changes
    private bool previousGrabInteraction = false;
    private bool previousReachInteraction = false;
    private bool previousHeadturnInteraction = false;


    public GameObject grab_interaction_ui;
    public GameObject reach_interaction_ui;
    public GameObject heart_ui;

    // RectTransform references for overlap detection
    private RectTransform grabUIRectTransform;
    private RectTransform heartUIRectTransform;

    enum InteractionState
    {
        Grab,
        Reach,
        Headturn
    }

    private InteractionState currentInteractionState = InteractionState.Grab;

    void Start()
    {
        grab_interaction_ui.SetActive(true);
        reach_interaction_ui.SetActive(true);
        
        // Get RectTransform components
        grabUIRectTransform = grab_interaction_ui.GetComponent<RectTransform>();
        heartUIRectTransform = heart_ui.GetComponent<RectTransform>();

        var server = GetComponent<uOscServer>();
        server.onDataReceived.AddListener(onDataReceived);

    }

    private void onDataReceived(Message arg0)
    {
        throw new NotImplementedException();
    }

    void Update()
    {
        var client = GetComponent<uOscClient>();

        // Check for overlap between grab UI and heart UI
        if (currentInteractionState == InteractionState.Grab && CheckUIOverlap())
        {
            TransitionToReachState();
        }

        if(currentInteractionState == InteractionState.Grab)
        {
            grab_interaction_ui.SetActive(true);
            grab_interaction = true;
            
        }
        else if(currentInteractionState == InteractionState.Reach)
        {
            reach_interaction_ui.SetActive(false);
            reach_interaction = true;

           
        }
        

        // Only send data if state has changed
        if (grab_interaction != previousGrabInteraction)
        {
            client.Send("/OSC/interaction", "grab_interaction", grab_interaction);
            previousGrabInteraction = grab_interaction;
        }
        
        if (reach_interaction != previousReachInteraction)
        {
            client.Send("/OSC/interaction", "reach_interaction", reach_interaction);
            previousReachInteraction = reach_interaction;
        }
        
        if (headturn_interaction != previousHeadturnInteraction)
        {
            client.Send("/OSC/interaction", "headturn_interaction", headturn_interaction);
            previousHeadturnInteraction = headturn_interaction;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            // sound triggered
        }
        if (Input.GetKey(KeyCode.UpArrow))
        {
            // sound triggered
        }

    }

    private bool CheckUIOverlap()
    {
        if (grabUIRectTransform == null || heartUIRectTransform == null)
            return false;

        // Get the screen space rectangles of both UI elements
        Vector3[] grabCorners = new Vector3[4];
        Vector3[] heartCorners = new Vector3[4];
        
        grabUIRectTransform.GetWorldCorners(grabCorners);
        heartUIRectTransform.GetWorldCorners(heartCorners);

        // Convert to screen space
        Camera uiCamera = Camera.main; // Assuming main camera is used for UI
        if (uiCamera == null)
            return false;

        // Convert world corners to screen space
        for (int i = 0; i < 4; i++)
        {
            grabCorners[i] = uiCamera.WorldToScreenPoint(grabCorners[i]);
            heartCorners[i] = uiCamera.WorldToScreenPoint(heartCorners[i]);
        }

        // Create rectangles from corners
        Rect grabRect = new Rect(
            Mathf.Min(grabCorners[0].x, grabCorners[1].x, grabCorners[2].x, grabCorners[3].x),
            Mathf.Min(grabCorners[0].y, grabCorners[1].y, grabCorners[2].y, grabCorners[3].y),
            Mathf.Max(grabCorners[0].x, grabCorners[1].x, grabCorners[2].x, grabCorners[3].x) - Mathf.Min(grabCorners[0].x, grabCorners[1].x, grabCorners[2].x, grabCorners[3].x),
            Mathf.Max(grabCorners[0].y, grabCorners[1].y, grabCorners[2].y, grabCorners[3].y) - Mathf.Min(grabCorners[0].y, grabCorners[1].y, grabCorners[2].y, grabCorners[3].y)
        );

        Rect heartRect = new Rect(
            Mathf.Min(heartCorners[0].x, heartCorners[1].x, heartCorners[2].x, heartCorners[3].x),
            Mathf.Min(heartCorners[0].y, heartCorners[1].y, heartCorners[2].y, heartCorners[3].y),
            Mathf.Max(heartCorners[0].x, heartCorners[1].x, heartCorners[2].x, heartCorners[3].x) - Mathf.Min(heartCorners[0].x, heartCorners[1].x, heartCorners[2].x, heartCorners[3].x),
            Mathf.Max(heartCorners[0].y, heartCorners[1].y, heartCorners[2].y, heartCorners[3].y) - Mathf.Min(heartCorners[0].y, heartCorners[1].y, heartCorners[2].y, heartCorners[3].y)
        );

        // Check if rectangles overlap
        return grabRect.Overlaps(heartRect);
    }

    private void TransitionToReachState()
    {
        var client = GetComponent<uOscClient>();
        client.Send("/OSC/interaction", "update");
        currentInteractionState = InteractionState.Reach;
        grab_interaction = false;
        reach_interaction = true;
        
        // Update UI visibility
        grab_interaction_ui.SetActive(false);
        reach_interaction_ui.SetActive(false);
        
        Debug.Log("Transitioned to Reach state due to UI overlap");
    }
/*
    void OnFirstSoundComplete(object in_cookie, AkCallbackType in_type, object in_info)
    {
        if (in_type == AkCallbackType.AK_EndOfEvent)
        {
            // Play second sound when first one ends
            AkUnitySoundEngine.PostEvent("Play_VOGrab", grab_interaction_ui);
        }
    }
*/
    void OnDataReceived(Message message)
    {
        
    }
}
