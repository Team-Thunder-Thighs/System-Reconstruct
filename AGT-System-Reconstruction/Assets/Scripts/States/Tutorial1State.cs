using UnityEngine;
using BodyTracking.DataModel;
using Pose = BodyTracking.DataModel.Pose;

/// <summary>
/// Tutorial1State - First tutorial step - 8-Direction Testing.
/// 
/// This state teaches the player to point their arms in 8 different directions.
/// Player must point to all 8 directions (holding for 2 seconds each).
/// When all 8 directions are completed, transitions to Tutorial2State.
/// 
/// Directions (8 total):
/// LEFT SIDE (4 directions with LEFT ARM):
///   1. Top
///   2. Right-Top
///   3. Right
///   4. Right-Bottom
/// 
/// RIGHT SIDE (4 directions with RIGHT ARM):
///   5. Top
///   6. Left-Top
///   7. Left
///   8. Left-Bottom
/// 
/// Uses ARM DIRECTION VECTORS calculated from:
/// - Shoulder → Elbow vector
/// - Shoulder → Wrist vector
/// Combined direction = average of both normalized vectors
/// 
/// Flow:
/// MainMenu → Tutorial1 (point to 8 directions) → Tutorial2
/// </summary>
public class Tutorial1State : IGameState
{
    private GameManager manager;
    private UIManager uiManager;
    private InputFacade inputFacade;
    private OutputFacade outputFacade;
    private bool isInitialized = false;
    
    // Pose tracking for arm direction vectors
    private Pose currentPose;
    private float directionCheckInterval = 0.1f; // Check every 0.1 seconds
    private float lastCheckTime = 0f;
    
    // Direction tracking (8 directions: 4 on left side, 4 on right side)
    private enum ArmDirection
    {
        None = 0,
        // Left side directions (LEFT ARM - 1-4)
        LeftTop = 1,         // Top
        LeftRightTop = 2,    // Right-Top (diagonal)
        LeftRight = 3,       // Right
        LeftRightBottom = 4, // Right-Bottom (diagonal)
        // Right side directions (RIGHT ARM - 5-8)
        RightTop = 5,        // Top
        RightLeftTop = 6,    // Left-Top (diagonal)
        RightLeft = 7,       // Left
        RightLeftBottom = 8  // Left-Bottom (diagonal)
    }
    
    private ArmDirection currentDirection = ArmDirection.None;
    private ArmDirection targetDirection = ArmDirection.LeftTop; // Start with Left arm pointing Top
    private float holdDuration = 2f; // Hold for 2 seconds
    private float holdStartTime = 0f;
    private bool isHolding = false;
    
    // Progress tracking
    private bool[] completedDirections = new bool[9]; // Index 0 unused, 1-8 for directions
    private int completedCount = 0;
    
    
    public void Enter(GameManager manager)
    {
        this.manager = manager;
        
        DebugLogger.LogInfo("[Tutorial1State] Entering Tutorial 1 - 8-Direction Testing (4 per side)");
        
        // Get references
        inputFacade = manager.GetInputFacade();
        outputFacade = manager.GetOutputFacade();
        uiManager = UIManager.Instance;
        
        // Subscribe to pose data for arm direction vectors
        if (inputFacade != null)
        {
            inputFacade.OnPoseDataReceived += OnPoseDataReceived;
            DebugLogger.LogInfo("[Tutorial1State] Subscribed to InputFacade pose data");
        }
        else
        {
            DebugLogger.LogError("[Tutorial1State] InputFacade not found!");
        }
        
        // Initialize progress
        for (int i = 0; i < completedDirections.Length; i++)
        {
            completedDirections[i] = false;
        }
        completedCount = 0;
        targetDirection = ArmDirection.LeftTop; // Start with left arm pointing top
        
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
        
        // Check arm direction periodically
        if (Time.time - lastCheckTime >= directionCheckInterval)
        {
            CheckArmDirection();
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
                OnDirectionCompleted();
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
    
    #region Arm Direction Checking
    
    /// <summary>
    /// Receive pose data from InputFacade to calculate arm direction vectors
    /// </summary>
    private void OnPoseDataReceived(Pose pose)
    {
        // Store the current pose for direction calculation
        currentPose = pose;
    }
    
    private void CheckArmDirection()
    {
        // Check if we have valid pose data
        if (!currentPose.IsValid())
        {
            ResetHolding();
            return;
        }
        
        // Determine which arm to check based on target direction
        bool isLeftArm = ((int)targetDirection >= 1 && (int)targetDirection <= 4);
        
        // Calculate arm direction vector
        Vector3 armDirection;
        string armName;
        
        if (isLeftArm)
        {
            // LEFT arm: shoulder→elbow + shoulder→wrist
            armDirection = CalculateArmDirection(
                currentPose.GetLandmark(BodyLandmark.LeftShoulder),
                currentPose.GetLandmark(BodyLandmark.LeftElbow),
                currentPose.GetLandmark(BodyLandmark.LeftWrist)
            );
            armName = "LEFT";
        }
        else
        {
            // RIGHT arm: shoulder→elbow + shoulder→wrist
            armDirection = CalculateArmDirection(
                currentPose.GetLandmark(BodyLandmark.RightShoulder),
                currentPose.GetLandmark(BodyLandmark.RightElbow),
                currentPose.GetLandmark(BodyLandmark.RightWrist)
            );
            armName = "RIGHT";
        }
        
        // Check if arm direction is valid
        if (armDirection == Vector3.zero)
        {
            ResetHolding();
            return;
        }
        
        // Determine which direction the arm is pointing
        ArmDirection detectedDirection = GetDirectionFromVector(armDirection, isLeftArm);
        
        if (detectedDirection != currentDirection)
        {
            // Changed direction
            currentDirection = detectedDirection;
            ResetHolding();
            
            if (currentDirection != ArmDirection.None)
            {
                DebugLogger.LogInfo($"[Tutorial1State] {armName} arm pointing {GetDirectionName(currentDirection)} - vector({armDirection.x:F2}, {armDirection.y:F2}, {armDirection.z:F2})");
            }
        }
        
        // Check if pointing at the next required direction
        if (currentDirection == targetDirection && !completedDirections[(int)targetDirection])
        {
            if (!isHolding)
            {
                // Start holding
                isHolding = true;
                holdStartTime = Time.time;
                DebugLogger.LogInfo($"[Tutorial1State] Started holding at {GetDirectionName(currentDirection)} with {armName} arm");
                
                // Play sound for this direction
                PlayDirectionSound(currentDirection);
            }
        }
        else
        {
            ResetHolding();
        }
    }
    
    /// <summary>
    /// Calculate combined arm direction from shoulder, elbow, and wrist positions
    /// Same logic as HandVisualizer
    /// </summary>
    private Vector3 CalculateArmDirection(Vector3 shoulder, Vector3 elbow, Vector3 wrist)
    {
        // Check if all landmarks are valid
        if (shoulder == Vector3.zero || elbow == Vector3.zero || wrist == Vector3.zero)
        {
            return Vector3.zero;
        }
        
        // Calculate direction vectors in world space
        Vector3 shoulderToElbow = (elbow - shoulder).normalized;
        Vector3 shoulderToWrist = (wrist - shoulder).normalized;
        
        // Calculate COMBINED direction vector (average of both)
        Vector3 combinedDirection = (shoulderToElbow + shoulderToWrist) / 2f;
        return combinedDirection.normalized;
    }
    
    /// <summary>
    /// Determine which of 8 directions the arm vector is pointing
    /// LEFT SIDE: Top, Right-Top, Right, Right-Bottom
    /// RIGHT SIDE: Top, Left-Top, Left, Left-Bottom
    /// Uses XY plane projection
    /// </summary>
    private ArmDirection GetDirectionFromVector(Vector3 direction, bool isLeftArm)
    {
        // IMPORTANT: Invert Y because TouchDesigner coordinate system is flipped
        // In TD: positive Y is DOWN, but we want positive Y to be UP
        float adjustedY = -direction.y;
        
        // Calculate angle in degrees (0° = East, 90° = North, 180° = West, 270° = South)
        float angle = Mathf.Atan2(adjustedY, direction.x) * Mathf.Rad2Deg;
        
        // Normalize to 0-360
        if (angle < 0) angle += 360f;
        
        // Define 4 direction zones (90° each, centered on cardinal/ordinal directions)
        // Top: 45° - 135° (centered at 90°)
        // Right: 315° - 45° (centered at 0°/360°)
        // Bottom: 225° - 315° (centered at 270°)
        // Left: 135° - 225° (centered at 180°)
        
        if (isLeftArm)
        {
            // LEFT SIDE: Top, Right-Top, Right, Right-Bottom
            if (angle >= 67.5f && angle < 112.5f)
                return ArmDirection.LeftTop;          // 90° - Top
            else if (angle >= 22.5f && angle < 67.5f)
                return ArmDirection.LeftRightTop;     // 45° - Right-Top (diagonal)
            else if (angle >= 337.5f || angle < 22.5f)
                return ArmDirection.LeftRight;        // 0° - Right
            else if (angle >= 292.5f && angle < 337.5f)
                return ArmDirection.LeftRightBottom;  // 315° - Right-Bottom (diagonal)
        }
        else
        {
            // RIGHT SIDE: Top, Left-Top, Left, Left-Bottom
            if (angle >= 67.5f && angle < 112.5f)
                return ArmDirection.RightTop;         // 90° - Top
            else if (angle >= 112.5f && angle < 157.5f)
                return ArmDirection.RightLeftTop;     // 135° - Left-Top (diagonal)
            else if (angle >= 157.5f && angle < 202.5f)
                return ArmDirection.RightLeft;        // 180° - Left
            else if (angle >= 202.5f && angle < 247.5f)
                return ArmDirection.RightLeftBottom;  // 225° - Left-Bottom (diagonal)
        }
        
        return ArmDirection.None;
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
    
    private void OnDirectionCompleted()
    {
        DebugLogger.LogInfo($"[Tutorial1State] Direction {GetDirectionName(targetDirection)} completed!");
        
        // Mark as completed
        completedDirections[(int)targetDirection] = true;
        completedCount++;
        isHolding = false;
        
        // Play success sound
        if (outputFacade != null)
        {
            outputFacade.SendAudioEvent($"direction_{(int)targetDirection}_complete", 1f);
        }
        
        // Check if all 8 directions are complete
        if (completedCount >= 8)
        {
            // All directions completed!
            OnTutorial1Complete();
        }
        else
        {
            // Move to next direction
            targetDirection = GetNextDirection();
            UpdateTutorialUI();
            
            if (uiManager != null)
            {
                uiManager.UpdateProgressBar(0f);
            }
        }
    }
    
    private ArmDirection GetNextDirection()
    {
        // Sequence: 1→2→3→4→5→6→7→8
        // Left: N→NE→E→SE, then Right: N→NW→W→SW
        int nextIndex = (int)targetDirection + 1;
        if (nextIndex > 8) nextIndex = 1;
        return (ArmDirection)nextIndex;
    }
    
    private void UpdateTutorialUI()
    {
        if (uiManager == null) return;
        
        string directionName = GetDirectionName(targetDirection);
        string visualIndicator = GetDirectionVisualIndicator(targetDirection);
        
        // Determine which arm/side to use
        bool isLeftArm = ((int)targetDirection >= 1 && (int)targetDirection <= 4);
        string armName = isLeftArm ? "LEFT ARM" : "RIGHT ARM";
        string sideName = isLeftArm ? "LEFT SIDE" : "RIGHT SIDE";
        
        string text = $"Tutorial 1: 8-Direction Test\n\n" +
                     $"{sideName}\n" +
                     $"Point your {armName} to: {directionName}\n" +
                     $"{visualIndicator}\n\n" +
                     $"Hold for 2 seconds\n\n" +
                     $"Progress: {completedCount}/8 directions";
        
        uiManager.ShowTutorialText(text);
    }
    
    private string GetDirectionName(ArmDirection direction)
    {
        switch (direction)
        {
            // Left side directions
            case ArmDirection.LeftTop: return "TOP";
            case ArmDirection.LeftRightTop: return "RIGHT-TOP";
            case ArmDirection.LeftRight: return "RIGHT";
            case ArmDirection.LeftRightBottom: return "RIGHT-BOTTOM";
            // Right side directions
            case ArmDirection.RightTop: return "TOP";
            case ArmDirection.RightLeftTop: return "LEFT-TOP";
            case ArmDirection.RightLeft: return "LEFT";
            case ArmDirection.RightLeftBottom: return "LEFT-BOTTOM";
            default: return "UNKNOWN";
        }
    }
    
    private string GetDirectionVisualIndicator(ArmDirection direction)
    {
        switch (direction)
        {
            // Left side directions
            case ArmDirection.LeftTop: return "↑";
            case ArmDirection.LeftRightTop: return "↗";
            case ArmDirection.LeftRight: return "→";
            case ArmDirection.LeftRightBottom: return "↘";
            // Right side directions
            case ArmDirection.RightTop: return "↑";
            case ArmDirection.RightLeftTop: return "↖";
            case ArmDirection.RightLeft: return "←";
            case ArmDirection.RightLeftBottom: return "↙";
            default: return "";
        }
    }
    
    private void PlayDirectionSound(ArmDirection direction)
    {
        if (outputFacade == null) return;
        
        // Send different audio events for each direction
        string soundName = $"direction_{(int)direction}_entered";
        outputFacade.SendAudioEvent(soundName, 1f);
    }
    
    private void OnTutorial1Complete()
    {
        DebugLogger.LogInfo("[Tutorial1State] Tutorial 1 completed! All 8 directions done!");
        
        // Show success message briefly
        if (uiManager != null)
        {
            uiManager.ShowTutorialText("Tutorial 1 Complete!\nGreat job! All 8 directions completed!");
        }
        
        // Send completion signal
        if (outputFacade != null)
        {
            outputFacade.SendCustomTrigger("all_directions_complete", 1f);
        }
        
        // Transition to Tutorial 2
        manager.TransitionToState("Tutorial2");
    }
    
    #endregion
}

