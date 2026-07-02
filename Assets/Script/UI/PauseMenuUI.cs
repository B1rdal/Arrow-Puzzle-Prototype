/*
Summary:
PauseMenuUI shows and controls the manually assigned pause menu. It pauses time,
blocks gameplay input through a shared pause flag, and handles resume/restart/exit
button actions.
*/

using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PauseMenuUI : MonoBehaviour
{
    public static bool IsGamePaused { get; private set; }

    [Header("References")]
    [SerializeField] private GameManager manager = null;
    [SerializeField] private Button pauseButton = null;
    [SerializeField] private RectTransform pauseMenuRoot = null;
    [SerializeField] private Text titleText = null;
    [SerializeField] private Button resumeButton = null;
    [SerializeField] private Button restartButton = null;
    [SerializeField] private Button exitButton = null;

    [Header("Text")]
    [SerializeField] private string titleLabel = "Paused";

    [Header("Input")]
    [SerializeField] private bool allowEscapeKey = true;

    private float previousTimeScale = 1f;
    private bool isPaused;

    private void Awake()
    {
        ResolveReferences();
        ApplyStaticText();
        SetMenuVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyStaticText();
        BindUiEvents();
        SubscribeManagerEvents();
        SetMenuVisible(false);
    }

    private void OnDisable()
    {
        UnbindUiEvents();
        UnsubscribeManagerEvents();
        ForceUnpauseAndHide();
    }

    private void Update()
    {
        if (allowEscapeKey && Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
            return;
        }

        PauseGame();
    }

    public void PauseGame()
    {
        if (isPaused || (manager != null && manager.LevelEnded))
        {
            return;
        }

        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        isPaused = true;
        IsGamePaused = true;
        SetMenuVisible(true);
    }

    public void ResumeGame()
    {
        ForceUnpauseAndHide();
    }

    public void RestartLevel()
    {
        ForceUnpauseAndHide();

        if (manager != null)
        {
            manager.RestartLevel();
        }
    }

    public void ExitGame()
    {
        ForceUnpauseAndHide();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ResolveReferences()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<GameManager>();
        }
    }

    private void ApplyStaticText()
    {
        if (titleText != null)
        {
            titleText.text = titleLabel;
        }
    }

    private void BindUiEvents()
    {
        BindButton(pauseButton, PauseGame);
        BindButton(resumeButton, ResumeGame);
        BindButton(restartButton, RestartLevel);
        BindButton(exitButton, ExitGame);
    }

    private void UnbindUiEvents()
    {
        UnbindButton(pauseButton, PauseGame);
        UnbindButton(resumeButton, ResumeGame);
        UnbindButton(restartButton, RestartLevel);
        UnbindButton(exitButton, ExitGame);
    }

    private void SubscribeManagerEvents()
    {
        if (manager == null)
        {
            return;
        }

        manager.LevelStarted += ForceUnpauseAndHide;
        manager.AllArrowsEscaped += ForceUnpauseAndHide;
        manager.GameLost += ForceUnpauseAndHide;
    }

    private void UnsubscribeManagerEvents()
    {
        if (manager == null)
        {
            return;
        }

        manager.LevelStarted -= ForceUnpauseAndHide;
        manager.AllArrowsEscaped -= ForceUnpauseAndHide;
        manager.GameLost -= ForceUnpauseAndHide;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    private void ForceUnpauseAndHide()
    {
        if (isPaused)
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            isPaused = false;
        }

        IsGamePaused = false;
        SetMenuVisible(false);
    }

    private void SetMenuVisible(bool visible)
    {
        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.gameObject.SetActive(visible);
        }

        if (pauseButton != null)
        {
            pauseButton.gameObject.SetActive(!visible);
        }
    }

}
