using UnityEngine;
using BodyTracking.DataModel;
using uOSC;
using Pose = BodyTracking.DataModel.Pose;

/// <summary>
/// InputFacade - The sole entry point for all external body tracking data.
/// 
/// This class implements the Facade design pattern to provide a clean, simple API
/// that completely encapsulates the complexity of OSC communication, data parsing,
/// coordinate transformations, and error handling.
/// 
/// Responsibilities:
/// 1. Encapsulate OSC complexity (only class that knows about OSC library)
/// 2. Parse and transform raw OSC data into structured Pose objects
/// 3. Implement resilience logic (handle packet loss, maintain cached data)
/// 4. Provide a simple, clean API to the rest of the game
/// 
/// This acts as an "anti-corruption layer" - if the input source changes
/// (e.g., from MediaPipe/OSC to a different tracking system), only this class
/// needs to be modified. All game logic remains unchanged.
/// </summary>
public class InputFacade : MonoBehaviour
{
    #region Singleton
    
    public static InputFacade Instance { get; private set; }
    
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
    
    [Header("OSC Configuration")]
    [SerializeField] private int receivePort = 3333;
    [SerializeField] private string expectedAddress = "/mediapipe/pose/world";
    
    [Header("Data Processing")]
    [SerializeField] private bool enableCoordinateTransformation = true;
    [SerializeField] private Vector3 coordinateScale = Vector3.one;
    [SerializeField] private Vector3 coordinateOffset = Vector3.zero;
    
    [Header("Resilience")]
    [SerializeField] private bool enableDataCaching = true;
    [SerializeField] private float dataTimeoutSeconds = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool logDataReceived = false;
    [SerializeField] private bool logTransformations = false;
    
    #endregion
    
    #region Private State
    
    // OSC server (encapsulated - no other class knows about this)
    private uOSC.uOscServer oscServer;
    
    // Current pose data
    private Pose currentPose;
    private Pose cachedPose;
    
    // Data validity tracking
    private bool hasReceivedData = false;
    private float lastDataReceivedTime = 0f;
    
    // Statistics
    private int totalPacketsReceived = 0;
    private int totalParseErrors = 0;
    
    // Events
    public System.Action<Pose> OnPoseDataReceived;
    
    #endregion
    
    #region Initialization
    
    void InitializeFacade()
    {
        // Initialize pose data structures
        currentPose = new Pose(true);
        cachedPose = new Pose(true);
        
        // Initialize OSC server
        InitializeOSCServer();
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[InputFacade] Initialized - Listening on port {receivePort} for address '{expectedAddress}'");
            DebugLogger.LogInfo($"[InputFacade] Coordinate transformation: {(enableCoordinateTransformation ? "Enabled" : "Disabled")}");
            DebugLogger.LogInfo($"[InputFacade] Data caching: {(enableDataCaching ? "Enabled" : "Disabled")}");
        }
    }
    
    void InitializeOSCServer()
    {
        // Find or create OSC server
        oscServer = GetComponent<uOSC.uOscServer>();
        if (oscServer == null)
        {
            oscServer = gameObject.AddComponent<uOSC.uOscServer>();
        }
        
        // Configure OSC server
        oscServer.port = receivePort;
        
        // Subscribe to OSC messages
        oscServer.onDataReceived.AddListener(OnOSCDataReceived);
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[InputFacade] OSC server initialized on port {receivePort}");
        }
    }
    
    #endregion
    
    #region OSC Message Handling (Private - Encapsulated)
    
    /// <summary>
    /// Private method - handles raw OSC messages
    /// This is the ONLY method that deals with OSC library specifics
    /// </summary>
    private void OnOSCDataReceived(uOSC.Message message)
    {
        // Filter by expected address
        if (message.address != expectedAddress)
        {
            return; // Ignore messages not meant for body pose data
        }
        
        try
        {
            // Parse OSC message into Pose
            Pose receivedPose = ParseOSCMessageToPose(message);
            
            // Apply coordinate transformations if enabled
            if (enableCoordinateTransformation)
            {
                receivedPose = TransformPoseCoordinates(receivedPose);
            }
            
            // Update current pose
            currentPose = receivedPose;
            
            // Cache for resilience
            if (enableDataCaching)
            {
                cachedPose.CopyFrom(currentPose);
            }
            
            // Update tracking
            hasReceivedData = true;
            lastDataReceivedTime = Time.time;
            totalPacketsReceived++;
            
            // Notify subscribers of new pose data
            OnPoseDataReceived?.Invoke(currentPose);
            
            if (logDataReceived && debugMode)
            {
                int landmarkCount = message.values.Length / 4;
                // DebugLogger.LogInfo($"[InputFacade] Pose data received (packet #{totalPacketsReceived}): {landmarkCount} landmarks from {message.values.Length} values");
            }
        }
        catch (System.Exception e)
        {
            totalParseErrors++;
            DebugLogger.LogError($"[InputFacade] Error parsing OSC message: {e.Message}");
        }
    }
    
    /// <summary>
    /// Parse raw OSC message into Pose structure
    /// Expected format: Variable-length 1D array with groups of [index, x, y, z]
    /// Example: [index_0, x, y, z, index_3, x, y, z, index_9, x, y, z]
    /// </summary>
    private Pose ParseOSCMessageToPose(uOSC.Message message)
    {
        if (message.values == null || message.values.Length == 0)
        {
            throw new System.Exception("Invalid OSC message: no values provided");
        }
        
        // Validate that we have at least one complete group (4 values: index, x, y, z)
        if (message.values.Length < 4)
        {
            throw new System.Exception($"Invalid OSC message: need at least 4 values for one landmark group, got {message.values.Length}");
        }
        
        // Validate that the array length is divisible by 4 (each group has 4 values)
        if (message.values.Length % 4 != 0)
        {
            throw new System.Exception($"Invalid OSC message: array length must be divisible by 4 (groups of [index,x,y,z]), got {message.values.Length}");
        }
        
        Pose pose = new Pose(true);
        int landmarkCount = message.values.Length / 4;
        
        if (debugMode)
        {
            // DebugLogger.LogInfo($"[InputFacade] Parsing {landmarkCount} landmarks from {message.values.Length} values");
        }
        
        // Parse each landmark group (4 values per group: index, x, y, z)
        for (int groupIndex = 0; groupIndex < landmarkCount; groupIndex++)
        {
            int baseIndex = groupIndex * 4;
            
            // Extract landmark index
            int landmarkIndex = GetIntFromOSCValue(message.values[baseIndex]);
            
            // Extract x, y, z coordinates
            float x = GetFloatFromOSCValue(message.values[baseIndex + 1]);
            float y = GetFloatFromOSCValue(message.values[baseIndex + 2]);
            float z = GetFloatFromOSCValue(message.values[baseIndex + 3]);
            
            // Validate landmark index is within valid range
            if (landmarkIndex < 0 || landmarkIndex >= BodyLandmarkExtensions.TotalLandmarks)
            {
                DebugLogger.LogWarning($"[InputFacade] Invalid landmark index {landmarkIndex}, skipping (valid range: 0-{BodyLandmarkExtensions.TotalLandmarks - 1})");
                continue;
            }
            
            // Create landmark position
            Vector3 position = new Vector3(x, y, z);
            
            // Set landmark in pose
            BodyLandmark landmark = BodyLandmarkExtensions.FromIndex(landmarkIndex);
            pose.SetLandmark(landmark, position);
            
            if (debugMode)
            {
                // DebugLogger.LogInfo($"[InputFacade] Parsed landmark {landmarkIndex} ({landmark}) at ({x:F3}, {y:F3}, {z:F3})");
            }
        }
        
        return pose;
    }
    
    /// <summary>
    /// Helper to safely extract int from OSC value (handles type variations)
    /// </summary>
    private int GetIntFromOSCValue(object value)
    {
        if (value is int i) return i;
        if (value is float f) return (int)f;
        if (value is double d) return (int)d;
        if (value is bool b) return b ? 1 : 0;
        
        DebugLogger.LogWarning($"[InputFacade] Unexpected OSC value type for int conversion: {value?.GetType()}, defaulting to 0");
        return 0;
    }
    
    /// <summary>
    /// Helper to safely extract float from OSC value (handles type variations)
    /// </summary>
    private float GetFloatFromOSCValue(object value)
    {
        if (value is float f) return f;
        if (value is double d) return (float)d;
        if (value is int i) return i;
        if (value is bool b) return b ? 1f : 0f;
        
        DebugLogger.LogWarning($"[InputFacade] Unexpected OSC value type: {value?.GetType()}, defaulting to 0");
        return 0f;
    }
    
    /// <summary>
    /// Transform pose from external coordinate space to Unity world space
    /// </summary>
    private Pose TransformPoseCoordinates(Pose pose)
    {
        Pose transformedPose = new Pose(true);
        
        for (int i = 0; i < BodyLandmarkExtensions.TotalLandmarks; i++)
        {
            BodyLandmark landmark = BodyLandmarkExtensions.FromIndex(i);
            Vector3 originalPos = pose.GetLandmark(landmark);
            
            // Apply scale and offset
            Vector3 transformedPos = Vector3.Scale(originalPos, coordinateScale) + coordinateOffset;
            
            transformedPose.SetLandmark(landmark, transformedPos);
            
            if (logTransformations && debugMode && i == 0) // Log only first landmark to avoid spam
            {
                DebugLogger.LogInfo($"[InputFacade] Coordinate transform: {originalPos} → {transformedPos}");
            }
        }
        
        return transformedPose;
    }
    
    #endregion
    
    #region Public API - Simple, Clean Interface
    
    /// <summary>
    /// Get the current body pose data.
    /// This is the primary API method that game logic should use.
    /// 
    /// Returns the most recently received pose, or cached data if no recent data
    /// has been received (resilience against packet loss).
    /// </summary>
    /// <returns>Current Pose data</returns>
    public Pose GetCurrentPose()
    {
        // Check if data is stale
        if (enableDataCaching && hasReceivedData)
        {
            float timeSinceLastData = Time.time - lastDataReceivedTime;
            if (timeSinceLastData > dataTimeoutSeconds)
            {
                if (debugMode)
                {
                    DebugLogger.LogWarning($"[InputFacade] Data timeout ({timeSinceLastData:F2}s), using cached pose");
                }
                return cachedPose;
            }
        }
        
        return currentPose;
    }
    
    /// <summary>
    /// Get a specific landmark from the current pose.
    /// Convenience method for quick landmark access.
    /// </summary>
    /// <param name="landmark">The landmark to retrieve</param>
    /// <returns>3D position of the landmark</returns>
    public Vector3 GetLandmark(BodyLandmark landmark)
    {
        return GetCurrentPose().GetLandmark(landmark);
    }
    
    /// <summary>
    /// Check if valid pose data has been received.
    /// </summary>
    /// <returns>True if valid data is available</returns>
    public bool HasValidData()
    {
        if (!hasReceivedData) return false;
        
        // Check if data is too old
        if (enableDataCaching)
        {
            float timeSinceLastData = Time.time - lastDataReceivedTime;
            if (timeSinceLastData > dataTimeoutSeconds)
            {
                return false;
            }
        }
        
        return currentPose.IsValid();
    }
    
    /// <summary>
    /// Check if data is currently stale (no recent updates).
    /// </summary>
    /// <returns>True if data is stale</returns>
    public bool IsDataStale()
    {
        if (!hasReceivedData) return true;
        
        float timeSinceLastData = Time.time - lastDataReceivedTime;
        return timeSinceLastData > dataTimeoutSeconds;
    }
    
    /// <summary>
    /// Get time since last data was received (in seconds).
    /// </summary>
    public float GetTimeSinceLastUpdate()
    {
        if (!hasReceivedData) return float.MaxValue;
        return Time.time - lastDataReceivedTime;
    }
    
    /// <summary>
    /// Get statistics about data reception.
    /// Useful for debugging and monitoring.
    /// </summary>
    public InputStatistics GetStatistics()
    {
        return new InputStatistics
        {
            totalPacketsReceived = totalPacketsReceived,
            totalParseErrors = totalParseErrors,
            hasReceivedData = hasReceivedData,
            timeSinceLastUpdate = GetTimeSinceLastUpdate(),
            isDataStale = IsDataStale()
        };
    }
    
    /// <summary>
    /// Reset the facade (clear cached data, reset statistics).
    /// </summary>
    public void Reset()
    {
        currentPose.Reset();
        cachedPose.Reset();
        hasReceivedData = false;
        lastDataReceivedTime = 0f;
        totalPacketsReceived = 0;
        totalParseErrors = 0;
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[InputFacade] Reset complete");
        }
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    void OnDestroy()
    {
        // Clean up OSC server
        if (oscServer != null)
        {
            oscServer.onDataReceived.RemoveListener(OnOSCDataReceived);
        }
        
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
        
        GUILayout.BeginArea(new Rect(10, 10, 400, 200));
        GUILayout.Box("Input Facade Status");
        
        GUILayout.Label($"Port: {receivePort}");
        GUILayout.Label($"Address: {expectedAddress}");
        GUILayout.Label($"Has Data: {hasReceivedData}");
        GUILayout.Label($"Valid: {HasValidData()}");
        GUILayout.Label($"Stale: {IsDataStale()}");
        GUILayout.Label($"Time Since Update: {GetTimeSinceLastUpdate():F2}s");
        GUILayout.Label($"Packets Received: {totalPacketsReceived}");
        GUILayout.Label($"Parse Errors: {totalParseErrors}");
        
        // Show current pose info
        if (hasReceivedData)
        {
            int validLandmarks = 0;
            for (int i = 0; i < BodyLandmarkExtensions.TotalLandmarks; i++)
            {
                BodyLandmark landmark = BodyLandmarkExtensions.FromIndex(i);
                if (currentPose.GetLandmark(landmark) != Vector3.zero)
                {
                    validLandmarks++;
                }
            }
            GUILayout.Label($"Valid Landmarks: {validLandmarks}/{BodyLandmarkExtensions.TotalLandmarks}");
        }
        
        GUILayout.EndArea();
    }
    
    #endregion
}

/// <summary>
/// Statistics about input data reception
/// </summary>
[System.Serializable]
public struct InputStatistics
{
    public int totalPacketsReceived;
    public int totalParseErrors;
    public bool hasReceivedData;
    public float timeSinceLastUpdate;
    public bool isDataStale;
    
    public override string ToString()
    {
        return $"InputStats: Packets={totalPacketsReceived}, Errors={totalParseErrors}, " +
               $"HasData={hasReceivedData}, TimeSinceUpdate={timeSinceLastUpdate:F2}s, Stale={isDataStale}";
    }
}

