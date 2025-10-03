using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using AgtOscData;
using uOSC;

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
    [SerializeField] private bool debugMode = false; // Disabled to reduce console spam
    
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
        
        if (debugMode)
        {
            Debug.Log("[SimpleInteractionBridge] Lightweight interaction bridge initialized");
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
    
    #region OSC Handlers
    
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
    
    // TouchDesigner hand tracking data handlers
    private Dictionary<int, Vector3> handPositions = new Dictionary<int, Vector3>();
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
        
        // Get or create hand position
        if (!handPositions.ContainsKey(handId))
        {
            handPositions[handId] = Vector3.zero;
        }
        
        Vector3 currentPos = handPositions[handId];
        
        // Update the appropriate coordinate based on the message address
        if (message.address.Contains(":x"))
        {
            currentPos.x = value;
        }
        else if (message.address.Contains(":y"))
        {
            currentPos.y = value;
        }
        else if (message.address.Contains(":z"))
        {
            currentPos.z = value;
        }
        
        handPositions[handId] = currentPos;
        
        // Check if we have a complete position and hand is active
        if (handActiveStates.ContainsKey(handId) && handActiveStates[handId])
        {
            // Convert 3D position to 2D screen coordinates (assuming z is depth)
            Vector2 screenPos = new Vector2(currentPos.x, currentPos.y);
            
            // Create simple hand data (no finger count complexity)
            var handData = new HandInteractionData(
                1, // Simple finger count - always 1 for now
                screenPos,
                1f, // Default confidence
                true
            );
            
            OnHandInteraction?.Invoke(handData);
            
            if (debugMode)
            {
                Debug.Log($"[Bridge] TouchDesigner Hand {handId} position: ({currentPos.x:F2}, {currentPos.y:F2}, {currentPos.z:F2}) " +
                         $"(address: {message.address}, value: {value}, type: {message.values?[0]?.GetType()})");
            }
        }
    }
    
    
    #endregion
    
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
    
    #endregion
}
