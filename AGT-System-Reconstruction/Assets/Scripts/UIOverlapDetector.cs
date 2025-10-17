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
        DebugLogger.LogInfo("[UIOverlapDetector] Start() method called - Initializing UIOverlapDetector");
        
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
            DebugLogger.LogInfo("[UIOverlapDetector] Found SimpleInteractionBridge, subscribing to hand events");
            interactionBridge.OnHandInteraction.AddListener(OnHandDataReceived);
            DebugLogger.LogInfo("[UIOverlapDetector] ✅ Successfully subscribed to hand interaction events");
        }
        else
        {
            DebugLogger.LogError("[UIOverlapDetector] ❌ SimpleInteractionBridge not found! UI overlap detection will not work.");
        }
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[UIOverlapDetector] Found {uiElementMap.Count} UI elements to monitor (using Pose data model)");
            if (sendToTouchDesigner && interactionBridge != null)
            {
                DebugLogger.LogInfo($"[UIOverlapDetector] TouchDesigner communication enabled via SimpleInteractionBridge");
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
            DebugLogger.LogInfo($"[UIOverlapDetector] ✅ Hand data received: position=({handData.position.x:F2}, {handData.position.y:F2}), valid={handData.isValid}");
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
        
        // Debug logging for overlap detection
        if (debugMode)
        {
            DebugLogger.LogInfo($"[UIOverlapDetector] Checking overlap: TouchDesigner({handPosition.x:F2},{handPosition.y:F2}) -> Screen({screenPosition.x:F2},{screenPosition.y:F2}), checking {uiElementMap.Count} UI elements");
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
                DebugLogger.LogInfo($"[UIOverlapDetector] Element '{elementId}': screen({screenPosition.x:F2},{screenPosition.y:F2}) -> local({localPoint.x:F2},{localPoint.y:F2}), rect({element.rect.x:F2},{element.rect.y:F2},{element.rect.width:F2},{element.rect.height:F2}), overlapping: {isOverlapping}");
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
                    DebugLogger.LogInfo($"[UIOverlapDetector] ✅ Hand entered UI: {elementId} (color: {config.overlapColor})");
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
                    DebugLogger.LogInfo($"[UIOverlapDetector] ❌ Hand exited UI: {elementId} (color: {config.normalColor})");
                }
            }
        }
    }
    
    bool IsPointInRectTransform(Vector2 screenPoint, RectTransform rectTransform)
    {
        // Find the canvas this UI element belongs to
        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            DebugLogger.LogWarning($"[UIOverlapDetector] No canvas found for {rectTransform.name}");
            return false;
        }
        
        // Use centralized CoordinateConverter for consistent overlap checking
        return CoordinateConverter.IsScreenPointInRectTransform(screenPoint, rectTransform, canvas);
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
                DebugLogger.LogInfo($"[UIOverlapDetector] Added UI element: {elementId}");
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
                DebugLogger.LogInfo($"[UIOverlapDetector] Removed UI element: {elementId}");
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
                DebugLogger.LogInfo($"[UIOverlapDetector] Sent to TouchDesigner via SimpleInteractionBridge: ui_overlap_{elementId} = {messageText}");
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
    /// Convert input position to Unity screen coordinates
    /// Uses centralized CoordinateConverter for consistent conversion
    /// </summary>
    Vector2 ConvertToScreenCoordinates(Vector2 inputPosition)
    {
        // InputPosition is already screen coordinates from SimpleInteractionBridge
        // No additional conversion needed - just return as-is
        return inputPosition;
    }
    
    /// <summary>
    /// Test UI overlap detection with a known position
    /// </summary>
    [ContextMenu("Test UI Overlap Detection")]
    public void TestUIOverlapDetection()
    {
        DebugLogger.LogInfo("[UIOverlapDetector] 🧪 Testing UI overlap detection...");
        
        // Test with center of screen
        Vector2 testPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        DebugLogger.LogInfo($"[UIOverlapDetector] Testing with screen position: ({testPosition.x:F2}, {testPosition.y:F2})");
        
        CheckHandOverlap(testPosition);
        
        DebugLogger.LogInfo($"[UIOverlapDetector] Found {uiElementMap.Count} UI elements to check");
        foreach (var kvp in uiElementMap)
        {
            DebugLogger.LogInfo($"[UIOverlapDetector] UI Element: {kvp.Key} at {kvp.Value.position}");
        }
    }
}
