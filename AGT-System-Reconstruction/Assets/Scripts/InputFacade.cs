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
    
    // Landmark accumulation (for incremental updates)
    private Dictionary<int, LandmarkData> landmarkBuffer = new Dictionary<int, LandmarkData>();
    private float lastLandmarkReceivedTime = 0f;
    private float poseCompletionTimeout = 0.1f; // 100ms to receive all landmarks
    
    // Data validity tracking
    private bool hasReceivedData = false;
    private float lastDataReceivedTime = 0f;
    
    // Statistics
    private int totalPacketsReceived = 0;
    private int totalParseErrors = 0;
    private int totalLandmarksReceived = 0;
    
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
            Debug.Log($"[InputFacade] Initialized - Listening on port {receivePort} for address '{expectedAddress}'");
            Debug.Log($"[InputFacade] Coordinate transformation: {(enableCoordinateTransformation ? "Enabled" : "Disabled")}");
            Debug.Log($"[InputFacade] Data caching: {(enableDataCaching ? "Enabled" : "Disabled")}");
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
            Debug.Log($"[InputFacade] OSC server initialized on port {receivePort}");
        }
    }
    
    #endregion
    
    #region OSC Message Handling (Private - Encapsulated)
    
    /// <summary>
    /// Private method - handles raw OSC messages
    /// This is the ONLY method that deals with OSC library specifics
    /// 
    /// NEW: Handles incremental landmark updates (one landmark per message)
    /// Message format: [index(int), x(float), y(float), z(float), visibility(float)]
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
            // Parse single landmark from message
            LandmarkData landmark = ParseLandmarkFromMessage(message);
            
            // Add to buffer
            landmarkBuffer[landmark.index] = landmark;
            lastLandmarkReceivedTime = Time.time;
            totalLandmarksReceived++;
            totalPacketsReceived++;
            
            if (logDataReceived && debugMode)
            {
                Debug.Log($"[InputFacade] Landmark #{landmark.index} received: ({landmark.position.x:F3}, {landmark.position.y:F3}, {landmark.position.z:F3}) vis:{landmark.visibility:F2}");
            }
            
            // Check if we have enough landmarks to build a pose
            if (landmarkBuffer.Count >= 20) // At least 20 landmarks for a valid pose
            {
                BuildPoseFromLandmarks();
            }
        }
        catch (System.Exception e)
        {
            totalParseErrors++;
            Debug.LogError($"[InputFacade] Error parsing OSC message: {e.Message}");
        }
    }
    
    /// <summary>
    /// Parse a single landmark from TouchDesigner message
    /// Format: [index(int), x(float), y(float), z(float), visibility(float)]
    /// </summary>
    private LandmarkData ParseLandmarkFromMessage(uOSC.Message message)
    {
        // Validate message has correct number of values
        if (message.values == null || message.values.Length < 5)
        {
            throw new System.Exception($"Invalid OSC message: expected 5 values (index, x, y, z, visibility), got {message.values?.Length ?? 0}");
        }
        
        LandmarkData landmark = new LandmarkData();
        
        // [0] = Landmark index (int)
        landmark.index = GetIntFromOSCValue(message.values[0]);
        
        // [1] = X coordinate (float)
        landmark.position.x = GetFloatFromOSCValue(message.values[1]);
        
        // [2] = Y coordinate (float)
        landmark.position.y = GetFloatFromOSCValue(message.values[2]);
        
        // [3] = Z coordinate (float)
        landmark.position.z = GetFloatFromOSCValue(message.values[3]);
        
        // [4] = Visibility (float, 0-1)
        landmark.visibility = GetFloatFromOSCValue(message.values[4]);
        
        return landmark;
    }
    
    /// <summary>
    /// Build complete Pose from accumulated landmarks
    /// </summary>
    private void BuildPoseFromLandmarks()
    {
        Pose pose = new Pose(true);
        
        // Copy all landmarks from buffer to pose
        foreach (var kvp in landmarkBuffer)
        {
            int index = kvp.Key;
            LandmarkData landmark = kvp.Value;
            
            // Validate index is within range
            if (index >= 0 && index < BodyTracking.DataModel.BodyLandmarkExtensions.TotalLandmarks)
            {
                BodyTracking.DataModel.BodyLandmark bodyLandmark = BodyTracking.DataModel.BodyLandmarkExtensions.FromIndex(index);
                
                // Apply coordinate transformation if enabled
                Vector3 position = landmark.position;
                if (enableCoordinateTransformation)
                {
                    position = Vector3.Scale(position, coordinateScale) + coordinateOffset;
                }
                
                pose.SetLandmark(bodyLandmark, position);
            }
        }
        
        // Update current pose
        currentPose = pose;
        
        // Cache for resilience
        if (enableDataCaching)
        {
            cachedPose.CopyFrom(currentPose);
        }
        
        // Update tracking
        hasReceivedData = true;
        lastDataReceivedTime = Time.time;
        
        if (debugMode)
        {
            Debug.Log($"[InputFacade] ✅ Pose built from {landmarkBuffer.Count} landmarks");
        }
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
        
        Debug.LogWarning($"[InputFacade] Unexpected OSC value type for int: {value?.GetType()}, defaulting to 0");
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
        
        Debug.LogWarning($"[InputFacade] Unexpected OSC value type: {value?.GetType()}, defaulting to 0");
        return 0f;
    }
    
    #endregion
    
    #region Data Structures
    
    /// <summary>
    /// Stores a single landmark with visibility data
    /// </summary>
    private struct LandmarkData
    {
        public int index;
        public Vector3 position;
        public float visibility;
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
                    Debug.LogWarning($"[InputFacade] Data timeout ({timeSinceLastData:F2}s), using cached pose");
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
            Debug.Log("[InputFacade] Reset complete");
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
        GUILayout.Label($"Has Data: {hasReceivedData}");
        GUILayout.Label($"Valid: {HasValidData()}");
        GUILayout.Label($"Stale: {IsDataStale()}");
        GUILayout.Label($"Time Since Update: {GetTimeSinceLastUpdate():F2}s");
        GUILayout.Label($"Packets Received: {totalPacketsReceived}");
        GUILayout.Label($"Parse Errors: {totalParseErrors}");
        GUILayout.Label($"Current Pose: {currentPose}");
        
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

