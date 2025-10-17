using UnityEngine;

/// <summary>
/// MainMenuState - The entry point of the game.
/// 
/// This state handles:
/// - Display of main menu UI
/// - Navigation to Tutorial or Gameplay
/// - Settings and options
/// - Game initialization
/// 
/// Example:
/// - Enter(): Activate main menu UI, play menu music
/// - Update(): Handle menu button clicks, navigation
/// - Exit(): Deactivate main menu UI, stop menu music
/// </summary>
public class MainMenuState : IGameState
{
    private GameManager manager;
    private UIManager uiManager;
    private bool isInitialized = false;
    
    public void Enter(GameManager manager)
    {
        this.manager = manager;
        
        DebugLogger.LogInfo("[MainMenuState] Entering Main Menu");
        
        // Get UI manager reference
        uiManager = UIManager.Instance;
        
        // Show main menu UI
        if (uiManager != null)
        {
            uiManager.ShowMainMenu();
        }
        
        // Reset game data for new game
        manager.ResetGameData();
        
        isInitialized = true;
        
        // Send signal to TouchDesigner
        if (manager.GetOutputFacade() != null)
        {
            manager.GetOutputFacade().SendCustomTrigger("main_menu_entered");
        }
    }
    
    public void Update()
    {
        if (!isInitialized) return;
        
        // Handle menu interactions
        // Example: Check for input, animate menu elements, etc.
        
        // Debug: Press '1' to go to Tutorial1
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            OnStartTutorialClicked();
        }
    }
    
    public void Exit()
    {
        DebugLogger.LogInfo("[MainMenuState] Exiting Main Menu");
        
        // Hide main menu UI
        if (uiManager != null)
        {
            uiManager.HideMainMenu();
        }
        
        isInitialized = false;
        
        // Send signal to TouchDesigner
        if (manager.GetOutputFacade() != null)
        {
            manager.GetOutputFacade().SendCustomTrigger("main_menu_exited");
        }
    }
    
    public string GetStateName()
    {
        return "MainMenu";
    }
    
    // UI Event Handlers
    private void OnStartTutorialClicked()
    {
        DebugLogger.LogInfo("[MainMenuState] Starting Tutorial 1");
        manager.TransitionToState("Tutorial1");
    }
    
    private void OnStartGameClicked()
    {
        DebugLogger.LogInfo("[MainMenuState] Starting Game (not implemented yet)");
        // manager.TransitionToState("Gameplay");
    }
    
    private void OnSettingsClicked()
    {
        DebugLogger.LogInfo("[MainMenuState] Opening Settings");
        // TODO: Open settings panel (could be a sub-state or overlay)
    }
    
    private void OnQuitClicked()
    {
        DebugLogger.LogInfo("[MainMenuState] Quitting Game");
        Application.Quit();
    }
}

