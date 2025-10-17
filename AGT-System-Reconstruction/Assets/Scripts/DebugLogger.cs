using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Centralized debug logging system that writes all logs to a text file
/// Automatically creates/overwrites the log file each time Unity plays
/// </summary>
public class DebugLogger : MonoBehaviour
{
    [Header("Logging Settings")]
    [SerializeField] private bool enableFileLogging = true;
    [SerializeField] private string logFileName = "debug_log.txt";
    [SerializeField] private bool includeTimestamp = true;
    [SerializeField] private bool logToConsole = true; // Keep console logging too
    
    [Header("Log Levels")]
    [SerializeField] private bool logInfo = true;
    [SerializeField] private bool logWarning = true;
    [SerializeField] private bool logError = true;
    
    private static DebugLogger instance;
    private string logFilePath;
    private StreamWriter logWriter;
    private bool isInitialized = false;
    
    public static DebugLogger Instance
    {
        get
        {
            if (instance == null)
            {
                // Auto-create instance if none exists
                GameObject loggerGO = new GameObject("DebugLogger");
                instance = loggerGO.AddComponent<DebugLogger>();
                DontDestroyOnLoad(loggerGO);
            }
            return instance;
        }
    }
    
    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeLogging();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    void InitializeLogging()
    {
        if (isInitialized) return;
        
        if (!enableFileLogging)
        {
            LogInfo("File logging disabled");
            return;
        }
        
        try
        {
            // Create log file path in project root
            string projectPath = Application.dataPath.Replace("/Assets", "");
            logFilePath = Path.Combine(projectPath, logFileName);
            
            // Create/overwrite the log file
            logWriter = new StreamWriter(logFilePath, false);
            
            // Write header
            string header = $"=== UNITY DEBUG LOG - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===";
            logWriter.WriteLine(header);
            logWriter.WriteLine($"Project: {Application.productName}");
            logWriter.WriteLine($"Unity Version: {Application.unityVersion}");
            logWriter.WriteLine($"Platform: {Application.platform}");
            logWriter.WriteLine($"Log File: {logFilePath}");
            logWriter.WriteLine(new string('=', header.Length));
            logWriter.WriteLine();
            logWriter.Flush();
            
            isInitialized = true;
            
            if (logToConsole)
            {
                Debug.Log($"[DebugLogger] ✅ File logging initialized: {logFilePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DebugLogger] ❌ Failed to initialize file logging: {e.Message}");
            enableFileLogging = false;
        }
    }
    
    void OnDestroy()
    {
        CloseLogFile();
    }
    
    void OnApplicationQuit()
    {
        CloseLogFile();
    }
    
    void CloseLogFile()
    {
        if (logWriter != null)
        {
            try
            {
                logWriter.WriteLine();
                logWriter.WriteLine($"=== LOG SESSION ENDED - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                logWriter.Close();
                logWriter = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DebugLogger] Error closing log file: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Log an info message
    /// </summary>
    public static void LogInfo(string message)
    {
        Instance.WriteLog("INFO", message);
    }
    
    /// <summary>
    /// Log a warning message
    /// </summary>
    public static void LogWarning(string message)
    {
        Instance.WriteLog("WARN", message);
    }
    
    /// <summary>
    /// Log an error message
    /// </summary>
    public static void LogError(string message)
    {
        Instance.WriteLog("ERROR", message);
    }
    
    /// <summary>
    /// Log a custom message with specified level
    /// </summary>
    public static void Log(string level, string message)
    {
        Instance.WriteLog(level, message);
    }
    
    /// <summary>
    /// Write log entry to file and console
    /// </summary>
    private void WriteLog(string level, string message)
    {
        if (!isInitialized) return;
        
        // Check if this log level is enabled
        bool shouldLog = false;
        switch (level.ToUpper())
        {
            case "INFO":
                shouldLog = logInfo;
                break;
            case "WARN":
            case "WARNING":
                shouldLog = logWarning;
                break;
            case "ERROR":
                shouldLog = logError;
                break;
            default:
                shouldLog = true; // Always log custom levels
                break;
        }
        
        if (!shouldLog) return;
        
        // Format log entry
        string timestamp = includeTimestamp ? DateTime.Now.ToString("HH:mm:ss.fff") : "";
        string logEntry = $"[{timestamp}] [{level}] {message}";
        
        // Write to console if enabled
        if (logToConsole)
        {
            switch (level.ToUpper())
            {
                case "INFO":
                    Debug.Log(logEntry);
                    break;
                case "WARN":
                case "WARNING":
                    Debug.LogWarning(logEntry);
                    break;
                case "ERROR":
                    Debug.LogError(logEntry);
                    break;
                default:
                    Debug.Log(logEntry);
                    break;
            }
        }
        
        // Write to file if enabled
        if (enableFileLogging && logWriter != null)
        {
            try
            {
                logWriter.WriteLine(logEntry);
                logWriter.Flush(); // Ensure immediate write
            }
            catch (Exception e)
            {
                Debug.LogError($"[DebugLogger] Failed to write to log file: {e.Message}");
                enableFileLogging = false;
            }
        }
    }
    
    /// <summary>
    /// Log a separator line for better readability
    /// </summary>
    public static void LogSeparator(string title = "")
    {
        if (string.IsNullOrEmpty(title))
        {
            Instance.WriteLog("---", new string('-', 50));
        }
        else
        {
            Instance.WriteLog("---", $"--- {title} ---");
        }
    }
    
    /// <summary>
    /// Log system information
    /// </summary>
    public static void LogSystemInfo()
    {
        LogSeparator("SYSTEM INFO");
        LogInfo($"Unity Version: {Application.unityVersion}");
        LogInfo($"Platform: {Application.platform}");
        LogInfo($"Product Name: {Application.productName}");
        LogInfo($"Company Name: {Application.companyName}");
        LogInfo($"Data Path: {Application.dataPath}");
        LogInfo($"Persistent Data Path: {Application.persistentDataPath}");
        LogInfo($"Screen Resolution: {Screen.width}x{Screen.height}");
        LogInfo($"Target Frame Rate: {Application.targetFrameRate}");
        LogSeparator();
    }
    
    /// <summary>
    /// Get the current log file path
    /// </summary>
    public static string GetLogFilePath()
    {
        return Instance.logFilePath;
    }
    
    /// <summary>
    /// Open the log file in the default text editor
    /// </summary>
    [ContextMenu("Open Log File")]
    public void OpenLogFile()
    {
        if (!string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath))
        {
            Application.OpenURL(logFilePath);
        }
        else
        {
            Debug.LogWarning("[DebugLogger] Log file not found or not created yet");
        }
    }
    
    /// <summary>
    /// Clear the current log file
    /// </summary>
    [ContextMenu("Clear Log File")]
    public void ClearLogFile()
    {
        if (logWriter != null)
        {
            logWriter.Close();
        }
        
        try
        {
            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
            }
            
            // Reinitialize
            isInitialized = false;
            InitializeLogging();
            
            Debug.Log("[DebugLogger] Log file cleared and reinitialized");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DebugLogger] Failed to clear log file: {e.Message}");
        }
    }
}
