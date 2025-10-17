using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Static helper class for coordinate system conversions in Unity.
/// 
/// This class handles conversions between different coordinate spaces:
/// - World Space (Unity 3D world coordinates)
/// - Screen Space (pixel coordinates)
/// - UI Local Space (RectTransform local coordinates)
/// 
/// Note: This class does NOT handle external data conversion (e.g., TouchDesigner → Unity).
/// That responsibility belongs to InputFacade.
/// 
/// This class handles ONLY Unity-internal coordinate transformations.
/// </summary>
public static class CoordinateConverter
{
    /// <summary>
    /// Convert world space coordinates to screen space (pixels).
    /// Assumes world coordinates are in the range TouchDesigner sends:
    /// X: -0.5 to 0.5 (left to right)
    /// Y: -0.9 to -0.3 (top to bottom)
    /// </summary>
    public static Vector2 WorldToScreen(Vector3 worldPosition)
    {
        // X: -0.5..0.5 → 0..Screen.width
        float screenX = (worldPosition.x + 0.5f) * Screen.width;
        
        // Y: -0.9..-0.3 → 0..Screen.height (inverted)
        // First normalize to 0..1 range
        float normalizedY = ((worldPosition.y + 0.9f) / 0.6f);
        normalizedY = Mathf.Clamp01(normalizedY); // Ensure 0..1 range
        normalizedY = 1f - normalizedY; // Invert the Y coordinate
        float screenY = normalizedY * Screen.height;
        
        return new Vector2(screenX, screenY);
    }
    
    /// <summary>
    /// Convert world space coordinates to UI local coordinates for a specific canvas.
    /// </summary>
    public static Vector2 WorldToUI(Vector3 worldPosition, Canvas canvas)
    {
        if (canvas == null)
        {
            DebugLogger.LogError("[CoordinateConverter] Canvas is null, cannot convert to UI coordinates");
            return Vector2.zero;
        }
        
        // First convert to screen space
        Vector2 screenPos = WorldToScreen(worldPosition);
        
        // Then convert screen to UI local space
        return ScreenToUI(screenPos, canvas);
    }
    
    /// <summary>
    /// Convert screen space (pixels) to UI local coordinates for a specific canvas.
    /// </summary>
    public static Vector2 ScreenToUI(Vector2 screenPosition, Canvas canvas)
    {
        if (canvas == null)
        {
            DebugLogger.LogError("[CoordinateConverter] Canvas is null, cannot convert to UI coordinates");
            return Vector2.zero;
        }
        
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            DebugLogger.LogError("[CoordinateConverter] Canvas does not have a RectTransform");
            return Vector2.zero;
        }
        
        // Determine camera based on canvas render mode
        Camera canvasCamera = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) 
            ? null 
            : canvas.worldCamera;
        
        // Convert screen point to local point in canvas
        Vector2 uiPosition;
        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            canvasCamera,
            out uiPosition
        );
        
        if (!success)
        {
            DebugLogger.LogWarning($"[CoordinateConverter] Failed to convert screen position {screenPosition} to UI local coordinates");
        }
        
        return uiPosition;
    }
    
    /// <summary>
    /// Convert world space coordinates to screen space using Unity's camera.
    /// This is the standard Unity world→screen conversion (for 3D objects).
    /// </summary>
    public static Vector2 WorldToScreenCamera(Vector3 worldPosition, Camera camera = null)
    {
        if (camera == null)
        {
            camera = Camera.main;
        }
        
        if (camera == null)
        {
            DebugLogger.LogError("[CoordinateConverter] No camera available for world→screen conversion");
            return Vector2.zero;
        }
        
        Vector3 screenPos = camera.WorldToScreenPoint(worldPosition);
        return new Vector2(screenPos.x, screenPos.y);
    }
    
    /// <summary>
    /// Convert screen space to world space using Unity's camera.
    /// This is the standard Unity screen→world conversion (for 3D objects).
    /// </summary>
    public static Vector3 ScreenToWorldCamera(Vector2 screenPosition, float depth, Camera camera = null)
    {
        if (camera == null)
        {
            camera = Camera.main;
        }
        
        if (camera == null)
        {
            DebugLogger.LogError("[CoordinateConverter] No camera available for screen→world conversion");
            return Vector3.zero;
        }
        
        Vector3 screenPos = new Vector3(screenPosition.x, screenPosition.y, depth);
        return camera.ScreenToWorldPoint(screenPos);
    }
    
    /// <summary>
    /// Check if a screen point is inside a RectTransform's bounds.
    /// </summary>
    public static bool IsScreenPointInRectTransform(Vector2 screenPoint, RectTransform rectTransform, Canvas canvas)
    {
        if (rectTransform == null || canvas == null)
        {
            return false;
        }
        
        // Convert screen point to local point in the rect transform's coordinate system
        Camera canvasCamera = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) 
            ? null 
            : canvas.worldCamera;
        
        Vector2 localPoint;
        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            screenPoint,
            canvasCamera,
            out localPoint
        );
        
        if (!success)
        {
            return false;
        }
        
        // Check if the point is within the rect
        return rectTransform.rect.Contains(localPoint);
    }
    
    /// <summary>
    /// Get information about the current screen resolution and coordinate system.
    /// Useful for debugging coordinate conversion issues.
    /// </summary>
    public static string GetCoordinateSystemInfo()
    {
        return $"Screen: {Screen.width}x{Screen.height}, " +
               $"DPI: {Screen.dpi}, " +
               $"Orientation: {Screen.orientation}";
    }
}

