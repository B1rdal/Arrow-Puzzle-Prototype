/*
Summary:
PathArrow is one runtime arrow on the board. It draws the line and arrow head,
handles press/hold/release input, shows preview and blocked feedback, then plays the
escape/fade animation after GameManager approves the move.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PathArrow : MonoBehaviour
{
    // BoardCameraController checks this so dragging the board does not fight arrow holding.
    private static int activeHoldPreviewCount;

    [Header("Rendering")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform headTransform;
    [SerializeField] private MeshRenderer headRenderer;
    [SerializeField] private Color blockedColor = new Color(1f, 0.25f, 0.2f);

    [Header("Feedback")]
    [SerializeField] private float blockedShakeDistance = 0.08f;
    [SerializeField] private float blockedFeedbackDuration = 0.18f;

    [Header("Hold Preview")]
    [SerializeField] private Color holdHighlightColor = Color.white;
    [Range(0f, 1f)]
    [SerializeField] private float holdHighlightWhiteness = 0.35f;
    [Min(1f)]
    [SerializeField] private float holdLineWidthMultiplier = 1.18f;
    [Min(1f)]
    [SerializeField] private float holdHeadScaleMultiplier = 1.12f;
    [Min(1f)]
    [SerializeField] private float previewBeamLength = 30f;
    [Range(0.05f, 1f)]
    [SerializeField] private float previewBeamWidthMultiplier = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float previewBeamAlpha = 0.24f;

    private readonly List<Vector2Int> occupiedCells = new List<Vector2Int>();

    private GameManager manager;
    private PathArrowAnimation pathAnimation;
    private PathArrowSegmentColliderSpawner2D colliderSpawner;
    private PathArrowStyleData styleData;
    private Material runtimeMaterial;
    private Material headMaterial;
    private LineRenderer previewBeamRenderer;
    private Material previewBeamMaterial;
    private Color normalColor = Color.white;
    private Coroutine feedbackRoutine;
    private Coroutine escapeCompletedRoutine;
    private Coroutine pressRoutine;
    private Rect boardExitBounds;
    private float outsideGridMargin = 0.5f;
    private float outsideGridFadeDelay = 1f;
    private float fadeOutDuration = 0.25f;
    private bool shortenBeforeExit = true;
    private float exitTargetVisibleLength = 1.6f;
    private float shorteningTailSpeedMultiplier = 2.25f;
    private float baseLineThickness = 0.28f;
    private bool inputEnabled = true;
    private bool collidersEnabled = true;
    private bool blockedHighlightActive;
    private bool holdPreviewActive;
    private float headSize = 0.7f;
    private float headSizeMultiplier = 2.2f;
    private float headTipLength = 0.56f;
    private float headBaseBackLength = 0.22f;
    private float headHalfWidth = 0.23f;

    public string Id { get; private set; }
    public Vector2Int HeadGridPosition { get; private set; }
    public Vector2Int ExitGridDirection { get; private set; }
    public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;
    public bool IsAnimating => pathAnimation != null && pathAnimation.IsPlaying;
    public static bool IsAnyArrowHeld => activeHoldPreviewCount > 0;

    // Called by GameManager after level data is validated and converted to local positions.
    public void Initialize(
        GameManager gameManager,
        string id,
        IReadOnlyList<Vector2Int> gridPoints,
        IReadOnlyList<Vector2Int> cells,
        Vector3[] localPositions,
        Color color,
        PathArrowStyleData arrowStyle,
        float lineThickness,
        float moveSpeed,
        Rect localBoardBounds,
        float outsideMargin,
        float fadeDelay,
        float fadeDuration,
        bool shortenPathBeforeExit,
        float targetVisibleLength,
        float tailSpeedMultiplier)
    {
        manager = gameManager;
        styleData = arrowStyle;
        ApplyStyleData();
        Id = id;
        normalColor = color;
        inputEnabled = true;
        baseLineThickness = Mathf.Max(0.01f, lineThickness);
        headSize = Mathf.Max(0.2f, lineThickness * headSizeMultiplier);
        boardExitBounds = localBoardBounds;
        outsideGridMargin = Mathf.Max(0f, outsideMargin);
        outsideGridFadeDelay = Mathf.Max(0f, fadeDelay);
        fadeOutDuration = Mathf.Max(0.01f, fadeDuration);
        shortenBeforeExit = shortenPathBeforeExit;
        exitTargetVisibleLength = Mathf.Max(0.1f, targetVisibleLength);
        shorteningTailSpeedMultiplier = Mathf.Max(1f, tailSpeedMultiplier);
        collidersEnabled = true;
        blockedHighlightActive = false;
        holdPreviewActive = false;

        occupiedCells.Clear();
        occupiedCells.AddRange(cells);

        HeadGridPosition = gridPoints[gridPoints.Count - 1];
        ExitGridDirection = PathArrowUtility.GetExitDirection(gridPoints);

        EnsureLineRenderer(lineThickness);
        EnsureAnimation(moveSpeed);
        EnsureColliderSpawner(lineThickness);
        EnsureHead();
        EnsurePreviewBeam();

        lineRenderer.positionCount = localPositions.Length;
        lineRenderer.SetPositions(localPositions);
        ApplyRestingVisualState();
        colliderSpawner.UpdateSegments();
        UpdateHeadPose();
    }

    public void HandlePressStarted()
    {
        if (PauseMenuUI.IsGamePaused || !inputEnabled || manager == null || IsAnimating)
        {
            return;
        }

        if (pressRoutine != null)
        {
            StopCoroutine(pressRoutine);
        }

        pressRoutine = StartCoroutine(PressRoutine());
    }

    public void PlayEscape()
    {
        inputEnabled = false;
        CancelHoldPreview();

        if (pathAnimation == null)
        {
            return;
        }

        pathAnimation.PlayForward(
            new Vector3(ExitGridDirection.x, ExitGridDirection.y, 0f),
            boardExitBounds,
            outsideGridMargin);
    }

    public void PlayBlockedFeedback()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(BlockedFeedbackRoutine());
    }

    public void SetBlockedHighlight(bool active)
    {
        blockedHighlightActive = active;

        if (!holdPreviewActive)
        {
            ApplyRestingVisualState();
        }
    }

    private IEnumerator PressRoutine()
    {
        bool shouldActivateOnRelease = true;
        SetHoldPreview(true);

        while (Input.GetMouseButton(0))
        {
            if (PauseMenuUI.IsGamePaused)
            {
                shouldActivateOnRelease = false;
                SetHoldPreview(false);
                break;
            }

            if (!IsPointerOverThisArrow())
            {
                // Leaving the arrow cancels this press; releasing over another arrow should not trigger it.
                shouldActivateOnRelease = false;
                SetHoldPreview(false);

                while (Input.GetMouseButton(0))
                {
                    yield return null;
                }

                break;
            }

            yield return null;
        }

        if (shouldActivateOnRelease && !IsPointerOverThisArrow())
        {
            // Final release-position check catches fast drags that leave on the same frame as release.
            shouldActivateOnRelease = false;
        }

        SetHoldPreview(false);
        pressRoutine = null;

        if (shouldActivateOnRelease && !PauseMenuUI.IsGamePaused && inputEnabled && manager != null && !IsAnimating)
        {
            manager.TryEscape(this);
        }
    }

    private void SetHoldPreview(bool active)
    {
        if (holdPreviewActive == active)
        {
            return;
        }

        holdPreviewActive = active;

        // Count active holds globally so camera panning can be disabled while an arrow is held.
        if (holdPreviewActive)
        {
            activeHoldPreviewCount++;
        }
        else
        {
            activeHoldPreviewCount = Mathf.Max(0, activeHoldPreviewCount - 1);
        }

        if (lineRenderer != null)
        {
            float widthMultiplier = holdPreviewActive ? holdLineWidthMultiplier : 1f;
            lineRenderer.startWidth = baseLineThickness * widthMultiplier;
            lineRenderer.endWidth = baseLineThickness * widthMultiplier;
        }

        if (headTransform != null)
        {
            float scaleMultiplier = holdPreviewActive ? holdHeadScaleMultiplier : 1f;
            headTransform.localScale = Vector3.one * headSize * scaleMultiplier;
        }

        if (holdPreviewActive)
        {
            SetColor(GetHoldPreviewColor());
            ShowPreviewBeam();
        }
        else
        {
            HidePreviewBeam();
            ApplyRestingVisualState();
        }
    }

    private void CancelHoldPreview()
    {
        if (pressRoutine != null)
        {
            StopCoroutine(pressRoutine);
            pressRoutine = null;
        }

        SetHoldPreview(false);
    }

    private IEnumerator BlockedFeedbackRoutine()
    {
        Vector3 startPosition = transform.localPosition;
        Vector3 exitDirection = new Vector3(ExitGridDirection.x, ExitGridDirection.y, 0f);

        if (exitDirection.sqrMagnitude <= 0.0001f)
        {
            exitDirection = Vector3.right;
        }

        SetColor(blockedColor);
        float elapsed = 0f;

        // Nudge in the exit direction so blocked feedback reads as "trying to move."
        while (elapsed < blockedFeedbackDuration)
        {
            float progress = elapsed / blockedFeedbackDuration;
            float strength = 1f - progress;
            float offset = Mathf.Sin(progress * Mathf.PI * 8f) * blockedShakeDistance * strength;
            transform.localPosition = startPosition + exitDirection.normalized * offset;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = startPosition;
        ApplyRestingVisualState();
        feedbackRoutine = null;
    }

    private void EnsureLineRenderer(float lineThickness)
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = false;
        lineRenderer.startWidth = baseLineThickness;
        lineRenderer.endWidth = baseLineThickness;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 8;
        lineRenderer.sortingOrder = 10;

        if (runtimeMaterial == null)
        {
            runtimeMaterial = CreateUnlitColorMaterial();
        }

        // Keep the line material white so LineRenderer vertex color controls the final arrow color.
        SetMaterialColor(runtimeMaterial, Color.white);
        lineRenderer.material = runtimeMaterial;
    }

    private void EnsureAnimation(float moveSpeed)
    {
        if (pathAnimation == null)
        {
            pathAnimation = GetComponent<PathArrowAnimation>();
        }

        if (pathAnimation == null)
        {
            pathAnimation = gameObject.AddComponent<PathArrowAnimation>();
        }

        pathAnimation.OnPositionsChanged -= HandlePositionsChanged;
        pathAnimation.OnHeadCrossedExitBounds -= HandleEscapeCompleted;
        pathAnimation.OnCompleted -= HandleEscapeCompleted;
        pathAnimation.Initialize(
            lineRenderer,
            moveSpeed,
            shortenBeforeExit,
            exitTargetVisibleLength,
            shorteningTailSpeedMultiplier);
        pathAnimation.OnPositionsChanged += HandlePositionsChanged;
        pathAnimation.OnHeadCrossedExitBounds += HandleEscapeCompleted;
        pathAnimation.OnCompleted += HandleEscapeCompleted;
    }

    // Generated segment colliders make the full line clickable, not only the arrow head.
    private void EnsureColliderSpawner(float lineThickness)
    {
        if (colliderSpawner == null)
        {
            colliderSpawner = GetComponent<PathArrowSegmentColliderSpawner2D>();
        }

        if (colliderSpawner == null)
        {
            colliderSpawner = gameObject.AddComponent<PathArrowSegmentColliderSpawner2D>();
        }

        colliderSpawner.Initialize(lineRenderer, this, lineThickness);
    }

    private void EnsureHead()
    {
        if (headTransform == null)
        {
            GameObject headObject = new GameObject("ArrowHead");
            headObject.transform.SetParent(transform, false);
            headTransform = headObject.transform;
        }

        MeshFilter meshFilter = headTransform.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = headTransform.gameObject.AddComponent<MeshFilter>();
        }

        // Generated from style data so the same level can reuse different arrow head shapes.
        meshFilter.sharedMesh = CreateHeadMesh();

        if (headRenderer == null)
        {
            headRenderer = headTransform.GetComponent<MeshRenderer>();
        }

        if (headRenderer == null)
        {
            headRenderer = headTransform.gameObject.AddComponent<MeshRenderer>();
        }

        if (headMaterial == null)
        {
            headMaterial = CreateUnlitColorMaterial();
        }

        headRenderer.sharedMaterial = headMaterial;

        CircleCollider2D headCollider = headTransform.GetComponent<CircleCollider2D>();
        if (headCollider == null)
        {
            headCollider = headTransform.gameObject.AddComponent<CircleCollider2D>();
        }

        headCollider.isTrigger = true;
        headCollider.radius = 0.4f;

        PathArrowColliderProxy proxy = headTransform.GetComponent<PathArrowColliderProxy>();
        if (proxy == null)
        {
            proxy = headTransform.gameObject.AddComponent<PathArrowColliderProxy>();
        }

        proxy.Initialize(this);
        headTransform.localScale = Vector3.one * headSize;
    }

    private void EnsurePreviewBeam()
    {
        if (previewBeamRenderer == null)
        {
            GameObject beamObject = new GameObject("EscapePreviewBeam");
            beamObject.transform.SetParent(transform, false);
            previewBeamRenderer = beamObject.AddComponent<LineRenderer>();
        }

        previewBeamRenderer.useWorldSpace = false;
        previewBeamRenderer.positionCount = 2;
        previewBeamRenderer.numCapVertices = 4;
        previewBeamRenderer.numCornerVertices = 2;
        previewBeamRenderer.sortingOrder = lineRenderer != null ? lineRenderer.sortingOrder - 1 : 9;
        previewBeamRenderer.startWidth = baseLineThickness * previewBeamWidthMultiplier;
        previewBeamRenderer.endWidth = baseLineThickness * previewBeamWidthMultiplier;

        if (previewBeamMaterial == null)
        {
            previewBeamMaterial = CreateUnlitColorMaterial();
        }

        previewBeamRenderer.material = previewBeamMaterial;
        previewBeamRenderer.gameObject.SetActive(false);
    }

    private void HandlePositionsChanged()
    {
        if (collidersEnabled && colliderSpawner != null)
        {
            colliderSpawner.UpdateSegments();
        }

        UpdateHeadPose();
    }

    private void HandleEscapeCompleted()
    {
        if (escapeCompletedRoutine != null)
        {
            return;
        }

        escapeCompletedRoutine = StartCoroutine(EscapeCompletedRoutine());
    }

    private IEnumerator EscapeCompletedRoutine()
    {
        // Once fading starts the arrow should no longer receive clicks.
        collidersEnabled = false;

        if (colliderSpawner != null)
        {
            colliderSpawner.ClearSegments();
        }

        Collider2D headCollider = headTransform != null ? headTransform.GetComponent<Collider2D>() : null;
        if (headCollider != null)
        {
            headCollider.enabled = false;
        }

        if (outsideGridFadeDelay > 0f)
        {
            float delayElapsed = 0f;

            while (delayElapsed < outsideGridFadeDelay)
            {
                delayElapsed += Time.deltaTime;
                yield return null;
            }
        }

        Color startColor = normalColor;
        Color endColor = normalColor;
        endColor.a = 0f;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            float progress = elapsed / fadeOutDuration;
            SetColor(Color.Lerp(startColor, endColor, progress));
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetColor(endColor);

        if (pathAnimation != null)
        {
            pathAnimation.Stop();
        }

        if (manager != null)
        {
            // Tell GameManager the visual object can now be removed.
            manager.HandleArrowEscapeCompleted(this);
        }
    }

    private void UpdateHeadPose()
    {
        if (headTransform == null || lineRenderer == null || lineRenderer.positionCount < 2)
        {
            return;
        }

        int lastIndex = lineRenderer.positionCount - 1;
        Vector3 endPosition = lineRenderer.GetPosition(lastIndex);
        Vector3 previousPosition = lineRenderer.GetPosition(lastIndex - 1);
        Vector3 direction = endPosition - previousPosition;

        headTransform.localPosition = endPosition;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        headTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (holdPreviewActive)
        {
            ShowPreviewBeam();
        }
    }

    private void SetColor(Color color)
    {
        if (lineRenderer != null)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }

        SetMaterialColor(headMaterial, color);
    }

    private void ApplyRestingVisualState()
    {
        if (holdPreviewActive)
        {
            SetColor(GetHoldPreviewColor());
            return;
        }

        SetColor(blockedHighlightActive ? blockedColor : normalColor);
    }

    private Color GetHoldPreviewColor()
    {
        Color baseColor = blockedHighlightActive ? blockedColor : normalColor;
        Color previewColor = Color.Lerp(baseColor, holdHighlightColor, holdHighlightWhiteness);
        previewColor.a = baseColor.a;
        return previewColor;
    }

    private void ShowPreviewBeam()
    {
        if (previewBeamRenderer == null || headTransform == null)
        {
            return;
        }

        Vector3 direction = new Vector3(ExitGridDirection.x, ExitGridDirection.y, 0f);

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction.Normalize();

        // The beam starts just past the head so it reads as a projected escape direction.
        Vector3 startPosition = headTransform.localPosition + direction * headSize * 0.35f;
        float boardLength = Mathf.Max(boardExitBounds.width, boardExitBounds.height) * 2f;
        float beamLength = Mathf.Max(previewBeamLength, boardLength);

        previewBeamRenderer.startWidth = baseLineThickness * previewBeamWidthMultiplier;
        previewBeamRenderer.endWidth = baseLineThickness * previewBeamWidthMultiplier;
        previewBeamRenderer.SetPosition(0, startPosition);
        previewBeamRenderer.SetPosition(1, startPosition + direction * beamLength);

        Color beamColor = GetHoldPreviewColor();
        beamColor.a = previewBeamAlpha;
        previewBeamRenderer.startColor = beamColor;
        previewBeamRenderer.endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0f);

        if (previewBeamMaterial != null)
        {
            SetMaterialColor(previewBeamMaterial, Color.white);
        }

        previewBeamRenderer.gameObject.SetActive(true);
    }

    private void ApplyStyleData()
    {
        if (styleData == null)
        {
            return;
        }

        // Copy values from the asset once when this runtime arrow is built.
        blockedColor = styleData.BlockedColor;
        holdHighlightColor = styleData.HoldHighlightColor;
        holdHighlightWhiteness = styleData.HoldHighlightBlend;
        blockedShakeDistance = styleData.BlockedShakeDistance;
        blockedFeedbackDuration = styleData.BlockedFeedbackDuration;
        headSizeMultiplier = styleData.HeadSizeMultiplier;
        headTipLength = styleData.HeadTipLength;
        headBaseBackLength = styleData.HeadBaseBackLength;
        headHalfWidth = styleData.HeadHalfWidth;
        holdLineWidthMultiplier = styleData.HoldLineWidthMultiplier;
        holdHeadScaleMultiplier = styleData.HoldHeadScaleMultiplier;
        previewBeamLength = styleData.PreviewBeamLength;
        previewBeamWidthMultiplier = styleData.PreviewBeamWidthMultiplier;
        previewBeamAlpha = styleData.PreviewBeamAlpha;
    }

    private void HidePreviewBeam()
    {
        if (previewBeamRenderer != null)
        {
            previewBeamRenderer.gameObject.SetActive(false);
        }
    }

    private bool IsPointerOverThisArrow()
    {
        Camera targetCamera = Camera.main;

        if (targetCamera == null)
        {
            return true;
        }

        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);

        // Any generated segment collider or head collider owned by this arrow keeps the press valid.
        for (int i = 0; i < hits.Length; i++)
        {
            PathArrowColliderProxy proxy = hits[i].GetComponent<PathArrowColliderProxy>();

            if (proxy != null && proxy.Owner == this)
            {
                return true;
            }
        }

        return false;
    }

    private Mesh CreateHeadMesh()
    {
        float tipLength = Mathf.Max(0.05f, headTipLength);
        float baseBackLength = Mathf.Max(0f, headBaseBackLength);
        float halfWidth = Mathf.Max(0.02f, headHalfWidth);

        Mesh mesh = new Mesh();
        mesh.name = "PathArrowHeadMesh";

        // Local +X is the arrow's forward direction; UpdateHeadPose rotates the head to match the path.
        mesh.vertices = new[]
        {
            new Vector3(tipLength, 0f, 0f),
            new Vector3(-baseBackLength, halfWidth, 0f),
            new Vector3(-baseBackLength, -halfWidth, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateUnlitColorMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        return new Material(shader);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
    }

    private void OnDestroy()
    {
        CancelHoldPreview();

        if (pathAnimation != null)
        {
            pathAnimation.OnPositionsChanged -= HandlePositionsChanged;
            pathAnimation.OnHeadCrossedExitBounds -= HandleEscapeCompleted;
            pathAnimation.OnCompleted -= HandleEscapeCompleted;
        }
    }
}
