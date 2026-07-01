using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Handles orthographic camera zoom, reset, and drag-pan clamped to the path-arrow board bounds.
public class BoardCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private GameManager boardManager;

    [Header("Zoom")]
    [Min(0.1f)]
    [SerializeField] private float zoomStep = 1f;
    [Min(0.5f)]
    [SerializeField] private float minZoomSize = 2.5f;
    [Min(0.5f)]
    [SerializeField] private float maxZoomSize = 8f;
    [SerializeField] private bool allowMouseWheelZoom = true;

    [Header("Drag Limits")]
    [Min(0f)]
    [SerializeField] private float boardPadding = 0.75f;
    [SerializeField] private bool clampCameraToBoard = true;

    [Header("Manual Controls")]
    [SerializeField] private bool useManualControls = false;
    [SerializeField] private Button manualZoomInButton = null;
    [SerializeField] private Slider manualZoomSlider = null;
    [SerializeField] private Button manualZoomOutButton = null;
    [SerializeField] private Button manualResetButton = null;

    [Header("Runtime Controls")]
    [SerializeField] private bool createRuntimeButtons = true;
    [SerializeField] private bool resetButtonActiveInEditor = true;
    [SerializeField] private Vector2 buttonSize = new Vector2(110f, 46f);
    [SerializeField] private Vector2 buttonMargin = new Vector2(18f, 18f);
    [SerializeField] private Vector2 sliderSize = new Vector2(34f, 220f);
    [SerializeField] private Vector2 sliderMargin = new Vector2(18f, 18f);
    [SerializeField] private Vector2 zoomSignSize = new Vector2(42f, 42f);
    [SerializeField] private float zoomSignSpacing = 6f;

    private Slider zoomSlider;
    private Button createdResetButton;
    private Vector3 defaultCameraPosition;
    private float defaultOrthographicSize;
    private bool hasDefaultView;
    private bool isDragging;
    private bool ignoreZoomSliderEvent;
    private Vector3 lastPointerWorldPosition;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (boardManager == null)
        {
            boardManager = FindFirstObjectByType<GameManager>();
        }
    }

    private void Start()
    {
        StartCoroutine(InitializeAfterSceneStart());
    }

    private IEnumerator InitializeAfterSceneStart()
    {
        // Wait one frame so GameManager can build/frame the board first.
        yield return null;

        if (targetCamera == null)
        {
            enabled = false;
            yield break;
        }

        targetCamera.orthographic = true;
        SaveDefaultView();
        ClampCameraPosition();

        if (useManualControls)
        {
            BindManualControls();
        }
        else if (createRuntimeButtons)
        {
            CreateRuntimeControls();
        }

        UpdateZoomSlider();
    }

    private void Update()
    {
        HandleDrag();

        if (allowMouseWheelZoom)
        {
            float wheel = Input.mouseScrollDelta.y;

            if (Mathf.Abs(wheel) > 0.01f)
            {
                ZoomBySteps(wheel);
            }
        }
    }

    public void ZoomIn()
    {
        ZoomBySteps(1f);
    }

    public void ZoomOut()
    {
        ZoomBySteps(-1f);
    }

    public void ResetZoom()
    {
        if (!hasDefaultView)
        {
            SaveDefaultView();
        }

        targetCamera.orthographicSize = maxZoomSize;
        targetCamera.transform.position = defaultCameraPosition;
        ClampCameraPosition();
        UpdateZoomSlider();
    }

    private void ZoomBySteps(float steps)
    {
        if (targetCamera == null)
        {
            return;
        }

        SetZoomSize(Mathf.Clamp(
            targetCamera.orthographicSize - steps * zoomStep,
            minZoomSize,
            maxZoomSize));
    }

    private void SetZoomSize(float orthographicSize)
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.orthographicSize = Mathf.Clamp(orthographicSize, minZoomSize, maxZoomSize);
        ClampCameraPosition();
        UpdateZoomSlider();
    }

    private void SetZoomFromSlider(float zoom01)
    {
        if (ignoreZoomSliderEvent)
        {
            return;
        }

        float size = Mathf.Lerp(maxZoomSize, minZoomSize, Mathf.Clamp01(zoom01));
        SetZoomSize(size);
    }

    private void HandleDrag()
    {
        if (targetCamera == null)
        {
            return;
        }

        if (PathArrow.IsAnyArrowHeld)
        {
            isDragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUi() || IsPointerOverArrow())
            {
                return;
            }

            isDragging = true;
            lastPointerWorldPosition = GetPointerWorldPosition(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (!isDragging || !Input.GetMouseButton(0))
        {
            return;
        }

        Vector3 currentPointerWorldPosition = GetPointerWorldPosition(Input.mousePosition);
        Vector3 movement = lastPointerWorldPosition - currentPointerWorldPosition;
        targetCamera.transform.position += new Vector3(movement.x, movement.y, 0f);
        ClampCameraPosition();

        lastPointerWorldPosition = GetPointerWorldPosition(Input.mousePosition);
    }

    private Vector3 GetPointerWorldPosition(Vector3 screenPosition)
    {
        Vector3 pointer = screenPosition;
        pointer.z = Mathf.Abs(targetCamera.transform.position.z);
        return targetCamera.ScreenToWorldPoint(pointer);
    }

    private void ClampCameraPosition()
    {
        if (!clampCameraToBoard || boardManager == null || targetCamera == null)
        {
            return;
        }

        Bounds boardBounds = boardManager.GetBoardWorldBounds();
        boardBounds.Expand(new Vector3(boardPadding * 2f, boardPadding * 2f, 0f));

        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;
        Vector3 position = targetCamera.transform.position;

        position.x = ClampAxis(position.x, boardBounds.min.x, boardBounds.max.x, halfWidth);
        position.y = ClampAxis(position.y, boardBounds.min.y, boardBounds.max.y, halfHeight);
        targetCamera.transform.position = position;
    }

    private static float ClampAxis(float value, float min, float max, float halfViewSize)
    {
        float minPosition = min + halfViewSize;
        float maxPosition = max - halfViewSize;

        if (minPosition > maxPosition)
        {
            return (min + max) * 0.5f;
        }

        return Mathf.Clamp(value, minPosition, maxPosition);
    }

    private void SaveDefaultView()
    {
        defaultCameraPosition = targetCamera.transform.position;
        defaultOrthographicSize = targetCamera.orthographicSize;
        hasDefaultView = true;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsPointerOverArrow()
    {
        if (targetCamera == null)
        {
            return false;
        }

        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].GetComponent<PathArrowColliderProxy>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void BindManualControls()
    {
        zoomSlider = manualZoomSlider;

        if (zoomSlider != null)
        {
            zoomSlider.minValue = 0f;
            zoomSlider.maxValue = 1f;
            zoomSlider.wholeNumbers = false;
            zoomSlider.onValueChanged.RemoveListener(SetZoomFromSlider);
            zoomSlider.onValueChanged.AddListener(SetZoomFromSlider);
        }

        BindButton(manualZoomInButton, ZoomIn);
        BindButton(manualZoomOutButton, ZoomOut);
        BindButton(manualResetButton, ResetZoom);
        ApplyResetButtonActiveState();
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

    private void CreateRuntimeControls()
    {
        EnsureEventSystem();

        Canvas canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("CameraControlsCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        Vector2 horizontalSliderSize = GetHorizontalSliderSize();
        float totalWidth = zoomSignSize.x * 2f + horizontalSliderSize.x + zoomSignSpacing * 2f;
        float totalHeight = Mathf.Max(zoomSignSize.y, horizontalSliderSize.y);
        RectTransform zoomControls = CreateBottomCenterGroup(canvas.transform, "ZoomControls", new Vector2(totalWidth, totalHeight), sliderMargin.y);

        float zoomOutX = -totalWidth * 0.5f + zoomSignSize.x * 0.5f;
        float sliderX = zoomOutX + zoomSignSize.x * 0.5f + zoomSignSpacing + horizontalSliderSize.x * 0.5f;
        float zoomInX = totalWidth * 0.5f - zoomSignSize.x * 0.5f;

        Button zoomOutButton = CreateButton(
            zoomControls,
            "ZoomOutButton",
            "-",
            new Vector2(zoomOutX, 0f),
            zoomSignSize,
            30,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f));
        zoomOutButton.onClick.AddListener(ZoomOut);

        zoomSlider = CreateZoomSlider(
            zoomControls,
            new Vector2(sliderX, 0f),
            horizontalSliderSize,
            Slider.Direction.LeftToRight,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f));
        zoomSlider.onValueChanged.AddListener(SetZoomFromSlider);
        UpdateZoomSlider();

        Button zoomInButton = CreateButton(
            zoomControls,
            "ZoomInButton",
            "+",
            new Vector2(zoomInX, 0f),
            zoomSignSize,
            30,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f));
        zoomInButton.onClick.AddListener(ZoomIn);

        float resetOffsetX = sliderMargin.x + zoomSignSize.x + buttonMargin.x;
        createdResetButton = CreateButton(canvas.transform, "ResetZoomButton", "Reset", new Vector2(-resetOffsetX, buttonMargin.y));
        createdResetButton.onClick.AddListener(ResetZoom);
        ApplyResetButtonActiveState();
    }

    private void ApplyResetButtonActiveState()
    {
        Button resetButton = manualResetButton != null ? manualResetButton : createdResetButton;

        if (resetButton == null)
        {
            return;
        }

        resetButton.gameObject.SetActive(resetButtonActiveInEditor);
    }

    private Vector2 GetHorizontalSliderSize()
    {
        float width = Mathf.Max(80f, Mathf.Max(sliderSize.x, sliderSize.y));
        float height = Mathf.Max(24f, Mathf.Min(sliderSize.x, sliderSize.y));
        return new Vector2(width, height);
    }

    private static RectTransform CreateBottomCenterGroup(Transform parent, string name, Vector2 size, float bottomOffset)
    {
        GameObject groupObject = new GameObject(name);
        groupObject.transform.SetParent(parent, false);

        RectTransform rectTransform = groupObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = new Vector2(0f, bottomOffset);

        return rectTransform;
    }

    private Slider CreateZoomSlider(
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        Slider.Direction direction,
        Vector2 anchor,
        Vector2 pivot)
    {
        GameObject sliderObject = new GameObject("ZoomSlider");
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.anchorMin = anchor;
        sliderRect.anchorMax = anchor;
        sliderRect.pivot = pivot;
        sliderRect.sizeDelta = size;
        sliderRect.anchoredPosition = anchoredPosition;

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = direction;

        Image background = CreateSliderImage(sliderObject.transform, "Background", Vector2.zero, Vector2.one, new Color(0.08f, 0.09f, 0.11f, 0.82f));

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(8f, 8f);
        fillAreaRect.offsetMax = new Vector2(-8f, -8f);

        Image fill = CreateSliderImage(fillArea.transform, "Fill", Vector2.zero, Vector2.one, new Color(0.28f, 0.72f, 1f, 0.95f));

        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(0f, 8f);
        handleAreaRect.offsetMax = new Vector2(0f, -8f);

        Image handle = CreateSliderImage(handleArea.transform, "Handle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(24f, 24f);

        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;

        // Background receives raycasts; the fill is visual only so dragging feels clean.
        background.raycastTarget = true;
        fill.raycastTarget = false;

        return slider;
    }

    private static Image CreateSliderImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);

        RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        return CreateButton(parent, name, label, anchoredPosition, buttonSize, 22);
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, int fontSize)
    {
        return CreateButton(
            parent,
            name,
            label,
            anchoredPosition,
            size,
            fontSize,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f));
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        Vector2 anchor,
        Vector2 pivot)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.09f, 0.11f, 0.82f);

        Button button = buttonObject.AddComponent<Button>();

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObject.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return button;
    }

    private void UpdateZoomSlider()
    {
        if (zoomSlider == null || targetCamera == null)
        {
            return;
        }

        float zoom01 = Mathf.Approximately(maxZoomSize, minZoomSize)
            ? 0f
            : Mathf.Clamp01((maxZoomSize - targetCamera.orthographicSize) / (maxZoomSize - minZoomSize));

        ignoreZoomSliderEvent = true;
        zoomSlider.value = zoom01;
        ignoreZoomSliderEvent = false;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ApplyResetButtonActiveState();
    }
}
