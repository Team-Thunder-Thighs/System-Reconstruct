using UnityEngine;
using BodyTracking.DataModel;
using Pose = BodyTracking.DataModel.Pose;

/// <summary>
/// Tutorial1State - First tutorial step.
/// 
/// This state teaches the player to point their hand to different screen quadrants.
/// Player must point to all 4 quadrants (holding for 2 seconds each).
/// When all 4 quadrants are completed, transitions to Tutorial2State.
/// 
/// Quadrants:
/// 1. Top-Left (Q1)
/// 2. Top-Right (Q2)
/// 3. Bottom-Right (Q3)
/// 4. Bottom-Left (Q4) → Completes tutorial
/// 
/// Flow:
/// MainMenu → Tutorial1 (point to quadrants) → Tutorial2
/// 
/// IMPORTANT: Uses screen coordinates from SimpleInteractionBridge to align with HandVisualizer cursor.
/// </summary>
public class Tutorial1State : IGameState
{
    private GameManager manager;
    private UIManager uiManager;
    private SimpleInteractionBridge interactionBridge;
    private OutputFacade outputFacade;
    private bool isInitialized = false;
    
    // Hand tracking - now using screen coordinates from SimpleInteractionBridge
    private Vector2 leftHandScreenPosition;
    private Vector2 rightHandScreenPosition;
    private bool hasLeftHandData = false;
    private bool hasRightHandData = false;
    private float handCheckInterval = 0.1f; // Check every 0.1 seconds
    private float lastCheckTime = 0f;
    
    // Quadrant tracking
    private enum Quadrant
    {
        None = 0,
        TopLeft = 1,      // Q1
        TopRight = 2,     // Q2
        BottomRight = 3,  // Q3
        BottomLeft = 4    // Q4
    }
    
    private Quadrant currentQuadrant = Quadrant.None;
    private Quadrant targetQuadrant = Quadrant.TopLeft; // Start with Q1
    private float holdDuration = 2f; // Hold for 2 seconds
    private float holdStartTime = 0f;
    private bool isHolding = false;
    
    // Progress tracking
    private bool[] completedQuadrants = new bool[5]; // Index 0 unused, 1-4 for quadrants
    private int completedCount = 0;
    
    public void Enter(GameManager manager)
    {
        this.manager = manager;
        
        DebugLogger.LogInfo("[Tutorial1State] Entering Tutorial 1 - Quadrant Pointing");
        
        // Get references
        interactionBridge = UnityEngine.Object.FindObjectOfType<SimpleInteractionBridge>();
        outputFacade = manager.GetOutputFacade();
        uiManager = UIManager.Instance;
        
        // Subscribe to hand interaction data (same as HandVisualizer)
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.AddListener(OnHandInteractionReceived);
            DebugLogger.LogInfo("[Tutorial1State] Subscribed to SimpleInteractionBridge hand events");
        }
        else
        {
            DebugLogger.LogError("[Tutorial1State] SimpleInteractionBridge not found!");
        }
        
        // Initialize progress
        for (int i = 0; i < completedQuadrants.Length; i++)
        {
            completedQuadrants[i] = false;
        }
        completedCount = 0;
        targetQuadrant = Quadrant.TopLeft;
        hasLeftHandData = false;
        hasRightHandData = false;
        
        // Show tutorial UI with progress bar
        if (uiManager != null)
        {
            UpdateTutorialUI();
            uiManager.ShowProgressBar(0f);
        }
        
        isInitialized = true;
        
        // Send signal to TouchDesigner
        if (outputFacade != null)
        {
            outputFacade.SendCustomTrigger("tutorial1_entered");
        }
    }
    
    public void Update()
    {
        if (!isInitialized) return;
        
        // Check hand position periodically
        if (Time.time - lastCheckTime >= handCheckInterval)
        {
            CheckHandQuadrant();
            lastCheckTime = Time.time;
        }
        
        // Update progress bar if holding
        if (isHolding)
        {
            float currentHoldTime = Time.time - holdStartTime;
            float progress = Mathf.Clamp01(currentHoldTime / holdDuration);
            
            if (uiManager != null)
            {
                uiManager.UpdateProgressBar(progress);
            }
            
            // Check if hold duration reached
            if (currentHoldTime >= holdDuration)
            {
                OnQuadrantCompleted();
            }
        }
        
        // Debug: Press N to skip to next tutorial
        if (Input.GetKeyDown(KeyCode.N))
        {
            DebugLogger.LogInfo("[Tutorial1State] Skipping to Tutorial 2");
            manager.TransitionToState("Tutorial2");
        }
        
        // Debug: Press Escape to go back to main menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            manager.TransitionToState("MainMenu");
        }
    }
    
    public void Exit()
    {
        DebugLogger.LogInfo("[Tutorial1State] Exiting Tutorial 1");
        
        // Unsubscribe from events
        if (interactionBridge != null)
        {
            interactionBridge.OnHandInteraction.RemoveListener(OnHandInteractionReceived);
        }
        
        // Clear UI
        if (uiManager != null)
        {
            uiManager.HideTutorialText();
            uiManager.HideProgressBar();
        }
        
        isInitialized = false;
        
        // Send signal to TouchDesigner
        if (outputFacade != null)
        {
            outputFacade.SendCustomTrigger("tutorial1_completed");
        }
    }
    
    public string GetStateName()
    {
        return "Tutorial1";
    }
    
    #region Hand Quadrant Checking
    
    /// <summary>
    /// Receive hand interaction data from SimpleInteractionBridge (same as HandVisualizer)
    /// This ensures cursor and quadrant detection are perfectly aligned
    /// </summary>
    private void OnHandInteractionReceived(SimpleInteractionBridge.HandInteractionData handData)
    {
        // Note: We receive screen coordinates, but SimpleInteractionBridge doesn't tell us which hand
        // For now, we'll track this as a single hand position and check if it matches our target
        // TODO: Enhance SimpleInteractionBridge to distinguish left/right hands
        
        // Store the position (this is screen coordinates)
        // For simplicity, we'll use this for whichever hand we're currently tracking
        bool isLeftQuadrant = (targetQuadrant == Quadrant.TopLeft || targetQuadrant == Quadrant.BottomLeft);
        
        if (isLeftQuadrant)
        {
            leftHandScreenPosition = handData.position;
            hasLeftHandData = handData.isValid;
        }
        else
        {
            rightHandScreenPosition = handData.position;
            hasRightHandData = handData.isValid;
        }
    }
    
    private void CheckHandQuadrant()
    {
        // Determine which hand to check based on target quadrant
        bool isLeftQuadrant = (targetQuadrant == Quadrant.TopLeft || targetQuadrant == Quadrant.BottomLeft);
        
        Vector2 handToCheck;
        bool hasHandData;
        
        if (isLeftQuadrant)
        {
            handToCheck = leftHandScreenPosition;
            hasHandData = hasLeftHandData;
        }
        else
        {
            handToCheck = rightHandScreenPosition;
            hasHandData = hasRightHandData;
        }
        
        // Check if we have valid hand data
        if (!hasHandData)
        {
            ResetHolding();
            return;
        }
        
        // Determine which quadrant the hand is in (USING SCREEN COORDINATES)
        Quadrant detectedQuadrant = GetQuadrantFromScreenPosition(handToCheck);
        
        if (detectedQuadrant != currentQuadrant)
        {
            // Changed quadrant
            currentQuadrant = detectedQuadrant;
            ResetHolding();
            
            if (currentQuadrant != Quadrant.None)
            {
                string handName = isLeftQuadrant ? "LEFT" : "RIGHT";
                DebugLogger.LogInfo($"[Tutorial1State] {handName} hand moved to {currentQuadrant} at screen position ({handToCheck.x:F1}, {handToCheck.y:F1})");
            }
        }
        
        // Check if pointing at the next required quadrant
        if (currentQuadrant == targetQuadrant && !completedQuadrants[(int)targetQuadrant])
        {
            if (!isHolding)
            {
                // Start holding
                isHolding = true;
                holdStartTime = Time.time;
                string handName = isLeftQuadrant ? "LEFT" : "RIGHT";
                DebugLogger.LogInfo($"[Tutorial1State] Started holding at {currentQuadrant} with {handName} hand");
                
                // Play sound for this quadrant
                PlayQuadrantSound(currentQuadrant);
            }
        }
        else
        {
            ResetHolding();
        }
    }
    
    /// <summary>
    /// Get quadrant from SCREEN COORDINATES (pixels)
    /// This matches how HandVisualizer displays the cursor
    /// </summary>
    private Quadrant GetQuadrantFromScreenPosition(Vector2 screenPos)
    {
        // Screen coordinates: (0,0) is bottom-left, (Screen.width, Screen.height) is top-right
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        
        bool isLeft = screenPos.x < centerX;
        bool isTop = screenPos.y > centerY;  // In screen coords, higher Y = higher on screen
        
        if (isTop && isLeft)
            return Quadrant.TopLeft;
        else if (isTop && !isLeft)
            return Quadrant.TopRight;
        else if (!isTop && !isLeft)
            return Quadrant.BottomRight;
        else if (!isTop && isLeft)
            return Quadrant.BottomLeft;
        
        return Quadrant.None;
    }
    
    private void ResetHolding()
    {
        if (isHolding)
        {
            isHolding = false;
            
            if (uiManager != null)
            {
                uiManager.UpdateProgressBar(0f);
            }
            
            DebugLogger.LogInfo("[Tutorial1State] Holding reset");
        }
    }
    
    private void OnQuadrantCompleted()
    {
        DebugLogger.LogInfo($"[Tutorial1State] Quadrant {targetQuadrant} completed!");
        
        // Mark as completed
        completedQuadrants[(int)targetQuadrant] = true;
        completedCount++;
        isHolding = false;
        
        // Play success sound
        if (outputFacade != null)
        {
            outputFacade.SendAudioEvent($"quadrant_{(int)targetQuadrant}_complete", 1f);
        }
        
        // Check if this was the 4th quadrant
        if (targetQuadrant == Quadrant.BottomLeft)
        {
            // All quadrants completed!
            OnTutorial1Complete();
        }
        else
        {
            // Move to next quadrant
            targetQuadrant = GetNextQuadrant();
            UpdateTutorialUI();
            
            if (uiManager != null)
            {
                uiManager.UpdateProgressBar(0f);
            }
        }
    }
    
    private Quadrant GetNextQuadrant()
    {
        // Sequence: TopLeft → TopRight → BottomRight → BottomLeft
        switch (targetQuadrant)
        {
            case Quadrant.TopLeft:
                return Quadrant.TopRight;
            case Quadrant.TopRight:
                return Quadrant.BottomRight;
            case Quadrant.BottomRight:
                return Quadrant.BottomLeft;
            default:
                return Quadrant.TopLeft;
        }
    }
    
    private void UpdateTutorialUI()
    {
        if (uiManager == null) return;
        
        string quadrantName = GetQuadrantDisplayName(targetQuadrant);
        string directionText = GetQuadrantDirectionText(targetQuadrant);
        
        // Determine which hand to use
        bool isLeftQuadrant = (targetQuadrant == Quadrant.TopLeft || targetQuadrant == Quadrant.BottomLeft);
        string handName = isLeftQuadrant ? "LEFT" : "RIGHT";
        
        string text = $"Tutorial 1: Point to Quadrants\n\n" +
                     $"Point your {handName} HAND to the {quadrantName}\n" +
                     $"({directionText})\n\n" +
                     $"Hold for 2 seconds\n\n" +
                     $"Progress: {completedCount}/4 quadrants";
        
        uiManager.ShowTutorialText(text);
    }
    
    private string GetQuadrantDisplayName(Quadrant q)
    {
        switch (q)
        {
            case Quadrant.TopLeft: return "TOP-LEFT";
            case Quadrant.TopRight: return "TOP-RIGHT";
            case Quadrant.BottomRight: return "BOTTOM-RIGHT";
            case Quadrant.BottomLeft: return "BOTTOM-LEFT";
            default: return "UNKNOWN";
        }
    }
    
    private string GetQuadrantDirectionText(Quadrant q)
    {
        switch (q)
        {
            case Quadrant.TopLeft: return "↖ Upper Left";
            case Quadrant.TopRight: return "↗ Upper Right";
            case Quadrant.BottomRight: return "↘ Lower Right";
            case Quadrant.BottomLeft: return "↙ Lower Left";
            default: return "";
        }
    }
    
    private void PlayQuadrantSound(Quadrant q)
    {
        if (outputFacade == null) return;
        
        // Send different audio events for each quadrant
        string soundName = $"quadrant_{(int)q}_entered";
        outputFacade.SendAudioEvent(soundName, 1f);
    }
    
    private void OnTutorial1Complete()
    {
        DebugLogger.LogInfo("[Tutorial1State] Tutorial 1 completed! All quadrants done!");
        
        // Show success message briefly
        if (uiManager != null)
        {
            uiManager.ShowTutorialText("Tutorial 1 Complete!\nGreat job! All quadrants completed!");
        }
        
        // Send completion signal
        if (outputFacade != null)
        {
            outputFacade.SendCustomTrigger("all_quadrants_complete", 1f);
        }
        
        // Transition to Tutorial 2
        manager.TransitionToState("Tutorial2");
    }
    
    #endregion
}

