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
/// </summary>
public class Tutorial1State : IGameState
{
    private GameManager manager;
    private UIManager uiManager;
    private InputFacade inputFacade;
    private OutputFacade outputFacade;
    private bool isInitialized = false;
    
    // Hand tracking
    private Pose currentPose;
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
        inputFacade = manager.GetInputFacade();
        outputFacade = manager.GetOutputFacade();
        uiManager = UIManager.Instance;
        
        // Subscribe to pose data
        if (inputFacade != null)
        {
            inputFacade.OnPoseDataReceived += OnPoseDataReceived;
        }
        
        // Initialize progress
        for (int i = 0; i < completedQuadrants.Length; i++)
        {
            completedQuadrants[i] = false;
        }
        completedCount = 0;
        targetQuadrant = Quadrant.TopLeft;
        
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
        if (inputFacade != null)
        {
            inputFacade.OnPoseDataReceived -= OnPoseDataReceived;
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
    
    private void OnPoseDataReceived(Pose newPose)
    {
        currentPose = newPose;
    }
    
    private void CheckHandQuadrant()
    {
        if (!currentPose.IsValid())
        {
            ResetHolding();
            return;
        }
        
        // Get both hand positions
        Vector3 leftWrist = currentPose.GetLandmark(BodyLandmark.LeftWrist);
        Vector3 rightWrist = currentPose.GetLandmark(BodyLandmark.RightWrist);
        
        // Determine which hand to use based on target quadrant
        Vector3 handToCheck = Vector3.zero;
        bool isLeftQuadrant = (targetQuadrant == Quadrant.TopLeft || targetQuadrant == Quadrant.BottomLeft);
        
        if (isLeftQuadrant)
        {
            // Use left hand for left quadrants
            handToCheck = leftWrist;
        }
        else
        {
            // Use right hand for right quadrants
            handToCheck = rightWrist;
        }
        
        // Check if we have valid hand
        if (handToCheck == Vector3.zero)
        {
            ResetHolding();
            return;
        }
        
        // Determine which quadrant the hand is in
        Quadrant detectedQuadrant = GetQuadrantFromPosition(handToCheck);
        
        if (detectedQuadrant != currentQuadrant)
        {
            // Changed quadrant
            currentQuadrant = detectedQuadrant;
            ResetHolding();
            
            if (currentQuadrant != Quadrant.None)
            {
                string handName = isLeftQuadrant ? "LEFT" : "RIGHT";
                DebugLogger.LogInfo($"[Tutorial1State] {handName} hand moved to {currentQuadrant}");
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
    
    private Quadrant GetQuadrantFromPosition(Vector3 handPos)
    {
        // Convert normalized coordinates to screen quadrants
        // X: -0.5 to 0.5 (left to right)
        // Y: -0.9 to -0.3 (top to bottom)
        
        float centerX = 0f;
        float centerY = -0.6f; // Center of Y range
        
        bool isLeft = handPos.x < centerX;
        bool isTop = handPos.y < centerY; // Y is inverted
        
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

