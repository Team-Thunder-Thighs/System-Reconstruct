using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using extOSC;
using UnityEngine.Events;
using uOSC;

public class OSCManager : MonoBehaviour
{
    [Header("=== OSC Manager Configuration ===")]
    
    [Header("Outgoing Messages (uOSC - Unity to TouchDesigner)")]
    [SerializeField] private string touchDesignerIP = "127.0.0.1";
    [SerializeField] private int touchDesignerPort = 7000;
    [SerializeField] private int maxQueueSize = 100;
    [SerializeField] private float dataTransmissionInterval = 0f;
    
    [Header("Incoming Messages (extOSC - TouchDesigner to Unity)")]
    [SerializeField] private int unityReceivePort = 62139;
    [SerializeField] private bool autoConnectReceiver = true;
    
    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool logIncomingMessages = false;
    [SerializeField] private bool logOutgoingMessages = false;
    
    //=============================Go TTT !!!============================================

    public static OSCManager Instance;
    [Header("Receiving (from TouchDesigner)")]
    public OSCReceiver receiver;  // extOSC
    
    [Header("Sending (to TouchDesigner)")]  
    public uOscClient sender;      // uOSC wrapper
    
    // Connection status
    public bool IsReceiverConnected => receiver != null && receiver.IsStarted;
    public bool IsSenderConnected => sender != null && sender.isRunning;
    public bool IsFullyConnected => IsReceiverConnected && IsSenderConnected;
    
    // Events for connection status
    public System.Action OnOSCManagerReady;
    public System.Action OnOSCManagerDisconnected;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeOSCComponents();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(InitializeAfterFrame());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            OnOSCManagerDisconnected?.Invoke();
        }
    }

    #endregion
    
    #region Initialization
    
    private void InitializeOSCComponents()
    {
        if (debugMode) Debug.Log("[OSCManager] Initializing OSC components...");
        
        // Setup extOSC Receiver (for incoming messages from TouchDesigner)
        SetupReceiver();
        
        // Setup uOSC Client (for outgoing messages to TouchDesigner)
        SetupSender();
    }
    
    private void SetupReceiver()
    {
        // Get or create extOSC receiver
        receiver = GetComponent<OSCReceiver>();
        if (receiver == null)
        {
            receiver = gameObject.AddComponent<OSCReceiver>();
            if (debugMode) Debug.Log("[OSCManager] Created new OSCReceiver component");
        }
        
        // Configure receiver settings
        receiver.LocalPort = unityReceivePort;
        receiver.AutoConnect = autoConnectReceiver;
        
        if (debugMode) Debug.Log($"[OSCManager] Receiver configured on port {unityReceivePort}");
    }
    
    private void SetupSender()
    {
        // Get or create uOSC client
        sender = GetComponent<uOscClient>();
        if (sender == null)
        {
            sender = gameObject.AddComponent<uOscClient>();
            if (debugMode) Debug.Log("[OSCManager] Created new uOscClient component");
        }
        
        // Configure sender settings
        sender.address = touchDesignerIP;
        sender.port = touchDesignerPort;
        sender.maxQueueSize = maxQueueSize;
        sender.dataTransimissionInterval = dataTransmissionInterval;
        
        if (debugMode) Debug.Log($"[OSCManager] Sender configured to {touchDesignerIP}:{touchDesignerPort}");
    }
    
    private IEnumerator InitializeAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        
        // Verify connections
        CheckConnectionStatus();
        
        if (IsFullyConnected)
        {
            if (debugMode) Debug.Log("[OSCManager] OSC Manager fully initialized and connected!");
            OnOSCManagerReady?.Invoke();
        }
        else
        {
            Debug.LogWarning("[OSCManager] OSC Manager initialization incomplete. Check connections.");
        }
    }
    
    #endregion
    
    #region Public API - Sending Messages (uOSC)
    
    /// <summary>
    /// Send a simple OSC message with basic parameters
    /// </summary>
    public void SendMessage(string address, params object[] values)
    {
        if (!IsSenderConnected)
        {
            Debug.LogWarning($"[OSCManager] Cannot send message - sender not connected: {address}");
            return;
        }
        
        if (logOutgoingMessages)
        {
            Debug.Log($"[OSCManager] Sending: {address} with {values.Length} parameters");
        }
        
        sender.Send(address, values);
    }
    
    /// <summary>
    /// Send interaction state message to TouchDesigner
    /// </summary>
    public void SendInteractionState(string interactionType, bool state)
    {
        SendMessage("/OSC/interaction", interactionType, state);
    }
    
    /// <summary>
    /// Send interaction update trigger to TouchDesigner
    /// </summary>
    public void SendInteractionUpdate()
    {
        SendMessage("/OSC/interaction", "update");
    }
    
    /// <summary>
    /// Send custom game event to TouchDesigner
    /// </summary>
    public void SendGameEvent(string eventName, params object[] parameters)
    {
        string address = $"/OSC/game/{eventName}";
        SendMessage(address, parameters);
    }
    
    #endregion
    
    #region Public API - Receiving Messages (extOSC)
    
    /// <summary>
    /// Bind a callback to an incoming OSC address
    /// </summary>
    public void BindReceiver(string address, UnityAction<OSCMessage> callback)
    {
        if (!IsReceiverConnected)
        {
            Debug.LogWarning($"[OSCManager] Cannot bind receiver - receiver not connected: {address}");
            return;
        }
        
        receiver.Bind(address, callback);
        
        if (debugMode)
        {
            Debug.Log($"[OSCManager] Bound receiver for address: {address}");
        }
    }
    
    /// <summary>
    /// Unbind a callback from an OSC address
    /// </summary>
    public void UnbindReceiver(string address)
    {
        if (receiver != null)
        {
            // Note: extOSC doesn't have a direct unbind by address method
            // You would need to keep track of the bind object to unbind
            if (debugMode)
            {
                Debug.Log($"[OSCManager] Unbind requested for address: {address}");
            }
        }
    }
    
    #endregion
    
    #region Connection Management
    
    private void CheckConnectionStatus()
    {
        if (debugMode)
        {
            Debug.Log($"[OSCManager] Connection Status - Receiver: {IsReceiverConnected}, Sender: {IsSenderConnected}");
        }
    }
    
    #endregion
    
    #region Debug & Monitoring
    
    /// <summary>
    /// Get current OSC status for debugging
    /// </summary>
    public string GetOSCStatus()
    {
        return $"OSC Status:\n" +
               $"- Receiver Connected: {IsReceiverConnected} (Port: {unityReceivePort})\n" +
               $"- Sender Connected: {IsSenderConnected} (Target: {touchDesignerIP}:{touchDesignerPort})\n" +
               $"- Fully Connected: {IsFullyConnected}";
    }
    
    #endregion

}
