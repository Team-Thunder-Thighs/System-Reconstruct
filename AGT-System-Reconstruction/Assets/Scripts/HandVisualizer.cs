using UnityEngine;
using UnityEngine.UI;
using AgtOscData;
using TMPro;

/// <summary>
/// Visualizes hand position data from TouchDesigner in Unity
/// Shows hand cursor, finger count, and hand tracking information
/// </summary>
public class HandVisualizer : MonoBehaviour
{
    [Header("Hand Visualization")]
    [SerializeField] private GameObject handCursorPrefab;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Camera mainCamera;
    
    [Header("Hand Cursor Settings")]
    [SerializeField] private bool showHandCursor = true;
    [SerializeField] private bool showFingerCount = true;
    [SerializeField] private bool showConfidence = true;
    [SerializeField] private Color validHandColor = Color.green;
    [SerializeField] private Color invalidHandColor = Color.red;
    [SerializeField] private float cursorSize = 50f;
    
    [Header("Coordinate System")]
    [SerializeField] private bool useNormalizedCoordinates = true; // TouchDesigner sends normalized coords (0-1)
    [SerializeField] private bool flipY = false; // Flip Y coordinate if needed
    
    [Header("Hand Representation")]
    [SerializeField] private bool showHandRepresentation = false;
    [SerializeField] private GameObject handModelPrefab;
    [SerializeField] private Transform handParent;
    
    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = true;
    
    // Runtime components
    private GameObject handCursor;
    private Image handCursorImage;
    private TextMeshProUGUI fingerCountText;
    private TextMeshProUGUI confidenceText;
    private GameObject handModel;
    
    // Hand data
    private Vector2 currentHandPosition;
    private int currentFingerCount;
    private float currentConfidence;
    private bool currentHandValid;
    private bool isHandVisible = false;
    
    // System reference
    private SimpleInteractionBridge interactionBridge;
    
    void Start()
    {
        // Get system references
        if (interactionBridge == null)
            interactionBridge = FindObjectOfType<SimpleInteractionBridge>();
        
        if (uiCanvas == null)
            uiCanvas = FindObjectOfType<Canvas>();
        
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        // Subscribe to hand data events
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.AddListener(OnHandDataReceived);
        }
        
        // Create hand visualization
        CreateHandVisualization();
        
        if (debugMode)
        {
            Debug.Log("[HandVisualizer] Hand visualizer initialized");
        }
    }
    
    void CreateHandVisualization()
    {
        if (!showHandCursor || uiCanvas == null) return;
        
        // Create hand cursor GameObject
        handCursor = new GameObject("HandCursor");
        handCursor.transform.SetParent(uiCanvas.transform, false);
        
        // Add Image component for cursor
        handCursorImage = handCursor.AddComponent<Image>();
        handCursorImage.color = invalidHandColor;
        
        // Set cursor size
        RectTransform cursorRect = handCursor.GetComponent<RectTransform>();
        cursorRect.sizeDelta = new Vector2(cursorSize, cursorSize);
        
        // Create finger count text
        if (showFingerCount)
        {
            GameObject fingerCountObj = new GameObject("FingerCount");
            fingerCountObj.transform.SetParent(handCursor.transform, false);
            
            fingerCountText = fingerCountObj.AddComponent<TextMeshProUGUI>();
            fingerCountText.text = "0";
            fingerCountText.fontSize = 24;
            fingerCountText.color = Color.white;
            fingerCountText.alignment = TextAlignmentOptions.Center;
            
            RectTransform fingerRect = fingerCountObj.GetComponent<RectTransform>();
            fingerRect.anchorMin = Vector2.zero;
            fingerRect.anchorMax = Vector2.one;
            fingerRect.offsetMin = Vector2.zero;
            fingerRect.offsetMax = Vector2.zero;
        }
        
        // Create confidence text
        if (showConfidence)
        {
            GameObject confidenceObj = new GameObject("Confidence");
            confidenceObj.transform.SetParent(handCursor.transform, false);
            
            confidenceText = confidenceObj.AddComponent<TextMeshProUGUI>();
            confidenceText.text = "0%";
            confidenceText.fontSize = 12;
            confidenceText.color = Color.yellow;
            confidenceText.alignment = TextAlignmentOptions.Center;
            
            RectTransform confidenceRect = confidenceObj.GetComponent<RectTransform>();
            confidenceRect.anchorMin = new Vector2(0, 0);
            confidenceRect.anchorMax = new Vector2(1, 0.5f);
            confidenceRect.offsetMin = Vector2.zero;
            confidenceRect.offsetMax = Vector2.zero;
        }
        
        // Initially hide the cursor
        handCursor.SetActive(false);
        
        // Create hand model if enabled
        if (showHandRepresentation && handModelPrefab != null)
        {
            CreateHandModel();
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
    
    void OnHandDataReceived(SimpleInteractionBridge.HandInteractionData handData)
    {
        // Update hand data
        currentHandPosition = handData.position;
        currentFingerCount = handData.fingers;
        currentConfidence = handData.confidence;
        currentHandValid = handData.isValid;
        
        // Update visualization
        UpdateHandVisualization();
        
        if (debugMode)
        {
            string coordType = useNormalizedCoordinates ? "normalized" : "screen";
            Debug.Log($"[HandVisualizer] Hand data: {currentFingerCount} fingers at ({currentHandPosition.x:F3}, {currentHandPosition.y:F3}) {coordType}, valid: {currentHandValid}");
        }
    }
    
    void UpdateHandVisualization()
    {
        // Update hand cursor
        if (showHandCursor && handCursor != null)
        {
            // Show/hide cursor based on hand validity
            bool shouldShow = currentHandValid && currentConfidence > 0.5f;
            
            if (shouldShow != isHandVisible)
            {
                handCursor.SetActive(shouldShow);
                isHandVisible = shouldShow;
            }
            
            if (shouldShow)
            {
                // Update cursor position (convert screen coordinates to UI coordinates)
                Vector2 uiPosition = ConvertScreenToUIPosition(currentHandPosition);
                RectTransform cursorRect = handCursor.GetComponent<RectTransform>();
                cursorRect.anchoredPosition = uiPosition;
                
                // Update cursor color based on validity
                handCursorImage.color = currentHandValid ? validHandColor : invalidHandColor;
                
                // Update finger count text
                if (fingerCountText != null)
                {
                    fingerCountText.text = currentFingerCount.ToString();
                }
                
                // Update confidence text
                if (confidenceText != null)
                {
                    confidenceText.text = $"{(currentConfidence * 100):F0}%";
                }
            }
        }
        
        // Update hand model
        if (showHandRepresentation && handModel != null)
        {
            bool shouldShowModel = currentHandValid && currentConfidence > 0.5f;
            handModel.SetActive(shouldShowModel);
            
            if (shouldShowModel)
            {
                // Convert screen position to world position for 3D hand model
                Vector3 worldPosition = ConvertScreenToWorldPosition(currentHandPosition);
                handModel.transform.position = worldPosition;
                
                // You could also update finger positions here if you have detailed hand data
                UpdateHandModelFingers();
            }
        }
    }
    
    Vector2 ConvertScreenToUIPosition(Vector2 inputPosition)
    {
        if (uiCanvas == null) return Vector2.zero;
        
        Vector2 screenPosition = inputPosition;
        
        // Convert normalized coordinates to screen coordinates if needed
        if (useNormalizedCoordinates)
        {
            screenPosition.x *= Screen.width;
            screenPosition.y *= Screen.height;
            
            if (flipY)
            {
                screenPosition.y = Screen.height - screenPosition.y;
            }
        }
        
        // Convert screen coordinates to UI coordinates
        Vector2 uiPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiCanvas.transform as RectTransform,
            screenPosition,
            uiCanvas.worldCamera,
            out uiPosition
        );
        
        return uiPosition;
    }
    
    Vector3 ConvertScreenToWorldPosition(Vector2 inputPosition)
    {
        if (mainCamera == null) return Vector3.zero;
        
        Vector2 screenPosition = inputPosition;
        
        // Convert normalized coordinates to screen coordinates if needed
        if (useNormalizedCoordinates)
        {
            screenPosition.x *= Screen.width;
            screenPosition.y *= Screen.height;
            
            if (flipY)
            {
                screenPosition.y = Screen.height - screenPosition.y;
            }
        }
        
        // Convert screen coordinates to world coordinates
        Vector3 screenPos = new Vector3(screenPosition.x, screenPosition.y, mainCamera.nearClipPlane + 1f);
        return mainCamera.ScreenToWorldPoint(screenPos);
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
    public void SetHandCursorVisible(bool visible)
    {
        showHandCursor = visible;
        if (handCursor != null)
        {
            handCursor.SetActive(visible && isHandVisible);
        }
    }
    
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
    public void SetHandCursorColor(Color color)
    {
        validHandColor = color;
        if (handCursorImage != null && currentHandValid)
        {
            handCursorImage.color = color;
        }
    }
    
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
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Draw hand position in scene view
        if (currentHandValid)
        {
            Gizmos.color = validHandColor;
            Vector3 worldPos = ConvertScreenToWorldPosition(currentHandPosition);
            Gizmos.DrawWireSphere(worldPos, 0.1f);
            
            // Draw finger count text
            #if UNITY_EDITOR
            string coordInfo = useNormalizedCoordinates ? " (norm)" : " (screen)";
            UnityEditor.Handles.Label(worldPos + Vector3.up * 0.2f, $"Fingers: {currentFingerCount}{coordInfo}");
            #endif
        }
    }
    
    void OnDestroy()
    {
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.RemoveListener(OnHandDataReceived);
        }
    }
}
