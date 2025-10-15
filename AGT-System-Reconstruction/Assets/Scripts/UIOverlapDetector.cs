using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BodyTracking.DataModel;
using Pose = BodyTracking.DataModel.Pose;

[System.Serializable]
public class UIElementConfig
{
    public string elementName;
    public Color overlapColor = Color.green;
    public Color normalColor = Color.white;
    public AudioClip overlapSound;
}

/// <summary>
/// Simple UI overlap detection system for hand tracking
/// Checks if hand position overlaps with UI elements
/// </summary>
public class UIOverlapDetector : MonoBehaviour
{
    [Header("UI Elements to Check")]
    [SerializeField] private RectTransform[] uiElements;
    
    [Header("Settings")]
    [SerializeField] private bool debugMode = true; // Enabled for debugging overlap detection
    [SerializeField] private Color defaultOverlapColor = Color.green;
    [SerializeField] private Color defaultNormalColor = Color.white;
    
    [Header("Per-Element Configuration")]
    [SerializeField] private UIElementConfig[] elementConfigs;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip defaultOverlapSound;
    
    [Header("Coordinate System")]
    [SerializeField] private bool useNormalizedCoordinates = true; // TouchDesigner sends normalized coords (X: -0.5 to 0.5, Y: -0.9 to -0.3)
    [SerializeField] private bool flipY = false; // Disabled - Y movement is already correct
    
    [Header("TouchDesigner Communication")]
    [SerializeField] private bool sendToTouchDesigner = true;
    [SerializeField] private SimpleInteractionBridge interactionBridge;
    
    [Header("Events")]
    public UnityEngine.Events.UnityEvent<string> OnUIOverlapEnter;
    public UnityEngine.Events.UnityEvent<string> OnUIOverlapExit;
    
    private Dictionary<string, bool> overlapStates = new Dictionary<string, bool>();
    private Dictionary<string, RectTransform> uiElementMap = new Dictionary<string, RectTransform>();
    
    // Hand tracking using Pose data model
    private Pose currentHandPose;
    
    void Start()
    {
        // Initialize Pose data model
        currentHandPose = new Pose(true);
        
        // Auto-find UI elements if not manually assigned
        if (uiElements == null || uiElements.Length == 0)
        {
            FindUIElements();
        }
        
        // Create mapping for easy lookup
        foreach (var element in uiElements)
        {
            if (element != null)
            {
                string elementId = element.name;
                uiElementMap[elementId] = element;
                overlapStates[elementId] = false;
            }
        }
        
        // Auto-find AudioSource if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Auto-find SimpleInteractionBridge if not assigned
        if (interactionBridge == null)
        {
            interactionBridge = FindObjectOfType<SimpleInteractionBridge>();
        }
        
        // Subscribe to hand interaction events
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.AddListener(OnHandDataReceived);
            if (debugMode)
            {
                Debug.Log("[UIOverlapDetector] ✅ Subscribed to hand interaction events");
            }
        }
        else
        {
            Debug.LogError("[UIOverlapDetector] ❌ SimpleInteractionBridge not found! UI overlap detection will not work.");
        }
        
        if (debugMode)
        {
            Debug.Log($"[UIOverlapDetector] Found {uiElementMap.Count} UI elements to monitor (using Pose data model)");
            if (sendToTouchDesigner && interactionBridge != null)
            {
                Debug.Log($"[UIOverlapDetector] TouchDesigner communication enabled via SimpleInteractionBridge");
            }
        }
    }
    
    void FindUIElements()
    {
        // Find all UI elements with Image components
        var images = FindObjectsOfType<Image>();
        
        var allElements = new List<RectTransform>();
        
        foreach (var img in images)
        {
            if (img.raycastTarget) // Only check interactive elements
            {
                allElements.Add(img.rectTransform);
            }
        }
        
        uiElements = allElements.ToArray();
    }
    
    /// <summary>
    /// Handle hand interaction data from SimpleInteractionBridge
    /// </summary>
    void OnHandDataReceived(SimpleInteractionBridge.HandInteractionData handData)
    {
        if (debugMode)
        {
            Debug.Log($"[UIOverlapDetector] ✅ Hand data received: position=({handData.position.x:F2}, {handData.position.y:F2}), valid={handData.isValid}");
        }
        
        // Check for UI overlap
        CheckHandOverlap(handData.position);
    }
    
    public void CheckHandOverlap(Vector2 handPosition)
    {
        // Convert TouchDesigner coordinates to screen coordinates
        Vector2 screenPosition = ConvertToScreenCoordinates(handPosition);
        
        // Update Pose data model with converted hand position
        Vector3 wristPos = new Vector3(screenPosition.x, screenPosition.y, 0f);
        currentHandPose.SetLandmark(BodyLandmark.LeftWrist, wristPos);
        
        if (debugMode)
        {
            Debug.Log($"[UIOverlapDetector] Checking overlap at TouchDesigner position: ({handPosition.x:F2}, {handPosition.y:F2}) -> Screen: ({screenPosition.x:F2}, {screenPosition.y:F2}), " +
                     $"Pose wrist: {currentHandPose.GetLandmark(BodyLandmark.LeftWrist)}");
            Debug.Log($"[UIOverlapDetector] Found {uiElementMap.Count} UI elements to check");
        }
        
        foreach (var kvp in uiElementMap)
        {
            string elementId = kvp.Key;
            RectTransform element = kvp.Value;
            
            bool isOverlapping = IsPointInRectTransform(screenPosition, element);
            bool wasOverlapping = overlapStates[elementId];
            
            if (debugMode)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    element, screenPosition, null, out localPoint);
                Debug.Log($"[UIOverlapDetector] Element '{elementId}': screen({screenPosition.x:F2},{screenPosition.y:F2}) -> local({localPoint.x:F2},{localPoint.y:F2}), rect({element.rect.x:F2},{element.rect.y:F2},{element.rect.width:F2},{element.rect.height:F2}), overlapping: {isOverlapping}");
            }
            
            if (isOverlapping && !wasOverlapping)
            {
                // Enter overlap
                overlapStates[elementId] = true;
                OnUIOverlapEnter?.Invoke(elementId);
                
                // Get element-specific configuration
                UIElementConfig config = GetElementConfig(elementId);
                
                // Visual feedback
                SetElementColor(element, config.overlapColor);
                
                // Audio feedback
                PlayOverlapSound(config.overlapSound);
                
                // Send message to TouchDesigner via SimpleInteractionBridge
                SendOverlapMessageToTouchDesigner(elementId, true);
                
                if (debugMode)
                {
                    Debug.Log($"[UIOverlapDetector] ✅ Hand entered UI: {elementId} (color: {config.overlapColor})");
                }
            }
            else if (!isOverlapping && wasOverlapping)
            {
                // Exit overlap
                overlapStates[elementId] = false;
                OnUIOverlapExit?.Invoke(elementId);
                
                // Get element-specific configuration
                UIElementConfig config = GetElementConfig(elementId);
                
                // Visual feedback
                SetElementColor(element, config.normalColor);
                
                // Send message to TouchDesigner via SimpleInteractionBridge
                SendOverlapMessageToTouchDesigner(elementId, false);
                
                if (debugMode)
                {
                    Debug.Log($"[UIOverlapDetector] ❌ Hand exited UI: {elementId} (color: {config.normalColor})");
                }
            }
        }
    }
    
    bool IsPointInRectTransform(Vector2 screenPoint, RectTransform rectTransform)
    {
        // Convert screen point to local point in the rect transform's coordinate system
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            screenPoint,
            null, // Use the canvas's camera if available
            out localPoint
        );
        
        // Check if the point is within the rect
        return rectTransform.rect.Contains(localPoint);
    }
    
    void SetElementColor(RectTransform element, Color color)
    {
        // Set color on Image component
        var image = element.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }
    }
    
    public bool IsOverlapping(string elementId)
    {
        return overlapStates.ContainsKey(elementId) && overlapStates[elementId];
    }
    
    public void AddUIElement(RectTransform element)
    {
        if (element != null)
        {
            string elementId = element.name;
            uiElementMap[elementId] = element;
            overlapStates[elementId] = false;
            
            if (debugMode)
            {
                Debug.Log($"[UIOverlapDetector] Added UI element: {elementId}");
            }
        }
    }
    
    public void RemoveUIElement(string elementId)
    {
        if (uiElementMap.ContainsKey(elementId))
        {
            uiElementMap.Remove(elementId);
            overlapStates.Remove(elementId);
            
            if (debugMode)
            {
                Debug.Log($"[UIOverlapDetector] Removed UI element: {elementId}");
            }
        }
    }
    
    UIElementConfig GetElementConfig(string elementId)
    {
        // Look for specific configuration for this element
        if (elementConfigs != null)
        {
            foreach (var config in elementConfigs)
            {
                if (config.elementName == elementId)
                {
                    return config;
                }
            }
        }
        
        // Return default configuration if no specific config found
        return new UIElementConfig
        {
            elementName = elementId,
            overlapColor = defaultOverlapColor,
            normalColor = defaultNormalColor,
            overlapSound = defaultOverlapSound
        };
    }
    
    void PlayOverlapSound(AudioClip soundClip)
    {
        if (audioSource != null && soundClip != null)
        {
            audioSource.PlayOneShot(soundClip);
        }
    }
    
    void SendOverlapMessageToTouchDesigner(string elementId, bool isOverlapping)
    {
        if (sendToTouchDesigner && interactionBridge != null)
        {
            string messageText = isOverlapping ? $"Hand overlapping {elementId}" : $"Hand left {elementId}";
            
            // Use SimpleInteractionBridge's SendCustomEvent method
            interactionBridge.SendCustomEvent($"ui_overlap_{elementId}", isOverlapping ? 1f : 0f);
            
            if (debugMode)
            {
                Debug.Log($"[UIOverlapDetector] Sent to TouchDesigner via SimpleInteractionBridge: ui_overlap_{elementId} = {messageText}");
            }
        }
    }
    
    /// <summary>
    /// Get current hand pose (using new Pose data model)
    /// </summary>
    public Pose GetCurrentHandPose()
    {
        return currentHandPose;
    }
    
    /// <summary>
    /// Get specific landmark from current hand pose
    /// </summary>
    public Vector3 GetLandmark(BodyLandmark landmark)
    {
        return currentHandPose.GetLandmark(landmark);
    }
    
    void OnDestroy()
    {
        // Unsubscribe from hand interaction events
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.RemoveListener(OnHandDataReceived);
        }
    }
    
    /// <summary>
    /// Convert TouchDesigner coordinates to Unity screen coordinates
    /// Uses the same conversion logic as HandVisualizer
    /// </summary>
    Vector2 ConvertToScreenCoordinates(Vector2 inputPosition)
    {
        Vector2 screenPosition = inputPosition;
        
        // Convert normalized coordinates to screen coordinates if needed
        if (useNormalizedCoordinates)
        {
            // TouchDesigner sends normalized coordinates in range:
            // X: -0.5 to 0.5 (left to right)
            // Y: -0.9 to -0.3 (top to bottom)
            
            // Convert X from -0.5..0.5 to 0..1, then to screen
            float normalizedX = (inputPosition.x + 0.5f); // -0.5..0.5 -> 0..1
            screenPosition.x = normalizedX * Screen.width;
            
            // Convert Y from -0.9..-0.3 to 0..1, then to screen (SIMPLE INVERT)
            float normalizedY = ((inputPosition.y + 0.9f) / 0.6f); // -0.9..-0.3 -> 0..1
            normalizedY = Mathf.Clamp01(normalizedY); // Ensure 0..1 range
            normalizedY = 1f - normalizedY; // Invert the Y coordinate
            screenPosition.y = normalizedY * Screen.height;
            
            if (flipY)
            {
                screenPosition.y = Screen.height - screenPosition.y;
            }
        }
        
        return screenPosition;
    }
}
