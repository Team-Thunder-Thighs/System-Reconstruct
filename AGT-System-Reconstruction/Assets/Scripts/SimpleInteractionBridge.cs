using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using AgtOscData;
using uOSC;
using BodyTracking.DataModel;
using Pose = BodyTracking.DataModel.Pose;

/// <summary>
/// Lightweight interaction bridge
/// Optional middle ground between direct communication and full interaction layer
/// </summary>
public class SimpleInteractionBridge : MonoBehaviour
{
    [Header("Interaction Events")]
    public UnityEvent<HandInteractionData> OnHandInteraction;
    public UnityEvent<string, bool> OnUIActivated;
    public UnityEvent<string, float> OnGameEvent;
    
    [Header("Settings")]
    [SerializeField] private bool debugMode = true; // Enabled for debugging hand tracking
    
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
        SetupOSCHandlers();
        SetupTouchDesignerHandlers();
        
        // TEMPORARY: Force hand active states for debugging
        handActiveStates[1] = true; // Left hand
        handActiveStates[2] = true; // Right hand
        
        if (debugMode)
        {
            Debug.Log("[SimpleInteractionBridge] Lightweight interaction bridge initialized");
            Debug.Log("[Bridge] TEMPORARY: Hand active states forced to true for debugging");
        }
    }
    
    void SetupTouchDesignerHandlers()
    {
        // Bind TouchDesigner hand tracking messages directly
        if (OSCManager.Instance != null)
        {
            OSCManager.Instance.BindReceiver("/h1:hand_active", OnTouchDesignerHandActive);
            OSCManager.Instance.BindReceiver("/h2:hand_active", OnTouchDesignerHandActive);
            OSCManager.Instance.BindReceiver("/h1:pinch_midpoint:x", OnTouchDesignerHandPosition);
            OSCManager.Instance.BindReceiver("/h1:pinch_midpoint:y", OnTouchDesignerHandPosition);
            OSCManager.Instance.BindReceiver("/h1:pinch_midpoint:z", OnTouchDesignerHandPosition);
            OSCManager.Instance.BindReceiver("/h2:pinch_midpoint:x", OnTouchDesignerHandPosition);
            OSCManager.Instance.BindReceiver("/h2:pinch_midpoint:y", OnTouchDesignerHandPosition);
            OSCManager.Instance.BindReceiver("/h2:pinch_midpoint:z", OnTouchDesignerHandPosition);
            
            // TouchDesigner body pose data (1D array format)
            OSCManager.Instance.BindReceiver("/mediapipe/pose/world", OnTouchDesignerBodyPose);
            
        }
    }
    
    void SetupOSCHandlers()
    {
        // Handle incoming data and convert to Unity events
        OscHelper.BindHandler("/hand/data", OnHandDataReceived, "fingers", "x", "y", "confidence");
        OscHelper.BindHandler("/ui/activated", OnUIActivatedReceived, "element_id", "success");
        OscHelper.BindHandler("/game/event", OnGameEventReceived, "event", "intensity");
        
        // TouchDesigner gesture validation data
        OscHelper.BindHandler("/gesture/validation", OnGestureValidationReceived, 
            "element_id", "actual_fingers", "required_fingers", "overlap_percentage", "is_valid", "hand_x", "hand_y", "confidence");
        
        // TouchDesigner UI activation events
        OscHelper.BindHandler("/ui/activation", OnUIActivationReceived, 
            "element_id", "success", "actual_fingers", "required_fingers");
        
        // TouchDesigner hand tracking data (actual addresses being sent)
        // Note: We'll handle these manually in OnMessageReceived since they have complex addresses
    }
    
    
    void OnHandDataReceived(OSCMessage message)
    {
        var handData = new HandInteractionData(
            message.GetInt("fingers"),
            new Vector2(message.GetFloat("x"), message.GetFloat("y")),
            message.GetFloat("confidence", 1f),
            message.GetFloat("confidence", 1f) > 0.7f
        );
        
        OnHandInteraction?.Invoke(handData);
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] Hand: {handData.fingers} fingers at ({handData.position.x:F2}, {handData.position.y:F2})");
        }
    }
    
    void OnUIActivatedReceived(OSCMessage message)
    {
        string elementId = message.GetString("element_id");
        bool success = message.GetBool("success");
        
        OnUIActivated?.Invoke(elementId, success);
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] UI {elementId} activated: {success}");
        }
    }
    
    void OnGameEventReceived(OSCMessage message)
    {
        string eventName = message.GetString("event");
        float intensity = message.GetFloat("intensity", 1f);
        
        OnGameEvent?.Invoke(eventName, intensity);
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] Game event: {eventName} with intensity {intensity:F2}");
        }
    }
    
    void OnGestureValidationReceived(OSCMessage message)
    {
        string elementId = message.GetString("element_id");
        int actualFingers = message.GetInt("actual_fingers");
        int requiredFingers = message.GetInt("required_fingers");
        float overlapPercentage = message.GetFloat("overlap_percentage");
        bool isValid = message.GetBool("is_valid");
        float handX = message.GetFloat("hand_x");
        float handY = message.GetFloat("hand_y");
        float confidence = message.GetFloat("confidence");
        
        Vector2 handPosition = new Vector2(handX, handY);
        
        // Convert to existing HandInteractionData format
        var handData = new HandInteractionData(actualFingers, handPosition, confidence, isValid);
        OnHandInteraction?.Invoke(handData);
        
        if (debugMode)
        {
            string status = isValid ? "VALID" : "INVALID";
            Debug.Log($"[Bridge] Gesture {status}: {elementId} - " +
                     $"{actualFingers}/{requiredFingers} fingers, overlap: {(overlapPercentage * 100):F1}%");
        }
    }
    
    void OnUIActivationReceived(OSCMessage message)
    {
        string elementId = message.GetString("element_id");
        bool success = message.GetBool("success");
        int actualFingers = message.GetInt("actual_fingers");
        int requiredFingers = message.GetInt("required_fingers");
        
        OnUIActivated?.Invoke(elementId, success);
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] UI {elementId} {(success ? "activated" : "deactivated")} " +
                     $"with {actualFingers}/{requiredFingers} fingers");
        }
    }
    
    // TouchDesigner hand tracking data handlers - now using Pose data model
    private Dictionary<int, Pose> handPoses = new Dictionary<int, Pose>();
    private Dictionary<int, bool> handActiveStates = new Dictionary<int, bool>();
    
    void OnTouchDesignerHandActive(uOSC.Message message)
    {
        // Extract hand ID from address (h1 or h2)
        int handId = message.address.Contains("h1") ? 1 : 2;
        
        // Handle different data types that TouchDesigner might send
        bool isActive = false;
        if (message.values != null && message.values.Length > 0)
        {
            var value = message.values[0];
            if (value is bool boolVal)
            {
                isActive = boolVal;
            }
            else if (value is int intVal)
            {
                isActive = intVal != 0;
            }
            else if (value is float floatVal)
            {
                isActive = floatVal != 0f;
            }
            else if (value is double doubleVal)
            {
                isActive = doubleVal != 0.0;
            }
        }
        
        handActiveStates[handId] = isActive;
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] TouchDesigner Hand {handId} active: {isActive} (value type: {message.values?[0]?.GetType()})");
        }
    }
    
    void OnTouchDesignerHandPosition(uOSC.Message message)
    {
        // Extract hand ID from address (h1 or h2)
        int handId = message.address.Contains("h1") ? 1 : 2;
        bool isLeftHand = handId == 1;
        
        // Handle different data types that TouchDesigner might send
        float value = 0f;
        if (message.values != null && message.values.Length > 0)
        {
            var rawValue = message.values[0];
            if (rawValue is float floatVal)
            {
                value = floatVal;
            }
            else if (rawValue is int intVal)
            {
                value = intVal;
            }
            else if (rawValue is double doubleVal)
            {
                value = (float)doubleVal;
            }
            else if (rawValue is bool boolVal)
            {
                value = boolVal ? 1f : 0f;
            }
        }
        
        // Get or create hand pose
        if (!handPoses.ContainsKey(handId))
        {
            handPoses[handId] = new Pose(true);
        }
        
        Pose currentPose = handPoses[handId];
        
        // Get current wrist position
        BodyLandmark wristLandmark = isLeftHand ? BodyLandmark.LeftWrist : BodyLandmark.RightWrist;
        Vector3 wristPos = currentPose.GetLandmark(wristLandmark);
        
        // Update the appropriate coordinate based on the message address
        if (message.address.Contains(":x"))
        {
            wristPos.x = value;
        }
        else if (message.address.Contains(":y"))
        {
            wristPos.y = value;
        }
        else if (message.address.Contains(":z"))
        {
            wristPos.z = value;
        }
        
        // Update pose with new wrist position
        currentPose.SetLandmark(wristLandmark, wristPos);
        handPoses[handId] = currentPose;
        
        // Check if we have a complete position and hand is active
        if (handActiveStates.ContainsKey(handId) && handActiveStates[handId])
        {
            // Pass TouchDesigner coordinates as-is (they are normalized coordinates, not screen coordinates)
            Vector2 touchDesignerPos = new Vector2(wristPos.x, wristPos.y);
            
            // Create simple hand data (no finger count complexity)
            var handData = new HandInteractionData(
                1, // Simple finger count - always 1 for now
                touchDesignerPos, // TouchDesigner normalized coordinates
                1f, // Default confidence
                true
            );
            
            OnHandInteraction?.Invoke(handData);
            
            if (debugMode)
            {
                Debug.Log($"[Bridge] TouchDesigner Hand {handId} ({wristLandmark}) position: ({wristPos.x:F2}, {wristPos.y:F2}, {wristPos.z:F2}) " +
                         $"(address: {message.address}, value: {value}, type: {message.values?[0]?.GetType()})");
            }
        }
    }
    
    /// <summary>
    /// Handle TouchDesigner body pose data in 1D array format
    /// Expected format: [index_1, x, y, z, index_2, x, y, z, ..., index_n, x, y, z]
    /// Array can have variable length (up to 33 groups = 132 elements)
    /// Filter and process only hand-related landmarks (LeftWrist=15, RightWrist=16)
    /// </summary>
    void OnTouchDesignerBodyPose(uOSC.Message message)
    {
        if (message.values == null || message.values.Length == 0)
        {
            return;
        }
        
        try
        {
            // Parse the 1D array structure: each group is [index, x, y, z] = 4 values
            int landmarkCount = message.values.Length / 4;
            
            if (debugMode)
            {
                Debug.Log($"[Bridge] Body pose received: {landmarkCount} landmarks, {message.values.Length} total values");
            }
            
            // Process each landmark and filter for hand data
            for (int i = 0; i < landmarkCount; i++)
            {
                int baseIndex = i * 4;
                
                // Extract landmark data: [index, x, y, z]
                int landmarkIndex = GetIntFromOSCValue(message.values[baseIndex]);
                float x = GetFloatFromOSCValue(message.values[baseIndex + 1]);
                float y = GetFloatFromOSCValue(message.values[baseIndex + 2]);
                float z = GetFloatFromOSCValue(message.values[baseIndex + 3]);
                
                // Filter for hand landmarks only
                if (landmarkIndex == 15) // LeftWrist
                {
                    if (debugMode)
                    {
                        Debug.Log($"[Bridge] Found LeftWrist (15) at ({x:F3}, {y:F3}, {z:F3})");
                    }
                    ProcessHandLandmark(1, true, x, y, z);
                }
                else if (landmarkIndex == 16) // RightWrist
                {
                    if (debugMode)
                    {
                        Debug.Log($"[Bridge] Found RightWrist (16) at ({x:F3}, {y:F3}, {z:F3})");
                    }
                    ProcessHandLandmark(2, false, x, y, z);
                }
                else if (debugMode && landmarkIndex >= 0 && landmarkIndex <= 32)
                {
                    // Log other landmarks for debugging
                    Debug.Log($"[Bridge] Landmark {landmarkIndex} at ({x:F3}, {y:F3}, {z:F3}) - not a wrist");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Bridge] Error parsing body pose message: {e.Message}");
        }
    }
    
    /// <summary>
    /// Process hand landmark data and trigger hand interaction
    /// </summary>
    void ProcessHandLandmark(int handId, bool isLeftHand, float x, float y, float z)
    {
        // Get or create hand pose
        if (!handPoses.ContainsKey(handId))
        {
            handPoses[handId] = new Pose(true);
        }
        
        Pose currentPose = handPoses[handId];
        BodyLandmark wristLandmark = isLeftHand ? BodyLandmark.LeftWrist : BodyLandmark.RightWrist;
        
        // Update wrist position
        Vector3 wristPos = new Vector3(x, y, z);
        currentPose.SetLandmark(wristLandmark, wristPos);
        handPoses[handId] = currentPose;
        
        // Check if hand is active and trigger interaction
        bool isHandActive = handActiveStates.ContainsKey(handId) && handActiveStates[handId];
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] Hand {handId} ({wristLandmark}) at ({wristPos.x:F2}, {wristPos.y:F2}, {wristPos.z:F2}) - Active: {isHandActive}");
        }
        
        if (isHandActive)
        {
            // Pass TouchDesigner coordinates as-is (they are normalized coordinates, not screen coordinates)
            Vector2 touchDesignerPos = new Vector2(wristPos.x, wristPos.y);
            
            // Create hand interaction data
            var handData = new HandInteractionData(
                1, // Simple finger count
                touchDesignerPos, // TouchDesigner normalized coordinates
                1f, // Default confidence
                true
            );
            
            OnHandInteraction?.Invoke(handData);
            
            if (debugMode)
            {
                Debug.Log($"[Bridge] ✅ Hand interaction triggered for Hand {handId} at TouchDesigner position ({touchDesignerPos.x:F2}, {touchDesignerPos.y:F2})");
            }
        }
        else if (debugMode)
        {
            Debug.Log($"[Bridge] ❌ Hand {handId} not active - no interaction triggered");
        }
    }
    
    /// <summary>
    /// Helper to safely extract int from OSC value
    /// </summary>
    private int GetIntFromOSCValue(object value)
    {
        if (value is int i) return i;
        if (value is float f) return (int)f;
        if (value is double d) return (int)d;
        if (value is bool b) return b ? 1 : 0;
        return 0;
    }
    
    /// <summary>
    /// Helper to safely extract float from OSC value
    /// </summary>
    private float GetFloatFromOSCValue(object value)
    {
        if (value is float f) return f;
        if (value is double d) return (float)d;
        if (value is int i) return i;
        if (value is bool b) return b ? 1f : 0f;
        return 0f;
    }
    
    #region Public API for Sending
    
    public void SendInteractionResult(string elementId, bool success, int actualFingers, int requiredFingers)
    {
        var result = DataTypes.InteractionResult(elementId, success, actualFingers, requiredFingers);
        OscHelper.Send(result, "element_id", "success", "actual_fingers", "required_fingers");
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] Sent interaction result: {elementId} = {success}");
        }
    }
    
    public void SendGameState(int score, int level, int correct, int wrong)
    {
        var gameState = DataTypes.GameState(score, level, correct, wrong);
        OscHelper.Send(gameState, "score", "level", "correct", "wrong");
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] Sent game state: Score {score}, Level {level}");
        }
    }
    
    public void SendAudioEvent(string soundName, float volume = 1f)
    {
        var audioEvent = DataTypes.AudioEvent(soundName, volume);
        OscHelper.Send(audioEvent, "sound", "volume");
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] Sent audio event: {soundName} at volume {volume:F2}");
        }
    }
    
    public void SendCustomEvent(string eventName, float intensity = 1f)
    {
        var customEvent = DataTypes.Trigger(eventName, intensity);
        OscHelper.Send(customEvent, "name", "intensity");
        
        if (debugMode)
        {
            Debug.Log($"[Bridge] Sent custom event: {eventName} with intensity {intensity:F2}");
        }
    }
    
    /// <summary>
    /// Get current pose for a specific hand (using new Pose data model)
    /// </summary>
    /// <param name="handId">1 for left hand, 2 for right hand</param>
    /// <returns>Current pose, or empty pose if hand not tracked</returns>
    public Pose GetHandPose(int handId)
    {
        if (handPoses.ContainsKey(handId))
        {
            return handPoses[handId];
        }
        return new Pose(true);
    }
    
    /// <summary>
    /// Check if a specific hand is currently active
    /// </summary>
    /// <param name="handId">1 for left hand, 2 for right hand</param>
    /// <returns>True if hand is active</returns>
    public bool IsHandActive(int handId)
    {
        return handActiveStates.ContainsKey(handId) && handActiveStates[handId];
    }
    
    #endregion
}
