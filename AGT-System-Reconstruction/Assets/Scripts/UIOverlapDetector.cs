using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    
    void Start()
    {
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
        
        if (debugMode)
        {
            Debug.Log($"[UIOverlapDetector] Found {uiElementMap.Count} UI elements to monitor");
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
    
    public void CheckHandOverlap(Vector2 handScreenPosition)
    {
        if (debugMode)
        {
            Debug.Log($"[UIOverlapDetector] Checking overlap at hand position: ({handScreenPosition.x:F2}, {handScreenPosition.y:F2})");
            Debug.Log($"[UIOverlapDetector] Found {uiElementMap.Count} UI elements to check");
        }
        
        foreach (var kvp in uiElementMap)
        {
            string elementId = kvp.Key;
            RectTransform element = kvp.Value;
            
            bool isOverlapping = IsPointInRectTransform(handScreenPosition, element);
            bool wasOverlapping = overlapStates[elementId];
            
            if (debugMode)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    element, handScreenPosition, null, out localPoint);
                Debug.Log($"[UIOverlapDetector] Element '{elementId}': screen({handScreenPosition.x:F2},{handScreenPosition.y:F2}) -> local({localPoint.x:F2},{localPoint.y:F2}), rect({element.rect.x:F2},{element.rect.y:F2},{element.rect.width:F2},{element.rect.height:F2}), overlapping: {isOverlapping}");
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
}
