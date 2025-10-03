using UnityEngine;
using uOSC;

/// <summary>
/// Debug script to inspect TouchDesigner OSC messages
/// This will help us understand what data types TouchDesigner is sending
/// </summary>
public class TouchDesignerOSCDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugging = true;
    [SerializeField] private bool logAllMessages = false;
    [SerializeField] private bool logHandMessages = true;
    [SerializeField] private bool logMessageTypes = true;
    
    private OSCManager oscManager;
    
    void Start()
    {
        oscManager = FindObjectOfType<OSCManager>();
        
        if (oscManager == null)
        {
            Debug.LogError("[TouchDesignerOSCDebugger] OSCManager not found!");
            return;
        }
        
        // Bind to all TouchDesigner messages we're interested in
        BindDebugHandlers();
        
        Debug.Log("[TouchDesignerOSCDebugger] TouchDesigner OSC debugger initialized");
    }
    
    void BindDebugHandlers()
    {
        if (oscManager == null) return;
        
        // Bind to hand-related messages
        oscManager.BindReceiver("/h1:hand_active", OnDebugMessage);
        oscManager.BindReceiver("/h2:hand_active", OnDebugMessage);
        oscManager.BindReceiver("/h1:hand_velocity", OnDebugMessage);
        oscManager.BindReceiver("/h2:hand_velocity", OnDebugMessage);
        oscManager.BindReceiver("/h1:pinch_midpoint:x", OnDebugMessage);
        oscManager.BindReceiver("/h1:pinch_midpoint:y", OnDebugMessage);
        oscManager.BindReceiver("/h1:pinch_midpoint:z", OnDebugMessage);
        oscManager.BindReceiver("/h1:pinch_midpoint:rotation", OnDebugMessage);
        oscManager.BindReceiver("/h1:pinch_midpoint:distance", OnDebugMessage);
        oscManager.BindReceiver("/h2:pinch_midpoint:x", OnDebugMessage);
        oscManager.BindReceiver("/h2:pinch_midpoint:y", OnDebugMessage);
        oscManager.BindReceiver("/h2:pinch_midpoint:z", OnDebugMessage);
        oscManager.BindReceiver("/h2:pinch_midpoint:rotation", OnDebugMessage);
        oscManager.BindReceiver("/h2:pinch_midpoint:distance", OnDebugMessage);
        oscManager.BindReceiver("/hand_distance", OnDebugMessage);
        oscManager.BindReceiver("/h1:Leftness", OnDebugMessage);
        oscManager.BindReceiver("/h1:Rightness", OnDebugMessage);
        oscManager.BindReceiver("/h2:Leftness", OnDebugMessage);
        oscManager.BindReceiver("/h2:Rightness", OnDebugMessage);
        oscManager.BindReceiver("/_samplerate", OnDebugMessage);
    }
    
    void OnDebugMessage(Message message)
    {
        if (!enableDebugging) return;
        
        if (logAllMessages || (logHandMessages && IsHandRelatedMessage(message.address)))
        {
            string logMessage = $"[TouchDesignerOSCDebugger] {message.address}";
            
            if (message.values != null && message.values.Length > 0)
            {
                logMessage += $" - Values: [";
                for (int i = 0; i < message.values.Length; i++)
                {
                    if (i > 0) logMessage += ", ";
                    logMessage += $"{message.values[i]}";
                    
                    if (logMessageTypes)
                    {
                        logMessage += $"({message.values[i].GetType().Name})";
                    }
                }
                logMessage += "]";
            }
            else
            {
                logMessage += " - No values";
            }
            
            Debug.Log(logMessage);
        }
    }
    
    bool IsHandRelatedMessage(string address)
    {
        return address.Contains("h1") || address.Contains("h2") || address.Contains("hand");
    }
    
    // Public methods for testing
    [ContextMenu("Test Message Types")]
    public void TestMessageTypes()
    {
        Debug.Log("[TouchDesignerOSCDebugger] Testing common OSC data types:");
        
        // Test different data types
        TestDataType(true, "bool");
        TestDataType(1, "int");
        TestDataType(1.0f, "float");
        TestDataType(1.0, "double");
        TestDataType("test", "string");
    }
    
    void TestDataType(object value, string typeName)
    {
        Debug.Log($"[TouchDesignerOSCDebugger] {typeName}: {value} (Type: {value.GetType().Name})");
        
        // Test conversions
        try
        {
            if (value is bool boolVal)
            {
                Debug.Log($"  -> bool: {boolVal}");
            }
            if (value is int intVal)
            {
                Debug.Log($"  -> int: {intVal}, bool: {intVal != 0}");
            }
            if (value is float floatVal)
            {
                Debug.Log($"  -> float: {floatVal}, bool: {floatVal != 0f}");
            }
            if (value is double doubleVal)
            {
                Debug.Log($"  -> double: {doubleVal}, float: {(float)doubleVal}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"  -> Conversion error: {e.Message}");
        }
    }
    
    void OnDestroy()
    {
        // Clean up bindings if needed
    }
}
