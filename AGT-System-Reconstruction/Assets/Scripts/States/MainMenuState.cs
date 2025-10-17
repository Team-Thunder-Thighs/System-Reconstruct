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
    private bool isInitialized = false;
    
    public void Enter(GameManager manager)
    {
        this.manager = manager;
        
        DebugLogger.LogInfo("[MainMenuState] Entering Main Menu");
        
        // TODO: Activate main menu UI panel
        // TODO: Load main menu music
        // TODO: Reset game data for new game
        manager.ResetGameData();
        
        // TODO: Subscribe to UI button events
        // Example: mainMenuUI.OnStartGameClicked += OnStartGameClicked;
        
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
        
        // Debug: Press '1' to go to Tutorial
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            OnStartTutorialClicked();
        }
        
        // Debug: Press '2' to go directly to Gameplay
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            OnStartGameClicked();
        }
    }
    
    public void Exit()
    {
        DebugLogger.LogInfo("[MainMenuState] Exiting Main Menu");
        
        // TODO: Deactivate main menu UI
        // TODO: Unsubscribe from UI events
        // TODO: Stop menu music
        
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
        DebugLogger.LogInfo("[MainMenuState] Starting Tutorial");
        manager.TransitionToState("Tutorial");
    }
    
    private void OnStartGameClicked()
    {
        DebugLogger.LogInfo("[MainMenuState] Starting Game");
        manager.TransitionToState("Gameplay");
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

