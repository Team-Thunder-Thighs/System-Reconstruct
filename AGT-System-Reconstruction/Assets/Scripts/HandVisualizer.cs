using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AgtOscData;
using TMPro;
using BodyTracking.DataModel;
using Pose = BodyTracking.DataModel.Pose;

/// <summary>
/// Visualizes body landmark positions from TouchDesigner in Unity
/// Shows multiple landmarks: shoulders, elbows, wrists (landmarks 11-16)
/// </summary>
public class HandVisualizer : MonoBehaviour
{
    [Header("Landmark Visualization")]
    [SerializeField] private GameObject landmarkCursorPrefab;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Camera mainCamera;
    
    [Header("Landmarks to Visualize")]
    [SerializeField] private bool showShoulders = true;
    [SerializeField] private bool showElbows = true;
    [SerializeField] private bool showWrists = true;
    
    [Header("Cursor Settings")]
    [SerializeField] private bool showLandmarkCursors = true;
    [SerializeField] private bool showLabels = true;
    [SerializeField] private Color shoulderColor = Color.blue;
    [SerializeField] private Color elbowColor = Color.yellow;
    [SerializeField] private Color wristColor = Color.green;
    [SerializeField] private Color invalidColor = Color.red;
    [SerializeField] private float cursorSize = 30f;
    
    [Header("Hand Representation")]
    [SerializeField] private bool showHandRepresentation = false;
    [SerializeField] private GameObject handModelPrefab;
    [SerializeField] private Transform handParent;
    
    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = true;
    
    // Runtime components - Dictionary of landmark visualizations
    private Dictionary<BodyLandmark, GameObject> landmarkCursors = new Dictionary<BodyLandmark, GameObject>();
    private Dictionary<BodyLandmark, Image> landmarkImages = new Dictionary<BodyLandmark, Image>();
    private Dictionary<BodyLandmark, TextMeshProUGUI> landmarkLabels = new Dictionary<BodyLandmark, TextMeshProUGUI>();
    private GameObject handModel;
    
    // Landmarks to visualize (11-16)
    private BodyLandmark[] landmarksToVisualize = new BodyLandmark[]
    {
        BodyLandmark.LeftShoulder,   // 11
        BodyLandmark.RightShoulder,  // 12
        BodyLandmark.LeftElbow,      // 13
        BodyLandmark.RightElbow,     // 14
        BodyLandmark.LeftWrist,      // 15
        BodyLandmark.RightWrist      // 16
    };
    
    // Hand data - now using Pose data model internally
    private Pose currentHandPose;
    private Vector2 currentHandPosition;
    private int currentFingerCount;
    private float currentConfidence;
    private bool currentHandValid;
    private bool isHandVisible = false;
    
    // System reference
    private SimpleInteractionBridge interactionBridge;
    
    void Start()
    {
        // Initialize pose
        currentHandPose = new Pose(true);
        
        // Get system references
        if (interactionBridge == null)
            interactionBridge = FindObjectOfType<SimpleInteractionBridge>();
        
        if (uiCanvas == null)
            uiCanvas = FindObjectOfType<Canvas>();
        
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        // Subscribe to InputFacade for pose data (to get all landmarks 11-16)
        if (InputFacade.Instance != null)
        {
            InputFacade.Instance.OnPoseDataReceived += OnPoseDataReceived;
            DebugLogger.LogInfo("[HandVisualizer] Subscribed to InputFacade pose data");
        }
        
        // Also subscribe to hand data events for compatibility
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.AddListener(OnHandDataReceived);
        }
        
        // Create landmark visualizations for all landmarks (11-16)
        CreateLandmarkVisualizations();
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[HandVisualizer] Landmark visualizer initialized - showing landmarks 11-16");
        }
    }
    
    void CreateLandmarkVisualizations()
    {
        if (!showLandmarkCursors || uiCanvas == null) 
        {
            DebugLogger.LogWarning($"[HandVisualizer] Cannot create landmark visualizations: showLandmarkCursors={showLandmarkCursors}, uiCanvas={uiCanvas != null}");
            return;
        }
        
        DebugLogger.LogInfo("[HandVisualizer] Creating landmark visualizations for landmarks 11-16");
        
        // Create a cursor for each landmark
        foreach (BodyLandmark landmark in landmarksToVisualize)
        {
            // Check if this landmark type should be shown
            if (!ShouldShowLandmark(landmark)) continue;
            
            // Create cursor GameObject
            GameObject cursor = new GameObject($"Landmark_{landmark}_{(int)landmark}");
            cursor.transform.SetParent(uiCanvas.transform, false);
            
            // Add Image component for cursor
            Image cursorImage = cursor.AddComponent<Image>();
            cursorImage.color = GetLandmarkColor(landmark);
            
            // Set cursor size
            RectTransform cursorRect = cursor.GetComponent<RectTransform>();
            cursorRect.sizeDelta = new Vector2(cursorSize, cursorSize);
            
            // Create label text
            if (showLabels)
            {
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(cursor.transform, false);
                
                TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
                labelText.text = GetLandmarkShortName(landmark);
                labelText.fontSize = 16;
                labelText.color = Color.white;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.fontStyle = FontStyles.Bold;
                
                // Add outline for better visibility
                labelText.outlineWidth = 0.2f;
                labelText.outlineColor = Color.black;
                
                RectTransform labelRect = labelObj.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                
                landmarkLabels[landmark] = labelText;
            }
            
            // Initially hide the cursor
            cursor.SetActive(false);
            
            // Store references
            landmarkCursors[landmark] = cursor;
            landmarkImages[landmark] = cursorImage;
        }
        
        DebugLogger.LogInfo($"[HandVisualizer] Created {landmarkCursors.Count} landmark visualizations");
        
        // Create hand model if enabled
        if (showHandRepresentation && handModelPrefab != null)
        {
            CreateHandModel();
        }
    }
    
    bool ShouldShowLandmark(BodyLandmark landmark)
    {
        if (landmark == BodyLandmark.LeftShoulder || landmark == BodyLandmark.RightShoulder)
            return showShoulders;
        if (landmark == BodyLandmark.LeftElbow || landmark == BodyLandmark.RightElbow)
            return showElbows;
        if (landmark == BodyLandmark.LeftWrist || landmark == BodyLandmark.RightWrist)
            return showWrists;
        return true;
    }
    
    Color GetLandmarkColor(BodyLandmark landmark)
    {
        if (landmark == BodyLandmark.LeftShoulder || landmark == BodyLandmark.RightShoulder)
            return shoulderColor;
        if (landmark == BodyLandmark.LeftElbow || landmark == BodyLandmark.RightElbow)
            return elbowColor;
        if (landmark == BodyLandmark.LeftWrist || landmark == BodyLandmark.RightWrist)
            return wristColor;
        return Color.white;
    }
    
    string GetLandmarkShortName(BodyLandmark landmark)
    {
        switch (landmark)
        {
            case BodyLandmark.LeftShoulder: return "LS";
            case BodyLandmark.RightShoulder: return "RS";
            case BodyLandmark.LeftElbow: return "LE";
            case BodyLandmark.RightElbow: return "RE";
            case BodyLandmark.LeftWrist: return "LW";
            case BodyLandmark.RightWrist: return "RW";
            default: return landmark.ToString();
        }
    }
    
    void CreateHandModel()
    {
        if (handParent == null)
        {
            GameObject handParentObj = new GameObject("HandParent");
            handParent = handParentObj.transform;
        }
        
        handModel = Instantiate(handModelPrefab, handParent);
        handModel.SetActive(false);
    }
    
    void OnPoseDataReceived(Pose newPose)
    {
        currentHandPose = newPose;
        
        // Update all landmark visualizations
        UpdateLandmarkVisualizations();
    }
    
    void OnHandDataReceived(SimpleInteractionBridge.HandInteractionData handData)
    {
        // Update legacy hand data for compatibility
        currentHandPosition = handData.position;
        currentFingerCount = handData.fingers;
        currentConfidence = handData.confidence;
        currentHandValid = handData.isValid;
    }
    
    void UpdateLandmarkVisualizations()
    {
        if (!showLandmarkCursors || !currentHandPose.IsValid()) return;
        
        // Update each landmark
        foreach (BodyLandmark landmark in landmarksToVisualize)
        {
            if (!landmarkCursors.ContainsKey(landmark)) continue;
            
            GameObject cursor = landmarkCursors[landmark];
            Image cursorImage = landmarkImages[landmark];
            
            // Get landmark position from pose (world/normalized coordinates)
            Vector3 landmarkWorldPos = currentHandPose.GetLandmark(landmark);
            
            // Check if landmark has valid data
            bool hasData = landmarkWorldPos != Vector3.zero;
            
            // Show/hide cursor based on data availability
            cursor.SetActive(hasData);
            
            if (hasData)
            {
                // Convert world coordinates to screen coordinates
                Vector2 screenPos = CoordinateConverter.WorldToScreen(landmarkWorldPos);
                
                // Convert screen coordinates to UI coordinates
                Vector2 uiPos = CoordinateConverter.ScreenToUI(screenPos, uiCanvas);
                
                // Update cursor position
                RectTransform cursorRect = cursor.GetComponent<RectTransform>();
                cursorRect.anchoredPosition = uiPos;
                
                // Update color (already set by GetLandmarkColor, but we could change it based on validity)
                cursorImage.color = GetLandmarkColor(landmark);
                
                if (debugMode)
                {
                    // DebugLogger.LogInfo($"[HandVisualizer] {landmark}: world({landmarkWorldPos.x:F3},{landmarkWorldPos.y:F3}) -> screen({screenPos.x:F1},{screenPos.y:F1}) -> ui({uiPos.x:F1},{uiPos.y:F1})");
                }
            }
        }
        
        // Update hand model if enabled
        if (showHandRepresentation && handModel != null)
        {
            bool shouldShowModel = currentHandPose.IsValid();
            handModel.SetActive(shouldShowModel);
            
            if (shouldShowModel)
            {
                // Use wrist position for model placement
                Vector3 leftWrist = currentHandPose.GetLandmark(BodyLandmark.LeftWrist);
                if (leftWrist != Vector3.zero)
                {
                    Vector2 screenPos = CoordinateConverter.WorldToScreen(leftWrist);
                    Vector3 worldPosition = ConvertScreenToWorldPosition(screenPos);
                    handModel.transform.position = worldPosition;
                    UpdateHandModelFingers();
                }
            }
        }
    }
    
    Vector2 ConvertScreenToUIPosition(Vector2 inputPosition)
    {
        if (uiCanvas == null) return Vector2.zero;
        
        // Use centralized CoordinateConverter
        // InputPosition is already screen coordinates from SimpleInteractionBridge
        Vector2 uiPosition = CoordinateConverter.ScreenToUI(inputPosition, uiCanvas);
        
        // Debug logging for coordinate conversion issues
        if (debugMode)
        {
            DebugLogger.LogInfo($"[HandVisualizer] Coordinate conversion: screen=({inputPosition.x:F2},{inputPosition.y:F2}) -> ui=({uiPosition.x:F2},{uiPosition.y:F2})");
        }
        
        return uiPosition;
    }
    
    Vector3 ConvertScreenToWorldPosition(Vector2 inputPosition)
    {
        if (mainCamera == null) return Vector3.zero;
        
        // Use centralized CoordinateConverter
        // InputPosition is already screen coordinates from SimpleInteractionBridge
        float depth = mainCamera.nearClipPlane + 1f;
        return CoordinateConverter.ScreenToWorldCamera(inputPosition, depth, mainCamera);
    }
    
    void UpdateHandModelFingers()
    {
        // This is where you would update individual finger positions
        // if you have detailed hand landmark data from TouchDesigner
        
        // For now, we'll just scale the hand model based on finger count
        if (handModel != null)
        {
            float scale = 0.8f + (currentFingerCount * 0.1f); // Scale based on finger count
            handModel.transform.localScale = Vector3.one * scale;
        }
    }
    
    // Public API methods
    
    /// <summary>
    /// Set hand cursor visibility
    /// </summary>
    // public void SetHandCursorVisible(bool visible)
    // {
    //     showHandCursor = visible;
    //     if (handCursor != null)
    //     {
    //         handCursor.SetActive(visible && isHandVisible);
    //     }
    // }
    
    /// <summary>
    /// Set hand model visibility
    /// </summary>
    public void SetHandModelVisible(bool visible)
    {
        showHandRepresentation = visible;
        if (handModel != null)
        {
            handModel.SetActive(visible && currentHandValid);
        }
    }
    
    /// <summary>
    /// Update hand cursor color
    /// </summary>
    // public void SetHandCursorColor(Color color)
    // {
    //     validHandColor = color;
    //     if (handCursorImage != null && currentHandValid)
    //     {
    //         handCursorImage.color = color;
    //     }
    // }
    //
    /// <summary>
    /// Get current hand position in screen coordinates
    /// </summary>
    public Vector2 GetCurrentHandPosition()
    {
        return currentHandPosition;
    }
    
    /// <summary>
    /// Get current finger count
    /// </summary>
    public int GetCurrentFingerCount()
    {
        return currentFingerCount;
    }
    
    /// <summary>
    /// Check if hand is currently visible
    /// </summary>
    public bool IsHandVisible()
    {
        return isHandVisible && currentHandValid;
    }
    
    /// <summary>
    /// Get current hand pose (using new Pose data model)
    /// </summary>
    public Pose GetCurrentHandPose()
    {
        return currentHandPose;
    }
    
    /// <summary>
    /// Get specific landmark from current hand pose
    /// </summary>
    public Vector3 GetLandmark(BodyLandmark landmark)
    {
        return currentHandPose.GetLandmark(landmark);
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Draw hand position in scene view
        if (currentHandValid)
        {
            // Gizmos.color = validHandColor;
            Vector3 worldPos = ConvertScreenToWorldPosition(currentHandPosition);
            Gizmos.DrawWireSphere(worldPos, 0.1f);
            
            // Draw finger count text
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(worldPos + Vector3.up * 0.2f, $"Fingers: {currentFingerCount} (screen)");
            #endif
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from InputFacade
        if (InputFacade.Instance != null)
        {
            InputFacade.Instance.OnPoseDataReceived -= OnPoseDataReceived;
        }
        
        // Unsubscribe from SimpleInteractionBridge
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.RemoveListener(OnHandDataReceived);
        }
    }
}
