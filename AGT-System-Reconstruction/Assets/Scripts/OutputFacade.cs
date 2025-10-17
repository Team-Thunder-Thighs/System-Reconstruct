using UnityEngine;
using uOSC;

/// <summary>
/// OutputFacade - The sole entry point for all outgoing messages to TouchDesigner.
/// 
/// This class implements the Facade design pattern to provide a clean, simple API
/// that completely encapsulates the complexity of OSC client communication, message
/// formatting, and error handling for outgoing messages.
/// 
/// Responsibilities:
/// 1. Encapsulate OSC client complexity (only class that knows about OSC library)
/// 2. Provide intent-based API methods (not generic SendMessage)
/// 3. Enforce communication contract with TouchDesigner
/// 4. Handle message formatting and validation
/// 5. Provide resilience logic (retry, error handling)
/// 
/// This acts as an "anti-corruption layer" for outgoing communication - if the
/// output destination changes (e.g., from TouchDesigner/OSC to a different system),
/// only this class needs to be modified. All game logic remains unchanged.
/// </summary>
public class OutputFacade : MonoBehaviour
{
    #region Singleton
    
    public static OutputFacade Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        InitializeFacade();
    }
    
    #endregion
    
    #region Configuration
    
    [Header("OSC Client Configuration")]
    [SerializeField] private string targetIP = "127.0.0.1";
    [SerializeField] private int targetPort = 8000;
    [SerializeField] private int maxQueueSize = 100;
    [SerializeField] private float dataTransmissionInterval = 0f;
    
    [Header("Resilience")]
    [SerializeField] private bool enableRetryLogic = true;
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float retryDelaySeconds = 0.1f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool logOutgoingMessages = false;
    [SerializeField] private bool logConnectionStatus = false;
    
    #endregion
    
    #region Private State
    
    // OSC client (encapsulated - no other class knows about this)
    private uOscClient oscClient;
    
    // Connection status
    private bool isConnected = false;
    private float lastConnectionCheckTime = 0f;
    private const float connectionCheckInterval = 1f;
    
    // Statistics
    private int totalMessagesSent = 0;
    private int totalSendErrors = 0;
    private int totalRetryAttempts = 0;
    
    #endregion
    
    #region Initialization
    
    void InitializeFacade()
    {
        // Initialize OSC client
        InitializeOSCClient();
        
        if (debugMode)
        {
            Debug.Log($"[OutputFacade] Initialized - Target: {targetIP}:{targetPort}");
            Debug.Log($"[OutputFacade] Retry logic: {(enableRetryLogic ? "Enabled" : "Disabled")}");
            Debug.Log($"[OutputFacade] Debug logging: {(logOutgoingMessages ? "Enabled" : "Disabled")}");
        }
    }
    
    void InitializeOSCClient()
    {
        // Find or create OSC client
        oscClient = GetComponent<uOscClient>();
        if (oscClient == null)
        {
            oscClient = gameObject.AddComponent<uOscClient>();
        }
        
        // Configure OSC client
        oscClient.address = targetIP;
        oscClient.port = targetPort;
        oscClient.maxQueueSize = maxQueueSize;
        oscClient.dataTransimissionInterval = dataTransmissionInterval;
        
        // Check initial connection
        CheckConnectionStatus();
        
        if (debugMode)
        {
            Debug.Log($"[OutputFacade] OSC client initialized to {targetIP}:{targetPort}");
        }
    }
    
    #endregion
    
    #region Connection Management (Private - Encapsulated)
    
    /// <summary>
    /// Check OSC client connection status
    /// </summary>
    private void CheckConnectionStatus()
    {
        bool wasConnected = isConnected;
        isConnected = oscClient != null && oscClient.isRunning;
        
        if (wasConnected != isConnected && logConnectionStatus && debugMode)
        {
            string status = isConnected ? "CONNECTED" : "DISCONNECTED";
            Debug.Log($"[OutputFacade] Connection status changed: {status}");
        }
    }
    
    /// <summary>
    /// Update connection status periodically
    /// </summary>
    void Update()
    {
        if (Time.time - lastConnectionCheckTime >= connectionCheckInterval)
        {
            CheckConnectionStatus();
            lastConnectionCheckTime = Time.time;
        }
    }
    
    #endregion
    
    #region Core Send Logic (Private - Encapsulated)
    
    /// <summary>
    /// Core method for sending OSC messages with retry logic
    /// This is the ONLY method that deals with OSC library specifics
    /// </summary>
    private bool SendOSCMessage(string address, params object[] values)
    {
        if (oscClient == null)
        {
            Debug.LogError($"[OutputFacade] OSC client not initialized");
            return false;
        }
        
        if (!isConnected)
        {
            Debug.LogWarning($"[OutputFacade] Cannot send message - not connected to {targetIP}:{targetPort}");
            return false;
        }
        
        try
        {
            oscClient.Send(address, values);
            totalMessagesSent++;
            
            if (logOutgoingMessages && debugMode)
            {
                Debug.Log($"[OutputFacade] Sent: {address} with {values.Length} parameters");
            }
            
            return true;
        }
        catch (System.Exception e)
        {
            totalSendErrors++;
            Debug.LogError($"[OutputFacade] Error sending message {address}: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Send message with retry logic
    /// </summary>
    private bool SendWithRetry(string address, params object[] values)
    {
        if (!enableRetryLogic)
        {
            return SendOSCMessage(address, values);
        }
        
        for (int attempt = 0; attempt < maxRetryAttempts; attempt++)
        {
            if (SendOSCMessage(address, values))
            {
                return true;
            }
            
            if (attempt < maxRetryAttempts - 1)
            {
                totalRetryAttempts++;
                System.Threading.Thread.Sleep((int)(retryDelaySeconds * 1000));
            }
        }
        
        Debug.LogError($"[OutputFacade] Failed to send {address} after {maxRetryAttempts} attempts");
        return false;
    }
    
    #endregion
    
    #region Public API - Intent-Based Methods
    
    /// <summary>
    /// Send hand interaction data to TouchDesigner
    /// </summary>
    /// <param name="fingers">Number of fingers detected</param>
    /// <param name="x">X coordinate (normalized)</param>
    /// <param name="y">Y coordinate (normalized)</param>
    /// <param name="confidence">Confidence level (0-1)</param>
    public void SendHandData(int fingers, float x, float y, float confidence = 1f)
    {
        SendWithRetry("/hand/data", fingers, x, y, confidence);
    }
    
    /// <summary>
    /// Send UI element activation result to TouchDesigner
    /// </summary>
    /// <param name="elementId">ID of the UI element</param>
    /// <param name="success">Whether activation was successful</param>
    /// <param name="actualFingers">Actual number of fingers used</param>
    /// <param name="requiredFingers">Required number of fingers</param>
    public void SendUIElementResult(string elementId, bool success, int actualFingers, int requiredFingers)
    {
        SendWithRetry("/ui/element_result", elementId, success, actualFingers, requiredFingers);
    }
    
    /// <summary>
    /// Send gesture validation result to TouchDesigner
    /// </summary>
    /// <param name="elementId">ID of the element being validated</param>
    /// <param name="isValid">Whether the gesture is valid</param>
    /// <param name="overlapPercentage">Percentage of overlap (0-1)</param>
    /// <param name="handX">Hand X position</param>
    /// <param name="handY">Hand Y position</param>
    /// <param name="confidence">Confidence level</param>
    public void SendGestureValidation(string elementId, bool isValid, float overlapPercentage, 
        float handX, float handY, float confidence)
    {
        SendWithRetry("/gesture/validation", elementId, isValid, overlapPercentage, handX, handY, confidence);
    }
    
    /// <summary>
    /// Send game state update to TouchDesigner
    /// </summary>
    /// <param name="score">Current score</param>
    /// <param name="level">Current level</param>
    /// <param name="correct">Number of correct interactions</param>
    /// <param name="wrong">Number of incorrect interactions</param>
    public void SendGameState(int score, int level, int correct, int wrong)
    {
        SendWithRetry("/game/state", score, level, correct, wrong);
    }
    
    /// <summary>
    /// Send audio event to TouchDesigner
    /// </summary>
    /// <param name="soundName">Name of the sound to play</param>
    /// <param name="volume">Volume level (0-1)</param>
    public void SendAudioEvent(string soundName, float volume = 1f)
    {
        SendWithRetry("/audio/event", soundName, volume);
    }
    
    /// <summary>
    /// Send custom trigger event to TouchDesigner
    /// </summary>
    /// <param name="triggerName">Name of the trigger</param>
    /// <param name="intensity">Intensity level (0-1)</param>
    public void SendCustomTrigger(string triggerName, float intensity = 1f)
    {
        SendWithRetry("/project1/oscin1", triggerName, intensity);
    }
    
    /// <summary>
    /// Send interaction state change to TouchDesigner
    /// </summary>
    /// <param name="interactionType">Type of interaction</param>
    /// <param name="isActive">Whether interaction is active</param>
    public void SendInteractionState(string interactionType, bool isActive)
    {
        SendWithRetry("/interaction/state", interactionType, isActive);
    }
    
    /// <summary>
    /// Send interaction update trigger to TouchDesigner
    /// </summary>
    public void SendInteractionUpdate()
    {
        SendWithRetry("/interaction/update");
    }
    
    #endregion
    
    #region Public API - Status and Control
    
    /// <summary>
    /// Check if OutputFacade is connected to TouchDesigner
    /// </summary>
    /// <returns>True if connected</returns>
    public bool IsConnected()
    {
        CheckConnectionStatus();
        return isConnected;
    }
    
    /// <summary>
    /// Get connection statistics
    /// </summary>
    /// <returns>Output statistics</returns>
    public OutputStatistics GetStatistics()
    {
        return new OutputStatistics
        {
            totalMessagesSent = totalMessagesSent,
            totalSendErrors = totalSendErrors,
            totalRetryAttempts = totalRetryAttempts,
            isConnected = isConnected,
            targetAddress = $"{targetIP}:{targetPort}"
        };
    }
    
    /// <summary>
    /// Reset statistics
    /// </summary>
    public void ResetStatistics()
    {
        totalMessagesSent = 0;
        totalSendErrors = 0;
        totalRetryAttempts = 0;
        
        if (debugMode)
        {
            Debug.Log("[OutputFacade] Statistics reset");
        }
    }
    
    /// <summary>
    /// Force reconnection to TouchDesigner
    /// </summary>
    public void Reconnect()
    {
        if (oscClient != null)
        {
            // Force reconnection by recreating the client
            DestroyImmediate(oscClient);
            InitializeOSCClient();
            
            if (debugMode)
            {
                Debug.Log("[OutputFacade] Forced reconnection");
            }
        }
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    #endregion
    
    #region Debug Visualization
    
    void OnGUI()
    {
        if (!debugMode || !Application.isPlaying) return;
        
        GUILayout.BeginArea(new Rect(10, 220, 400, 200));
        GUILayout.Box("Output Facade Status");
        
        GUILayout.Label($"Target: {targetIP}:{targetPort}");
        GUILayout.Label($"Connected: {isConnected}");
        GUILayout.Label($"Messages Sent: {totalMessagesSent}");
        GUILayout.Label($"Send Errors: {totalSendErrors}");
        GUILayout.Label($"Retry Attempts: {totalRetryAttempts}");
        
        if (GUILayout.Button("Reconnect"))
        {
            Reconnect();
        }
        
        if (GUILayout.Button("Reset Stats"))
        {
            ResetStatistics();
        }
        
        GUILayout.EndArea();
    }
    
    #endregion
}

/// <summary>
/// Statistics about output message sending
/// </summary>
[System.Serializable]
public struct OutputStatistics
{
    public int totalMessagesSent;
    public int totalSendErrors;
    public int totalRetryAttempts;
    public bool isConnected;
    public string targetAddress;
    
    public override string ToString()
    {
        return $"OutputStats: Sent={totalMessagesSent}, Errors={totalSendErrors}, " +
               $"Retries={totalRetryAttempts}, Connected={isConnected}, Target={targetAddress}";
    }
}
