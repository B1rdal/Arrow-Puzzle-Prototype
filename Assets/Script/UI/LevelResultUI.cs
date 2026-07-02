/*
Summary:
LevelResultUI shows the manually assigned win/loss popup. It listens to GameManager
events and switches the action button between loading the next level and restarting
the current level.
*/

using UnityEngine;
using UnityEngine.UI;

public class LevelResultUI : MonoBehaviour
{
    private enum ResultMode
    {
        None,
        Complete,
        Failed
    }

    [Header("References")]
    [SerializeField] private GameManager manager = null;
    [SerializeField] private RectTransform resultPanel = null;
    [SerializeField] private Text titleText = null;
    [SerializeField] private Button actionButton = null;
    [SerializeField] private Text actionButtonText = null;

    [Header("Text")]
    [SerializeField] private string completeMessage = "Level Complete";
    [SerializeField] private string failedMessage = "Level Failed";
    [SerializeField] private string nextLevelButtonText = "Next Level";
    [SerializeField] private string restartButtonText = "Restart Level";

    private ResultMode currentMode;

    private void Awake()
    {
        ResolveManager();
        Hide();
    }

    private void OnEnable()
    {
        ResolveManager();

        if (manager != null)
        {
            manager.AllArrowsEscaped += ShowComplete;
            manager.GameLost += ShowFailed;
            manager.LevelStarted += Hide;
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(HandleActionButtonClicked);
            actionButton.onClick.AddListener(HandleActionButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.AllArrowsEscaped -= ShowComplete;
            manager.GameLost -= ShowFailed;
            manager.LevelStarted -= Hide;
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(HandleActionButtonClicked);
        }
    }

    private void ResolveManager()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<GameManager>();
        }
    }

    private void ShowComplete()
    {
        Show(ResultMode.Complete, completeMessage, nextLevelButtonText);
    }

    private void ShowFailed()
    {
        Show(ResultMode.Failed, failedMessage, restartButtonText);
    }

    private void Show(ResultMode mode, string message, string buttonLabel)
    {
        currentMode = mode;

        if (titleText != null)
        {
            titleText.text = message;
        }

        if (actionButtonText != null)
        {
            actionButtonText.text = buttonLabel;
        }

        if (resultPanel != null)
        {
            resultPanel.gameObject.SetActive(true);
        }
    }

    private void Hide()
    {
        currentMode = ResultMode.None;

        if (resultPanel != null)
        {
            resultPanel.gameObject.SetActive(false);
        }
    }

    private void HandleActionButtonClicked()
    {
        if (manager == null)
        {
            Hide();
            return;
        }

        ResultMode mode = currentMode;
        Hide();

        // The same button changes behavior depending on whether the player won or lost.
        if (mode == ResultMode.Complete)
        {
            manager.LoadNextLevel();
        }
        else if (mode == ResultMode.Failed)
        {
            manager.RestartLevel();
        }
    }
}
