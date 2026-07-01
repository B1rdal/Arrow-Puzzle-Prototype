using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
// Owns the path-arrow board: builds arrows, tracks occupied cells, validates moves, and completes levels.
public class GameManager : MonoBehaviour
{
    [Header("Levels")]
    [SerializeField] private List<PathArrowLevelData> levels = new List<PathArrowLevelData>();
    [Min(0)]
    [SerializeField] private int currentLevelIndex;
    [SerializeField] private PathArrow pathArrowPrefab = null;
    [SerializeField] private bool buildOnStart = true;

    [Header("Arrow Style")]
    [SerializeField] private PathArrowStyleData arrowStyle = null;
    [SerializeField] private bool useLevelArrowColors = false;

    [Header("Fallback Level")]
    [Min(1)]
    [SerializeField] private int fallbackWidth = 8;
    [Min(1)]
    [SerializeField] private int fallbackHeight = 8;

    [Header("Layout")]
    [SerializeField] private Transform arrowParent;
    [SerializeField] private Vector3 boardOrigin = Vector3.zero;
    [SerializeField] private bool centerBoardAtOrigin = true;
    [Min(0.1f)]
    [SerializeField] private float cellSize = 1f;
    [Min(0.05f)]
    [SerializeField] private float lineThickness = 0.28f;

    [Header("Motion")]
    [Min(0.1f)]
    [SerializeField] private float escapeSpeed = 6f;
    [SerializeField] private bool allowConcurrentEscapes = true;
    [SerializeField] private bool shortenArrowsBeforeExit = true;
    [Min(0.1f)]
    [SerializeField] private float exitVisibleLengthCells = 1.6f;
    [Min(1f)]
    [SerializeField] private float shorteningTailSpeedMultiplier = 2.25f;
    [Min(0f)]
    [SerializeField] private float headCrossFadeDelay = 0f;
    [Min(0.01f)]
    [SerializeField] private float fadeOutDuration = 0.25f;
    [Min(0f)]
    [SerializeField] private float outsideGridMargin = 0.35f;

    [Header("Lives")]
    [Min(1)]
    [SerializeField] private int maxLives = 3;

    [Header("Camera")]
    [SerializeField] private bool frameCameraOnBuild = true;
    [SerializeField] private Camera targetCamera;

    [Header("Grid Dots")]
    [SerializeField] private bool showGridCenterDots = true;
    [SerializeField] private Color gridDotColor = new Color(0f, 0f, 0f, 0.35f);
    [Min(0.01f)]
    [SerializeField] private float gridDotRadius = 0.06f;
    [SerializeField] private int gridDotSortingOrder = 0;

    [Header("Events")]
    [SerializeField] private UnityEvent levelCompleted = new UnityEvent();

    private readonly List<PathArrow> activeArrows = new List<PathArrow>();
    private readonly List<GameObject> gridDotObjects = new List<GameObject>();
    private readonly Dictionary<Vector2Int, PathArrow> occupiedCells = new Dictionary<Vector2Int, PathArrow>();
    private readonly HashSet<PathArrow> blockedMoveDebounce = new HashSet<PathArrow>();
    private readonly HashSet<PathArrow> escapingArrows = new HashSet<PathArrow>();
    private readonly List<Vector2Int> reusableCells = new List<Vector2Int>();

    private Transform gridDotParent;
    private int width;
    private int height;
    private int currentLives;
    private bool inputLocked;
    private bool hasLoggedGameLoss;
    private bool levelEnded;

    public int ActiveArrowCount => activeArrows.Count;
    public UnityEvent LevelCompleted => levelCompleted;
    public int Width => width > 0 ? width : GetConfiguredWidth();
    public int Height => height > 0 ? height : GetConfiguredHeight();
    public float CellSize => cellSize;
    public int MaxLives => Mathf.Max(1, maxLives);
    public int CurrentLives => currentLives;
    public IReadOnlyList<PathArrowLevelData> Levels => levels;
    public int CurrentLevelIndex => GetClampedLevelIndex();
    public PathArrowLevelData CurrentLevel => GetCurrentLevelData();
    public bool LevelEnded => levelEnded;

    public event Action<PathArrow> ArrowEscaped;
    public event Action<PathArrow> BlockedArrowTapped;
    public event Action<int, int> LivesChanged;
    public event Action GameLost;
    public event Action LevelStarted;
    public event Action AllArrowsEscaped;

    private void Awake()
    {
        if (arrowParent == null)
        {
            arrowParent = transform;
        }

        ResetLives();
    }

    private void Start()
    {
        if (buildOnStart)
        {
            BuildLevel();
        }
    }

    [ContextMenu("Build Path Arrow Level")]
    public void BuildLevel()
    {
        ClearLevel();
        inputLocked = false;
        levelEnded = false;
        ResetLives();

        IReadOnlyList<PathArrowData> arrows = GetLevelArrows(out width, out height);

        // Dots are rebuilt first so they sit behind generated arrows.
        RebuildGridDots();

        for (int i = 0; i < arrows.Count; i++)
        {
            TrySpawnArrow(arrows[i]);
        }

        if (frameCameraOnBuild)
        {
            FrameCamera();
        }

        LevelStarted?.Invoke();
    }

    public void BuildLevel(int levelIndex)
    {
        currentLevelIndex = Mathf.Max(0, levelIndex);
        BuildLevel();
    }

    public void BuildNextLevel()
    {
        if (levels == null || levels.Count == 0)
        {
            BuildLevel();
            return;
        }

        currentLevelIndex = (GetClampedLevelIndex() + 1) % levels.Count;
        BuildLevel();
    }

    public void BuildPreviousLevel()
    {
        if (levels == null || levels.Count == 0)
        {
            BuildLevel();
            return;
        }

        currentLevelIndex = (GetClampedLevelIndex() - 1 + levels.Count) % levels.Count;
        BuildLevel();
    }

    public void RestartLevel()
    {
        BuildLevel();
    }

    public void LoadNextLevel()
    {
        BuildNextLevel();
    }

    public bool TryEscape(PathArrow arrow)
    {
        if (levelEnded || (!allowConcurrentEscapes && inputLocked) || arrow == null || arrow.IsAnimating || escapingArrows.Contains(arrow) || !activeArrows.Contains(arrow))
        {
            return false;
        }

        if (!CanArrowEscape(arrow))
        {
            bool shouldLoseLife = RegisterBlockedMove(arrow);

            if (shouldLoseLife)
            {
                BlockedArrowTapped?.Invoke(arrow);
                LoseLifeForFailedMove();
            }

            return false;
        }

        if (!allowConcurrentEscapes)
        {
            inputLocked = true;
        }

        ClearBlockedMoveDebounce();
        escapingArrows.Add(arrow);

        // Free the cells immediately so other arrows can be tapped while this one animates away.
        UnregisterOccupiedCells(arrow);
        ArrowEscaped?.Invoke(arrow);
        arrow.PlayEscape();

        if (AreAllRemainingArrowsEscaping())
        {
            CompleteLevel();
        }

        return true;
    }

    public bool CanArrowEscape(PathArrow arrow)
    {
        if (arrow == null || arrow.ExitGridDirection == Vector2Int.zero)
        {
            return false;
        }

        Vector2Int checkPosition = arrow.HeadGridPosition + arrow.ExitGridDirection;

        // Only the straight path in front of the head matters for escape.
        while (IsInsideGrid(checkPosition))
        {
            if (occupiedCells.TryGetValue(checkPosition, out PathArrow blocker) && blocker != arrow)
            {
                return false;
            }

            checkPosition += arrow.ExitGridDirection;
        }

        return true;
    }

    public void HandleArrowEscapeCompleted(PathArrow arrow)
    {
        if (arrow == null)
        {
            if (!allowConcurrentEscapes)
            {
                inputLocked = false;
            }
            return;
        }

        activeArrows.Remove(arrow);
        escapingArrows.Remove(arrow);
        DestroyArrowObject(arrow.gameObject);

        if (!allowConcurrentEscapes)
        {
            inputLocked = false;
        }

        if (activeArrows.Count == 0)
        {
            CompleteLevel();
        }
    }

    public Vector3 GridToLocalPosition(Vector2Int gridPosition)
    {
        return GridToLocalPosition(gridPosition, Width, Height);
    }

    public Bounds GetBoardWorldBounds()
    {
        Rect localBounds = GetBoardLocalBounds();
        Vector3 min = transform.TransformPoint(new Vector3(localBounds.xMin, localBounds.yMin, 0f));
        Vector3 max = transform.TransformPoint(new Vector3(localBounds.xMax, localBounds.yMax, 0f));
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), 0f);

        return new Bounds(center, size);
    }

    private Vector3 GridToLocalPosition(Vector2Int gridPosition, int boardWidth, int boardHeight)
    {
        Vector3 offset = new Vector3(gridPosition.x * cellSize, gridPosition.y * cellSize, 0f);

        if (centerBoardAtOrigin)
        {
            offset -= new Vector3((boardWidth - 1) * cellSize * 0.5f, (boardHeight - 1) * cellSize * 0.5f, 0f);
        }

        return boardOrigin + offset;
    }

    public bool IsInsideGrid(Vector2Int gridPosition)
    {
        return gridPosition.x >= 0
            && gridPosition.y >= 0
            && gridPosition.x < width
            && gridPosition.y < height;
    }

    public void ClearLevel()
    {
        ClearBlockedMoveDebounce();

        for (int i = activeArrows.Count - 1; i >= 0; i--)
        {
            if (activeArrows[i] != null)
            {
                DestroyArrowObject(activeArrows[i].gameObject);
            }
        }

        activeArrows.Clear();
        escapingArrows.Clear();
        ClearGridDots();
        occupiedCells.Clear();
        inputLocked = false;
        levelEnded = false;
    }

    private void CompleteLevel()
    {
        if (levelEnded)
        {
            return;
        }

        ClearBlockedMoveDebounce();
        levelEnded = true;
        inputLocked = true;
        levelCompleted?.Invoke();
        AllArrowsEscaped?.Invoke();
    }

    private bool RegisterBlockedMove(PathArrow arrow)
    {
        if (arrow == null)
        {
            return false;
        }

        if (blockedMoveDebounce.Contains(arrow))
        {
            return false;
        }

        blockedMoveDebounce.Add(arrow);
        arrow.SetBlockedHighlight(true);
        arrow.PlayBlockedFeedback();
        return true;
    }

    private void ClearBlockedMoveDebounce()
    {
        foreach (PathArrow arrow in blockedMoveDebounce)
        {
            if (arrow != null)
            {
                arrow.SetBlockedHighlight(false);
            }
        }

        blockedMoveDebounce.Clear();
    }

    private bool AreAllRemainingArrowsEscaping()
    {
        bool foundArrow = false;

        for (int i = 0; i < activeArrows.Count; i++)
        {
            PathArrow arrow = activeArrows[i];

            if (arrow == null)
            {
                continue;
            }

            foundArrow = true;

            if (!escapingArrows.Contains(arrow))
            {
                return false;
            }
        }

        return foundArrow;
    }

    private void ResetLives()
    {
        currentLives = MaxLives;
        hasLoggedGameLoss = false;
        LivesChanged?.Invoke(currentLives, MaxLives);
    }

    private void LoseLifeForFailedMove()
    {
        if (hasLoggedGameLoss)
        {
            return;
        }

        currentLives = Mathf.Max(0, currentLives - 1);
        LivesChanged?.Invoke(currentLives, MaxLives);
        Debug.Log($"Failed move. Lives left: {currentLives}/{MaxLives}", this);

        if (currentLives > 0)
        {
            return;
        }

        hasLoggedGameLoss = true;
        levelEnded = true;
        inputLocked = true;
        Debug.Log("Game loss: no lives remaining.", this);
        GameLost?.Invoke();
    }

    private bool TrySpawnArrow(PathArrowData arrowData)
    {
        if (arrowData == null || arrowData.Points == null || arrowData.Points.Count < 2)
        {
            Debug.LogWarning("Skipping path arrow with fewer than 2 points.", this);
            return false;
        }

        if (!PathArrowUtility.TryBuildOccupiedCells(arrowData.Points, reusableCells))
        {
            Debug.LogWarning($"Skipping {arrowData.Id}: path must use horizontal/vertical segments and have a valid head direction.", this);
            return false;
        }

        for (int i = 0; i < reusableCells.Count; i++)
        {
            // All occupied cells must be inside the board and unique.
            if (!IsInsideGrid(reusableCells[i]))
            {
                Debug.LogWarning($"Skipping {arrowData.Id}: cell {reusableCells[i]} is outside the board.", this);
                return false;
            }

            if (occupiedCells.ContainsKey(reusableCells[i]))
            {
                Debug.LogWarning($"Skipping {arrowData.Id}: cell {reusableCells[i]} overlaps another path arrow.", this);
                return false;
            }
        }

        Vector3[] localPositions = new Vector3[arrowData.Points.Count];

        for (int i = 0; i < arrowData.Points.Count; i++)
        {
            localPositions[i] = GridToLocalPosition(arrowData.Points[i]);
        }

        PathArrow arrow = CreateArrowInstance(arrowData.Id);
        arrow.Initialize(
            this,
            arrowData.Id,
            arrowData.Points,
            reusableCells,
            localPositions,
            GetArrowColor(arrowData),
            arrowStyle,
            lineThickness,
            escapeSpeed,
            GetBoardLocalBounds(),
            outsideGridMargin,
            headCrossFadeDelay,
            fadeOutDuration,
            shortenArrowsBeforeExit,
            exitVisibleLengthCells * cellSize,
            shorteningTailSpeedMultiplier);
        activeArrows.Add(arrow);

        for (int i = 0; i < reusableCells.Count; i++)
        {
            occupiedCells.Add(reusableCells[i], arrow);
        }

        return true;
    }

    private Color GetArrowColor(PathArrowData arrowData)
    {
        if (arrowStyle != null && !useLevelArrowColors)
        {
            return arrowStyle.ArrowColor;
        }

        return arrowData != null ? arrowData.Color : Color.white;
    }

    private PathArrow CreateArrowInstance(string id)
    {
        PathArrow arrow;

        if (pathArrowPrefab != null)
        {
            arrow = Instantiate(pathArrowPrefab, arrowParent);
        }
        else
        {
            GameObject arrowObject = new GameObject(id);
            arrowObject.transform.SetParent(arrowParent, false);
            arrowObject.transform.localPosition = Vector3.zero;
            arrow = arrowObject.AddComponent<PathArrow>();
        }

        arrow.name = id;
        arrow.transform.localPosition = Vector3.zero;
        return arrow;
    }

    private void UnregisterOccupiedCells(PathArrow arrow)
    {
        for (int i = 0; i < arrow.OccupiedCells.Count; i++)
        {
            Vector2Int cell = arrow.OccupiedCells[i];

            if (occupiedCells.TryGetValue(cell, out PathArrow occupant) && occupant == arrow)
            {
                occupiedCells.Remove(cell);
            }
        }
    }

    private IReadOnlyList<PathArrowData> GetLevelArrows(out int levelWidth, out int levelHeight)
    {
        PathArrowLevelData currentLevel = GetCurrentLevelData();

        if (currentLevel != null)
        {
            levelWidth = currentLevel.Width;
            levelHeight = currentLevel.Height;
            return currentLevel.Arrows;
        }

        levelWidth = Mathf.Max(1, fallbackWidth);
        levelHeight = Mathf.Max(1, fallbackHeight);
        return CreateFallbackArrows();
    }

    private Rect GetBoardLocalBounds()
    {
        // Bounds are measured around cell edges, not center dots.
        int boardWidth = Width;
        int boardHeight = Height;
        Vector3 bottomLeftCenter = GridToLocalPosition(Vector2Int.zero, boardWidth, boardHeight);
        Vector3 topRightCenter = GridToLocalPosition(new Vector2Int(boardWidth - 1, boardHeight - 1), boardWidth, boardHeight);
        float halfCell = cellSize * 0.5f;

        float xMin = Mathf.Min(bottomLeftCenter.x, topRightCenter.x) - halfCell;
        float xMax = Mathf.Max(bottomLeftCenter.x, topRightCenter.x) + halfCell;
        float yMin = Mathf.Min(bottomLeftCenter.y, topRightCenter.y) - halfCell;
        float yMax = Mathf.Max(bottomLeftCenter.y, topRightCenter.y) + halfCell;

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private int GetConfiguredWidth()
    {
        PathArrowLevelData currentLevel = GetCurrentLevelData();
        return currentLevel != null ? currentLevel.Width : Mathf.Max(1, fallbackWidth);
    }

    private int GetConfiguredHeight()
    {
        PathArrowLevelData currentLevel = GetCurrentLevelData();
        return currentLevel != null ? currentLevel.Height : Mathf.Max(1, fallbackHeight);
    }

    private PathArrowLevelData GetCurrentLevelData()
    {
        if (levels == null || levels.Count == 0)
        {
            return null;
        }

        return levels[GetClampedLevelIndex()];
    }

    private int GetClampedLevelIndex()
    {
        if (levels == null || levels.Count == 0)
        {
            return 0;
        }

        return Mathf.Clamp(currentLevelIndex, 0, levels.Count - 1);
    }

    private static List<PathArrowData> CreateFallbackArrows()
    {
        // Built-in sample level used when no PathArrowLevelData asset is assigned.
        return new List<PathArrowData>
        {
            new PathArrowData(
                "Red Hook",
                new Color(0.95f, 0.28f, 0.25f),
                new[]
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(2, 1),
                    new Vector2Int(2, 3),
                    new Vector2Int(4, 3)
                }),
            new PathArrowData(
                "Blue Bend",
                new Color(0.18f, 0.48f, 1f),
                new[]
                {
                    new Vector2Int(6, 3),
                    new Vector2Int(6, 5),
                    new Vector2Int(7, 5)
                }),
            new PathArrowData(
                "Green Sprint",
                new Color(0.25f, 0.72f, 0.38f),
                new[]
                {
                    new Vector2Int(5, 0),
                    new Vector2Int(5, 2),
                    new Vector2Int(7, 2)
                }),
            new PathArrowData(
                "Gold Curl",
                new Color(1f, 0.72f, 0.18f),
                new[]
                {
                    new Vector2Int(3, 7),
                    new Vector2Int(3, 5),
                    new Vector2Int(1, 5),
                    new Vector2Int(1, 7)
                }),
            new PathArrowData(
                "Violet Corner",
                new Color(0.58f, 0.35f, 0.92f),
                new[]
                {
                    new Vector2Int(0, 6),
                    new Vector2Int(0, 4),
                    new Vector2Int(3, 4)
                })
        };
    }

    private void RebuildGridDots()
    {
        ClearGridDots();

        if (!showGridCenterDots)
        {
            return;
        }

        if (gridDotParent == null)
        {
            // Keep dots grouped so they are easy to hide/delete in the hierarchy.
            GameObject dotParentObject = new GameObject("GridCenterDots");
            gridDotParent = dotParentObject.transform;
            gridDotParent.SetParent(arrowParent != null ? arrowParent : transform, false);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 position = GridToLocalPosition(new Vector2Int(x, y));
                position.z -= 0.08f;

                GameObject dot = GridCenterDotFactory.CreateDot(
                    gridDotParent,
                    $"GridDot_{x}_{y}",
                    position,
                    gridDotRadius,
                    gridDotColor,
                    gridDotSortingOrder,
                    useLocalPosition: true);

                gridDotObjects.Add(dot);
            }
        }
    }

    private void ClearGridDots()
    {
        for (int i = gridDotObjects.Count - 1; i >= 0; i--)
        {
            if (gridDotObjects[i] != null)
            {
                DestroyArrowObject(gridDotObjects[i]);
            }
        }

        gridDotObjects.Clear();
    }

    private void FrameCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        targetCamera.orthographic = true;

        Vector3 boardCenter = centerBoardAtOrigin
            ? boardOrigin
            : boardOrigin + new Vector3((width - 1) * cellSize * 0.5f, (height - 1) * cellSize * 0.5f, 0f);

        Vector3 worldCenter = transform.TransformPoint(boardCenter);
        targetCamera.transform.position = new Vector3(worldCenter.x, worldCenter.y, targetCamera.transform.position.z);

        float aspect = Mathf.Max(0.01f, targetCamera.aspect);
        float verticalSize = height * cellSize * 0.55f + cellSize;
        float horizontalSize = width * cellSize * 0.55f / aspect + cellSize;
        targetCamera.orthographicSize = Mathf.Max(verticalSize, horizontalSize);
    }

    private static void DestroyArrowObject(GameObject arrowObject)
    {
        if (Application.isPlaying)
        {
            Destroy(arrowObject);
        }
        else
        {
            DestroyImmediate(arrowObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        int drawWidth = width > 0 ? width : fallbackWidth;
        int drawHeight = height > 0 ? height : fallbackHeight;

        Gizmos.color = new Color(0.1f, 0.1f, 0.1f, 0.25f);

        for (int y = 0; y < drawHeight; y++)
        {
            for (int x = 0; x < drawWidth; x++)
            {
                Vector3 position = boardOrigin + new Vector3(x * cellSize, y * cellSize, 0f);

                if (centerBoardAtOrigin)
                {
                    position -= new Vector3((drawWidth - 1) * cellSize * 0.5f, (drawHeight - 1) * cellSize * 0.5f, 0f);
                }

                Gizmos.DrawWireCube(transform.TransformPoint(position), Vector3.one * cellSize * 0.9f);
                Gizmos.DrawSphere(transform.TransformPoint(position), cellSize * 0.06f);
            }
        }
    }
}
