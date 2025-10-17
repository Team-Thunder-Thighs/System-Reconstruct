using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UIManager - Simple temporary UI manager for testing game states.
/// 
/// This is a minimal implementation to test the state machine.
/// In production, this would be expanded with proper UI panels, animations, etc.
/// 
/// Features:
/// - Tutorial text display
/// - Main menu buttons
/// - Simple UI element creation
/// </summary>
public class UIManager : MonoBehaviour
{
    #region Singleton
    
    public static UIManager Instance { get; private set; }
    
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
        
        InitializeUI();
    }
    
    #endregion
    
    #region UI Components
    
    [Header("UI Canvas")]
    [SerializeField] private Canvas mainCanvas;
    
    [Header("Main Menu")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button startTutorialButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button quitButton;
    
    [Header("Tutorial")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TextMeshProUGUI tutorialText;
    
    [Header("Progress Bar")]
    [SerializeField] private GameObject progressBarPanel;
    [SerializeField] private UnityEngine.UI.Slider progressSlider;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    
    #endregion
    
    #region Initialization
    
    void InitializeUI()
    {
        // Setup button listeners
        SetupButtonListeners();
        
        // Hide tutorial panel and progress bar initially
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
        
        if (progressBarPanel != null)
        {
            progressBarPanel.SetActive(false);
        }
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[UIManager] UI initialized");
        }
    }
    
    void SetupButtonListeners()
    {
        // Setup Start Tutorial button
        if (startTutorialButton != null)
        {
            startTutorialButton.onClick.RemoveAllListeners();
            startTutorialButton.onClick.AddListener(OnStartTutorialClicked);
        }
        else
        {
            DebugLogger.LogWarning("[UIManager] Start Tutorial button not assigned!");
        }
        
        // Setup Start Game button
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }
        else
        {
            DebugLogger.LogWarning("[UIManager] Start Game button not assigned!");
        }
        
        // Setup Quit button
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitClicked);
        }
        else
        {
            DebugLogger.LogWarning("[UIManager] Quit button not assigned!");
        }
    }
    
    #endregion
    
    #region Main Menu
    
    public void ShowMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
        
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[UIManager] Main menu shown");
        }
    }
    
    public void HideMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[UIManager] Main menu hidden");
        }
    }
    
    void OnStartTutorialClicked()
    {
        DebugLogger.LogInfo("[UIManager] Start Tutorial button clicked");
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TransitionToState("Tutorial1");
        }
    }
    
    void OnStartGameClicked()
    {
        DebugLogger.LogInfo("[UIManager] Start Game button clicked");
        
        if (GameManager.Instance != null)
        {
            // For now, just log - you can add gameplay state later
            DebugLogger.LogInfo("[UIManager] Gameplay not implemented yet");
        }
    }
    
    void OnQuitClicked()
    {
        DebugLogger.LogInfo("[UIManager] Quit button clicked");
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
    
    #endregion
    
    #region Tutorial
    
    public void ShowTutorialText(string text)
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
        }
        
        if (tutorialText != null)
        {
            tutorialText.text = text;
        }
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[UIManager] Tutorial text shown: {text}");
        }
    }
    
    public void HideTutorialText()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[UIManager] Tutorial text hidden");
        }
    }
    
    public void ShowProgressBar(float initialProgress = 0f)
    {
        if (progressBarPanel != null)
        {
            progressBarPanel.SetActive(true);
        }
        
        if (progressSlider != null)
        {
            progressSlider.value = initialProgress;
        }
        
        if (debugMode)
        {
            DebugLogger.LogInfo($"[UIManager] Progress bar shown at {(initialProgress * 100):F0}%");
        }
    }
    
    public void UpdateProgressBar(float progress)
    {
        if (progressSlider != null)
        {
            progressSlider.value = Mathf.Clamp01(progress);
        }
    }
    
    public void HideProgressBar()
    {
        if (progressBarPanel != null)
        {
            progressBarPanel.SetActive(false);
        }
        
        if (debugMode)
        {
            DebugLogger.LogInfo("[UIManager] Progress bar hidden");
        }
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    void OnDestroy()
    {
        // Remove button listeners
        if (startTutorialButton != null)
        {
            startTutorialButton.onClick.RemoveListener(OnStartTutorialClicked);
        }
        
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(OnStartGameClicked);
        }
        
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
        }
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    #endregion
}

