using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple example showing UI overlap detection with hand tracking
/// This script demonstrates how to detect when a hand overlaps with UI elements
/// </summary>
public class SimpleUIOverlapExample : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SimpleInteractionBridge interactionBridge;
    [SerializeField] private UIOverlapDetector overlapDetector;
    
    [Header("UI Elements")]
    [SerializeField] private Image[] images;
    
    [Header("Settings")]
    [SerializeField] private bool debugMode = true; // Enabled for debugging overlap detection
    
    void Start()
    {
        // Auto-find components if not assigned
        if (interactionBridge == null)
        {
            interactionBridge = FindObjectOfType<SimpleInteractionBridge>();
        }
        
        if (overlapDetector == null)
        {
            overlapDetector = FindObjectOfType<UIOverlapDetector>();
        }
        
        // Subscribe to hand interaction events
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.AddListener(OnHandInteraction);
        }
        
        // Subscribe to UI overlap events
        if (overlapDetector != null)
        {
            overlapDetector.OnUIOverlapEnter.AddListener(OnUIOverlapEnter);
            overlapDetector.OnUIOverlapExit.AddListener(OnUIOverlapExit);
        }
        
        // Use manually created UI elements - no programmatic creation
        
        if (debugMode)
        {
            Debug.Log("[SimpleUIOverlapExample] UI overlap detection system ready!");
        }
    }
    
    void OnHandInteraction(SimpleInteractionBridge.HandInteractionData handData)
    {
        // Convert hand position to screen coordinates
        Vector2 screenPos = handData.position;
        
        // Check if position is normalized (0-1) and convert to screen coordinates
        if (screenPos.x <= 1f && screenPos.y <= 1f && screenPos.x >= 0f && screenPos.y >= 0f)
        {
            screenPos.x *= Screen.width;
            screenPos.y *= Screen.height;
            if (debugMode)
            {
                Debug.Log($"[SimpleUIOverlapExample] Converted normalized position to screen: ({screenPos.x:F2}, {screenPos.y:F2})");
            }
        }
        
        // Check for UI overlap
        if (overlapDetector != null)
        {
            overlapDetector.CheckHandOverlap(screenPos);
        }
        
        if (debugMode)
        {
            Debug.Log($"[SimpleUIOverlapExample] Hand at ({screenPos.x:F2}, {screenPos.y:F2})");
        }
    }
    
    void OnUIOverlapEnter(string elementId)
    {
        if (debugMode)
        {
            Debug.Log($"[SimpleUIOverlapExample] ✅ Hand overlapping UI: {elementId}");
        }
        
        // You can add custom logic here for when hand enters a UI element
        // For example: play sound, show tooltip, etc.
    }
    
    void OnUIOverlapExit(string elementId)
    {
        if (debugMode)
        {
            Debug.Log($"[SimpleUIOverlapExample] ❌ Hand left UI: {elementId}");
        }
        
        // You can add custom logic here for when hand exits a UI element
    }
    
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.RemoveListener(OnHandInteraction);
        }
        
        if (overlapDetector != null)
        {
            overlapDetector.OnUIOverlapEnter.RemoveListener(OnUIOverlapEnter);
            overlapDetector.OnUIOverlapExit.RemoveListener(OnUIOverlapExit);
        }
    }
}
