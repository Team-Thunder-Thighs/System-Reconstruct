using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using AgtOscData;
using BodyTracking.DataModel;
using Pose = BodyTracking.DataModel.Pose;

/// <summary>
/// Game Logic Layer - Converts pose data into game events
/// 
/// This class sits between InputFacade (raw pose data) and game systems (events).
/// It processes body tracking data and converts it into meaningful game interactions.
/// 
/// Responsibilities:
/// 1. Process pose data from InputFacade
/// 2. Extract hand interaction information
/// 3. Convert coordinates for game systems
/// 4. Broadcast Unity Events for game logic
/// 5. Send feedback to external systems
/// 
/// This layer is completely decoupled from OSC communication - InputFacade handles all that.
/// </summary>
public class SimpleInteractionBridge : MonoBehaviour
{
    [Header("Interaction Events")]
    public UnityEvent<HandInteractionData> OnHandInteraction;
    public UnityEvent<string, bool> OnUIActivated;
    public UnityEvent<string, float> OnGameEvent;
    
    [Header("Settings")]
    [SerializeField] private bool debugMode = true;
    
    [Header("Hand Tracking")]
    [SerializeField] public float handConfidenceThreshold = 0.5f; // Lowered from 0.7f
    [SerializeField] public bool enableHandTracking = true;
    [SerializeField] public float poseChangeThreshold = 0.01f; // Lowered from 0.01f
    
    // Internal state
    private Pose lastProcessedPose;
    
    // data structures for events
    [System.Serializable]
    public struct HandInteractionData
    {
        public int fingers;
        public Vector2 position;
        public float confidence;
        public bool isValid;
        
        public HandInteractionData(int fingers, Vector2 pos, float confidence, bool valid)
        {
            this.fingers = fingers;
            this.position = pos;
            this.confidence = confidence;
            this.isValid = valid;
        }
    }
    
    void Start()
    {
        // Initialize pose data
        lastProcessedPose = new Pose(true);
        
        // Subscribe to InputFacade events
        SubscribeToInputEvents();
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[SimpleInteractionBridge] Event-driven game logic layer initialized");
            DebugLogger.LogInfo("[Bridge] Subscribed to InputFacade pose data events");
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        UnsubscribeFromInputEvents();
    }
    
    /// <summary>
    /// Subscribe to InputFacade events for pose data updates
    /// </summary>
    void SubscribeToInputEvents()
    {
        if (InputFacade.Instance != null)
        {
            InputFacade.Instance.OnPoseDataReceived += OnPoseDataReceived;
        }
        else
        {
            // If InputFacade isn't ready yet, try again later
            StartCoroutine(RetrySubscribeToInputEvents());
        }
    }
    
    /// <summary>
    /// Retry subscribing to InputFacade events if it wasn't ready initially
    /// </summary>
    System.Collections.IEnumerator RetrySubscribeToInputEvents()
    {
        while (InputFacade.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        InputFacade.Instance.OnPoseDataReceived += OnPoseDataReceived;
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[Bridge] Successfully subscribed to InputFacade events after retry");
        }
    }
    
    /// <summary>
    /// Unsubscribe from InputFacade events
    /// </summary>
    void UnsubscribeFromInputEvents()
    {
        if (InputFacade.Instance != null)
        {
            InputFacade.Instance.OnPoseDataReceived -= OnPoseDataReceived;
        }
    }
    
    /// <summary>
    /// Event handler for new pose data from InputFacade
    /// </summary>
    void OnPoseDataReceived(Pose newPose)
    {
        if (!enableHandTracking) return;
        
        // Check if pose has changed significantly
        if (HasPoseChanged(newPose))
        {
            ProcessHandInteractions(newPose);
            lastProcessedPose.CopyFrom(newPose);
        }
    }
    
    /// <summary>
    /// Check if the pose has changed significantly since last processing
    /// </summary>
    bool HasPoseChanged(Pose currentPose)
    {
        // Get hand tracking points (wrist if available, otherwise shoulder)
        Vector3 leftHandCurrent = GetHandTrackingPoint(currentPose, true);
        Vector3 rightHandCurrent = GetHandTrackingPoint(currentPose, false);
        
        Vector3 leftHandLast = GetHandTrackingPoint(lastProcessedPose, true);
        Vector3 rightHandLast = GetHandTrackingPoint(lastProcessedPose, false);
        
        float leftMovement = Vector3.Distance(leftHandCurrent, leftHandLast);
        float rightMovement = Vector3.Distance(rightHandCurrent, rightHandLast);
        
        return leftMovement > poseChangeThreshold || rightMovement > poseChangeThreshold;
    }
    
    /// <summary>
    /// Get hand tracking point - wrist if available, otherwise shoulder
    /// </summary>
    Vector3 GetHandTrackingPoint(Pose pose, bool isLeft)
    {
        BodyLandmark wristLandmark = isLeft ? BodyLandmark.LeftWrist : BodyLandmark.RightWrist;
        BodyLandmark shoulderLandmark = isLeft ? BodyLandmark.LeftShoulder : BodyLandmark.RightShoulder;
        
        Vector3 wristPos = pose.GetLandmark(wristLandmark);
        if (wristPos != Vector3.zero)
        {
            return wristPos;
        }
        
        // Fallback to shoulder
        Vector3 shoulderPos = pose.GetLandmark(shoulderLandmark);
        if (shoulderPos != Vector3.zero)
        {
            // DebugLogger.LogInfo($"[Bridge] Using {(isLeft ? "Left" : "Right")}Shoulder as fallback for hand tracking");
        }
        
        return shoulderPos;
    }
    
    /// <summary>
    /// Process hand interactions from pose data
    /// </summary>
    void ProcessHandInteractions(Pose pose)
    {
        // Process left hand (using fallback system)
        Vector3 leftHandPos = GetHandTrackingPoint(pose, true);
        if (leftHandPos != Vector3.zero)
        {
            ProcessHandInteraction(pose, BodyLandmark.LeftWrist, true);
        }
        
        // Process right hand (using fallback system)
        Vector3 rightHandPos = GetHandTrackingPoint(pose, false);
        if (rightHandPos != Vector3.zero)
        {
            ProcessHandInteraction(pose, BodyLandmark.RightWrist, false);
        }
    }
    
    /// <summary>
    /// Process individual hand interaction
    /// </summary>
    void ProcessHandInteraction(Pose pose, BodyLandmark wristLandmark, bool isLeftHand)
    {
        // Get hand position using fallback system (wrist if available, otherwise shoulder)
        Vector3 handPosition = GetHandTrackingPoint(pose, isLeftHand);
        
        // Convert 3D world position to 2D screen coordinates for UI interaction
        Vector2 screenPosition = ConvertWorldToScreenPosition(handPosition);
        
        // Determine finger count based on hand pose (simplified)
        int fingerCount = EstimateFingerCount(pose, wristLandmark);
        
        // Calculate confidence based on pose validity
        float confidence = CalculateHandConfidence(pose, wristLandmark);
        
        // Only trigger interaction if confidence is above threshold
        if (confidence >= handConfidenceThreshold)
        {
            var handData = new HandInteractionData(
                fingerCount,
                screenPosition,
                confidence,
                true
            );
            
            OnHandInteraction?.Invoke(handData);
            
            if (debugMode)
            {
                string handName = isLeftHand ? "Left" : "Right";
                // DebugLogger.LogInfo($"[Bridge] {handName} hand interaction: {fingerCount} fingers at ({screenPosition.x:F2}, {screenPosition.y:F2}) - confidence: {confidence:F2}");
            }
        }
    }
    
    /// <summary>
    /// Convert 3D world position to 2D screen coordinates
    /// Uses centralized CoordinateConverter for consistent conversion
    /// </summary>
    Vector2 ConvertWorldToScreenPosition(Vector3 worldPosition)
    {
        // Use centralized coordinate conversion
        return CoordinateConverter.WorldToScreen(worldPosition);
    }
    
    /// <summary>
    /// Estimate finger count based on hand pose (simplified implementation)
    /// </summary>
    int EstimateFingerCount(Pose pose, BodyLandmark wristLandmark)
    {
        // This is a simplified implementation
        // In a real system, you'd analyze finger landmark positions
        // For now, return a default value
        return 1; // Default to 1 finger (pointing gesture)
    }
    
    /// <summary>
    /// Calculate confidence for hand tracking
    /// </summary>
    float CalculateHandConfidence(Pose pose, BodyLandmark wristLandmark)
    {
        // Simple confidence calculation based on pose validity
        if (!pose.IsValid()) return 0f;
        
        Vector3 wristPos = pose.GetLandmark(wristLandmark);
        
        // Check if wrist position is reasonable (not at origin)
        if (wristPos == Vector3.zero) return 0f;
        
        // More lenient confidence calculation
        // Any non-zero position with reasonable magnitude gets high confidence
        float distance = wristPos.magnitude;
        
        // If distance is reasonable (between 0.1 and 2.0), give high confidence
        if (distance >= 0.1f && distance <= 2.0f)
        {
            return 0.9f; // High confidence for reasonable positions
        }
        
        // For other distances, scale confidence
        return Mathf.Clamp01(distance / 1.0f);
    }
    
    #region Public API for Manual Processing
    
    /// <summary>
    /// Manually trigger hand interaction processing
    /// Useful for testing or when you need immediate processing
    /// </summary>
    public void ProcessCurrentPose()
    {
        if (!enableHandTracking || InputFacade.Instance == null || !InputFacade.Instance.HasValidData())
        {
            return;
        }
        
        Pose currentPose = InputFacade.Instance.GetCurrentPose();
        ProcessHandInteractions(currentPose);
        lastProcessedPose.CopyFrom(currentPose);
            
            if (debugMode)
        {
            DebugLogger.LogInfo("[Bridge] Manual pose processing triggered");
        }
    }
    
    /// <summary>
    /// Force processing of a specific pose (for testing)
    /// </summary>
    public void ProcessSpecificPose(Pose pose)
    {
        if (!enableHandTracking) return;
        
        ProcessHandInteractions(pose);
        lastProcessedPose.CopyFrom(pose);
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[Bridge] Specific pose processing triggered");
        }
    }
    
    #endregion
    
    #region Public API for External Events
    
    /// <summary>
    /// Trigger UI activation event (called by external systems)
    /// </summary>
    public void TriggerUIActivation(string elementId, bool success)
    {
        OnUIActivated?.Invoke(elementId, success);
            
            if (debugMode)
        {
            DebugLogger.LogInfo($"[Bridge] UI {elementId} {(success ? "activated" : "deactivated")}");
        }
    }
    
    /// <summary>
    /// Trigger game event (called by external systems)
    /// </summary>
    public void TriggerGameEvent(string eventName, float intensity = 1f)
    {
        OnGameEvent?.Invoke(eventName, intensity);
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[Bridge] Game event: {eventName} with intensity {intensity:F2}");
        }
    }
    
    /// <summary>
    /// Trigger gesture validation event (called by external systems)
    /// </summary>
    public void TriggerGestureValidation(string elementId, int actualFingers, int requiredFingers, 
        float overlapPercentage, bool isValid, Vector2 handPosition, float confidence)
    {
        // Convert to HandInteractionData format
        var handData = new HandInteractionData(actualFingers, handPosition, confidence, isValid);
        OnHandInteraction?.Invoke(handData);
        
        if (debugMode)
        {
            string status = isValid ? "VALID" : "INVALID";
            DebugLogger.LogInfo($"[Bridge] Gesture {status}: {elementId} - " +
                     $"{actualFingers}/{requiredFingers} fingers, overlap: {(overlapPercentage * 100):F1}%");
        }
    }
    
    
    #endregion
    
    #region Public API for Sending Messages
    
    /// <summary>
    /// Send interaction result to external systems
    /// Note: This method is kept for compatibility but now triggers internal events instead of OSC
    /// </summary>
    public void SendInteractionResult(string elementId, bool success, int actualFingers, int requiredFingers)
    {
        // Trigger internal UI activation event
        TriggerUIActivation(elementId, success);
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[Bridge] Interaction result: {elementId} = {success} ({actualFingers}/{requiredFingers} fingers)");
        }
    }
    
    /// <summary>
    /// Send game state to external systems
    /// Note: This method is kept for compatibility but now triggers internal events instead of OSC
    /// </summary>
    public void SendGameState(int score, int level, int correct, int wrong)
    {
        // Trigger internal game event
        TriggerGameEvent("game_state_update", score);
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[Bridge] Game state: Score {score}, Level {level}, Correct: {correct}, Wrong: {wrong}");
        }
    }
    
    /// <summary>
    /// Send audio event to external systems
    /// Note: This method is kept for compatibility but now triggers internal events instead of OSC
    /// </summary>
    public void SendAudioEvent(string soundName, float volume = 1f)
    {
        // Trigger internal game event
        TriggerGameEvent("audio_play", volume);
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[Bridge] Audio event: {soundName} at volume {volume:F2}");
        }
    }
    
    /// <summary>
    /// Send custom event to external systems
    /// Note: This method is kept for compatibility but now triggers internal events instead of OSC
    /// </summary>
    public void SendCustomEvent(string eventName, float intensity = 1f)
    {
        // Trigger internal game event
        TriggerGameEvent(eventName, intensity);
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[Bridge] Custom event: {eventName} with intensity {intensity:F2}");
        }
    }
    
    /// <summary>
    /// Get current pose data from InputFacade
    /// </summary>
    /// <returns>Current pose data, or empty pose if no data available</returns>
    public Pose GetCurrentPose()
    {
        if (InputFacade.Instance != null && InputFacade.Instance.HasValidData())
        {
            return InputFacade.Instance.GetCurrentPose();
        }
        return new Pose(true);
    }
    
    /// <summary>
    /// Get specific landmark from current pose
    /// </summary>
    /// <param name="landmark">The landmark to retrieve</param>
    /// <returns>3D position of the landmark</returns>
    public Vector3 GetLandmark(BodyLandmark landmark)
    {
        if (InputFacade.Instance != null && InputFacade.Instance.HasValidData())
        {
            return InputFacade.Instance.GetLandmark(landmark);
        }
        return Vector3.zero;
    }
    
    /// <summary>
    /// Check if valid pose data is available
    /// </summary>
    /// <returns>True if valid data is available</returns>
    public bool HasValidPoseData()
    {
        return InputFacade.Instance != null && InputFacade.Instance.HasValidData();
    }
    
    /// <summary>
    /// Get hand tracking statistics
    /// </summary>
    /// <returns>Input statistics from InputFacade</returns>
    public InputStatistics GetTrackingStatistics()
    {
        if (InputFacade.Instance != null)
        {
            return InputFacade.Instance.GetStatistics();
        }
        return new InputStatistics();
    }
    
    #endregion
}
