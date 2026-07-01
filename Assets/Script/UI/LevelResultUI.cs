using UnityEngine;
using UnityEngine.UI;

// Shows a simple win/loss popup and wires its button to next/restart level actions.
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
    [SerializeField] private Canvas targetCanvas = null;
    [SerializeField] private RectTransform resultPanel = null;
    [SerializeField] private Text titleText = null;
    [SerializeField] private Button actionButton = null;
    [SerializeField] private Text actionButtonText = null;

    [Header("Auto UI")]
    [SerializeField] private bool generateUiIfMissing = true;
    [SerializeField] private Vector2 panelSize = new Vector2(420f, 220f);

    [Header("Text")]
    [SerializeField] private string completeMessage = "Level Complete";
    [SerializeField] private string failedMessage = "Level Failed";
    [SerializeField] private string nextLevelButtonText = "Next Level";
    [SerializeField] private string restartButtonText = "Restart Level";

    [Header("Colors")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
    [SerializeField] private Color buttonColor = new Color(0.18f, 0.55f, 1f, 0.95f);
    [SerializeField] private Color textColor = Color.white;

    private ResultMode currentMode;

    private void Awake()
    {
        ResolveManager();
        EnsureUi();
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

    private void EnsureUi()
    {
        if (resultPanel != null || !generateUiIfMissing)
        {
            return;
        }

        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
        }

        if (targetCanvas == null)
        {
            Debug.LogWarning("LevelResultUI needs a Canvas. Create one in the scene or assign Target Canvas.", this);
            return;
        }

        CreateGeneratedUi(targetCanvas.transform);
    }

    private void CreateGeneratedUi(Transform parent)
    {
        GameObject panelObject = new GameObject("LevelResultPanel");
        panelObject.transform.SetParent(parent, false);

        resultPanel = panelObject.AddComponent<RectTransform>();
        resultPanel.anchorMin = new Vector2(0.5f, 0.5f);
        resultPanel.anchorMax = new Vector2(0.5f, 0.5f);
        resultPanel.pivot = new Vector2(0.5f, 0.5f);
        resultPanel.anchoredPosition = Vector2.zero;
        resultPanel.sizeDelta = panelSize;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = panelColor;

        titleText = CreateText(resultPanel, "ResultTitle", 34, new Vector2(0f, 46f), new Vector2(panelSize.x - 48f, 70f));
        actionButton = CreateButton(resultPanel, "ResultActionButton", new Vector2(0f, -58f), new Vector2(220f, 56f));
        actionButtonText = CreateButtonText(actionButton.transform);
    }

    private Text CreateText(Transform parent, string name, int fontSize, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.color = textColor;
        text.fontSize = fontSize;
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return text;
    }

    private Button CreateButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = buttonColor;

        return buttonObject.AddComponent<Button>();
    }

    private Text CreateButtonText(Transform parent)
    {
        Text text = CreateText(parent, "Label", 22, Vector2.zero, Vector2.zero);
        RectTransform rectTransform = text.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        return text;
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
        EnsureUi();
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
