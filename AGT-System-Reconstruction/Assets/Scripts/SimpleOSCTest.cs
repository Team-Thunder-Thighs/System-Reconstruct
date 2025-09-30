using UnityEngine;
using uOSC;
using UnityEditor;

/// <summary>
/// Super simple OSC test - just checks if Unity and TouchDesigner can talk
/// </summary>
public class SimpleOSCTest : MonoBehaviour
{
    [Header("Simple Connection Test")]
    [SerializeField] private bool autoTest = true;
    [SerializeField] private float testInterval = 3f;
    
    [Header("Status")]
    [SerializeField] private int messagesSent = 0;
    [SerializeField] private int messagesReceived = 0;
    
    private float lastTestTime;
    
    void Start()
    {
        Debug.Log("[SimpleOSCTest] Starting basic OSC test...");
        
        // Setup receiver for TouchDesigner messages
        if (OSCManager.Instance != null)
        {
            OSCManager.Instance.BindReceiver("/test/hello", OnTestMessage);
            Debug.Log("[SimpleOSCTest] ✅ Listening for TouchDesigner messages on /test/hello");
        }
        else
        {
            Debug.LogError("[SimpleOSCTest] OSCManager not found! Add OSCManager to scene first.");
        }
    }
    
    void Update()
    {
        if (!autoTest || OSCManager.Instance == null) return;
        
        // Send test message every few seconds
        if (Time.time - lastTestTime >= testInterval)
        {
            SendTestMessage();
            lastTestTime = Time.time;
        }
    }
    
    void SendTestMessage()
    {
        if (OSCManager.Instance == null) return;
        
        messagesSent++;
        string message = $"Hello TouchDesigner! #{messagesSent}";
        
        OSCManager.Instance.SendMessage("/test/unity", message, messagesSent);
        Debug.Log($"[SimpleOSCTest] Sent: {message}");
    }
    
    void OnTestMessage(Message message)
    {
        messagesReceived++;
        Debug.Log($"[SimpleOSCTest] Received from TouchDesigner: {message.values[0]}");
    }
    
    // Manual test buttons
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        
        GUILayout.Label("OSC Connection Test");
        GUILayout.Label($"Messages Sent: {messagesSent}");
        GUILayout.Label($"Messages Received: {messagesReceived}");
        
        if (GUILayout.Button("Send Test Message"))
        {
            SendTestMessage();
        }
        
        GUILayout.EndArea();
    }
}
