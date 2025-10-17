using UnityEngine;

/// <summary>
/// Interface for all game states in the State pattern implementation.
/// 
/// This contract defines the lifecycle methods that all concrete state classes must implement.
/// Each state represents a distinct phase of the game (MainMenu, Tutorial, Gameplay, Results, etc.)
/// and encapsulates all logic and data relevant only to that phase.
/// 
/// Design Benefits:
/// - Modularity: Each state is independent and can be developed/tested in isolation
/// - Maintainability: State-specific logic is encapsulated in its own class
/// - Extensibility: New states can be added without modifying existing code
/// - Clarity: Game flow is explicit and easy to understand
/// </summary>
public interface IGameState
{
    /// <summary>
    /// Called once when entering this state.
    /// Use this for setup: activate UI, load data, subscribe to events, etc.
    /// </summary>
    /// <param name="manager">Reference to the GameManager (context)</param>
    void Enter(GameManager manager);
    
    /// <summary>
    /// Called every frame while this state is active.
    /// Contains the per-frame logic for this state (input handling, animations, etc.)
    /// </summary>
    void Update();
    
    /// <summary>
    /// Called once when exiting this state.
    /// Use this for cleanup: deactivate UI, unsubscribe from events, save data, etc.
    /// </summary>
    void Exit();
    
    /// <summary>
    /// Optional: Called at fixed intervals (useful for physics-based states)
    /// </summary>
    void FixedUpdate() { }
    
    /// <summary>
    /// Optional: Called after all Update() calls (useful for camera or UI states)
    /// </summary>
    void LateUpdate() { }
    
    /// <summary>
    /// Get the name of this state (for debugging and logging)
    /// </summary>
    /// <returns>State name</returns>
    string GetStateName();
}

