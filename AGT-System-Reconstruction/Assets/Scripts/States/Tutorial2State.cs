using UnityEngine;
using BodyTracking.DataModel;
using Pose = BodyTracking.DataModel.Pose;

/// <summary>
/// Tutorial2State - Second tutorial step.
/// 
/// This state confirms the player can successfully complete the gesture again.
/// When successful, returns to MainMenuState.
/// 
/// Flow:
/// Tutorial1 → Tutorial2 (raise hands again) → MainMenu
/// </summary>
public class Tutorial2State : IGameState
{
    private GameManager manager;
    private UIManager uiManager;
    private InputFacade inputFacade;
    private bool isInitialized = false;
    
    // Hand tracking
    private Pose currentPose;
    private float handCheckInterval = 0.2f; // Check every 0.2 seconds
    private float lastCheckTime = 0f;
    
    // Success tracking
    private float successDuration = 2f; // Hold hands up for 2 seconds
    private float successStartTime = 0f;
    private bool isHoldingHandsUp = false;
    
    public void Enter(GameManager manager)
    {
        this.manager = manager;
        
        DebugLogger.LogInfo("[Tutorial2State] Entering Tutorial 2");
        
        // Get references
        inputFacade = manager.GetInputFacade();
        uiManager = UIManager.Instance;
        
        // Subscribe to pose data
        if (inputFacade != null)
        {
            inputFacade.OnPoseDataReceived += OnPoseDataReceived;
        }
        
        // Show tutorial UI
        if (uiManager != null)
        {
            uiManager.ShowTutorialText("Tutorial 2:\nYou have exited Tutorial 1 and entered Tutorial 2!\n\nRaise both hands above your head again\nto return to Main Menu");
        }
        
        isInitialized = true;
        
        // Send signal to TouchDesigner
        if (manager.GetOutputFacade() != null)
        {
            manager.GetOutputFacade().SendCustomTrigger("tutorial2_entered");
        }
    }
    
    public void Update()
    {
        if (!isInitialized) return;
        
        // Check hand position periodically
        if (Time.time - lastCheckTime >= handCheckInterval)
        {
            CheckHandPosition();
            lastCheckTime = Time.time;
        }
        
        // Check if player has held hands up long enough
        if (isHoldingHandsUp)
        {
            float holdDuration = Time.time - successStartTime;
            
            if (uiManager != null)
            {
                uiManager.ShowTutorialText($"Tutorial 2:\nYou have exited Tutorial 1 and entered Tutorial 2!\n\nRaise both hands above your head again\nto return to Main Menu\n\nHolding... {holdDuration:F1}s / {successDuration:F1}s");
            }
            
            if (holdDuration >= successDuration)
            {
                OnTutorial2Complete();
            }
        }
        
        // Debug: Press M to skip to main menu
        if (Input.GetKeyDown(KeyCode.M))
        {
            DebugLogger.LogInfo("[Tutorial2State] Skipping to Main Menu");
            OnTutorial2Complete();
        }
        
        // Debug: Press Escape to go back to main menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            manager.TransitionToState("MainMenu");
        }
    }
    
    public void Exit()
    {
        DebugLogger.LogInfo("[Tutorial2State] Exiting Tutorial 2");
        
        // Unsubscribe from events
        if (inputFacade != null)
        {
            inputFacade.OnPoseDataReceived -= OnPoseDataReceived;
        }
        
        // Clear UI
        if (uiManager != null)
        {
            uiManager.HideTutorialText();
        }
        
        isInitialized = false;
        
        // Send signal to TouchDesigner
        if (manager.GetOutputFacade() != null)
        {
            manager.GetOutputFacade().SendCustomTrigger("tutorial2_completed");
        }
    }
    
    public string GetStateName()
    {
        return "Tutorial2";
    }
    
    #region Hand Position Checking
    
    private void OnPoseDataReceived(Pose newPose)
    {
        currentPose = newPose;
    }
    
    private void CheckHandPosition()
    {
        if (!currentPose.IsValid())
        {
            ResetSuccess();
            return;
        }
        
        // Get hand positions
        Vector3 leftWrist = currentPose.GetLandmark(BodyLandmark.LeftWrist);
        Vector3 rightWrist = currentPose.GetLandmark(BodyLandmark.RightWrist);
        
        // Get head position (nose) for reference
        Vector3 nose = currentPose.GetLandmark(BodyLandmark.Nose);
        
        // Check if we have valid landmarks
        bool hasLeftHand = leftWrist != Vector3.zero;
        bool hasRightHand = rightWrist != Vector3.zero;
        bool hasHead = nose != Vector3.zero;
        
        if (!hasHead)
        {
            ResetSuccess();
            return;
        }
        
        // Check if both hands are above head
        bool leftHandAboveHead = hasLeftHand && leftWrist.y < nose.y; // Y is inverted in normalized coords
        bool rightHandAboveHead = hasRightHand && rightWrist.y < nose.y;
        
        bool bothHandsUp = leftHandAboveHead && rightHandAboveHead;
        
        if (bothHandsUp)
        {
            if (!isHoldingHandsUp)
            {
                // Just started holding hands up
                isHoldingHandsUp = true;
                successStartTime = Time.time;
                DebugLogger.LogInfo("[Tutorial2State] Both hands raised above head!");
            }
        }
        else
        {
            ResetSuccess();
        }
    }
    
    private void ResetSuccess()
    {
        if (isHoldingHandsUp)
        {
            isHoldingHandsUp = false;
            DebugLogger.LogInfo("[Tutorial2State] Hands lowered, resetting progress");
            
            if (uiManager != null)
            {
                uiManager.ShowTutorialText("Tutorial 2:\nYou have exited Tutorial 1 and entered Tutorial 2!\n\nRaise both hands above your head again\nto return to Main Menu");
            }
        }
    }
    
    private void OnTutorial2Complete()
    {
        DebugLogger.LogInfo("[Tutorial2State] Tutorial 2 completed! Returning to Main Menu");
        
        // Show success message briefly
        if (uiManager != null)
        {
            uiManager.ShowTutorialText("Tutorial 2 Complete!\nWell done!");
        }
        
        // Return to Main Menu
        manager.TransitionToState("MainMenu");
    }
    
    #endregion
}

