using UnityEngine;
using System.Collections.Generic;
using System.Text;
using uOSC;

/// <summary>
/// OSC Message Analyzer - Test tool to receive and analyze OSC messages from TouchDesigner
/// 
/// This tool helps you understand:
/// - What OSC addresses are being sent
/// - What data types are in each message
/// - How many values are in each message
/// - The actual data values
/// - Message frequency and statistics
/// 
/// Network Configuration:
/// - Address: 127.0.0.1 (localhost)
/// - Port: 3333
/// </summary>
public class OSCMessageAnalyzer : MonoBehaviour
{
    [Header("Network Configuration")]
    [SerializeField] private int receivePort = 3333;
    [SerializeField] private string networkAddress = "127.0.0.1";
    
    [Header("Display Settings")]
    [SerializeField] private bool showOnScreenLog = true;
    [SerializeField] private int maxLogEntries = 20;
    [SerializeField] private bool showDetailedAnalysis = true;
    [SerializeField] private bool autoScrollLog = true;
    
    [Header("Filtering")]
    [SerializeField] private bool filterByAddress = false;
    [SerializeField] private string addressFilter = "/body";
    
    [Header("Analysis Options")]
    [SerializeField] private bool analyzeDataTypes = true;
    [SerializeField] private bool analyzeValueRanges = true;
    [SerializeField] private bool trackMessageFrequency = true;
    [SerializeField] private bool groupSimilarMessages = true;
    
    // OSC Server
    private uOscServer oscServer;
    
    // Message tracking
    private List<MessageLog> messageLog = new List<MessageLog>();
    private Dictionary<string, MessageStatistics> messageStats = new Dictionary<string, MessageStatistics>();
    
    // Display
    private Vector2 scrollPosition = Vector2.zero;
    private GUIStyle logStyle;
    private GUIStyle headerStyle;
    private GUIStyle statsStyle;
    private bool stylesInitialized = false;
    
    // Statistics
    private int totalMessagesReceived = 0;
    private float sessionStartTime;
    
    void Start()
    {
        sessionStartTime = Time.time;
        InitializeOSCServer();
    }
    
    void InitializeOSCServer()
    {
        // Find or create OSC server
        oscServer = GetComponent<uOscServer>();
        if (oscServer == null)
        {
            oscServer = gameObject.AddComponent<uOscServer>();
        }
        
        // Configure server
        oscServer.port = receivePort;
        
        // Subscribe to all messages
        oscServer.onDataReceived.AddListener(OnMessageReceived);
        
        Debug.Log($"[OSCAnalyzer] 🎯 Started listening on {networkAddress}:{receivePort}");
        Debug.Log($"[OSCAnalyzer] Waiting for OSC messages from TouchDesigner...");
    }
    
    void OnMessageReceived(Message message)
    {
        totalMessagesReceived++;
        
        // Filter by address if enabled
        if (filterByAddress && !message.address.Contains(addressFilter))
        {
            return;
        }
        
        // Analyze message
        MessageLog log = AnalyzeMessage(message);
        
        // Add to log
        messageLog.Add(log);
        if (messageLog.Count > maxLogEntries * 2)
        {
            messageLog.RemoveAt(0); // Remove oldest to prevent memory issues
        }
        
        // Update statistics
        UpdateStatistics(message, log);
        
        // Print to console
        PrintMessageToConsole(log);
    }
    
    MessageLog AnalyzeMessage(Message message)
    {
        MessageLog log = new MessageLog();
        log.timestamp = Time.time;
        log.address = message.address;
        log.valueCount = message.values?.Length ?? 0;
        
        // Analyze values
        if (message.values != null)
        {
            log.values = new List<ValueInfo>();
            
            for (int i = 0; i < message.values.Length; i++)
            {
                object value = message.values[i];
                ValueInfo info = new ValueInfo();
                info.index = i;
                info.value = value;
                info.typeName = value?.GetType().Name ?? "null";
                
                // Analyze type and value
                if (value is float f)
                {
                    info.floatValue = f;
                    info.isNumeric = true;
                }
                else if (value is double d)
                {
                    info.floatValue = (float)d;
                    info.isNumeric = true;
                }
                else if (value is int intVal)
                {
                    info.floatValue = intVal;
                    info.isNumeric = true;
                }
                else if (value is bool b)
                {
                    info.boolValue = b;
                    info.isNumeric = false;
                }
                else if (value is string s)
                {
                    info.stringValue = s;
                    info.isNumeric = false;
                }
                
                log.values.Add(info);
            }
        }
        
        return log;
    }
    
    void UpdateStatistics(Message message, MessageLog log)
    {
        if (!messageStats.ContainsKey(message.address))
        {
            messageStats[message.address] = new MessageStatistics();
            messageStats[message.address].address = message.address;
        }
        
        MessageStatistics stats = messageStats[message.address];
        stats.count++;
        stats.lastReceivedTime = Time.time;
        
        if (stats.firstReceivedTime == 0)
        {
            stats.firstReceivedTime = Time.time;
        }
        
        // Update value count stats
        if (log.valueCount > 0)
        {
            if (!stats.valueCountHistogram.ContainsKey(log.valueCount))
            {
                stats.valueCountHistogram[log.valueCount] = 0;
            }
            stats.valueCountHistogram[log.valueCount]++;
        }
        
        // Track value ranges
        if (analyzeValueRanges && log.values != null)
        {
            foreach (var valueInfo in log.values)
            {
                if (valueInfo.isNumeric)
                {
                    if (valueInfo.floatValue < stats.minValue || stats.count == 1)
                    {
                        stats.minValue = valueInfo.floatValue;
                    }
                    if (valueInfo.floatValue > stats.maxValue || stats.count == 1)
                    {
                        stats.maxValue = valueInfo.floatValue;
                    }
                }
            }
        }
    }
    
    void PrintMessageToConsole(MessageLog log)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"\n[OSCAnalyzer] 📨 Message #{totalMessagesReceived} at {log.timestamp:F2}s");
        sb.AppendLine($"  Address: {log.address}");
        sb.AppendLine($"  Value Count: {log.valueCount}");
        
        if (log.values != null && log.values.Count > 0)
        {
            sb.AppendLine("  Values:");
            
            for (int i = 0; i < log.values.Count; i++)
            {
                ValueInfo info = log.values[i];
                sb.Append($"    [{i}] {info.typeName}: ");
                
                if (info.isNumeric)
                {
                    sb.AppendLine($"{info.floatValue:F4}");
                }
                else if (info.value is bool)
                {
                    sb.AppendLine($"{info.boolValue}");
                }
                else if (info.value is string)
                {
                    sb.AppendLine($"\"{info.stringValue}\"");
                }
                else
                {
                    sb.AppendLine($"{info.value}");
                }
            }
            
            // Show value structure analysis
            if (showDetailedAnalysis)
            {
                // Detect 99 floats structure (33 landmarks × 3 coordinates)
                if (log.valueCount == 99)
                {
                    sb.AppendLine("\n  📊 Detected 99 floats - Possible body pose data (33 landmarks × 3):");
                    sb.AppendLine("  Structure breakdown:");
                    for (int i = 0; i < 33; i++)
                    {
                        int baseIdx = i * 3;
                        if (baseIdx + 2 < log.values.Count)
                        {
                            float x = log.values[baseIdx].floatValue;
                            float y = log.values[baseIdx + 1].floatValue;
                            float z = log.values[baseIdx + 2].floatValue;
                            sb.AppendLine($"    Landmark {i:D2}: ({x:F3}, {y:F3}, {z:F3})");
                        }
                    }
                }
                // Detect 1D array structure (variable length, each group is [index, x, y, z])
                else if (log.valueCount % 4 == 0 && log.valueCount >= 4)
                {
                    int landmarkCount = log.valueCount / 4;
                    sb.AppendLine($"\n  📊 Detected 1D array - Possible body pose data ({landmarkCount} landmarks × 4 values):");
                    sb.AppendLine("  Structure breakdown:");
                    for (int i = 0; i < landmarkCount; i++)
                    {
                        int baseIdx = i * 4;
                        if (baseIdx + 3 < log.values.Count)
                        {
                            int index = (int)log.values[baseIdx].floatValue;
                            float x = log.values[baseIdx + 1].floatValue;
                            float y = log.values[baseIdx + 2].floatValue;
                            float z = log.values[baseIdx + 3].floatValue;
                            sb.AppendLine($"    [{i}] Index {index:D2}: ({x:F3}, {y:F3}, {z:F3})");
                        }
                    }
                }
            }
        }
        
        Debug.Log(sb.ToString());
    }
    
    void InitializeStyles()
    {
        if (stylesInitialized) return;
        
        logStyle = new GUIStyle(GUI.skin.box);
        logStyle.alignment = TextAnchor.UpperLeft;
        logStyle.fontSize = 11;
        logStyle.wordWrap = false;
        logStyle.normal.textColor = Color.white;
        
        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 14;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = Color.yellow;
        
        statsStyle = new GUIStyle(GUI.skin.label);
        statsStyle.fontSize = 11;
        statsStyle.normal.textColor = Color.cyan;
        
        stylesInitialized = true;
    }
    
    void OnGUI()
    {
        if (!showOnScreenLog) return;
        
        InitializeStyles();
        
        // Main panel
        float panelWidth = Screen.width * 0.95f;
        float panelHeight = Screen.height * 0.9f;
        float panelX = Screen.width * 0.025f;
        float panelY = Screen.height * 0.05f;
        
        GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight));
        
        // Header
        GUILayout.Label("OSC Message Analyzer", headerStyle);
        GUILayout.Label($"Listening on {networkAddress}:{receivePort}", statsStyle);
        GUILayout.Label($"Total Messages: {totalMessagesReceived} | Unique Addresses: {messageStats.Count} | Session Time: {(Time.time - sessionStartTime):F1}s", statsStyle);
        
        GUILayout.Space(10);
        
        // Controls
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Log"))
        {
            messageLog.Clear();
        }
        if (GUILayout.Button("Clear Statistics"))
        {
            messageStats.Clear();
            totalMessagesReceived = 0;
            sessionStartTime = Time.time;
        }
        if (GUILayout.Button(autoScrollLog ? "Auto-Scroll: ON" : "Auto-Scroll: OFF"))
        {
            autoScrollLog = !autoScrollLog;
        }
        GUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // Tabs
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Message Log", GUILayout.Width(120)))
        {
            // Switch to log view (already default)
        }
        if (GUILayout.Button("Statistics", GUILayout.Width(120)))
        {
            // Switch to stats view
        }
        GUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // Message log with scroll
        scrollPosition = GUILayout.BeginScrollView(
            autoScrollLog ? new Vector2(0, float.MaxValue) : scrollPosition,
            GUILayout.Height(panelHeight - 150)
        );
        
        // Display recent messages
        int startIdx = Mathf.Max(0, messageLog.Count - maxLogEntries);
        for (int i = startIdx; i < messageLog.Count; i++)
        {
            DrawMessageLog(messageLog[i]);
        }
        
        GUILayout.EndScrollView();
        
        // Statistics summary at bottom
        DrawStatisticsSummary();
        
        GUILayout.EndArea();
    }
    
    void DrawMessageLog(MessageLog log)
    {
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"[{log.timestamp:F2}s] {log.address}", headerStyle);
        GUILayout.Label($"Values: {log.valueCount}", statsStyle);
        
        if (log.values != null && log.values.Count > 0)
        {
            // Show first few values as preview
            int previewCount = Mathf.Min(10, log.values.Count);
            StringBuilder preview = new StringBuilder();
            preview.Append("Preview: ");
            
            for (int i = 0; i < previewCount; i++)
            {
                ValueInfo info = log.values[i];
                if (info.isNumeric)
                {
                    preview.Append($"{info.floatValue:F2}");
                }
                else
                {
                    preview.Append(info.value?.ToString() ?? "null");
                }
                
                if (i < previewCount - 1)
                {
                    preview.Append(", ");
                }
            }
            
            if (log.values.Count > previewCount)
            {
                preview.Append($" ... (+{log.values.Count - previewCount} more)");
            }
            
            GUILayout.Label(preview.ToString());
            
            // Type analysis
            if (analyzeDataTypes)
            {
                var typeCount = new Dictionary<string, int>();
                foreach (var v in log.values)
                {
                    if (!typeCount.ContainsKey(v.typeName))
                        typeCount[v.typeName] = 0;
                    typeCount[v.typeName]++;
                }
                
                StringBuilder types = new StringBuilder("Types: ");
                foreach (var kvp in typeCount)
                {
                    types.Append($"{kvp.Key}×{kvp.Value} ");
                }
                GUILayout.Label(types.ToString(), statsStyle);
            }
        }
        
        GUILayout.EndVertical();
        GUILayout.Space(5);
    }
    
    void DrawStatisticsSummary()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Address Statistics:", headerStyle);
        
        foreach (var kvp in messageStats)
        {
            MessageStatistics stats = kvp.Value;
            float frequency = stats.count / (Time.time - stats.firstReceivedTime);
            GUILayout.Label($"{stats.address}: {stats.count} msgs @ {frequency:F1} Hz | Range: [{stats.minValue:F2}, {stats.maxValue:F2}]", statsStyle);
        }
        
        GUILayout.EndVertical();
    }
    
    void OnDestroy()
    {
        if (oscServer != null)
        {
            oscServer.onDataReceived.RemoveListener(OnMessageReceived);
        }
    }
    
    // Data structures
    [System.Serializable]
    public class MessageLog
    {
        public float timestamp;
        public string address;
        public int valueCount;
        public List<ValueInfo> values;
    }
    
    [System.Serializable]
    public class ValueInfo
    {
        public int index;
        public object value;
        public string typeName;
        public bool isNumeric;
        public float floatValue;
        public bool boolValue;
        public string stringValue;
    }
    
    [System.Serializable]
    public class MessageStatistics
    {
        public string address;
        public int count;
        public float firstReceivedTime;
        public float lastReceivedTime;
        public Dictionary<int, int> valueCountHistogram = new Dictionary<int, int>();
        public float minValue = float.MaxValue;
        public float maxValue = float.MinValue;
    }
}

