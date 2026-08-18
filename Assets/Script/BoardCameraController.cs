/*
Summary:
BoardCameraController handles board navigation. It controls orthographic zoom,
binds manually assigned zoom UI, supports mouse/touch pan and mobile pinch zoom,
scales zoom-out to the current board size, and clamps movement so the view stays near the puzzle board.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BoardCameraController : MonoBehaviour
{
    private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();

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
    [SerializeField] private bool scaleMaxZoomSizeWithScreenAspect = true;
    [SerializeField] private Vector2 maxZoomReferenceResolution = new Vector2(1080f, 1920f);
    [SerializeField] private bool scaleMaxZoomSizeWithBoard = true;
    [Min(0f)]
    [SerializeField] private float maxZoomBoardPaddingCells = 1.5f;
    [Range(0f, 0.2f)]
    [SerializeField] private float maxZoomBoardLongSidePadding = 0.1f;

    [Header("Drag Limits")]
    [Min(0f)]
    [SerializeField] private float boardPadding = 0.75f;
    [SerializeField] private bool clampCameraToBoard = true;

    [Header("Mobile Touch")]
    [SerializeField] private bool allowTouchControls = true;
    [SerializeField] private bool allowOneFingerPan = true;
    [SerializeField] private bool allowPinchZoom = true;
    [Min(0f)]
    [SerializeField] private float touchPanStartThresholdPixels = 6f;
    [Range(0.25f, 2f)]
    [SerializeField] private float pinchZoomSensitivity = 1f;
    [Min(0f)]
    [SerializeField] private float touchArrowAvoidRadiusPixels = 44f;
    [Min(0f)]
    [SerializeField] private float mouseArrowAvoidRadiusPixels = 8f;

    [Header("Zoom UI")]
    [SerializeField] private Button manualZoomInButton = null;
    [SerializeField] private Slider manualZoomSlider = null;
    [SerializeField] private Button manualZoomOutButton = null;
    [SerializeField] private Button manualResetButton = null;
    [SerializeField] private bool resetButtonActiveInEditor = true;

    private Slider zoomSlider;
    private Vector3 defaultCameraPosition;
    private float defaultOrthographicSize;
    private bool hasDefaultView;
    private bool isDragging;
    private bool ignoreZoomSliderEvent;
    private bool isTouchDragging;
    private bool isPinching;
    private bool ignoreSingleTouchUntilReleased;
    private int activePanFingerId = -1;
    private float lastPinchDistance;
    private float lastEffectiveMaxZoomSize = -1f;
    private Vector2Int lastZoomLimitScreenSize = new Vector2Int(-1, -1);
    private Vector2 panStartScreenPosition;
    private Vector3 lastPointerWorldPosition;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        StartCoroutine(InitializeAfterSceneStart());
    }

    private void OnDisable()
    {
        UnbindManualControls();
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
        ApplyZoomLimitsIfNeeded(true);
        SaveDefaultView();
        ClampCameraPosition();
        BindManualControls();
        UpdateZoomSlider();
    }

    private void Update()
    {
        ApplyZoomLimitsIfNeeded(false);

        if (PauseMenuUI.IsGamePaused)
        {
            ResetPointerState();
            return;
        }

        if (allowTouchControls && Input.touchCount > 0)
        {
            HandleTouchGestures();
            return;
        }

        ResetTouchState();
        HandleMouseDrag();

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

        targetCamera.orthographicSize = defaultOrthographicSize;
        targetCamera.transform.position = defaultCameraPosition;
        ClampCameraPosition();
        UpdateZoomSlider();
    }

    public void ConfigureRuntimeTesterControls(
        Camera camera,
        GameManager manager,
        float testerZoomStep,
        float testerMinZoomSize,
        float testerMaxZoomSize,
        float testerPanPaddingCells)
    {
        targetCamera = camera != null ? camera : targetCamera;
        boardManager = manager != null ? manager : boardManager;
        zoomStep = Mathf.Max(0.05f, testerZoomStep);
        minZoomSize = Mathf.Max(0.5f, testerMinZoomSize);
        maxZoomSize = Mathf.Max(minZoomSize, testerMaxZoomSize);
        allowMouseWheelZoom = true;
        allowTouchControls = false;
        clampCameraToBoard = true;
        scaleMaxZoomSizeWithScreenAspect = false;
        float testerCellSize = boardManager != null ? Mathf.Max(0.0001f, boardManager.CellSize) : 1f;
        boardPadding = Mathf.Max(0f, testerPanPaddingCells) * testerCellSize;
        lastEffectiveMaxZoomSize = -1f;
        lastZoomLimitScreenSize = new Vector2Int(-1, -1);

        if (targetCamera == null)
        {
            return;
        }

        targetCamera.orthographic = true;
        ApplyZoomLimitsIfNeeded(true);
        SaveDefaultView();
        ClampCameraPosition();
        UpdateZoomSlider();
    }

    private void ResolveReferences()
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

    private void ZoomBySteps(float steps)
    {
        if (targetCamera == null)
        {
            return;
        }

        float effectiveMaxZoomSize = GetEffectiveMaxZoomSize();

        SetZoomSize(Mathf.Clamp(
            targetCamera.orthographicSize - steps * zoomStep,
            minZoomSize,
            effectiveMaxZoomSize));
    }

    private void SetZoomSize(float orthographicSize)
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.orthographicSize = Mathf.Clamp(orthographicSize, minZoomSize, GetEffectiveMaxZoomSize());
        ClampCameraPosition();
        UpdateZoomSlider();
    }

    private void SetZoomFromSlider(float zoom01)
    {
        if (ignoreZoomSliderEvent)
        {
            return;
        }

        float effectiveMaxZoomSize = GetEffectiveMaxZoomSize();
        float size = Mathf.Lerp(effectiveMaxZoomSize, minZoomSize, Mathf.Clamp01(zoom01));
        SetZoomSize(size);
    }

    private float GetEffectiveMaxZoomSize()
    {
        float safeMaxZoomSize = Mathf.Max(minZoomSize, maxZoomSize);
        float effectiveMaxZoomSize = GetScreenScaledMaxZoomSize(safeMaxZoomSize);

        if (scaleMaxZoomSizeWithBoard)
        {
            effectiveMaxZoomSize = Mathf.Max(effectiveMaxZoomSize, GetBoardFitMaxZoomSize());
        }

        return Mathf.Max(minZoomSize, effectiveMaxZoomSize);
    }

    private float GetScreenScaledMaxZoomSize(float safeMaxZoomSize)
    {
        if (!scaleMaxZoomSizeWithScreenAspect ||
            maxZoomReferenceResolution.x <= 0f ||
            maxZoomReferenceResolution.y <= 0f ||
            Screen.width <= 0 ||
            Screen.height <= 0)
        {
            return safeMaxZoomSize;
        }

        float referenceAspect = maxZoomReferenceResolution.x / maxZoomReferenceResolution.y;
        float currentAspect = GetCurrentCameraAspect();

        if (currentAspect <= 0.0001f)
        {
            return safeMaxZoomSize;
        }

        // Orthographic size controls vertical view. This keeps the zoomed-out horizontal view
        // matching the reference device where Max Zoom Size was tuned.
        float scaledMaxZoomSize = safeMaxZoomSize * (referenceAspect / currentAspect);
        return Mathf.Max(minZoomSize, scaledMaxZoomSize);
    }

    private float GetBoardFitMaxZoomSize()
    {
        if (boardManager == null)
        {
            return minZoomSize;
        }

        Bounds boardBounds = boardManager.GetBoardWorldBounds();

        if (boardBounds.size.x <= 0.0001f || boardBounds.size.y <= 0.0001f)
        {
            return minZoomSize;
        }

        float aspect = GetCurrentCameraAspect();
        float cellWorldSize = GetBoardCellWorldSize(boardBounds);
        float fixedPadding = cellWorldSize * maxZoomBoardPaddingCells;
        float longSidePaddingFactor = Mathf.Max(0.1f, maxZoomBoardLongSidePadding);
        float longSidePadding = Mathf.Max(boardBounds.size.x, boardBounds.size.y) * longSidePaddingFactor;
        float boardPaddingForZoom = Mathf.Max(0f, fixedPadding + longSidePadding);
        float verticalFitSize = boardBounds.size.y * 0.5f + boardPaddingForZoom;
        float horizontalFitSize = boardBounds.size.x * 0.5f / aspect + boardPaddingForZoom;

        // 15x15 on a 1080x1920 portrait screen still lands near the old 15 target:
        // 15 / (2 * 0.5625 aspect) + 1.5 cell padding + 10% long-side padding = about 16.3.
        // Tall or wide boards, such as 20x35, get extra breathing room from the long-side term.
        return Mathf.Max(minZoomSize, Mathf.Max(verticalFitSize, horizontalFitSize));
    }

    private float GetBoardCellWorldSize(Bounds boardBounds)
    {
        if (boardManager == null)
        {
            return 1f;
        }

        float fallbackCellSize = Mathf.Max(0.0001f, boardManager.CellSize);
        float xCellSize = boardManager.Width > 0 ? boardBounds.size.x / boardManager.Width : fallbackCellSize;
        float yCellSize = boardManager.Height > 0 ? boardBounds.size.y / boardManager.Height : fallbackCellSize;
        return Mathf.Max(0.0001f, Mathf.Max(xCellSize, yCellSize));
    }

    private float GetCurrentCameraAspect()
    {
        if (targetCamera != null && targetCamera.aspect > 0f)
        {
            return Mathf.Max(0.0001f, targetCamera.aspect);
        }

        if (Screen.width > 0 && Screen.height > 0)
        {
            return Mathf.Max(0.0001f, (float)Screen.width / Screen.height);
        }

        return Mathf.Max(0.0001f, maxZoomReferenceResolution.x / Mathf.Max(1f, maxZoomReferenceResolution.y));
    }

    private void ApplyZoomLimitsIfNeeded(bool force)
    {
        if (targetCamera == null)
        {
            return;
        }

        float effectiveMaxZoomSize = GetEffectiveMaxZoomSize();
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

        if (!force &&
            Mathf.Approximately(effectiveMaxZoomSize, lastEffectiveMaxZoomSize) &&
            screenSize == lastZoomLimitScreenSize)
        {
            return;
        }

        targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize, minZoomSize, effectiveMaxZoomSize);
        ClampCameraPosition();
        UpdateZoomSlider();
        lastEffectiveMaxZoomSize = effectiveMaxZoomSize;
        lastZoomLimitScreenSize = screenSize;
    }

    private void HandleMouseDrag()
    {
        if (targetCamera == null)
        {
            return;
        }

        if (PathArrow.IsAnyArrowHeld)
        {
            // Arrow hold-preview has priority over board dragging.
            isDragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUi(Input.mousePosition) || IsPointerOverArrow(Input.mousePosition, false))
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

    private void HandleTouchGestures()
    {
        if (targetCamera == null)
        {
            ResetTouchState();
            return;
        }

        if (PathArrow.IsAnyArrowHeld)
        {
            // Arrow hold-preview has priority over mobile board gestures too.
            ResetTouchState();
            return;
        }

        if (Input.touchCount >= 2 && allowPinchZoom)
        {
            HandlePinchZoom();
            return;
        }

        if (Input.touchCount == 1 && allowOneFingerPan)
        {
            HandleSingleTouchPan();
            return;
        }

        ResetTouchState();
    }

    private void HandleSingleTouchPan()
    {
        Touch touch = Input.GetTouch(0);

        if (ignoreSingleTouchUntilReleased)
        {
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                ignoreSingleTouchUntilReleased = false;
            }

            return;
        }

        if (touch.phase == TouchPhase.Began)
        {
            if (IsPointerOverUi(touch.position) || IsPointerOverArrow(touch.position, true))
            {
                isTouchDragging = false;
                activePanFingerId = -1;
                return;
            }

            isTouchDragging = false;
            activePanFingerId = touch.fingerId;
            panStartScreenPosition = touch.position;
            lastPointerWorldPosition = GetPointerWorldPosition(touch.position);
            return;
        }

        if (activePanFingerId != touch.fingerId)
        {
            return;
        }

        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            isTouchDragging = false;
            activePanFingerId = -1;
            return;
        }

        if (!isTouchDragging)
        {
            if ((touch.position - panStartScreenPosition).sqrMagnitude < touchPanStartThresholdPixels * touchPanStartThresholdPixels)
            {
                return;
            }

            isTouchDragging = true;
            lastPointerWorldPosition = GetPointerWorldPosition(touch.position);
        }

        Vector3 currentPointerWorldPosition = GetPointerWorldPosition(touch.position);
        Vector3 movement = lastPointerWorldPosition - currentPointerWorldPosition;
        targetCamera.transform.position += new Vector3(movement.x, movement.y, 0f);
        ClampCameraPosition();

        lastPointerWorldPosition = GetPointerWorldPosition(touch.position);
    }

    private void HandlePinchZoom()
    {
        Touch firstTouch = Input.GetTouch(0);
        Touch secondTouch = Input.GetTouch(1);

        if (!isPinching)
        {
            if (IsPointerOverUi(firstTouch.position) ||
                IsPointerOverUi(secondTouch.position) ||
                IsPointerOverArrow(firstTouch.position, true) ||
                IsPointerOverArrow(secondTouch.position, true))
            {
                ResetTouchState();
                ignoreSingleTouchUntilReleased = true;
                return;
            }

            isPinching = true;
            isTouchDragging = false;
            activePanFingerId = -1;
            lastPinchDistance = Vector2.Distance(firstTouch.position, secondTouch.position);
        }

        if (firstTouch.phase == TouchPhase.Ended ||
            firstTouch.phase == TouchPhase.Canceled ||
            secondTouch.phase == TouchPhase.Ended ||
            secondTouch.phase == TouchPhase.Canceled)
        {
            ResetTouchState();
            ignoreSingleTouchUntilReleased = true;
            return;
        }

        float currentDistance = Vector2.Distance(firstTouch.position, secondTouch.position);

        if (lastPinchDistance <= 0.01f || currentDistance <= 0.01f)
        {
            lastPinchDistance = currentDistance;
            return;
        }

        Vector2 pinchCenter = (firstTouch.position + secondTouch.position) * 0.5f;
        Vector3 worldPointBeforeZoom = GetPointerWorldPosition(pinchCenter);
        float zoomScale = Mathf.Pow(lastPinchDistance / currentDistance, pinchZoomSensitivity);

        SetZoomSize(targetCamera.orthographicSize * zoomScale);

        // Keep the board point under the fingers stable while zooming and moving the pinch center.
        Vector3 worldPointAfterZoom = GetPointerWorldPosition(pinchCenter);
        Vector3 centerCorrection = worldPointBeforeZoom - worldPointAfterZoom;
        targetCamera.transform.position += new Vector3(centerCorrection.x, centerCorrection.y, 0f);
        ClampCameraPosition();

        lastPinchDistance = currentDistance;
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

        // Clamp the camera center using the visible orthographic half-size, not just the board bounds.
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

    private static bool IsPointerOverUi(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = screenPosition
        };

        UiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, UiRaycastResults);
        return UiRaycastResults.Count > 0;
    }

    private bool IsPointerOverArrow(Vector2 screenPosition, bool isTouch)
    {
        float hitRadius = isTouch ? touchArrowAvoidRadiusPixels : mouseArrowAvoidRadiusPixels;
        return PathArrowInputUtility.IsScreenPositionOverArrow(targetCamera, screenPosition, null, hitRadius);
    }

    private void ResetPointerState()
    {
        isDragging = false;
        ResetTouchState();
    }

    private void ResetTouchState()
    {
        isTouchDragging = false;
        isPinching = false;
        activePanFingerId = -1;
        lastPinchDistance = 0f;

        if (Input.touchCount == 0)
        {
            ignoreSingleTouchUntilReleased = false;
        }
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

    private void UnbindManualControls()
    {
        if (manualZoomSlider != null)
        {
            manualZoomSlider.onValueChanged.RemoveListener(SetZoomFromSlider);
        }

        UnbindButton(manualZoomInButton, ZoomIn);
        UnbindButton(manualZoomOutButton, ZoomOut);
        UnbindButton(manualResetButton, ResetZoom);
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

    private void ApplyResetButtonActiveState()
    {
        if (manualResetButton == null)
        {
            return;
        }

        manualResetButton.gameObject.SetActive(resetButtonActiveInEditor);
    }

    private void UpdateZoomSlider()
    {
        if (zoomSlider == null || targetCamera == null)
        {
            return;
        }

        float effectiveMaxZoomSize = GetEffectiveMaxZoomSize();
        float zoom01 = Mathf.Approximately(effectiveMaxZoomSize, minZoomSize)
            ? 0f
            : Mathf.Clamp01((effectiveMaxZoomSize - targetCamera.orthographicSize) / (effectiveMaxZoomSize - minZoomSize));

        // Prevent slider value sync from recursively changing camera zoom.
        ignoreZoomSliderEvent = true;
        zoomSlider.value = zoom01;
        ignoreZoomSliderEvent = false;
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
