using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// GameManager - The Context for the State pattern implementation.
/// 
/// This is the central orchestrator for the entire game flow. It maintains a reference to the
/// currently active IGameState and delegates all frame-by-frame logic to that state. The GameManager's
/// primary responsibility is to coordinate state transitions, ensuring a clean lifecycle for each state.
/// 
/// Responsibilities:
/// 1. Maintain the current state
/// 2. Delegate Update/FixedUpdate/LateUpdate calls to the current state
/// 3. Orchestrate state transitions (Exit → Switch → Enter)
/// 4. Provide access to shared game resources and references
/// 5. Track game flow history (for debugging)
/// 
/// Architecture:
/// - Singleton pattern for global access
/// - State pattern for game flow control
/// - Event-driven for loose coupling with other systems
/// 
/// The State pattern ensures that state-specific behavior is defined independently,
/// allowing new states to be added without impacting existing ones.
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton
    
    public static GameManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        Initialize();
    }
    
    #endregion
    
    #region Configuration
    
    [Header("Initial State")]
    [SerializeField] private string initialStateName = "MainMenu";
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showStateHistory = true;
    [SerializeField] private int maxHistoryLength = 10;
    
    #endregion
    
    #region State Management
    
    // Current active state
    private IGameState currentState;
    
    // State history for debugging
    private System.Collections.Generic.List<string> stateHistory = new System.Collections.Generic.List<string>();
    
    // State transition tracking
    private float stateEnterTime;
    private string currentStateName = "None";
    private string previousStateName = "None";
    
    #endregion
    
    #region Events
    
    [Header("State Events")]
    public UnityEvent<string> OnStateEntered;
    public UnityEvent<string> OnStateExited;
    public UnityEvent<string, string> OnStateTransitioned; // (from, to)
    
    #endregion
    
    #region Shared Resources (Access for States)
    
    // References to other systems that states might need
    private InputFacade inputFacade;
    private OutputFacade outputFacade;
    private SimpleInteractionBridge interactionBridge;
    
    // Game data that persists across states
    private int currentScore = 0;
    private int currentLevel = 1;
    private int correctMoves = 0;
    private int wrongMoves = 0;
    
    #endregion
    
    #region Initialization
    
    void Initialize()
    {
        // Get references to shared systems
        inputFacade = InputFacade.Instance;
        outputFacade = OutputFacade.Instance;
        interactionBridge = FindObjectOfType<SimpleInteractionBridge>();
        
        // Initialize events if null
        if (OnStateEntered == null) OnStateEntered = new UnityEvent<string>();
        if (OnStateExited == null) OnStateExited = new UnityEvent<string>();
        if (OnStateTransitioned == null) OnStateTransitioned = new UnityEvent<string, string>();
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[GameManager] GameManager initialized");
        }
        
        // Transition to initial state
        TransitionToInitialState();
    }
    
    void TransitionToInitialState()
    {
        // For now, we'll start with MainMenuState
        // You can change this to load from a saved state or configuration
        IGameState initialState = CreateStateByName(initialStateName);
        
        if (initialState != null)
        {
            TransitionToState(initialState);
        }
        else
        {
            DebugLogger.LogError($"[GameManager] Failed to create initial state: {initialStateName}");
        }
    }
    
    /// <summary>
    /// Factory method to create states by name
    /// This can be extended to support more sophisticated state creation
    /// </summary>
    IGameState CreateStateByName(string stateName)
    {
        switch (stateName)
        {
            case "MainMenu":
                return new MainMenuState();
            case "Tutorial1":
                return new Tutorial1State();
            case "Tutorial2":
                return new Tutorial2State();
            case "Tutorial":
                // Old tutorial state - redirect to Tutorial1
                return new Tutorial1State();
            case "Gameplay":
                // return new GameplayState();
                DebugLogger.LogWarning("[GameManager] Gameplay state not implemented yet");
                return null;
            case "Results":
                // return new ResultsState();
                DebugLogger.LogWarning("[GameManager] Results state not implemented yet");
                return null;
            default:
                DebugLogger.LogWarning($"[GameManager] Unknown state name: {stateName}, defaulting to MainMenu");
                return new MainMenuState();
        }
    }
    
    #endregion
    
    #region Unity Lifecycle (Delegation to Current State)
    
    void Update()
    {
        if (currentState != null)
        {
            currentState.Update();
        }
    }
    
    void FixedUpdate()
    {
        if (currentState != null)
        {
            currentState.FixedUpdate();
        }
    }
    
    void LateUpdate()
    {
        if (currentState != null)
        {
            currentState.LateUpdate();
        }
    }
    
    #endregion
    
    #region State Transition (Public API)
    
    /// <summary>
    /// Transition to a new game state.
    /// This orchestrates the clean lifecycle: Exit current → Switch → Enter new
    /// </summary>
    /// <param name="nextState">The state to transition to</param>
    public void TransitionToState(IGameState nextState)
    {
        if (nextState == null)
        {
            DebugLogger.LogError("[GameManager] Attempted to transition to null state");
            return;
        }
        
        string nextStateName = nextState.GetStateName();
        
        // Exit current state
        if (currentState != null)
        {
            if (debugMode)
            {
                float timeInState = Time.time - stateEnterTime;
                DebugLogger.LogInfo($"[GameManager] Exiting state: {currentStateName} (duration: {timeInState:F2}s)");
            }
            
            currentState.Exit();
            OnStateExited?.Invoke(currentStateName);
        }
        
        // Track state history
        previousStateName = currentStateName;
        currentStateName = nextStateName;
        AddToStateHistory(currentStateName);
        
        // Switch to new state
        currentState = nextState;
        stateEnterTime = Time.time;
        
        // Enter new state
        if (debugMode)
        {
            DebugLogger.LogInfo($"[GameManager] Entering state: {currentStateName}");
        }
        
        currentState.Enter(this);
        OnStateEntered?.Invoke(currentStateName);
        OnStateTransitioned?.Invoke(previousStateName, currentStateName);
    }
    
    /// <summary>
    /// Transition to a state by name (convenience method)
    /// </summary>
    public void TransitionToState(string stateName)
    {
        IGameState newState = CreateStateByName(stateName);
        if (newState != null)
        {
            TransitionToState(newState);
        }
    }
    
    #endregion
    
    #region State Query (Public API)
    
    /// <summary>
    /// Get the name of the current state
    /// </summary>
    public string GetCurrentStateName()
    {
        return currentStateName;
    }
    
    /// <summary>
    /// Get the current state instance
    /// </summary>
    public IGameState GetCurrentState()
    {
        return currentState;
    }
    
    /// <summary>
    /// Check if currently in a specific state
    /// </summary>
    public bool IsInState(string stateName)
    {
        return currentStateName == stateName;
    }
    
    /// <summary>
    /// Get time spent in current state
    /// </summary>
    public float GetTimeInCurrentState()
    {
        return Time.time - stateEnterTime;
    }
    
    #endregion
    
    #region Shared Resource Access (For States)
    
    /// <summary>
    /// Get reference to InputFacade
    /// </summary>
    public InputFacade GetInputFacade()
    {
        if (inputFacade == null)
            inputFacade = InputFacade.Instance;
        return inputFacade;
    }
    
    /// <summary>
    /// Get reference to OutputFacade
    /// </summary>
    public OutputFacade GetOutputFacade()
    {
        if (outputFacade == null)
            outputFacade = OutputFacade.Instance;
        return outputFacade;
    }
    
    /// <summary>
    /// Get reference to SimpleInteractionBridge
    /// </summary>
    public SimpleInteractionBridge GetInteractionBridge()
    {
        if (interactionBridge == null)
            interactionBridge = FindObjectOfType<SimpleInteractionBridge>();
        return interactionBridge;
    }
    
    #endregion
    
    #region Game Data Access (For States)
    
    /// <summary>
    /// Get/Set current score
    /// </summary>
    public int Score
    {
        get { return currentScore; }
        set { currentScore = value; }
    }
    
    /// <summary>
    /// Get/Set current level
    /// </summary>
    public int Level
    {
        get { return currentLevel; }
        set { currentLevel = value; }
    }
    
    /// <summary>
    /// Get/Set correct moves count
    /// </summary>
    public int CorrectMoves
    {
        get { return correctMoves; }
        set { correctMoves = value; }
    }
    
    /// <summary>
    /// Get/Set wrong moves count
    /// </summary>
    public int WrongMoves
    {
        get { return wrongMoves; }
        set { wrongMoves = value; }
    }
    
    /// <summary>
    /// Reset game data (for new game)
    /// </summary>
    public void ResetGameData()
    {
        currentScore = 0;
        currentLevel = 1;
        correctMoves = 0;
        wrongMoves = 0;
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[GameManager] Game data reset");
        }
    }
    
    #endregion
    
    #region State History (Debug Support)
    
    void AddToStateHistory(string stateName)
    {
        if (!showStateHistory) return;
        
        stateHistory.Add($"{stateName} ({Time.time:F2}s)");
        
        // Keep history limited
        if (stateHistory.Count > maxHistoryLength)
        {
            stateHistory.RemoveAt(0);
        }
    }
    
    /// <summary>
    /// Get state transition history
    /// </summary>
    public string[] GetStateHistory()
    {
        return stateHistory.ToArray();
    }
    
    /// <summary>
    /// Clear state history
    /// </summary>
    public void ClearStateHistory()
    {
        stateHistory.Clear();
    }
    
    #endregion
    
    #region Debug Visualization
    
    void OnGUI()
    {
        if (!debugMode || !Application.isPlaying) return;
        
        GUILayout.BeginArea(new Rect(10, 430, 400, 300));
        GUILayout.Box("Game Manager State");
        
        GUILayout.Label($"Current State: {currentStateName}");
        GUILayout.Label($"Time in State: {GetTimeInCurrentState():F2}s");
        GUILayout.Label($"Previous State: {previousStateName}");
        
        GUILayout.Space(10);
        GUILayout.Label($"Score: {currentScore} | Level: {currentLevel}");
        GUILayout.Label($"Correct: {correctMoves} | Wrong: {wrongMoves}");
        
        if (showStateHistory && stateHistory.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("State History:");
            foreach (var entry in stateHistory)
            {
                GUILayout.Label($"  {entry}");
            }
        }
        
        GUILayout.Space(10);
        
        // Quick state transition buttons for testing
        if (GUILayout.Button("→ Main Menu"))
        {
            TransitionToState("MainMenu");
        }
        if (GUILayout.Button("→ Tutorial 1"))
        {
            TransitionToState("Tutorial1");
        }
        if (GUILayout.Button("→ Tutorial 2"))
        {
            TransitionToState("Tutorial2");
        }
        
        GUILayout.EndArea();
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            // Exit current state before destroying
            if (currentState != null)
            {
                currentState.Exit();
            }
            
            Instance = null;
        }
    }
    
    #endregion
}

