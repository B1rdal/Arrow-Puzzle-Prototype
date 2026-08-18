/*
Summary:
RuntimeArrowLevelEditorApp is the standalone .exe level editor. It uses runtime IMGUI
so it can run outside the Unity Editor, lets designers draw arrow paths on a grid,
paint optional non-rectangular board shapes, validates illegal paths, tracks
undo/redo history, supports color themes, tests solvability, and saves/loads JSON
level files.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RuntimeArrowLevelEditorApp : MonoBehaviour
{
    private const float BaseCellSize = 30f;
    private const float BaseCellGap = 2f;
    private const float HeaderSize = 24f;
    private const float DefaultGridZoom = 1.15f;
    private const float ReferenceGridZoomBoardSize = 15f;
    private const float ReferenceMinGridZoom = 0.65f;
    private const float AbsoluteMinGridZoom = 0.25f;
    private const float MaxGridZoom = 2.25f;
    private const float GridZoomStep = 0.1f;
    private const float GridScrollPadding = 240f;
    private const float DefaultLeftPanelWidth = 260f;
    private const float DefaultRightPanelWidth = 340f;
    private const float MinLeftPanelWidth = 190f;
    private const float MinRightPanelWidth = 260f;
    private const float MinGridPanelWidth = 260f;
    private const float SplitterWidth = 8f;
    private const float PanelPadding = 8f;
    private const float StatusBarHeight = 34f;
    private const float DefaultStatusHistoryPanelHeight = 220f;
    private const float MinStatusHistoryPanelHeight = 170f;
    private const float GridPanDragThreshold = 4f;
    private const int MaxHistoryEntries = 80;
    private const string ThemePrefsKey = "RuntimeArrowLevelEditor.ThemeIndex";
    private const string SaveFolderPrefsKey = "RuntimeArrowLevelEditor.SaveFolder";
    private const string ShowCoordinatesPrefsKey = "RuntimeArrowLevelEditor.ShowCoordinates";
    private const string GeneratorColorModePrefsKey = "RuntimeArrowLevelEditor.GeneratorColorMode";
    private const string GeneratorAlgorithmModePrefsKey = "RuntimeArrowLevelEditor.GeneratorAlgorithmMode";
    private const string GeneratorMinimumLengthWeightPrefsKey = "RuntimeArrowLevelEditor.GeneratorMinimumLengthWeight";
    private const string GeneratorAutoLengthPrefsKey = "RuntimeArrowLevelEditor.GeneratorAutoLength";
    private const string GeneratorComplexityPrefsKey = "RuntimeArrowLevelEditor.GeneratorComplexity";
    private const string TextInputControlPrefix = "RuntimeArrowLevelEditor.TextInput.";

    private const string LeftPanelWidthPrefsKey = "RuntimeArrowLevelEditor.LeftPanelWidth";
    private const string RightPanelWidthPrefsKey = "RuntimeArrowLevelEditor.RightPanelWidth";
    private const string LeftGeneralPanelRatioPrefsKey = "RuntimeArrowLevelEditor.LeftGeneralPanelRatioV2";
    private const string RightControlsPanelRatioPrefsKey = "RuntimeArrowLevelEditor.RightControlsPanelRatioV3";
    private const string RightDebugPanelRatioPrefsKey = "RuntimeArrowLevelEditor.RightDebugPanelRatio";
    private const string StatusHistoryPanelHeightPrefsKey = "RuntimeArrowLevelEditor.StatusHistoryPanelHeight";
    private const string PlayTestManagerObjectName = "RuntimeLevelEditorPlayableTester";
    private const string PlayTestBoardBackdropObjectName = "RuntimeLevelEditorPlayableTestBackdrop";
    private const float PlayTestBoardBackdropPaddingCells = 1.25f;
    private const int PlayTestBoardBackdropSortingOrder = -100;
    private const int GeneratorNormalTimeBudgetMs = 1200;
    private const int GeneratorFullFillTimeBudgetMs = 3000;
    private const int GeneratorComplexGuidedTimeBudgetMs = 9000;
    private const int GeneratorComplexGuidedDxTimeBudgetMs = 14000;
    private const int GeneratorComplexGuidedDxPolishBudgetMs = 3500;
    private const int GeneratorComplexGuidedDxFlowTimeBudgetMs = 16000;
    private const int GeneratorComplexGuidedDxFlowPolishBudgetMs = 4500;
    private const int GeneratorLockstepWeaveTimeBudgetMs = 12000;
    private const int GeneratorTargetMaxPlayableRoutes = 2;
    private const int GeneratorGateBurstRouteCeiling = 3;
    private const int GeneratorGateBuildThreshold = 2;
    private const int GeneratorGateMinimumDependents = 2;
    private const int GeneratorGateMaximumDependents = 4;
    private const int GeneratorDecisionExitBufferCells = 2;
    private const int GeneratorDecisionStateBudget = 8192;
    private const float GeneratorGateTailGrowthChance = 0.18f;
    private const float GeneratorComplexGuidedDxTailGrowthChance = 0.12f;
    private const float GeneratorComplexGuidedDxFlowTailGrowthChance = 0.14f;
    private const int GeneratorGateDensityRepairTimeBudgetMs = 6000;
    private const int GeneratorDiversityCandidateSamples = 6;
    private const int GeneratorProfileCandidateAttempts = 4;
    private const float GeneratorMaxUShapeRatio = 0.2f;
    private const int GeneratorNearbyShapeDistance = 2;
    private const float GeneratorMinimumLengthWeightLimit = 0.2f;
    private const float GeneratorMaximumLengthWeightLimit = 0.8f;

    private static readonly RuntimeEditorTheme[] Themes =
    {
        new RuntimeEditorTheme(
            "Dark",
            new Color(0.08f, 0.08f, 0.09f),
            new Color(0.18f, 0.18f, 0.2f),
            new Color(0.13f, 0.13f, 0.14f),
            new Color(0.24f, 0.24f, 0.25f),
            new Color(0.18f, 0.18f, 0.19f),
            new Color(0.35f, 0.65f, 1f, 0.45f),
            new Color(1f, 0.8f, 0.05f, 0.55f),
            new Color(0.96f, 0.98f, 1f, 1f),
            new Color(1f, 0.8f, 0.05f, 1f),
            new Color(0.9f, 0.95f, 1f, 0.95f),
            new Color(1f, 0.92f, 0.25f, 0.36f),
            new Color(1f, 0.95f, 0.25f, 1f),
            new Color(0.45f, 0.65f, 1f),
            new Color(1f, 0.8f, 0.1f)),
        new RuntimeEditorTheme(
            "Light",
            new Color(0.86f, 0.88f, 0.9f),
            new Color(0.82f, 0.84f, 0.86f),
            new Color(0.68f, 0.7f, 0.72f),
            new Color(0.92f, 0.94f, 0.96f),
            new Color(0.72f, 0.77f, 0.81f),
            new Color(0.22f, 0.55f, 1f, 0.35f),
            new Color(1f, 0.65f, 0f, 0.45f),
            new Color(0.06f, 0.08f, 0.1f, 1f),
            new Color(0.95f, 0.45f, 0f, 1f),
            new Color(0.08f, 0.1f, 0.12f, 0.95f),
            new Color(0.1f, 0.5f, 1f, 0.28f),
            new Color(0.02f, 0.2f, 0.5f, 1f),
            new Color(0.55f, 0.75f, 1f),
            new Color(1f, 0.72f, 0.18f)),
        new RuntimeEditorTheme(
            "Ocean",
            new Color(0.02f, 0.08f, 0.1f),
            new Color(0.07f, 0.18f, 0.22f),
            new Color(0.04f, 0.14f, 0.17f),
            new Color(0.08f, 0.22f, 0.25f),
            new Color(0.05f, 0.16f, 0.19f),
            new Color(0f, 0.8f, 1f, 0.42f),
            new Color(0.8f, 1f, 0.3f, 0.45f),
            new Color(0.16f, 1f, 0.9f, 1f),
            new Color(0.85f, 1f, 0.25f, 1f),
            new Color(0.75f, 0.95f, 1f, 0.95f),
            new Color(0.1f, 1f, 0.9f, 0.3f),
            new Color(0.2f, 1f, 0.95f, 1f),
            new Color(0.15f, 0.75f, 0.9f),
            new Color(0.7f, 1f, 0.25f)),
        new RuntimeEditorTheme(
            "Contrast",
            new Color(0.01f, 0.01f, 0.01f),
            new Color(0.13f, 0.13f, 0.13f),
            new Color(0f, 0f, 0f),
            new Color(0.13f, 0.13f, 0.13f),
            new Color(0.03f, 0.03f, 0.03f),
            new Color(1f, 1f, 1f, 0.5f),
            new Color(1f, 0.85f, 0f, 0.65f),
            new Color(1f, 0.92f, 0.08f, 1f),
            new Color(1f, 0.85f, 0f, 1f),
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 0.1f, 0.1f, 0.55f),
            new Color(1f, 0.25f, 0.25f, 1f),
            new Color(0.95f, 0.95f, 0.2f),
            new Color(1f, 0.85f, 0f))
    };

    private static readonly string[] ThemeNames = { "Dark", "Light", "Ocean", "Contrast" };
    private static readonly string[] FileMenuNames = { "New Level", "Save", "Save As...", "Load JSON...", "Open Folder", "Copy Path" };
    private static readonly string[] GeneratorColorModeNames = { "Theme", "Rainbow", "Pastel", "Contrast" };
    private static readonly string[] GeneratorAlgorithmModeNames =
    {
        "Legacy",
        "Profile Guided",
        "Complex Guided",
        "Gate Network",
        "Complex Guided DX",
        "Complex Guided DX Flow"
    };
    private static readonly GeneratedArrowProfile[] GuidedGeneratorProfiles =
    {
        GeneratedArrowProfile.StraightRail,
        GeneratedArrowProfile.LHook,
        GeneratedArrowProfile.OffsetS,
        GeneratedArrowProfile.Hairpin,
        GeneratedArrowProfile.OpenC,
        GeneratedArrowProfile.RectangularSpiral,
        GeneratedArrowProfile.Serpentine,
        GeneratedArrowProfile.Staircase,
        GeneratedArrowProfile.PerimeterRunner,
        GeneratedArrowProfile.LongSpine
    };
    // Slider colors are explicit because Unity's default IMGUI slider textures lose
    // contrast against the darker editor themes.
    private static readonly Color[] SliderTrackColors =
    {
        new Color(0.38f, 0.4f, 0.44f, 1f),
        new Color(0.42f, 0.46f, 0.5f, 1f),
        new Color(0.08f, 0.42f, 0.48f, 1f),
        new Color(0.32f, 0.32f, 0.32f, 1f)
    };

    private static readonly Color[] SliderFillColors =
    {
        new Color(1f, 0.78f, 0.08f, 1f),
        new Color(0.05f, 0.42f, 0.95f, 1f),
        new Color(0.65f, 1f, 0.18f, 1f),
        new Color(1f, 0.85f, 0f, 1f)
    };

    private static readonly Color[] SliderHandleColors =
    {
        new Color(0.96f, 0.98f, 1f, 1f),
        new Color(0.08f, 0.1f, 0.12f, 1f),
        new Color(0.78f, 1f, 0.95f, 1f),
        Color.white
    };

    private static readonly Color[] SliderOutlineColors =
    {
        new Color(0.03f, 0.03f, 0.04f, 1f),
        new Color(0.95f, 0.97f, 1f, 1f),
        new Color(0.01f, 0.07f, 0.09f, 1f),
        Color.white
    };

    private readonly List<RuntimeArrowDraft> arrows = new List<RuntimeArrowDraft>();
    private readonly HashSet<Vector2Int> activeCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> occupiedCellOwners = new Dictionary<Vector2Int, int>();
    private readonly List<string> validationMessages = new List<string>();
    private readonly List<string> testerMessages = new List<string>();
    private readonly List<int> testerClearOrder = new List<int>();
    private readonly HashSet<Vector2Int> shapePaintTrailCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> arrowDrawTrailCells = new HashSet<Vector2Int>();
    private readonly List<RuntimeArrowLevelDocument> undoHistory = new List<RuntimeArrowLevelDocument>();
    private readonly List<RuntimeArrowLevelDocument> redoHistory = new List<RuntimeArrowLevelDocument>();
    private readonly List<string> statusHistory = new List<string>();

    [Header("EXE Features")]
    [SerializeField, Tooltip("Show the procedural Generator button and popup in the standalone level editor.")]
    private bool showLevelGeneratorButton = true;

    [Header("Playable Test")]
    [SerializeField] private PathArrowStyleData playableTestArrowStyle = null;
    [SerializeField] private float playableTestZoomStep = 0.35f;
    [SerializeField] private float playableTestMinZoomSize = 6f;
    [SerializeField] private float playableTestMaxZoomSize = 20f;
    [SerializeField] private float playableTestPanPaddingCells = 8f;

    private Vector2 leftScroll;
    private Vector2 arrowListScroll;
    private Vector2 gridScroll = new Vector2(GridScrollPadding, GridScrollPadding);
    private Vector2 debugLogScroll;
    private Vector2 clearOrderScroll;
    private Vector2 generatorDropdownScroll;
    private Vector2 statusHistoryScroll;
    private Vector2 gridPanStartMousePosition;
    private Vector2 gridPanLastMousePosition;
    private bool isGridRightMousePanning;
    private bool gridRightMouseMoved;
    private bool isShapePaintDragging;
    private bool shapePaintSetActive;
    private Vector2Int shapePaintLastCell;
    private bool isArrowDrawDragging;
    private bool arrowDrawRecordedHistory;
    private Vector2Int arrowDrawLastCell;
    private float leftPanelWidth = DefaultLeftPanelWidth;
    private float rightPanelWidth = DefaultRightPanelWidth;
    private float leftGeneralPanelRatio = 0.38f;
    private float rightControlsPanelRatio = 0.125f;
    private float rightDebugPanelRatio = 0.22f;
    private float statusHistoryPanelHeight = DefaultStatusHistoryPanelHeight;
    private int activeResizeHandle;
    private int activeVerticalResizeHandle;
    private Rect fileToolbarButtonRect;
    private Rect themeToolbarButtonRect;
    private Rect generatorToolbarButtonRect;
    private int width = 15;
    private int height = 15;
    private int selectedArrowIndex = -1;
    private int testerHighlightedArrowIndex = -1;
    private bool showCoordinates = true;
    private bool editBoardShape;
    private bool customShapeEnabled;

    private bool hasTesterResult;
    private bool testerSolved;
    private float gridZoom = DefaultGridZoom;
    private string fileName = "ArrowLevel.json";
    private string customSaveFolder;
    private bool fileDropdownOpen;
    private bool themeDropdownOpen;
    private bool generatorDropdownOpen;
    private bool toolbarPopupBlocksGridInput;
    private bool generatorUseCurrentShape = true;
    private bool generatorClearExisting = true;
    private bool generatorRandomSeed = true;
    private bool generatorAutoLength;
    private bool guideOverlayOpen;
    private bool generationInProgress;
    private bool playTestMode;
    private bool statusHistoryOpen;
    private GameManager playTestManager;
    private BoardCameraController playTestCameraController;
    private GameObject playTestBoardBackdrop;
    private Mesh playTestBoardBackdropMesh;
    private Material playTestBoardBackdropMaterial;
    private string widthText = "15";
    private string heightText = "15";
    private string currentStatusMessage;
    private string statusHistorySearch = string.Empty;
    private string statusMessage
    {
        get => currentStatusMessage;
        set => SetStatusMessage(value);
    }
    private string playTestStatus;
    private string lastSavedOrLoadedPath;
    private string generatorMinLengthText = "2";
    private string generatorMaxLengthText = "8";
    private string generatorFillPercentText = "85";
    private string generatorSeedText = "0";
    private string generatorAttemptText = "120";
    private float generatorMinimumLengthWeight = 0.5f;
    private int generatorColorModeIndex = 1;
    private int generatorAlgorithmModeIndex = (int)GeneratorAlgorithmMode.ProfileGuided;
    private int generatorComplexityPercent = 85;
    private int selectedThemeIndex;
    private GUIStyle centeredCellLabelStyle;
    private GUIStyle axisLabelStyle;
    private GUIStyle arrowHeadStyle;
    private GUIStyle gridInstructionStyle;
    private GUIStyle panelSectionHeaderStyle;
    private GUIStyle guideTitleStyle;
    private GUIStyle guideHeadingStyle;
    private GUIStyle guideBodyStyle;
    private GUIStyle guideCloseStyle;
    private GUIStyle dropdownMenuLabelStyle;
    private GUIStyle generationOverlayStyle;

    private void Awake()
    {
        Application.runInBackground = true;
        selectedThemeIndex = Mathf.Clamp(PlayerPrefs.GetInt(ThemePrefsKey, 0), 0, Themes.Length - 1);
        customSaveFolder = PlayerPrefs.GetString(SaveFolderPrefsKey, Application.persistentDataPath);
        leftPanelWidth = PlayerPrefs.GetFloat(LeftPanelWidthPrefsKey, DefaultLeftPanelWidth);
        rightPanelWidth = PlayerPrefs.GetFloat(RightPanelWidthPrefsKey, DefaultRightPanelWidth);
        leftGeneralPanelRatio = Mathf.Clamp(PlayerPrefs.GetFloat(LeftGeneralPanelRatioPrefsKey, leftGeneralPanelRatio), 0.15f, 0.85f);
        rightControlsPanelRatio = Mathf.Clamp(PlayerPrefs.GetFloat(RightControlsPanelRatioPrefsKey, rightControlsPanelRatio), 0.12f, 0.45f);
        rightDebugPanelRatio = Mathf.Clamp(PlayerPrefs.GetFloat(RightDebugPanelRatioPrefsKey, rightDebugPanelRatio), 0.12f, 0.55f);
        statusHistoryPanelHeight = Mathf.Max(
            MinStatusHistoryPanelHeight,
            PlayerPrefs.GetFloat(StatusHistoryPanelHeightPrefsKey, DefaultStatusHistoryPanelHeight));
        showCoordinates = PlayerPrefs.GetInt(ShowCoordinatesPrefsKey, showCoordinates ? 1 : 0) == 1;
        generatorColorModeIndex = Mathf.Clamp(PlayerPrefs.GetInt(GeneratorColorModePrefsKey, generatorColorModeIndex), 0, GeneratorColorModeNames.Length - 1);
        generatorAlgorithmModeIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(GeneratorAlgorithmModePrefsKey, generatorAlgorithmModeIndex),
            0,
            GeneratorAlgorithmModeNames.Length - 1);
        generatorMinimumLengthWeight = Mathf.Clamp(
            PlayerPrefs.GetFloat(GeneratorMinimumLengthWeightPrefsKey, generatorMinimumLengthWeight),
            GeneratorMinimumLengthWeightLimit,
            GeneratorMaximumLengthWeightLimit);
        generatorAutoLength = PlayerPrefs.GetInt(GeneratorAutoLengthPrefsKey, generatorAutoLength ? 1 : 0) == 1;
        generatorComplexityPercent = Mathf.Clamp(
            PlayerPrefs.GetInt(GeneratorComplexityPrefsKey, generatorComplexityPercent),
            25,
            100);

        EnsureCamera();
        NewLevel(false);
        ClearHistory();
        statusMessage = "Ready. Use File > Load JSON... or Save As... to choose a JSON file.";
    }

    private void OnDestroy()
    {
        ClearPlayableTestBackdrop();
    }

    private void SetStatusMessage(string value)
    {
        string nextMessage = value ?? string.Empty;
        if (string.Equals(currentStatusMessage, nextMessage, StringComparison.Ordinal))
        {
            return;
        }

        currentStatusMessage = nextMessage;
        if (!string.IsNullOrWhiteSpace(nextMessage))
        {
            statusHistory.Add($"{DateTime.Now:HH:mm:ss}  {nextMessage}");
            statusHistoryScroll.y = float.MaxValue;
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        ApplyCameraTheme();

        if (!showLevelGeneratorButton)
        {
            generatorDropdownOpen = false;
        }

        if (generationInProgress)
        {
            DrawGenerationOverlay();
            return;
        }

        ValidateLevel();
        HandleKeyboardShortcuts();
        DrawToolbar();
        HandleToolbarFileDropdownInput();
        HandleToolbarThemeDropdownInput();
        HandleToolbarGeneratorDropdownInput();
        HandleGuideOverlayInput();
        toolbarPopupBlocksGridInput = IsPointerOverOpenToolbarPopup(Event.current.mousePosition);
        Rect statusPanelRect = GetStatusPanelRect();
        HandleStatusHistoryPanelResize(statusPanelRect);
        statusPanelRect = GetStatusPanelRect();

        if (playTestMode)
        {
            DrawPlayableTesterOverlay();
            DrawStatusPanel(statusPanelRect);
            DrawToolbarFileDropdown();
            DrawToolbarThemeDropdown();
            DrawToolbarGeneratorDropdown();
            DrawGuideOverlay();
            DrawGenerationOverlay();
            return;
        }

        float panelTop = 46f;
        float panelHeight = Mathf.Max(120f, statusPanelRect.yMin - panelTop - PanelPadding);
        ClampPanelWidths();

        Rect leftRect = new Rect(PanelPadding, panelTop, leftPanelWidth, panelHeight);
        Rect rightRect = new Rect(Screen.width - rightPanelWidth - PanelPadding, panelTop, rightPanelWidth, panelHeight);
        Rect gridRect = new Rect(
            leftRect.xMax + PanelPadding,
            panelTop,
            Mathf.Max(MinGridPanelWidth, rightRect.xMin - leftRect.xMax - PanelPadding * 2f),
            panelHeight);
        Rect leftSplitterRect = new Rect(leftRect.xMax + (PanelPadding - SplitterWidth) * 0.5f, panelTop, SplitterWidth, panelHeight);
        Rect rightSplitterRect = new Rect(rightRect.xMin - PanelPadding + (PanelPadding - SplitterWidth) * 0.5f, panelTop, SplitterWidth, panelHeight);

        HandlePanelResizing(leftSplitterRect, rightSplitterRect);

        DrawGridPanel(gridRect);
        DrawLeftPanel(leftRect);
        DrawRightPanel(rightRect);
        DrawPanelResizeHandle(leftSplitterRect, activeResizeHandle == 1);
        DrawPanelResizeHandle(rightSplitterRect, activeResizeHandle == 2);
        DrawStatusPanel(statusPanelRect);
        DrawToolbarFileDropdown();
        DrawToolbarThemeDropdown();
        DrawToolbarGeneratorDropdown();
        DrawGuideOverlay();
        DrawGenerationOverlay();
    }

    private void ClampPanelWidths()
    {
        float maxLeftWidth = Mathf.Max(MinLeftPanelWidth, Screen.width - rightPanelWidth - MinGridPanelWidth - PanelPadding * 4f);
        leftPanelWidth = Mathf.Clamp(leftPanelWidth, MinLeftPanelWidth, maxLeftWidth);

        float maxRightWidth = Mathf.Max(MinRightPanelWidth, Screen.width - leftPanelWidth - MinGridPanelWidth - PanelPadding * 4f);
        rightPanelWidth = Mathf.Clamp(rightPanelWidth, MinRightPanelWidth, maxRightWidth);
    }

    private void HandlePanelResizing(Rect leftSplitterRect, Rect rightSplitterRect)
    {
        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            if (leftSplitterRect.Contains(currentEvent.mousePosition))
            {
                activeResizeHandle = 1;
                currentEvent.Use();
                return;
            }

            if (rightSplitterRect.Contains(currentEvent.mousePosition))
            {
                activeResizeHandle = 2;
                currentEvent.Use();
                return;
            }
        }

        if (currentEvent.type == EventType.MouseDrag && activeResizeHandle != 0)
        {
            if (activeResizeHandle == 1)
            {
                leftPanelWidth = currentEvent.mousePosition.x - PanelPadding;
            }
            else
            {
                rightPanelWidth = Screen.width - PanelPadding - currentEvent.mousePosition.x;
            }

            ClampPanelWidths();
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp && activeResizeHandle != 0)
        {
            activeResizeHandle = 0;
            SavePanelWidthPreferences();
            currentEvent.Use();
        }
    }

    private void DrawPanelResizeHandle(Rect rect, bool active)
    {
        Color handleColor = CurrentTheme.GridText;
        handleColor.a = active ? 0.75f : 0.32f;
        Rect lineRect = new Rect(rect.center.x - 1f, rect.y + 10f, 2f, Mathf.Max(0f, rect.height - 20f));
        DrawRect(lineRect, handleColor);
    }

    private void SavePanelWidthPreferences()
    {
        PlayerPrefs.SetFloat(LeftPanelWidthPrefsKey, leftPanelWidth);
        PlayerPrefs.SetFloat(RightPanelWidthPrefsKey, rightPanelWidth);
        PlayerPrefs.Save();
    }

    private void HandleLeftVerticalPanelResize(
        Rect panelRect,
        Rect splitterRect,
        float availableHeight,
        float minimumGeneralHeight,
        float maximumGeneralHeight)
    {
        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.MouseDown &&
            currentEvent.button == 0 &&
            splitterRect.Contains(currentEvent.mousePosition))
        {
            activeVerticalResizeHandle = 3;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && activeVerticalResizeHandle == 3)
        {
            float generalHeight = Mathf.Clamp(
                currentEvent.mousePosition.y - panelRect.y,
                minimumGeneralHeight,
                maximumGeneralHeight);
            leftGeneralPanelRatio = availableHeight > 0f
                ? generalHeight / availableHeight
                : leftGeneralPanelRatio;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp && activeVerticalResizeHandle == 3)
        {
            activeVerticalResizeHandle = 0;
            SaveVerticalPanelPreferences();
            currentEvent.Use();
        }
    }

    private void HandleRightVerticalPanelResize(
        Rect panelRect,
        Rect controlsRect,
        Rect firstSplitterRect,
        Rect secondSplitterRect,
        float availableHeight,
        float minimumControlsHeight,
        float maximumControlsHeight,
        float minimumDebugHeight,
        float maximumDebugHeight)
    {
        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            if (firstSplitterRect.Contains(currentEvent.mousePosition))
            {
                activeVerticalResizeHandle = 4;
                currentEvent.Use();
                return;
            }

            if (secondSplitterRect.Contains(currentEvent.mousePosition))
            {
                activeVerticalResizeHandle = 5;
                currentEvent.Use();
                return;
            }
        }

        if (currentEvent.type == EventType.MouseDrag && activeVerticalResizeHandle == 4)
        {
            float controlsHeight = Mathf.Clamp(
                currentEvent.mousePosition.y - panelRect.y,
                minimumControlsHeight,
                maximumControlsHeight);
            rightControlsPanelRatio = availableHeight > 0f
                ? controlsHeight / availableHeight
                : rightControlsPanelRatio;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && activeVerticalResizeHandle == 5)
        {
            float debugTop = controlsRect.yMax + PanelPadding;
            float debugHeight = Mathf.Clamp(
                currentEvent.mousePosition.y - debugTop,
                minimumDebugHeight,
                maximumDebugHeight);
            rightDebugPanelRatio = availableHeight > 0f
                ? debugHeight / availableHeight
                : rightDebugPanelRatio;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp &&
            (activeVerticalResizeHandle == 4 || activeVerticalResizeHandle == 5))
        {
            activeVerticalResizeHandle = 0;
            SaveVerticalPanelPreferences();
            currentEvent.Use();
        }
    }

    private void DrawHorizontalPanelResizeHandle(Rect rect, bool active)
    {
        Color handleColor = CurrentTheme.GridText;
        handleColor.a = active ? 0.75f : 0.32f;
        Rect lineRect = new Rect(rect.x + 10f, rect.center.y - 1f, Mathf.Max(0f, rect.width - 20f), 2f);
        DrawRect(lineRect, handleColor);
    }

    private void SaveVerticalPanelPreferences()
    {
        PlayerPrefs.SetFloat(LeftGeneralPanelRatioPrefsKey, leftGeneralPanelRatio);
        PlayerPrefs.SetFloat(RightControlsPanelRatioPrefsKey, rightControlsPanelRatio);
        PlayerPrefs.SetFloat(RightDebugPanelRatioPrefsKey, rightDebugPanelRatio);
        PlayerPrefs.Save();
    }

    private Rect GetStatusPanelRect()
    {
        float panelHeight = StatusBarHeight;
        if (statusHistoryOpen)
        {
            statusHistoryPanelHeight = Mathf.Clamp(
                statusHistoryPanelHeight,
                MinStatusHistoryPanelHeight,
                GetMaxStatusHistoryPanelHeight());
            panelHeight = statusHistoryPanelHeight;
        }

        return new Rect(0f, Mathf.Max(40f, Screen.height - panelHeight), Screen.width, panelHeight);
    }

    private float GetMaxStatusHistoryPanelHeight()
    {
        const float minimumEditorHeight = 120f;
        float availableHeight = Screen.height - 46f - PanelPadding - minimumEditorHeight;
        return Mathf.Max(MinStatusHistoryPanelHeight, availableHeight);
    }

    private void HandleStatusHistoryPanelResize(Rect panelRect)
    {
        if (!statusHistoryOpen)
        {
            if (activeVerticalResizeHandle == 6)
            {
                activeVerticalResizeHandle = 0;
            }

            return;
        }

        Rect splitterRect = new Rect(panelRect.x, panelRect.y, panelRect.width, PanelPadding);
        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseDown
            && currentEvent.button == 0
            && splitterRect.Contains(currentEvent.mousePosition))
        {
            activeVerticalResizeHandle = 6;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && activeVerticalResizeHandle == 6)
        {
            statusHistoryPanelHeight = Mathf.Clamp(
                Screen.height - currentEvent.mousePosition.y,
                MinStatusHistoryPanelHeight,
                GetMaxStatusHistoryPanelHeight());
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp && activeVerticalResizeHandle == 6)
        {
            activeVerticalResizeHandle = 0;
            PlayerPrefs.SetFloat(StatusHistoryPanelHeightPrefsKey, statusHistoryPanelHeight);
            PlayerPrefs.Save();
            currentEvent.Use();
        }
    }

    private void DrawStatusPanel(Rect panelRect)
    {
        DrawSidePanelBackground(panelRect);
        Rect barRect = new Rect(
            panelRect.x,
            panelRect.yMax - StatusBarHeight,
            panelRect.width,
            StatusBarHeight);

        if (statusHistoryOpen)
        {
            Rect splitterRect = new Rect(panelRect.x, panelRect.y, panelRect.width, PanelPadding);
            Rect historyRect = new Rect(
                panelRect.x,
                splitterRect.yMax,
                panelRect.width,
                Mathf.Max(0f, barRect.yMin - splitterRect.yMax));
            DrawHorizontalPanelResizeHandle(splitterRect, activeVerticalResizeHandle == 6);

            GUILayout.BeginArea(Shrink(historyRect, 8f));
            DrawPanelSectionHeader("Status History");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(48f));
            GUI.SetNextControlName(TextInputControlPrefix + "StatusHistorySearch");
            statusHistorySearch = GUILayout.TextField(statusHistorySearch ?? string.Empty);
            using (new GuiDisabledScope(string.IsNullOrEmpty(statusHistorySearch)))
            {
                if (GUILayout.Button("X", GUILayout.Width(26f)))
                {
                    statusHistorySearch = string.Empty;
                    GUIUtility.keyboardControl = 0;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            statusHistoryScroll = GUILayout.BeginScrollView(statusHistoryScroll);
            if (statusHistory.Count == 0)
            {
                GUILayout.Label("No status messages yet.", guideBodyStyle);
            }
            else
            {
                int matchingEntryCount = 0;
                for (int i = 0; i < statusHistory.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(statusHistorySearch)
                        && statusHistory[i].IndexOf(statusHistorySearch, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    matchingEntryCount++;
                    GUILayout.Label(statusHistory[i], guideBodyStyle);
                }

                if (matchingEntryCount == 0)
                {
                    GUILayout.Label("No status messages match this search.", guideBodyStyle);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        Color separatorColor = CurrentTheme.GridText;
        separatorColor.a = 0.35f;
        DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1f), separatorColor);

        GUILayout.BeginArea(new Rect(barRect.x + 8f, barRect.y + 4f, Mathf.Max(0f, barRect.width - 16f), barRect.height - 8f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Status", panelSectionHeaderStyle, GUILayout.Width(58f));
        GUILayout.Label(statusMessage ?? string.Empty, GUILayout.ExpandWidth(true));
        string historyButtonText = statusHistoryOpen
            ? "Hide History"
            : $"History ({statusHistory.Count})";
        if (GUILayout.Button(historyButtonText, GUILayout.Width(112f)))
        {
            statusHistoryOpen = !statusHistoryOpen;
            statusHistoryScroll.y = float.MaxValue;
            if (!statusHistoryOpen && activeVerticalResizeHandle == 6)
            {
                activeVerticalResizeHandle = 0;
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void EnsureCamera()
    {
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = CurrentTheme.CameraBackground;
            return;
        }

        GameObject cameraObject = new GameObject("RuntimeEditorCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = CurrentTheme.CameraBackground;
        camera.orthographic = true;
        cameraObject.tag = "MainCamera";
    }

    private void EnsureStyles()
    {
        int baseLabelFontSize = GUI.skin.label.fontSize > 0 ? GUI.skin.label.fontSize : 12;

        centeredCellLabelStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Normal
        };

        axisLabelStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };

        arrowHeadStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 20
        };

        gridInstructionStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = baseLabelFontSize + 4,
            wordWrap = true
        };

        panelSectionHeaderStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 13,
            wordWrap = false
        };

        guideTitleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 18
        };

        guideHeadingStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 13
        };

        guideBodyStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true
        };

        guideCloseStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };

        dropdownMenuLabelStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            wordWrap = false
        };

        generationOverlayStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 28
        };

        centeredCellLabelStyle.normal.textColor = CurrentTheme.GridText;
        centeredCellLabelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(GetCellSize() * 0.32f), 8, 15);
        axisLabelStyle.normal.textColor = CurrentTheme.GridText;
        axisLabelStyle.fontSize = 11;
        gridInstructionStyle.normal.textColor = Color.yellow;
        gridInstructionStyle.fontSize = baseLabelFontSize + 4;
        gridInstructionStyle.wordWrap = true;
        panelSectionHeaderStyle.normal.textColor = Color.yellow;
        guideTitleStyle.normal.textColor = Color.yellow;
        guideHeadingStyle.normal.textColor = Color.yellow;
        guideBodyStyle.normal.textColor = CurrentTheme.GridText;
        guideCloseStyle.normal.textColor = Color.white;
        dropdownMenuLabelStyle.normal.textColor = Color.white;
        generationOverlayStyle.normal.textColor = Color.white;
    }

    private void HandleKeyboardShortcuts()
    {
        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.KeyDown)
        {
            return;
        }

        bool isModifierHeld = currentEvent.control || currentEvent.command;
        if (isModifierHeld)
        {
            if (currentEvent.keyCode == KeyCode.Z && currentEvent.shift)
            {
                RedoEdit();
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.Z)
            {
                UndoEdit();
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.Y)
            {
                RedoEdit();
                currentEvent.Use();
            }

            return;
        }

        if (IsTextInputFocused())
        {
            return;
        }

        switch (currentEvent.keyCode)
        {
            case KeyCode.W:
                CycleSelectedArrow(-1);
                currentEvent.Use();
                break;
            case KeyCode.S:
                CycleSelectedArrow(1);
                currentEvent.Use();
                break;
            case KeyCode.Q:
                AddArrow();
                currentEvent.Use();
                break;
            case KeyCode.E:
                DeleteSelectedArrowFromShortcut();
                currentEvent.Use();
                break;
        }
    }

    private bool IsTextInputFocused()
    {
        string focusedControl = GUI.GetNameOfFocusedControl();
        return !string.IsNullOrEmpty(focusedControl)
            && focusedControl.StartsWith(TextInputControlPrefix, StringComparison.Ordinal);
    }

    private void ClearEditorTextInputFocus()
    {
        GUI.FocusControl(null);
        GUIUtility.keyboardControl = 0;
    }

    private void CycleSelectedArrow(int direction)
    {
        if (arrows.Count == 0)
        {
            selectedArrowIndex = -1;
            statusMessage = "No arrows to select.";
            return;
        }

        if (!HasSelectedArrow())
        {
            selectedArrowIndex = direction > 0 ? 0 : arrows.Count - 1;
        }
        else
        {
            selectedArrowIndex = (selectedArrowIndex + direction + arrows.Count) % arrows.Count;
        }

        statusMessage = $"Selected {GetArrowDisplayName(selectedArrowIndex)}.";
    }

    private void DeleteSelectedArrowFromShortcut()
    {
        if (!HasSelectedArrow())
        {
            statusMessage = "Select an arrow before deleting.";
            return;
        }

        DeleteSelectedArrow();
    }
    private void DrawToolbar()
    {
        Rect toolbarRect = new Rect(0f, 0f, Screen.width, 40f);
        DrawSidePanelBackground(toolbarRect);
        Color separatorColor = CurrentTheme.GridText;
        separatorColor.a = 0.35f;
        DrawRect(new Rect(0f, toolbarRect.yMax - 1f, toolbarRect.width, 1f), separatorColor);

        GUILayout.BeginArea(toolbarRect);
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(4f);
        GUILayout.Label("Arrow Level Editor EXE", GUILayout.Width(170f));

        using (new GuiDisabledScope(!CanUndo()))
        {
            if (GUILayout.Button("Undo", GUILayout.Width(58f)))
            {
                UndoEdit();
            }
        }

        using (new GuiDisabledScope(!CanRedo()))
        {
            if (GUILayout.Button("Redo", GUILayout.Width(58f)))
            {
                RedoEdit();
            }
        }

        GUILayout.Space(8f);

        if (GUILayout.Button("File", GUILayout.Width(64f)))
        {
            fileDropdownOpen = !fileDropdownOpen;
            if (fileDropdownOpen)
            {
                themeDropdownOpen = false;
                generatorDropdownOpen = false;
                guideOverlayOpen = false;
            }
        }

        if (Event.current.type == EventType.Repaint)
        {
            fileToolbarButtonRect = GUILayoutUtility.GetLastRect();
        }

        if (GUILayout.Button($"Theme: {CurrentTheme.Name}", GUILayout.Width(128f)))
        {
            themeDropdownOpen = !themeDropdownOpen;
            if (themeDropdownOpen)
            {
                fileDropdownOpen = false;
                generatorDropdownOpen = false;
                guideOverlayOpen = false;
            }
        }

        if (Event.current.type == EventType.Repaint)
        {
            themeToolbarButtonRect = GUILayoutUtility.GetLastRect();
        }

        if (showLevelGeneratorButton && GUILayout.Button("Generator", GUILayout.Width(92f)))
        {
            generatorDropdownOpen = !generatorDropdownOpen;
            if (generatorDropdownOpen)
            {
                fileDropdownOpen = false;
                themeDropdownOpen = false;
                guideOverlayOpen = false;
                generatorDropdownScroll = Vector2.zero;
            }
        }

        if (showLevelGeneratorButton && Event.current.type == EventType.Repaint)
        {
            generatorToolbarButtonRect = GUILayoutUtility.GetLastRect();
        }
        else if (!showLevelGeneratorButton)
        {
            generatorToolbarButtonRect = Rect.zero;
        }

        if (GUILayout.Button("Guide", GUILayout.Width(68f)))
        {
            guideOverlayOpen = !guideOverlayOpen;
            if (guideOverlayOpen)
            {
                fileDropdownOpen = false;
                themeDropdownOpen = false;
                generatorDropdownOpen = false;
            }
        }

        if (GUILayout.Button("\u25B6\uFE0F Test Level", GUILayout.Width(98f)))
        {
            fileDropdownOpen = false;
            themeDropdownOpen = false;
            generatorDropdownOpen = false;
            guideOverlayOpen = false;
            GeneratePlayableTestLevel();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Zoom", GUILayout.Width(38f));
        if (GUILayout.Button("-", GUILayout.Width(28f)))
        {
            gridZoom = ClampGridZoom(gridZoom - GridZoomStep);
        }

        GUILayout.BeginVertical(GUILayout.Width(120f));
        GUILayout.Space(2f);
        gridZoom = DrawThemedHorizontalSlider(ClampGridZoom(gridZoom), GetMinGridZoom(), MaxGridZoom, 120f);
        GUILayout.EndVertical();

        if (GUILayout.Button("+", GUILayout.Width(28f)))
        {
            gridZoom = ClampGridZoom(gridZoom + GridZoomStep);
        }

        GUILayout.Label($"{gridZoom:0.0}x", GUILayout.Width(44f));

        if (GUILayout.Button("Reset Camera", GUILayout.Width(108f)))
        {
            ResetGridView();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void ResetGridView()
    {
        gridZoom = GetBoardFriendlyDefaultGridZoom();
        gridScroll = new Vector2(GridScrollPadding, GridScrollPadding);
        statusMessage = "Grid camera reset.";
    }

    private void HandleGuideOverlayInput()
    {
        if (!guideOverlayOpen)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
        {
            guideOverlayOpen = false;
            currentEvent.Use();
            return;
        }

        Rect overlayRect = GetGuideOverlayRect();
        Rect closeRect = GetGuideCloseButtonRect(overlayRect);
        bool isPointerEvent = currentEvent.type == EventType.MouseDown
            || currentEvent.type == EventType.MouseUp
            || currentEvent.type == EventType.MouseDrag
            || currentEvent.type == EventType.ScrollWheel;

        if (!isPointerEvent || !overlayRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && closeRect.Contains(currentEvent.mousePosition))
        {
            guideOverlayOpen = false;
        }

        currentEvent.Use();
    }

    private void DrawGuideOverlay()
    {
        if (!guideOverlayOpen)
        {
            return;
        }

        Rect overlayRect = GetGuideOverlayRect();
        Color background = Color.Lerp(CurrentTheme.SidePanelBackground, Color.black, 0.18f);
        background.a = 0.98f;
        DrawRect(overlayRect, background);
        DrawRectOutline(overlayRect, CurrentTheme.GridText, 2f);

        Rect closeRect = GetGuideCloseButtonRect(overlayRect);
        Color closeColor = closeRect.Contains(Event.current.mousePosition)
            ? new Color(1f, 0.25f, 0.25f, 1f)
            : new Color(0.75f, 0.12f, 0.12f, 1f);
        DrawRect(closeRect, closeColor);
        DrawRectOutline(closeRect, Color.white, 1f);
        GUI.Label(closeRect, "X", guideCloseStyle);

        GUI.BeginGroup(overlayRect);
        DrawOutlinedLabel(new Rect(16f, 10f, overlayRect.width - 64f, 28f), "Guide", guideTitleStyle, Color.yellow, Color.black);

        float columnGap = 18f;
        float columnWidth = (overlayRect.width - 48f - columnGap) * 0.5f;
        float columnTop = 46f;
        float columnHeight = overlayRect.height - columnTop - 16f;
        float separatorX = 16f + columnWidth + columnGap * 0.5f - 0.5f;
        Color separatorColor = CurrentTheme.GridText;
        separatorColor.a = 0.32f;
        DrawRect(new Rect(separatorX, columnTop, 1f, columnHeight), separatorColor);

        GUILayout.BeginArea(new Rect(16f, columnTop, columnWidth, columnHeight));
        DrawGuideSection("Keyboard");
        DrawGuideLine("W / S", "Cycle previous / next arrow in the list.");
        DrawGuideLine("Q", "Add a new arrow.");
        DrawGuideLine("E", "Delete the selected arrow.");
        DrawGuideLine("Ctrl+Z", "Undo.");
        DrawGuideLine("Ctrl+Y", "Redo.");
        DrawGuideLine("Ctrl+Shift+Z", "Redo.");
        DrawGuideLine("Esc", "Close this guide.");
        DrawGuideSection("Grid");
        DrawGuideLine("Left click", "Select an existing arrow, or add the next point on an empty cell.");
        DrawGuideLine("Left drag", "Draw the selected arrow through every crossed cell.");
        DrawGuideLine("Drag back", "Move back through the arrow head to erase recent path.");
        DrawGuideLine("Right click", "Remove the last point if you do not drag.");
        DrawGuideLine("Right click\nDrag", "Pan around the grid.");
        DrawGuideLine("Edit Shape", "Turn on board shape painting and create a custom shape if needed.");
        DrawGuideLine("Shape drag", "Start on active cells to erase, inactive cells to restore.");
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(16f + columnWidth + columnGap, columnTop, columnWidth, columnHeight));
        DrawGuideSection("Top Bar");
        DrawGuideLine("File", "New, save, save as, load JSON, open folder, copy path.");
        DrawGuideLine("Theme", "Change the editor color theme.");
        DrawGuideLine("Generator", "Build incrementally validated solvable levels, biased toward 1-2 playable routes.");
        DrawGuideLine("- / + / slider", "Zoom the grid view.");
        DrawGuideLine("Reset Camera", "Restore default zoom and grid position.");

        DrawGuideSection("Panels");
        DrawGuideLine("Split bars", "Drag between panels to resize them.");
        DrawGuideLine("Coords", "Show or hide grid coordinates.");
        DrawGuideLine("Arrow buttons", "Select an arrow from the left list.");
        DrawGuideLine("Point buttons", "Remove last point or clear selected arrow points.");
        DrawGuideLine("Level Tester", "Play the current level or run the solver debug.");
        DrawGuideLine("Clear Order", "Review and highlight the solver order in its own panel.");
        GUILayout.EndArea();
        GUI.EndGroup();
    }

    private Rect GetGuideOverlayRect()
    {
        float overlayWidth = Mathf.Min(620f, Mathf.Max(320f, Screen.width - PanelPadding * 2f));
        float overlayHeight = Mathf.Min(540f, Mathf.Max(380f, Screen.height - 72f));
        float overlayX = Mathf.Clamp((Screen.width - overlayWidth) * 0.5f, PanelPadding, Screen.width - overlayWidth - PanelPadding);
        return new Rect(overlayX, 52f, overlayWidth, overlayHeight);
    }

    private Rect GetGuideCloseButtonRect(Rect overlayRect)
    {
        return new Rect(overlayRect.xMax - 34f, overlayRect.y + 10f, 24f, 24f);
    }

    private void DrawGuideSection(string title)
    {
        GUILayout.Space(4f);
        Rect titleRect = GUILayoutUtility.GetRect(new GUIContent(title), guideHeadingStyle, GUILayout.MinHeight(20f), GUILayout.ExpandWidth(true));
        DrawOutlinedLabel(titleRect, title, guideHeadingStyle, Color.yellow, Color.black);
    }

    private void DrawGuideLine(string control, string description)
    {
        float rowHeight = control.Contains("\n") || description.Contains("\n") ? 34f : 20f;
        GUILayout.BeginHorizontal(GUILayout.MinHeight(rowHeight));
        GUILayout.Label(control, guideBodyStyle, GUILayout.Width(106f), GUILayout.MinHeight(rowHeight));
        GUILayout.Label(description, guideBodyStyle, GUILayout.MinHeight(rowHeight));
        GUILayout.EndHorizontal();
    }

    private void DrawPanelSectionHeader(string title)
    {
        Rect headerRect = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
        Color background = Color.Lerp(CurrentTheme.SidePanelBackground, CurrentTheme.GridText, 0.18f);
        Color border = Color.Lerp(CurrentTheme.GridText, background, 0.25f);
        DrawRect(headerRect, background);
        DrawRectOutline(headerRect, border, 1f);
        DrawOutlinedLabel(headerRect, title, panelSectionHeaderStyle, Color.yellow, Color.black);
        GUILayout.Space(4f);
    }

    private void DrawOutlinedLabel(Rect rect, string text, GUIStyle style, Color fillColor, Color outlineColor)
    {
        Color previousColor = style.normal.textColor;
        style.normal.textColor = outlineColor;

        GUI.Label(new Rect(rect.x - 1f, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + 1f, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y - 1f, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y + 1f, rect.width, rect.height), text, style);

        style.normal.textColor = fillColor;
        GUI.Label(rect, text, style);
        style.normal.textColor = previousColor;
    }

    private void DrawOutlinedGUILayoutLabel(string text, GUIStyle style, float minHeight)
    {
        Rect rect = GUILayoutUtility.GetRect(new GUIContent(text), style, GUILayout.MinHeight(minHeight), GUILayout.ExpandWidth(true));
        DrawOutlinedLabel(rect, text, style, Color.yellow, Color.black);
    }

    private void DrawLeftPanel(Rect rect)
    {
        float panelGap = PanelPadding;
        float availableHeight = Mathf.Max(0f, rect.height - panelGap);
        float minimumGeneralHeight = Mathf.Min(210f, availableHeight * 0.4f);
        float minimumArrowHeight = Mathf.Min(220f, availableHeight * 0.45f);
        float maximumGeneralHeight = Mathf.Max(minimumGeneralHeight, availableHeight - minimumArrowHeight);
        float generalHeight = Mathf.Clamp(availableHeight * leftGeneralPanelRatio, minimumGeneralHeight, maximumGeneralHeight);

        Rect generalRect = new Rect(rect.x, rect.y, rect.width, generalHeight);
        Rect splitterRect = new Rect(rect.x, generalRect.yMax, rect.width, panelGap);
        Rect arrowRect = new Rect(rect.x, splitterRect.yMax, rect.width, Mathf.Max(0f, rect.yMax - splitterRect.yMax));
        HandleLeftVerticalPanelResize(rect, splitterRect, availableHeight, minimumGeneralHeight, maximumGeneralHeight);

        DrawSidePanelBackground(generalRect);
        GUILayout.BeginArea(Shrink(generalRect, 8f));
        leftScroll = GUILayout.BeginScrollView(leftScroll);
        DrawLeftGeneralPanelContents();
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawSidePanelBackground(arrowRect);
        GUILayout.BeginArea(Shrink(arrowRect, 8f));
        arrowListScroll = GUILayout.BeginScrollView(arrowListScroll);
        DrawArrowListPanelContents();
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawHorizontalPanelResizeHandle(splitterRect, activeVerticalResizeHandle == 3);
    }

    private void DrawLeftGeneralPanelContents()
    {
        DrawPanelSectionHeader("File");
        GUILayout.Label("File Name");
        GUI.SetNextControlName(TextInputControlPrefix + "FileName");
        fileName = GUILayout.TextField(fileName);
        if (!string.IsNullOrEmpty(lastSavedOrLoadedPath))
        {
            GUILayout.Label("Last file:");
            GUILayout.TextArea(lastSavedOrLoadedPath, GUILayout.MinHeight(42f));
        }

        GUILayout.Space(8f);
        DrawPanelSectionHeader("Level");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Width", GUILayout.Width(48f));
        GUI.SetNextControlName(TextInputControlPrefix + "Width");
        widthText = GUILayout.TextField(widthText, GUILayout.Width(54f));
        GUILayout.Label("Height", GUILayout.Width(52f));
        GUI.SetNextControlName(TextInputControlPrefix + "Height");
        heightText = GUILayout.TextField(heightText, GUILayout.Width(54f));

        if (GUILayout.Button("Apply", GUILayout.Width(62f)))
        {
            ApplySizeFields();
        }

        GUILayout.EndHorizontal();

        DrawBoardShapeControls();
    }

    private void DrawArrowListPanelContents()
    {
        DrawPanelSectionHeader("Arrows");
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Arrow"))
        {
            AddArrow();
        }

        using (new GuiDisabledScope(!HasSelectedArrow()))
        {
            if (GUILayout.Button("Delete"))
            {
                DeleteSelectedArrow();
            }
        }

        GUILayout.EndHorizontal();

        if (HasSelectedArrow())
        {
            RuntimeArrowDraft arrow = arrows[selectedArrowIndex];
            GUILayout.BeginHorizontal();
            GUILayout.Label("ID", GUILayout.Width(24f));
            GUI.SetNextControlName(TextInputControlPrefix + "ArrowId");
            arrow.id = GUILayout.TextField(arrow.id);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove Last Point"))
            {
                RemoveLastPoint();
            }

            if (GUILayout.Button("Clear Points"))
            {
                RecordHistory();
                arrow.points.Clear();
                ClearTesterResult();
                statusMessage = "Arrow points cleared.";
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6f);

        for (int i = 0; i < arrows.Count; i++)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = i == selectedArrowIndex ? CurrentTheme.SelectedListBackground : Color.white;

            if (GUILayout.Button($"{i + 1}. {GetArrowDisplayName(i)} ({arrows[i].points.Count} pts)"))
            {
                ClearEditorTextInputFocus();
                selectedArrowIndex = i;
                statusMessage = $"Selected {GetArrowDisplayName(i)}.";
            }

            GUI.backgroundColor = previousColor;
        }
    }

    private void DrawBoardShapeControls()
    {
        GUILayout.Space(8f);
        DrawPanelSectionHeader("Board Shape");
        GUILayout.Label(HasCustomShape()
            ? $"Custom shape: {activeCells.Count} active cells. Click or drag grid cells while editing."
            : "Full rectangle. Turn on Edit Shape to make holes or non-rectangular boards.");

        GUILayout.BeginHorizontal();
        Color previousBackgroundColor = GUI.backgroundColor;
        Color previousContentColor = GUI.contentColor;
        if (editBoardShape)
        {
            GUI.backgroundColor = new Color(0.9f, 0.12f, 0.12f, 1f);
            GUI.contentColor = Color.white;
        }

        bool newEditBoardShape = GUILayout.Toggle(
            editBoardShape,
            editBoardShape ? "Edit Shape: ON" : "Edit Shape",
            "Button");
        GUI.backgroundColor = previousBackgroundColor;
        GUI.contentColor = previousContentColor;
        if (newEditBoardShape != editBoardShape)
        {
            editBoardShape = newEditBoardShape;
            if (editBoardShape && !HasCustomShape())
            {
                EnableCustomShapeFromFullRectangle();
            }
        }

        if (GUILayout.Button("Full Rectangle"))
        {
            UseFullRectangleShape();
        }

        GUILayout.EndHorizontal();

        if (GUILayout.Button("Disable All Grid Cells"))
        {
            DisableAllShapeCells();
        }
    }

    private void HandleToolbarFileDropdownInput()
    {
        if (!fileDropdownOpen)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
        {
            return;
        }

        Rect dropdownRect = GetFileDropdownRect();
        Rect toolbarButtonRect = GetFileToolbarButtonRect();

        if (dropdownRect.Contains(currentEvent.mousePosition))
        {
            float localY = currentEvent.mousePosition.y - dropdownRect.y - 4f;
            int itemIndex = Mathf.FloorToInt(localY / 30f);

            if (itemIndex >= 0 && itemIndex < FileMenuNames.Length)
            {
                ExecuteFileMenuAction(itemIndex);
            }

            currentEvent.Use();
            return;
        }

        if (!toolbarButtonRect.Contains(currentEvent.mousePosition))
        {
            fileDropdownOpen = false;
            currentEvent.Use();
        }
    }

    private void HandleToolbarThemeDropdownInput()
    {
        if (!themeDropdownOpen)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
        {
            return;
        }

        Rect dropdownRect = GetThemeDropdownRect();
        Rect toolbarButtonRect = GetThemeToolbarButtonRect();

        if (dropdownRect.Contains(currentEvent.mousePosition))
        {
            float localY = currentEvent.mousePosition.y - dropdownRect.y - 4f;
            int themeIndex = Mathf.FloorToInt(localY / 30f);

            if (themeIndex >= 0 && themeIndex < ThemeNames.Length)
            {
                SelectTheme(themeIndex);
            }

            currentEvent.Use();
            return;
        }

        if (!toolbarButtonRect.Contains(currentEvent.mousePosition))
        {
            themeDropdownOpen = false;
            currentEvent.Use();
        }
    }

    private void HandleToolbarGeneratorDropdownInput()
    {
        if (!showLevelGeneratorButton || !generatorDropdownOpen)
        {
            generatorDropdownOpen = false;
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
        {
            return;
        }

        Rect dropdownRect = GetGeneratorDropdownRect();
        Rect toolbarButtonRect = GetGeneratorToolbarButtonRect();

        if (!dropdownRect.Contains(currentEvent.mousePosition) && !toolbarButtonRect.Contains(currentEvent.mousePosition))
        {
            generatorDropdownOpen = false;
        }
    }

    private bool IsPointerOverOpenToolbarPopup(Vector2 mousePosition)
    {
        return (fileDropdownOpen && GetFileDropdownRect().Contains(mousePosition))
            || (themeDropdownOpen && GetThemeDropdownRect().Contains(mousePosition))
            || (showLevelGeneratorButton && generatorDropdownOpen && GetGeneratorDropdownRect().Contains(mousePosition));
    }

    private void DrawToolbarFileDropdown()
    {
        if (!fileDropdownOpen)
        {
            return;
        }

        Rect dropdownRect = GetFileDropdownRect();
        Color background = Color.Lerp(CurrentTheme.CameraBackground, Color.black, 0.65f);
        background.a = 0.98f;
        DrawRect(dropdownRect, background);
        DrawRectOutline(dropdownRect, CurrentTheme.GridText, 2f);

        for (int i = 0; i < FileMenuNames.Length; i++)
        {
            Rect rowRect = new Rect(dropdownRect.x + 4f, dropdownRect.y + 4f + i * 30f, dropdownRect.width - 8f, 28f);
            bool isHovered = rowRect.Contains(Event.current.mousePosition);
            Color rowColor = new Color(0.08f, 0.08f, 0.08f, 0.98f);

            if (isHovered)
            {
                rowColor = Color.Lerp(rowColor, Color.white, 0.18f);
            }

            DrawRect(rowRect, rowColor);

            Color previousContentColor = GUI.contentColor;
            GUI.contentColor = Color.white;
            GUI.Label(rowRect, FileMenuNames[i], dropdownMenuLabelStyle);
            GUI.contentColor = previousContentColor;
        }
    }

    private void DrawToolbarGeneratorDropdown()
    {
        if (!showLevelGeneratorButton || !generatorDropdownOpen)
        {
            return;
        }

        Rect dropdownRect = GetGeneratorDropdownRect();
        Color background = Color.Lerp(CurrentTheme.CameraBackground, Color.black, 0.65f);
        background.a = 0.98f;
        DrawRect(dropdownRect, background);
        DrawRectOutline(dropdownRect, CurrentTheme.GridText, 2f);

        GUILayout.BeginArea(Shrink(dropdownRect, 8f));
        generatorDropdownScroll = GUILayout.BeginScrollView(generatorDropdownScroll);
        DrawOutlinedGUILayoutLabel("Procedural Generator", guideHeadingStyle, 22f);

        generatorUseCurrentShape = GUILayout.Toggle(generatorUseCurrentShape, "Use current custom board shape");
        generatorClearExisting = GUILayout.Toggle(generatorClearExisting, "Replace current arrows");
        generatorRandomSeed = GUILayout.Toggle(generatorRandomSeed, "Random seed");

        DrawGeneratorAlgorithmModeSelector();
        GeneratorAlgorithmMode selectedAlgorithm = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
        if (IsAdvancedGuidedMode(selectedAlgorithm))
        {
            DrawAdvancedGuidedGeneratorSettings(selectedAlgorithm);
        }

        DrawGeneratorColorModeSelector();

        GUILayout.Label("Arrow count: automatic (generation fills toward the target density).");
        bool newAutoLength = GUILayout.Toggle(generatorAutoLength, "Auto Length");
        if (newAutoLength != generatorAutoLength)
        {
            generatorAutoLength = newAutoLength;
            PlayerPrefs.SetInt(GeneratorAutoLengthPrefsKey, generatorAutoLength ? 1 : 0);
            PlayerPrefs.Save();
        }

        using (new GuiDisabledScope(generatorAutoLength))
        {
            DrawGeneratorTextField("Min Length", ref generatorMinLengthText);
            DrawGeneratorTextField("Max Length", ref generatorMaxLengthText);
            DrawGeneratorLengthWeightSlider();
        }

        if (generatorAutoLength)
        {
            CalculateAutomaticGeneratorLengthRange(
                GetGeneratorZoneCells(generatorUseCurrentShape),
                out int automaticMinLength,
                out int automaticMaxLength);
            GUILayout.Label($"Automatic range: {automaticMinLength}-{automaticMaxLength}.");
            GUILayout.Label("Short arrows are added when needed.");
        }

        DrawGeneratorFillPercentSlider();

        using (new GuiDisabledScope(generatorRandomSeed))
        {
            DrawGeneratorTextField("Seed", ref generatorSeedText);
        }

        DrawGeneratorTextField("Attempts", ref generatorAttemptText);

        GUILayout.Space(6f);
        if (GUILayout.Button("Generate Level", GUILayout.Height(30f)))
        {
            BeginProceduralGeneration();
        }

        if (GUILayout.Button("Close"))
        {
            generatorDropdownOpen = false;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawGeneratorColorModeSelector()
    {
        GUILayout.Space(4f);
        DrawOutlinedGUILayoutLabel("Editor Preview Colors", guideHeadingStyle, 20f);
        int newMode = GUILayout.SelectionGrid(generatorColorModeIndex, GeneratorColorModeNames, 2);
        if (newMode != generatorColorModeIndex)
        {
            generatorColorModeIndex = Mathf.Clamp(newMode, 0, GeneratorColorModeNames.Length - 1);
            PlayerPrefs.SetInt(GeneratorColorModePrefsKey, generatorColorModeIndex);
            PlayerPrefs.Save();
        }
    }

    private void DrawGeneratorAlgorithmModeSelector()
    {
        GUILayout.Space(4f);
        DrawOutlinedGUILayoutLabel("Algorithm", guideHeadingStyle, 20f);
        int newMode = GUILayout.SelectionGrid(generatorAlgorithmModeIndex, GeneratorAlgorithmModeNames, 2);
        if (newMode != generatorAlgorithmModeIndex)
        {
            generatorAlgorithmModeIndex = Mathf.Clamp(newMode, 0, GeneratorAlgorithmModeNames.Length - 1);
            PlayerPrefs.SetInt(GeneratorAlgorithmModePrefsKey, generatorAlgorithmModeIndex);
            PlayerPrefs.Save();
        }
    }

    private static bool IsAdvancedGuidedMode(GeneratorAlgorithmMode mode)
    {
        return mode == GeneratorAlgorithmMode.ComplexGuided
            || mode == GeneratorAlgorithmMode.ComplexGuidedDx
            || mode == GeneratorAlgorithmMode.ComplexGuidedDxFlow
            || mode == GeneratorAlgorithmMode.LockstepWeave;
    }

    private static bool UsesDependencyStructureRules(GeneratorAlgorithmMode mode)
    {
        return mode == GeneratorAlgorithmMode.ComplexGuided
            || mode == GeneratorAlgorithmMode.ComplexGuidedDx
            || mode == GeneratorAlgorithmMode.ComplexGuidedDxFlow
            || mode == GeneratorAlgorithmMode.LockstepWeave;
    }

    private static bool PrioritizesNarrowRouteProfile(GeneratorAlgorithmMode mode)
    {
        return mode == GeneratorAlgorithmMode.ComplexGuided
            || mode == GeneratorAlgorithmMode.ChainFocus
            || mode == GeneratorAlgorithmMode.CompactLocks
            || mode == GeneratorAlgorithmMode.ExpertMix;
    }

    private int GetGeneratorPlacementRouteCeiling(GeneratorAlgorithmMode mode, int currentPlayableRoutes)
    {
        if (mode == GeneratorAlgorithmMode.LockstepWeave)
        {
            return GeneratorTargetMaxPlayableRoutes;
        }

        return GetGeneratorPlacementRouteTarget(mode);
    }

    private int GetGeneratorDesiredPlacementRoutes(GeneratorAlgorithmMode mode, int currentPlayableRoutes)
    {
        if (mode == GeneratorAlgorithmMode.LockstepWeave)
        {
            return currentPlayableRoutes < GeneratorGateBuildThreshold
                ? Mathf.Min(GeneratorGateBuildThreshold, currentPlayableRoutes + 1)
                : 1;
        }

        return GetGeneratorPlacementRouteTarget(mode);
    }

    private int GetGeneratorFutureRouteCeiling(GeneratorAlgorithmMode mode)
    {
        return mode == GeneratorAlgorithmMode.LockstepWeave
            ? GeneratorTargetMaxPlayableRoutes
            : GetGeneratorPlacementRouteTarget(mode) + 1;
    }

    private int GetAdvancedRelaxedRouteFailureThreshold(GeneratorAlgorithmMode mode)
    {
        float strength = generatorComplexityPercent / 100f;
        switch (mode)
        {
            case GeneratorAlgorithmMode.ComplexGuided:
                return Mathf.RoundToInt(Mathf.Lerp(5f, 10f, strength));
            case GeneratorAlgorithmMode.ComplexGuidedDx:
                return Mathf.RoundToInt(Mathf.Lerp(8f, 14f, strength));
            case GeneratorAlgorithmMode.ComplexGuidedDxFlow:
                return Mathf.RoundToInt(Mathf.Lerp(9f, 15f, strength));
            case GeneratorAlgorithmMode.LockstepWeave:
                return Mathf.RoundToInt(Mathf.Lerp(8f, 14f, strength));
            case GeneratorAlgorithmMode.ChainFocus:
                return Mathf.RoundToInt(Mathf.Lerp(10f, 18f, strength));
            case GeneratorAlgorithmMode.Crossweave:
                return Mathf.RoundToInt(Mathf.Lerp(5f, 9f, strength));
            case GeneratorAlgorithmMode.Longform:
                return Mathf.RoundToInt(Mathf.Lerp(4f, 8f, strength));
            case GeneratorAlgorithmMode.CompactLocks:
                return Mathf.RoundToInt(Mathf.Lerp(8f, 14f, strength));
            case GeneratorAlgorithmMode.ExpertMix:
                return Mathf.RoundToInt(Mathf.Lerp(12f, 20f, strength));
            default:
                return Mathf.RoundToInt(Mathf.Lerp(6f, 14f, strength));
        }
    }

    private float GetAdvancedRouteWeight(GeneratorAlgorithmMode mode)
    {
        float strength = generatorComplexityPercent / 100f;
        switch (mode)
        {
            case GeneratorAlgorithmMode.ChainFocus:
                return Mathf.Lerp(24f, 38f, strength);
            case GeneratorAlgorithmMode.Crossweave:
                return Mathf.Lerp(12f, 22f, strength);
            case GeneratorAlgorithmMode.Longform:
                return Mathf.Lerp(10f, 18f, strength);
            case GeneratorAlgorithmMode.CompactLocks:
                return Mathf.Lerp(20f, 32f, strength);
            case GeneratorAlgorithmMode.ExpertMix:
                return Mathf.Lerp(26f, 42f, strength);
            case GeneratorAlgorithmMode.LockstepWeave:
                return Mathf.Lerp(16f, 26f, strength);
            case GeneratorAlgorithmMode.ComplexGuidedDx:
                return Mathf.Lerp(24f, 38f, strength);
            case GeneratorAlgorithmMode.ComplexGuidedDxFlow:
                return Mathf.Lerp(26f, 40f, strength);
            default:
                return Mathf.Lerp(14f, 28f, strength);
        }
    }

    private int GetGeneratorPlacementRouteTarget(GeneratorAlgorithmMode mode)
    {
        if (generatorComplexityPercent >= 100
            && (mode == GeneratorAlgorithmMode.ChainFocus || mode == GeneratorAlgorithmMode.ExpertMix))
        {
            return 1;
        }

        return GeneratorTargetMaxPlayableRoutes;
    }

    private static int GetGeneratorTimeBudgetMs(GeneratorAlgorithmMode mode, int fillPercent)
    {
        switch (mode)
        {
            case GeneratorAlgorithmMode.ChainFocus: return 6500;
            case GeneratorAlgorithmMode.Crossweave: return 7000;
            case GeneratorAlgorithmMode.CompactLocks: return 6500;
            case GeneratorAlgorithmMode.ExpertMix: return 7000;
            case GeneratorAlgorithmMode.ComplexGuided: return GeneratorComplexGuidedTimeBudgetMs;
            case GeneratorAlgorithmMode.ComplexGuidedDx: return GeneratorComplexGuidedDxTimeBudgetMs;
            case GeneratorAlgorithmMode.ComplexGuidedDxFlow: return GeneratorComplexGuidedDxFlowTimeBudgetMs;
            case GeneratorAlgorithmMode.LockstepWeave: return GeneratorLockstepWeaveTimeBudgetMs;
            case GeneratorAlgorithmMode.Longform:
                return GeneratorComplexGuidedTimeBudgetMs;
            default:
                return fillPercent >= 100 ? GeneratorFullFillTimeBudgetMs : GeneratorNormalTimeBudgetMs;
        }
    }

    private static float GetAdvancedAutomaticMinimumLengthWeight(GeneratorAlgorithmMode mode)
    {
        switch (mode)
        {
            case GeneratorAlgorithmMode.ChainFocus: return 0.42f;
            case GeneratorAlgorithmMode.Crossweave: return 0.28f;
            case GeneratorAlgorithmMode.Longform: return GeneratorMinimumLengthWeightLimit;
            case GeneratorAlgorithmMode.CompactLocks: return 0.72f;
            case GeneratorAlgorithmMode.ExpertMix: return 0.32f;
            case GeneratorAlgorithmMode.LockstepWeave: return 0.24f;
            case GeneratorAlgorithmMode.ComplexGuidedDx: return 0.24f;
            case GeneratorAlgorithmMode.ComplexGuidedDxFlow: return 0.22f;
            default: return 0.28f;
        }
    }

    private void DrawAdvancedGuidedGeneratorSettings(GeneratorAlgorithmMode mode)
    {
        GUILayout.Space(4f);
        DrawOutlinedGUILayoutLabel(GeneratorAlgorithmModeNames[(int)mode], guideHeadingStyle, 20f);

        if (GUILayout.Button("Use Recommended Settings", GUILayout.Height(28f)))
        {
            ApplyRecommendedAdvancedGeneratorSettings(mode);
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Complexity", GUILayout.Width(92f));
        int newComplexity = Mathf.RoundToInt(DrawThemedHorizontalSlider(generatorComplexityPercent, 25f, 100f, 122f));
        GUILayout.Label($"{newComplexity}%", GUILayout.Width(38f));
        GUILayout.EndHorizontal();
        if (newComplexity != generatorComplexityPercent)
        {
            generatorComplexityPercent = newComplexity;
            PlayerPrefs.SetInt(GeneratorComplexityPrefsKey, generatorComplexityPercent);
            PlayerPrefs.Save();
        }

        switch (mode)
        {
            case GeneratorAlgorithmMode.ChainFocus:
                GUILayout.Label("Builds narrow dependency chains with very few open routes.");
                GUILayout.Label("Best for deliberate move order and deeper clear sequences.");
                break;
            case GeneratorAlgorithmMode.Crossweave:
                GUILayout.Label("Favors arrows that cross exit corridors and interlock bounds.");
                GUILayout.Label("Best for visually woven boards with meaningful interference.");
                break;
            case GeneratorAlgorithmMode.Longform:
                GUILayout.Label("Favors long, board-spanning arrows with several turns.");
                GUILayout.Label("Best for fewer arrows with larger and more readable paths.");
                break;
            case GeneratorAlgorithmMode.CompactLocks:
                GUILayout.Label("Favors short and medium arrows packed into local blockers.");
                GUILayout.Label("Best for dense boards with frequent tactical decisions.");
                break;
            case GeneratorAlgorithmMode.ExpertMix:
                GUILayout.Label("Combines narrow routes, long paths, and strong turn variety.");
                GUILayout.Label("Best for the highest overall generated difficulty.");
                break;
            case GeneratorAlgorithmMode.LockstepWeave:
                GUILayout.Label("Builds remote arrow dependencies on an acyclic solve graph.");
                GUILayout.Label("Separates valid keys and protects long blocker sightlines.");
                break;
            case GeneratorAlgorithmMode.ComplexGuidedDx:
                GUILayout.Label("Keeps Complex Guided composition, then polishes the full board.");
                GUILayout.Label("Favors remote unlocks, region jumps, and meaningful arrows.");
                break;
            case GeneratorAlgorithmMode.ComplexGuidedDxFlow:
                GUILayout.Label("Builds on DX with long horizontal solve transitions.");
                GUILayout.Label("Favors distant hand-offs and a broad left-to-right flow.");
                break;
            default:
                GUILayout.Label("Uses Profile Guided shapes with deeper blocking chains,");
                GUILayout.Label("more useful intersections, and fewer open routes.");
                break;
        }
    }

    private void ApplyRecommendedAdvancedGeneratorSettings(GeneratorAlgorithmMode mode)
    {
        List<Vector2Int> zoneCells = GetGeneratorZoneCells(generatorUseCurrentShape);

        generatorAutoLength = true;
        generatorFillPercentText = "100";
        switch (mode)
        {
            case GeneratorAlgorithmMode.ChainFocus:
                generatorAttemptText = "360";
                generatorMinimumLengthWeight = 0.35f;
                generatorComplexityPercent = 92;
                break;
            case GeneratorAlgorithmMode.Crossweave:
                generatorAttemptText = "360";
                generatorMinimumLengthWeight = 0.25f;
                generatorComplexityPercent = 88;
                break;
            case GeneratorAlgorithmMode.Longform:
                generatorAttemptText = "320";
                generatorMinimumLengthWeight = GeneratorMinimumLengthWeightLimit;
                generatorComplexityPercent = 82;
                break;
            case GeneratorAlgorithmMode.CompactLocks:
                generatorAttemptText = "380";
                generatorMinimumLengthWeight = 0.65f;
                generatorComplexityPercent = 90;
                break;
            case GeneratorAlgorithmMode.ExpertMix:
                generatorAttemptText = "420";
                generatorMinimumLengthWeight = 0.3f;
                generatorComplexityPercent = 95;
                break;
            case GeneratorAlgorithmMode.LockstepWeave:
                generatorAttemptText = "600";
                generatorMinimumLengthWeight = 0.24f;
                generatorComplexityPercent = 100;
                generatorFillPercentText = "100";
                break;
            case GeneratorAlgorithmMode.ComplexGuidedDx:
                generatorAttemptText = "500";
                generatorMinimumLengthWeight = 0.24f;
                generatorComplexityPercent = 95;
                generatorFillPercentText = "100";
                break;
            case GeneratorAlgorithmMode.ComplexGuidedDxFlow:
                generatorAttemptText = "560";
                generatorMinimumLengthWeight = 0.22f;
                generatorComplexityPercent = 97;
                generatorFillPercentText = "100";
                break;
            default:
                generatorAttemptText = "300";
                generatorMinimumLengthWeight = GeneratorMinimumLengthWeightLimit;
                generatorComplexityPercent = 85;
                break;
        }

        PlayerPrefs.SetInt(GeneratorAutoLengthPrefsKey, 1);
        PlayerPrefs.SetFloat(GeneratorMinimumLengthWeightPrefsKey, generatorMinimumLengthWeight);
        PlayerPrefs.SetInt(GeneratorComplexityPrefsKey, generatorComplexityPercent);
        PlayerPrefs.Save();

        statusMessage = $"Recommended {GeneratorAlgorithmModeNames[(int)mode]} settings loaded for {zoneCells.Count} usable board cells.";
    }

    private void DrawGeneratorLengthWeightSlider()
    {
        int minimumPercent = Mathf.RoundToInt(generatorMinimumLengthWeight * 100f);
        int maximumPercent = 100 - minimumPercent;
        GUILayout.Space(4f);
        GUILayout.Label($"Length Weight: Min {minimumPercent}% / Max {maximumPercent}%");
        GUILayout.BeginHorizontal();
        GUILayout.Label("More Min", GUILayout.Width(62f));
        float displayedMaximumWeight = 1f - generatorMinimumLengthWeight;
        float newDisplayedMaximumWeight = DrawThemedHorizontalSlider(
            displayedMaximumWeight,
            GeneratorMinimumLengthWeightLimit,
            GeneratorMaximumLengthWeightLimit,
            108f);
        GUILayout.Label("More Max", GUILayout.Width(62f));
        GUILayout.EndHorizontal();

        float newWeight = 1f - newDisplayedMaximumWeight;

        if (!Mathf.Approximately(newWeight, generatorMinimumLengthWeight))
        {
            generatorMinimumLengthWeight = newWeight;
            PlayerPrefs.SetFloat(GeneratorMinimumLengthWeightPrefsKey, generatorMinimumLengthWeight);
            PlayerPrefs.Save();
        }
    }

    private void DrawGeneratorFillPercentSlider()
    {
        int fillPercent = 85;
        if (int.TryParse(generatorFillPercentText, out int parsedFillPercent))
        {
            fillPercent = Mathf.Clamp(parsedFillPercent, 1, 100);
        }

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Fill", GUILayout.Width(38f));
        float sliderValue = DrawThemedHorizontalSlider(fillPercent, 1f, 100f, 160f);
        int newFillPercent = Mathf.RoundToInt(sliderValue);
        GUILayout.Label($"{newFillPercent}%", GUILayout.Width(44f));
        GUILayout.EndHorizontal();

        generatorFillPercentText = newFillPercent.ToString();
    }

    private float DrawThemedHorizontalSlider(float value, float minimum, float maximum, float width)
    {
        const float controlHeight = 22f;
        const float handleWidth = 16f;
        const float handleHeight = 18f;
        const float trackHeight = 8f;

        Rect controlRect = GUILayoutUtility.GetRect(
            width,
            controlHeight,
            GUILayout.Width(width),
            GUILayout.Height(controlHeight));
        Rect travelRect = new Rect(
            controlRect.x + handleWidth * 0.5f,
            controlRect.center.y - trackHeight * 0.5f,
            Mathf.Max(1f, controlRect.width - handleWidth),
            trackHeight);
        int controlId = GUIUtility.GetControlID(FocusType.Passive, controlRect);
        Event currentEvent = Event.current;

        if (GUI.enabled)
        {
            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (currentEvent.button == 0 && controlRect.Contains(currentEvent.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        value = SliderValueFromMouse(currentEvent.mousePosition.x, travelRect, minimum, maximum);
                        currentEvent.Use();
                    }

                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        value = SliderValueFromMouse(currentEvent.mousePosition.x, travelRect, minimum, maximum);
                        currentEvent.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId && currentEvent.button == 0)
                    {
                        value = SliderValueFromMouse(currentEvent.mousePosition.x, travelRect, minimum, maximum);
                        GUIUtility.hotControl = 0;
                        currentEvent.Use();
                    }

                    break;
            }
        }

        value = Mathf.Clamp(value, minimum, maximum);
        if (currentEvent.type == EventType.Repaint)
        {
            int themeIndex = Mathf.Clamp(selectedThemeIndex, 0, Themes.Length - 1);
            Color trackColor = SliderTrackColors[themeIndex];
            Color fillColor = SliderFillColors[themeIndex];
            Color handleColor = SliderHandleColors[themeIndex];
            Color outlineColor = SliderOutlineColors[themeIndex];

            if (!GUI.enabled)
            {
                trackColor = Color.Lerp(trackColor, CurrentTheme.SidePanelBackground, 0.55f);
                fillColor = Color.Lerp(fillColor, CurrentTheme.SidePanelBackground, 0.55f);
                handleColor = Color.Lerp(handleColor, CurrentTheme.SidePanelBackground, 0.55f);
            }

            float normalizedValue = Mathf.InverseLerp(minimum, maximum, value);
            float handleX = Mathf.Lerp(travelRect.xMin, travelRect.xMax, normalizedValue);
            Rect fillRect = new Rect(travelRect.xMin, travelRect.yMin, Mathf.Max(0f, handleX - travelRect.xMin), travelRect.height);

            DrawRect(travelRect, trackColor);
            DrawRect(fillRect, fillColor);
            DrawRectOutline(travelRect, outlineColor, 1f);

            bool isHovered = controlRect.Contains(currentEvent.mousePosition);
            if (GUIUtility.hotControl == controlId)
            {
                handleColor = Color.Lerp(handleColor, fillColor, 0.35f);
            }
            else if (isHovered)
            {
                handleColor = Color.Lerp(handleColor, Color.white, 0.16f);
            }

            Rect handleRect = new Rect(
                handleX - handleWidth * 0.5f,
                controlRect.center.y - handleHeight * 0.5f,
                handleWidth,
                handleHeight);
            DrawRect(handleRect, handleColor);
            DrawRectOutline(handleRect, outlineColor, 2f);
        }

        return value;
    }

    private static float SliderValueFromMouse(float mouseX, Rect travelRect, float minimum, float maximum)
    {
        float normalizedValue = Mathf.InverseLerp(travelRect.xMin, travelRect.xMax, mouseX);
        return Mathf.Lerp(minimum, maximum, normalizedValue);
    }

    private void BeginProceduralGeneration()
    {
        if (generationInProgress)
        {
            return;
        }

        generatorDropdownOpen = false;
        fileDropdownOpen = false;
        themeDropdownOpen = false;
        guideOverlayOpen = false;
        generationInProgress = true;
        StartCoroutine(GenerateProceduralLevelAfterOverlay());
    }

    private IEnumerator GenerateProceduralLevelAfterOverlay()
    {
        // Let IMGUI render a complete modal frame before the synchronous generator runs.
        yield return null;
        yield return new WaitForEndOfFrame();

        try
        {
            GenerateProceduralLevel();
        }
        finally
        {
            generationInProgress = false;
        }
    }

    private void DrawGenerationOverlay()
    {
        if (!generationInProgress)
        {
            return;
        }

        Rect overlayRect = new Rect(0f, 0f, Screen.width, Screen.height);
        float overlayAlpha = playTestMode ? 0.82f : 0.42f;
        DrawRect(overlayRect, new Color(0f, 0f, 0f, overlayAlpha));
        Rect labelRect = new Rect(0f, Screen.height * 0.5f - 30f, Screen.width, 60f);
        DrawOutlinedLabel(labelRect, "Generating Level", generationOverlayStyle, Color.white, Color.black);

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseDown
            || currentEvent.type == EventType.MouseUp
            || currentEvent.type == EventType.MouseDrag
            || currentEvent.type == EventType.ScrollWheel
            || currentEvent.type == EventType.KeyDown
            || currentEvent.type == EventType.KeyUp)
        {
            currentEvent.Use();
        }
    }

    private void DrawGeneratorTextField(string label, ref string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(82f));
        GUI.SetNextControlName(TextInputControlPrefix + "Generator." + label);
        value = GUILayout.TextField(value ?? string.Empty, GUILayout.Width(118f));
        GUILayout.EndHorizontal();
    }

    private void DrawToolbarThemeDropdown()
    {
        if (!themeDropdownOpen)
        {
            return;
        }

        Rect dropdownRect = GetThemeDropdownRect();
        Color background = Color.Lerp(CurrentTheme.CameraBackground, Color.black, 0.65f);
        background.a = 0.98f;
        DrawRect(dropdownRect, background);
        DrawRectOutline(dropdownRect, CurrentTheme.GridText, 2f);

        for (int i = 0; i < ThemeNames.Length; i++)
        {
            Rect rowRect = new Rect(dropdownRect.x + 4f, dropdownRect.y + 4f + i * 30f, dropdownRect.width - 8f, 28f);
            bool isHovered = rowRect.Contains(Event.current.mousePosition);
            Color rowColor = i == selectedThemeIndex
                ? Color.Lerp(CurrentTheme.PreviewArrow, Color.black, 0.35f)
                : new Color(0.08f, 0.08f, 0.08f, 0.98f);

            if (isHovered)
            {
                rowColor = Color.Lerp(rowColor, Color.white, 0.18f);
            }

            DrawRect(rowRect, rowColor);

            Color previousContentColor = GUI.contentColor;
            GUI.contentColor = Color.white;
            GUI.Label(rowRect, ThemeNames[i], dropdownMenuLabelStyle);
            GUI.contentColor = previousContentColor;
        }
    }

    private Rect GetFileDropdownRect()
    {
        float dropdownWidth = Mathf.Max(190f, fileToolbarButtonRect.width);
        float dropdownHeight = FileMenuNames.Length * 30f + 8f;
        float dropdownX = Mathf.Clamp(fileToolbarButtonRect.x, PanelPadding, Screen.width - dropdownWidth - PanelPadding);
        return new Rect(dropdownX, 40f, dropdownWidth, dropdownHeight);
    }

    private Rect GetFileToolbarButtonRect()
    {
        return new Rect(fileToolbarButtonRect.x, 0f, fileToolbarButtonRect.width, 40f);
    }

    private void ExecuteFileMenuAction(int itemIndex)
    {
        fileDropdownOpen = false;

        switch (itemIndex)
        {
            case 0:
                NewLevel();
                break;
            case 1:
                SaveJson();
                break;
            case 2:
                SaveJsonAs();
                break;
            case 3:
                LoadJson();
                break;
            case 4:
                OpenJsonFolder();
                break;
            case 5:
                CopyCurrentJsonPath();
                break;
        }
    }

    private Rect GetThemeDropdownRect()
    {
        float dropdownWidth = Mathf.Max(168f, themeToolbarButtonRect.width);
        float dropdownHeight = ThemeNames.Length * 30f + 8f;
        float dropdownX = Mathf.Clamp(themeToolbarButtonRect.x, PanelPadding, Screen.width - dropdownWidth - PanelPadding);
        return new Rect(dropdownX, 40f, dropdownWidth, dropdownHeight);
    }

    private Rect GetThemeToolbarButtonRect()
    {
        return new Rect(themeToolbarButtonRect.x, 0f, themeToolbarButtonRect.width, 40f);
    }

    private Rect GetGeneratorDropdownRect()
    {
        float dropdownWidth = Mathf.Min(380f, Screen.width - PanelPadding * 2f);
        float dropdownHeight = Mathf.Clamp(Screen.height - 52f, 340f, 760f);
        float dropdownX = Mathf.Clamp(generatorToolbarButtonRect.x, PanelPadding, Screen.width - dropdownWidth - PanelPadding);
        return new Rect(dropdownX, 40f, dropdownWidth, dropdownHeight);
    }

    private Rect GetGeneratorToolbarButtonRect()
    {
        return new Rect(generatorToolbarButtonRect.x, 0f, generatorToolbarButtonRect.width, 40f);
    }

    private void SelectTheme(int themeIndex)
    {
        selectedThemeIndex = Mathf.Clamp(themeIndex, 0, Themes.Length - 1);
        PlayerPrefs.SetInt(ThemePrefsKey, selectedThemeIndex);
        PlayerPrefs.Save();
        themeDropdownOpen = false;
        ApplyCameraTheme();
        UpdatePlayableTestBackdropColor();
        statusMessage = $"Theme changed to {CurrentTheme.Name}.";
    }

    private void DrawSidePanelBackground(Rect rect)
    {
        DrawRect(rect, CurrentTheme.SidePanelBackground);
        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = CurrentTheme.SidePanelBackground;
        GUI.Box(rect, GUIContent.none);
        GUI.backgroundColor = previousBackground;
    }

    private void DrawGridPanel(Rect rect)
    {
        GUI.Box(rect, GUIContent.none);
        GUILayout.BeginArea(Shrink(rect, 8f));
        GUILayout.BeginHorizontal();
        string instructionText = editBoardShape
            ? "Grid - shape edit mode.\nClick or drag cells to paint the board shape. 0,0 is bottom-left."
            : "Grid - draw arrow mode.\nClick or drag cells to draw the selected arrow. 0,0 is bottom-left.";
        Rect instructionRect = GUILayoutUtility.GetRect(new GUIContent(instructionText), gridInstructionStyle, GUILayout.MinHeight(46f), GUILayout.ExpandWidth(true));
        DrawOutlinedLabel(instructionRect, instructionText, gridInstructionStyle, Color.yellow, Color.black);
        GUILayout.FlexibleSpace();
        bool newShowCoordinates = GUILayout.Toggle(showCoordinates, "Coords", GUILayout.Width(72f));
        if (newShowCoordinates != showCoordinates)
        {
            showCoordinates = newShowCoordinates;
            PlayerPrefs.SetInt(ShowCoordinatesPrefsKey, showCoordinates ? 1 : 0);
            PlayerPrefs.Save();
        }

        GUILayout.EndHorizontal();

        Rect viewRect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        Rect contentRect = new Rect(0f, 0f, GetGridContentWidth(), GetGridContentHeight());
        if (!toolbarPopupBlocksGridInput)
        {
            HandleGridRightMousePan(viewRect, contentRect);
        }

        gridScroll = ClampGridScroll(gridScroll, viewRect, contentRect);
        gridScroll = GUI.BeginScrollView(viewRect, gridScroll, contentRect);
        DrawGrid(contentRect);
        GUI.EndScrollView();
        GUILayout.EndArea();
    }

    private void HandleGridRightMousePan(Rect viewRect, Rect contentRect)
    {
        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 && viewRect.Contains(currentEvent.mousePosition))
        {
            isGridRightMousePanning = true;
            gridRightMouseMoved = false;
            gridPanStartMousePosition = currentEvent.mousePosition;
            gridPanLastMousePosition = currentEvent.mousePosition;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && isGridRightMousePanning)
        {
            Vector2 delta = currentEvent.mousePosition - gridPanLastMousePosition;
            gridPanLastMousePosition = currentEvent.mousePosition;

            if ((currentEvent.mousePosition - gridPanStartMousePosition).sqrMagnitude >= GridPanDragThreshold * GridPanDragThreshold)
            {
                gridRightMouseMoved = true;
            }

            gridScroll = ClampGridScroll(gridScroll - delta, viewRect, contentRect);
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp && currentEvent.button == 1 && isGridRightMousePanning)
        {
            isGridRightMousePanning = false;

            if (!gridRightMouseMoved)
            {
                RemoveLastPoint();
            }

            currentEvent.Use();
        }
    }

    private Vector2 ClampGridScroll(Vector2 value, Rect viewRect, Rect contentRect)
    {
        float maxX = Mathf.Max(0f, contentRect.width - viewRect.width);
        float maxY = Mathf.Max(0f, contentRect.height - viewRect.height);
        return new Vector2(Mathf.Clamp(value.x, 0f, maxX), Mathf.Clamp(value.y, 0f, maxY));
    }
    private void DrawRightPanel(Rect rect)
    {
        float panelGap = PanelPadding;
        float availableHeight = Mathf.Max(0f, rect.height - panelGap * 2f);
        float minimumControlsHeight = Mathf.Min(120f, availableHeight * 0.24f);
        float minimumDebugHeight = Mathf.Min(120f, availableHeight * 0.2f);
        float minimumClearOrderHeight = Mathf.Min(220f, availableHeight * 0.35f);
        float maximumControlsHeight = Mathf.Max(
            minimumControlsHeight,
            availableHeight - minimumDebugHeight - minimumClearOrderHeight);
        float controlsHeight = Mathf.Clamp(
            availableHeight * rightControlsPanelRatio,
            minimumControlsHeight,
            maximumControlsHeight);

        float remainingHeight = Mathf.Max(0f, availableHeight - controlsHeight);
        float maximumDebugHeight = Mathf.Max(minimumDebugHeight, remainingHeight - minimumClearOrderHeight);
        float debugHeight = Mathf.Clamp(
            availableHeight * rightDebugPanelRatio,
            minimumDebugHeight,
            maximumDebugHeight);
        float clearOrderHeight = Mathf.Max(0f, remainingHeight - debugHeight);

        Rect controlsRect = new Rect(rect.x, rect.y, rect.width, controlsHeight);
        Rect firstSplitterRect = new Rect(rect.x, controlsRect.yMax, rect.width, panelGap);
        Rect debugRect = new Rect(rect.x, firstSplitterRect.yMax, rect.width, debugHeight);
        Rect secondSplitterRect = new Rect(rect.x, debugRect.yMax, rect.width, panelGap);
        Rect clearOrderRect = new Rect(rect.x, secondSplitterRect.yMax, rect.width, clearOrderHeight);

        HandleRightVerticalPanelResize(
            rect,
            controlsRect,
            firstSplitterRect,
            secondSplitterRect,
            availableHeight,
            minimumControlsHeight,
            maximumControlsHeight,
            minimumDebugHeight,
            maximumDebugHeight);

        DrawSidePanelBackground(controlsRect);
        GUILayout.BeginArea(Shrink(controlsRect, 8f));
        DrawLevelTester();
        GUILayout.EndArea();

        DrawSidePanelBackground(debugRect);
        GUILayout.BeginArea(Shrink(debugRect, 8f));
        debugLogScroll = GUILayout.BeginScrollView(debugLogScroll);
        DrawValidationPanel();
        GUILayout.Space(8f);
        DrawDebugLogPanel();
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawSidePanelBackground(clearOrderRect);
        GUILayout.BeginArea(Shrink(clearOrderRect, 8f));
        clearOrderScroll = GUILayout.BeginScrollView(clearOrderScroll);
        DrawClearOrderPanel();
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawHorizontalPanelResizeHandle(firstSplitterRect, activeVerticalResizeHandle == 4);
        DrawHorizontalPanelResizeHandle(secondSplitterRect, activeVerticalResizeHandle == 5);
    }

    private void DrawValidationPanel()
    {
        DrawPanelSectionHeader("Validation");
        if (validationMessages.Count == 0)
        {
            GUILayout.Label("No validation issues found.");
        }
        else
        {
            for (int i = 0; i < validationMessages.Count; i++)
            {
                GUILayout.TextArea(validationMessages[i]);
            }
        }
    }

    private void DrawPlayableTesterOverlay()
    {
        Rect overlayRect = new Rect(PanelPadding, 46f, 420f, 190f);
        DrawSidePanelBackground(overlayRect);

        GUILayout.BeginArea(Shrink(overlayRect, 8f));
        DrawPanelSectionHeader("Level Tester");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Restart"))
        {
            RestartPlayableTestLevel();
        }

        if (GUILayout.Button("Editor View"))
        {
            ReturnToEditorView();
        }

        if (GUILayout.Button("Clear"))
        {
            ClearPlayableTestLevel();
        }
        GUILayout.EndHorizontal();

        DrawPlayableTesterZoomControls();
        GUILayout.Label($"Level: {GetCurrentLevelDisplayName()}", guideHeadingStyle);
        GUILayout.Label(string.IsNullOrEmpty(playTestStatus) ? "Mouse wheel zoom, drag empty board space to pan. Editor View clears the test board." : playTestStatus);
        GUILayout.EndArea();
    }

    private string GetCurrentLevelDisplayName()
    {
        string sourceName = string.IsNullOrWhiteSpace(lastSavedOrLoadedPath)
            ? fileName
            : Path.GetFileName(lastSavedOrLoadedPath);
        string displayName = Path.GetFileNameWithoutExtension(sourceName?.Trim());
        return string.IsNullOrWhiteSpace(displayName) ? "Untitled Level" : displayName;
    }

    private bool GeneratePlayableTestLevel()
    {
        ValidateLevel();
        if (validationMessages.Count > 0)
        {
            playTestStatus = "Cannot generate playable level: fix validation issues first.";
            statusMessage = playTestStatus;
            return false;
        }

        RuntimeArrowLevelDocument document = BuildDocument();
        List<PathArrowData> levelArrows = ConvertToPathArrowData(document);
        ForcePlayableTestArrowColor(levelArrows, Color.black);
        List<Vector2Int> levelActiveCells = ConvertToActiveCells(document);
        GameManager manager = EnsurePlayableTestManager();
        EnsurePlayableCameraController(manager);
        manager.ConfigureRuntimeLevelTester(playableTestArrowStyle, Camera.main);
        manager.BuildRuntimeLevel(document.width, document.height, levelArrows, levelActiveCells, document.UsesCustomShape);
        UpdatePlayableTestBackdrop(manager);
        ConfigurePlayableCameraZoom(manager);
        playTestMode = true;
        playTestStatus = $"Playing {document.width}x{document.height} level with {levelArrows.Count} arrows.";
        statusMessage = playTestStatus;
        return true;
    }

    private static void ForcePlayableTestArrowColor(List<PathArrowData> levelArrows, Color color)
    {
        for (int i = 0; i < levelArrows.Count; i++)
        {
            PathArrowData arrow = levelArrows[i];
            levelArrows[i] = new PathArrowData(arrow.Id, color, arrow.Points);
        }
    }

    private void RestartPlayableTestLevel()
    {
        if (playTestManager == null)
        {
            GeneratePlayableTestLevel();
            return;
        }

        playTestManager.RestartLevel();
        UpdatePlayableTestBackdrop(playTestManager);
        playTestMode = true;
        playTestStatus = "Playable level restarted.";
    }

    private void ClearPlayableTestLevel()
    {
        if (playTestManager != null)
        {
            playTestManager.ClearRuntimeLevelOverride();
        }

        playTestMode = false;
        if (playTestCameraController != null)
        {
            playTestCameraController.enabled = false;
        }
        playTestCameraController = null;
        ClearPlayableTestBackdrop();
        playTestStatus = "Playable test level cleared.";
        statusMessage = playTestStatus;
    }

    private void ReturnToEditorView()
    {
        if (playTestManager != null)
        {
            playTestManager.ClearRuntimeLevelOverride();
        }

        playTestMode = false;
        if (playTestCameraController != null)
        {
            playTestCameraController.enabled = false;
        }
        playTestCameraController = null;
        ClearPlayableTestBackdrop();
        playTestStatus = "Returned to editor view.";
        statusMessage = playTestStatus;
        ApplyCameraTheme();
    }

    private GameManager EnsurePlayableTestManager()
    {
        if (playTestManager != null)
        {
            return playTestManager;
        }

        GameObject managerObject = GameObject.Find(PlayTestManagerObjectName);
        if (managerObject == null)
        {
            managerObject = new GameObject(PlayTestManagerObjectName);
        }

        playTestManager = managerObject.GetComponent<GameManager>();
        if (playTestManager == null)
        {
            playTestManager = managerObject.AddComponent<GameManager>();
        }

        return playTestManager;
    }

    private BoardCameraController EnsurePlayableCameraController(GameManager manager)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            EnsureCamera();
            camera = Camera.main;
        }

        if (camera == null)
        {
            return null;
        }

        playTestCameraController = camera.GetComponent<BoardCameraController>();
        if (playTestCameraController == null)
        {
            playTestCameraController = camera.gameObject.AddComponent<BoardCameraController>();
        }

        playTestCameraController.enabled = true;
        return playTestCameraController;
    }

    private void ConfigurePlayableCameraZoom(GameManager manager)
    {
        if (playTestCameraController == null)
        {
            return;
        }

        playTestCameraController.ConfigureRuntimeTesterControls(
            Camera.main,
            manager,
            playableTestZoomStep,
            playableTestMinZoomSize,
            playableTestMaxZoomSize,
            playableTestPanPaddingCells);
    }
    private void UpdatePlayableTestBackdrop(GameManager manager)
    {
        if (manager == null)
        {
            ClearPlayableTestBackdrop();
            return;
        }

        EnsurePlayableTestBackdrop();

        Bounds boardBounds = manager.GetBoardWorldBounds();
        float padding = Mathf.Max(manager.CellSize * PlayTestBoardBackdropPaddingCells, 0.5f);
        Vector3 backdropSize = boardBounds.size + new Vector3(padding * 2f, padding * 2f, 0f);
        Vector3 center = boardBounds.center;

        playTestBoardBackdrop.transform.position = new Vector3(center.x, center.y, center.z + 0.25f);
        playTestBoardBackdrop.transform.localScale = new Vector3(Mathf.Max(0.1f, backdropSize.x), Mathf.Max(0.1f, backdropSize.y), 1f);
        UpdatePlayableTestBackdropColor();
    }

    private void EnsurePlayableTestBackdrop()
    {
        if (playTestBoardBackdrop != null)
        {
            return;
        }

        playTestBoardBackdrop = new GameObject(PlayTestBoardBackdropObjectName);
        playTestBoardBackdropMesh = CreatePlayableTestBackdropMesh();
        playTestBoardBackdropMaterial = CreateUnlitColorMaterial(GetPlayableTestBoardBackgroundColor());

        MeshFilter meshFilter = playTestBoardBackdrop.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = playTestBoardBackdropMesh;

        MeshRenderer meshRenderer = playTestBoardBackdrop.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = playTestBoardBackdropMaterial;
        meshRenderer.sortingOrder = PlayTestBoardBackdropSortingOrder;
    }

    private void UpdatePlayableTestBackdropColor()
    {
        if (playTestBoardBackdropMaterial == null)
        {
            return;
        }

        SetMaterialColor(playTestBoardBackdropMaterial, GetPlayableTestBoardBackgroundColor());
    }

    private Color GetPlayableTestBoardBackgroundColor()
    {
        Color tintedWhite = Color.Lerp(CurrentTheme.GridBackground, Color.white, 0.88f);
        tintedWhite.a = 1f;
        return tintedWhite;
    }

    private void ClearPlayableTestBackdrop()
    {
        DestroyRuntimeObject(playTestBoardBackdrop);
        DestroyRuntimeObject(playTestBoardBackdropMaterial);
        DestroyRuntimeObject(playTestBoardBackdropMesh);
        playTestBoardBackdrop = null;
        playTestBoardBackdropMaterial = null;
        playTestBoardBackdropMesh = null;
    }

    private static Mesh CreatePlayableTestBackdropMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "RuntimeLevelEditorPlayableTestBackdropMesh"
        };

        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateUnlitColorMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader);
        SetMaterialColor(material, color);
        return material;
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

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }

    private void DrawPlayableTesterZoomControls()
    {
        GUILayout.BeginHorizontal();
        using (new GuiDisabledScope(playTestCameraController == null))
        {
            if (GUILayout.Button("Zoom -"))
            {
                playTestCameraController.ZoomOut();
            }

            if (GUILayout.Button("Reset View"))
            {
                playTestCameraController.ResetZoom();
            }

            if (GUILayout.Button("Zoom +"))
            {
                playTestCameraController.ZoomIn();
            }
        }
        GUILayout.EndHorizontal();
    }

    private static List<PathArrowData> ConvertToPathArrowData(RuntimeArrowLevelDocument document)
    {
        List<PathArrowData> levelArrows = new List<PathArrowData>();
        if (document == null || document.arrows == null)
        {
            return levelArrows;
        }

        for (int i = 0; i < document.arrows.Count; i++)
        {
            RuntimeArrowJson source = document.arrows[i];
            if (source == null || source.points == null || source.points.Count < 2)
            {
                continue;
            }

            List<Vector2Int> points = new List<Vector2Int>();
            for (int pointIndex = 0; pointIndex < source.points.Count; pointIndex++)
            {
                points.Add(source.points[pointIndex].ToVector2Int());
            }

            string id = string.IsNullOrWhiteSpace(source.id) ? $"Arrow {i + 1}" : source.id;
            levelArrows.Add(new PathArrowData(id, source.color.ToColor(), points));
        }

        return levelArrows;
    }

    private static List<Vector2Int> ConvertToActiveCells(RuntimeArrowLevelDocument document)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        if (document == null || document.activeCells == null)
        {
            return result;
        }

        for (int i = 0; i < document.activeCells.Count; i++)
        {
            result.Add(document.activeCells[i].ToVector2Int());
        }

        return result;
    }
    private void DrawGrid(Rect contentRect)
    {
        Event currentEvent = Event.current;
        float cellSize = GetCellSize();
        float cellGap = GetCellGap();
        Vector2 gridOffset = GetGridDrawOffset();
        Rect gridBackground = new Rect(gridOffset.x + HeaderSize, gridOffset.y + HeaderSize, width * (cellSize + cellGap), height * (cellSize + cellGap));
        DrawRect(gridBackground, CurrentTheme.GridBackground);
        DrawCoordinateHeaders();
        if (!toolbarPopupBlocksGridInput)
        {
            HandleShapePaintInput(currentEvent, gridBackground);
            HandleArrowDrawInput(currentEvent, gridBackground);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Rect cellRect = GetCellRect(cell);
                Color cellColor = GetCellColor(cell);
                DrawRect(cellRect, cellColor);

                if (showCoordinates && cellSize >= 18f)
                {
                    GUI.Label(cellRect, $"{x},{y}", centeredCellLabelStyle);
                }

            }
        }

        DrawArrowPaths();
        DrawStartTileHighlight();

    }

    private void DrawCoordinateHeaders()
    {
        Vector2 gridOffset = GetGridDrawOffset();
        float cellSize = GetCellSize();
        float cellGap = GetCellGap();

        for (int x = 0; x < width; x++)
        {
            Rect headerRect = new Rect(gridOffset.x + HeaderSize + x * (cellSize + cellGap), gridOffset.y, cellSize, HeaderSize);
            GUI.Label(headerRect, x.ToString(), axisLabelStyle);
        }

        for (int y = 0; y < height; y++)
        {
            Rect headerRect = new Rect(gridOffset.x, gridOffset.y + HeaderSize + (height - 1 - y) * (cellSize + cellGap), HeaderSize, cellSize);
            GUI.Label(headerRect, y.ToString(), axisLabelStyle);
        }
    }

    private void DrawArrowPaths()
    {
        Rect clipRect = new Rect(0f, 0f, GetGridContentWidth(), GetGridContentHeight());
        GUI.BeginGroup(clipRect);

        for (int i = 0; i < arrows.Count; i++)
        {
            RuntimeArrowDraft arrow = arrows[i];
            if (arrow.points.Count == 0)
            {
                continue;
            }

            Color arrowColor = GetArrowPreviewColor(i);
            float lineWidth = i == testerHighlightedArrowIndex ? GetArrowLineWidth() * 1.55f : GetArrowLineWidth();

            for (int pointIndex = 0; pointIndex < arrow.points.Count - 1; pointIndex++)
            {
                Vector2Int a = arrow.points[pointIndex];
                Vector2Int b = arrow.points[pointIndex + 1];

                if (!IsInsideGrid(a) || !IsInsideGrid(b))
                {
                    continue;
                }

                DrawAxisAlignedSegment(GetCellCenter(a), GetCellCenter(b), arrowColor, lineWidth);
            }

            DrawArrowJoints(arrow, arrowColor, lineWidth);
            DrawArrowHead(arrow, arrowColor, lineWidth);
        }

        GUI.EndGroup();
    }

    private void DrawArrowJoints(RuntimeArrowDraft arrow, Color color, float lineWidth)
    {
        if (arrow.points.Count < 2)
        {
            return;
        }

        float jointSize = lineWidth;
        for (int pointIndex = 0; pointIndex < arrow.points.Count; pointIndex++)
        {
            Vector2Int point = arrow.points[pointIndex];
            if (!IsInsideGrid(point))
            {
                continue;
            }

            Vector2 center = GetCellCenter(point);
            DrawRect(new Rect(center.x - jointSize * 0.5f, center.y - jointSize * 0.5f, jointSize, jointSize), color);
        }
    }

    private void DrawAxisAlignedSegment(Vector2 start, Vector2 end, Color color, float width)
    {
        if (Mathf.Abs(start.x - end.x) >= Mathf.Abs(start.y - end.y))
        {
            float xMin = Mathf.Min(start.x, end.x) - width * 0.5f;
            float xMax = Mathf.Max(start.x, end.x) + width * 0.5f;
            DrawRect(new Rect(xMin, start.y - width * 0.5f, xMax - xMin, width), color);
            return;
        }

        float yMin = Mathf.Min(start.y, end.y) - width * 0.5f;
        float yMax = Mathf.Max(start.y, end.y) + width * 0.5f;
        DrawRect(new Rect(start.x - width * 0.5f, yMin, width, yMax - yMin), color);
    }

    private void DrawArrowHead(RuntimeArrowDraft arrow, Color color, float lineWidth)
    {
        if (arrow.points.Count < 2)
        {
            return;
        }

        Vector2Int previous = arrow.points[arrow.points.Count - 2];
        Vector2Int head = arrow.points[arrow.points.Count - 1];
        Vector2Int delta = head - previous;
        Vector2Int direction = new Vector2Int(Sign(delta.x), Sign(delta.y));

        if (direction == Vector2Int.zero || !IsInsideGrid(head))
        {
            return;
        }

        Vector2 headCenter = GetCellCenter(head);
        float size = Mathf.Clamp(GetCellSize() * 0.46f, lineWidth * 1.7f, GetCellSize() * 0.72f);
        float baseInset = Mathf.Max(lineWidth * 0.2f, size * 0.12f);
        DrawFilledDirectionalTriangle(headCenter, direction, size, color, baseInset);
    }

    private void DrawStartTileHighlight()
    {
        if (!HasSelectedArrow())
        {
            return;
        }

        RuntimeArrowDraft arrow = arrows[selectedArrowIndex];
        if (arrow.points.Count == 0 || !IsInsideGrid(arrow.points[0]))
        {
            return;
        }

        Rect cellRect = GetCellRect(arrow.points[0]);
        float inset = Mathf.Max(3f, GetCellSize() * 0.12f);
        Rect fillRect = Shrink(cellRect, inset);
        Color fillColor = CurrentTheme.StartTileFill;
        float pulse = 0.5f + Mathf.Sin(Time.realtimeSinceStartup * 4f) * 0.5f;
        fillColor.a = Mathf.Lerp(fillColor.a * 0.75f, Mathf.Min(1f, fillColor.a * 1.25f), pulse);

        DrawRect(fillRect, fillColor);
        DrawRectOutline(Shrink(cellRect, 1f), CurrentTheme.StartTileBorder, Mathf.Max(2f, GetCellGap()));
    }

    private void DrawLevelTester()
    {
        DrawPanelSectionHeader("Level Tester");
        if (GUILayout.Button("\u25B6\uFE0F Test Level"))
        {
            GeneratePlayableTestLevel();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Debug Level"))
        {
            RunLevelTest();
        }
    }

    private void DrawDebugLogPanel()
    {
        DrawPanelSectionHeader("Debug Log");
        if (!hasTesterResult)
        {
            GUILayout.Label("Run Debug Level to inspect solvability.");
            return;
        }

        GUILayout.TextArea(testerSolved
            ? $"Solvable in {testerClearOrder.Count} moves."
            : "Level is stuck before all arrows can escape.");

        if (testerMessages.Count == 0)
        {
            GUILayout.Label("No additional debug messages.");
            return;
        }

        for (int i = 0; i < testerMessages.Count; i++)
        {
            GUILayout.TextArea(testerMessages[i]);
        }
    }

    private void DrawClearOrderPanel()
    {
        DrawPanelSectionHeader("Clear Order");
        if (!hasTesterResult)
        {
            GUILayout.Label("Run Debug Level to calculate the clear order.");
            return;
        }

        if (testerClearOrder.Count == 0)
        {
            GUILayout.Label("No clear order is available.");
            return;
        }

        for (int i = 0; i < testerClearOrder.Count; i++)
        {
            int arrowIndex = testerClearOrder[i];
            GUILayout.BeginHorizontal();
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = arrowIndex == testerHighlightedArrowIndex ? CurrentTheme.TesterListBackground : Color.white;
            GUILayout.Label($"{i + 1}. {GetArrowDisplayName(arrowIndex)}");

            if (GUILayout.Button("Highlight", GUILayout.Width(88f)))
            {
                HighlightTesterArrow(arrowIndex);
            }

            GUI.backgroundColor = previousColor;
            GUILayout.EndHorizontal();
        }
    }

    private void HandleCellClick(Vector2Int cell)
    {
        TryAppendArrowDrawCell(cell);
    }

    private bool TryAppendArrowDrawCell(Vector2Int cell)
    {
        if (!IsCellActive(cell))
        {
            statusMessage = $"Cell {cell.x},{cell.y} is outside the active board shape.";
            return false;
        }

        if (!HasSelectedArrow())
        {
            if (IsCellOwnedByOtherArrow(cell, out int ownerIndex))
            {
                statusMessage = $"Cell {cell.x},{cell.y} is already used by {GetArrowDisplayName(ownerIndex)}.";
                return false;
            }

            EnsureArrowDrawHistoryRecorded();
            AddArrow(false);
        }

        RuntimeArrowDraft arrow = arrows[selectedArrowIndex];

        if (arrow.points.Count == 0)
        {
            if (IsCellOwnedByOtherArrow(cell, out int ownerIndex))
            {
                statusMessage = $"Cell {cell.x},{cell.y} is already used by {GetArrowDisplayName(ownerIndex)}.";
                return false;
            }
        }
        else
        {
            Vector2Int lastPoint = arrow.points[arrow.points.Count - 1];

            if (lastPoint == cell)
            {
                statusMessage = "That point is already the arrow head.";
                RefreshArrowDrawTrail(arrow.points);
                return true;
            }

            if (TryBacktrackArrowDrawCell(arrow, cell))
            {
                return true;
            }

            Vector2Int delta = cell - lastPoint;
            if (delta.x != 0 && delta.y != 0)
            {
                statusMessage = "Diagonal segments are not allowed.";
                return false;
            }

            if (WouldSegmentCrossInactiveCell(lastPoint, cell, out Vector2Int inactiveCell))
            {
                statusMessage = $"That segment crosses inactive cell {inactiveCell.x},{inactiveCell.y}.";
                return false;
            }

            if (WouldNewSegmentSelfOverlap(arrow, lastPoint, cell))
            {
                statusMessage = "That segment crosses this arrow's own body.";
                return false;
            }

            if (WouldNewSegmentOverlapOtherArrow(lastPoint, cell, out Vector2Int blockedCell, out int ownerIndex))
            {
                statusMessage = $"That segment crosses {GetArrowDisplayName(ownerIndex)} at {blockedCell.x},{blockedCell.y}.";
                return false;
            }
        }

        EnsureArrowDrawHistoryRecorded();

        if (ShouldExtendLastArrowSegment(arrow.points, cell))
        {
            arrow.points[arrow.points.Count - 1] = cell;
        }
        else
        {
            arrow.points.Add(cell);
        }

        RefreshArrowDrawTrail(arrow.points);
        ClearTesterResult();
        statusMessage = $"Added point {cell.x},{cell.y}.";
        return true;
    }

    private bool TryBacktrackArrowDrawCell(RuntimeArrowDraft arrow, Vector2Int cell)
    {
        if (arrow == null || arrow.points.Count < 2)
        {
            return false;
        }

        Vector2Int previous = arrow.points[arrow.points.Count - 2];
        Vector2Int head = arrow.points[arrow.points.Count - 1];

        if (!IsCellOnSegment(cell, previous, head))
        {
            return false;
        }

        EnsureArrowDrawHistoryRecorded();

        if (cell == previous)
        {
            arrow.points.RemoveAt(arrow.points.Count - 1);
        }
        else
        {
            arrow.points[arrow.points.Count - 1] = cell;
        }

        RefreshArrowDrawTrail(arrow.points);
        ClearTesterResult();
        statusMessage = $"Backtracked arrow to {cell.x},{cell.y}.";
        return true;
    }

    private bool IsCellOnSegment(Vector2Int cell, Vector2Int start, Vector2Int end)
    {
        if (start.x == end.x)
        {
            return cell.x == start.x
                && cell.y >= Mathf.Min(start.y, end.y)
                && cell.y <= Mathf.Max(start.y, end.y);
        }

        if (start.y == end.y)
        {
            return cell.y == start.y
                && cell.x >= Mathf.Min(start.x, end.x)
                && cell.x <= Mathf.Max(start.x, end.x);
        }

        return false;
    }

    private void RefreshArrowDrawTrail(List<Vector2Int> points)
    {
        arrowDrawTrailCells.Clear();

        if (points == null || points.Count == 0)
        {
            return;
        }

        if (points.Count == 1)
        {
            arrowDrawTrailCells.Add(points[0]);
            return;
        }

        FillCells(points, arrowDrawTrailCells);
    }

    private bool ShouldExtendLastArrowSegment(List<Vector2Int> points, Vector2Int nextCell)
    {
        if (points == null || points.Count < 2)
        {
            return false;
        }

        Vector2Int previous = points[points.Count - 2];
        Vector2Int head = points[points.Count - 1];
        Vector2Int oldDirection = new Vector2Int(Sign(head.x - previous.x), Sign(head.y - previous.y));
        Vector2Int newDirection = new Vector2Int(Sign(nextCell.x - head.x), Sign(nextCell.y - head.y));

        return oldDirection != Vector2Int.zero && oldDirection == newDirection;
    }

    private void AddArrow(bool recordHistory = true)
    {
        if (recordHistory)
        {
            RecordHistory();
        }

        RuntimeArrowDraft arrow = new RuntimeArrowDraft
        {
            id = $"Arrow {arrows.Count + 1}",
            color = CurrentTheme.PreviewArrow
        };

        arrows.Add(arrow);
        selectedArrowIndex = arrows.Count - 1;
        ClearTesterResult();
        statusMessage = "Arrow added. Click or drag grid cells from tail to head.";
    }

    private void DeleteSelectedArrow()
    {
        if (!HasSelectedArrow())
        {
            return;
        }

        RecordHistory();
        arrows.RemoveAt(selectedArrowIndex);
        selectedArrowIndex = Mathf.Clamp(selectedArrowIndex, -1, arrows.Count - 1);
        ClearTesterResult();
        statusMessage = "Arrow deleted.";
    }

    private void RemoveLastPoint()
    {
        if (!HasSelectedArrow())
        {
            return;
        }

        RuntimeArrowDraft arrow = arrows[selectedArrowIndex];

        if (arrow.points.Count == 0)
        {
            statusMessage = "No point to remove.";
            return;
        }

        RecordHistory();
        arrow.points.RemoveAt(arrow.points.Count - 1);
        ClearTesterResult();
        statusMessage = "Last point removed.";
    }

    private void ValidateLevel()
    {
        validationMessages.Clear();
        occupiedCellOwners.Clear();

        for (int arrowIndex = 0; arrowIndex < arrows.Count; arrowIndex++)
        {
            ValidateArrow(arrowIndex, arrows[arrowIndex]);
        }
    }

    private void ValidateArrow(int arrowIndex, RuntimeArrowDraft arrow)
    {
        if (arrow.points.Count == 0)
        {
            validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: needs at least 2 points.");
            return;
        }

        if (arrow.points.Count == 1)
        {
            Vector2Int singlePoint = arrow.points[0];

            if (!IsInsideGrid(singlePoint))
            {
                validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: start point is outside the board.");
                return;
            }

            if (occupiedCellOwners.TryGetValue(singlePoint, out int ownerIndex) && ownerIndex != arrowIndex)
            {
                validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: overlaps {GetArrowDisplayName(ownerIndex)} at {singlePoint.x},{singlePoint.y}.");
            }
            else
            {
                occupiedCellOwners[singlePoint] = arrowIndex;
            }

            validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: needs at least 2 points.");
            return;
        }

        HashSet<Vector2Int> arrowCells = new HashSet<Vector2Int>();

        for (int i = 0; i < arrow.points.Count - 1; i++)
        {
            Vector2Int start = arrow.points[i];
            Vector2Int end = arrow.points[i + 1];
            Vector2Int delta = end - start;

            if (!IsInsideGrid(start) || !IsInsideGrid(end))
            {
                validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: segment {i} is outside the board.");
                continue;
            }

            if (delta == Vector2Int.zero)
            {
                validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: segment {i} has duplicate points.");
                continue;
            }

            if (delta.x != 0 && delta.y != 0)
            {
                validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: segment {i} is diagonal.");
                continue;
            }

            Vector2Int step = new Vector2Int(Sign(delta.x), Sign(delta.y));
            int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

            for (int distance = 0; distance <= length; distance++)
            {
                Vector2Int cell = start + step * distance;
                bool sharedEndpoint = distance == 0 && i > 0;

                if (!IsInsideGrid(cell))
                {
                    validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: segment {i} crosses inactive/outside cell {cell.x},{cell.y}.");
                    continue;
                }

                if (!sharedEndpoint && !arrowCells.Add(cell))
                {
                    validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: cell {cell.x},{cell.y} is used more than once.");
                }

                if (occupiedCellOwners.TryGetValue(cell, out int ownerIndex) && ownerIndex != arrowIndex)
                {
                    validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: overlaps {GetArrowDisplayName(ownerIndex)} at {cell.x},{cell.y}.");
                }
                else
                {
                    occupiedCellOwners[cell] = arrowIndex;
                }
            }
        }

        Vector2Int previous = arrow.points[arrow.points.Count - 2];
        Vector2Int head = arrow.points[arrow.points.Count - 1];
        Vector2Int exitDelta = head - previous;

        if (exitDelta == Vector2Int.zero || (exitDelta.x != 0 && exitDelta.y != 0))
        {
            validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: last segment must be straight.");
        }

        if (PathArrowUtility.TryFindOwnExitBlock(arrow.points, width, height, HasCustomShape(), activeCells, out Vector2Int ownExitBlockCell))
        {
            validationMessages.Add($"{GetArrowDisplayName(arrowIndex)}: exit path hits its own body at {ownExitBlockCell.x},{ownExitBlockCell.y}.");
        }
    }

    private void RunLevelTest()
    {
        hasTesterResult = true;
        testerSolved = false;
        testerHighlightedArrowIndex = -1;
        testerMessages.Clear();
        testerClearOrder.Clear();
        ValidateLevel();

        if (validationMessages.Count > 0)
        {
            testerMessages.Add("Fix validation issues before testing solvability.");
            statusMessage = "Level test failed: validation issues found.";
            return;
        }

        List<TestArrow> testArrows = BuildTestArrows();
        Dictionary<Vector2Int, TestArrow> occupied = BuildTestOccupiedCells(testArrows);
        HashSet<int> removed = new HashSet<int>();

        while (removed.Count < testArrows.Count)
        {
            TestArrow escaped = null;

            for (int i = 0; i < testArrows.Count; i++)
            {
                TestArrow arrow = testArrows[i];
                if (!removed.Contains(arrow.index) && CanTestArrowEscape(arrow, occupied, removed, out _, out _))
                {
                    escaped = arrow;
                    break;
                }
            }

            if (escaped == null)
            {
                testerSolved = false;
                BuildTesterStuckMessages(testArrows, occupied, removed);
                statusMessage = "Level test failed: puzzle gets stuck.";
                return;
            }

            testerClearOrder.Add(escaped.index);
            removed.Add(escaped.index);

            foreach (Vector2Int cell in escaped.cells)
            {
                occupied.Remove(cell);
            }
        }

        testerSolved = true;
        testerMessages.Add("Every arrow can escape using the shown order.");
        statusMessage = $"Level test passed: {testerClearOrder.Count} moves.";
    }

    private List<TestArrow> BuildTestArrows()
    {
        List<TestArrow> result = new List<TestArrow>();

        for (int arrowIndex = 0; arrowIndex < arrows.Count; arrowIndex++)
        {
            RuntimeArrowDraft source = arrows[arrowIndex];
            TestArrow arrow = new TestArrow
            {
                index = arrowIndex,
                name = GetArrowDisplayName(arrowIndex),
                head = source.points[source.points.Count - 1]
            };

            arrow.points.AddRange(source.points);
            Vector2Int previous = source.points[source.points.Count - 2];
            Vector2Int exitDelta = arrow.head - previous;
            arrow.exitDirection = new Vector2Int(Sign(exitDelta.x), Sign(exitDelta.y));
            FillCells(arrow.points, arrow.cells);
            result.Add(arrow);
        }

        return result;
    }

    private Dictionary<Vector2Int, TestArrow> BuildTestOccupiedCells(List<TestArrow> testArrows)
    {
        Dictionary<Vector2Int, TestArrow> result = new Dictionary<Vector2Int, TestArrow>();

        foreach (TestArrow arrow in testArrows)
        {
            foreach (Vector2Int cell in arrow.cells)
            {
                result[cell] = arrow;
            }
        }

        return result;
    }

    private bool CanTestArrowEscape(TestArrow arrow, Dictionary<Vector2Int, TestArrow> occupied, HashSet<int> removed, out TestArrow blocker, out Vector2Int blockerCell)
    {
        blocker = null;
        blockerCell = Vector2Int.zero;
        Vector2Int checkPosition = arrow.head + arrow.exitDirection;

        // Shape holes are transparent to movement. Only leaving the rectangular
        // board ends the ray, so separated active regions can still block it.
        while (IsInsideGridBounds(checkPosition))
        {
            if (occupied.TryGetValue(checkPosition, out TestArrow possibleBlocker)
                && !removed.Contains(possibleBlocker.index))
            {
                blocker = possibleBlocker;
                blockerCell = checkPosition;
                return false;
            }

            checkPosition += arrow.exitDirection;
        }

        return true;
    }

    private void BuildTesterStuckMessages(List<TestArrow> testArrows, Dictionary<Vector2Int, TestArrow> occupied, HashSet<int> removed)
    {
        testerMessages.Add($"Cleared {removed.Count} of {testArrows.Count} arrows before getting stuck.");
        Dictionary<int, TestArrow> remainingByIndex = new Dictionary<int, TestArrow>();
        Dictionary<int, int> blockedBy = new Dictionary<int, int>();

        foreach (TestArrow arrow in testArrows)
        {
            if (removed.Contains(arrow.index))
            {
                continue;
            }

            remainingByIndex[arrow.index] = arrow;

            if (CanTestArrowEscape(arrow, occupied, removed, out TestArrow blocker, out Vector2Int blockerCell) && blocker == null)
            {
                testerMessages.Add($"{arrow.name}: unexpectedly appears movable.");
            }
            else if (blocker != null)
            {
                string blockerName = blocker.index == arrow.index ? "its own body" : blocker.name;
                testerMessages.Add($"{arrow.name}: blocked by {blockerName} at {blockerCell.x},{blockerCell.y}.");
                if (!removed.Contains(blocker.index))
                {
                    blockedBy[arrow.index] = blocker.index;
                }
            }
        }

        AppendTesterDeadlockCycles(remainingByIndex, blockedBy);
    }

    private void AppendTesterDeadlockCycles(
        Dictionary<int, TestArrow> remainingByIndex,
        Dictionary<int, int> blockedBy)
    {
        HashSet<int> processed = new HashSet<int>();

        foreach (int startIndex in remainingByIndex.Keys)
        {
            if (processed.Contains(startIndex))
            {
                continue;
            }

            List<int> path = new List<int>();
            Dictionary<int, int> pathPositions = new Dictionary<int, int>();
            int currentIndex = startIndex;

            while (remainingByIndex.ContainsKey(currentIndex) && !processed.Contains(currentIndex))
            {
                if (pathPositions.TryGetValue(currentIndex, out int cycleStart))
                {
                    List<string> cycleNames = new List<string>();
                    for (int i = cycleStart; i < path.Count; i++)
                    {
                        cycleNames.Add(remainingByIndex[path[i]].name);
                    }

                    cycleNames.Add(remainingByIndex[currentIndex].name);
                    testerMessages.Add($"Deadlock cycle: {string.Join(" -> ", cycleNames)}.");
                    break;
                }

                pathPositions[currentIndex] = path.Count;
                path.Add(currentIndex);

                if (!blockedBy.TryGetValue(currentIndex, out int nextIndex))
                {
                    break;
                }

                currentIndex = nextIndex;
            }

            for (int i = 0; i < path.Count; i++)
            {
                processed.Add(path[i]);
            }
        }
    }

    private void SaveJson()
    {
        if (string.IsNullOrWhiteSpace(lastSavedOrLoadedPath))
        {
            SaveJsonAs();
            return;
        }

        SaveJsonToPath(lastSavedOrLoadedPath);
    }

    private void SaveJsonAs()
    {
        if (!TryPickSaveJsonFile(out string selectedPath))
        {
            statusMessage = "Save As cancelled or unavailable.";
            return;
        }

        SaveJsonToPath(selectedPath);
    }

    private void SaveJsonToPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            statusMessage = "Could not save JSON: no file path selected.";
            return;
        }

        RuntimeArrowLevelDocument document = BuildDocument();
        string json = JsonUtility.ToJson(document, true);
        string resolvedPath = ResolveJsonPath(path);
        string directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resolvedPath, json);
        RememberJsonPath(resolvedPath);
        statusMessage = $"Saved JSON: {resolvedPath}";
    }

    private bool LoadJson()
    {
        return BrowseAndLoadJson();
    }

    private bool BrowseAndLoadJson()
    {
        if (!TryPickJsonFile(out string selectedPath))
        {
            statusMessage = "JSON file browse cancelled or unavailable.";
            return false;
        }

        return LoadJsonFromPath(selectedPath);
    }

    private bool LoadJsonFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            statusMessage = $"JSON file not found: {path}";
            return false;
        }

        RuntimeArrowLevelDocument document = JsonUtility.FromJson<RuntimeArrowLevelDocument>(File.ReadAllText(path));

        if (document == null)
        {
            statusMessage = "Could not read JSON file.";
            return false;
        }

        RecordHistory();
        ApplyDocument(document);
        RememberJsonPath(path);
        statusMessage = $"Loaded JSON: {path}";
        return true;
    }

    private void RememberJsonPath(string path)
    {
        string directory = Path.GetDirectoryName(path);
        string selectedFileName = Path.GetFileName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            customSaveFolder = directory;
            PlayerPrefs.SetString(SaveFolderPrefsKey, customSaveFolder);
        }

        if (!string.IsNullOrWhiteSpace(selectedFileName))
        {
            fileName = selectedFileName;
        }

        PlayerPrefs.Save();
        lastSavedOrLoadedPath = path;
    }

    private bool TryPickJsonFile(out string path)
    {
        return RuntimeJsonFileDialog.TryOpenJsonFile(GetSaveFolder(), out path);
    }

    private bool TryPickSaveJsonFile(out string path)
    {
        return RuntimeJsonFileDialog.TrySaveJsonFile(GetSaveFolder(), fileName, out path);
    }
    private RuntimeArrowLevelDocument BuildDocument()
    {
        RuntimeArrowLevelDocument document = new RuntimeArrowLevelDocument
        {
            width = width,
            height = height,
            hasCustomShape = HasCustomShape()
        };

        if (HasCustomShape())
        {
            List<Vector2Int> sortedActiveCells = new List<Vector2Int>(activeCells);
            sortedActiveCells.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            for (int i = 0; i < sortedActiveCells.Count; i++)
            {
                document.activeCells.Add(IntPoint.FromVector2Int(sortedActiveCells[i]));
            }
        }

        for (int i = 0; i < arrows.Count; i++)
        {
            RuntimeArrowDraft arrow = arrows[i];
            RuntimeArrowJson jsonArrow = new RuntimeArrowJson
            {
                id = string.IsNullOrWhiteSpace(arrow.id) ? $"Arrow {i + 1}" : arrow.id,
                color = SerializableColor.FromColor(arrow.color)
            };

            foreach (Vector2Int point in arrow.points)
            {
                jsonArrow.points.Add(IntPoint.FromVector2Int(point));
            }

            document.arrows.Add(jsonArrow);
        }

        return document;
    }

    private void ApplyDocument(RuntimeArrowLevelDocument document)
    {
        width = Mathf.Max(1, document.width);
        height = Mathf.Max(1, document.height);
        widthText = width.ToString();
        heightText = height.ToString();
        FitGridZoomToBoardIfNeeded();
        activeCells.Clear();
        customShapeEnabled = document.UsesCustomShape;

        if (document.activeCells != null)
        {
            for (int i = 0; i < document.activeCells.Count; i++)
            {
                Vector2Int cell = document.activeCells[i].ToVector2Int();
                if (IsInsideGridBounds(cell))
                {
                    activeCells.Add(cell);
                }
            }
        }

        arrows.Clear();

        if (document.arrows != null)
        {
            for (int i = 0; i < document.arrows.Count; i++)
            {
                RuntimeArrowJson source = document.arrows[i];
                RuntimeArrowDraft arrow = new RuntimeArrowDraft
                {
                    id = string.IsNullOrWhiteSpace(source.id) ? $"Arrow {i + 1}" : source.id,
                    color = source.color.ToColor()
                };

                if (source.points != null)
                {
                    for (int pointIndex = 0; pointIndex < source.points.Count; pointIndex++)
                    {
                        arrow.points.Add(source.points[pointIndex].ToVector2Int());
                    }
                }

                arrows.Add(arrow);
            }
        }

        selectedArrowIndex = arrows.Count > 0 ? 0 : -1;
        ClearTesterResult();
    }

    private void NewLevel(bool recordHistory = true)
    {
        if (recordHistory)
        {
            RecordHistory();
        }

        width = 15;
        height = 15;
        widthText = width.ToString();
        heightText = height.ToString();
        activeCells.Clear();
        customShapeEnabled = false;
        editBoardShape = false;
        FitGridZoomToBoardIfNeeded();
        arrows.Clear();
        selectedArrowIndex = -1;
        fileName = "ArrowLevel.json";
        lastSavedOrLoadedPath = null;
        ClearTesterResult();
        AddArrow(false);
        statusMessage = "New level created.";
    }

    private void ApplySizeFields()
    {
        int newWidth = width;
        int newHeight = height;

        if (int.TryParse(widthText, out int parsedWidth))
        {
            newWidth = Mathf.Clamp(parsedWidth, 1, 64);
        }

        if (int.TryParse(heightText, out int parsedHeight))
        {
            newHeight = Mathf.Clamp(parsedHeight, 1, 64);
        }

        if (newWidth != width || newHeight != height)
        {
            RecordHistory();
            width = newWidth;
            height = newHeight;
            PruneActiveCellsToBounds();
        }

        widthText = width.ToString();
        heightText = height.ToString();
        FitGridZoomToBoardIfNeeded();
        ClearTesterResult();
        statusMessage = "Board size applied.";
    }

    private void RecordHistory()
    {
        RuntimeArrowLevelDocument snapshot = BuildDocument();
        undoHistory.Add(snapshot);

        if (undoHistory.Count > MaxHistoryEntries)
        {
            undoHistory.RemoveAt(0);
        }

        redoHistory.Clear();
    }

    private void UndoEdit()
    {
        if (!CanUndo())
        {
            return;
        }

        redoHistory.Add(BuildDocument());
        RuntimeArrowLevelDocument previous = undoHistory[undoHistory.Count - 1];
        undoHistory.RemoveAt(undoHistory.Count - 1);
        ApplyDocument(previous);
        statusMessage = "Undid last edit.";
    }

    private void RedoEdit()
    {
        if (!CanRedo())
        {
            return;
        }

        undoHistory.Add(BuildDocument());
        RuntimeArrowLevelDocument next = redoHistory[redoHistory.Count - 1];
        redoHistory.RemoveAt(redoHistory.Count - 1);
        ApplyDocument(next);
        statusMessage = "Redid last edit.";
    }

    private void ClearHistory()
    {
        undoHistory.Clear();
        redoHistory.Clear();
    }

    private bool CanUndo()
    {
        return undoHistory.Count > 0;
    }

    private bool CanRedo()
    {
        return redoHistory.Count > 0;
    }

    private void OpenJsonFolder()
    {
        Application.OpenURL("file:///" + GetSaveFolder().Replace("\\", "/"));
    }

    private void CopyCurrentJsonPath()
    {
        GUIUtility.systemCopyBuffer = string.IsNullOrEmpty(lastSavedOrLoadedPath)
            ? GetSaveFolder()
            : lastSavedOrLoadedPath;
        statusMessage = "Copied path to clipboard.";
    }

    private string GetSaveFolder()
    {
        string folder = string.IsNullOrWhiteSpace(customSaveFolder)
            ? Application.persistentDataPath
            : customSaveFolder.Trim();

        try
        {
            return Path.GetFullPath(folder);
        }
        catch (Exception)
        {
            return Application.persistentDataPath;
        }
    }
    private string ResolveJsonPath(string input)
    {
        string safeName = string.IsNullOrWhiteSpace(input) ? "ArrowLevel.json" : input.Trim();

        if (!safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".json";
        }

        return Path.IsPathRooted(safeName)
            ? safeName
            : Path.Combine(GetSaveFolder(), safeName);
    }

    private void GenerateProceduralLevel()
    {
        int minLength = ParseGeneratorInt(generatorMinLengthText, 2, 2, 50);
        int maxLength = ParseGeneratorInt(generatorMaxLengthText, 8, minLength, 80);
        int fillPercent = ParseGeneratorInt(generatorFillPercentText, 85, 1, 100);
        int attemptsPerArrow = ParseGeneratorInt(generatorAttemptText, 120, 10, 5000);
        int seed = generatorRandomSeed ? Environment.TickCount : ParseGeneratorInt(generatorSeedText, 0, int.MinValue + 1, int.MaxValue);

        if (maxLength < minLength)
        {
            int swap = minLength;
            minLength = maxLength;
            maxLength = swap;
        }

        List<Vector2Int> zoneCells = GetGeneratorZoneCells(generatorUseCurrentShape);
        if (generatorAutoLength)
        {
            CalculateAutomaticGeneratorLengthRange(zoneCells, out minLength, out maxLength);
        }

        if (zoneCells.Count < minLength)
        {
            statusMessage = "Generator failed: board shape is too small for the minimum arrow length.";
            return;
        }

        HashSet<Vector2Int> zoneSet = new HashSet<Vector2Int>(zoneCells);
        bool fullRectangleZone = IsFullRectangleGeneratorZone(zoneSet);
        HashSet<Vector2Int> baseOccupied = new HashSet<Vector2Int>();
        List<RuntimeArrowDraft> baseArrows = generatorClearExisting ? new List<RuntimeArrowDraft>() : CloneRuntimeArrows(arrows);

        if (!TryCollectGeneratedOccupiedCells(baseArrows, zoneSet, baseOccupied))
        {
            statusMessage = "Generator failed: current arrows are invalid for this board. Replace current arrows or fix validation first.";
            return;
        }

        if (baseArrows.Count > 0 && !CanGeneratedLevelSolve(baseArrows, zoneSet))
        {
            statusMessage = "Generator failed: current arrows are not solvable. Replace current arrows or fix the level first.";
            return;
        }

        GeneratorAlgorithmMode algorithmMode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
        bool useAdvancedGuided = IsAdvancedGuidedMode(algorithmMode);
        List<Vector2Int> placementCells = zoneCells;
        HashSet<Vector2Int> placementSet = zoneSet;

        if (placementCells.Count < minLength)
        {
            statusMessage = "Generator failed: the board is too small for the minimum arrow length.";
            return;
        }

        int startingArrowCount = baseArrows.Count;
        int targetOccupiedCells = Mathf.Clamp(
            Mathf.RoundToInt(placementCells.Count * (fillPercent / 100f)),
            Mathf.Max(minLength, baseOccupied.Count),
            placementCells.Count);

        GeneratedLevelBuild bestBuild = null;
        DateTime generationStart = DateTime.UtcNow;
        int timeBudgetMs = GetGeneratorTimeBudgetMs(algorithmMode, fillPercent);
        int retryCount = algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDxFlow
            ? 16
            : algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDx ? 14
            : useAdvancedGuided ? 10 : (fillPercent >= 100 ? (fullRectangleZone ? 8 : 6) : 4);

        for (int retryIndex = 0; retryIndex < retryCount && !IsGeneratorTimeExpired(generationStart, timeBudgetMs); retryIndex++)
        {
            int passSeed = seed + retryIndex * 7919;
            System.Random rng = new System.Random(passSeed);
            List<RuntimeArrowDraft> generatedArrows = CloneRuntimeArrows(baseArrows);
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>(baseOccupied);

            int addedCount = RunProceduralGeneratorPass(
                placementCells,
                placementSet,
                zoneSet,
                generatedArrows,
                occupied,
                startingArrowCount,
                minLength,
                maxLength,
                targetOccupiedCells,
                attemptsPerArrow,
                fillPercent,
                rng,
                generationStart,
                timeBudgetMs);

            if (fillPercent >= 100
                && occupied.Count < targetOccupiedCells
                && !IsGeneratorTimeExpired(generationStart, timeBudgetMs))
            {
                CompleteGeneratedFullFill(
                    placementCells,
                    placementSet,
                    zoneSet,
                    generatedArrows,
                    occupied,
                    maxLength,
                    rng,
                    generationStart,
                    timeBudgetMs);
                addedCount = generatedArrows.Count - startingArrowCount;
            }

            if (addedCount <= 0 || !CanGeneratedLevelSolve(generatedArrows, zoneSet))
            {
                continue;
            }

            GeneratedLevelBuild build = CreateGeneratedLevelBuild(generatedArrows, occupied, addedCount, passSeed, false, zoneSet);

            if (IsBetterGeneratedBuild(build, bestBuild, targetOccupiedCells))
            {
                bestBuild = build;

                int desiredComplexDepth = Mathf.Max(2, Mathf.CeilToInt(bestBuild.addedCount * 0.55f));
                int desiredGateCount = Mathf.Clamp(Mathf.CeilToInt(bestBuild.addedCount * 0.08f), 2, 6);
                bool meetsGateNetworkTarget = algorithmMode == GeneratorAlgorithmMode.LockstepWeave
                    && bestBuild.initialPlayableCount <= GeneratorTargetMaxPlayableRoutes
                    && bestBuild.maxPlayableCount <= GeneratorTargetMaxPlayableRoutes
                    && bestBuild.oversizedUnlockWaveCount == 0
                    && bestBuild.controlledGateCount >= desiredGateCount
                    && bestBuild.nearBlockerRatio <= 0.35f
                    && bestBuild.remoteBlockerRatio >= 0.2f
                    && bestBuild.zeroImpactPlayableRatio <= 0.25f
                    && bestBuild.clusteredChoiceWaveRatio <= 0.25f
                    && bestBuild.shortPlayableExitLaneRatio <= 0.3f;
                bool meetsStandardTarget = algorithmMode != GeneratorAlgorithmMode.LockstepWeave
                    && algorithmMode != GeneratorAlgorithmMode.ComplexGuidedDx
                    && algorithmMode != GeneratorAlgorithmMode.ComplexGuidedDxFlow
                    && bestBuild.maxPlayableCount <= GeneratorTargetMaxPlayableRoutes
                    && bestBuild.maxUnlockWaveCount <= GeneratorTargetMaxPlayableRoutes
                    && (!useAdvancedGuided || bestBuild.dependencyDepth >= desiredComplexDepth);
                bool meetsComplexDxTarget = (algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDx
                        || algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDxFlow)
                    && bestBuild.initialPlayableCount <= GeneratorTargetMaxPlayableRoutes
                    && bestBuild.maxPlayableCount <= GeneratorGateBurstRouteCeiling
                    && bestBuild.maxUnlockWaveCount <= GeneratorGateBurstRouteCeiling
                    && bestBuild.dependencyDepth >= desiredComplexDepth
                    && bestBuild.dependencyParticipationRatio >= 0.7f
                    && bestBuild.zeroImpactPlayableRatio <= 0.45f
                    && bestBuild.clusteredChoiceWaveRatio <= 0.5f
                    && (algorithmMode != GeneratorAlgorithmMode.ComplexGuidedDxFlow
                        || (bestBuild.solveCrossColumnTransitionRatio >= 0.25f
                            && bestBuild.solveRegionTransitionRatio >= 0.3f
                            && bestBuild.solveLeftToRightProgress >= 0.12f
                            && bestBuild.solveHorizontalAreaCoverage >= 0.75f
                            && bestBuild.solveHorizontalAreaOrderScore >= 0.62f
                            && bestBuild.solveForwardAreaHandoffRatio >= 0.55f));
                if (bestBuild.occupied.Count >= targetOccupiedCells
                    && (meetsGateNetworkTarget || meetsComplexDxTarget || meetsStandardTarget))
                {
                    break;
                }
            }
        }

        if (bestBuild == null || bestBuild.arrows.Count == startingArrowCount)
        {
            statusMessage = "Generator failed: no valid arrows could be placed. Try fewer arrows, shorter lengths, or a bigger board.";
            return;
        }

        if (algorithmMode == GeneratorAlgorithmMode.LockstepWeave
            && bestBuild.occupied.Count < targetOccupiedCells)
        {
            DateTime densityRepairStart = DateTime.UtcNow;
            System.Random densityRepairRng = new System.Random(bestBuild.seed ^ 0x2c1b3c6d);
            CompleteGeneratedFullFill(
                placementCells,
                placementSet,
                zoneSet,
                bestBuild.arrows,
                bestBuild.occupied,
                maxLength,
                densityRepairRng,
                densityRepairStart,
                GeneratorGateDensityRepairTimeBudgetMs,
                targetOccupiedCells);
            bestBuild.addedCount = bestBuild.arrows.Count - startingArrowCount;
            ApplyGeneratedSolveProfile(bestBuild, zoneSet);
            ApplyGeneratedGeometryProfile(bestBuild);
            ApplyGeneratedSpatialDependencyProfile(bestBuild, zoneSet);
            bestBuild.complexityScore = CalculateAdvancedGuidedBuildScore(bestBuild);
        }

        if (fillPercent >= 100 && bestBuild.occupied.Count < targetOccupiedCells)
        {
            DateTime finalRepairStart = DateTime.UtcNow;
            System.Random finalRepairRng = new System.Random(bestBuild.seed ^ 0x5f3759df);
            int finalRepairBudgetMs = algorithmMode == GeneratorAlgorithmMode.LockstepWeave
                ? 8000
                : algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDxFlow
                    ? 4000
                    : algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDx ? 3500 : 1200;
            CompleteGeneratedFullFill(
                placementCells,
                placementSet,
                zoneSet,
                bestBuild.arrows,
                bestBuild.occupied,
                maxLength,
                finalRepairRng,
                finalRepairStart,
                finalRepairBudgetMs,
                -1,
                true);
            bestBuild.addedCount = bestBuild.arrows.Count - startingArrowCount;
            ApplyGeneratedSolveProfile(bestBuild, zoneSet);
            ApplyGeneratedGeometryProfile(bestBuild);
            ApplyGeneratedSpatialDependencyProfile(bestBuild, zoneSet);
            bestBuild.complexityScore = CalculateAdvancedGuidedBuildScore(bestBuild);
        }

        bool optimizedComplexBuild = false;
        if (UsesDependencyStructureRules(algorithmMode)
            && algorithmMode != GeneratorAlgorithmMode.LockstepWeave)
        {
            int mergedStraightArrows = ConsolidateGeneratedArrowsByTail(
                placementSet,
                zoneSet,
                bestBuild.arrows,
                startingArrowCount,
                minLength,
                maxLength,
                DateTime.UtcNow,
                3500);
            if (mergedStraightArrows > 0)
            {
                bestBuild.addedCount = bestBuild.arrows.Count - startingArrowCount;
                ApplyGeneratedSolveProfile(bestBuild, zoneSet);
                optimizedComplexBuild = true;
            }

            optimizedComplexBuild |= ImproveGeneratedOpeningRouteProfile(bestBuild, zoneSet, maxLength);
        }

        if (optimizedComplexBuild)
        {
            ApplyGeneratedGeometryProfile(bestBuild);
            ApplyGeneratedSpatialDependencyProfile(bestBuild, zoneSet);
            bestBuild.complexityScore = CalculateAdvancedGuidedBuildScore(bestBuild);
        }

        if (algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDx
            || algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDxFlow)
        {
            ImproveComplexGuidedDxSolveFlow(
                bestBuild,
                zoneSet,
                startingArrowCount,
                DateTime.UtcNow,
                algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDxFlow
                    ? GeneratorComplexGuidedDxFlowPolishBudgetMs
                    : GeneratorComplexGuidedDxPolishBudgetMs);
        }

        RecordHistory();
        ShuffleGeneratedVisualOrder(bestBuild.arrows, generatorClearExisting ? 0 : startingArrowCount, bestBuild.seed);
        ApplyGeneratedSolveProfile(bestBuild, zoneSet);
        arrows.Clear();
        arrows.AddRange(bestBuild.arrows);
        selectedArrowIndex = arrows.Count > 0 ? 0 : -1;

        if (!generatorUseCurrentShape)
        {
            activeCells.Clear();
            customShapeEnabled = false;
            editBoardShape = false;
        }

        generatorSeedText = seed.ToString();
        ClearTesterResult();
        RunLevelTest();
        string fillWarning = bestBuild.occupied.Count < targetOccupiedCells
            ? (algorithmMode == GeneratorAlgorithmMode.LockstepWeave
                ? " Density packing stopped before every remaining cell could be added safely."
                : " Increase arrows/attempts or lower min length for a denser fill.")
            : string.Empty;
        string timeNote = IsGeneratorTimeExpired(generationStart, timeBudgetMs)
            && bestBuild.occupied.Count < targetOccupiedCells
            ? " Stopped at safety time limit."
            : string.Empty;
        string playableNote = bestBuild.initialPlayableCount >= 0
            ? $" Routes: initial {bestBuild.initialPlayableCount}, max {bestBuild.maxPlayableCount}, >2 moves {bestBuild.overTwoRouteMoveCount}."
            : string.Empty;
        string dependencyNote = bestBuild.dependencyDepth >= 0
            ? $" Dependency: {bestBuild.dependencyDepth} waves, widest {bestBuild.maxUnlockWaveCount}, >2 waves {bestBuild.overTwoUnlockWaveCount}."
            : string.Empty;
        string algorithmName = GeneratorAlgorithmModeNames[Mathf.Clamp(generatorAlgorithmModeIndex, 0, GeneratorAlgorithmModeNames.Length - 1)];
        string automaticLengthNote = generatorAutoLength
            ? $" Auto length range: {minLength}-{maxLength}."
            : string.Empty;
        string compositionNote = useAdvancedGuided
            ? $" Complexity: {generatorComplexityPercent}%. Score: {bestBuild.complexityScore:0.0}."
            : string.Empty;
        string spatialNote = useAdvancedGuided && bestBuild.dependencyEdgeCount > 0
            ? $" Spatial: distance {bestBuild.averageDependencyDistance:0.0}, cross-region {bestBuild.crossRegionDependencyRatio * 100f:0}%, direction mix {bestBuild.dependencyDirectionBalance * 100f:0}%."
            : string.Empty;
        string flowNote = useAdvancedGuided && bestBuild.solveTransitionCount > 0
            ? $" Flow: side jumps {bestBuild.solveCrossColumnTransitionRatio * 100f:0}%, horizontal distance {bestBuild.averageSolveHorizontalJumpDistance:0.0}, left-to-right {bestBuild.solveLeftToRightProgress * 100f:0}%, area order {bestBuild.solveHorizontalAreaOrderScore * 100f:0}%, forward hand-offs {bestBuild.solveForwardAreaHandoffRatio * 100f:0}%, region switches {bestBuild.solveRegionTransitionRatio * 100f:0}%, longest inward run {bestBuild.longestInwardSolveRun}."
            : string.Empty;
        string gateNote = algorithmMode == GeneratorAlgorithmMode.LockstepWeave
            ? $" Gates: {bestBuild.controlledGateCount}, max dependents {bestBuild.maximumGateDependentCount}, controlled bursts {bestBuild.controlledBurstWaveCount}, gate switches {bestBuild.gateWaveTransitionCount}."
            : string.Empty;
        string decisionNote = algorithmMode == GeneratorAlgorithmMode.LockstepWeave
            || algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDx
            || algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDxFlow
            ? $" Decisions: blocker distance {bestBuild.averageBlockerDistance:0.0}, remote {bestBuild.remoteBlockerRatio * 100f:0}%, near {bestBuild.nearBlockerRatio * 100f:0}%, zero-impact {bestBuild.zeroImpactPlayableRatio * 100f:0}%, clustered choices {bestBuild.clusteredChoiceWaveRatio * 100f:0}%."
            : string.Empty;
        string generationSummary = $"{algorithmName}: generated {bestBuild.addedCount} arrow(s), {bestBuild.occupied.Count}/{zoneCells.Count} board cells filled. Seed: {seed}.{automaticLengthNote}{compositionNote}{playableNote}{dependencyNote}{spatialNote}{flowNote}{gateNote}{decisionNote}{timeNote}{fillWarning}";
        statusMessage = generationSummary;

        // Generating from the Play Test toolbar should replace the running board,
        // not leave the tester displaying the previous editor draft.
        if (playTestMode)
        {
            if (GeneratePlayableTestLevel())
            {
                playTestStatus = $"Generated and loaded a new {width}x{height} level with {arrows.Count} arrows.";
                statusMessage = $"{generationSummary} Loaded into Play Test.";
            }
        }

        generatorDropdownOpen = false;
    }

    private List<Vector2Int> GetGeneratorZoneCells(bool useCurrentShape)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        if (useCurrentShape && HasCustomShape())
        {
            foreach (Vector2Int cell in activeCells)
            {
                if (IsInsideGridBounds(cell))
                {
                    result.Add(cell);
                }
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result.Add(new Vector2Int(x, y));
                }
            }
        }

        result.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return result;
    }

    private void CalculateAutomaticGeneratorLengthRange(
        List<Vector2Int> zoneCells,
        out int minLength,
        out int maxLength)
    {
        minLength = 2;
        maxLength = 2;
        if (zoneCells == null || zoneCells.Count < 2)
        {
            return;
        }

        int minX = zoneCells[0].x;
        int maxX = zoneCells[0].x;
        int minY = zoneCells[0].y;
        int maxY = zoneCells[0].y;
        for (int i = 1; i < zoneCells.Count; i++)
        {
            Vector2Int cell = zoneCells[i];
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        int boundsArea = Mathf.Max(1, (maxX - minX + 1) * (maxY - minY + 1));
        float compactness = Mathf.Clamp01(zoneCells.Count / (float)boundsArea);
        float shapeFactor = Mathf.Lerp(0.82f, 1f, compactness);
        GeneratorAlgorithmMode mode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
        if (mode == GeneratorAlgorithmMode.LockstepWeave)
        {
            minLength = Mathf.Min(3, zoneCells.Count);
        }

        float lengthScale = mode == GeneratorAlgorithmMode.LockstepWeave
            ? 1.65f
            : mode == GeneratorAlgorithmMode.ComplexGuidedDxFlow
                ? 1.35f
                : mode == GeneratorAlgorithmMode.ComplexGuidedDx ? 1.25f : 1f;
        int calculatedMaximum = Mathf.RoundToInt(Mathf.Sqrt(zoneCells.Count) * shapeFactor * lengthScale);
        if (mode == GeneratorAlgorithmMode.LockstepWeave)
        {
            int longestBoundsSide = Mathf.Max(maxX - minX + 1, maxY - minY + 1);
            calculatedMaximum = Mathf.Max(calculatedMaximum, Mathf.RoundToInt(longestBoundsSide * 1.25f));
        }

        int minimumMaximum = Mathf.Min(6, zoneCells.Count);
        int maximumMaximum = Mathf.Min(
            mode == GeneratorAlgorithmMode.LockstepWeave
                ? 48
                : mode == GeneratorAlgorithmMode.ComplexGuidedDxFlow
                    ? 40
                    : mode == GeneratorAlgorithmMode.ComplexGuidedDx ? 36 : 30,
            zoneCells.Count);
        maxLength = Mathf.Clamp(calculatedMaximum, minimumMaximum, maximumMaximum);
    }


    private int RunProceduralGeneratorPass(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int startingArrowCount,
        int minLength,
        int maxLength,
        int targetOccupiedCells,
        int attemptsPerArrow,
        int fillPercent,
        System.Random rng,
        DateTime generationStart,
        int timeBudgetMs)
    {
        GeneratorAlgorithmMode algorithmMode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
        bool useAdvancedGuided = IsAdvancedGuidedMode(algorithmMode);
        int targetPlayableRoutes = GetGeneratorPlacementRouteTarget(algorithmMode);
        int attemptMultiplier = fillPercent >= 100 ? (useAdvancedGuided ? 7 : 5) : (useAdvancedGuided ? 5 : 3);
        int totalAttempts = Mathf.Clamp(attemptsPerArrow * attemptMultiplier, 10, 5000);
        int attempts = 0;
        int consecutivePlacementFailures = 0;

        while (attempts < totalAttempts
            && generatedArrows.Count < 500
            && occupied.Count < targetOccupiedCells
            && !IsGeneratorTimeExpired(generationStart, timeBudgetMs))
        {
            attempts++;

            List<Vector2Int> freeCells = GetFreeGeneratorCells(zoneCells, occupied);
            if (freeCells.Count < minLength)
            {
                break;
            }

            ShuffleList(freeCells, rng);
            if (occupied.Count > 0)
            {
                SortGeneratorCellsByCompactness(freeCells, occupied);
            }
            bool placedArrow = false;

            int currentPlayableRoutes = generatedArrows.Count == 0
                ? 0
                : CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, solveZoneSet);
            int placementRouteCeiling = GetGeneratorPlacementRouteCeiling(algorithmMode, currentPlayableRoutes);
            int desiredPlacementRoutes = GetGeneratorDesiredPlacementRoutes(algorithmMode, currentPlayableRoutes);
            int futureRouteCeiling = GetGeneratorFutureRouteCeiling(algorithmMode);
            int relaxedRouteFailureThreshold = useAdvancedGuided
                ? GetAdvancedRelaxedRouteFailureThreshold(algorithmMode)
                : 2;
            bool allowRelaxedRoutes = algorithmMode != GeneratorAlgorithmMode.LockstepWeave
                && consecutivePlacementFailures >= relaxedRouteFailureThreshold;
            int remainingTargetCells = Mathf.Max(minLength, targetOccupiedCells - occupied.Count);
            int placementMaxLength = Mathf.Clamp(Mathf.Min(maxLength, remainingTargetCells), minLength, maxLength);
            int placementMinLength = minLength;
            int preferredLength = ChooseGeneratorPreferredLength(placementMinLength, placementMaxLength, rng);
            bool gateNetworkMode = algorithmMode == GeneratorAlgorithmMode.LockstepWeave;
            bool complexGuidedDxMode = algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDx;
            bool complexGuidedDxFlowMode = algorithmMode == GeneratorAlgorithmMode.ComplexGuidedDxFlow;

            if ((gateNetworkMode || complexGuidedDxMode || complexGuidedDxFlowMode)
                && generatedArrows.Count > startingArrowCount + 1
                && rng.NextDouble() < (gateNetworkMode
                    ? GeneratorGateTailGrowthChance
                    : complexGuidedDxFlowMode
                        ? GeneratorComplexGuidedDxFlowTailGrowthChance
                        : GeneratorComplexGuidedDxTailGrowthChance)
                && TryGrowGeneratedTailPurposefully(
                    zoneCells,
                    placementZoneSet,
                    solveZoneSet,
                    generatedArrows,
                    occupied,
                    startingArrowCount,
                    placementMaxLength,
                    targetOccupiedCells,
                    rng,
                    generationStart,
                    timeBudgetMs))
            {
                consecutivePlacementFailures = 0;
                continue;
            }

            bool gateNeedsAnchor = !gateNetworkMode
                || currentPlayableRoutes >= GeneratorGateBuildThreshold
                || (currentPlayableRoutes == 1
                    && generatedArrows.Count > startingArrowCount + 2
                    && rng.NextDouble() < 0.8d);
            if (generatedArrows.Count > 0
                && gateNeedsAnchor
                && TryCreateReverseBlockingArrow(
                    zoneCells,
                    placementZoneSet,
                    solveZoneSet,
                    generatedArrows,
                    occupied,
                    placementMinLength,
                    placementMaxLength,
                    targetOccupiedCells,
                    preferredLength,
                    fillPercent,
                    allowRelaxedRoutes,
                    rng,
                    generationStart,
                    timeBudgetMs,
                    out RuntimeArrowDraft blockingArrow,
                    out HashSet<Vector2Int> blockingArrowCells))
            {
                blockingArrow.id = $"Arrow {generatedArrows.Count + 1}";
                blockingArrow.color = GetGeneratedSavedArrowColor();
                generatedArrows.Add(blockingArrow);

                foreach (Vector2Int cell in blockingArrowCells)
                {
                    occupied.Add(cell);
                }

                placedArrow = true;
            }

            int startCellTryCount = Mathf.Min(
                freeCells.Count,
                gateNetworkMode ? 72 : (fillPercent >= 100 ? 96 : 48));
            RuntimeArrowDraft relaxedArrow = null;
            HashSet<Vector2Int> relaxedArrowCells = null;
            int relaxedArrowPlayableRoutes = int.MaxValue;
            int relaxedArrowFutureMaxPlayableRoutes = int.MaxValue;
            bool relaxedArrowWasBlocked = false;
            float relaxedArrowDiversityScore = float.MinValue;
            RuntimeArrowDraft bestArrow = null;
            HashSet<Vector2Int> bestArrowCells = null;
            int bestArrowPlayableRoutes = int.MaxValue;
            bool bestArrowWasBlocked = false;
            float bestArrowDiversityScore = float.MinValue;
            int diversityCandidateSamples = 0;
            int diversitySampleTarget = occupied.Count * 5 < targetOccupiedCells * 4
                ? GeneratorDiversityCandidateSamples
                : Mathf.Max(3, GeneratorDiversityCandidateSamples / 2);
            if (useAdvancedGuided)
            {
                diversitySampleTarget = Mathf.Min(18, diversitySampleTarget * 3);
            }
            if (gateNetworkMode)
            {
                diversitySampleTarget = Mathf.Max(diversitySampleTarget, 18);
            }

            for (int startIndex = 0;
                startIndex < startCellTryCount
                && !placedArrow
                && diversityCandidateSamples < diversitySampleTarget
                && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
                startIndex++)
            {
                int availableTargetCellCount = Mathf.Min(freeCells.Count, Mathf.Max(0, targetOccupiedCells - occupied.Count));
                List<int> lengthOrder = BuildGeneratorLengthOrder(
                    placementMinLength,
                    placementMaxLength,
                    availableTargetCellCount,
                    preferredLength,
                    fillPercent,
                    rng);

                for (int lengthIndex = 0;
                    lengthIndex < lengthOrder.Count
                    && !placedArrow
                    && diversityCandidateSamples < diversitySampleTarget
                    && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
                    lengthIndex++)
                {
                    int targetLength = lengthOrder[lengthIndex];
                    if (!TryCreateGeneratedArrowCandidate(freeCells[startIndex], targetLength, occupied, placementZoneSet, rng, out RuntimeArrowDraft arrow))
                    {
                        continue;
                    }

                    HashSet<Vector2Int> arrowCells = new HashSet<Vector2Int>();
                    if (!FillGeneratedCells(arrow.points, placementZoneSet, arrowCells))
                    {
                        continue;
                    }

                    if (gateNetworkMode
                        && ViolatesGeneratedDecisionCorridors(
                            arrow,
                            arrowCells,
                            generatedArrows,
                            occupied,
                            solveZoneSet))
                    {
                        continue;
                    }

                    if (UsesDependencyStructureRules(algorithmMode)
                        && occupied.Count * 10 < targetOccupiedCells * 9
                        && WouldCreateGeneratedStraightContinuation(arrow, generatedArrows, placementMaxLength))
                    {
                        continue;
                    }

                    if (fillPercent >= 100
                        && WouldLeaveUnfillableFreeRegion(zoneCells, placementZoneSet, occupied, arrowCells, minLength, targetOccupiedCells))
                    {
                        continue;
                    }

                    if (generatedArrows.Count == 0 && !HasGeneratedFreeExitRayCell(arrow, occupied, solveZoneSet))
                    {
                        continue;
                    }

                    if (!TryEvaluateGeneratedCandidate(
                        generatedArrows,
                        occupied,
                        solveZoneSet,
                        arrow,
                        arrowCells,
                        out int playableRoutesAfterPlacement,
                        out int futureMaxPlayableRoutes,
                        out bool candidateWasBlocked,
                        out int playableHeadDistanceAfterPlacement))
                    {
                        continue;
                    }

                    if (gateNetworkMode
                        && playableRoutesAfterPlacement > 1
                        && playableHeadDistanceAfterPlacement < GetGeneratedDecisionChoiceDistanceThreshold(solveZoneSet))
                    {
                        continue;
                    }

                    float diversityScore = CalculateGeneratedDiversityScore(
                        arrow,
                        arrowCells,
                        generatedArrows,
                        candidateWasBlocked);
                    diversityScore += CalculateAdvancedGuidedCandidateBonus(
                        arrow,
                        arrowCells,
                        generatedArrows,
                        occupied,
                        solveZoneSet,
                        playableRoutesAfterPlacement,
                        futureMaxPlayableRoutes,
                        candidateWasBlocked);

                    bool exceedsComplexRouteProfile = UsesDependencyStructureRules(algorithmMode)
                        && futureMaxPlayableRoutes > futureRouteCeiling;
                    if (playableRoutesAfterPlacement > placementRouteCeiling || exceedsComplexRouteProfile)
                    {
                        int candidatePeakRoutes = Mathf.Max(playableRoutesAfterPlacement, futureMaxPlayableRoutes);
                        int relaxedPeakRoutes = Mathf.Max(relaxedArrowPlayableRoutes, relaxedArrowFutureMaxPlayableRoutes);
                        bool isBetterRelaxedCandidate = candidatePeakRoutes < relaxedPeakRoutes
                            || (candidatePeakRoutes == relaxedPeakRoutes
                                && (futureMaxPlayableRoutes < relaxedArrowFutureMaxPlayableRoutes
                                    || (futureMaxPlayableRoutes == relaxedArrowFutureMaxPlayableRoutes
                                        && (playableRoutesAfterPlacement < relaxedArrowPlayableRoutes
                                            || (playableRoutesAfterPlacement == relaxedArrowPlayableRoutes
                                                && (diversityScore > relaxedArrowDiversityScore
                                                    || (Mathf.Approximately(diversityScore, relaxedArrowDiversityScore)
                                                        && candidateWasBlocked
                                                        && !relaxedArrowWasBlocked)))))));
                        if (isBetterRelaxedCandidate)
                        {
                            relaxedArrow = arrow;
                            relaxedArrowCells = arrowCells;
                            relaxedArrowPlayableRoutes = playableRoutesAfterPlacement;
                            relaxedArrowFutureMaxPlayableRoutes = futureMaxPlayableRoutes;
                            relaxedArrowWasBlocked = candidateWasBlocked;
                            relaxedArrowDiversityScore = diversityScore;
                        }

                        continue;
                    }

                    diversityCandidateSamples++;
                    float routeWeight = useAdvancedGuided ? GetAdvancedRouteWeight(algorithmMode) : 12f;
                    float selectionScore = algorithmMode == GeneratorAlgorithmMode.LockstepWeave
                        ? diversityScore - Mathf.Abs(desiredPlacementRoutes - playableRoutesAfterPlacement) * routeWeight
                        : diversityScore + (targetPlayableRoutes - playableRoutesAfterPlacement) * routeWeight;
                    bool isBetterCandidate = bestArrow == null
                        || selectionScore > bestArrowDiversityScore
                        || (Mathf.Approximately(selectionScore, bestArrowDiversityScore)
                            && (playableRoutesAfterPlacement < bestArrowPlayableRoutes
                                || (playableRoutesAfterPlacement == bestArrowPlayableRoutes
                                    && candidateWasBlocked
                                    && !bestArrowWasBlocked)));
                    if (isBetterCandidate)
                    {
                        bestArrow = arrow;
                        bestArrowCells = arrowCells;
                        bestArrowPlayableRoutes = playableRoutesAfterPlacement;
                        bestArrowWasBlocked = candidateWasBlocked;
                        bestArrowDiversityScore = selectionScore;
                    }
                }
            }

            if (!placedArrow && bestArrow != null)
            {
                bestArrow.id = $"Arrow {generatedArrows.Count + 1}";
                bestArrow.color = GetGeneratedSavedArrowColor();
                generatedArrows.Add(bestArrow);
                foreach (Vector2Int cell in bestArrowCells)
                {
                    occupied.Add(cell);
                }

                placedArrow = true;
            }

            if (!placedArrow && allowRelaxedRoutes && relaxedArrow != null)
            {
                relaxedArrow.id = $"Arrow {generatedArrows.Count + 1}";
                relaxedArrow.color = GetGeneratedSavedArrowColor();
                generatedArrows.Add(relaxedArrow);
                foreach (Vector2Int cell in relaxedArrowCells)
                {
                    occupied.Add(cell);
                }

                placedArrow = true;
            }

            if (placedArrow)
            {
                consecutivePlacementFailures = 0;
                continue;
            }

            consecutivePlacementFailures++;
            int failureLimit = currentPlayableRoutes < targetPlayableRoutes
                ? Mathf.Clamp(attemptsPerArrow / 3, 10, 48)
                : Mathf.Clamp(attemptsPerArrow / 12, 4, 12);
            if (consecutivePlacementFailures >= failureLimit)
            {
                break;
            }
        }

        return generatedArrows.Count - startingArrowCount;
    }

    // Purposeful mode grows an existing arrow backward from its tail before it
    // spends free cells on another arrow. The head and exit direction never move.
    private bool TryGrowGeneratedTailPurposefully(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int startingArrowCount,
        int maxLength,
        int targetOccupiedCells,
        System.Random rng,
        DateTime generationStart,
        int timeBudgetMs)
    {
        List<GeneratedTailExtensionCandidate> candidates = new List<GeneratedTailExtensionCandidate>();
        List<int> arrowOrder = new List<int>();
        for (int i = startingArrowCount; i < generatedArrows.Count; i++)
        {
            arrowOrder.Add(i);
        }

        ShuffleList(arrowOrder, rng);
        List<Vector2Int> pathCells = new List<Vector2Int>();
        for (int orderIndex = 0; orderIndex < arrowOrder.Count; orderIndex++)
        {
            int arrowIndex = arrowOrder[orderIndex];
            RuntimeArrowDraft sourceArrow = generatedArrows[arrowIndex];
            if (!ExpandGeneratedPathCells(sourceArrow.points, pathCells) || pathCells.Count >= maxLength)
            {
                continue;
            }

            Vector2Int tail = pathCells[0];
            Vector2Int firstStep = pathCells[1] - tail;
            int firstRunLength = CountGeneratedInitialRunLength(pathCells);
            int existingTurnCount = Mathf.Max(0, sourceArrow.points.Count - 2);
            List<Vector2Int> extensionCells = GetFreeTailExtensionCells(tail, placementZoneSet, occupied, rng);

            for (int candidateIndex = 0; candidateIndex < extensionCells.Count; candidateIndex++)
            {
                Vector2Int extensionCell = extensionCells[candidateIndex];
                Vector2Int extensionStep = tail - extensionCell;
                bool continuesStraight = extensionStep == firstStep;
                if (!continuesStraight && firstRunLength < 2)
                {
                    continue;
                }

                List<Vector2Int> extendedPath = new List<Vector2Int>(pathCells.Count + 1) { extensionCell };
                extendedPath.AddRange(pathCells);

                RuntimeArrowDraft extendedArrow = new RuntimeArrowDraft
                {
                    id = sourceArrow.id,
                    color = sourceArrow.color,
                    generatedProfile = continuesStraight
                        ? sourceArrow.generatedProfile
                        : GeneratedArrowProfile.OrganicWinding
                };
                extendedArrow.points.AddRange(CompressGeneratedPath(extendedPath));

                if (extendedArrow.points.Count < 2
                    || PathArrowUtility.TryFindSelfOverlap(extendedArrow.points, out _, out _)
                    || PathArrowUtility.TryFindOwnExitBlock(extendedArrow.points, width, height, solveZoneSet, out _))
                {
                    continue;
                }

                HashSet<Vector2Int> extendedCells = new HashSet<Vector2Int>();
                if (!FillGeneratedCells(extendedArrow.points, placementZoneSet, extendedCells)
                    || extendedCells.Count != pathCells.Count + 1)
                {
                    continue;
                }

                HashSet<Vector2Int> addedCell = new HashSet<Vector2Int> { extensionCell };
                if ((GeneratorAlgorithmMode)generatorAlgorithmModeIndex == GeneratorAlgorithmMode.LockstepWeave
                    && WouldEnterGeneratedExitBuffer(
                        addedCell,
                        generatedArrows,
                        solveZoneSet,
                        arrowIndex))
                {
                    continue;
                }

                if (WouldWorsenUnfillableFreeRegions(
                    zoneCells,
                    placementZoneSet,
                    occupied,
                    addedCell,
                    2,
                    targetOccupiedCells))
                {
                    continue;
                }

                int continuationLength = CountGeneratedFreeRun(
                    extensionCell,
                    extensionCell - tail,
                    placementZoneSet,
                    occupied,
                    maxLength - pathCells.Count);
                int freeNeighbors = CountFreeGeneratorNeighbors(extensionCell, placementZoneSet, occupied);
                int blockedExitRays = CountGeneratedExitRaysThroughCell(extensionCell, generatedArrows, solveZoneSet);
                float score = (continuesStraight ? 42f : 10f)
                    + continuationLength * 7f
                    + blockedExitRays * 16f
                    + Mathf.Min(maxLength - pathCells.Count, 8) * 1.5f
                    + (freeNeighbors <= 1 ? 14f : 0f)
                    - Mathf.Max(0, freeNeighbors - 2) * 8f
                    - (!continuesStraight ? existingTurnCount * 2.5f : 0f);

                candidates.Add(new GeneratedTailExtensionCandidate(arrowIndex, extensionCell, extendedArrow, score));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        int currentPlayableRoutes = CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, solveZoneSet);
        int targetPlayableRoutes = GetGeneratorPlacementRouteTarget((GeneratorAlgorithmMode)generatorAlgorithmModeIndex);
        int routeCeiling = Mathf.Max(targetPlayableRoutes, currentPlayableRoutes);
        int evaluationCount = Mathf.Min(
            candidates.Count,
            (GeneratorAlgorithmMode)generatorAlgorithmModeIndex == GeneratorAlgorithmMode.LockstepWeave ? 6 : 12);
        for (int candidateIndex = 0;
            candidateIndex < evaluationCount && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
            candidateIndex++)
        {
            GeneratedTailExtensionCandidate candidate = candidates[candidateIndex];
            RuntimeArrowDraft sourceArrow = generatedArrows[candidate.ArrowIndex];
            generatedArrows[candidate.ArrowIndex] = candidate.Arrow;
            occupied.Add(candidate.Cell);

            bool accepted = !WouldExceedGeneratedUShapeLimit(generatedArrows, candidate.Arrow, candidate.ArrowIndex)
                && CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, solveZoneSet) <= routeCeiling
                && CanGeneratedLevelSolve(generatedArrows, solveZoneSet)
                && ((GeneratorAlgorithmMode)generatorAlgorithmModeIndex != GeneratorAlgorithmMode.LockstepWeave
                    || HasGeneratedDecisionSpacingAcrossAllWaves(generatedArrows, solveZoneSet));
            if (accepted)
            {
                return true;
            }

            occupied.Remove(candidate.Cell);
            generatedArrows[candidate.ArrowIndex] = sourceArrow;
        }

        return false;
    }

    private static int CountGeneratedInitialRunLength(List<Vector2Int> pathCells)
    {
        Vector2Int direction = pathCells[1] - pathCells[0];
        int runLength = 1;
        for (int i = 2; i < pathCells.Count && pathCells[i] - pathCells[i - 1] == direction; i++)
        {
            runLength++;
        }

        return runLength;
    }

    private static int CountGeneratedFreeRun(
        Vector2Int start,
        Vector2Int direction,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied,
        int maximumCount)
    {
        int count = 0;
        Vector2Int cell = start + direction;
        while (count < maximumCount && zoneSet.Contains(cell) && !occupied.Contains(cell))
        {
            count++;
            cell += direction;
        }

        return count;
    }

    private int CountGeneratedExitRaysThroughCell(
        Vector2Int cell,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> zoneSet)
    {
        int count = 0;
        for (int i = 0; i < generatedArrows.Count; i++)
        {
            RuntimeArrowDraft arrow = generatedArrows[i];
            Vector2Int direction = GetGeneratedExitDirection(arrow);
            if (direction == Vector2Int.zero)
            {
                continue;
            }

            Vector2Int rayCell = arrow.points[arrow.points.Count - 1] + direction;
            while (IsInsideGridBounds(rayCell))
            {
                if (rayCell == cell)
                {
                    count++;
                    break;
                }

                rayCell += direction;
            }
        }

        return count;
    }

    // Adjacent short arrows can be represented by one longer arrow without
    // changing board coverage. A merge is kept only when the resulting puzzle
    // remains solvable, so consolidation cannot silently corrupt clear order.
    private int ConsolidateGeneratedArrowsByTail(
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        int startingArrowCount,
        int minLength,
        int maxLength,
        DateTime consolidationStart,
        int timeBudgetMs)
    {
        int mergedCount = 0;
        int mergeGuard = Mathf.Max(1, generatedArrows.Count - startingArrowCount);

        while (mergeGuard-- > 0
            && generatedArrows.Count - startingArrowCount > 1
            && !IsGeneratorTimeExpired(consolidationStart, timeBudgetMs))
        {
            List<GeneratedTailMergeCandidate> candidates = new List<GeneratedTailMergeCandidate>();
            for (int sourceIndex = startingArrowCount; sourceIndex < generatedArrows.Count; sourceIndex++)
            {
                for (int victimIndex = startingArrowCount; victimIndex < generatedArrows.Count; victimIndex++)
                {
                    if (sourceIndex == victimIndex
                        || !TryBuildGeneratedTailMerge(
                            generatedArrows[sourceIndex],
                            generatedArrows[victimIndex],
                            placementZoneSet,
                            solveZoneSet,
                            maxLength,
                            out RuntimeArrowDraft mergedArrow,
                            out int sourceLength,
                            out int victimLength,
                            out bool straightJoin))
                    {
                        continue;
                    }

                    GeneratorAlgorithmMode mode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
                    bool gateNetworkMode = mode == GeneratorAlgorithmMode.LockstepWeave;
                    bool allowBentTailMerge = gateNetworkMode
                        || mode == GeneratorAlgorithmMode.ComplexGuidedDx
                        || mode == GeneratorAlgorithmMode.ComplexGuidedDxFlow;
                    int mergedTurnCount = Mathf.Max(0, mergedArrow.points.Count - 2);
                    int maximumMergedTurns = mode == GeneratorAlgorithmMode.ComplexGuidedDxFlow
                        ? 12
                        : mode == GeneratorAlgorithmMode.ComplexGuidedDx ? 10 : 8;
                    if (!straightJoin && (!allowBentTailMerge || mergedTurnCount > maximumMergedTurns))
                    {
                        continue;
                    }

                    float score = victimLength * 7f
                        + Mathf.Min(sourceLength + victimLength, maxLength) * 3f
                        + (!straightJoin ? 18f + mergedTurnCount * 3f : 0f);
                    candidates.Add(new GeneratedTailMergeCandidate(
                        sourceIndex,
                        victimIndex,
                        mergedArrow,
                        score));
                }
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            GeneratedTailMergeCandidate bestCandidate = null;
            bool bestHasFocusedOpening = false;
            int bestOverTwoRouteMoveCount = int.MaxValue;
            int bestMaxPlayableCount = int.MaxValue;
            float bestAveragePlayableCount = float.MaxValue;
            int evaluationCount = candidates.Count;
            for (int candidateIndex = 0;
                candidateIndex < evaluationCount && !IsGeneratorTimeExpired(consolidationStart, timeBudgetMs);
                candidateIndex++)
            {
                GeneratedTailMergeCandidate candidate = candidates[candidateIndex];
                List<RuntimeArrowDraft> testArrows = new List<RuntimeArrowDraft>(generatedArrows);
                testArrows[candidate.SourceIndex] = candidate.Arrow;
                testArrows.RemoveAt(candidate.VictimIndex);
                if (ClassifyGeneratedArrowShape(candidate.Arrow) == GeneratedArrowShape.UShape
                    || !TryMeasureGeneratedSolveProfile(
                        testArrows,
                        solveZoneSet,
                        out int initialPlayableCount,
                        out int maxPlayableCount,
                        out int overTwoRouteMoveCount,
                        out float averagePlayableCount))
                {
                    continue;
                }

                bool hasFocusedOpening = initialPlayableCount <= GeneratorTargetMaxPlayableRoutes;
                bool isBetter = bestCandidate == null
                    || (hasFocusedOpening && !bestHasFocusedOpening)
                    || (hasFocusedOpening == bestHasFocusedOpening
                        && (overTwoRouteMoveCount < bestOverTwoRouteMoveCount
                            || (overTwoRouteMoveCount == bestOverTwoRouteMoveCount
                                && (maxPlayableCount < bestMaxPlayableCount
                                    || (maxPlayableCount == bestMaxPlayableCount
                                        && (averagePlayableCount < bestAveragePlayableCount
                                            || (Mathf.Approximately(averagePlayableCount, bestAveragePlayableCount)
                                                && candidate.Score > bestCandidate.Score)))))));
                if (!isBetter)
                {
                    continue;
                }

                bestCandidate = candidate;
                bestHasFocusedOpening = hasFocusedOpening;
                bestOverTwoRouteMoveCount = overTwoRouteMoveCount;
                bestMaxPlayableCount = maxPlayableCount;
                bestAveragePlayableCount = averagePlayableCount;
            }

            if (bestCandidate == null)
            {
                break;
            }

            generatedArrows[bestCandidate.SourceIndex] = bestCandidate.Arrow;
            generatedArrows.RemoveAt(bestCandidate.VictimIndex);
            mergedCount++;
        }

        for (int i = startingArrowCount; i < generatedArrows.Count; i++)
        {
            generatedArrows[i].id = $"Arrow {i + 1}";
        }

        return mergedCount;
    }

    private bool TryBuildGeneratedTailMerge(
        RuntimeArrowDraft sourceArrow,
        RuntimeArrowDraft victimArrow,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        int maxLength,
        out RuntimeArrowDraft mergedArrow,
        out int sourceLength,
        out int victimLength,
        out bool straightJoin)
    {
        mergedArrow = null;
        sourceLength = 0;
        victimLength = 0;
        straightJoin = false;
        List<Vector2Int> sourceCells = new List<Vector2Int>();
        List<Vector2Int> victimCells = new List<Vector2Int>();
        if (!ExpandGeneratedPathCells(sourceArrow.points, sourceCells)
            || !ExpandGeneratedPathCells(victimArrow.points, victimCells))
        {
            return false;
        }

        sourceLength = sourceCells.Count;
        victimLength = victimCells.Count;
        if (sourceLength + victimLength > maxLength)
        {
            return false;
        }

        Vector2Int sourceTail = sourceCells[0];
        bool victimEndConnects = IsOrthogonallyAdjacent(victimCells[victimCells.Count - 1], sourceTail);
        bool victimStartConnects = IsOrthogonallyAdjacent(victimCells[0], sourceTail);
        if (!victimEndConnects && !victimStartConnects)
        {
            return false;
        }

        if (victimStartConnects)
        {
            victimCells.Reverse();
        }

        Vector2Int incomingStep = sourceTail - victimCells[victimCells.Count - 1];
        Vector2Int outgoingStep = sourceCells[1] - sourceTail;
        straightJoin = incomingStep == outgoingStep;

        List<Vector2Int> mergedCells = new List<Vector2Int>(sourceLength + victimLength);
        mergedCells.AddRange(victimCells);
        mergedCells.AddRange(sourceCells);
        mergedArrow = new RuntimeArrowDraft
        {
            id = sourceArrow.id,
            color = sourceArrow.color,
            generatedProfile = straightJoin
                ? sourceArrow.generatedProfile
                : GeneratedArrowProfile.OrganicWinding
        };
        mergedArrow.points.AddRange(CompressGeneratedPath(mergedCells));

        if (mergedArrow.points.Count < 2
            || PathArrowUtility.TryFindSelfOverlap(mergedArrow.points, out _, out _)
            || PathArrowUtility.TryFindOwnExitBlock(mergedArrow.points, width, height, solveZoneSet, out _))
        {
            mergedArrow = null;
            return false;
        }

        HashSet<Vector2Int> mergedOccupiedCells = new HashSet<Vector2Int>();
        if (!FillGeneratedCells(mergedArrow.points, placementZoneSet, mergedOccupiedCells)
            || mergedOccupiedCells.Count != sourceLength + victimLength)
        {
            mergedArrow = null;
            return false;
        }

        return true;
    }

    private bool WouldCreateGeneratedStraightContinuation(
        RuntimeArrowDraft candidate,
        List<RuntimeArrowDraft> generatedArrows,
        int maxLength)
    {
        for (int i = 0; i < generatedArrows.Count; i++)
        {
            RuntimeArrowDraft existing = generatedArrows[i];
            if (CanGeneratedArrowsJoinStraight(candidate, existing, maxLength)
                || CanGeneratedArrowsJoinStraight(existing, candidate, maxLength))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanGeneratedArrowsJoinStraight(
        RuntimeArrowDraft sourceArrow,
        RuntimeArrowDraft victimArrow,
        int maxLength)
    {
        List<Vector2Int> sourceCells = new List<Vector2Int>();
        List<Vector2Int> victimCells = new List<Vector2Int>();
        if (!ExpandGeneratedPathCells(sourceArrow.points, sourceCells)
            || !ExpandGeneratedPathCells(victimArrow.points, victimCells)
            || sourceCells.Count + victimCells.Count > maxLength)
        {
            return false;
        }

        Vector2Int sourceTail = sourceCells[0];
        Vector2Int victimJoinCell;
        if (IsOrthogonallyAdjacent(victimCells[victimCells.Count - 1], sourceTail))
        {
            victimJoinCell = victimCells[victimCells.Count - 1];
        }
        else if (IsOrthogonallyAdjacent(victimCells[0], sourceTail))
        {
            victimJoinCell = victimCells[0];
        }
        else
        {
            return false;
        }

        Vector2Int incomingStep = sourceTail - victimJoinCell;
        Vector2Int outgoingStep = sourceCells[1] - sourceTail;
        return incomingStep == outgoingStep;
    }

    private static bool IsOrthogonallyAdjacent(Vector2Int first, Vector2Int second)
    {
        Vector2Int delta = first - second;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }

    // Complex Guided keeps the Profile Guided shapes, then favors candidates that
    // participate in the puzzle's dependency graph instead of merely filling space.
    private float CalculateAdvancedGuidedCandidateBonus(
        RuntimeArrowDraft candidate,
        HashSet<Vector2Int> candidateCells,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet,
        int playableRoutesAfterPlacement,
        int futureMaxPlayableRoutes,
        bool candidateWasBlocked)
    {
        GeneratorAlgorithmMode mode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
        if (!IsAdvancedGuidedMode(mode)
            || candidate == null
            || candidateCells == null)
        {
            return 0f;
        }

        float strength = Mathf.InverseLerp(25f, 100f, generatorComplexityPercent);
        int currentPlayableRoutes = generatedArrows.Count == 0
            ? 0
            : CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, zoneSet);
        int blockedExistingArrows = CountGeneratedExitRaysBlockedByCandidate(
            candidateCells,
            generatedArrows,
            occupied,
            zoneSet,
            out int totalBlockingDistance,
            out int horizontalBlockedArrows,
            out int nearBlockedArrows,
            out int remoteBlockedArrows);
        int adjacencyEdges = CountGeneratedCandidateAdjacencyEdges(candidateCells, occupied);
        int overlappingBounds = CountGeneratedCandidateBoundsIntersections(candidate, generatedArrows);
        int turnCount = Mathf.Max(0, candidate.points.Count - 2);

        GetGeneratedArrowBounds(candidate, out Vector2Int minimum, out Vector2Int maximum);
        int boardSpan = maximum.x - minimum.x + maximum.y - minimum.y;
        bool createsChainLink = candidateWasBlocked && blockedExistingArrows > 0;
        bool hasDependencyPurpose = candidateWasBlocked || blockedExistingArrows > 0;

        float blockedRayMultiplier = 1f;
        float chainMultiplier = 1f;
        float blockedCandidateMultiplier = 1f;
        float adjacencyMultiplier = 1f;
        float overlapMultiplier = 1f;
        float lengthMultiplier = 1f;
        float spanMultiplier = 1f;
        float turnMultiplier = 1f;
        float purposelessPenaltyMultiplier = 1f;
        float currentRoutePenaltyMultiplier = 1f;
        float futureRoutePenaltyMultiplier = 1f;

        switch (mode)
        {
            case GeneratorAlgorithmMode.ComplexGuided:
                blockedRayMultiplier = 1.1f;
                chainMultiplier = 1.15f;
                purposelessPenaltyMultiplier = 1.2f;
                currentRoutePenaltyMultiplier = 1.55f;
                futureRoutePenaltyMultiplier = 1.9f;
                break;
            case GeneratorAlgorithmMode.ComplexGuidedDx:
                blockedRayMultiplier = 1.45f;
                chainMultiplier = 1.65f;
                blockedCandidateMultiplier = 1.15f;
                adjacencyMultiplier = 0.9f;
                overlapMultiplier = 1.35f;
                lengthMultiplier = 1.4f;
                spanMultiplier = 1.55f;
                turnMultiplier = 1.65f;
                purposelessPenaltyMultiplier = 1.7f;
                currentRoutePenaltyMultiplier = 1.2f;
                futureRoutePenaltyMultiplier = 1.6f;
                break;
            case GeneratorAlgorithmMode.ComplexGuidedDxFlow:
                blockedRayMultiplier = 1.55f;
                chainMultiplier = 1.75f;
                blockedCandidateMultiplier = 1.15f;
                adjacencyMultiplier = 0.85f;
                overlapMultiplier = 1.4f;
                lengthMultiplier = 1.5f;
                spanMultiplier = 1.75f;
                turnMultiplier = 1.7f;
                purposelessPenaltyMultiplier = 1.8f;
                currentRoutePenaltyMultiplier = 1.15f;
                futureRoutePenaltyMultiplier = 1.55f;
                break;
            case GeneratorAlgorithmMode.LockstepWeave:
                blockedRayMultiplier = 1.75f;
                chainMultiplier = 2.1f;
                blockedCandidateMultiplier = 1.6f;
                adjacencyMultiplier = 0.7f;
                overlapMultiplier = 1.5f;
                lengthMultiplier = 1.8f;
                spanMultiplier = 2.2f;
                turnMultiplier = 2.25f;
                purposelessPenaltyMultiplier = 0.55f;
                currentRoutePenaltyMultiplier = 0.4f;
                futureRoutePenaltyMultiplier = 1.1f;
                break;
            case GeneratorAlgorithmMode.ChainFocus:
                blockedRayMultiplier = 1.25f;
                chainMultiplier = 1.4f;
                blockedCandidateMultiplier = 1.25f;
                adjacencyMultiplier = 0.7f;
                overlapMultiplier = 0.7f;
                lengthMultiplier = 0.8f;
                spanMultiplier = 0.7f;
                turnMultiplier = 0.85f;
                purposelessPenaltyMultiplier = 1.25f;
                currentRoutePenaltyMultiplier = 1.4f;
                futureRoutePenaltyMultiplier = 1.55f;
                break;
            case GeneratorAlgorithmMode.Crossweave:
                blockedRayMultiplier = 1.5f;
                chainMultiplier = 0.85f;
                blockedCandidateMultiplier = 0.8f;
                adjacencyMultiplier = 1.55f;
                overlapMultiplier = 2f;
                lengthMultiplier = 1.1f;
                spanMultiplier = 1.35f;
                turnMultiplier = 1.35f;
                purposelessPenaltyMultiplier = 0.9f;
                currentRoutePenaltyMultiplier = 0.75f;
                futureRoutePenaltyMultiplier = 0.65f;
                break;
            case GeneratorAlgorithmMode.Longform:
                blockedRayMultiplier = 0.85f;
                chainMultiplier = 0.8f;
                blockedCandidateMultiplier = 0.7f;
                adjacencyMultiplier = 0.65f;
                lengthMultiplier = 2.3f;
                spanMultiplier = 2f;
                turnMultiplier = 1.35f;
                purposelessPenaltyMultiplier = 0.8f;
                currentRoutePenaltyMultiplier = 0.7f;
                futureRoutePenaltyMultiplier = 0.55f;
                break;
            case GeneratorAlgorithmMode.CompactLocks:
                blockedRayMultiplier = 1.15f;
                chainMultiplier = 1.2f;
                blockedCandidateMultiplier = 1.1f;
                adjacencyMultiplier = 1.8f;
                overlapMultiplier = 0.8f;
                lengthMultiplier = 0.45f;
                spanMultiplier = 0.45f;
                turnMultiplier = 0.65f;
                purposelessPenaltyMultiplier = 1.1f;
                currentRoutePenaltyMultiplier = 1.2f;
                futureRoutePenaltyMultiplier = 1.3f;
                break;
            case GeneratorAlgorithmMode.ExpertMix:
                blockedRayMultiplier = 1.35f;
                chainMultiplier = 1.35f;
                blockedCandidateMultiplier = 1.2f;
                adjacencyMultiplier = 1.2f;
                overlapMultiplier = 1.4f;
                lengthMultiplier = 1.25f;
                spanMultiplier = 1.2f;
                turnMultiplier = 1.5f;
                purposelessPenaltyMultiplier = 1.4f;
                currentRoutePenaltyMultiplier = 1.45f;
                futureRoutePenaltyMultiplier = 1.5f;
                break;
        }

        int usefulBlockedArrows = Mathf.Min(
            mode == GeneratorAlgorithmMode.LockstepWeave ? GeneratorGateMaximumDependents : 2,
            blockedExistingArrows);
        float score = usefulBlockedArrows * Mathf.Lerp(10f, 25f, strength) * blockedRayMultiplier;
        float averageBlockingDistance = blockedExistingArrows > 0
            ? totalBlockingDistance / (float)blockedExistingArrows
            : 0f;
        score += averageBlockingDistance * Mathf.Lerp(2f, 6f, strength);
        score += horizontalBlockedArrows * Mathf.Lerp(3f, 9f, strength);
        if (mode == GeneratorAlgorithmMode.LockstepWeave)
        {
            score += averageBlockingDistance * Mathf.Lerp(10f, 18f, strength);
            score += remoteBlockedArrows * Mathf.Lerp(42f, 72f, strength);
            score -= nearBlockedArrows * Mathf.Lerp(90f, 145f, strength);

            int candidateBlockerDistance = GetGeneratedFirstBlockerDistance(candidate, occupied, zoneSet);
            if (candidateBlockerDistance > 0)
            {
                score += candidateBlockerDistance * Mathf.Lerp(7f, 13f, strength);
                score -= candidateBlockerDistance <= GeneratorDecisionExitBufferCells
                    ? Mathf.Lerp(90f, 145f, strength)
                    : 0f;
            }
        }
        else if (mode == GeneratorAlgorithmMode.ComplexGuidedDx)
        {
            // DX keeps the softer placement rules of Complex Guided, but makes
            // distant interference substantially more valuable than local clutter.
            score += averageBlockingDistance * Mathf.Lerp(6f, 11f, strength);
            score += remoteBlockedArrows * Mathf.Lerp(28f, 48f, strength);
            score -= nearBlockedArrows * Mathf.Lerp(34f, 58f, strength);
        }
        else if (mode == GeneratorAlgorithmMode.ComplexGuidedDxFlow)
        {
            // Flow particularly values remote horizontal interference because it
            // creates solve hand-offs between distant sides of the board.
            score += averageBlockingDistance * Mathf.Lerp(8f, 14f, strength);
            score += remoteBlockedArrows * Mathf.Lerp(34f, 56f, strength);
            score -= nearBlockedArrows * Mathf.Lerp(30f, 50f, strength);
            score += horizontalBlockedArrows * Mathf.Lerp(12f, 24f, strength);

            // Arrows are placed in reverse solve order. Starting placement on the
            // right and sweeping toward the left makes the resulting clear order
            // progress from the left side toward the right in distinct phases.
            float placementProgress = occupied.Count / (float)Mathf.Max(1, zoneSet.Count);
            float desiredNormalizedX = 1f - placementProgress;
            float candidateCenterX = 0f;
            foreach (Vector2Int cell in candidateCells)
            {
                candidateCenterX += cell.x;
            }

            candidateCenterX /= Mathf.Max(1, candidateCells.Count);
            float candidateNormalizedX = candidateCenterX / Mathf.Max(1f, width - 1f);
            float sweepAlignment = 1f - Mathf.Abs(candidateNormalizedX - desiredNormalizedX);
            score += Mathf.Clamp01(sweepAlignment) * Mathf.Lerp(45f, 90f, strength);
        }
        int excessiveDependentThreshold = mode == GeneratorAlgorithmMode.LockstepWeave
            ? GeneratorGateMaximumDependents
            : 2;
        score -= Mathf.Max(0, blockedExistingArrows - excessiveDependentThreshold)
            * Mathf.Lerp(24f, 52f, strength);
        score += createsChainLink ? Mathf.Lerp(14f, 34f, strength) * chainMultiplier : 0f;
        score += candidateWasBlocked ? Mathf.Lerp(4f, 12f, strength) * blockedCandidateMultiplier : 0f;
        score += Mathf.Min(adjacencyEdges, 18) * Mathf.Lerp(0.25f, 0.8f, strength) * adjacencyMultiplier;
        score += Mathf.Min(overlappingBounds, 5) * Mathf.Lerp(1f, 4f, strength) * overlapMultiplier;
        score += Mathf.Min(candidateCells.Count, 15) * Mathf.Lerp(0.35f, 0.9f, strength) * lengthMultiplier;
        score += Mathf.Min(boardSpan, 16) * Mathf.Lerp(0.2f, 0.65f, strength) * spanMultiplier;

        if (turnCount >= 2)
        {
            score += Mathf.Min(turnCount, 7) * Mathf.Lerp(2f, 4.5f, strength) * turnMultiplier;
        }
        else if (turnCount == 0 && candidateCells.Count >= 7)
        {
            score += Mathf.Lerp(2f, 6f, strength);
        }

        if (!hasDependencyPurpose && generatedArrows.Count >= 3)
        {
            score -= Mathf.Lerp(10f, 32f, strength) * purposelessPenaltyMultiplier;
        }

        if (mode == GeneratorAlgorithmMode.CompactLocks)
        {
            score += Mathf.Max(0, 9 - candidateCells.Count) * Mathf.Lerp(0.4f, 1.4f, strength);
        }

        int desiredPlacementRoutes = GetGeneratorDesiredPlacementRoutes(mode, currentPlayableRoutes);
        score -= Mathf.Max(0, playableRoutesAfterPlacement - desiredPlacementRoutes)
            * Mathf.Lerp(3f, 10f, strength)
            * currentRoutePenaltyMultiplier;
        int futureRouteCeiling = GetGeneratorFutureRouteCeiling(mode);
        score -= Mathf.Max(0, futureMaxPlayableRoutes - futureRouteCeiling)
            * Mathf.Lerp(14f, 34f, strength)
            * futureRoutePenaltyMultiplier;

        if (mode == GeneratorAlgorithmMode.LockstepWeave)
        {
            score += CalculateLockstepWeaveCandidateBonus(
                candidate,
                candidateCells,
                generatedArrows,
                blockedExistingArrows,
                createsChainLink,
                candidateWasBlocked,
                currentPlayableRoutes,
                playableRoutesAfterPlacement,
                occupied,
                zoneSet);
        }

        return score;
    }

    // Lockstep Weave alternates dependency anchors across the board. This makes
    // the intended route travel between regions instead of peeling one edge.
    private float CalculateLockstepWeaveCandidateBonus(
        RuntimeArrowDraft candidate,
        HashSet<Vector2Int> candidateCells,
        List<RuntimeArrowDraft> generatedArrows,
        int blockedExistingArrows,
        bool createsChainLink,
        bool candidateWasBlocked,
        int currentPlayableRoutes,
        int playableRoutesAfterPlacement,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet)
    {
        if (generatedArrows.Count == 0 || candidateCells.Count == 0)
        {
            return 0f;
        }

        Vector2 candidateCenter = Vector2.zero;
        foreach (Vector2Int cell in candidateCells)
        {
            candidateCenter += (Vector2)cell;
        }
        candidateCenter /= candidateCells.Count;

        RuntimeArrowDraft previous = generatedArrows[generatedArrows.Count - 1];
        GetGeneratedArrowBounds(previous, out Vector2Int previousMin, out Vector2Int previousMax);
        Vector2 previousCenter = new Vector2(
            (previousMin.x + previousMax.x) * 0.5f,
            (previousMin.y + previousMax.y) * 0.5f);

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;
        foreach (Vector2Int cell in zoneSet)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        float deltaX = Mathf.Abs(candidateCenter.x - previousCenter.x);
        float deltaY = Mathf.Abs(candidateCenter.y - previousCenter.y);
        float boardWidth = Mathf.Max(1f, maxX - minX + 1f);
        float boardHeight = Mathf.Max(1f, maxY - minY + 1f);
        float normalizedJump = deltaX / boardWidth + deltaY / boardHeight;
        int candidateRegion = GetGeneratedSpatialRegionIndex(candidateCenter, minX, maxX, minY, maxY);
        int previousRegion = GetGeneratedSpatialRegionIndex(previousCenter, minX, maxX, minY, maxY);
        bool buildingReleaseGroup = currentPlayableRoutes < GeneratorGateBuildThreshold
            && blockedExistingArrows == 0;

        float score = normalizedJump * (buildingReleaseGroup ? 12f : 70f);
        if (candidateRegion != previousRegion)
        {
            score += buildingReleaseGroup ? 18f : 28f;
        }
        if (candidateRegion % 3 != previousRegion % 3)
        {
            score += buildingReleaseGroup ? 22f : 34f;
        }
        if (candidateRegion / 3 != previousRegion / 3)
        {
            score += buildingReleaseGroup ? 14f : 18f;
        }

        int turnCount = Mathf.Max(0, candidate.points.Count - 2);
        score += Mathf.Min(candidateCells.Count, 18) * 1.5f;
        score += Mathf.Min(turnCount, 8) * 8f;
        if (candidateCells.Count <= 3 && turnCount == 0)
        {
            score -= 55f;
        }

        if (buildingReleaseGroup)
        {
            int expectedRoutes = Mathf.Min(GeneratorGateBuildThreshold, currentPlayableRoutes + 1);
            score -= Mathf.Abs(playableRoutesAfterPlacement - expectedRoutes) * 46f;
            if (!candidateWasBlocked && blockedExistingArrows == 0 && playableRoutesAfterPlacement > currentPlayableRoutes)
            {
                score += 115f;
            }

            if (!candidateWasBlocked)
            {
                Vector2Int candidateHead = candidate.points[candidate.points.Count - 1];
                int nearestPlayableHeadDistance = int.MaxValue;
                int differentPlayableRegions = 0;
                for (int i = 0; i < generatedArrows.Count; i++)
                {
                    RuntimeArrowDraft existing = generatedArrows[i];
                    if (!CanGeneratedArrowEscapeThroughOccupied(existing, occupied, zoneSet))
                    {
                        continue;
                    }

                    Vector2Int existingHead = existing.points[existing.points.Count - 1];
                    int headDistance = Mathf.Abs(candidateHead.x - existingHead.x)
                        + Mathf.Abs(candidateHead.y - existingHead.y);
                    nearestPlayableHeadDistance = Mathf.Min(nearestPlayableHeadDistance, headDistance);

                    int existingRegion = GetGeneratedSpatialRegionIndex(existingHead, minX, maxX, minY, maxY);
                    differentPlayableRegions += existingRegion != candidateRegion ? 1 : 0;
                }

                if (nearestPlayableHeadDistance < int.MaxValue)
                {
                    int desiredDistance = GetGeneratedDecisionChoiceDistanceThreshold(zoneSet);
                    score += Mathf.Min(nearestPlayableHeadDistance, desiredDistance * 2) * 14f;
                    score -= Mathf.Max(0, desiredDistance - nearestPlayableHeadDistance) * 48f;
                    score += differentPlayableRegions * 36f;
                }
            }
        }
        else
        {
            int controlledDependents = Mathf.Clamp(
                blockedExistingArrows,
                0,
                GeneratorGateMaximumDependents);
            score += controlledDependents * 38f;
            if (blockedExistingArrows >= GeneratorGateMinimumDependents)
            {
                score += 90f;
            }

            score -= Mathf.Abs(playableRoutesAfterPlacement - GeneratorTargetMaxPlayableRoutes) * 34f;
        }

        score += createsChainLink ? 36f : 0f;
        score -= Mathf.Max(0, blockedExistingArrows - GeneratorGateMaximumDependents) * 55f;

        Vector2Int exitDirection = GetGeneratedExitDirection(candidate);
        int repeatedRecentDirections = 0;
        for (int i = generatedArrows.Count - 1; i >= Mathf.Max(0, generatedArrows.Count - 4); i--)
        {
            if (GetGeneratedExitDirection(generatedArrows[i]) == exitDirection)
            {
                repeatedRecentDirections++;
            }
        }
        score -= repeatedRecentDirections * 24f;
        return score;
    }

    private int CountGeneratedExitRaysBlockedByCandidate(
        HashSet<Vector2Int> candidateCells,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet,
        out int totalBlockingDistance,
        out int horizontalBlockedArrows,
        out int nearBlockedArrows,
        out int remoteBlockedArrows)
    {
        int blockedArrowCount = 0;
        totalBlockingDistance = 0;
        horizontalBlockedArrows = 0;
        nearBlockedArrows = 0;
        remoteBlockedArrows = 0;
        int remoteDistanceThreshold = GetGeneratedRemoteBlockerDistanceThreshold(zoneSet);
        for (int i = 0; i < generatedArrows.Count; i++)
        {
            RuntimeArrowDraft arrow = generatedArrows[i];
            Vector2Int direction = GetGeneratedExitDirection(arrow);
            if (direction == Vector2Int.zero)
            {
                continue;
            }

            Vector2Int rayCell = arrow.points[arrow.points.Count - 1] + direction;
            int rayDistance = 1;
            while (IsInsideGridBounds(rayCell))
            {
                if (candidateCells.Contains(rayCell))
                {
                    blockedArrowCount++;
                    totalBlockingDistance += rayDistance;
                    if (direction.x != 0)
                    {
                        horizontalBlockedArrows++;
                    }

                    if (rayDistance <= GeneratorDecisionExitBufferCells)
                    {
                        nearBlockedArrows++;
                    }

                    if (rayDistance >= remoteDistanceThreshold)
                    {
                        remoteBlockedArrows++;
                    }
                    break;
                }

                if (occupied.Contains(rayCell))
                {
                    break;
                }

                rayCell += direction;
                rayDistance++;
            }
        }

        return blockedArrowCount;
    }

    // Counts both immediate and later blockers. A cell hidden behind the current
    // first blocker is still useful because it becomes part of a later solve layer.
    private int CountGeneratedPotentialExitRaysCrossedByCandidate(
        HashSet<Vector2Int> candidateCells,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> zoneSet)
    {
        int crossedRayCount = 0;
        for (int arrowIndex = 0; arrowIndex < generatedArrows.Count; arrowIndex++)
        {
            RuntimeArrowDraft arrow = generatedArrows[arrowIndex];
            Vector2Int direction = GetGeneratedExitDirection(arrow);
            if (direction == Vector2Int.zero)
            {
                continue;
            }

            Vector2Int cell = arrow.points[arrow.points.Count - 1] + direction;
            while (IsInsideGridBounds(cell))
            {
                if (candidateCells.Contains(cell))
                {
                    crossedRayCount++;
                    break;
                }

                cell += direction;
            }
        }

        return crossedRayCount;
    }

    // Gate Network reserves the first cells in front of every head. A dependency
    // can still cross that ray, but it must do so far enough away that the player
    // has to trace the sightline instead of reading the adjacent cell.
    private bool ViolatesGeneratedDecisionCorridors(
        RuntimeArrowDraft candidate,
        HashSet<Vector2Int> candidateCells,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet)
    {
        if (WouldEnterGeneratedExitBuffer(candidateCells, generatedArrows, zoneSet, -1))
        {
            return true;
        }

        int distance = GetGeneratedFirstBlockerDistance(candidate, occupied, zoneSet);
        return distance > 0 && distance <= GeneratorDecisionExitBufferCells;
    }

    private bool WouldEnterGeneratedExitBuffer(
        HashSet<Vector2Int> newCells,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> zoneSet,
        int ignoredArrowIndex)
    {
        for (int arrowIndex = 0; arrowIndex < generatedArrows.Count; arrowIndex++)
        {
            if (arrowIndex == ignoredArrowIndex)
            {
                continue;
            }

            RuntimeArrowDraft arrow = generatedArrows[arrowIndex];
            Vector2Int direction = GetGeneratedExitDirection(arrow);
            if (direction == Vector2Int.zero)
            {
                continue;
            }

            Vector2Int cell = arrow.points[arrow.points.Count - 1] + direction;
            for (int distance = 1;
                distance <= GeneratorDecisionExitBufferCells && IsInsideGridBounds(cell);
                distance++, cell += direction)
            {
                if (newCells.Contains(cell))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int GetGeneratedFirstBlockerDistance(
        RuntimeArrowDraft arrow,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet)
    {
        Vector2Int direction = GetGeneratedExitDirection(arrow);
        if (direction == Vector2Int.zero)
        {
            return -1;
        }

        Vector2Int cell = arrow.points[arrow.points.Count - 1] + direction;
        int distance = 1;
        while (IsInsideGridBounds(cell))
        {
            if (occupied.Contains(cell))
            {
                return distance;
            }

            distance++;
            cell += direction;
        }

        return 0;
    }

    private static int GetGeneratedDecisionChoiceDistanceThreshold(HashSet<Vector2Int> zoneSet)
    {
        GetGeneratedZoneBounds(zoneSet, out int minX, out int maxX, out int minY, out int maxY);
        int maximumManhattanDistance = Mathf.Max(1, maxX - minX + maxY - minY);
        return Mathf.Max(4, Mathf.CeilToInt(maximumManhattanDistance * 0.25f));
    }

    private static int GetGeneratedRemoteBlockerDistanceThreshold(HashSet<Vector2Int> zoneSet)
    {
        GetGeneratedZoneBounds(zoneSet, out int minX, out int maxX, out int minY, out int maxY);
        int longestSide = Mathf.Max(maxX - minX + 1, maxY - minY + 1);
        return Mathf.Max(4, Mathf.CeilToInt(longestSide * 0.3f));
    }

    private static void GetGeneratedZoneBounds(
        HashSet<Vector2Int> zoneSet,
        out int minX,
        out int maxX,
        out int minY,
        out int maxY)
    {
        minX = int.MaxValue;
        maxX = int.MinValue;
        minY = int.MaxValue;
        maxY = int.MinValue;
        foreach (Vector2Int cell in zoneSet)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        if (zoneSet.Count == 0)
        {
            minX = maxX = minY = maxY = 0;
        }
    }

    private static int CountGeneratedCandidateAdjacencyEdges(
        HashSet<Vector2Int> candidateCells,
        HashSet<Vector2Int> occupied)
    {
        int adjacencyEdges = 0;
        foreach (Vector2Int cell in candidateCells)
        {
            adjacencyEdges += occupied.Contains(cell + Vector2Int.left) ? 1 : 0;
            adjacencyEdges += occupied.Contains(cell + Vector2Int.right) ? 1 : 0;
            adjacencyEdges += occupied.Contains(cell + Vector2Int.up) ? 1 : 0;
            adjacencyEdges += occupied.Contains(cell + Vector2Int.down) ? 1 : 0;
        }

        return adjacencyEdges;
    }

    private int CountGeneratedCandidateBoundsIntersections(
        RuntimeArrowDraft candidate,
        List<RuntimeArrowDraft> generatedArrows)
    {
        GetGeneratedArrowBounds(candidate, out Vector2Int candidateMin, out Vector2Int candidateMax);
        int count = 0;

        for (int i = 0; i < generatedArrows.Count; i++)
        {
            GetGeneratedArrowBounds(generatedArrows[i], out Vector2Int existingMin, out Vector2Int existingMax);
            bool overlapsX = candidateMin.x <= existingMax.x && candidateMax.x >= existingMin.x;
            bool overlapsY = candidateMin.y <= existingMax.y && candidateMax.y >= existingMin.y;
            if (overlapsX && overlapsY)
            {
                count++;
            }
        }

        return count;
    }

    // Repairs the small gaps left by the main reverse-blocking pass. Existing arrows
    // grow from their tails first; new short arrows are only used when no tail can
    // reach the remaining cells. Every accepted change is checked for solvability.
    private void CompleteGeneratedFullFill(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int maxLength,
        System.Random rng,
        DateTime generationStart,
        int timeBudgetMs,
        int targetOccupiedCells = -1,
        bool prioritizeSmallRemainders = false)
    {
        int repairTarget = targetOccupiedCells > 0
            ? Mathf.Clamp(targetOccupiedCells, occupied.Count, zoneCells.Count)
            : zoneCells.Count;
        int repairGuard = Mathf.Max(16, zoneCells.Count * 2);

        while (occupied.Count < repairTarget
            && repairGuard-- > 0
            && !IsGeneratorTimeExpired(generationStart, timeBudgetMs))
        {
            bool gateNetworkMode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex == GeneratorAlgorithmMode.LockstepWeave;
            bool tryCompletionFirst = prioritizeSmallRemainders
                && gateNetworkMode
                && repairTarget - occupied.Count <= Mathf.Max(8, Mathf.Min(maxLength, 24));
            if (tryCompletionFirst
                && TryAbsorbGeneratedSmallFreeComponentIntoTail(
                    zoneCells,
                    placementZoneSet,
                    solveZoneSet,
                    generatedArrows,
                    occupied,
                    maxLength,
                    rng))
            {
                continue;
            }

            if (tryCompletionFirst
                && generatedArrows.Count < 500
                && TryPlaceGeneratedPackedCompletionArrow(
                    zoneCells,
                    placementZoneSet,
                    solveZoneSet,
                    generatedArrows,
                    occupied,
                    maxLength,
                    repairTarget,
                    rng,
                    generationStart,
                    timeBudgetMs,
                    true))
            {
                continue;
            }

            if (TryExtendGeneratedTailIntoGap(
                zoneCells,
                placementZoneSet,
                solveZoneSet,
                generatedArrows,
                occupied,
                maxLength,
                repairTarget,
                rng,
                generationStart,
                timeBudgetMs))
            {
                continue;
            }

            if (gateNetworkMode
                && !tryCompletionFirst
                && generatedArrows.Count < 500
                && TryPlaceGeneratedPackedCompletionArrow(
                    zoneCells,
                    placementZoneSet,
                    solveZoneSet,
                    generatedArrows,
                    occupied,
                    maxLength,
                    repairTarget,
                    rng,
                    generationStart,
                    timeBudgetMs,
                    false))
            {
                continue;
            }

            if (generatedArrows.Count < 500
                && TryPlaceGeneratedCompletionArrow(
                    zoneCells,
                    placementZoneSet,
                    solveZoneSet,
                    generatedArrows,
                    occupied,
                    maxLength,
                    rng,
                    generationStart,
                    timeBudgetMs))
            {
                continue;
            }

            break;
        }
    }

    // Small endgame pockets are cheapest to repair by attaching the whole
    // connected component to a neighboring tail. The arrow keeps its head and
    // exit direction, so this usually preserves the existing dependency role.
    private bool TryAbsorbGeneratedSmallFreeComponentIntoTail(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int maxLength,
        System.Random rng)
    {
        List<List<Vector2Int>> components = CollectGeneratedFreeCellComponents(zoneCells, placementZoneSet, occupied);
        ShuffleList(components, rng);
        components.Sort((a, b) => a.Count.CompareTo(b.Count));
        List<Vector2Int> sourcePath = new List<Vector2Int>();

        for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
        {
            List<Vector2Int> component = components[componentIndex];
            if (component.Count == 0 || component.Count > 10)
            {
                continue;
            }

            HashSet<Vector2Int> componentSet = new HashSet<Vector2Int>(component);
            List<int> arrowOrder = new List<int>();
            for (int arrowIndex = 0; arrowIndex < generatedArrows.Count; arrowIndex++)
            {
                arrowOrder.Add(arrowIndex);
            }
            ShuffleList(arrowOrder, rng);

            for (int orderIndex = 0; orderIndex < arrowOrder.Count; orderIndex++)
            {
                int arrowIndex = arrowOrder[orderIndex];
                RuntimeArrowDraft sourceArrow = generatedArrows[arrowIndex];
                if (!ExpandGeneratedPathCells(sourceArrow.points, sourcePath)
                    || sourcePath.Count + component.Count > maxLength + Mathf.Min(8, component.Count))
                {
                    continue;
                }

                Vector2Int tail = sourcePath[0];
                List<Vector2Int> attachmentCells = new List<Vector2Int>();
                foreach (Vector2Int cell in component)
                {
                    Vector2Int delta = cell - tail;
                    if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                    {
                        attachmentCells.Add(cell);
                    }
                }
                ShuffleList(attachmentCells, rng);

                for (int attachmentIndex = 0; attachmentIndex < attachmentCells.Count; attachmentIndex++)
                {
                    List<Vector2Int> nearToFarPath = new List<Vector2Int>();
                    HashSet<Vector2Int> used = new HashSet<Vector2Int>();
                    if (!TryBuildGeneratedComponentCoveringPath(
                        attachmentCells[attachmentIndex],
                        componentSet,
                        used,
                        nearToFarPath,
                        rng))
                    {
                        continue;
                    }

                    List<Vector2Int> extendedPath = new List<Vector2Int>(sourcePath.Count + component.Count);
                    for (int pathIndex = nearToFarPath.Count - 1; pathIndex >= 0; pathIndex--)
                    {
                        extendedPath.Add(nearToFarPath[pathIndex]);
                    }
                    extendedPath.AddRange(sourcePath);

                    RuntimeArrowDraft extendedArrow = new RuntimeArrowDraft
                    {
                        id = sourceArrow.id,
                        color = sourceArrow.color,
                        generatedProfile = GeneratedArrowProfile.OrganicWinding
                    };
                    extendedArrow.points.AddRange(CompressGeneratedPath(extendedPath));
                    if (extendedArrow.points.Count < 2
                        || PathArrowUtility.TryFindSelfOverlap(extendedArrow.points, out _, out _)
                        || PathArrowUtility.TryFindOwnExitBlock(extendedArrow.points, width, height, solveZoneSet, out _)
                        || WouldExceedGeneratedUShapeLimit(generatedArrows, extendedArrow, arrowIndex))
                    {
                        continue;
                    }

                    generatedArrows[arrowIndex] = extendedArrow;
                    foreach (Vector2Int cell in component)
                    {
                        occupied.Add(cell);
                    }

                    bool accepted = CanGeneratedLevelSolve(generatedArrows, solveZoneSet)
                        && HasGeneratedDecisionSpacingAcrossAllWaves(generatedArrows, solveZoneSet, false);
                    if (accepted)
                    {
                        return true;
                    }

                    foreach (Vector2Int cell in component)
                    {
                        occupied.Remove(cell);
                    }
                    generatedArrows[arrowIndex] = sourceArrow;
                }
            }
        }

        return false;
    }

    private List<List<Vector2Int>> CollectGeneratedFreeCellComponents(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied)
    {
        HashSet<Vector2Int> remaining = new HashSet<Vector2Int>();
        for (int cellIndex = 0; cellIndex < zoneCells.Count; cellIndex++)
        {
            Vector2Int cell = zoneCells[cellIndex];
            if (!occupied.Contains(cell))
            {
                remaining.Add(cell);
            }
        }

        List<List<Vector2Int>> result = new List<List<Vector2Int>>();
        Queue<Vector2Int> pending = new Queue<Vector2Int>();
        while (remaining.Count > 0)
        {
            Vector2Int start = Vector2Int.zero;
            foreach (Vector2Int cell in remaining)
            {
                start = cell;
                break;
            }

            List<Vector2Int> component = new List<Vector2Int>();
            remaining.Remove(start);
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                Vector2Int current = pending.Dequeue();
                component.Add(current);
                Vector2Int[] neighbors =
                {
                    current + Vector2Int.right,
                    current + Vector2Int.left,
                    current + Vector2Int.up,
                    current + Vector2Int.down
                };
                for (int neighborIndex = 0; neighborIndex < neighbors.Length; neighborIndex++)
                {
                    Vector2Int neighbor = neighbors[neighborIndex];
                    if (zoneSet.Contains(neighbor) && remaining.Remove(neighbor))
                    {
                        pending.Enqueue(neighbor);
                    }
                }
            }

            result.Add(component);
        }

        return result;
    }

    private bool TryBuildGeneratedComponentCoveringPath(
        Vector2Int current,
        HashSet<Vector2Int> component,
        HashSet<Vector2Int> used,
        List<Vector2Int> path,
        System.Random rng)
    {
        used.Add(current);
        path.Add(current);
        if (used.Count == component.Count)
        {
            return true;
        }

        List<Vector2Int> neighbors = new List<Vector2Int>(4);
        Vector2Int[] directions =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };
        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2Int neighbor = current + directions[directionIndex];
            if (component.Contains(neighbor) && !used.Contains(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }
        ShuffleList(neighbors, rng);
        neighbors.Sort((a, b) =>
            CountUnusedGeneratedComponentNeighbors(a, component, used)
                .CompareTo(CountUnusedGeneratedComponentNeighbors(b, component, used)));

        for (int neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
        {
            if (TryBuildGeneratedComponentCoveringPath(neighbors[neighborIndex], component, used, path, rng))
            {
                return true;
            }
        }

        used.Remove(current);
        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static int CountUnusedGeneratedComponentNeighbors(
        Vector2Int cell,
        HashSet<Vector2Int> component,
        HashSet<Vector2Int> used)
    {
        int count = 0;
        count += component.Contains(cell + Vector2Int.right) && !used.Contains(cell + Vector2Int.right) ? 1 : 0;
        count += component.Contains(cell + Vector2Int.left) && !used.Contains(cell + Vector2Int.left) ? 1 : 0;
        count += component.Contains(cell + Vector2Int.up) && !used.Contains(cell + Vector2Int.up) ? 1 : 0;
        count += component.Contains(cell + Vector2Int.down) && !used.Contains(cell + Vector2Int.down) ? 1 : 0;
        return count;
    }

    private bool TryExtendGeneratedTailIntoGap(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int maxLength,
        int repairTarget,
        System.Random rng,
        DateTime generationStart,
        int timeBudgetMs)
    {
        if (generatedArrows.Count == 0)
        {
            return false;
        }

        if ((GeneratorAlgorithmMode)generatorAlgorithmModeIndex == GeneratorAlgorithmMode.LockstepWeave)
        {
            return TryPackGeneratedGateTailIntoGap(
                zoneCells,
                placementZoneSet,
                solveZoneSet,
                generatedArrows,
                occupied,
                maxLength,
                repairTarget,
                rng,
                generationStart,
                timeBudgetMs);
        }

        List<int> arrowOrder = new List<int>();
        for (int i = 0; i < generatedArrows.Count; i++)
        {
            arrowOrder.Add(i);
        }

        ShuffleList(arrowOrder, rng);
        arrowOrder.Sort((a, b) =>
        {
            bool aPlayable = CanGeneratedArrowEscapeThroughOccupied(generatedArrows[a], occupied, solveZoneSet);
            bool bPlayable = CanGeneratedArrowEscapeThroughOccupied(generatedArrows[b], occupied, solveZoneSet);
            return aPlayable == bPlayable ? 0 : (aPlayable ? -1 : 1);
        });

        List<Vector2Int> pathCells = new List<Vector2Int>();
        int fallbackArrowIndex = -1;
        RuntimeArrowDraft fallbackExtendedArrow = null;
        Vector2Int fallbackExtensionCell = Vector2Int.zero;
        for (int orderIndex = 0; orderIndex < arrowOrder.Count && !IsGeneratorTimeExpired(generationStart, timeBudgetMs); orderIndex++)
        {
            int arrowIndex = arrowOrder[orderIndex];
            RuntimeArrowDraft sourceArrow = generatedArrows[arrowIndex];
            if (!ExpandGeneratedPathCells(sourceArrow.points, pathCells) || pathCells.Count >= maxLength)
            {
                continue;
            }

            Vector2Int tail = pathCells[0];
            List<Vector2Int> candidates = GetFreeTailExtensionCells(tail, placementZoneSet, occupied, rng);
            candidates.Sort((a, b) =>
                CountFreeGeneratorNeighbors(a, placementZoneSet, occupied).CompareTo(CountFreeGeneratorNeighbors(b, placementZoneSet, occupied)));

            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                Vector2Int extensionCell = candidates[candidateIndex];
                List<Vector2Int> extendedPath = new List<Vector2Int>(pathCells.Count + 1) { extensionCell };
                extendedPath.AddRange(pathCells);

                RuntimeArrowDraft extendedArrow = new RuntimeArrowDraft
                {
                    id = sourceArrow.id,
                    color = sourceArrow.color,
                    generatedProfile = GeneratedArrowProfile.Unknown
                };
                extendedArrow.points.AddRange(CompressGeneratedPath(extendedPath));

                if (extendedArrow.points.Count < 2
                    || PathArrowUtility.TryFindSelfOverlap(extendedArrow.points, out _, out _)
                    || PathArrowUtility.TryFindOwnExitBlock(extendedArrow.points, width, height, solveZoneSet, out _))
                {
                    continue;
                }

                HashSet<Vector2Int> extendedCells = new HashSet<Vector2Int>();
                if (!FillGeneratedCells(extendedArrow.points, placementZoneSet, extendedCells)
                    || extendedCells.Count != pathCells.Count + 1)
                {
                    continue;
                }

                HashSet<Vector2Int> addedCell = new HashSet<Vector2Int> { extensionCell };

                if (WouldLeaveUnfillableFreeRegion(
                    zoneCells,
                    placementZoneSet,
                    occupied,
                    addedCell,
                    2,
                    zoneCells.Count))
                {
                    continue;
                }

                generatedArrows[arrowIndex] = extendedArrow;
                occupied.Add(extensionCell);

                if (CanGeneratedLevelSolve(generatedArrows, solveZoneSet)
                    && ((GeneratorAlgorithmMode)generatorAlgorithmModeIndex != GeneratorAlgorithmMode.LockstepWeave
                        || HasGeneratedDecisionSpacingAcrossAllWaves(generatedArrows, solveZoneSet)))
                {
                    if (!WouldExceedGeneratedUShapeLimit(generatedArrows, extendedArrow, arrowIndex))
                    {
                        return true;
                    }

                    if (fallbackExtendedArrow == null)
                    {
                        fallbackArrowIndex = arrowIndex;
                        fallbackExtendedArrow = extendedArrow;
                        fallbackExtensionCell = extensionCell;
                    }
                }

                occupied.Remove(extensionCell);
                generatedArrows[arrowIndex] = sourceArrow;
            }
        }

        if (fallbackExtendedArrow != null)
        {
            generatedArrows[fallbackArrowIndex] = fallbackExtendedArrow;
            occupied.Add(fallbackExtensionCell);
            return true;
        }

        return false;
    }

    // Gate Network packs several connected cells onto an existing tail before it
    // creates another arrow. Cells behind a blocker may become later dependency
    // layers, so the completed board is validated after every packed extension.
    private bool TryPackGeneratedGateTailIntoGap(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int maxLength,
        int repairTarget,
        System.Random rng,
        DateTime generationStart,
        int timeBudgetMs)
    {
        int remainingTargetCells = Mathf.Max(0, repairTarget - occupied.Count);
        if (remainingTargetCells == 0)
        {
            return false;
        }

        bool allowProtectedPacking = occupied.Count * 100 >= placementZoneSet.Count * 82;
        List<int> arrowOrder = new List<int>();
        for (int arrowIndex = 0; arrowIndex < generatedArrows.Count; arrowIndex++)
        {
            arrowOrder.Add(arrowIndex);
        }

        ShuffleList(arrowOrder, rng);
        List<Vector2Int> sourcePath = new List<Vector2Int>();
        for (int orderIndex = 0;
            orderIndex < arrowOrder.Count && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
            orderIndex++)
        {
            int arrowIndex = arrowOrder[orderIndex];
            RuntimeArrowDraft sourceArrow = generatedArrows[arrowIndex];
            if (!ExpandGeneratedPathCells(sourceArrow.points, sourcePath))
            {
                continue;
            }

            int maximumAdditionalCells = Mathf.Min(
                Mathf.Min(maxLength - sourcePath.Count, remainingTargetCells),
                14);
            if (maximumAdditionalCells <= 0)
            {
                continue;
            }

            Vector2Int tail = sourcePath[0];
            List<Vector2Int> firstCells = GetFreeTailExtensionCells(tail, placementZoneSet, occupied, rng);
            List<List<Vector2Int>> packingPaths = new List<List<Vector2Int>>();
            for (int firstIndex = 0; firstIndex < firstCells.Count; firstIndex++)
            {
                List<Vector2Int> packingPath = BuildGeneratedTailPackingPath(
                    firstCells[firstIndex],
                    placementZoneSet,
                    occupied,
                    maximumAdditionalCells,
                    rng);
                if (packingPath.Count > 0)
                {
                    packingPaths.Add(packingPath);
                }
            }

            packingPaths.Sort((a, b) => b.Count.CompareTo(a.Count));
            for (int pathIndex = 0;
                pathIndex < packingPaths.Count && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
                pathIndex++)
            {
                List<Vector2Int> packingPath = packingPaths[pathIndex];
                for (int packedCount = packingPath.Count;
                    packedCount >= 1 && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
                    packedCount--)
                {
                    HashSet<Vector2Int> addedCells = new HashSet<Vector2Int>();
                    for (int cellIndex = 0; cellIndex < packedCount; cellIndex++)
                    {
                        addedCells.Add(packingPath[cellIndex]);
                    }

                    if (!allowProtectedPacking
                        && WouldEnterGeneratedExitBuffer(addedCells, generatedArrows, solveZoneSet, arrowIndex))
                    {
                        continue;
                    }

                    if (WouldWorsenUnfillableFreeRegions(
                        zoneCells,
                        placementZoneSet,
                        occupied,
                        addedCells,
                        2,
                        repairTarget))
                    {
                        continue;
                    }

                    List<Vector2Int> extendedPath = new List<Vector2Int>(sourcePath.Count + packedCount);
                    for (int cellIndex = packedCount - 1; cellIndex >= 0; cellIndex--)
                    {
                        extendedPath.Add(packingPath[cellIndex]);
                    }
                    extendedPath.AddRange(sourcePath);

                    RuntimeArrowDraft extendedArrow = new RuntimeArrowDraft
                    {
                        id = sourceArrow.id,
                        color = sourceArrow.color,
                        generatedProfile = GeneratedArrowProfile.OrganicWinding
                    };
                    extendedArrow.points.AddRange(CompressGeneratedPath(extendedPath));

                    if (extendedArrow.points.Count < 2
                        || PathArrowUtility.TryFindSelfOverlap(extendedArrow.points, out _, out _)
                        || PathArrowUtility.TryFindOwnExitBlock(extendedArrow.points, width, height, solveZoneSet, out _))
                    {
                        continue;
                    }

                    HashSet<Vector2Int> extendedCells = new HashSet<Vector2Int>();
                    if (!FillGeneratedCells(extendedArrow.points, placementZoneSet, extendedCells)
                        || extendedCells.Count != sourcePath.Count + packedCount)
                    {
                        continue;
                    }

                    generatedArrows[arrowIndex] = extendedArrow;
                    foreach (Vector2Int cell in addedCells)
                    {
                        occupied.Add(cell);
                    }

                    bool accepted = !WouldExceedGeneratedUShapeLimit(generatedArrows, extendedArrow, arrowIndex)
                        && CanGeneratedLevelSolve(generatedArrows, solveZoneSet)
                        && HasGeneratedDecisionSpacingAcrossAllWaves(generatedArrows, solveZoneSet, false);
                    if (accepted)
                    {
                        return true;
                    }

                    foreach (Vector2Int cell in addedCells)
                    {
                        occupied.Remove(cell);
                    }
                    generatedArrows[arrowIndex] = sourceArrow;
                }
            }
        }

        return false;
    }

    private List<Vector2Int> BuildGeneratedTailPackingPath(
        Vector2Int firstCell,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied,
        int maximumCount,
        System.Random rng)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        Vector2Int current = firstCell;

        while (result.Count < maximumCount)
        {
            result.Add(current);
            used.Add(current);

            List<Vector2Int> nextCells = GetGeneratedPackingNeighbors(current, zoneSet, occupied, used);
            if (nextCells.Count == 0)
            {
                break;
            }

            int fewestOptions = int.MaxValue;
            List<Vector2Int> bestCells = new List<Vector2Int>();
            for (int nextIndex = 0; nextIndex < nextCells.Count; nextIndex++)
            {
                Vector2Int candidate = nextCells[nextIndex];
                int options = GetGeneratedPackingNeighbors(candidate, zoneSet, occupied, used).Count;
                if (options < fewestOptions)
                {
                    fewestOptions = options;
                    bestCells.Clear();
                    bestCells.Add(candidate);
                }
                else if (options == fewestOptions)
                {
                    bestCells.Add(candidate);
                }
            }

            current = bestCells[rng.Next(bestCells.Count)];
        }

        return result;
    }

    private static List<Vector2Int> GetGeneratedPackingNeighbors(
        Vector2Int cell,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> used)
    {
        List<Vector2Int> result = new List<Vector2Int>(4);
        Vector2Int[] directions =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2Int candidate = cell + directions[directionIndex];
            if (zoneSet.Contains(candidate)
                && !occupied.Contains(candidate)
                && !used.Contains(candidate))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    // Builds a completion arrow from its blocked head backward through an empty
    // pocket. Unlike the profile sampler, this deliberately consumes a connected
    // run of free cells and inserts the arrow into the existing dependency graph.
    private bool TryPlaceGeneratedPackedCompletionArrow(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int maxLength,
        int repairTarget,
        System.Random rng,
        DateTime generationStart,
        int timeBudgetMs,
        bool allowPlayableRouteReplacement)
    {
        List<Vector2Int> freeCells = GetFreeGeneratorCells(zoneCells, occupied);
        if (freeCells.Count < 2)
        {
            return false;
        }

        ShuffleList(freeCells, rng);
        freeCells.Sort((a, b) =>
            CountFreeGeneratorNeighbors(a, placementZoneSet, occupied)
                .CompareTo(CountFreeGeneratorNeighbors(b, placementZoneSet, occupied)));

        int remainingTargetCells = Mathf.Max(0, repairTarget - occupied.Count);
        int maximumPackedLength = Mathf.Min(
            Mathf.Min(Mathf.Max(2, maxLength), freeCells.Count),
            Mathf.Max(2, remainingTargetCells));
        int currentPlayableRoutes = CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, solveZoneSet);
        int routeCeiling = Mathf.Max(GeneratorTargetMaxPlayableRoutes, currentPlayableRoutes);
        int headTryCount = Mathf.Min(freeCells.Count, 192);
        int evaluatedCandidates = 0;

        for (int headIndex = 0;
            headIndex < headTryCount
            && evaluatedCandidates < 96
            && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
            headIndex++)
        {
            Vector2Int head = freeCells[headIndex];
            List<Vector2Int> directions = new List<Vector2Int>
            {
                Vector2Int.right,
                Vector2Int.left,
                Vector2Int.up,
                Vector2Int.down
            };
            ShuffleList(directions, rng);

            for (int directionIndex = 0;
                directionIndex < directions.Count
                && evaluatedCandidates < 96
                && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
                directionIndex++)
            {
                Vector2Int exitDirection = directions[directionIndex];
                Vector2Int previous = head - exitDirection;
                Vector2Int front = head + exitDirection;
                if (!placementZoneSet.Contains(previous)
                    || occupied.Contains(previous))
                {
                    continue;
                }

                bool blockedAtHead = occupied.Contains(front);
                bool exitsImmediately = !IsInsideGridBounds(front);
                if (!allowPlayableRouteReplacement
                    && !blockedAtHead
                    && (!exitsImmediately || currentPlayableRoutes >= GeneratorTargetMaxPlayableRoutes))
                {
                    continue;
                }

                List<Vector2Int> backwardPath = BuildGeneratedPackedArrowBackwardPath(
                    head,
                    previous,
                    placementZoneSet,
                    occupied,
                    maximumPackedLength,
                    rng);
                for (int packedLength = backwardPath.Count;
                    packedLength >= 2
                    && evaluatedCandidates < 96
                    && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
                    packedLength--)
                {
                    evaluatedCandidates++;
                    List<Vector2Int> candidatePath = new List<Vector2Int>(packedLength);
                    for (int pathIndex = packedLength - 1; pathIndex >= 0; pathIndex--)
                    {
                        candidatePath.Add(backwardPath[pathIndex]);
                    }

                    RuntimeArrowDraft candidateArrow = new RuntimeArrowDraft
                    {
                        id = $"Arrow {generatedArrows.Count + 1}",
                        color = GetGeneratedSavedArrowColor(),
                        generatedProfile = GeneratedArrowProfile.OrganicWinding
                    };
                    candidateArrow.points.AddRange(CompressGeneratedPath(candidatePath));

                    if (candidateArrow.points.Count < 2
                        || PathArrowUtility.TryFindSelfOverlap(candidateArrow.points, out _, out _)
                        || PathArrowUtility.TryFindOwnExitBlock(candidateArrow.points, width, height, solveZoneSet, out _)
                        || WouldExceedGeneratedUShapeLimit(generatedArrows, candidateArrow))
                    {
                        continue;
                    }

                    HashSet<Vector2Int> candidateCells = new HashSet<Vector2Int>();
                    if (!FillGeneratedCells(candidateArrow.points, placementZoneSet, candidateCells)
                        || candidateCells.Count != packedLength
                        || WouldWorsenUnfillableFreeRegions(
                            zoneCells,
                            placementZoneSet,
                            occupied,
                            candidateCells,
                            2,
                            repairTarget))
                    {
                        continue;
                    }

                    int blockedExitRays = CountGeneratedExitRaysBlockedByCandidate(
                        candidateCells,
                        generatedArrows,
                        occupied,
                        solveZoneSet,
                        out _,
                        out _,
                        out _,
                        out _);
                    int futureExitRays = CountGeneratedPotentialExitRaysCrossedByCandidate(
                        candidateCells,
                        generatedArrows,
                        solveZoneSet);
                    int candidateBlockerDistance = GetGeneratedFirstBlockerDistance(
                        candidateArrow,
                        occupied,
                        solveZoneSet);
                    bool insertsDependency = blockedAtHead
                        || candidateBlockerDistance > 0
                        || blockedExitRays > 0
                        || futureExitRays > 0;
                    if (!insertsDependency)
                    {
                        continue;
                    }

                    generatedArrows.Add(candidateArrow);
                    foreach (Vector2Int cell in candidateCells)
                    {
                        occupied.Add(cell);
                    }

                    bool accepted = CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, solveZoneSet) <= routeCeiling
                        && CanGeneratedLevelSolve(generatedArrows, solveZoneSet)
                        && HasGeneratedDecisionSpacingAcrossAllWaves(generatedArrows, solveZoneSet, false);
                    if (accepted)
                    {
                        return true;
                    }

                    foreach (Vector2Int cell in candidateCells)
                    {
                        occupied.Remove(cell);
                    }
                    generatedArrows.RemoveAt(generatedArrows.Count - 1);
                }
            }
        }

        return false;
    }

    private List<Vector2Int> BuildGeneratedPackedArrowBackwardPath(
        Vector2Int head,
        Vector2Int previous,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied,
        int maximumCount,
        System.Random rng)
    {
        List<Vector2Int> result = new List<Vector2Int> { head, previous };
        HashSet<Vector2Int> used = new HashSet<Vector2Int>(result);
        Vector2Int current = previous;

        while (result.Count < maximumCount)
        {
            List<Vector2Int> nextCells = GetGeneratedPackingNeighbors(current, zoneSet, occupied, used);
            if (nextCells.Count == 0)
            {
                break;
            }

            ShuffleList(nextCells, rng);
            nextCells.Sort((a, b) =>
                GetGeneratedPackingNeighbors(a, zoneSet, occupied, used).Count
                    .CompareTo(GetGeneratedPackingNeighbors(b, zoneSet, occupied, used).Count));
            current = nextCells[0];
            result.Add(current);
            used.Add(current);
        }

        return result;
    }

    private bool TryPlaceGeneratedCompletionArrow(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int maxLength,
        System.Random rng,
        DateTime generationStart,
        int timeBudgetMs)
    {
        List<Vector2Int> freeCells = GetFreeGeneratorCells(zoneCells, occupied);
        if (freeCells.Count < 2)
        {
            return false;
        }

        RuntimeArrowDraft fallbackUArrow = null;
        HashSet<Vector2Int> fallbackUCells = null;
        float fallbackUScore = float.MinValue;
        bool densePacking = occupied.Count * 100 >= placementZoneSet.Count * 70;

        List<Vector2Int> startCells = GetReverseBlockingCandidateCells(generatedArrows, occupied, solveZoneSet, true);
        HashSet<Vector2Int> uniqueStarts = new HashSet<Vector2Int>(startCells);
        ShuffleList(freeCells, rng);
        SortGeneratorCellsByCompactness(freeCells, occupied);
        for (int i = 0; i < freeCells.Count; i++)
        {
            if (uniqueStarts.Add(freeCells[i]))
            {
                startCells.Add(freeCells[i]);
            }
        }

        int currentPlayableRoutes = CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, solveZoneSet);
        int completionRouteTarget = GetGeneratorPlacementRouteTarget((GeneratorAlgorithmMode)generatorAlgorithmModeIndex);
        int routeCeiling = Mathf.Max(completionRouteTarget, currentPlayableRoutes);
        int maxCompletionLength = Mathf.Min(Mathf.Max(2, maxLength), Mathf.Min(freeCells.Count, 16));
        List<int> lengthOrder = new List<int>();
        for (int length = 2; length <= maxCompletionLength; length++)
        {
            lengthOrder.Add(length);
        }

        ShuffleList(lengthOrder, rng);
        lengthOrder.Sort((a, b) => b.CompareTo(a));
        int startTryCount = Mathf.Min(startCells.Count, densePacking ? 160 : 96);

        for (int startIndex = 0; startIndex < startTryCount && !IsGeneratorTimeExpired(generationStart, timeBudgetMs); startIndex++)
        {
            Vector2Int startCell = startCells[startIndex];
            if (!placementZoneSet.Contains(startCell) || occupied.Contains(startCell))
            {
                continue;
            }

            for (int lengthIndex = 0; lengthIndex < lengthOrder.Count; lengthIndex++)
            {
                int targetLength = lengthOrder[lengthIndex];
                for (int pathAttempt = 0; pathAttempt < 4; pathAttempt++)
                {
                    if (!TryCreateGeneratedArrowCandidate(startCell, targetLength, occupied, placementZoneSet, rng, out RuntimeArrowDraft candidateArrow))
                    {
                        continue;
                    }

                    HashSet<Vector2Int> candidateCells = new HashSet<Vector2Int>();
                    if (!FillGeneratedCells(candidateArrow.points, placementZoneSet, candidateCells))
                    {
                        continue;
                    }

                    if ((GeneratorAlgorithmMode)generatorAlgorithmModeIndex == GeneratorAlgorithmMode.LockstepWeave)
                    {
                        if (!densePacking
                            && ViolatesGeneratedDecisionCorridors(
                            candidateArrow,
                            candidateCells,
                            generatedArrows,
                            occupied,
                            solveZoneSet))
                        {
                            continue;
                        }

                        int blockedArrows = CountGeneratedExitRaysBlockedByCandidate(
                            candidateCells,
                            generatedArrows,
                            occupied,
                            solveZoneSet,
                            out _,
                            out _,
                            out _,
                            out _);
                        int futureBlockedArrows = CountGeneratedPotentialExitRaysCrossedByCandidate(
                            candidateCells,
                            generatedArrows,
                            solveZoneSet);
                        int candidateBlockerDistance = GetGeneratedFirstBlockerDistance(
                            candidateArrow,
                            occupied,
                            solveZoneSet);
                        if (blockedArrows == 0
                            && futureBlockedArrows == 0
                            && candidateBlockerDistance == 0)
                        {
                            continue;
                        }
                    }

                    if (WouldWorsenUnfillableFreeRegions(
                        zoneCells,
                        placementZoneSet,
                        occupied,
                        candidateCells,
                        2,
                        zoneCells.Count))
                    {
                        continue;
                    }

                    float diversityScore = CalculateGeneratedDiversityScore(
                        candidateArrow,
                        candidateCells,
                        generatedArrows,
                        false);
                    bool exceedsUShapeLimit = WouldExceedGeneratedUShapeLimit(generatedArrows, candidateArrow);
                    candidateArrow.id = $"Arrow {generatedArrows.Count + 1}";
                    candidateArrow.color = GetGeneratedSavedArrowColor();
                    generatedArrows.Add(candidateArrow);
                    foreach (Vector2Int cell in candidateCells)
                    {
                        occupied.Add(cell);
                    }

                    bool routeCountAccepted = CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, solveZoneSet) <= routeCeiling;
                    bool decisionSpacingAccepted = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex != GeneratorAlgorithmMode.LockstepWeave
                        || HasGeneratedDecisionSpacingAcrossAllWaves(generatedArrows, solveZoneSet, !densePacking);
                    bool futureRouteProfileAccepted = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex != GeneratorAlgorithmMode.LockstepWeave
                        || (TryMeasureGeneratedSolveProfile(
                                generatedArrows,
                                solveZoneSet,
                                out _,
                                out int completionFutureMaxPlayableRoutes,
                                out _,
                                out _)
                            && completionFutureMaxPlayableRoutes <= GeneratorTargetMaxPlayableRoutes);
                    if (routeCountAccepted
                        && decisionSpacingAccepted
                        && futureRouteProfileAccepted
                        && CanGeneratedLevelSolve(generatedArrows, solveZoneSet))
                    {
                        if (!exceedsUShapeLimit)
                        {
                            return true;
                        }

                        if (fallbackUArrow == null || diversityScore > fallbackUScore)
                        {
                            fallbackUArrow = candidateArrow;
                            fallbackUCells = candidateCells;
                            fallbackUScore = diversityScore;
                        }
                    }

                    foreach (Vector2Int cell in candidateCells)
                    {
                        occupied.Remove(cell);
                    }
                    generatedArrows.RemoveAt(generatedArrows.Count - 1);
                }
            }
        }

        if (fallbackUArrow != null)
        {
            generatedArrows.Add(fallbackUArrow);
            foreach (Vector2Int cell in fallbackUCells)
            {
                occupied.Add(cell);
            }

            return true;
        }

        return false;
    }

    private bool WouldExceedGeneratedUShapeLimit(
        List<RuntimeArrowDraft> generatedArrows,
        RuntimeArrowDraft candidate,
        int replacementIndex = -1)
    {
        if (ClassifyGeneratedArrowShape(candidate) != GeneratedArrowShape.UShape)
        {
            return false;
        }

        int uShapeCount = 1;
        for (int i = 0; i < generatedArrows.Count; i++)
        {
            if (i != replacementIndex && ClassifyGeneratedArrowShape(generatedArrows[i]) == GeneratedArrowShape.UShape)
            {
                uShapeCount++;
            }
        }

        int projectedArrowCount = replacementIndex >= 0
            ? generatedArrows.Count
            : generatedArrows.Count + 1;
        int allowedUShapes = Mathf.Max(1, Mathf.CeilToInt(projectedArrowCount * GeneratorMaxUShapeRatio));
        return uShapeCount > allowedUShapes;
    }

    private List<Vector2Int> GetFreeTailExtensionCells(
        Vector2Int tail,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied,
        System.Random rng)
    {
        List<Vector2Int> candidates = new List<Vector2Int>
        {
            tail + Vector2Int.right,
            tail + Vector2Int.left,
            tail + Vector2Int.up,
            tail + Vector2Int.down
        };
        ShuffleList(candidates, rng);
        candidates.RemoveAll(cell => !zoneSet.Contains(cell) || occupied.Contains(cell));
        return candidates;
    }

    private int CountFreeGeneratorNeighbors(Vector2Int cell, HashSet<Vector2Int> zoneSet, HashSet<Vector2Int> occupied)
    {
        int count = 0;
        if (zoneSet.Contains(cell + Vector2Int.right) && !occupied.Contains(cell + Vector2Int.right)) count++;
        if (zoneSet.Contains(cell + Vector2Int.left) && !occupied.Contains(cell + Vector2Int.left)) count++;
        if (zoneSet.Contains(cell + Vector2Int.up) && !occupied.Contains(cell + Vector2Int.up)) count++;
        if (zoneSet.Contains(cell + Vector2Int.down) && !occupied.Contains(cell + Vector2Int.down)) count++;
        return count;
    }

    private int CountOccupiedGeneratorNeighbors(Vector2Int cell, HashSet<Vector2Int> occupied)
    {
        int count = 0;
        if (occupied.Contains(cell + Vector2Int.right)) count++;
        if (occupied.Contains(cell + Vector2Int.left)) count++;
        if (occupied.Contains(cell + Vector2Int.up)) count++;
        if (occupied.Contains(cell + Vector2Int.down)) count++;
        return count;
    }

    private void SortGeneratorCellsByCompactness(
        List<Vector2Int> cells,
        HashSet<Vector2Int> occupied)
    {
        cells.Sort((a, b) =>
        {
            int occupiedNeighborCompare = CountOccupiedGeneratorNeighbors(b, occupied)
                .CompareTo(CountOccupiedGeneratorNeighbors(a, occupied));
            if (occupiedNeighborCompare != 0)
            {
                return occupiedNeighborCompare;
            }

            // The list is shuffled before this sort, so equal compactness retains
            // variation instead of forcing every arrow toward corners and edges.
            return 0;
        });
    }

    // Early Complex Guided blockers are deliberately spread away from recently
    // placed arrows. This prevents the dependency graph from growing as one local
    // top-to-bottom cluster before the rest of the board becomes relevant.
    private void SortGeneratorCellsBySpatialMix(
        List<Vector2Int> cells,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied)
    {
        if (generatedArrows.Count == 0)
        {
            SortGeneratorCellsByCompactness(cells, occupied);
            return;
        }

        int recentStart = Mathf.Max(0, generatedArrows.Count - 6);
        List<Vector2Int> recentCenters = new List<Vector2Int>();
        for (int i = recentStart; i < generatedArrows.Count; i++)
        {
            GetGeneratedArrowBounds(generatedArrows[i], out Vector2Int minimum, out Vector2Int maximum);
            recentCenters.Add(new Vector2Int(
                Mathf.RoundToInt((minimum.x + maximum.x) * 0.5f),
                Mathf.RoundToInt((minimum.y + maximum.y) * 0.5f)));
        }

        Vector2Int latestCenter = recentCenters[recentCenters.Count - 1];
        cells.Sort((a, b) =>
        {
            int aScore = CalculateSpatialMixCellScore(a, latestCenter, recentCenters, occupied);
            int bScore = CalculateSpatialMixCellScore(b, latestCenter, recentCenters, occupied);
            return bScore.CompareTo(aScore);
        });
    }

    private int CalculateSpatialMixCellScore(
        Vector2Int cell,
        Vector2Int latestCenter,
        List<Vector2Int> recentCenters,
        HashSet<Vector2Int> occupied)
    {
        int latestDistance = Mathf.Abs(cell.x - latestCenter.x) + Mathf.Abs(cell.y - latestCenter.y);
        int nearestRecentDistance = int.MaxValue;
        for (int i = 0; i < recentCenters.Count; i++)
        {
            Vector2Int center = recentCenters[i];
            int distance = Mathf.Abs(cell.x - center.x) + Mathf.Abs(cell.y - center.y);
            nearestRecentDistance = Mathf.Min(nearestRecentDistance, distance);
        }

        return latestDistance * 5
            + nearestRecentDistance * 3
            - CountOccupiedGeneratorNeighbors(cell, occupied) * 4;
    }

    private bool HasUnrepairableSingleCellGap(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> zoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int maxLength)
    {
        for (int i = 0; i < zoneCells.Count; i++)
        {
            Vector2Int cell = zoneCells[i];
            if (occupied.Contains(cell) || CountFreeGeneratorNeighbors(cell, zoneSet, occupied) > 0)
            {
                continue;
            }

            if (!CanAnyGeneratedTailExtendTo(cell, generatedArrows, maxLength))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanAnyGeneratedTailExtendTo(Vector2Int cell, List<RuntimeArrowDraft> generatedArrows, int maxLength)
    {
        List<Vector2Int> pathCells = new List<Vector2Int>();
        for (int i = 0; i < generatedArrows.Count; i++)
        {
            if (!ExpandGeneratedPathCells(generatedArrows[i].points, pathCells) || pathCells.Count >= maxLength)
            {
                continue;
            }

            Vector2Int delta = pathCells[0] - cell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
            {
                return true;
            }
        }

        return false;
    }

    private bool ExpandGeneratedPathCells(IReadOnlyList<Vector2Int> points, List<Vector2Int> pathCells)
    {
        pathCells.Clear();
        if (points == null || points.Count < 2)
        {
            return false;
        }

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2Int start = points[i];
            Vector2Int delta = points[i + 1] - start;
            if (delta == Vector2Int.zero || (delta.x != 0 && delta.y != 0))
            {
                return false;
            }

            Vector2Int step = new Vector2Int(Sign(delta.x), Sign(delta.y));
            int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            int firstDistance = i == 0 ? 0 : 1;
            for (int distance = firstDistance; distance <= length; distance++)
            {
                Vector2Int cell = start + step * distance;
                if (!visited.Add(cell))
                {
                    return false;
                }

                pathCells.Add(cell);
            }
        }

        return pathCells.Count >= 2;
    }

    private bool TryCreateReverseBlockingArrow(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> placementZoneSet,
        HashSet<Vector2Int> solveZoneSet,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int minLength,
        int maxLength,
        int targetOccupiedCells,
        int preferredLength,
        int fillPercent,
        bool allowRelaxedRoutes,
        System.Random rng,
        DateTime generationStart,
        int timeBudgetMs,
        out RuntimeArrowDraft arrow,
        out HashSet<Vector2Int> arrowCells)
    {
        arrow = null;
        arrowCells = null;
        RuntimeArrowDraft relaxedArrow = null;
        HashSet<Vector2Int> relaxedCells = null;
        int relaxedPlayableRoutes = int.MaxValue;
        int relaxedFutureMaxPlayableRoutes = int.MaxValue;
        bool relaxedWasBlocked = false;
        float relaxedDiversityScore = float.MinValue;
        RuntimeArrowDraft bestArrow = null;
        HashSet<Vector2Int> bestCells = null;
        int bestPlayableRoutes = int.MaxValue;
        bool bestWasBlocked = false;
        float bestDiversityScore = float.MinValue;
        int diversityCandidateSamples = 0;
        int diversitySampleTarget = occupied.Count * 5 < targetOccupiedCells * 4
            ? GeneratorDiversityCandidateSamples
            : Mathf.Max(3, GeneratorDiversityCandidateSamples / 2);
        GeneratorAlgorithmMode algorithmMode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
        bool useAdvancedGuided = IsAdvancedGuidedMode(algorithmMode);
        int targetPlayableRoutes = GetGeneratorPlacementRouteTarget(algorithmMode);
        int currentPlayableRoutes = CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, solveZoneSet);
        int placementRouteCeiling = GetGeneratorPlacementRouteCeiling(algorithmMode, currentPlayableRoutes);
        int desiredPlacementRoutes = GetGeneratorDesiredPlacementRoutes(algorithmMode, currentPlayableRoutes);
        int futureRouteCeiling = GetGeneratorFutureRouteCeiling(algorithmMode);
        if (useAdvancedGuided)
        {
            diversitySampleTarget = Mathf.Min(18, diversitySampleTarget * 3);
        }
        if (algorithmMode == GeneratorAlgorithmMode.LockstepWeave)
        {
            diversitySampleTarget = Mathf.Max(diversitySampleTarget, 18);
        }

        List<Vector2Int> blockerCells = GetReverseBlockingCandidateCells(generatedArrows, occupied, solveZoneSet, true);
        if (blockerCells.Count == 0)
        {
            blockerCells = GetReverseBlockingCandidateCells(generatedArrows, occupied, solveZoneSet, false);
        }

        if (blockerCells.Count == 0)
        {
            return false;
        }

        float occupiedRatio = occupied.Count / (float)Mathf.Max(1, targetOccupiedCells);
        if (algorithmMode == GeneratorAlgorithmMode.LockstepWeave
            || (UsesDependencyStructureRules(algorithmMode) && occupiedRatio < 0.5f))
        {
            // Decision-oriented modes preserve the farthest-first ray order so a
            // later compactness pass cannot replace remote blockers with obvious ones.
        }
        else
        {
            ShuffleList(blockerCells, rng);
            SortGeneratorCellsByCompactness(blockerCells, occupied);
        }
        int freeCellCount = Mathf.Min(
            Mathf.Max(0, zoneCells.Count - occupied.Count),
            Mathf.Max(0, targetOccupiedCells - occupied.Count));
        int blockerTryCount = Mathf.Min(
            blockerCells.Count,
            algorithmMode == GeneratorAlgorithmMode.LockstepWeave
                ? 48
                : (fillPercent >= 100 ? 64 : 28));

        for (int blockerIndex = 0;
            blockerIndex < blockerTryCount
            && diversityCandidateSamples < diversitySampleTarget
            && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
            blockerIndex++)
        {
            Vector2Int blockerCell = blockerCells[blockerIndex];
            if (!placementZoneSet.Contains(blockerCell) || occupied.Contains(blockerCell))
            {
                continue;
            }

            List<int> lengthOrder = BuildGeneratorLengthOrder(
                minLength,
                maxLength,
                freeCellCount,
                preferredLength,
                fillPercent,
                rng);
            for (int lengthIndex = 0;
                lengthIndex < lengthOrder.Count
                && diversityCandidateSamples < diversitySampleTarget
                && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
                lengthIndex++)
            {
                int targetLength = lengthOrder[lengthIndex];
                int pathAttemptCount = algorithmMode == GeneratorAlgorithmMode.LockstepWeave
                    ? 6
                    : (fillPercent >= 100 ? 7 : 4);

                for (int pathAttempt = 0;
                    pathAttempt < pathAttemptCount
                    && diversityCandidateSamples < diversitySampleTarget
                    && !IsGeneratorTimeExpired(generationStart, timeBudgetMs);
                    pathAttempt++)
                {
                    if (!TryCreateGeneratedArrowCandidate(blockerCell, targetLength, occupied, placementZoneSet, rng, out RuntimeArrowDraft candidateArrow))
                    {
                        continue;
                    }

                    HashSet<Vector2Int> candidateCells = new HashSet<Vector2Int>();
                    if (!FillGeneratedCells(candidateArrow.points, placementZoneSet, candidateCells) || !candidateCells.Contains(blockerCell))
                    {
                        continue;
                    }


                    if (algorithmMode == GeneratorAlgorithmMode.LockstepWeave
                        && ViolatesGeneratedDecisionCorridors(
                            candidateArrow,
                            candidateCells,
                            generatedArrows,
                            occupied,
                            solveZoneSet))
                    {
                        continue;
                    }

                    if (UsesDependencyStructureRules(algorithmMode)
                        && occupied.Count * 10 < targetOccupiedCells * 9
                        && WouldCreateGeneratedStraightContinuation(candidateArrow, generatedArrows, maxLength))
                    {
                        continue;
                    }

                    if (fillPercent >= 100
                        && WouldLeaveUnfillableFreeRegion(zoneCells, placementZoneSet, occupied, candidateCells, minLength, targetOccupiedCells))
                    {
                        continue;
                    }

                    if (!TryEvaluateGeneratedCandidate(
                        generatedArrows,
                        occupied,
                        solveZoneSet,
                        candidateArrow,
                        candidateCells,
                        out int playableRoutesAfterPlacement,
                        out int futureMaxPlayableRoutes,
                        out bool candidateWasBlocked,
                        out int playableHeadDistanceAfterPlacement))
                    {
                        continue;
                    }


                    if (algorithmMode == GeneratorAlgorithmMode.LockstepWeave
                        && playableRoutesAfterPlacement > 1
                        && playableHeadDistanceAfterPlacement < GetGeneratedDecisionChoiceDistanceThreshold(solveZoneSet))
                    {
                        continue;
                    }

                    float diversityScore = CalculateGeneratedDiversityScore(
                        candidateArrow,
                        candidateCells,
                        generatedArrows,
                        candidateWasBlocked);
                    diversityScore += CalculateAdvancedGuidedCandidateBonus(
                        candidateArrow,
                        candidateCells,
                        generatedArrows,
                        occupied,
                        solveZoneSet,
                        playableRoutesAfterPlacement,
                        futureMaxPlayableRoutes,
                        candidateWasBlocked);

                    bool meetsComplexRouteProfile = !UsesDependencyStructureRules(algorithmMode)
                        || futureMaxPlayableRoutes <= futureRouteCeiling;
                    if (playableRoutesAfterPlacement <= placementRouteCeiling && meetsComplexRouteProfile)
                    {
                        diversityCandidateSamples++;
                        float routeWeight = useAdvancedGuided ? GetAdvancedRouteWeight(algorithmMode) : 12f;
                        float selectionScore = algorithmMode == GeneratorAlgorithmMode.LockstepWeave
                            ? diversityScore - Mathf.Abs(desiredPlacementRoutes - playableRoutesAfterPlacement) * routeWeight
                            : diversityScore + (targetPlayableRoutes - playableRoutesAfterPlacement) * routeWeight;
                        bool isBetterCandidate = bestArrow == null
                            || selectionScore > bestDiversityScore
                            || (Mathf.Approximately(selectionScore, bestDiversityScore)
                                && (playableRoutesAfterPlacement < bestPlayableRoutes
                                    || (playableRoutesAfterPlacement == bestPlayableRoutes
                                        && candidateWasBlocked
                                        && !bestWasBlocked)));
                        if (isBetterCandidate)
                        {
                            bestArrow = candidateArrow;
                            bestCells = candidateCells;
                            bestPlayableRoutes = playableRoutesAfterPlacement;
                            bestWasBlocked = candidateWasBlocked;
                            bestDiversityScore = selectionScore;
                        }

                        continue;
                    }

                    int candidatePeakRoutes = Mathf.Max(playableRoutesAfterPlacement, futureMaxPlayableRoutes);
                    int relaxedPeakRoutes = Mathf.Max(relaxedPlayableRoutes, relaxedFutureMaxPlayableRoutes);
                    bool isBetterRelaxedCandidate = candidatePeakRoutes < relaxedPeakRoutes
                        || (candidatePeakRoutes == relaxedPeakRoutes
                            && (futureMaxPlayableRoutes < relaxedFutureMaxPlayableRoutes
                                || (futureMaxPlayableRoutes == relaxedFutureMaxPlayableRoutes
                                    && (playableRoutesAfterPlacement < relaxedPlayableRoutes
                                        || (playableRoutesAfterPlacement == relaxedPlayableRoutes
                                            && (diversityScore > relaxedDiversityScore
                                                || (Mathf.Approximately(diversityScore, relaxedDiversityScore)
                                                    && candidateWasBlocked
                                                    && !relaxedWasBlocked)))))));
                    if (isBetterRelaxedCandidate)
                    {
                        relaxedArrow = candidateArrow;
                        relaxedCells = candidateCells;
                        relaxedPlayableRoutes = playableRoutesAfterPlacement;
                        relaxedFutureMaxPlayableRoutes = futureMaxPlayableRoutes;
                        relaxedWasBlocked = candidateWasBlocked;
                        relaxedDiversityScore = diversityScore;
                    }
                }
            }
        }

        if (bestArrow != null)
        {
            arrow = bestArrow;
            arrowCells = bestCells;
            return true;
        }

        if (allowRelaxedRoutes && relaxedArrow != null)
        {
            arrow = relaxedArrow;
            arrowCells = relaxedCells;
            return true;
        }

        return false;
    }

    private List<Vector2Int> GetReverseBlockingCandidateCells(
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet,
        bool onlyCurrentlyPlayable)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();
        List<int> arrowOrder = new List<int>();
        for (int i = generatedArrows.Count - 1; i >= 0; i--)
        {
            arrowOrder.Add(i);
        }

        if (UsesDependencyStructureRules((GeneratorAlgorithmMode)generatorAlgorithmModeIndex)
            && arrowOrder.Count > 1)
        {
            Vector2[] centers = new Vector2[generatedArrows.Count];
            for (int i = 0; i < generatedArrows.Count; i++)
            {
                GetGeneratedArrowBounds(generatedArrows[i], out Vector2Int minimum, out Vector2Int maximum);
                centers[i] = new Vector2(
                    (minimum.x + maximum.x) * 0.5f,
                    (minimum.y + maximum.y) * 0.5f);
            }

            Vector2 latestCenter = centers[generatedArrows.Count - 1];
            arrowOrder.Sort((a, b) =>
            {
                float aDistance = Mathf.Abs(centers[a].x - latestCenter.x)
                    + Mathf.Abs(centers[a].y - latestCenter.y);
                float bDistance = Mathf.Abs(centers[b].x - latestCenter.x)
                    + Mathf.Abs(centers[b].y - latestCenter.y);
                int distanceCompare = bDistance.CompareTo(aDistance);
                return distanceCompare != 0 ? distanceCompare : b.CompareTo(a);
            });
        }

        for (int orderIndex = 0; orderIndex < arrowOrder.Count; orderIndex++)
        {
            int i = arrowOrder[orderIndex];
            RuntimeArrowDraft arrow = generatedArrows[i];
            if (onlyCurrentlyPlayable && !CanGeneratedArrowEscapeThroughOccupied(arrow, occupied, zoneSet))
            {
                continue;
            }

            Vector2Int exitDirection = GetGeneratedExitDirection(arrow);
            if (exitDirection == Vector2Int.zero)
            {
                continue;
            }

            Vector2Int checkPosition = arrow.points[arrow.points.Count - 1] + exitDirection;
            List<Vector2Int> rayCells = new List<Vector2Int>();
            while (IsInsideGridBounds(checkPosition))
            {
                if (occupied.Contains(checkPosition))
                {
                    break;
                }

                if (zoneSet.Contains(checkPosition))
                {
                    rayCells.Add(checkPosition);
                }

                checkPosition += exitDirection;
            }

            if (UsesDependencyStructureRules((GeneratorAlgorithmMode)generatorAlgorithmModeIndex))
            {
                rayCells.Reverse();
            }

            for (int rayIndex = 0; rayIndex < rayCells.Count; rayIndex++)
            {
                if (uniqueCells.Add(rayCells[rayIndex]))
                {
                    result.Add(rayCells[rayIndex]);
                }
            }
        }

        if ((GeneratorAlgorithmMode)generatorAlgorithmModeIndex == GeneratorAlgorithmMode.LockstepWeave
            && result.Count > 1)
        {
            Dictionary<Vector2Int, int> originalOrder = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < result.Count; i++)
            {
                originalOrder[result[i]] = i;
            }

            result.Sort((a, b) =>
            {
                int aRayCount = CountGeneratedClearExitRaysThroughCell(a, generatedArrows, occupied, zoneSet);
                int bRayCount = CountGeneratedClearExitRaysThroughCell(b, generatedArrows, occupied, zoneSet);
                int rayCompare = bRayCount.CompareTo(aRayCount);
                return rayCompare != 0 ? rayCompare : originalOrder[a].CompareTo(originalOrder[b]);
            });
        }

        return result;
    }

    private int CountGeneratedClearExitRaysThroughCell(
        Vector2Int cell,
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet)
    {
        int count = 0;
        for (int i = 0; i < generatedArrows.Count; i++)
        {
            RuntimeArrowDraft arrow = generatedArrows[i];
            if (!CanGeneratedArrowEscapeThroughOccupied(arrow, occupied, zoneSet))
            {
                continue;
            }

            Vector2Int direction = GetGeneratedExitDirection(arrow);
            if (direction == Vector2Int.zero)
            {
                continue;
            }

            Vector2Int checkPosition = arrow.points[arrow.points.Count - 1] + direction;
            while (IsInsideGridBounds(checkPosition))
            {
                if (checkPosition == cell)
                {
                    count++;
                    break;
                }

                checkPosition += direction;
            }
        }

        return count;
    }

    private List<int> BuildGeneratorLengthOrder(
        int minLength,
        int maxLength,
        int freeCellCount,
        int requestedPreferredLength,
        int fillPercent,
        System.Random rng)
    {
        List<int> result = new List<int>();
        int cappedMaxLength = Mathf.Min(maxLength, freeCellCount);

        for (int length = minLength; length <= cappedMaxLength; length++)
        {
            result.Add(length);
        }

        if (result.Count > 1)
        {
            ShuffleList(result, rng);
            int preferredLength = Mathf.Clamp(requestedPreferredLength, minLength, cappedMaxLength);
            double variationChance = fillPercent >= 100 ? 0.22 : 0.38;
            if (rng.NextDouble() < variationChance)
            {
                preferredLength = Mathf.Clamp(preferredLength + rng.Next(-1, 2), minLength, cappedMaxLength);
            }

            result.Sort((a, b) =>
            {
                int distanceCompare = Mathf.Abs(a - preferredLength).CompareTo(Mathf.Abs(b - preferredLength));
                return distanceCompare != 0 ? distanceCompare : b.CompareTo(a);
            });
        }

        return result;
    }

    private int ChooseGeneratorPreferredLength(int minLength, int maxLength, System.Random rng)
    {
        minLength = Mathf.Max(2, minLength);
        maxLength = Mathf.Max(minLength, maxLength);
        if (minLength == maxLength)
        {
            return minLength;
        }

        GeneratorAlgorithmMode algorithmMode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
        float automaticMinimumWeight = IsAdvancedGuidedMode(algorithmMode)
            ? GetAdvancedAutomaticMinimumLengthWeight(algorithmMode)
            : 0.28f;
        float minimumWeight = generatorAutoLength
            ? automaticMinimumWeight
            : Mathf.Clamp(
                generatorMinimumLengthWeight,
                GeneratorMinimumLengthWeightLimit,
                GeneratorMaximumLengthWeightLimit);
        float maximumWeight = 1f - minimumWeight;
        double totalWeight = 0d;

        for (int length = minLength; length <= maxLength; length++)
        {
            float rangePosition = (length - minLength) / (float)(maxLength - minLength);
            totalWeight += Mathf.Lerp(minimumWeight, maximumWeight, rangePosition);
        }

        double selection = rng.NextDouble() * totalWeight;
        for (int length = minLength; length <= maxLength; length++)
        {
            float rangePosition = (length - minLength) / (float)(maxLength - minLength);
            selection -= Mathf.Lerp(minimumWeight, maximumWeight, rangePosition);
            if (selection <= 0d)
            {
                return length;
            }
        }

        return maxLength;
    }

    private GeneratedLevelBuild CreateGeneratedLevelBuild(
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        int addedCount,
        int seed,
        bool usedFallback,
        HashSet<Vector2Int> zoneSet)
    {
        GeneratedLevelBuild build = new GeneratedLevelBuild
        {
            arrows = generatedArrows,
            occupied = occupied,
            addedCount = addedCount,
            seed = seed,
            usedFallback = usedFallback
        };

        ApplyGeneratedSolveProfile(build, zoneSet);
        ApplyGeneratedGeometryProfile(build);
        ApplyGeneratedSpatialDependencyProfile(build, zoneSet);
        build.complexityScore = CalculateAdvancedGuidedBuildScore(build);
        return build;
    }

    private void ApplyGeneratedSolveProfile(GeneratedLevelBuild build, HashSet<Vector2Int> zoneSet)
    {
        if (build == null)
        {
            return;
        }

        if (TryMeasureGeneratedSolveProfile(
            build.arrows,
            zoneSet,
            out int initialPlayableCount,
            out int maxPlayableCount,
            out int overTwoRouteMoveCount,
            out float averagePlayableCount)
            && TryMeasureGeneratedDependencyProfile(
                build.arrows,
                zoneSet,
                out int dependencyDepth,
                out int maxUnlockWaveCount,
                out int overTwoUnlockWaveCount,
                out int controlledBurstWaveCount,
                out int gateWaveTransitionCount,
                out int oversizedUnlockWaveCount,
                out float singleArrowWaveRatio))
        {
            build.initialPlayableCount = initialPlayableCount;
            build.maxPlayableCount = maxPlayableCount;
            build.overTwoRouteMoveCount = overTwoRouteMoveCount;
            build.averagePlayableCount = averagePlayableCount;
            build.dependencyDepth = dependencyDepth;
            build.maxUnlockWaveCount = maxUnlockWaveCount;
            build.overTwoUnlockWaveCount = overTwoUnlockWaveCount;
            build.controlledBurstWaveCount = controlledBurstWaveCount;
            build.gateWaveTransitionCount = gateWaveTransitionCount;
            build.oversizedUnlockWaveCount = oversizedUnlockWaveCount;
            build.singleArrowWaveRatio = singleArrowWaveRatio;
            ApplyGeneratedDecisionProfile(build, zoneSet);
            return;
        }

        build.initialPlayableCount = int.MaxValue;
        build.maxPlayableCount = int.MaxValue;
        build.overTwoRouteMoveCount = int.MaxValue;
        build.averagePlayableCount = float.MaxValue;
        build.dependencyDepth = -1;
        build.maxUnlockWaveCount = int.MaxValue;
        build.overTwoUnlockWaveCount = int.MaxValue;
        build.controlledBurstWaveCount = 0;
        build.gateWaveTransitionCount = 0;
        build.oversizedUnlockWaveCount = int.MaxValue;
        build.singleArrowWaveRatio = 1f;
        ResetGeneratedDecisionProfile(build);
    }

    // Measures the decisions a player actually sees. The dependency graph can be
    // acyclic and still feel trivial, so Gate Network also evaluates blocker
    // distance, choice spacing, exit-lane visibility, and whether moves unlock
    // anything before the terminal cleanup wave.
    private void ApplyGeneratedDecisionProfile(GeneratedLevelBuild build, HashSet<Vector2Int> zoneSet)
    {
        ResetGeneratedDecisionProfile(build);

        List<TestArrow> testArrows = new List<TestArrow>();
        Dictionary<Vector2Int, TestArrow> occupied = new Dictionary<Vector2Int, TestArrow>();
        if (!TryBuildGeneratedTestState(build.arrows, zoneSet, testArrows, occupied))
        {
            return;
        }

        GetGeneratedZoneBounds(zoneSet, out int minX, out int maxX, out int minY, out int maxY);
        int maximumHeadDistance = Mathf.Max(1, maxX - minX + maxY - minY);
        int clusteredDistanceThreshold = GetGeneratedDecisionChoiceDistanceThreshold(zoneSet);
        int remoteBlockerThreshold = GetGeneratedRemoteBlockerDistanceThreshold(zoneSet);

        HashSet<int> removed = new HashSet<int>();
        List<TestArrow> currentWave = new List<TestArrow>();
        int waveCount = 0;
        int multiChoiceWaveCount = 0;
        int clusteredChoiceWaveCount = 0;
        int distributedChoiceWaveCount = 0;
        float totalChoiceSeparationRatio = 0f;
        int playableAppearances = 0;
        int shortPlayableExitLanes = 0;
        int boundaryPlayableMoves = 0;
        int nonTerminalPlayableAppearances = 0;
        int zeroImpactPlayableMoves = 0;
        int blockerObservations = 0;
        int totalBlockerDistance = 0;
        int nearBlockerObservations = 0;
        int remoteBlockerObservations = 0;
        int totalWaveUnlocks = 0;

        while (removed.Count < testArrows.Count)
        {
            currentWave.Clear();
            for (int arrowIndex = 0; arrowIndex < testArrows.Count; arrowIndex++)
            {
                TestArrow arrow = testArrows[arrowIndex];
                if (removed.Contains(arrow.index))
                {
                    continue;
                }

                if (CanGeneratedArrowEscape(arrow, occupied, removed, zoneSet))
                {
                    currentWave.Add(arrow);
                    continue;
                }

                if (TryGetGeneratedFirstBlockerDistance(
                    arrow,
                    occupied,
                    removed,
                    zoneSet,
                    out int blockerDistance))
                {
                    blockerObservations++;
                    totalBlockerDistance += blockerDistance;
                    nearBlockerObservations += blockerDistance <= GeneratorDecisionExitBufferCells ? 1 : 0;
                    remoteBlockerObservations += blockerDistance >= remoteBlockerThreshold ? 1 : 0;
                }
            }

            if (currentWave.Count == 0)
            {
                ResetGeneratedDecisionProfile(build);
                return;
            }

            waveCount++;
            bool terminalWave = removed.Count + currentWave.Count >= testArrows.Count;
            HashSet<int> currentPlayableIndices = new HashSet<int>();
            foreach (TestArrow arrow in currentWave)
            {
                currentPlayableIndices.Add(arrow.index);
            }

            foreach (TestArrow arrow in currentWave)
            {
                int exitLaneLength = CountGeneratedStaticExitDepth(arrow, zoneSet);
                playableAppearances++;
                build.averagePlayableExitLane += exitLaneLength;
                shortPlayableExitLanes += exitLaneLength <= 1 ? 1 : 0;
                boundaryPlayableMoves += exitLaneLength == 0 ? 1 : 0;

                if (!terminalWave)
                {
                    nonTerminalPlayableAppearances++;
                    zeroImpactPlayableMoves += CountGeneratedNewlyPlayableAfterRemoving(
                        arrow,
                        testArrows,
                        occupied,
                        removed,
                        currentPlayableIndices,
                        zoneSet) == 0 ? 1 : 0;
                }
            }

            if (currentWave.Count > 1)
            {
                multiChoiceWaveCount++;
                int minimumHeadDistance = int.MaxValue;
                HashSet<int> regions = new HashSet<int>();
                for (int first = 0; first < currentWave.Count; first++)
                {
                    TestArrow firstArrow = currentWave[first];
                    regions.Add(GetGeneratedSpatialRegionIndex(firstArrow.head, minX, maxX, minY, maxY));
                    for (int second = first + 1; second < currentWave.Count; second++)
                    {
                        Vector2Int delta = firstArrow.head - currentWave[second].head;
                        minimumHeadDistance = Mathf.Min(
                            minimumHeadDistance,
                            Mathf.Abs(delta.x) + Mathf.Abs(delta.y));
                    }
                }

                totalChoiceSeparationRatio += minimumHeadDistance / (float)maximumHeadDistance;
                clusteredChoiceWaveCount += minimumHeadDistance < clusteredDistanceThreshold ? 1 : 0;
                distributedChoiceWaveCount += regions.Count >= Mathf.Min(currentWave.Count, 3) ? 1 : 0;
            }

            foreach (TestArrow escaped in currentWave)
            {
                removed.Add(escaped.index);
                foreach (Vector2Int cell in escaped.cells)
                {
                    occupied.Remove(cell);
                }
            }

            if (!terminalWave)
            {
                int nextPlayableCount = 0;
                for (int arrowIndex = 0; arrowIndex < testArrows.Count; arrowIndex++)
                {
                    TestArrow arrow = testArrows[arrowIndex];
                    if (!removed.Contains(arrow.index)
                        && CanGeneratedArrowEscape(arrow, occupied, removed, zoneSet))
                    {
                        nextPlayableCount++;
                    }
                }

                totalWaveUnlocks += nextPlayableCount;
            }
        }

        build.averageBlockerDistance = blockerObservations > 0
            ? totalBlockerDistance / (float)blockerObservations
            : 0f;
        build.nearBlockerRatio = blockerObservations > 0
            ? nearBlockerObservations / (float)blockerObservations
            : 0f;
        build.remoteBlockerRatio = blockerObservations > 0
            ? remoteBlockerObservations / (float)blockerObservations
            : 0f;
        build.averagePlayableExitLane = playableAppearances > 0
            ? build.averagePlayableExitLane / playableAppearances
            : 0f;
        build.shortPlayableExitLaneRatio = playableAppearances > 0
            ? shortPlayableExitLanes / (float)playableAppearances
            : 0f;
        build.boundaryPlayableRatio = playableAppearances > 0
            ? boundaryPlayableMoves / (float)playableAppearances
            : 0f;
        build.zeroImpactPlayableRatio = nonTerminalPlayableAppearances > 0
            ? zeroImpactPlayableMoves / (float)nonTerminalPlayableAppearances
            : 0f;
        build.clusteredChoiceWaveRatio = multiChoiceWaveCount > 0
            ? clusteredChoiceWaveCount / (float)multiChoiceWaveCount
            : 0f;
        build.distributedChoiceWaveRatio = multiChoiceWaveCount > 0
            ? distributedChoiceWaveCount / (float)multiChoiceWaveCount
            : 1f;
        build.averageChoiceSeparationRatio = multiChoiceWaveCount > 0
            ? totalChoiceSeparationRatio / multiChoiceWaveCount
            : 1f;
        build.averageWaveUnlockCount = waveCount > 1
            ? totalWaveUnlocks / (float)(waveCount - 1)
            : 0f;
    }

    private static void ResetGeneratedDecisionProfile(GeneratedLevelBuild build)
    {
        build.averageBlockerDistance = 0f;
        build.nearBlockerRatio = 1f;
        build.remoteBlockerRatio = 0f;
        build.averagePlayableExitLane = 0f;
        build.shortPlayableExitLaneRatio = 1f;
        build.boundaryPlayableRatio = 1f;
        build.zeroImpactPlayableRatio = 1f;
        build.clusteredChoiceWaveRatio = 1f;
        build.distributedChoiceWaveRatio = 0f;
        build.averageChoiceSeparationRatio = 0f;
        build.averageWaveUnlockCount = 0f;
    }

    private bool TryGetGeneratedFirstBlockerDistance(
        TestArrow arrow,
        Dictionary<Vector2Int, TestArrow> occupied,
        HashSet<int> removed,
        HashSet<Vector2Int> zoneSet,
        out int blockerDistance)
    {
        blockerDistance = 0;
        Vector2Int cell = arrow.head + arrow.exitDirection;
        while (IsInsideGridBounds(cell))
        {
            blockerDistance++;
            if (occupied.TryGetValue(cell, out TestArrow blocker)
                && !removed.Contains(blocker.index))
            {
                return true;
            }

            cell += arrow.exitDirection;
        }

        blockerDistance = 0;
        return false;
    }

    private int CountGeneratedNewlyPlayableAfterRemoving(
        TestArrow selected,
        List<TestArrow> testArrows,
        Dictionary<Vector2Int, TestArrow> occupied,
        HashSet<int> removed,
        HashSet<int> currentlyPlayable,
        HashSet<Vector2Int> zoneSet)
    {
        Dictionary<Vector2Int, TestArrow> nextOccupied = new Dictionary<Vector2Int, TestArrow>(occupied);
        foreach (Vector2Int cell in selected.cells)
        {
            nextOccupied.Remove(cell);
        }

        HashSet<int> nextRemoved = new HashSet<int>(removed) { selected.index };
        int newlyPlayable = 0;
        for (int arrowIndex = 0; arrowIndex < testArrows.Count; arrowIndex++)
        {
            TestArrow arrow = testArrows[arrowIndex];
            if (!nextRemoved.Contains(arrow.index)
                && !currentlyPlayable.Contains(arrow.index)
                && CanGeneratedArrowEscape(arrow, nextOccupied, nextRemoved, zoneSet))
            {
                newlyPlayable++;
            }
        }

        return newlyPlayable;
    }

    // Keeps a full board intact while narrowing an overly broad opening. Reversing
    // an arrow preserves its occupied cells and geometry, but gives it a new head
    // and exit ray that can participate in a more deliberate dependency chain.
    private bool ImproveGeneratedOpeningRouteProfile(
        GeneratedLevelBuild build,
        HashSet<Vector2Int> zoneSet,
        int maxLength)
    {
        if (build == null
            || build.arrows == null
            || build.initialPlayableCount <= GeneratorTargetMaxPlayableRoutes)
        {
            return false;
        }

        bool changed = false;
        int guard = Mathf.Min(4, build.arrows.Count);
        while (build.initialPlayableCount > GeneratorTargetMaxPlayableRoutes && guard-- > 0)
        {
            int bestArrowIndex = -1;
            int bestInitialPlayableCount = build.initialPlayableCount;
            int bestMaxPlayableCount = int.MaxValue;
            int bestOverTwoRouteMoveCount = int.MaxValue;
            float bestAveragePlayableCount = float.MaxValue;

            for (int arrowIndex = 0; arrowIndex < build.arrows.Count; arrowIndex++)
            {
                RuntimeArrowDraft arrow = build.arrows[arrowIndex];
                if (arrow == null || arrow.points.Count < 2)
                {
                    continue;
                }

                arrow.points.Reverse();
                bool validDirection = !PathArrowUtility.TryFindOwnExitBlock(
                    arrow.points,
                    width,
                    height,
                    zoneSet,
                    out _);
                int initialPlayableCount = int.MaxValue;
                int maxPlayableCount = int.MaxValue;
                int overTwoRouteMoveCount = int.MaxValue;
                float averagePlayableCount = float.MaxValue;
                bool remainsSolvable = validDirection
                    && TryMeasureGeneratedSolveProfile(
                        build.arrows,
                        zoneSet,
                        out initialPlayableCount,
                        out maxPlayableCount,
                        out overTwoRouteMoveCount,
                        out averagePlayableCount);
                arrow.points.Reverse();

                if (!remainsSolvable
                    || initialPlayableCount >= build.initialPlayableCount
                    || maxPlayableCount > build.maxPlayableCount
                    || overTwoRouteMoveCount > build.overTwoRouteMoveCount + 2)
                {
                    continue;
                }

                bool isBetter = bestArrowIndex < 0
                    || initialPlayableCount < bestInitialPlayableCount
                    || (initialPlayableCount == bestInitialPlayableCount
                        && (overTwoRouteMoveCount < bestOverTwoRouteMoveCount
                            || (overTwoRouteMoveCount == bestOverTwoRouteMoveCount
                                && (maxPlayableCount < bestMaxPlayableCount
                                    || (maxPlayableCount == bestMaxPlayableCount
                                        && averagePlayableCount < bestAveragePlayableCount)))));
                if (!isBetter)
                {
                    continue;
                }

                bestArrowIndex = arrowIndex;
                bestInitialPlayableCount = initialPlayableCount;
                bestMaxPlayableCount = maxPlayableCount;
                bestOverTwoRouteMoveCount = overTwoRouteMoveCount;
                bestAveragePlayableCount = averagePlayableCount;
            }

            if (bestArrowIndex < 0)
            {
                if (!TryImproveGeneratedOpeningByTailMerge(build, zoneSet, maxLength))
                {
                    break;
                }

                ApplyGeneratedSolveProfile(build, zoneSet);
                changed = true;
                continue;
            }

            build.arrows[bestArrowIndex].points.Reverse();
            ApplyGeneratedSolveProfile(build, zoneSet);
            changed = true;
        }

        return changed;
    }

    private bool TryImproveGeneratedOpeningByTailMerge(
        GeneratedLevelBuild build,
        HashSet<Vector2Int> zoneSet,
        int maxLength)
    {
        RuntimeArrowDraft bestMergedArrow = null;
        int bestSourceIndex = -1;
        int bestVictimIndex = -1;
        int bestInitialPlayableCount = build.initialPlayableCount;
        int bestMaxPlayableCount = int.MaxValue;
        int bestOverTwoRouteMoveCount = int.MaxValue;
        float bestAveragePlayableCount = float.MaxValue;

        for (int victimIndex = 0; victimIndex < build.arrows.Count; victimIndex++)
        {
            if (!CanGeneratedArrowEscapeThroughOccupied(build.arrows[victimIndex], build.occupied, zoneSet))
            {
                continue;
            }

            for (int sourceIndex = 0; sourceIndex < build.arrows.Count; sourceIndex++)
            {
                if (sourceIndex == victimIndex
                    || !TryBuildGeneratedTailMerge(
                        build.arrows[sourceIndex],
                        build.arrows[victimIndex],
                        zoneSet,
                        zoneSet,
                        maxLength,
                        out RuntimeArrowDraft mergedArrow,
                        out _,
                        out _,
                        out _))
                {
                    continue;
                }

                List<RuntimeArrowDraft> testArrows = new List<RuntimeArrowDraft>(build.arrows);
                testArrows[sourceIndex] = mergedArrow;
                testArrows.RemoveAt(victimIndex);
                if (!TryMeasureGeneratedSolveProfile(
                    testArrows,
                    zoneSet,
                    out int initialPlayableCount,
                    out int maxPlayableCount,
                    out int overTwoRouteMoveCount,
                    out float averagePlayableCount)
                    || initialPlayableCount >= build.initialPlayableCount
                    || maxPlayableCount > build.maxPlayableCount
                    || overTwoRouteMoveCount > build.overTwoRouteMoveCount + 2)
                {
                    continue;
                }

                bool isBetter = bestMergedArrow == null
                    || initialPlayableCount < bestInitialPlayableCount
                    || (initialPlayableCount == bestInitialPlayableCount
                        && (overTwoRouteMoveCount < bestOverTwoRouteMoveCount
                            || (overTwoRouteMoveCount == bestOverTwoRouteMoveCount
                                && (maxPlayableCount < bestMaxPlayableCount
                                    || (maxPlayableCount == bestMaxPlayableCount
                                        && averagePlayableCount < bestAveragePlayableCount)))));
                if (!isBetter)
                {
                    continue;
                }

                bestMergedArrow = mergedArrow;
                bestSourceIndex = sourceIndex;
                bestVictimIndex = victimIndex;
                bestInitialPlayableCount = initialPlayableCount;
                bestMaxPlayableCount = maxPlayableCount;
                bestOverTwoRouteMoveCount = overTwoRouteMoveCount;
                bestAveragePlayableCount = averagePlayableCount;
            }
        }

        if (bestMergedArrow == null)
        {
            return false;
        }

        build.arrows[bestSourceIndex] = bestMergedArrow;
        build.arrows.RemoveAt(bestVictimIndex);
        for (int i = 0; i < build.arrows.Count; i++)
        {
            build.arrows[i].id = $"Arrow {i + 1}";
        }

        return true;
    }

    // DX preserves the completed board geometry and tests alternate arrow heads.
    // Reversing a path changes its dependency rays without changing fill, length,
    // or turns, so the polish can improve puzzle flow without flattening the art.
    private bool ImproveComplexGuidedDxSolveFlow(
        GeneratedLevelBuild build,
        HashSet<Vector2Int> zoneSet,
        int startingArrowCount,
        DateTime polishStart,
        int timeBudgetMs)
    {
        if (build == null
            || build.arrows == null
            || build.arrows.Count - startingArrowCount < 2
            || build.dependencyDepth < 0)
        {
            return false;
        }

        bool changed = false;
        int acceptedChangeLimit = Mathf.Clamp((build.arrows.Count - startingArrowCount) / 30, 2, 6);
        System.Random rng = new System.Random(build.seed ^ 0x44d9582b);

        for (int pass = 0;
            pass < acceptedChangeLimit && !IsGeneratorTimeExpired(polishStart, timeBudgetMs);
            pass++)
        {
            float baselineScore = build.complexityScore;
            int baselineInitialRoutes = build.initialPlayableCount;
            int baselineMaxRoutes = build.maxPlayableCount;
            int baselineOverTwoMoves = build.overTwoRouteMoveCount;
            float baselineRemoteRatio = build.remoteBlockerRatio;
            float baselineZeroImpactRatio = build.zeroImpactPlayableRatio;
            float baselineRegionTransitionRatio = build.solveRegionTransitionRatio;
            float baselineHorizontalJumpDistance = build.averageSolveHorizontalJumpDistance;
            float baselineLongHorizontalRatio = build.solveLongHorizontalTransitionRatio;
            float baselineLeftToRightProgress = build.solveLeftToRightProgress;
            float baselineAreaOrderScore = build.solveHorizontalAreaOrderScore;
            float baselineForwardHandoffRatio = build.solveForwardAreaHandoffRatio;
            float baselineBackwardHandoffRatio = build.solveBackwardAreaHandoffRatio;
            float baselineParticipationRatio = build.dependencyParticipationRatio;
            int baselineInwardRun = build.longestInwardSolveRun;

            List<int> candidateOrder = new List<int>();
            for (int arrowIndex = startingArrowCount; arrowIndex < build.arrows.Count; arrowIndex++)
            {
                candidateOrder.Add(arrowIndex);
            }

            ShuffleList(candidateOrder, rng);
            int evaluationLimit = Mathf.Min(candidateOrder.Count, 64);
            int bestArrowIndex = -1;
            float bestScore = baselineScore + 0.5f;

            for (int candidateIndex = 0;
                candidateIndex < evaluationLimit && !IsGeneratorTimeExpired(polishStart, timeBudgetMs);
                candidateIndex++)
            {
                int arrowIndex = candidateOrder[candidateIndex];
                RuntimeArrowDraft arrow = build.arrows[arrowIndex];
                if (arrow == null || arrow.points.Count < 2)
                {
                    continue;
                }

                arrow.points.Reverse();
                bool validDirection = !PathArrowUtility.TryFindOwnExitBlock(
                    arrow.points,
                    width,
                    height,
                    zoneSet,
                    out _);
                GeneratedLevelBuild testBuild = null;
                if (validDirection && CanGeneratedLevelSolve(build.arrows, zoneSet))
                {
                    testBuild = CreateGeneratedLevelBuild(
                        build.arrows,
                        build.occupied,
                        build.addedCount,
                        build.seed,
                        build.usedFallback,
                        zoneSet);
                }

                arrow.points.Reverse();
                if (testBuild == null
                    || testBuild.initialPlayableCount > Mathf.Max(GeneratorTargetMaxPlayableRoutes, baselineInitialRoutes)
                    || testBuild.maxPlayableCount > Mathf.Max(GeneratorGateBurstRouteCeiling, baselineMaxRoutes)
                    || testBuild.overTwoRouteMoveCount > baselineOverTwoMoves + 1)
                {
                    continue;
                }

                bool improvesDecisionQuality = testBuild.remoteBlockerRatio > baselineRemoteRatio + 0.001f
                    || testBuild.zeroImpactPlayableRatio < baselineZeroImpactRatio - 0.001f
                    || testBuild.solveRegionTransitionRatio > baselineRegionTransitionRatio + 0.001f
                    || testBuild.averageSolveHorizontalJumpDistance > baselineHorizontalJumpDistance + 0.001f
                    || testBuild.solveLongHorizontalTransitionRatio > baselineLongHorizontalRatio + 0.001f
                    || testBuild.solveLeftToRightProgress > baselineLeftToRightProgress + 0.001f
                    || testBuild.solveHorizontalAreaOrderScore > baselineAreaOrderScore + 0.001f
                    || testBuild.solveForwardAreaHandoffRatio > baselineForwardHandoffRatio + 0.001f
                    || testBuild.solveBackwardAreaHandoffRatio < baselineBackwardHandoffRatio - 0.001f
                    || testBuild.dependencyParticipationRatio > baselineParticipationRatio + 0.001f
                    || testBuild.longestInwardSolveRun < baselineInwardRun;
                if (improvesDecisionQuality && testBuild.complexityScore > bestScore)
                {
                    bestScore = testBuild.complexityScore;
                    bestArrowIndex = arrowIndex;
                }
            }

            if (bestArrowIndex < 0)
            {
                break;
            }

            build.arrows[bestArrowIndex].points.Reverse();
            ApplyGeneratedSolveProfile(build, zoneSet);
            ApplyGeneratedSpatialDependencyProfile(build, zoneSet);
            build.complexityScore = CalculateAdvancedGuidedBuildScore(build);
            changed = true;
        }

        return changed;
    }

    private void ApplyGeneratedGeometryProfile(GeneratedLevelBuild build)
    {
        if (build == null || build.arrows == null || build.arrows.Count == 0)
        {
            return;
        }

        int totalLength = 0;
        int totalTurns = 0;
        int multiTurnArrowCount = 0;
        HashSet<int> distinctTurnCounts = new HashSet<int>();
        List<Vector2Int> pathCells = new List<Vector2Int>();

        for (int i = 0; i < build.arrows.Count; i++)
        {
            RuntimeArrowDraft arrow = build.arrows[i];
            if (ExpandGeneratedPathCells(arrow.points, pathCells))
            {
                totalLength += pathCells.Count;
            }

            int turnCount = Mathf.Max(0, arrow.points.Count - 2);
            totalTurns += turnCount;
            distinctTurnCounts.Add(turnCount);
            if (turnCount >= 2)
            {
                multiTurnArrowCount++;
            }
        }

        build.averageArrowLength = totalLength / (float)build.arrows.Count;
        build.averageTurnCount = totalTurns / (float)build.arrows.Count;
        build.multiTurnArrowRatio = multiTurnArrowCount / (float)build.arrows.Count;
        build.distinctTurnCount = distinctTurnCounts.Count;
    }

    private void ApplyGeneratedSpatialDependencyProfile(GeneratedLevelBuild build, HashSet<Vector2Int> zoneSet)
    {
        if (build == null || build.arrows == null || build.arrows.Count == 0 || zoneSet == null || zoneSet.Count == 0)
        {
            return;
        }

        List<TestArrow> testArrows = new List<TestArrow>();
        Dictionary<Vector2Int, TestArrow> occupied = new Dictionary<Vector2Int, TestArrow>();
        if (!TryBuildGeneratedTestState(build.arrows, zoneSet, testArrows, occupied))
        {
            return;
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;
        foreach (Vector2Int cell in zoneSet)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        Vector2[] centers = new Vector2[testArrows.Count];
        for (int i = 0; i < testArrows.Count; i++)
        {
            centers[i] = GetGeneratedTestArrowCenter(testArrows[i]);
        }

        int edgeCount = 0;
        float totalDistance = 0f;
        int crossRegionEdges = 0;
        int crossColumnEdges = 0;
        int crossRowEdges = 0;
        int[] directionCounts = new int[4];
        Dictionary<int, int> anchorDependents = new Dictionary<int, int>();
        Dictionary<int, HashSet<int>> anchorSourceRegions = new Dictionary<int, HashSet<int>>();
        HashSet<int> dependencyParticipants = new HashSet<int>();

        for (int i = 0; i < testArrows.Count; i++)
        {
            TestArrow arrow = testArrows[i];
            Vector2Int checkPosition = arrow.head + arrow.exitDirection;
            while (IsInsideGridBounds(checkPosition))
            {
                if (occupied.TryGetValue(checkPosition, out TestArrow blocker) && blocker.index != arrow.index)
                {
                    edgeCount++;
                    dependencyParticipants.Add(arrow.index);
                    dependencyParticipants.Add(blocker.index);
                    totalDistance += Mathf.Abs(centers[blocker.index].x - centers[arrow.index].x)
                        + Mathf.Abs(centers[blocker.index].y - centers[arrow.index].y);

                    int directionIndex = GetGeneratedDirectionIndex(arrow.exitDirection);
                    if (directionIndex >= 0)
                    {
                        directionCounts[directionIndex]++;
                    }

                    int sourceRegion = GetGeneratedSpatialRegionIndex(centers[arrow.index], minX, maxX, minY, maxY);
                    int blockerRegion = GetGeneratedSpatialRegionIndex(centers[blocker.index], minX, maxX, minY, maxY);
                    if (sourceRegion != blockerRegion)
                    {
                        crossRegionEdges++;
                    }

                    if (sourceRegion % 3 != blockerRegion % 3)
                    {
                        crossColumnEdges++;
                    }

                    if (sourceRegion / 3 != blockerRegion / 3)
                    {
                        crossRowEdges++;
                    }

                    anchorDependents.TryGetValue(blocker.index, out int dependentCount);
                    anchorDependents[blocker.index] = dependentCount + 1;
                    if (!anchorSourceRegions.TryGetValue(blocker.index, out HashSet<int> sourceRegions))
                    {
                        sourceRegions = new HashSet<int>();
                        anchorSourceRegions[blocker.index] = sourceRegions;
                    }

                    sourceRegions.Add(sourceRegion);
                    break;
                }

                checkPosition += arrow.exitDirection;
            }
        }

        if (edgeCount == 0)
        {
            build.dependencyParticipationRatio = 0f;
            build.isolatedDependencyArrowCount = testArrows.Count;
            return;
        }

        int largestDirectionCount = 0;
        for (int i = 0; i < directionCounts.Length; i++)
        {
            largestDirectionCount = Mathf.Max(largestDirectionCount, directionCounts[i]);
        }

        int horizontalEdges = directionCounts[0] + directionCounts[1];
        int verticalEdges = directionCounts[2] + directionCounts[3];
        int multiDependentAnchors = 0;
        int multiRegionAnchors = 0;
        int excessAnchorDependents = 0;
        int controlledGateCount = 0;
        int controlledGateEdges = 0;
        int maximumGateDependentCount = 0;
        bool gateNetworkMode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex
            == GeneratorAlgorithmMode.LockstepWeave;
        foreach (KeyValuePair<int, int> pair in anchorDependents)
        {
            maximumGateDependentCount = Mathf.Max(maximumGateDependentCount, pair.Value);
            if (pair.Value >= 2)
            {
                multiDependentAnchors++;
            }

            if (pair.Value >= GeneratorGateMinimumDependents
                && pair.Value <= GeneratorGateMaximumDependents)
            {
                controlledGateCount++;
                controlledGateEdges += pair.Value;
            }

            int excessiveDependentThreshold = gateNetworkMode ? GeneratorGateMaximumDependents : 2;
            excessAnchorDependents += Mathf.Max(0, pair.Value - excessiveDependentThreshold);

            if (anchorSourceRegions.TryGetValue(pair.Key, out HashSet<int> regions) && regions.Count >= 2)
            {
                multiRegionAnchors++;
            }
        }

        build.dependencyEdgeCount = edgeCount;
        build.dependencyParticipationRatio = dependencyParticipants.Count / (float)testArrows.Count;
        build.isolatedDependencyArrowCount = testArrows.Count - dependencyParticipants.Count;
        build.averageDependencyDistance = totalDistance / edgeCount;
        build.crossRegionDependencyRatio = crossRegionEdges / (float)edgeCount;
        build.crossColumnDependencyRatio = crossColumnEdges / (float)edgeCount;
        build.crossRowDependencyRatio = crossRowEdges / (float)edgeCount;
        build.dependencyDirectionBalance = Mathf.Clamp01((1f - largestDirectionCount / (float)edgeCount) / 0.75f);
        build.dependencyAxisBalance = 1f - Mathf.Abs(horizontalEdges - verticalEdges) / (float)edgeCount;
        build.multiDependentAnchorCount = multiDependentAnchors;
        build.multiRegionAnchorCount = multiRegionAnchors;
        build.excessAnchorDependentCount = excessAnchorDependents;
        build.controlledGateCount = controlledGateCount;
        build.maximumGateDependentCount = maximumGateDependentCount;
        build.controlledGateEdgeRatio = controlledGateEdges / (float)edgeCount;
        build.spatialDependencyScore = build.averageDependencyDistance * 4f
            + build.crossRegionDependencyRatio * 80f
            + build.crossColumnDependencyRatio * 70f
            + build.crossRowDependencyRatio * 35f
            + build.dependencyDirectionBalance * 80f
            + build.dependencyAxisBalance * 60f
            + multiDependentAnchors * (gateNetworkMode ? 18f : 6f)
            + multiRegionAnchors * (gateNetworkMode ? 34f : 10f)
            + controlledGateCount * (gateNetworkMode ? 42f : 0f)
            + build.controlledGateEdgeRatio * (gateNetworkMode ? 120f : 0f)
            - excessAnchorDependents * (gateNetworkMode ? 18f : 34f);

        ApplyGeneratedSolveFlowProfile(build, testArrows, occupied, centers, zoneSet, minX, maxX, minY, maxY);
    }

    private void ApplyGeneratedSolveFlowProfile(
        GeneratedLevelBuild build,
        List<TestArrow> testArrows,
        Dictionary<Vector2Int, TestArrow> initialOccupied,
        Vector2[] centers,
        HashSet<Vector2Int> zoneSet,
        int minX,
        int maxX,
        int minY,
        int maxY)
    {
        Dictionary<Vector2Int, TestArrow> occupied = new Dictionary<Vector2Int, TestArrow>(initialOccupied);
        HashSet<int> removed = new HashSet<int>();
        bool hasPreviousMove = false;
        Vector2 previousCenter = Vector2.zero;
        int previousRegion = -1;
        int previousExitDepth = 0;
        int currentInwardRun = 1;
        int longestInwardRun = testArrows.Count > 0 ? 1 : 0;
        int transitionCount = 0;
        int regionTransitions = 0;
        int crossColumnTransitions = 0;
        int outwardResets = 0;
        float totalJumpDistance = 0f;
        float totalHorizontalJumpDistance = 0f;
        int longHorizontalTransitions = 0;
        float earlySolveX = 0f;
        int earlySolveCount = 0;
        float lateSolveX = 0f;
        int lateSolveCount = 0;
        float longHorizontalThreshold = Mathf.Max(2f, (maxX - minX + 1) / 3f);
        const int horizontalAreaCount = 4;
        float[] areaSolveOrderTotals = new float[horizontalAreaCount];
        int[] areaSolveCounts = new int[horizontalAreaCount];
        int previousHorizontalArea = -1;
        int sameAreaTransitions = 0;
        int forwardAreaHandoffs = 0;
        int backwardAreaHandoffs = 0;
        int currentSameAreaRun = 0;
        int longestSameAreaRun = 0;

        while (removed.Count < testArrows.Count)
        {
            TestArrow selected = null;
            for (int i = 0; i < testArrows.Count; i++)
            {
                TestArrow candidate = testArrows[i];
                if (removed.Contains(candidate.index)
                    || !CanGeneratedArrowEscape(candidate, occupied, removed, zoneSet))
                {
                    continue;
                }

                // Newer arrows are the intended reverse-placement route. Following
                // that route exposes whether generation is merely peeling layers.
                if (selected == null || candidate.index > selected.index)
                {
                    selected = candidate;
                }
            }

            if (selected == null)
            {
                break;
            }

            Vector2 center = centers[selected.index];
            int region = GetGeneratedSpatialRegionIndex(center, minX, maxX, minY, maxY);
            float normalizedCenterX = (center.x - minX) / Mathf.Max(1f, maxX - minX + 1f);
            int horizontalArea = Mathf.Clamp(
                Mathf.FloorToInt(normalizedCenterX * horizontalAreaCount),
                0,
                horizontalAreaCount - 1);
            areaSolveOrderTotals[horizontalArea] += removed.Count;
            areaSolveCounts[horizontalArea]++;
            int exitDepth = CountGeneratedStaticExitDepth(selected, zoneSet);
            float solveProgress = testArrows.Count > 1
                ? removed.Count / (float)(testArrows.Count - 1)
                : 0f;
            if (solveProgress <= 0.33f)
            {
                earlySolveX += center.x;
                earlySolveCount++;
            }
            else if (solveProgress >= 0.67f)
            {
                lateSolveX += center.x;
                lateSolveCount++;
            }

            if (hasPreviousMove)
            {
                transitionCount++;
                totalJumpDistance += Mathf.Abs(center.x - previousCenter.x) + Mathf.Abs(center.y - previousCenter.y);
                float horizontalJump = Mathf.Abs(center.x - previousCenter.x);
                totalHorizontalJumpDistance += horizontalJump;
                if (horizontalJump >= longHorizontalThreshold)
                {
                    longHorizontalTransitions++;
                }

                if (horizontalArea == previousHorizontalArea)
                {
                    sameAreaTransitions++;
                    currentSameAreaRun++;
                }
                else
                {
                    if (horizontalArea > previousHorizontalArea)
                    {
                        forwardAreaHandoffs++;
                    }
                    else
                    {
                        backwardAreaHandoffs++;
                    }

                    currentSameAreaRun = 1;
                }

                longestSameAreaRun = Mathf.Max(longestSameAreaRun, currentSameAreaRun);
                if (region != previousRegion)
                {
                    regionTransitions++;
                }

                if (region % 3 != previousRegion % 3)
                {
                    crossColumnTransitions++;
                }

                if (exitDepth >= previousExitDepth)
                {
                    currentInwardRun++;
                }
                else
                {
                    currentInwardRun = 1;
                    outwardResets++;
                }

                longestInwardRun = Mathf.Max(longestInwardRun, currentInwardRun);
            }

            hasPreviousMove = true;
            previousCenter = center;
            previousRegion = region;
            previousHorizontalArea = horizontalArea;
            previousExitDepth = exitDepth;
            removed.Add(selected.index);
            foreach (Vector2Int cell in selected.cells)
            {
                occupied.Remove(cell);
            }
        }

        build.solveTransitionCount = transitionCount;
        build.averageSolveJumpDistance = transitionCount > 0 ? totalJumpDistance / transitionCount : 0f;
        build.averageSolveHorizontalJumpDistance = transitionCount > 0
            ? totalHorizontalJumpDistance / transitionCount
            : 0f;
        build.solveLongHorizontalTransitionRatio = transitionCount > 0
            ? longHorizontalTransitions / (float)transitionCount
            : 0f;
        float earlyAverageX = earlySolveCount > 0 ? earlySolveX / earlySolveCount : minX;
        float lateAverageX = lateSolveCount > 0 ? lateSolveX / lateSolveCount : earlyAverageX;
        build.solveLeftToRightProgress = (lateAverageX - earlyAverageX) / Mathf.Max(1f, maxX - minX);
        int areaHandoffCount = forwardAreaHandoffs + backwardAreaHandoffs;
        build.solveForwardAreaHandoffRatio = areaHandoffCount > 0
            ? forwardAreaHandoffs / (float)areaHandoffCount
            : 0f;
        build.solveBackwardAreaHandoffRatio = areaHandoffCount > 0
            ? backwardAreaHandoffs / (float)areaHandoffCount
            : 0f;
        build.solveSameAreaTransitionRatio = transitionCount > 0
            ? sameAreaTransitions / (float)transitionCount
            : 0f;
        build.longestSameAreaSolveRun = longestSameAreaRun;

        int visitedAreaCount = 0;
        float orderedAreaPairScore = 0f;
        int orderedAreaPairCount = 0;
        int previousPopulatedArea = -1;
        float previousAreaAverageOrder = 0f;
        for (int area = 0; area < horizontalAreaCount; area++)
        {
            if (areaSolveCounts[area] <= 0)
            {
                continue;
            }

            visitedAreaCount++;
            float averageOrder = areaSolveOrderTotals[area] / areaSolveCounts[area];
            if (previousPopulatedArea >= 0)
            {
                float normalizedOrderGap = (averageOrder - previousAreaAverageOrder)
                    / Mathf.Max(1f, testArrows.Count - 1f);
                orderedAreaPairScore += Mathf.InverseLerp(-0.12f, 0.12f, normalizedOrderGap);
                orderedAreaPairCount++;
            }

            previousPopulatedArea = area;
            previousAreaAverageOrder = averageOrder;
        }

        build.solveHorizontalAreaCoverage = visitedAreaCount / (float)horizontalAreaCount;
        build.solveHorizontalAreaOrderScore = orderedAreaPairCount > 0
            ? orderedAreaPairScore / orderedAreaPairCount
            : 0f;
        build.solveRegionTransitionRatio = transitionCount > 0 ? regionTransitions / (float)transitionCount : 0f;
        build.solveCrossColumnTransitionRatio = transitionCount > 0 ? crossColumnTransitions / (float)transitionCount : 0f;
        build.solveOutwardResetRatio = transitionCount > 0 ? outwardResets / (float)transitionCount : 0f;
        build.longestInwardSolveRun = longestInwardRun;
        build.spatialDependencyScore += build.averageSolveJumpDistance * 3f
            + build.averageSolveHorizontalJumpDistance * 2f
            + build.solveLongHorizontalTransitionRatio * 45f
            + build.solveLeftToRightProgress * 35f
            + build.solveHorizontalAreaOrderScore * 70f
            + build.solveForwardAreaHandoffRatio * 45f
            - build.solveBackwardAreaHandoffRatio * 55f
            + build.solveRegionTransitionRatio * 70f
            + build.solveCrossColumnTransitionRatio * 85f
            + build.solveOutwardResetRatio * 55f
            - build.longestInwardSolveRun * 7f;
    }

    private int CountGeneratedStaticExitDepth(TestArrow arrow, HashSet<Vector2Int> zoneSet)
    {
        int depth = 0;
        Vector2Int cell = arrow.head + arrow.exitDirection;
        while (IsInsideGridBounds(cell))
        {
            depth++;
            cell += arrow.exitDirection;
        }

        return depth;
    }

    private static Vector2 GetGeneratedTestArrowCenter(TestArrow arrow)
    {
        Vector2 center = Vector2.zero;
        foreach (Vector2Int cell in arrow.cells)
        {
            center += (Vector2)cell;
        }

        return arrow.cells.Count > 0 ? center / arrow.cells.Count : (Vector2)arrow.head;
    }

    private static int GetGeneratedSpatialRegionIndex(
        Vector2 center,
        int minX,
        int maxX,
        int minY,
        int maxY)
    {
        float normalizedX = (center.x - minX) / Mathf.Max(1f, maxX - minX + 1f);
        float normalizedY = (center.y - minY) / Mathf.Max(1f, maxY - minY + 1f);
        int regionX = Mathf.Clamp(Mathf.FloorToInt(normalizedX * 3f), 0, 2);
        int regionY = Mathf.Clamp(Mathf.FloorToInt(normalizedY * 3f), 0, 2);
        return regionY * 3 + regionX;
    }

    private float CalculateAdvancedGuidedBuildScore(GeneratedLevelBuild build)
    {
        if (build == null || build.dependencyDepth < 0)
        {
            return float.MinValue;
        }

        GeneratorAlgorithmMode mode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
        float strength = Mathf.InverseLerp(25f, 100f, generatorComplexityPercent);
        float score;

        switch (mode)
        {
            case GeneratorAlgorithmMode.ComplexGuidedDxFlow:
                // Flow keeps DX's dependency quality, then ranks boards by whether
                // their intended route makes distant horizontal hand-offs and
                // gradually advances from the left side toward the right.
                score = build.dependencyDepth * Mathf.Lerp(32f, 48f, strength);
                score -= build.maxUnlockWaveCount * Mathf.Lerp(16f, 24f, strength);
                score -= build.overTwoUnlockWaveCount * Mathf.Lerp(26f, 40f, strength);
                score -= build.maxPlayableCount * Mathf.Lerp(14f, 22f, strength);
                score -= build.overTwoRouteMoveCount * Mathf.Lerp(12f, 20f, strength);
                score -= build.averagePlayableCount * Mathf.Lerp(10f, 16f, strength);

                score += build.averageArrowLength * Mathf.Lerp(2.4f, 3.7f, strength);
                score += build.averageTurnCount * Mathf.Lerp(8f, 14f, strength);
                score += build.multiTurnArrowRatio * Mathf.Lerp(38f, 62f, strength);
                score += build.distinctTurnCount * Mathf.Lerp(7f, 12f, strength);

                score += build.dependencyParticipationRatio * Mathf.Lerp(240f, 400f, strength);
                score -= build.isolatedDependencyArrowCount * Mathf.Lerp(9f, 16f, strength);
                score += build.averageBlockerDistance * Mathf.Lerp(17f, 27f, strength);
                score += build.remoteBlockerRatio * Mathf.Lerp(200f, 330f, strength);
                score -= build.nearBlockerRatio * Mathf.Lerp(220f, 360f, strength);
                score -= build.zeroImpactPlayableRatio * Mathf.Lerp(280f, 450f, strength);
                score -= build.clusteredChoiceWaveRatio * Mathf.Lerp(230f, 380f, strength);
                score += build.distributedChoiceWaveRatio * Mathf.Lerp(130f, 225f, strength);
                score += build.averageChoiceSeparationRatio * Mathf.Lerp(280f, 460f, strength);
                score -= build.boundaryPlayableRatio * Mathf.Lerp(95f, 170f, strength);
                score -= build.shortPlayableExitLaneRatio * Mathf.Lerp(75f, 140f, strength);

                score += build.averageSolveJumpDistance * Mathf.Lerp(7f, 12f, strength);
                score += build.averageSolveHorizontalJumpDistance * Mathf.Lerp(12f, 22f, strength);
                score += build.solveLongHorizontalTransitionRatio * Mathf.Lerp(80f, 140f, strength);
                score += build.solveLeftToRightProgress * Mathf.Lerp(360f, 620f, strength);
                score += build.solveHorizontalAreaOrderScore * Mathf.Lerp(420f, 700f, strength);
                score += build.solveHorizontalAreaCoverage * Mathf.Lerp(120f, 200f, strength);
                score += build.solveForwardAreaHandoffRatio * Mathf.Lerp(240f, 400f, strength);
                score -= build.solveBackwardAreaHandoffRatio * Mathf.Lerp(360f, 600f, strength);

                // A useful regional flow contains short local phases followed by a
                // deliberate hand-off. Penalize both constant hopping and spending
                // nearly the entire solve in one horizontal area.
                score -= Mathf.Abs(build.solveSameAreaTransitionRatio - 0.68f)
                    * Mathf.Lerp(180f, 300f, strength);
                score -= Mathf.Max(0, build.longestSameAreaSolveRun - Mathf.CeilToInt(build.addedCount * 0.42f))
                    * Mathf.Lerp(12f, 20f, strength);
                score += build.solveRegionTransitionRatio * Mathf.Lerp(145f, 240f, strength);
                score += build.solveCrossColumnTransitionRatio * Mathf.Lerp(170f, 290f, strength);
                score += build.solveOutwardResetRatio * Mathf.Lerp(75f, 130f, strength);
                score -= build.longestInwardSolveRun * Mathf.Lerp(25f, 42f, strength);
                break;
            case GeneratorAlgorithmMode.ComplexGuidedDx:
                // DX ranks the finished puzzle, not just individual placements.
                // Shape quality remains important, but arrows earn most of their
                // value by creating remote, geographically varied decisions.
                score = build.dependencyDepth * Mathf.Lerp(30f, 46f, strength);
                score -= build.maxUnlockWaveCount * Mathf.Lerp(16f, 24f, strength);
                score -= build.overTwoUnlockWaveCount * Mathf.Lerp(24f, 38f, strength);
                score -= build.maxPlayableCount * Mathf.Lerp(13f, 21f, strength);
                score -= build.overTwoRouteMoveCount * Mathf.Lerp(11f, 18f, strength);
                score -= build.averagePlayableCount * Mathf.Lerp(9f, 15f, strength);

                score += build.averageArrowLength * Mathf.Lerp(2.2f, 3.4f, strength);
                score += build.averageTurnCount * Mathf.Lerp(8f, 13f, strength);
                score += build.multiTurnArrowRatio * Mathf.Lerp(36f, 58f, strength);
                score += build.distinctTurnCount * Mathf.Lerp(7f, 11f, strength);

                score += build.dependencyParticipationRatio * Mathf.Lerp(230f, 380f, strength);
                score -= build.isolatedDependencyArrowCount * Mathf.Lerp(8f, 15f, strength);
                score += build.averageBlockerDistance * Mathf.Lerp(15f, 24f, strength);
                score += build.remoteBlockerRatio * Mathf.Lerp(180f, 300f, strength);
                score -= build.nearBlockerRatio * Mathf.Lerp(210f, 340f, strength);
                score -= build.zeroImpactPlayableRatio * Mathf.Lerp(260f, 420f, strength);
                score -= build.clusteredChoiceWaveRatio * Mathf.Lerp(220f, 360f, strength);
                score += build.distributedChoiceWaveRatio * Mathf.Lerp(120f, 210f, strength);
                score += build.averageChoiceSeparationRatio * Mathf.Lerp(260f, 430f, strength);
                score -= build.boundaryPlayableRatio * Mathf.Lerp(90f, 160f, strength);
                score -= build.shortPlayableExitLaneRatio * Mathf.Lerp(70f, 130f, strength);

                score += build.averageSolveJumpDistance * Mathf.Lerp(8f, 14f, strength);
                score += build.solveRegionTransitionRatio * Mathf.Lerp(130f, 220f, strength);
                score += build.solveCrossColumnTransitionRatio * Mathf.Lerp(100f, 180f, strength);
                score += build.solveOutwardResetRatio * Mathf.Lerp(80f, 140f, strength);
                score -= build.longestInwardSolveRun * Mathf.Lerp(24f, 40f, strength);
                break;
            case GeneratorAlgorithmMode.LockstepWeave:
                score = build.dependencyDepth * Mathf.Lerp(12f, 18f, strength);
                score += build.controlledGateCount * Mathf.Lerp(65f, 95f, strength);
                score += build.multiRegionAnchorCount * Mathf.Lerp(70f, 110f, strength);
                score += build.controlledGateEdgeRatio * Mathf.Lerp(75f, 120f, strength);
                score += build.controlledBurstWaveCount * Mathf.Lerp(6f, 12f, strength);
                score += build.singleArrowWaveRatio * Mathf.Lerp(35f, 60f, strength);
                score -= build.oversizedUnlockWaveCount * Mathf.Lerp(150f, 220f, strength);
                score -= Mathf.Max(0, build.maxPlayableCount - GeneratorTargetMaxPlayableRoutes)
                    * Mathf.Lerp(110f, 170f, strength);
                score -= Mathf.Max(0, build.initialPlayableCount - GeneratorTargetMaxPlayableRoutes)
                    * Mathf.Lerp(170f, 260f, strength);
                score -= Mathf.Max(0, build.maxUnlockWaveCount - GeneratorTargetMaxPlayableRoutes)
                    * Mathf.Lerp(110f, 175f, strength);

                // Decision quality dominates geometry. A long winding tail is only
                // valuable after its head participates in a non-obvious dependency.
                score += build.averageBlockerDistance * Mathf.Lerp(38f, 62f, strength);
                score += build.remoteBlockerRatio * Mathf.Lerp(340f, 520f, strength);
                score -= build.nearBlockerRatio * Mathf.Lerp(470f, 720f, strength);
                score += build.averagePlayableExitLane * Mathf.Lerp(16f, 28f, strength);
                score -= build.shortPlayableExitLaneRatio * Mathf.Lerp(220f, 340f, strength);
                score -= build.boundaryPlayableRatio * Mathf.Lerp(260f, 390f, strength);
                score -= build.zeroImpactPlayableRatio * Mathf.Lerp(420f, 650f, strength);
                score -= build.clusteredChoiceWaveRatio * Mathf.Lerp(360f, 560f, strength);
                score += build.distributedChoiceWaveRatio * Mathf.Lerp(180f, 300f, strength);
                score += build.averageChoiceSeparationRatio * Mathf.Lerp(420f, 680f, strength);
                score += build.averageWaveUnlockCount * Mathf.Lerp(28f, 46f, strength);

                score += build.averageArrowLength * Mathf.Lerp(1f, 1.8f, strength);
                score += build.averageTurnCount * Mathf.Lerp(3f, 5f, strength);
                score += build.multiTurnArrowRatio * Mathf.Lerp(14f, 24f, strength);
                score += build.distinctTurnCount * 3f;
                score += build.solveCrossColumnTransitionRatio * 55f;
                score += build.solveRegionTransitionRatio * 45f;
                score -= build.longestInwardSolveRun * 12f;
                break;
            case GeneratorAlgorithmMode.ChainFocus:
                score = build.dependencyDepth * Mathf.Lerp(34f, 52f, strength);
                score -= build.maxUnlockWaveCount * Mathf.Lerp(20f, 30f, strength);
                score -= build.overTwoUnlockWaveCount * Mathf.Lerp(30f, 46f, strength);
                score -= build.maxPlayableCount * Mathf.Lerp(14f, 22f, strength);
                score -= build.overTwoRouteMoveCount * Mathf.Lerp(12f, 20f, strength);
                score -= build.averagePlayableCount * Mathf.Lerp(10f, 16f, strength);
                score += build.averageArrowLength * 1.2f;
                score += build.averageTurnCount * 5f;
                score += build.multiTurnArrowRatio * 20f;
                score += build.distinctTurnCount * 3f;
                break;
            case GeneratorAlgorithmMode.Crossweave:
                score = build.dependencyDepth * Mathf.Lerp(18f, 28f, strength);
                score -= build.maxUnlockWaveCount * 10f;
                score -= build.overTwoUnlockWaveCount * 14f;
                score -= build.maxPlayableCount * 8f;
                score -= build.overTwoRouteMoveCount * 6f;
                score -= build.averagePlayableCount * 5f;
                score += build.averageArrowLength * 3f;
                score += build.averageTurnCount * Mathf.Lerp(16f, 25f, strength);
                score += build.multiTurnArrowRatio * Mathf.Lerp(55f, 82f, strength);
                score += build.distinctTurnCount * 12f;
                break;
            case GeneratorAlgorithmMode.Longform:
                score = build.dependencyDepth * Mathf.Lerp(14f, 22f, strength);
                score -= build.maxUnlockWaveCount * 8f;
                score -= build.overTwoUnlockWaveCount * 10f;
                score -= build.maxPlayableCount * 6f;
                score -= build.overTwoRouteMoveCount * 4f;
                score -= build.averagePlayableCount * 4f;
                score += build.averageArrowLength * Mathf.Lerp(5f, 8f, strength);
                score += build.averageTurnCount * Mathf.Lerp(8f, 13f, strength);
                score += build.multiTurnArrowRatio * 48f;
                score += build.distinctTurnCount * 5f;
                score -= build.addedCount * 2f;
                break;
            case GeneratorAlgorithmMode.CompactLocks:
                score = build.dependencyDepth * Mathf.Lerp(24f, 36f, strength);
                score -= build.maxUnlockWaveCount * Mathf.Lerp(15f, 22f, strength);
                score -= build.overTwoUnlockWaveCount * Mathf.Lerp(20f, 30f, strength);
                score -= build.maxPlayableCount * Mathf.Lerp(11f, 17f, strength);
                score -= build.overTwoRouteMoveCount * Mathf.Lerp(9f, 15f, strength);
                score -= build.averagePlayableCount * Mathf.Lerp(8f, 13f, strength);
                score += build.addedCount * Mathf.Lerp(1.5f, 3.5f, strength);
                score += build.averageTurnCount * 5f;
                score += build.multiTurnArrowRatio * 16f;
                score += build.distinctTurnCount * 3f;
                break;
            case GeneratorAlgorithmMode.ExpertMix:
                score = build.dependencyDepth * Mathf.Lerp(38f, 56f, strength);
                score -= build.maxUnlockWaveCount * Mathf.Lerp(22f, 32f, strength);
                score -= build.overTwoUnlockWaveCount * Mathf.Lerp(34f, 48f, strength);
                score -= build.maxPlayableCount * Mathf.Lerp(16f, 25f, strength);
                score -= build.overTwoRouteMoveCount * Mathf.Lerp(15f, 23f, strength);
                score -= build.averagePlayableCount * Mathf.Lerp(12f, 19f, strength);
                score += build.averageArrowLength * 3f;
                score += build.averageTurnCount * Mathf.Lerp(10f, 15f, strength);
                score += build.multiTurnArrowRatio * Mathf.Lerp(42f, 64f, strength);
                score += build.distinctTurnCount * 10f;
                break;
            default:
                score = build.dependencyDepth * Mathf.Lerp(24f, 42f, strength);
                score -= build.maxUnlockWaveCount * Mathf.Lerp(12f, 22f, strength);
                score -= build.overTwoUnlockWaveCount * Mathf.Lerp(18f, 34f, strength);
                score -= build.maxPlayableCount * Mathf.Lerp(14f, 25f, strength);
                score -= build.overTwoRouteMoveCount * Mathf.Lerp(12f, 24f, strength);
                score -= build.averagePlayableCount * Mathf.Lerp(10f, 18f, strength);
                score += build.averageArrowLength * Mathf.Lerp(1f, 2.2f, strength);
                score += build.averageTurnCount * Mathf.Lerp(5f, 10f, strength);
                score += build.multiTurnArrowRatio * Mathf.Lerp(18f, 42f, strength);
                score += build.distinctTurnCount * Mathf.Lerp(2f, 6f, strength);
                break;
        }

        score += build.spatialDependencyScore * Mathf.Lerp(0.55f, 0.95f, strength);
        return score;
    }

    private bool IsBetterGeneratedBuild(GeneratedLevelBuild candidate, GeneratedLevelBuild currentBest, int targetOccupiedCells)
    {
        if (candidate == null || candidate.occupied == null)
        {
            return false;
        }

        if (currentBest == null || currentBest.occupied == null)
        {
            return true;
        }

        bool candidateHitsTarget = candidate.occupied.Count >= targetOccupiedCells;
        bool currentHitsTarget = currentBest.occupied.Count >= targetOccupiedCells;
        GeneratorAlgorithmMode mode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;

        if (mode == GeneratorAlgorithmMode.LockstepWeave)
        {
            bool candidateDecisionValid = candidate.maxPlayableCount <= GeneratorTargetMaxPlayableRoutes
                && candidate.maxUnlockWaveCount <= GeneratorTargetMaxPlayableRoutes
                && candidate.clusteredChoiceWaveRatio <= 0f;
            bool currentDecisionValid = currentBest.maxPlayableCount <= GeneratorTargetMaxPlayableRoutes
                && currentBest.maxUnlockWaveCount <= GeneratorTargetMaxPlayableRoutes
                && currentBest.clusteredChoiceWaveRatio <= 0f;
            if (candidateDecisionValid != currentDecisionValid)
            {
                return candidateDecisionValid;
            }

            if (candidate.occupied.Count != currentBest.occupied.Count)
            {
                return candidate.occupied.Count > currentBest.occupied.Count;
            }

            if (!Mathf.Approximately(candidate.complexityScore, currentBest.complexityScore))
            {
                return candidate.complexityScore > currentBest.complexityScore;
            }
        }

        if (candidateHitsTarget != currentHitsTarget)
        {
            return candidateHitsTarget;
        }

        bool useAdvancedGuided = IsAdvancedGuidedMode(mode);
        bool prioritizeNarrowRoutes = PrioritizesNarrowRouteProfile(mode);
        bool requireFocusedOpening = prioritizeNarrowRoutes
            || mode == GeneratorAlgorithmMode.LockstepWeave
            || mode == GeneratorAlgorithmMode.ComplexGuidedDx
            || mode == GeneratorAlgorithmMode.ComplexGuidedDxFlow;

        bool candidateHasFocusedOpening = candidate.initialPlayableCount <= GeneratorTargetMaxPlayableRoutes;
        bool currentHasFocusedOpening = currentBest.initialPlayableCount <= GeneratorTargetMaxPlayableRoutes;
        if (requireFocusedOpening
            && candidateHitsTarget
            && currentHitsTarget
            && candidateHasFocusedOpening != currentHasFocusedOpening)
        {
            return candidateHasFocusedOpening;
        }

        if (prioritizeNarrowRoutes
            && candidateHitsTarget
            && currentHitsTarget
            && candidate.overTwoRouteMoveCount != currentBest.overTwoRouteMoveCount)
        {
            return candidate.overTwoRouteMoveCount < currentBest.overTwoRouteMoveCount;
        }

        if (prioritizeNarrowRoutes
            && candidateHitsTarget
            && currentHitsTarget
            && candidate.maxPlayableCount != currentBest.maxPlayableCount)
        {
            return candidate.maxPlayableCount < currentBest.maxPlayableCount;
        }

        if ((!useAdvancedGuided || prioritizeNarrowRoutes)
            && candidateHitsTarget
            && currentHitsTarget
            && candidate.overTwoUnlockWaveCount != currentBest.overTwoUnlockWaveCount)
        {
            return candidate.overTwoUnlockWaveCount < currentBest.overTwoUnlockWaveCount;
        }

        if ((!useAdvancedGuided || prioritizeNarrowRoutes)
            && candidateHitsTarget
            && currentHitsTarget
            && candidate.maxUnlockWaveCount != currentBest.maxUnlockWaveCount)
        {
            return candidate.maxUnlockWaveCount < currentBest.maxUnlockWaveCount;
        }

        if (prioritizeNarrowRoutes
            && candidateHitsTarget
            && currentHitsTarget
            && candidate.longestInwardSolveRun != currentBest.longestInwardSolveRun)
        {
            return candidate.longestInwardSolveRun < currentBest.longestInwardSolveRun;
        }

        if (prioritizeNarrowRoutes
            && candidateHitsTarget
            && currentHitsTarget
            && !Mathf.Approximately(
                candidate.solveCrossColumnTransitionRatio,
                currentBest.solveCrossColumnTransitionRatio))
        {
            return candidate.solveCrossColumnTransitionRatio > currentBest.solveCrossColumnTransitionRatio;
        }

        if (useAdvancedGuided
            && candidateHitsTarget
            && currentHitsTarget
            && !Mathf.Approximately(candidate.complexityScore, currentBest.complexityScore))
        {
            return candidate.complexityScore > currentBest.complexityScore;
        }

        if (candidateHitsTarget && currentHitsTarget && candidate.dependencyDepth != currentBest.dependencyDepth)
        {
            return candidate.dependencyDepth > currentBest.dependencyDepth;
        }

        if (candidateHitsTarget && currentHitsTarget && candidate.maxPlayableCount != currentBest.maxPlayableCount)
        {
            return candidate.maxPlayableCount < currentBest.maxPlayableCount;
        }

        if (candidateHitsTarget && currentHitsTarget && candidate.overTwoRouteMoveCount != currentBest.overTwoRouteMoveCount)
        {
            return candidate.overTwoRouteMoveCount < currentBest.overTwoRouteMoveCount;
        }

        if (candidateHitsTarget && currentHitsTarget && !Mathf.Approximately(candidate.averagePlayableCount, currentBest.averagePlayableCount))
        {
            return candidate.averagePlayableCount < currentBest.averagePlayableCount;
        }

        if (candidateHitsTarget && currentHitsTarget && candidate.initialPlayableCount != currentBest.initialPlayableCount)
        {
            return candidate.initialPlayableCount < currentBest.initialPlayableCount;
        }

        int closeEnoughRange = Mathf.Max(2, Mathf.CeilToInt(targetOccupiedCells * 0.05f));
        if (!candidateHitsTarget
            && Mathf.Abs(candidate.occupied.Count - currentBest.occupied.Count) <= closeEnoughRange
            && HasBetterGeneratedRouteProfile(candidate, currentBest))
        {
            return true;
        }

        if (candidate.occupied.Count != currentBest.occupied.Count)
        {
            return candidate.occupied.Count > currentBest.occupied.Count;
        }

        return candidate.addedCount > currentBest.addedCount;
    }

    private bool HasBetterGeneratedRouteProfile(GeneratedLevelBuild candidate, GeneratedLevelBuild currentBest)
    {
        bool candidateHasFocusedOpening = candidate.initialPlayableCount <= GeneratorTargetMaxPlayableRoutes;
        bool currentHasFocusedOpening = currentBest.initialPlayableCount <= GeneratorTargetMaxPlayableRoutes;
        if (candidateHasFocusedOpening != currentHasFocusedOpening)
        {
            return candidateHasFocusedOpening;
        }

        if (candidate.overTwoRouteMoveCount != currentBest.overTwoRouteMoveCount)
        {
            return candidate.overTwoRouteMoveCount < currentBest.overTwoRouteMoveCount;
        }

        if (candidate.maxPlayableCount != currentBest.maxPlayableCount)
        {
            return candidate.maxPlayableCount < currentBest.maxPlayableCount;
        }

        if (candidate.overTwoUnlockWaveCount != currentBest.overTwoUnlockWaveCount)
        {
            return candidate.overTwoUnlockWaveCount < currentBest.overTwoUnlockWaveCount;
        }

        if (candidate.maxUnlockWaveCount != currentBest.maxUnlockWaveCount)
        {
            return candidate.maxUnlockWaveCount < currentBest.maxUnlockWaveCount;
        }

        if (candidate.dependencyDepth != currentBest.dependencyDepth)
        {
            return candidate.dependencyDepth > currentBest.dependencyDepth;
        }

        if (!Mathf.Approximately(candidate.averagePlayableCount, currentBest.averagePlayableCount))
        {
            return candidate.averagePlayableCount < currentBest.averagePlayableCount;
        }

        return candidate.initialPlayableCount < currentBest.initialPlayableCount;
    }

    private void ShuffleGeneratedVisualOrder(List<RuntimeArrowDraft> generatedArrows, int firstShuffleIndex, int seed)
    {
        if (generatedArrows == null || generatedArrows.Count <= 1)
        {
            return;
        }

        firstShuffleIndex = Mathf.Clamp(firstShuffleIndex, 0, generatedArrows.Count);
        int shuffleCount = generatedArrows.Count - firstShuffleIndex;
        if (shuffleCount <= 1)
        {
            return;
        }

        List<RuntimeArrowDraft> shuffledArrows = generatedArrows.GetRange(firstShuffleIndex, shuffleCount);
        ShuffleList(shuffledArrows, new System.Random(seed ^ 0x5f3759df));

        for (int i = 0; i < shuffledArrows.Count; i++)
        {
            int targetIndex = firstShuffleIndex + i;
            generatedArrows[targetIndex] = shuffledArrows[i];
            generatedArrows[targetIndex].id = $"Arrow {targetIndex + 1}";
        }
    }

    private bool IsFullRectangleGeneratorZone(HashSet<Vector2Int> zoneSet)
    {
        if (zoneSet == null || zoneSet.Count != width * height)
        {
            return false;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!zoneSet.Contains(new Vector2Int(x, y)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsGeneratorTimeExpired(DateTime generationStart, int timeBudgetMs)
    {
        return (DateTime.UtcNow - generationStart).TotalMilliseconds >= timeBudgetMs;
    }

    private bool WouldLeaveUnfillableFreeRegion(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> candidateCells,
        int minLength,
        int targetOccupiedCells)
    {
        int remainingFreeCells = zoneCells.Count - occupied.Count - candidateCells.Count;
        if (remainingFreeCells == 0 || occupied.Count + candidateCells.Count >= targetOccupiedCells)
        {
            return false;
        }

        if (remainingFreeCells < minLength)
        {
            return true;
        }

        return CountUnfillableFreeRegionCells(
            zoneCells,
            zoneSet,
            occupied,
            candidateCells,
            minLength) > 0;
    }

    // Density repair may inherit several small pockets from the main pass. It
    // can remove those pockets incrementally as long as a change does not create
    // more trapped cells than the current board already contains.
    private bool WouldWorsenUnfillableFreeRegions(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> candidateCells,
        int minLength,
        int targetOccupiedCells)
    {
        if (occupied.Count + candidateCells.Count >= targetOccupiedCells)
        {
            return false;
        }

        int currentUnfillableCells = CountUnfillableFreeRegionCells(
            zoneCells,
            zoneSet,
            occupied,
            null,
            minLength);
        int projectedUnfillableCells = CountUnfillableFreeRegionCells(
            zoneCells,
            zoneSet,
            occupied,
            candidateCells,
            minLength);
        return projectedUnfillableCells > currentUnfillableCells;
    }

    private int CountUnfillableFreeRegionCells(
        List<Vector2Int> zoneCells,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> candidateCells,
        int minLength)
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        int unfillableCellCount = 0;

        for (int i = 0; i < zoneCells.Count; i++)
        {
            Vector2Int start = zoneCells[i];
            if (visited.Contains(start)
                || occupied.Contains(start)
                || (candidateCells != null && candidateCells.Contains(start)))
            {
                continue;
            }

            int componentSize = 0;
            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                componentSize++;

                TryQueueFreeGeneratorNeighbor(current + Vector2Int.right, zoneSet, occupied, candidateCells, visited, queue);
                TryQueueFreeGeneratorNeighbor(current + Vector2Int.left, zoneSet, occupied, candidateCells, visited, queue);
                TryQueueFreeGeneratorNeighbor(current + Vector2Int.up, zoneSet, occupied, candidateCells, visited, queue);
                TryQueueFreeGeneratorNeighbor(current + Vector2Int.down, zoneSet, occupied, candidateCells, visited, queue);
            }

            if (componentSize < minLength)
            {
                unfillableCellCount += componentSize;
            }
        }

        return unfillableCellCount;
    }

    private void TryQueueFreeGeneratorNeighbor(
        Vector2Int cell,
        HashSet<Vector2Int> zoneSet,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> candidateCells,
        HashSet<Vector2Int> visited,
        Queue<Vector2Int> queue)
    {
        if (!zoneSet.Contains(cell)
            || occupied.Contains(cell)
            || (candidateCells != null && candidateCells.Contains(cell))
            || visited.Contains(cell))
        {
            return;
        }

        visited.Add(cell);
        queue.Enqueue(cell);
    }

    private bool CanGeneratedArrowEscapeThroughOccupied(RuntimeArrowDraft arrow, HashSet<Vector2Int> occupied, HashSet<Vector2Int> zoneSet)
    {
        Vector2Int direction = GetGeneratedExitDirection(arrow);
        if (direction == Vector2Int.zero)
        {
            return false;
        }

        Vector2Int head = arrow.points[arrow.points.Count - 1];
        Vector2Int checkPosition = head + direction;
        while (IsInsideGridBounds(checkPosition))
        {
            if (occupied.Contains(checkPosition))
            {
                return false;
            }

            checkPosition += direction;
        }

        return true;
    }

    private int CountGeneratedCurrentPlayableArrows(
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet)
    {
        int result = 0;
        for (int i = 0; i < generatedArrows.Count; i++)
        {
            if (CanGeneratedArrowEscapeThroughOccupied(generatedArrows[i], occupied, zoneSet))
            {
                result++;
            }
        }

        return result;
    }

    private bool HasGeneratedFreeExitRayCell(RuntimeArrowDraft arrow, HashSet<Vector2Int> occupied, HashSet<Vector2Int> zoneSet)
    {
        Vector2Int direction = GetGeneratedExitDirection(arrow);
        if (direction == Vector2Int.zero)
        {
            return false;
        }

        Vector2Int checkPosition = arrow.points[arrow.points.Count - 1] + direction;
        while (IsInsideGridBounds(checkPosition))
        {
            if (zoneSet.Contains(checkPosition) && !occupied.Contains(checkPosition))
            {
                return true;
            }

            checkPosition += direction;
        }

        return false;
    }

    private Vector2Int GetGeneratedExitDirection(RuntimeArrowDraft arrow)
    {
        if (arrow == null || arrow.points.Count < 2)
        {
            return Vector2Int.zero;
        }

        Vector2Int head = arrow.points[arrow.points.Count - 1];
        Vector2Int previous = arrow.points[arrow.points.Count - 2];
        Vector2Int delta = head - previous;
        if (delta == Vector2Int.zero || (delta.x != 0 && delta.y != 0))
        {
            return Vector2Int.zero;
        }

        return new Vector2Int(Sign(delta.x), Sign(delta.y));
    }

    private bool TryBuildShapePeelingFallback(
        HashSet<Vector2Int> zoneSet,
        int minLength,
        int maxLength,
        int seed,
        DateTime generationStart,
        int timeBudgetMs,
        out GeneratedLevelBuild build)
    {
        build = null;
        if (zoneSet == null || zoneSet.Count < minLength)
        {
            return false;
        }

        int fallbackAttemptCount = 10;

        for (int attemptIndex = 0; attemptIndex < fallbackAttemptCount && !IsGeneratorTimeExpired(generationStart, timeBudgetMs); attemptIndex++)
        {
            System.Random rng = new System.Random(seed + 104729 + attemptIndex * 4099);
            List<RuntimeArrowDraft> fallbackArrows = new List<RuntimeArrowDraft>();
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(zoneSet);
            int guard = 0;

            while (remaining.Count > 0 && guard < zoneSet.Count && !IsGeneratorTimeExpired(generationStart, timeBudgetMs))
            {
                guard++;

                if (!TryBuildPeelingFallbackArrow(remaining, zoneSet, minLength, maxLength, rng, out RuntimeArrowDraft arrow, out HashSet<Vector2Int> arrowCells))
                {
                    break;
                }

                arrow.id = $"Arrow {fallbackArrows.Count + 1}";
                arrow.color = GetGeneratedSavedArrowColor();
                fallbackArrows.Add(arrow);

                foreach (Vector2Int cell in arrowCells)
                {
                    remaining.Remove(cell);
                    occupied.Add(cell);
                }
            }

            if (remaining.Count == 0 && occupied.Count == zoneSet.Count && CanGeneratedLevelSolve(fallbackArrows, zoneSet))
            {
                build = CreateGeneratedLevelBuild(fallbackArrows, occupied, fallbackArrows.Count, seed, true, zoneSet);
                return true;
            }
        }

        return false;
    }

    private bool TryBuildPeelingFallbackArrow(
        HashSet<Vector2Int> remaining,
        HashSet<Vector2Int> zoneSet,
        int minLength,
        int maxLength,
        System.Random rng,
        out RuntimeArrowDraft arrow,
        out HashSet<Vector2Int> arrowCells)
    {
        arrow = null;
        arrowCells = null;

        List<GeneratorExitCandidate> candidates = GetPeelingExitCandidates(remaining, zoneSet);
        ShuffleList(candidates, rng);

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            GeneratorExitCandidate candidate = candidates[candidateIndex];
            List<int> lengthOrder = BuildPeelingLengthOrder(remaining.Count, minLength, maxLength, rng);

            for (int lengthIndex = 0; lengthIndex < lengthOrder.Count; lengthIndex++)
            {
                int targetLength = lengthOrder[lengthIndex];
                List<Vector2Int> pathCells = new List<Vector2Int> { candidate.head };
                HashSet<Vector2Int> visited = new HashSet<Vector2Int> { candidate.head };
                Vector2Int firstBodyCell = candidate.head - candidate.exitDirection;

                if (!remaining.Contains(firstBodyCell) || visited.Contains(firstBodyCell))
                {
                    continue;
                }

                pathCells.Add(firstBodyCell);
                visited.Add(firstBodyCell);

                if (pathCells.Count < targetLength
                    && !TryExtendPeelingFallbackPath(targetLength, remaining, rng, pathCells, visited))
                {
                    continue;
                }

                HashSet<Vector2Int> candidateCells = new HashSet<Vector2Int>(pathCells);
                if (WouldLeaveInvalidPeelingRemainder(remaining, candidateCells, minLength))
                {
                    continue;
                }

                List<Vector2Int> pointCells = new List<Vector2Int>(pathCells);
                pointCells.Reverse();
                List<Vector2Int> points = CompressGeneratedPath(pointCells);
                if (points.Count < 2
                    || PathArrowUtility.TryFindSelfOverlap(points, out _, out _)
                    || PathArrowUtility.TryFindOwnExitBlock(points, width, height, zoneSet, out _))
                {
                    continue;
                }

                HashSet<Vector2Int> filledCells = new HashSet<Vector2Int>();
                if (!FillGeneratedCells(points, zoneSet, filledCells) || filledCells.Count != candidateCells.Count)
                {
                    continue;
                }

                arrow = new RuntimeArrowDraft();
                arrow.points.AddRange(points);
                arrowCells = filledCells;
                return true;
            }
        }

        return false;
    }

    private List<GeneratorExitCandidate> GetPeelingExitCandidates(HashSet<Vector2Int> remaining, HashSet<Vector2Int> zoneSet)
    {
        List<GeneratorExitCandidate> result = new List<GeneratorExitCandidate>();
        Vector2Int[] directions =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        foreach (Vector2Int cell in remaining)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int exitDirection = directions[i];
                if (remaining.Contains(cell - exitDirection) && HasClearPeelingExitRay(cell, exitDirection, remaining, zoneSet))
                {
                    result.Add(new GeneratorExitCandidate(cell, exitDirection));
                }
            }
        }

        return result;
    }

    private bool HasClearPeelingExitRay(
        Vector2Int head,
        Vector2Int exitDirection,
        HashSet<Vector2Int> remaining,
        HashSet<Vector2Int> zoneSet)
    {
        Vector2Int checkPosition = head + exitDirection;
        while (IsInsideGridBounds(checkPosition))
        {
            if (remaining.Contains(checkPosition))
            {
                return false;
            }

            checkPosition += exitDirection;
        }

        return true;
    }

    private List<int> BuildPeelingLengthOrder(int remainingCount, int minLength, int maxLength, System.Random rng)
    {
        List<int> result = new List<int>();
        int cappedMaxLength = Mathf.Min(maxLength, remainingCount);

        for (int length = minLength; length <= cappedMaxLength; length++)
        {
            int leftover = remainingCount - length;
            if (leftover == 0 || leftover >= minLength)
            {
                result.Add(length);
            }
        }

        ShuffleList(result, rng);
        if (rng.NextDouble() < 0.62)
        {
            result.Sort((a, b) => b.CompareTo(a));
        }

        return result;
    }

    private bool TryExtendPeelingFallbackPath(
        int targetLength,
        HashSet<Vector2Int> remaining,
        System.Random rng,
        List<Vector2Int> pathCells,
        HashSet<Vector2Int> visited)
    {
        if (pathCells.Count >= targetLength)
        {
            return true;
        }

        Vector2Int current = pathCells[pathCells.Count - 1];
        List<Vector2Int> directions = GetPeelingPathDirections(pathCells, rng);

        for (int i = 0; i < directions.Count; i++)
        {
            Vector2Int next = current + directions[i];
            if (!remaining.Contains(next) || visited.Contains(next))
            {
                continue;
            }

            pathCells.Add(next);
            visited.Add(next);

            if (TryExtendPeelingFallbackPath(targetLength, remaining, rng, pathCells, visited))
            {
                return true;
            }

            visited.Remove(next);
            pathCells.RemoveAt(pathCells.Count - 1);
        }

        return false;
    }

    private List<Vector2Int> GetPeelingPathDirections(List<Vector2Int> pathCells, System.Random rng)
    {
        List<Vector2Int> directions = new List<Vector2Int>
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };
        ShuffleList(directions, rng);

        if (pathCells.Count < 2 || rng.NextDouble() > 0.72)
        {
            return directions;
        }

        Vector2Int previousStep = pathCells[pathCells.Count - 1] - pathCells[pathCells.Count - 2];
        Vector2Int backStep = new Vector2Int(-previousStep.x, -previousStep.y);
        directions.Sort((a, b) =>
        {
            bool aTurns = a != previousStep && a != backStep;
            bool bTurns = b != previousStep && b != backStep;
            return aTurns == bTurns ? 0 : (aTurns ? -1 : 1);
        });
        return directions;
    }

    private bool WouldLeaveInvalidPeelingRemainder(
        HashSet<Vector2Int> remaining,
        HashSet<Vector2Int> candidateCells,
        int minLength)
    {
        int remainingAfter = remaining.Count - candidateCells.Count;
        if (remainingAfter == 0)
        {
            return false;
        }

        if (remainingAfter < minLength)
        {
            return true;
        }

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        foreach (Vector2Int start in remaining)
        {
            if (candidateCells.Contains(start) || visited.Contains(start))
            {
                continue;
            }

            int componentSize = 0;
            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                componentSize++;
                TryQueuePeelingRemainderNeighbor(current + Vector2Int.right, remaining, candidateCells, visited, queue);
                TryQueuePeelingRemainderNeighbor(current + Vector2Int.left, remaining, candidateCells, visited, queue);
                TryQueuePeelingRemainderNeighbor(current + Vector2Int.up, remaining, candidateCells, visited, queue);
                TryQueuePeelingRemainderNeighbor(current + Vector2Int.down, remaining, candidateCells, visited, queue);
            }

            if (componentSize < minLength)
            {
                return true;
            }
        }

        return false;
    }

    private void TryQueuePeelingRemainderNeighbor(
        Vector2Int cell,
        HashSet<Vector2Int> remaining,
        HashSet<Vector2Int> candidateCells,
        HashSet<Vector2Int> visited,
        Queue<Vector2Int> queue)
    {
        if (!remaining.Contains(cell) || candidateCells.Contains(cell) || visited.Contains(cell))
        {
            return;
        }

        visited.Add(cell);
        queue.Enqueue(cell);
    }

    private HashSet<Vector2Int> GetFullRectangleZoneSet()
    {
        HashSet<Vector2Int> result = new HashSet<Vector2Int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result.Add(new Vector2Int(x, y));
            }
        }

        return result;
    }

    private bool TryCreateGeneratedArrowCandidate(
        Vector2Int start,
        int targetLength,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet,
        System.Random rng,
        out RuntimeArrowDraft arrow)
    {
        if ((GeneratorAlgorithmMode)generatorAlgorithmModeIndex != GeneratorAlgorithmMode.Legacy)
        {
            GeneratorAlgorithmMode algorithmMode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
            int profileAttempts = IsAdvancedGuidedMode(algorithmMode)
                ? GeneratorProfileCandidateAttempts + 2
                : GeneratorProfileCandidateAttempts;
            if (algorithmMode == GeneratorAlgorithmMode.Longform
                || algorithmMode == GeneratorAlgorithmMode.ExpertMix)
            {
                profileAttempts += 2;
            }
            for (int attempt = 0; attempt < profileAttempts; attempt++)
            {
                GeneratedArrowProfile profile = ChooseGeneratedArrowProfile(targetLength, rng);
                if (profile != GeneratedArrowProfile.Unknown
                    && TryCreateGeneratedProfileArrow(start, targetLength, occupied, zoneSet, profile, rng, out arrow))
                {
                    return true;
                }
            }
        }

        return TryCreateGeneratedSnakeArrow(start, targetLength, occupied, zoneSet, rng, out arrow);
    }

    private GeneratedArrowProfile ChooseGeneratedArrowProfile(int targetLength, System.Random rng)
    {
        GeneratedArrowProfile[] profiles = GuidedGeneratorProfiles;
        float totalWeight = 0f;
        for (int i = 0; i < profiles.Length; i++)
        {
            totalWeight += GetSelectedGeneratedProfileWeight(profiles[i], targetLength);
        }

        if (totalWeight <= 0f)
        {
            return GeneratedArrowProfile.Unknown;
        }

        double selection = rng.NextDouble() * totalWeight;
        for (int i = 0; i < profiles.Length; i++)
        {
            GeneratedArrowProfile profile = profiles[i];
            selection -= GetSelectedGeneratedProfileWeight(profile, targetLength);
            if (selection <= 0d)
            {
                return profile;
            }
        }

        return profiles[profiles.Length - 1];
    }

    private float GetSelectedGeneratedProfileWeight(GeneratedArrowProfile profile, int targetLength)
    {
        float weight = GetGeneratedProfileWeight(profile, targetLength);
        GeneratorAlgorithmMode mode = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex;
        if (!IsAdvancedGuidedMode(mode) || weight <= 0f)
        {
            return weight;
        }

        float strength = Mathf.InverseLerp(25f, 100f, generatorComplexityPercent);
        float complexMultiplier = GetAdvancedGeneratedProfileMultiplier(mode, profile);
        return weight * Mathf.Lerp(1f, complexMultiplier, strength);
    }

    private static float GetAdvancedGeneratedProfileMultiplier(
        GeneratorAlgorithmMode mode,
        GeneratedArrowProfile profile)
    {
        if (mode == GeneratorAlgorithmMode.ChainFocus)
        {
            switch (profile)
            {
                case GeneratedArrowProfile.StraightRail: return 0.8f;
                case GeneratedArrowProfile.LHook: return 0.9f;
                case GeneratedArrowProfile.Hairpin: return 0.68f;
                case GeneratedArrowProfile.OffsetS: return 1.15f;
                case GeneratedArrowProfile.OpenC: return 1.15f;
                case GeneratedArrowProfile.RectangularSpiral: return 1.2f;
                case GeneratedArrowProfile.Serpentine: return 1.25f;
                case GeneratedArrowProfile.Staircase: return 1.2f;
                case GeneratedArrowProfile.PerimeterRunner: return 1.1f;
                case GeneratedArrowProfile.LongSpine: return 1.15f;
            }
        }

        if (mode == GeneratorAlgorithmMode.Crossweave)
        {
            switch (profile)
            {
                case GeneratedArrowProfile.StraightRail: return 0.45f;
                case GeneratedArrowProfile.LHook: return 0.7f;
                case GeneratedArrowProfile.Hairpin: return 0.62f;
                case GeneratedArrowProfile.OffsetS: return 1.4f;
                case GeneratedArrowProfile.OpenC: return 1.3f;
                case GeneratedArrowProfile.RectangularSpiral: return 1.5f;
                case GeneratedArrowProfile.Serpentine: return 1.65f;
                case GeneratedArrowProfile.Staircase: return 1.45f;
                case GeneratedArrowProfile.PerimeterRunner: return 1.5f;
                case GeneratedArrowProfile.LongSpine: return 1.3f;
            }
        }

        if (mode == GeneratorAlgorithmMode.Longform)
        {
            switch (profile)
            {
                case GeneratedArrowProfile.StraightRail: return 1f;
                case GeneratedArrowProfile.LHook: return 0.6f;
                case GeneratedArrowProfile.Hairpin: return 0.5f;
                case GeneratedArrowProfile.OffsetS: return 1f;
                case GeneratedArrowProfile.OpenC: return 1.2f;
                case GeneratedArrowProfile.RectangularSpiral: return 1.4f;
                case GeneratedArrowProfile.Serpentine: return 1.5f;
                case GeneratedArrowProfile.Staircase: return 1.2f;
                case GeneratedArrowProfile.PerimeterRunner: return 1.65f;
                case GeneratedArrowProfile.LongSpine: return 2f;
            }
        }

        if (mode == GeneratorAlgorithmMode.CompactLocks)
        {
            switch (profile)
            {
                case GeneratedArrowProfile.StraightRail: return 1.25f;
                case GeneratedArrowProfile.LHook: return 1.4f;
                case GeneratedArrowProfile.Hairpin: return 0.8f;
                case GeneratedArrowProfile.OffsetS: return 1.25f;
                case GeneratedArrowProfile.OpenC: return 0.8f;
                case GeneratedArrowProfile.RectangularSpiral: return 0.45f;
                case GeneratedArrowProfile.Serpentine: return 0.7f;
                case GeneratedArrowProfile.Staircase: return 0.8f;
                case GeneratedArrowProfile.PerimeterRunner: return 0.5f;
                case GeneratedArrowProfile.LongSpine: return 0.45f;
            }
        }

        if (mode == GeneratorAlgorithmMode.ExpertMix)
        {
            switch (profile)
            {
                case GeneratedArrowProfile.StraightRail: return 0.55f;
                case GeneratedArrowProfile.LHook: return 0.75f;
                case GeneratedArrowProfile.Hairpin: return 0.5f;
                case GeneratedArrowProfile.OffsetS: return 1.25f;
                case GeneratedArrowProfile.OpenC: return 1.2f;
                case GeneratedArrowProfile.RectangularSpiral: return 1.6f;
                case GeneratedArrowProfile.Serpentine: return 1.55f;
                case GeneratedArrowProfile.Staircase: return 1.5f;
                case GeneratedArrowProfile.PerimeterRunner: return 1.4f;
                case GeneratedArrowProfile.LongSpine: return 1.45f;
            }
        }

        if (mode == GeneratorAlgorithmMode.ComplexGuidedDx)
        {
            switch (profile)
            {
                case GeneratedArrowProfile.StraightRail: return 0.55f;
                case GeneratedArrowProfile.LHook: return 0.75f;
                case GeneratedArrowProfile.Hairpin: return 0.55f;
                case GeneratedArrowProfile.OffsetS: return 1.35f;
                case GeneratedArrowProfile.OpenC: return 1.25f;
                case GeneratedArrowProfile.RectangularSpiral: return 1.55f;
                case GeneratedArrowProfile.Serpentine: return 1.65f;
                case GeneratedArrowProfile.Staircase: return 1.5f;
                case GeneratedArrowProfile.PerimeterRunner: return 1.45f;
                case GeneratedArrowProfile.LongSpine: return 1.65f;
            }
        }

        if (mode == GeneratorAlgorithmMode.ComplexGuidedDxFlow)
        {
            switch (profile)
            {
                case GeneratedArrowProfile.StraightRail: return 0.5f;
                case GeneratedArrowProfile.LHook: return 0.7f;
                case GeneratedArrowProfile.Hairpin: return 0.5f;
                case GeneratedArrowProfile.OffsetS: return 1.4f;
                case GeneratedArrowProfile.OpenC: return 1.25f;
                case GeneratedArrowProfile.RectangularSpiral: return 1.55f;
                case GeneratedArrowProfile.Serpentine: return 1.7f;
                case GeneratedArrowProfile.Staircase: return 1.55f;
                case GeneratedArrowProfile.PerimeterRunner: return 1.5f;
                case GeneratedArrowProfile.LongSpine: return 1.8f;
            }
        }

        if (mode == GeneratorAlgorithmMode.LockstepWeave)
        {
            switch (profile)
            {
                case GeneratedArrowProfile.StraightRail: return 0.45f;
                case GeneratedArrowProfile.LHook: return 0.65f;
                case GeneratedArrowProfile.Hairpin: return 0.5f;
                case GeneratedArrowProfile.OffsetS: return 1.55f;
                case GeneratedArrowProfile.OpenC: return 1.3f;
                case GeneratedArrowProfile.RectangularSpiral: return 1.75f;
                case GeneratedArrowProfile.Serpentine: return 1.9f;
                case GeneratedArrowProfile.Staircase: return 1.65f;
                case GeneratedArrowProfile.PerimeterRunner: return 1.45f;
                case GeneratedArrowProfile.LongSpine: return 1.7f;
            }
        }

        switch (profile)
        {
            case GeneratedArrowProfile.StraightRail:
                return 0.62f;
            case GeneratedArrowProfile.LHook:
                return 0.78f;
            case GeneratedArrowProfile.Hairpin:
                return 0.72f;
            case GeneratedArrowProfile.OffsetS:
                return 1.18f;
            case GeneratedArrowProfile.OpenC:
                return 1.12f;
            case GeneratedArrowProfile.RectangularSpiral:
                return 1.45f;
            case GeneratedArrowProfile.Serpentine:
                return 1.42f;
            case GeneratedArrowProfile.Staircase:
                return 1.32f;
            case GeneratedArrowProfile.PerimeterRunner:
                return 1.36f;
            case GeneratedArrowProfile.LongSpine:
                return 1.34f;
            default:
                return 1f;
        }
    }

    private static float GetGeneratedProfileWeight(GeneratedArrowProfile profile, int targetLength)
    {
        if (targetLength < GetGeneratedProfileMinimumLength(profile))
        {
            return 0f;
        }

        switch (profile)
        {
            case GeneratedArrowProfile.StraightRail:
                return targetLength <= 4 ? 18f : 6f;
            case GeneratedArrowProfile.LHook:
                return targetLength <= 5 ? 18f : 10f;
            case GeneratedArrowProfile.OffsetS:
                return 15f;
            case GeneratedArrowProfile.Hairpin:
                return 9f;
            case GeneratedArrowProfile.OpenC:
                return 14f;
            case GeneratedArrowProfile.RectangularSpiral:
                return 13f;
            case GeneratedArrowProfile.Serpentine:
                return 18f;
            case GeneratedArrowProfile.Staircase:
                return 15f;
            case GeneratedArrowProfile.PerimeterRunner:
                return 14f;
            case GeneratedArrowProfile.LongSpine:
                return 15f;
            default:
                return 0f;
        }
    }

    private static int GetGeneratedProfileMinimumLength(GeneratedArrowProfile profile)
    {
        switch (profile)
        {
            case GeneratedArrowProfile.StraightRail:
                return 2;
            case GeneratedArrowProfile.LHook:
                return 3;
            case GeneratedArrowProfile.OffsetS:
            case GeneratedArrowProfile.Hairpin:
                return 4;
            case GeneratedArrowProfile.LongSpine:
            case GeneratedArrowProfile.Staircase:
                return 6;
            case GeneratedArrowProfile.OpenC:
                return 7;
            case GeneratedArrowProfile.Serpentine:
            case GeneratedArrowProfile.PerimeterRunner:
                return 8;
            case GeneratedArrowProfile.RectangularSpiral:
                return 12;
            default:
                return int.MaxValue;
        }
    }

    private bool TryCreateGeneratedProfileArrow(
        Vector2Int start,
        int targetLength,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet,
        GeneratedArrowProfile profile,
        System.Random rng,
        out RuntimeArrowDraft arrow)
    {
        arrow = null;
        if (targetLength < GetGeneratedProfileMinimumLength(profile))
        {
            return false;
        }

        for (int planAttempt = 0; planAttempt < 2; planAttempt++)
        {
            if (!TryBuildGeneratedProfilePlan(profile, targetLength, rng, out List<int> segmentLengths, out List<int> turnSigns))
            {
                continue;
            }

            int rotationOffset = rng.Next(0, 4);
            int mirrorOffset = rng.Next(0, 2);
            for (int transformAttempt = 0; transformAttempt < 8; transformAttempt++)
            {
                int directionIndex = (rotationOffset + transformAttempt % 4) % 4;
                int mirrorSign = ((transformAttempt / 4 + mirrorOffset) % 2 == 0) ? 1 : -1;
                Vector2Int firstDirection = GetGeneratedCardinalDirection(directionIndex);

                if (!TryBuildGeneratedProfilePath(
                    start,
                    firstDirection,
                    mirrorSign,
                    segmentLengths,
                    turnSigns,
                    occupied,
                    zoneSet,
                    out List<Vector2Int> pathCells))
                {
                    continue;
                }

                for (int endpointAttempt = 0; endpointAttempt < 2; endpointAttempt++)
                {
                    List<Vector2Int> orientedPath = pathCells;
                    if (endpointAttempt == 1)
                    {
                        orientedPath = new List<Vector2Int>(pathCells);
                        orientedPath.Reverse();
                    }

                    List<Vector2Int> points = CompressGeneratedPath(orientedPath);
                    if (points.Count < 2
                        || PathArrowUtility.TryFindSelfOverlap(points, out _, out _)
                        || PathArrowUtility.TryFindOwnExitBlock(points, width, height, zoneSet, out _))
                    {
                        continue;
                    }

                    HashSet<Vector2Int> arrowCells = new HashSet<Vector2Int>();
                    if (!FillGeneratedCells(points, zoneSet, arrowCells) || arrowCells.Count != targetLength)
                    {
                        continue;
                    }

                    arrow = new RuntimeArrowDraft
                    {
                        generatedProfile = profile
                    };
                    arrow.points.AddRange(points);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryBuildGeneratedProfilePlan(
        GeneratedArrowProfile profile,
        int targetLength,
        System.Random rng,
        out List<int> segmentLengths,
        out List<int> turnSigns)
    {
        segmentLengths = new List<int>();
        turnSigns = new List<int>();
        int[] extraTargets;
        float[] extraWeights;

        switch (profile)
        {
            case GeneratedArrowProfile.StraightRail:
                segmentLengths.Add(1);
                extraTargets = new[] { 0 };
                extraWeights = new[] { 1f };
                break;

            case GeneratedArrowProfile.LHook:
                segmentLengths.AddRange(new[] { 1, 1 });
                turnSigns.Add(1);
                extraTargets = new[] { 0, 1 };
                extraWeights = new[] { 1.35f, 1f };
                break;

            case GeneratedArrowProfile.OffsetS:
                segmentLengths.AddRange(new[] { 1, 1, 1 });
                turnSigns.AddRange(new[] { 1, -1 });
                extraTargets = new[] { 0, 2, 1 };
                extraWeights = new[] { 1.35f, 1.2f, 0.45f };
                break;

            case GeneratedArrowProfile.Hairpin:
                segmentLengths.AddRange(new[] { 1, 1, 1 });
                turnSigns.AddRange(new[] { 1, 1 });
                extraTargets = new[] { 0, 2, 1 };
                extraWeights = new[] { 1.3f, 1.1f, 0.4f };
                break;

            case GeneratedArrowProfile.OpenC:
                segmentLengths.AddRange(new[] { 2, 2, 1, 1 });
                turnSigns.AddRange(new[] { 1, 1, 1 });
                extraTargets = new[] { 0, 1, 2 };
                extraWeights = new[] { 1.5f, 1.1f, 0.8f };
                break;

            case GeneratedArrowProfile.RectangularSpiral:
                segmentLengths.AddRange(new[] { 3, 3, 2, 2, 1 });
                turnSigns.AddRange(new[] { 1, 1, 1, 1 });
                extraTargets = new[] { 0, 1 };
                extraWeights = new[] { 1f, 1f };
                break;

            case GeneratedArrowProfile.Serpentine:
                segmentLengths.AddRange(new[] { 2, 1, 2, 1, 1 });
                turnSigns.AddRange(new[] { 1, 1, -1, -1 });
                extraTargets = new[] { 0, 2, 4 };
                extraWeights = new[] { 1.3f, 1.15f, 0.9f };
                break;

            case GeneratedArrowProfile.Staircase:
                int staircaseSegments = Mathf.Clamp(targetLength - 1, 5, 7);
                for (int i = 0; i < staircaseSegments; i++)
                {
                    segmentLengths.Add(1);
                    if (i < staircaseSegments - 1)
                    {
                        turnSigns.Add(i % 2 == 0 ? 1 : -1);
                    }
                }

                extraTargets = new int[staircaseSegments];
                extraWeights = new float[staircaseSegments];
                for (int i = 0; i < staircaseSegments; i++)
                {
                    extraTargets[i] = i;
                    extraWeights[i] = 1f;
                }

                break;

            case GeneratedArrowProfile.PerimeterRunner:
                segmentLengths.AddRange(new[] { 3, 2, 1, 1 });
                turnSigns.AddRange(new[] { 1, 1, 1 });
                extraTargets = new[] { 0, 2, 1 };
                extraWeights = new[] { 1.7f, 1.15f, 0.45f };
                break;

            case GeneratedArrowProfile.LongSpine:
                segmentLengths.AddRange(new[] { 3, 1, 1 });
                turnSigns.AddRange(new[] { 1, -1 });
                extraTargets = new[] { 0, 2 };
                extraWeights = new[] { 2f, 0.6f };
                break;

            default:
                return false;
        }

        int remainingSteps = targetLength - 1 - SumGeneratedProfileSegments(segmentLengths);
        if (remainingSteps < 0)
        {
            return false;
        }

        DistributeGeneratedProfileExtras(segmentLengths, remainingSteps, extraTargets, extraWeights, rng);
        return turnSigns.Count == segmentLengths.Count - 1;
    }

    private static int SumGeneratedProfileSegments(List<int> segmentLengths)
    {
        int total = 0;
        for (int i = 0; i < segmentLengths.Count; i++)
        {
            total += segmentLengths[i];
        }

        return total;
    }

    private static void DistributeGeneratedProfileExtras(
        List<int> segmentLengths,
        int remainingSteps,
        int[] targetIndices,
        float[] weights,
        System.Random rng)
    {
        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            totalWeight += Mathf.Max(0f, weights[i]);
        }

        for (int step = 0; step < remainingSteps; step++)
        {
            double selection = rng.NextDouble() * totalWeight;
            int selectedIndex = targetIndices[targetIndices.Length - 1];
            for (int i = 0; i < targetIndices.Length; i++)
            {
                selection -= Mathf.Max(0f, weights[i]);
                if (selection <= 0d)
                {
                    selectedIndex = targetIndices[i];
                    break;
                }
            }

            segmentLengths[selectedIndex]++;
        }
    }

    private static bool TryBuildGeneratedProfilePath(
        Vector2Int start,
        Vector2Int firstDirection,
        int mirrorSign,
        List<int> segmentLengths,
        List<int> turnSigns,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet,
        out List<Vector2Int> pathCells)
    {
        pathCells = new List<Vector2Int> { start };
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start };
        Vector2Int current = start;
        Vector2Int direction = firstDirection;

        for (int segmentIndex = 0; segmentIndex < segmentLengths.Count; segmentIndex++)
        {
            for (int distance = 0; distance < segmentLengths[segmentIndex]; distance++)
            {
                Vector2Int next = current + direction;
                if (!zoneSet.Contains(next) || occupied.Contains(next) || !visited.Add(next))
                {
                    pathCells = null;
                    return false;
                }

                pathCells.Add(next);
                current = next;
            }

            if (segmentIndex < turnSigns.Count)
            {
                direction = RotateGeneratedDirection(direction, turnSigns[segmentIndex] * mirrorSign);
            }
        }

        return true;
    }

    private static Vector2Int GetGeneratedCardinalDirection(int index)
    {
        switch (index & 3)
        {
            case 0:
                return Vector2Int.right;
            case 1:
                return Vector2Int.up;
            case 2:
                return Vector2Int.left;
            default:
                return Vector2Int.down;
        }
    }

    private static Vector2Int RotateGeneratedDirection(Vector2Int direction, int turnSign)
    {
        return turnSign >= 0
            ? new Vector2Int(-direction.y, direction.x)
            : new Vector2Int(direction.y, -direction.x);
    }

    private bool TryCreateGeneratedSnakeArrow(
        Vector2Int start,
        int targetLength,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet,
        System.Random rng,
        out RuntimeArrowDraft arrow)
    {
        arrow = null;
        List<Vector2Int> pathCells = new List<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        CreateGeneratedPathProfile(
            targetLength,
            rng,
            out int targetTurnCount,
            out int preferredRunLength,
            out bool preferAlternatingTurns,
            out bool avoidUShape);

        if (!BuildGeneratedSnakePath(
            start,
            targetLength,
            occupied,
            zoneSet,
            rng,
            pathCells,
            visited,
            targetTurnCount,
            preferredRunLength,
            preferAlternatingTurns,
            avoidUShape))
        {
            return false;
        }

        List<Vector2Int> points = CompressGeneratedPath(pathCells);
        if (points.Count < 2)
        {
            return false;
        }

        if (PathArrowUtility.TryFindSelfOverlap(points, out _, out _)
            || PathArrowUtility.TryFindOwnExitBlock(points, width, height, zoneSet, out _))
        {
            return false;
        }

        HashSet<Vector2Int> arrowCells = new HashSet<Vector2Int>();
        if (!FillGeneratedCells(points, zoneSet, arrowCells))
        {
            return false;
        }

        foreach (Vector2Int cell in arrowCells)
        {
            if (occupied.Contains(cell))
            {
                return false;
            }
        }

        arrow = new RuntimeArrowDraft();
        arrow.points.AddRange(points);
        return true;
    }

    private void CreateGeneratedPathProfile(
        int targetLength,
        System.Random rng,
        out int targetTurnCount,
        out int preferredRunLength,
        out bool preferAlternatingTurns,
        out bool avoidUShape)
    {
        int maxUsefulTurns = Mathf.Max(0, targetLength - 2);
        double profileRoll = rng.NextDouble();
        preferAlternatingTurns = false;
        avoidUShape = false;

        if (maxUsefulTurns == 0 || profileRoll < 0.16)
        {
            targetTurnCount = 0;
            preferredRunLength = 3;
        }
        else if (profileRoll < 0.36)
        {
            targetTurnCount = 1;
            preferredRunLength = 2 + rng.Next(0, 2);
        }
        else if (profileRoll < 0.6)
        {
            targetTurnCount = Mathf.Min(2, maxUsefulTurns);
            preferredRunLength = 2 + rng.Next(0, 2);
            preferAlternatingTurns = true;
            avoidUShape = true;
        }
        else if (profileRoll < 0.84)
        {
            targetTurnCount = Mathf.Min(maxUsefulTurns, 3 + rng.Next(0, 3));
            preferredRunLength = 1 + rng.Next(0, 3);
            preferAlternatingTurns = true;
            avoidUShape = true;
        }
        else
        {
            targetTurnCount = Mathf.Min(maxUsefulTurns, 3 + rng.Next(0, 5));
            preferredRunLength = 1 + rng.Next(0, 3);
            avoidUShape = true;
        }

        preferredRunLength = Mathf.Clamp(preferredRunLength, 1, Mathf.Max(1, targetLength - 1));
    }

    private bool BuildGeneratedSnakePath(
        Vector2Int current,
        int targetLength,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet,
        System.Random rng,
        List<Vector2Int> pathCells,
        HashSet<Vector2Int> visited,
        int targetTurnCount,
        int preferredRunLength,
        bool preferAlternatingTurns,
        bool avoidUShape)
    {
        pathCells.Add(current);
        visited.Add(current);

        if (pathCells.Count >= targetLength)
        {
            return true;
        }

        List<Vector2Int> directions = new List<Vector2Int>
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };
        ShuffleList(directions, rng);
        if (pathCells.Count >= 2)
        {
            Vector2Int previousStep = current - pathCells[pathCells.Count - 2];
            Vector2Int reverseStep = new Vector2Int(-previousStep.x, -previousStep.y);
            GetGeneratedPathTurnState(pathCells, out int turnCount, out int currentRunLength, out int lastTurnSign);
            directions.Sort((a, b) =>
            {
                int aRank = GetGeneratedDirectionRank(
                    a,
                    previousStep,
                    reverseStep,
                    turnCount,
                    currentRunLength,
                    lastTurnSign,
                    targetTurnCount,
                    preferredRunLength,
                    preferAlternatingTurns,
                    avoidUShape);
                int bRank = GetGeneratedDirectionRank(
                    b,
                    previousStep,
                    reverseStep,
                    turnCount,
                    currentRunLength,
                    lastTurnSign,
                    targetTurnCount,
                    preferredRunLength,
                    preferAlternatingTurns,
                    avoidUShape);
                return aRank.CompareTo(bRank);
            });
        }

        for (int i = 0; i < directions.Count; i++)
        {
            Vector2Int next = current + directions[i];

            if (!zoneSet.Contains(next) || occupied.Contains(next) || visited.Contains(next))
            {
                continue;
            }

            if (BuildGeneratedSnakePath(
                next,
                targetLength,
                occupied,
                zoneSet,
                rng,
                pathCells,
                visited,
                targetTurnCount,
                preferredRunLength,
                preferAlternatingTurns,
                avoidUShape))
            {
                return true;
            }
        }

        pathCells.RemoveAt(pathCells.Count - 1);
        visited.Remove(current);
        return false;
    }

    private void GetGeneratedPathTurnState(
        IReadOnlyList<Vector2Int> pathCells,
        out int turnCount,
        out int currentRunLength,
        out int lastTurnSign)
    {
        turnCount = 0;
        currentRunLength = pathCells.Count >= 2 ? 1 : 0;
        lastTurnSign = 0;
        if (pathCells.Count < 3)
        {
            return;
        }

        Vector2Int previousStep = pathCells[1] - pathCells[0];
        for (int i = 2; i < pathCells.Count; i++)
        {
            Vector2Int step = pathCells[i] - pathCells[i - 1];
            if (step == previousStep)
            {
                currentRunLength++;
                continue;
            }

            turnCount++;
            currentRunLength = 1;
            lastTurnSign = Sign(previousStep.x * step.y - previousStep.y * step.x);
            previousStep = step;
        }
    }

    private int GetGeneratedDirectionRank(
        Vector2Int direction,
        Vector2Int previousStep,
        Vector2Int reverseStep,
        int turnCount,
        int currentRunLength,
        int lastTurnSign,
        int targetTurnCount,
        int preferredRunLength,
        bool preferAlternatingTurns,
        bool avoidUShape)
    {
        if (direction == reverseStep)
        {
            return 100;
        }

        if (direction == previousStep)
        {
            int straightRank = currentRunLength < preferredRunLength ? -5 : 0;
            if (turnCount >= targetTurnCount)
            {
                straightRank -= 4;
            }
            else if (currentRunLength >= preferredRunLength)
            {
                straightRank += 2;
            }

            return straightRank;
        }

        int turnRank = currentRunLength < preferredRunLength ? 6 : 0;
        turnRank += turnCount < targetTurnCount ? -5 : 5;
        int newTurnSign = Sign(previousStep.x * direction.y - previousStep.y * direction.x);

        if (lastTurnSign != 0)
        {
            if (preferAlternatingTurns)
            {
                turnRank += newTurnSign == -lastTurnSign ? -4 : 5;
            }

            if (avoidUShape && newTurnSign == lastTurnSign)
            {
                turnRank += 10;
            }
        }

        return turnRank;
    }

    private float CalculateGeneratedDiversityScore(
        RuntimeArrowDraft candidate,
        HashSet<Vector2Int> candidateCells,
        List<RuntimeArrowDraft> generatedArrows,
        bool candidateWasBlocked)
    {
        bool useProfileGuidance = (GeneratorAlgorithmMode)generatorAlgorithmModeIndex != GeneratorAlgorithmMode.Legacy;
        GeneratedArrowShape candidateShape = ClassifyGeneratedArrowShape(candidate);
        GeneratedArrowProfile candidateProfile = GetGeneratedArrowProfile(candidate);
        int candidateTurnCount = Mathf.Max(0, candidate.points.Count - 2);
        int[] shapeCounts = new int[(int)GeneratedArrowShape.Count];
        int[] profileCounts = new int[(int)GeneratedArrowProfile.Count];
        int[] directionCounts = new int[4];
        int repeatedTurnCount = 0;

        for (int i = 0; i < generatedArrows.Count; i++)
        {
            RuntimeArrowDraft existing = generatedArrows[i];
            shapeCounts[(int)ClassifyGeneratedArrowShape(existing)]++;
            if (useProfileGuidance)
            {
                profileCounts[(int)GetGeneratedArrowProfile(existing)]++;
            }
            int directionIndex = GetGeneratedDirectionIndex(GetGeneratedExitDirection(existing));
            if (directionIndex >= 0)
            {
                directionCounts[directionIndex]++;
            }

            if (Mathf.Max(0, existing.points.Count - 2) == candidateTurnCount)
            {
                repeatedTurnCount++;
            }
        }

        float score = candidateWasBlocked ? 5f : 0f;
        score -= shapeCounts[(int)candidateShape] * 6f;
        if (useProfileGuidance)
        {
            score -= profileCounts[(int)candidateProfile] * 4f;
        }
        score -= repeatedTurnCount * 1.5f;

        switch (candidateShape)
        {
            case GeneratedArrowShape.Straight:
                score += 2f;
                break;
            case GeneratedArrowShape.Bend:
                score += 5f;
                break;
            case GeneratedArrowShape.UShape:
                score -= 10f;
                break;
            case GeneratedArrowShape.Offset:
                score += 10f;
                break;
            case GeneratedArrowShape.Zigzag:
                score += 12f;
                break;
            case GeneratedArrowShape.Winding:
                score += 10f;
                break;
        }

        if (useProfileGuidance)
        {
            switch (candidateProfile)
            {
                case GeneratedArrowProfile.StraightRail:
                    score += 1f;
                    break;
                case GeneratedArrowProfile.LHook:
                    score += 3f;
                    break;
                case GeneratedArrowProfile.OffsetS:
                    score += 6f;
                    break;
                case GeneratedArrowProfile.Hairpin:
                    score -= 4f;
                    break;
                case GeneratedArrowProfile.OpenC:
                    score += 8f;
                    break;
                case GeneratedArrowProfile.RectangularSpiral:
                    score += 14f;
                    break;
                case GeneratedArrowProfile.Serpentine:
                    score += 12f;
                    break;
                case GeneratedArrowProfile.Staircase:
                    score += 10f;
                    break;
                case GeneratedArrowProfile.PerimeterRunner:
                    score += 12f;
                    break;
                case GeneratedArrowProfile.LongSpine:
                    score += 9f;
                    break;
                case GeneratedArrowProfile.OrganicWinding:
                    score += 5f;
                    break;
            }
        }

        if (candidateShape == GeneratedArrowShape.UShape)
        {
            int projectedArrowCount = generatedArrows.Count + 1;
            int allowedUShapes = Mathf.Max(1, Mathf.CeilToInt(projectedArrowCount * GeneratorMaxUShapeRatio));
            int projectedUShapes = shapeCounts[(int)GeneratedArrowShape.UShape] + 1;
            if (projectedUShapes > allowedUShapes)
            {
                score -= (projectedUShapes - allowedUShapes) * 140f;
            }
        }

        if (!useProfileGuidance || !IsIntentionalSameHandedProfile(candidateProfile))
        {
            score -= CountGeneratedUShapeMotifs(candidate) * 18f;
        }

        int candidateDirectionIndex = GetGeneratedDirectionIndex(GetGeneratedExitDirection(candidate));
        if (candidateDirectionIndex >= 0)
        {
            int leastUsedDirectionCount = directionCounts[0];
            for (int i = 1; i < directionCounts.Length; i++)
            {
                leastUsedDirectionCount = Mathf.Min(leastUsedDirectionCount, directionCounts[i]);
            }

            score -= directionCounts[candidateDirectionIndex] * 4f;
            if (directionCounts[candidateDirectionIndex] == leastUsedDirectionCount)
            {
                score += 8f;
            }
        }

        GetGeneratedSegmentLengthProfile(candidate, out int shortestSegment, out int longestSegment, out int unitSegmentCount);
        if (longestSegment >= 3)
        {
            score += 4f;
        }

        if (longestSegment - shortestSegment >= 2)
        {
            score += 6f;
        }

        if (candidate.points.Count > 3 && unitSegmentCount == candidate.points.Count - 1)
        {
            score -= 8f;
        }

        GetGeneratedArrowBounds(candidate, out Vector2Int candidateMin, out Vector2Int candidateMax);
        int recentStart = Mathf.Max(0, generatedArrows.Count - 8);
        for (int i = 0; i < generatedArrows.Count; i++)
        {
            RuntimeArrowDraft existing = generatedArrows[i];
            GeneratedArrowShape existingShape = ClassifyGeneratedArrowShape(existing);
            if (existingShape != candidateShape)
            {
                continue;
            }

            GetGeneratedArrowBounds(existing, out Vector2Int existingMin, out Vector2Int existingMax);
            int boundsDistance = GetGeneratedBoundsDistance(candidateMin, candidateMax, existingMin, existingMax);
            if (boundsDistance <= GeneratorNearbyShapeDistance)
            {
                score -= candidateShape == GeneratedArrowShape.UShape ? 24f : 12f;
            }

            if (i >= recentStart)
            {
                score -= 8f;
            }
        }

        if (candidateCells != null)
        {
            score += Mathf.Min(8, candidateCells.Count) * 0.15f;
        }

        return score;
    }

    private GeneratedArrowProfile GetGeneratedArrowProfile(RuntimeArrowDraft arrow)
    {
        if (arrow == null || arrow.points.Count < 2)
        {
            return GeneratedArrowProfile.Unknown;
        }

        if (arrow.generatedProfile != GeneratedArrowProfile.Unknown)
        {
            return arrow.generatedProfile;
        }

        int segmentCount = arrow.points.Count - 1;
        if (segmentCount == 1)
        {
            return GeneratedArrowProfile.StraightRail;
        }

        if (segmentCount == 2)
        {
            return GeneratedArrowProfile.LHook;
        }

        int alternatingTurns = 0;
        int sameHandedTurns = 0;
        int previousTurnSign = 0;
        Vector2Int previousDirection = GetGeneratedSegmentDirection(arrow.points[0], arrow.points[1]);
        for (int i = 1; i < segmentCount; i++)
        {
            Vector2Int direction = GetGeneratedSegmentDirection(arrow.points[i], arrow.points[i + 1]);
            int turnSign = Sign(previousDirection.x * direction.y - previousDirection.y * direction.x);
            if (previousTurnSign != 0)
            {
                if (turnSign == previousTurnSign)
                {
                    sameHandedTurns++;
                }
                else
                {
                    alternatingTurns++;
                }
            }

            previousTurnSign = turnSign;
            previousDirection = direction;
        }

        if (segmentCount == 3)
        {
            return sameHandedTurns > 0
                ? GeneratedArrowProfile.Hairpin
                : GeneratedArrowProfile.OffsetS;
        }

        if (sameHandedTurns == segmentCount - 2)
        {
            return segmentCount >= 5
                ? GeneratedArrowProfile.RectangularSpiral
                : GeneratedArrowProfile.OpenC;
        }

        GetGeneratedSegmentLengthProfile(arrow, out int shortestSegment, out int longestSegment, out int unitSegmentCount);
        if (longestSegment >= Mathf.Max(4, shortestSegment * 3))
        {
            return GeneratedArrowProfile.LongSpine;
        }

        if (alternatingTurns > sameHandedTurns)
        {
            return longestSegment <= 2 && unitSegmentCount >= segmentCount / 2
                ? GeneratedArrowProfile.Staircase
                : GeneratedArrowProfile.Serpentine;
        }

        return GeneratedArrowProfile.OrganicWinding;
    }

    private static bool IsIntentionalSameHandedProfile(GeneratedArrowProfile profile)
    {
        return profile == GeneratedArrowProfile.OpenC
            || profile == GeneratedArrowProfile.RectangularSpiral
            || profile == GeneratedArrowProfile.Serpentine
            || profile == GeneratedArrowProfile.PerimeterRunner;
    }

    private GeneratedArrowShape ClassifyGeneratedArrowShape(RuntimeArrowDraft arrow)
    {
        if (arrow == null || arrow.points.Count < 2)
        {
            return GeneratedArrowShape.Straight;
        }

        int segmentCount = arrow.points.Count - 1;
        if (segmentCount == 1)
        {
            return GeneratedArrowShape.Straight;
        }

        if (segmentCount == 2)
        {
            return GeneratedArrowShape.Bend;
        }

        int alternatingTurns = 0;
        int sameHandedTurns = 0;
        int previousTurnSign = 0;
        Vector2Int previousDirection = GetGeneratedSegmentDirection(arrow.points[0], arrow.points[1]);

        for (int i = 1; i < segmentCount; i++)
        {
            Vector2Int direction = GetGeneratedSegmentDirection(arrow.points[i], arrow.points[i + 1]);
            int turnSign = Sign(previousDirection.x * direction.y - previousDirection.y * direction.x);
            if (previousTurnSign != 0)
            {
                if (turnSign == previousTurnSign)
                {
                    sameHandedTurns++;
                }
                else
                {
                    alternatingTurns++;
                }
            }

            previousTurnSign = turnSign;
            previousDirection = direction;
        }

        if (segmentCount == 3)
        {
            return sameHandedTurns > 0 ? GeneratedArrowShape.UShape : GeneratedArrowShape.Offset;
        }

        return alternatingTurns > sameHandedTurns
            ? GeneratedArrowShape.Zigzag
            : GeneratedArrowShape.Winding;
    }

    private int CountGeneratedUShapeMotifs(RuntimeArrowDraft arrow)
    {
        if (arrow == null || arrow.points.Count < 4)
        {
            return 0;
        }

        int motifCount = 0;
        int previousTurnSign = 0;
        Vector2Int previousDirection = GetGeneratedSegmentDirection(arrow.points[0], arrow.points[1]);
        for (int i = 1; i < arrow.points.Count - 1; i++)
        {
            Vector2Int direction = GetGeneratedSegmentDirection(arrow.points[i], arrow.points[i + 1]);
            int turnSign = Sign(previousDirection.x * direction.y - previousDirection.y * direction.x);
            if (previousTurnSign != 0 && turnSign == previousTurnSign)
            {
                motifCount++;
            }

            previousTurnSign = turnSign;
            previousDirection = direction;
        }

        return motifCount;
    }

    private Vector2Int GetGeneratedSegmentDirection(Vector2Int start, Vector2Int end)
    {
        Vector2Int delta = end - start;
        return new Vector2Int(Sign(delta.x), Sign(delta.y));
    }

    private int GetGeneratedDirectionIndex(Vector2Int direction)
    {
        if (direction == Vector2Int.right) return 0;
        if (direction == Vector2Int.left) return 1;
        if (direction == Vector2Int.up) return 2;
        if (direction == Vector2Int.down) return 3;
        return -1;
    }

    private void GetGeneratedSegmentLengthProfile(
        RuntimeArrowDraft arrow,
        out int shortestSegment,
        out int longestSegment,
        out int unitSegmentCount)
    {
        shortestSegment = int.MaxValue;
        longestSegment = 0;
        unitSegmentCount = 0;

        for (int i = 0; i < arrow.points.Count - 1; i++)
        {
            Vector2Int delta = arrow.points[i + 1] - arrow.points[i];
            int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            shortestSegment = Mathf.Min(shortestSegment, length);
            longestSegment = Mathf.Max(longestSegment, length);
            if (length == 1)
            {
                unitSegmentCount++;
            }
        }

        if (shortestSegment == int.MaxValue)
        {
            shortestSegment = 0;
        }
    }

    private void GetGeneratedArrowBounds(RuntimeArrowDraft arrow, out Vector2Int minimum, out Vector2Int maximum)
    {
        minimum = arrow.points[0];
        maximum = arrow.points[0];
        for (int i = 1; i < arrow.points.Count; i++)
        {
            Vector2Int point = arrow.points[i];
            minimum = new Vector2Int(Mathf.Min(minimum.x, point.x), Mathf.Min(minimum.y, point.y));
            maximum = new Vector2Int(Mathf.Max(maximum.x, point.x), Mathf.Max(maximum.y, point.y));
        }
    }

    private int GetGeneratedBoundsDistance(
        Vector2Int firstMin,
        Vector2Int firstMax,
        Vector2Int secondMin,
        Vector2Int secondMax)
    {
        int xDistance = firstMax.x < secondMin.x
            ? secondMin.x - firstMax.x
            : (secondMax.x < firstMin.x ? firstMin.x - secondMax.x : 0);
        int yDistance = firstMax.y < secondMin.y
            ? secondMin.y - firstMax.y
            : (secondMax.y < firstMin.y ? firstMin.y - secondMax.y : 0);
        return xDistance + yDistance;
    }

    private List<Vector2Int> CompressGeneratedPath(List<Vector2Int> pathCells)
    {
        List<Vector2Int> points = new List<Vector2Int>();
        if (pathCells == null || pathCells.Count == 0)
        {
            return points;
        }

        points.Add(pathCells[0]);

        for (int i = 1; i < pathCells.Count - 1; i++)
        {
            Vector2Int before = pathCells[i] - pathCells[i - 1];
            Vector2Int after = pathCells[i + 1] - pathCells[i];
            if (before != after)
            {
                points.Add(pathCells[i]);
            }
        }

        if (pathCells.Count > 1)
        {
            points.Add(pathCells[pathCells.Count - 1]);
        }

        return points;
    }

    private int CountGeneratedPlayableArrows(List<RuntimeArrowDraft> sourceArrows, HashSet<Vector2Int> zoneSet)
    {
        if (!TryMeasureGeneratedSolveProfile(
            sourceArrows,
            zoneSet,
            out int initialPlayableCount,
            out _,
            out _,
            out _))
        {
            return int.MaxValue;
        }

        return initialPlayableCount;
    }

    private bool TryMeasureGeneratedSolveProfile(
        List<RuntimeArrowDraft> sourceArrows,
        HashSet<Vector2Int> zoneSet,
        out int initialPlayableCount,
        out int maxPlayableCount,
        out int overTwoRouteMoveCount,
        out float averagePlayableCount)
    {
        initialPlayableCount = 0;
        maxPlayableCount = 0;
        overTwoRouteMoveCount = 0;
        averagePlayableCount = 0f;

        List<TestArrow> testArrows = new List<TestArrow>();
        Dictionary<Vector2Int, TestArrow> occupied = new Dictionary<Vector2Int, TestArrow>();
        if (!TryBuildGeneratedTestState(sourceArrows, zoneSet, testArrows, occupied))
        {
            return false;
        }

        HashSet<int> removed = new HashSet<int>();
        int totalPlayableCount = 0;
        int moveCount = 0;

        while (removed.Count < testArrows.Count)
        {
            TestArrow escaped = null;
            int playableCount = 0;

            for (int i = 0; i < testArrows.Count; i++)
            {
                TestArrow arrow = testArrows[i];
                if (removed.Contains(arrow.index) || !CanGeneratedArrowEscape(arrow, occupied, removed, zoneSet))
                {
                    continue;
                }

                if (escaped == null)
                {
                    escaped = arrow;
                }

                playableCount++;
            }

            if (playableCount == 0 || escaped == null)
            {
                return false;
            }

            if (moveCount == 0)
            {
                initialPlayableCount = playableCount;
            }

            maxPlayableCount = Mathf.Max(maxPlayableCount, playableCount);
            if (playableCount > GeneratorTargetMaxPlayableRoutes)
            {
                overTwoRouteMoveCount++;
            }

            totalPlayableCount += playableCount;
            moveCount++;
            removed.Add(escaped.index);

            foreach (Vector2Int cell in escaped.cells)
            {
                occupied.Remove(cell);
            }
        }

        averagePlayableCount = moveCount > 0 ? totalPlayableCount / (float)moveCount : 0f;
        return true;
    }

    // Removes every currently playable arrow as one wave. This produces an
    // order-independent view of the dependency graph: narrow, deep waves are
    // generally more deliberate than a shallow wave that unlocks many arrows.
    private bool TryMeasureGeneratedDependencyProfile(
        List<RuntimeArrowDraft> sourceArrows,
        HashSet<Vector2Int> zoneSet,
        out int dependencyDepth,
        out int maxUnlockWaveCount,
        out int overTwoUnlockWaveCount,
        out int controlledBurstWaveCount,
        out int gateWaveTransitionCount,
        out int oversizedUnlockWaveCount,
        out float singleArrowWaveRatio)
    {
        dependencyDepth = 0;
        maxUnlockWaveCount = 0;
        overTwoUnlockWaveCount = 0;
        controlledBurstWaveCount = 0;
        gateWaveTransitionCount = 0;
        oversizedUnlockWaveCount = 0;
        singleArrowWaveRatio = 0f;

        List<TestArrow> testArrows = new List<TestArrow>();
        Dictionary<Vector2Int, TestArrow> occupied = new Dictionary<Vector2Int, TestArrow>();
        if (!TryBuildGeneratedTestState(sourceArrows, zoneSet, testArrows, occupied))
        {
            return false;
        }

        HashSet<int> removed = new HashSet<int>();
        List<TestArrow> currentWave = new List<TestArrow>();
        int singleArrowWaveCount = 0;
        int previousWaveCategory = 0;

        while (removed.Count < testArrows.Count)
        {
            currentWave.Clear();

            for (int i = 0; i < testArrows.Count; i++)
            {
                TestArrow arrow = testArrows[i];
                if (!removed.Contains(arrow.index) && CanGeneratedArrowEscape(arrow, occupied, removed, zoneSet))
                {
                    currentWave.Add(arrow);
                }
            }

            if (currentWave.Count == 0)
            {
                return false;
            }

            dependencyDepth++;
            maxUnlockWaveCount = Mathf.Max(maxUnlockWaveCount, currentWave.Count);
            if (currentWave.Count > GeneratorTargetMaxPlayableRoutes)
            {
                overTwoUnlockWaveCount++;
            }

            if (currentWave.Count == 1)
            {
                singleArrowWaveCount++;
            }
            else if (currentWave.Count <= GeneratorGateBurstRouteCeiling)
            {
                controlledBurstWaveCount++;
            }
            else
            {
                oversizedUnlockWaveCount++;
            }

            int waveCategory = currentWave.Count == 1
                ? 1
                : (currentWave.Count <= GeneratorGateBurstRouteCeiling ? 2 : 3);
            if (previousWaveCategory > 0
                && previousWaveCategory <= 2
                && waveCategory <= 2
                && previousWaveCategory != waveCategory)
            {
                gateWaveTransitionCount++;
            }

            previousWaveCategory = waveCategory;

            for (int i = 0; i < currentWave.Count; i++)
            {
                TestArrow escaped = currentWave[i];
                removed.Add(escaped.index);
                foreach (Vector2Int cell in escaped.cells)
                {
                    occupied.Remove(cell);
                }
            }
        }

        singleArrowWaveRatio = dependencyDepth > 0
            ? singleArrowWaveCount / (float)dependencyDepth
            : 0f;

        return true;
    }

    // Matches the incremental strategy used by the referenced generator: place a
    // candidate temporarily and reject it immediately if the whole board deadlocks.
    // Unlike the older reverse-only rule, the candidate itself may initially be blocked.
    private bool TryEvaluateGeneratedCandidate(
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet,
        RuntimeArrowDraft candidateArrow,
        HashSet<Vector2Int> candidateCells,
        out int playableRoutesAfterPlacement,
        out int futureMaxPlayableRoutes,
        out bool candidateWasBlocked,
        out int playableHeadDistanceAfterPlacement)
    {
        playableRoutesAfterPlacement = int.MaxValue;
        futureMaxPlayableRoutes = int.MaxValue;
        playableHeadDistanceAfterPlacement = int.MaxValue;
        candidateWasBlocked = !CanGeneratedArrowEscapeThroughOccupied(candidateArrow, occupied, zoneSet);
        List<Vector2Int> addedCells = new List<Vector2Int>(candidateCells.Count);

        generatedArrows.Add(candidateArrow);
        foreach (Vector2Int cell in candidateCells)
        {
            if (!occupied.Add(cell))
            {
                foreach (Vector2Int addedCell in addedCells)
                {
                    occupied.Remove(addedCell);
                }

                generatedArrows.RemoveAt(generatedArrows.Count - 1);
                return false;
            }

            addedCells.Add(cell);
        }

        bool useAdvancedGuided = IsAdvancedGuidedMode((GeneratorAlgorithmMode)generatorAlgorithmModeIndex);
        bool remainsSolvable;
        if (useAdvancedGuided)
        {
            remainsSolvable = TryMeasureGeneratedSolveProfile(
                generatedArrows,
                zoneSet,
                out playableRoutesAfterPlacement,
                out futureMaxPlayableRoutes,
                out _,
                out _);
        }
        else
        {
            remainsSolvable = CanGeneratedLevelSolve(generatedArrows, zoneSet);
            if (remainsSolvable)
            {
                playableRoutesAfterPlacement = CountGeneratedCurrentPlayableArrows(generatedArrows, occupied, zoneSet);
                futureMaxPlayableRoutes = playableRoutesAfterPlacement;
            }
        }

        if (remainsSolvable)
        {
            playableHeadDistanceAfterPlacement = GetGeneratedMinimumPlayableHeadDistance(
                generatedArrows,
                occupied,
                zoneSet);

            if ((GeneratorAlgorithmMode)generatorAlgorithmModeIndex == GeneratorAlgorithmMode.LockstepWeave
                && !HasGeneratedDecisionSpacingAcrossAllWaves(generatedArrows, zoneSet))
            {
                remainsSolvable = false;
            }
        }

        foreach (Vector2Int addedCell in addedCells)
        {
            occupied.Remove(addedCell);
        }

        generatedArrows.RemoveAt(generatedArrows.Count - 1);
        return remainsSolvable;
    }

    private int GetGeneratedMinimumPlayableHeadDistance(
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> zoneSet)
    {
        List<Vector2Int> playableHeads = new List<Vector2Int>();
        for (int arrowIndex = 0; arrowIndex < generatedArrows.Count; arrowIndex++)
        {
            RuntimeArrowDraft arrow = generatedArrows[arrowIndex];
            if (CanGeneratedArrowEscapeThroughOccupied(arrow, occupied, zoneSet))
            {
                playableHeads.Add(arrow.points[arrow.points.Count - 1]);
            }
        }

        if (playableHeads.Count < 2)
        {
            return int.MaxValue;
        }

        int minimumDistance = int.MaxValue;
        for (int first = 0; first < playableHeads.Count - 1; first++)
        {
            for (int second = first + 1; second < playableHeads.Count; second++)
            {
                Vector2Int delta = playableHeads[first] - playableHeads[second];
                minimumDistance = Mathf.Min(minimumDistance, Mathf.Abs(delta.x) + Mathf.Abs(delta.y));
            }
        }

        return minimumDistance;
    }

    // Checks every reachable player decision state, rather than only removing
    // whole waves at once. This prevents a two-choice state from briefly opening
    // a third route when the player clears either choice first.
    private bool HasGeneratedDecisionSpacingAcrossAllWaves(
        List<RuntimeArrowDraft> generatedArrows,
        HashSet<Vector2Int> zoneSet,
        bool requireSpatialSpacing = true)
    {
        List<TestArrow> testArrows = new List<TestArrow>();
        Dictionary<Vector2Int, TestArrow> occupied = new Dictionary<Vector2Int, TestArrow>();
        if (!TryBuildGeneratedTestState(generatedArrows, zoneSet, testArrows, occupied))
        {
            return false;
        }

        if (testArrows.Count > 63)
        {
            return HasGeneratedDecisionSpacingAcrossWaveFronts(
                testArrows,
                occupied,
                zoneSet,
                requireSpatialSpacing);
        }

        int minimumAllowedDistance = requireSpatialSpacing
            ? GetGeneratedDecisionChoiceDistanceThreshold(zoneSet)
            : 0;
        ulong allRemoved = testArrows.Count == 0
            ? 0UL
            : (1UL << testArrows.Count) - 1UL;
        Queue<ulong> pendingStates = new Queue<ulong>();
        HashSet<ulong> visitedStates = new HashSet<ulong>();
        pendingStates.Enqueue(0UL);
        visitedStates.Add(0UL);

        while (pendingStates.Count > 0)
        {
            ulong removedMask = pendingStates.Dequeue();
            if (removedMask == allRemoved)
            {
                continue;
            }

            List<TestArrow> playable = new List<TestArrow>(GeneratorTargetMaxPlayableRoutes + 1);
            for (int arrowIndex = 0; arrowIndex < testArrows.Count; arrowIndex++)
            {
                ulong arrowBit = 1UL << arrowIndex;
                if ((removedMask & arrowBit) == 0UL
                    && CanGeneratedArrowEscape(testArrows[arrowIndex], occupied, removedMask, zoneSet))
                {
                    playable.Add(testArrows[arrowIndex]);
                    if (playable.Count > GeneratorTargetMaxPlayableRoutes)
                    {
                        return false;
                    }
                }
            }

            if (playable.Count == 0)
            {
                return false;
            }

            if (requireSpatialSpacing && playable.Count > 1)
            {
                Vector2Int delta = playable[0].head - playable[1].head;
                int headDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
                if (headDistance < minimumAllowedDistance)
                {
                    return false;
                }
            }

            for (int choiceIndex = 0; choiceIndex < playable.Count; choiceIndex++)
            {
                ulong nextState = removedMask | (1UL << playable[choiceIndex].index);
                if (visitedStates.Add(nextState))
                {
                    if (visitedStates.Count > GeneratorDecisionStateBudget)
                    {
                        return false;
                    }

                    pendingStates.Enqueue(nextState);
                }
            }
        }

        return true;
    }

    // Very large generated boards fall back to the linear wave check so the
    // editor never allocates an unbounded decision-state graph.
    private bool HasGeneratedDecisionSpacingAcrossWaveFronts(
        List<TestArrow> testArrows,
        Dictionary<Vector2Int, TestArrow> occupied,
        HashSet<Vector2Int> zoneSet,
        bool requireSpatialSpacing)
    {
        int minimumAllowedDistance = requireSpatialSpacing
            ? GetGeneratedDecisionChoiceDistanceThreshold(zoneSet)
            : 0;
        HashSet<int> removed = new HashSet<int>();
        List<TestArrow> currentWave = new List<TestArrow>();
        while (removed.Count < testArrows.Count)
        {
            currentWave.Clear();
            for (int arrowIndex = 0; arrowIndex < testArrows.Count; arrowIndex++)
            {
                TestArrow arrow = testArrows[arrowIndex];
                if (!removed.Contains(arrow.index)
                    && CanGeneratedArrowEscape(arrow, occupied, removed, zoneSet))
                {
                    currentWave.Add(arrow);
                }
            }

            if (currentWave.Count == 0 || currentWave.Count > GeneratorTargetMaxPlayableRoutes)
            {
                return false;
            }

            if (requireSpatialSpacing && currentWave.Count > 1)
            {
                Vector2Int delta = currentWave[0].head - currentWave[1].head;
                int headDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
                if (headDistance < minimumAllowedDistance)
                {
                    return false;
                }
            }

            foreach (TestArrow escaped in currentWave)
            {
                removed.Add(escaped.index);
                foreach (Vector2Int cell in escaped.cells)
                {
                    occupied.Remove(cell);
                }
            }
        }

        return true;
    }

    private bool CanGeneratedArrowEscape(
        TestArrow arrow,
        Dictionary<Vector2Int, TestArrow> occupied,
        ulong removedMask,
        HashSet<Vector2Int> zoneSet)
    {
        Vector2Int checkPosition = arrow.head + arrow.exitDirection;
        while (IsInsideGridBounds(checkPosition))
        {
            if (occupied.TryGetValue(checkPosition, out TestArrow blocker)
                && (removedMask & (1UL << blocker.index)) == 0UL)
            {
                return false;
            }

            checkPosition += arrow.exitDirection;
        }

        return true;
    }

    private bool CanGeneratedLevelSolve(List<RuntimeArrowDraft> sourceArrows, HashSet<Vector2Int> zoneSet)
    {
        List<TestArrow> testArrows = new List<TestArrow>();
        Dictionary<Vector2Int, TestArrow> occupied = new Dictionary<Vector2Int, TestArrow>();

        if (!TryBuildGeneratedTestState(sourceArrows, zoneSet, testArrows, occupied))
        {
            return false;
        }

        HashSet<int> removed = new HashSet<int>();
        while (removed.Count < testArrows.Count)
        {
            TestArrow escaped = null;

            for (int i = 0; i < testArrows.Count; i++)
            {
                TestArrow arrow = testArrows[i];
                if (!removed.Contains(arrow.index) && CanGeneratedArrowEscape(arrow, occupied, removed, zoneSet))
                {
                    escaped = arrow;
                    break;
                }
            }

            if (escaped == null)
            {
                return false;
            }

            removed.Add(escaped.index);
            foreach (Vector2Int cell in escaped.cells)
            {
                occupied.Remove(cell);
            }
        }

        return true;
    }

    private bool TryBuildGeneratedTestState(
        List<RuntimeArrowDraft> sourceArrows,
        HashSet<Vector2Int> zoneSet,
        List<TestArrow> testArrows,
        Dictionary<Vector2Int, TestArrow> occupied)
    {
        testArrows.Clear();
        occupied.Clear();

        for (int i = 0; i < sourceArrows.Count; i++)
        {
            RuntimeArrowDraft source = sourceArrows[i];
            if (source.points.Count < 2)
            {
                return false;
            }

            Vector2Int head = source.points[source.points.Count - 1];
            Vector2Int previous = source.points[source.points.Count - 2];
            Vector2Int exitDelta = head - previous;
            if (exitDelta == Vector2Int.zero || (exitDelta.x != 0 && exitDelta.y != 0))
            {
                return false;
            }

            Vector2Int exitDirection = new Vector2Int(Sign(exitDelta.x), Sign(exitDelta.y));
            if (exitDirection == Vector2Int.zero)
            {
                return false;
            }

            TestArrow testArrow = new TestArrow
            {
                index = i,
                name = string.IsNullOrWhiteSpace(source.id) ? $"Arrow {i + 1}" : source.id,
                head = head,
                exitDirection = exitDirection
            };
            testArrow.points.AddRange(source.points);

            if (!FillGeneratedCells(source.points, zoneSet, testArrow.cells))
            {
                return false;
            }

            foreach (Vector2Int cell in testArrow.cells)
            {
                if (occupied.ContainsKey(cell))
                {
                    return false;
                }

                occupied[cell] = testArrow;
            }

            testArrows.Add(testArrow);
        }

        return true;
    }

    private bool CanGeneratedArrowEscape(
        TestArrow arrow,
        Dictionary<Vector2Int, TestArrow> occupied,
        HashSet<int> removed,
        HashSet<Vector2Int> zoneSet)
    {
        Vector2Int checkPosition = arrow.head + arrow.exitDirection;

        while (IsInsideGridBounds(checkPosition))
        {
            if (occupied.TryGetValue(checkPosition, out TestArrow blocker) && !removed.Contains(blocker.index))
            {
                return false;
            }

            checkPosition += arrow.exitDirection;
        }

        return true;
    }

    private bool FillGeneratedCells(IReadOnlyList<Vector2Int> points, HashSet<Vector2Int> zoneSet, HashSet<Vector2Int> cells)
    {
        cells.Clear();

        if (points == null || points.Count < 2)
        {
            return false;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2Int start = points[i];
            Vector2Int end = points[i + 1];
            Vector2Int delta = end - start;

            if (delta == Vector2Int.zero || (delta.x != 0 && delta.y != 0))
            {
                return false;
            }

            Vector2Int step = new Vector2Int(Sign(delta.x), Sign(delta.y));
            int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            int firstDistance = i == 0 ? 0 : 1;

            for (int distance = firstDistance; distance <= length; distance++)
            {
                Vector2Int cell = start + step * distance;
                if (!zoneSet.Contains(cell) || !cells.Add(cell))
                {
                    return false;
                }
            }
        }

        return cells.Count > 0;
    }

    private bool TryCollectGeneratedOccupiedCells(List<RuntimeArrowDraft> sourceArrows, HashSet<Vector2Int> zoneSet, HashSet<Vector2Int> occupied)
    {
        occupied.Clear();

        for (int i = 0; i < sourceArrows.Count; i++)
        {
            HashSet<Vector2Int> arrowCells = new HashSet<Vector2Int>();
            if (!FillGeneratedCells(sourceArrows[i].points, zoneSet, arrowCells))
            {
                return false;
            }

            foreach (Vector2Int cell in arrowCells)
            {
                if (!occupied.Add(cell))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private List<Vector2Int> GetFreeGeneratorCells(List<Vector2Int> zoneCells, HashSet<Vector2Int> occupied)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        for (int i = 0; i < zoneCells.Count; i++)
        {
            if (!occupied.Contains(zoneCells[i]))
            {
                result.Add(zoneCells[i]);
            }
        }

        return result;
    }

    private List<RuntimeArrowDraft> CloneRuntimeArrows(List<RuntimeArrowDraft> sourceArrows)
    {
        List<RuntimeArrowDraft> result = new List<RuntimeArrowDraft>();
        for (int i = 0; i < sourceArrows.Count; i++)
        {
            RuntimeArrowDraft clone = new RuntimeArrowDraft
            {
                id = sourceArrows[i].id,
                color = sourceArrows[i].color,
                generatedProfile = sourceArrows[i].generatedProfile
            };
            clone.points.AddRange(sourceArrows[i].points);
            result.Add(clone);
        }

        return result;
    }

    private Color GetGeneratedSavedArrowColor()
    {
        return WithFullAlpha(CurrentTheme.PreviewArrow);
    }

    private Color GetEditorPreviewArrowColor(int index)
    {
        switch (Mathf.Clamp(generatorColorModeIndex, 0, GeneratorColorModeNames.Length - 1))
        {
            case 0:
                return WithFullAlpha(CurrentTheme.PreviewArrow);
            case 2:
                return Color.HSVToRGB(Mathf.Repeat(index * 0.173f, 1f), 0.42f, 1f);
            case 3:
                return GetGeneratedContrastColor(index);
            default:
                return Color.HSVToRGB(Mathf.Repeat(index * 0.137f, 1f), 0.72f, 0.96f);
        }
    }

    private Color GetGeneratedContrastColor(int index)
    {
        switch (index % 8)
        {
            case 0: return new Color(1f, 0.85f, 0.05f, 1f);
            case 1: return new Color(0.1f, 0.85f, 1f, 1f);
            case 2: return new Color(1f, 0.25f, 0.35f, 1f);
            case 3: return new Color(0.25f, 1f, 0.45f, 1f);
            case 4: return new Color(0.95f, 0.45f, 1f, 1f);
            case 5: return new Color(1f, 0.55f, 0.05f, 1f);
            case 6: return new Color(0.55f, 0.65f, 1f, 1f);
            default: return new Color(0.95f, 0.95f, 0.95f, 1f);
        }
    }

    private Color WithFullAlpha(Color color)
    {
        color.a = 1f;
        return color;
    }

    private int ParseGeneratorInt(string text, int fallback, int min, int max)
    {
        if (!int.TryParse(text, out int value))
        {
            value = fallback;
        }

        return Mathf.Clamp(value, min, max);
    }

    private void ShuffleList<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private bool WouldNewSegmentSelfOverlap(RuntimeArrowDraft arrow, Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> candidatePoints = new List<Vector2Int>(arrow.points);

        if (candidatePoints.Count == 0 || candidatePoints[candidatePoints.Count - 1] != start)
        {
            candidatePoints.Add(start);
        }

        candidatePoints.Add(end);
        return PathArrowUtility.TryFindSelfOverlap(candidatePoints, out _, out _);
    }

    private void HandleArrowDrawInput(Event currentEvent, Rect gridBackground)
    {
        if (editBoardShape)
        {
            if (isArrowDrawDragging)
            {
                EndArrowDraw();
            }

            return;
        }

        if (currentEvent.type == EventType.MouseDown
            && currentEvent.button == 0
            && TryGetGridCellAtMousePosition(currentEvent.mousePosition, gridBackground, out Vector2Int startCell))
        {
            ClearEditorTextInputFocus();

            if (TrySelectArrowFromGridCell(startCell))
            {
                currentEvent.Use();
                return;
            }

            BeginArrowDraw(startCell);
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && isArrowDrawDragging)
        {
            if (TryGetGridCellAtMousePosition(currentEvent.mousePosition, gridBackground, out Vector2Int dragCell))
            {
                ContinueArrowDraw(dragCell);
            }

            currentEvent.Use();
            return;
        }

        if ((currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            || currentEvent.type == EventType.MouseLeaveWindow)
        {
            if (isArrowDrawDragging)
            {
                EndArrowDraw();
                currentEvent.Use();
            }
        }
    }

    private bool TrySelectArrowFromGridCell(Vector2Int cell)
    {
        if (!occupiedCellOwners.TryGetValue(cell, out int ownerIndex)
            || ownerIndex < 0
            || ownerIndex >= arrows.Count
            || ownerIndex == selectedArrowIndex)
        {
            return false;
        }

        selectedArrowIndex = ownerIndex;
        testerHighlightedArrowIndex = -1;
        statusMessage = $"Selected {GetArrowDisplayName(ownerIndex)} from the grid.";
        return true;
    }

    private void BeginArrowDraw(Vector2Int cell)
    {
        arrowDrawTrailCells.Clear();
        arrowDrawRecordedHistory = false;
        arrowDrawLastCell = cell;
        isArrowDrawDragging = true;

        if (!TryAppendArrowDrawCell(cell))
        {
            isArrowDrawDragging = false;
            arrowDrawTrailCells.Clear();
            return;
        }

        arrowDrawLastCell = cell;
    }

    private void ContinueArrowDraw(Vector2Int targetCell)
    {
        Vector2Int current = arrowDrawLastCell;

        while (current.x != targetCell.x)
        {
            Vector2Int next = current + new Vector2Int(Sign(targetCell.x - current.x), 0);
            if (!TryAppendArrowDrawCell(next))
            {
                return;
            }

            current = next;
            arrowDrawLastCell = current;
        }

        while (current.y != targetCell.y)
        {
            Vector2Int next = current + new Vector2Int(0, Sign(targetCell.y - current.y));
            if (!TryAppendArrowDrawCell(next))
            {
                return;
            }

            current = next;
            arrowDrawLastCell = current;
        }
    }

    private void EndArrowDraw()
    {
        isArrowDrawDragging = false;
        arrowDrawRecordedHistory = false;
        arrowDrawTrailCells.Clear();
    }

    private void EnsureArrowDrawHistoryRecorded()
    {
        if (arrowDrawRecordedHistory)
        {
            return;
        }

        RecordHistory();
        arrowDrawRecordedHistory = true;
    }

    private bool WouldNewSegmentOverlapOtherArrow(Vector2Int start, Vector2Int end, out Vector2Int blockedCell, out int ownerIndex)
    {
        blockedCell = Vector2Int.zero;
        ownerIndex = -1;
        Vector2Int delta = end - start;
        Vector2Int step = new Vector2Int(Sign(delta.x), Sign(delta.y));
        int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

        for (int distance = 1; distance <= length; distance++)
        {
            Vector2Int cell = start + step * distance;

            if (IsCellOwnedByOtherArrow(cell, out ownerIndex))
            {
                blockedCell = cell;
                return true;
            }
        }

        return false;
    }

    private bool WouldSegmentCrossInactiveCell(Vector2Int start, Vector2Int end, out Vector2Int inactiveCell)
    {
        inactiveCell = Vector2Int.zero;
        Vector2Int delta = end - start;
        Vector2Int step = new Vector2Int(Sign(delta.x), Sign(delta.y));
        int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

        for (int distance = 0; distance <= length; distance++)
        {
            Vector2Int cell = start + step * distance;

            if (!IsCellActive(cell))
            {
                inactiveCell = cell;
                return true;
            }
        }

        return false;
    }

    private bool IsCellOwnedByOtherArrow(Vector2Int cell, out int ownerIndex)
    {
        if (occupiedCellOwners.TryGetValue(cell, out ownerIndex) && ownerIndex != selectedArrowIndex)
        {
            return true;
        }

        ownerIndex = -1;
        return false;
    }

    private void FillCells(IReadOnlyList<Vector2Int> points, HashSet<Vector2Int> cells)
    {
        cells.Clear();

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2Int start = points[i];
            Vector2Int end = points[i + 1];
            Vector2Int delta = end - start;

            if (delta == Vector2Int.zero || (delta.x != 0 && delta.y != 0))
            {
                continue;
            }

            Vector2Int step = new Vector2Int(Sign(delta.x), Sign(delta.y));
            int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

            for (int distance = 0; distance <= length; distance++)
            {
                Vector2Int cell = start + step * distance;

                if (IsInsideGrid(cell))
                {
                    cells.Add(cell);
                }
            }
        }
    }

    private bool HasSelectedArrow()
    {
        return selectedArrowIndex >= 0 && selectedArrowIndex < arrows.Count;
    }

    private bool IsInsideGrid(Vector2Int cell)
    {
        return IsCellActive(cell);
    }

    private bool IsInsideGridBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
    }

    private bool HasCustomShape()
    {
        return customShapeEnabled;
    }

    private bool IsCellActive(Vector2Int cell)
    {
        if (!IsInsideGridBounds(cell))
        {
            return false;
        }

        return !customShapeEnabled || activeCells.Contains(cell);
    }

    private void ToggleActiveCell(Vector2Int cell)
    {
        if (!IsInsideGridBounds(cell))
        {
            return;
        }

        if (!HasCustomShape())
        {
            EnableCustomShapeFromFullRectangle();
        }

        if (occupiedCellOwners.ContainsKey(cell))
        {
            statusMessage = $"Cell {cell.x},{cell.y} is used by an arrow. Remove the arrow point before making it inactive.";
            return;
        }

        RecordHistory();

        if (activeCells.Contains(cell))
        {
            activeCells.Remove(cell);
            statusMessage = $"Cell {cell.x},{cell.y} set inactive.";
        }
        else
        {
            activeCells.Add(cell);
            statusMessage = $"Cell {cell.x},{cell.y} set active.";
        }

        ClearTesterResult();
    }

    private void HandleShapePaintInput(Event currentEvent, Rect gridBackground)
    {
        if (!editBoardShape)
        {
            if (isShapePaintDragging)
            {
                EndShapePaint();
            }

            return;
        }

        if (currentEvent.type == EventType.MouseDown
            && currentEvent.button == 0
            && TryGetGridCellAtMousePosition(currentEvent.mousePosition, gridBackground, out Vector2Int startCell))
        {
            BeginShapePaint(startCell);
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && isShapePaintDragging)
        {
            if (TryGetGridCellAtMousePosition(currentEvent.mousePosition, gridBackground, out Vector2Int dragCell))
            {
                ContinueShapePaint(dragCell);
            }

            currentEvent.Use();
            return;
        }

        if ((currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            || currentEvent.type == EventType.MouseLeaveWindow)
        {
            if (isShapePaintDragging)
            {
                EndShapePaint();
                currentEvent.Use();
            }
        }
    }

    private bool TryGetGridCellAtMousePosition(Vector2 mousePosition, Rect gridBackground, out Vector2Int cell)
    {
        cell = Vector2Int.zero;

        if (!gridBackground.Contains(mousePosition))
        {
            return false;
        }

        float pitch = GetCellSize() + GetCellGap();
        Vector2 gridOffset = GetGridDrawOffset();
        int x = Mathf.FloorToInt((mousePosition.x - gridOffset.x - HeaderSize) / pitch);
        int rowFromTop = Mathf.FloorToInt((mousePosition.y - gridOffset.y - HeaderSize) / pitch);
        int y = height - 1 - rowFromTop;
        cell = new Vector2Int(x, y);
        return IsInsideGridBounds(cell);
    }

    private void BeginShapePaint(Vector2Int cell)
    {
        shapePaintTrailCells.Clear();
        shapePaintSetActive = !IsCellActive(cell);
        shapePaintLastCell = cell;
        isShapePaintDragging = true;

        RecordHistory();

        if (!HasCustomShape())
        {
            EnableCustomShapeFromFullRectangle(false);
        }

        PaintShapeLine(cell, cell);
    }

    private void ContinueShapePaint(Vector2Int cell)
    {
        PaintShapeLine(shapePaintLastCell, cell);
        shapePaintLastCell = cell;
    }

    private void EndShapePaint()
    {
        isShapePaintDragging = false;
        shapePaintTrailCells.Clear();
    }

    private void PaintShapeLine(Vector2Int from, Vector2Int to)
    {
        int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));

        for (int stepIndex = 0; stepIndex <= steps; stepIndex++)
        {
            float t = steps == 0 ? 0f : stepIndex / (float)steps;
            Vector2Int cell = new Vector2Int(
                Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t)));

            ApplyShapePaintCell(cell);
        }
    }

    private void ApplyShapePaintCell(Vector2Int cell)
    {
        if (!IsInsideGridBounds(cell))
        {
            return;
        }

        shapePaintTrailCells.Add(cell);

        if (!shapePaintSetActive && occupiedCellOwners.ContainsKey(cell))
        {
            statusMessage = $"Cell {cell.x},{cell.y} is used by an arrow. Remove the arrow point before making it inactive.";
            return;
        }

        if (shapePaintSetActive)
        {
            if (activeCells.Add(cell))
            {
                statusMessage = $"Painting active cells. Last cell: {cell.x},{cell.y}.";
                ClearTesterResult();
            }

            return;
        }

        if (activeCells.Remove(cell))
        {
            statusMessage = $"Painting inactive cells. Last cell: {cell.x},{cell.y}.";
            ClearTesterResult();
        }
    }

    private void EnableCustomShapeFromFullRectangle(bool recordHistory = true)
    {
        if (recordHistory)
        {
            RecordHistory();
        }

        customShapeEnabled = true;
        activeCells.Clear();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                activeCells.Add(new Vector2Int(x, y));
            }
        }

        editBoardShape = true;
        ClearTesterResult();
        statusMessage = "Custom shape enabled. Click or drag grid cells to paint holes.";
    }

    private void UseFullRectangleShape()
    {
        if (!customShapeEnabled && activeCells.Count == 0)
        {
            editBoardShape = false;
            return;
        }

        RecordHistory();
        customShapeEnabled = false;
        activeCells.Clear();
        editBoardShape = false;
        ClearTesterResult();
        statusMessage = "Board shape reset to full rectangle.";
    }

    private void DisableAllShapeCells()
    {
        RecordHistory();
        customShapeEnabled = true;
        activeCells.Clear();
        editBoardShape = true;
        ClearTesterResult();
        statusMessage = "All grid cells disabled. Paint cells active while editing the shape.";
    }

    private void PruneActiveCellsToBounds()
    {
        if (activeCells.Count == 0)
        {
            return;
        }

        activeCells.RemoveWhere(cell => !IsInsideGridBounds(cell));
    }

    private string GetArrowDisplayName(int index)
    {
        if (index < 0 || index >= arrows.Count)
        {
            return $"Arrow {index + 1}";
        }

        return string.IsNullOrWhiteSpace(arrows[index].id) ? $"Arrow {index + 1}" : arrows[index].id;
    }

    private void HighlightTesterArrow(int arrowIndex)
    {
        if (arrowIndex < 0 || arrowIndex >= arrows.Count)
        {
            return;
        }

        testerHighlightedArrowIndex = arrowIndex;
        statusMessage = $"Highlighted {GetArrowDisplayName(arrowIndex)} from the clear order.";
    }

    private void ClearTesterResult()
    {
        hasTesterResult = false;
        testerSolved = false;
        testerHighlightedArrowIndex = -1;
        testerMessages.Clear();
        testerClearOrder.Clear();
    }

    private Color GetArrowPreviewColor(int arrowIndex)
    {
        if (arrowIndex == testerHighlightedArrowIndex)
        {
            return CurrentTheme.TesterArrow;
        }

        Color color = CurrentTheme.PreviewArrow;
        if (generatorColorModeIndex > 0)
        {
            color = GetEditorPreviewArrowColor(arrowIndex);
        }

        if (arrowIndex == selectedArrowIndex)
        {
            color = Color.Lerp(color, CurrentTheme.GridText, 0.22f);
        }

        return color;
    }

    private Color GetCellColor(Vector2Int cell)
    {
        bool isShapeTrailCell = isShapePaintDragging && shapePaintTrailCells.Contains(cell);
        bool isArrowTrailCell = isArrowDrawDragging && arrowDrawTrailCells.Contains(cell);
        bool isTrailCell = isShapeTrailCell || isArrowTrailCell;
        Color trailColor = isShapeTrailCell
            ? (shapePaintSetActive ? new Color(0.15f, 1f, 0.45f) : new Color(1f, 0.25f, 0.15f))
            : new Color(1f, 0.92f, 0.12f);

        if (!IsCellActive(cell))
        {
            Color inactiveColor = Color.Lerp(new Color(0.34f, 0.02f, 0.02f, 1f), CurrentTheme.GridBackground, 0.2f);
            return isTrailCell ? Color.Lerp(inactiveColor, trailColor, 0.65f) : inactiveColor;
        }

        if (occupiedCellOwners.TryGetValue(cell, out int ownerIndex))
        {
            if (ownerIndex == testerHighlightedArrowIndex)
            {
                return isTrailCell ? Color.Lerp(CurrentTheme.TesterCell, trailColor, 0.65f) : CurrentTheme.TesterCell;
            }

            if (ownerIndex == selectedArrowIndex)
            {
                return isTrailCell ? Color.Lerp(CurrentTheme.SelectedCell, trailColor, 0.65f) : CurrentTheme.SelectedCell;
            }

            return isTrailCell ? Color.Lerp(CurrentTheme.OccupiedCell, trailColor, 0.65f) : CurrentTheme.OccupiedCell;
        }

        return isTrailCell
            ? Color.Lerp(CurrentTheme.EmptyCell, trailColor, 0.65f)
            : CurrentTheme.EmptyCell;
    }

    private float GetMinGridZoom()
    {
        float longSide = Mathf.Max(1f, Mathf.Max(width, height));
        float scaledMinimum = ReferenceMinGridZoom * (ReferenceGridZoomBoardSize / Mathf.Max(ReferenceGridZoomBoardSize, longSide));
        return Mathf.Clamp(scaledMinimum, AbsoluteMinGridZoom, ReferenceMinGridZoom);
    }

    private float ClampGridZoom(float value)
    {
        return Mathf.Clamp(value, GetMinGridZoom(), MaxGridZoom);
    }

    private float GetBoardFriendlyDefaultGridZoom()
    {
        float longSide = Mathf.Max(1f, Mathf.Max(width, height));
        float sizeScale = Mathf.Min(1f, ReferenceGridZoomBoardSize / longSide);
        return ClampGridZoom(DefaultGridZoom * sizeScale);
    }

    private void FitGridZoomToBoardIfNeeded()
    {
        gridZoom = Mathf.Min(ClampGridZoom(gridZoom), GetBoardFriendlyDefaultGridZoom());
    }

    private float GetGridContentWidth()
    {
        return GridScrollPadding * 2f + HeaderSize + width * (GetCellSize() + GetCellGap()) + GetCellGap();
    }

    private float GetGridContentHeight()
    {
        return GridScrollPadding * 2f + HeaderSize + height * (GetCellSize() + GetCellGap()) + GetCellGap();
    }

    private Vector2 GetGridDrawOffset()
    {
        return new Vector2(GridScrollPadding, GridScrollPadding);
    }

    private float GetCellSize()
    {
        return BaseCellSize * ClampGridZoom(gridZoom);
    }

    private float GetCellGap()
    {
        return Mathf.Max(1f, BaseCellGap * ClampGridZoom(gridZoom));
    }

    private float GetArrowLineWidth()
    {
        return 6f * ClampGridZoom(gridZoom);
    }

    private Rect GetCellRect(Vector2Int cell)
    {
        float cellSize = GetCellSize();
        float cellGap = GetCellGap();
        Vector2 gridOffset = GetGridDrawOffset();
        float x = gridOffset.x + HeaderSize + cell.x * (cellSize + cellGap);
        float y = gridOffset.y + HeaderSize + (height - 1 - cell.y) * (cellSize + cellGap);
        return new Rect(x, y, cellSize, cellSize);
    }

    private Vector2 GetCellCenter(Vector2Int cell)
    {
        return GetCellRect(cell).center;
    }

    private RuntimeEditorTheme CurrentTheme => Themes[Mathf.Clamp(selectedThemeIndex, 0, Themes.Length - 1)];

    private void ApplyCameraTheme()
    {
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = playTestMode ? GetPlayableTestBoardBackgroundColor() : CurrentTheme.CameraBackground;
        }
    }

    private static Rect Shrink(Rect rect, float padding)
    {
        return new Rect(rect.x + padding, rect.y + padding, rect.width - padding * 2f, rect.height - padding * 2f);
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static void DrawRectOutline(Rect rect, Color color, float width)
    {
        DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, width), color);
        DrawRect(new Rect(rect.xMin, rect.yMax - width, rect.width, width), color);
        DrawRect(new Rect(rect.xMin, rect.yMin, width, rect.height), color);
        DrawRect(new Rect(rect.xMax - width, rect.yMin, width, rect.height), color);
    }

    private static void DrawFilledDirectionalTriangle(Vector2 center, Vector2Int direction, float size, Color color, float baseInset)
    {
        int stripCount = Mathf.Max(8, Mathf.CeilToInt(size));
        float halfBase = size * 0.5f;

        for (int i = 0; i < stripCount; i++)
        {
            float t0 = i / (float)stripCount;
            float t1 = (i + 1f) / stripCount;
            float tMid = (t0 + t1) * 0.5f;
            float thickness = Mathf.Max(1f, size / stripCount + 0.75f);
            float halfWidth = Mathf.Lerp(halfBase, 0f, tMid);
            float forward = Mathf.Lerp(-size * 0.5f + baseInset, size * 0.5f, tMid);

            if (direction.x > 0)
            {
                DrawRect(new Rect(center.x + forward - thickness * 0.5f, center.y - halfWidth, thickness, halfWidth * 2f), color);
            }
            else if (direction.x < 0)
            {
                DrawRect(new Rect(center.x - forward - thickness * 0.5f, center.y - halfWidth, thickness, halfWidth * 2f), color);
            }
            else if (direction.y > 0)
            {
                DrawRect(new Rect(center.x - halfWidth, center.y - forward - thickness * 0.5f, halfWidth * 2f, thickness), color);
            }
            else
            {
                DrawRect(new Rect(center.x - halfWidth, center.y + forward - thickness * 0.5f, halfWidth * 2f, thickness), color);
            }
        }
    }
    private static int Sign(int value)
    {
        if (value > 0)
        {
            return 1;
        }

        if (value < 0)
        {
            return -1;
        }

        return 0;
    }

    private sealed class RuntimeEditorTheme
    {
        public readonly string Name;
        public readonly Color CameraBackground;
        public readonly Color SidePanelBackground;
        public readonly Color GridBackground;
        public readonly Color EmptyCell;
        public readonly Color OccupiedCell;
        public readonly Color SelectedCell;
        public readonly Color TesterCell;
        public readonly Color PreviewArrow;
        public readonly Color TesterArrow;
        public readonly Color GridText;
        public readonly Color StartTileFill;
        public readonly Color StartTileBorder;
        public readonly Color SelectedListBackground;
        public readonly Color TesterListBackground;

        public RuntimeEditorTheme(
            string name,
            Color cameraBackground,
            Color sidePanelBackground,
            Color gridBackground,
            Color emptyCell,
            Color occupiedCell,
            Color selectedCell,
            Color testerCell,
            Color previewArrow,
            Color testerArrow,
            Color gridText,
            Color startTileFill,
            Color startTileBorder,
            Color selectedListBackground,
            Color testerListBackground)
        {
            Name = name;
            CameraBackground = cameraBackground;
            SidePanelBackground = sidePanelBackground;
            GridBackground = gridBackground;
            EmptyCell = emptyCell;
            OccupiedCell = occupiedCell;
            SelectedCell = selectedCell;
            TesterCell = testerCell;
            PreviewArrow = previewArrow;
            TesterArrow = testerArrow;
            GridText = gridText;
            StartTileFill = startTileFill;
            StartTileBorder = startTileBorder;
            SelectedListBackground = selectedListBackground;
            TesterListBackground = testerListBackground;
        }
    }

    private class RuntimeArrowDraft
    {
        public string id;
        public Color color;
        public GeneratedArrowProfile generatedProfile = GeneratedArrowProfile.Unknown;
        public readonly List<Vector2Int> points = new List<Vector2Int>();
    }

    private sealed class GeneratedTailExtensionCandidate
    {
        public readonly int ArrowIndex;
        public readonly Vector2Int Cell;
        public readonly RuntimeArrowDraft Arrow;
        public readonly float Score;

        public GeneratedTailExtensionCandidate(int arrowIndex, Vector2Int cell, RuntimeArrowDraft arrow, float score)
        {
            ArrowIndex = arrowIndex;
            Cell = cell;
            Arrow = arrow;
            Score = score;
        }
    }

    private sealed class GeneratedTailMergeCandidate
    {
        public readonly int SourceIndex;
        public readonly int VictimIndex;
        public readonly RuntimeArrowDraft Arrow;
        public readonly float Score;

        public GeneratedTailMergeCandidate(int sourceIndex, int victimIndex, RuntimeArrowDraft arrow, float score)
        {
            SourceIndex = sourceIndex;
            VictimIndex = victimIndex;
            Arrow = arrow;
            Score = score;
        }
    }

    private enum GeneratorAlgorithmMode
    {
        Legacy,
        ProfileGuided,
        ComplexGuided,
        LockstepWeave,
        ComplexGuidedDx,
        ComplexGuidedDxFlow,
        ChainFocus,
        Crossweave,
        Longform,
        CompactLocks,
        ExpertMix
    }

    private enum GeneratedArrowProfile
    {
        Unknown,
        StraightRail,
        LHook,
        OffsetS,
        Hairpin,
        OpenC,
        RectangularSpiral,
        Serpentine,
        Staircase,
        PerimeterRunner,
        LongSpine,
        OrganicWinding,
        Count
    }

    private enum GeneratedArrowShape
    {
        Straight,
        Bend,
        UShape,
        Offset,
        Zigzag,
        Winding,
        Count
    }

    private class GeneratedLevelBuild
    {
        public List<RuntimeArrowDraft> arrows;
        public HashSet<Vector2Int> occupied;
        public int addedCount;
        public int seed;
        public int initialPlayableCount = -1;
        public int maxPlayableCount = -1;
        public int overTwoRouteMoveCount = -1;
        public float averagePlayableCount = -1f;
        public int dependencyDepth = -1;
        public int maxUnlockWaveCount = -1;
        public int overTwoUnlockWaveCount = -1;
        public float averageArrowLength;
        public float averageTurnCount;
        public float multiTurnArrowRatio;
        public int distinctTurnCount;
        public int dependencyEdgeCount;
        public float dependencyParticipationRatio;
        public int isolatedDependencyArrowCount;
        public float averageDependencyDistance;
        public float crossRegionDependencyRatio;
        public float crossColumnDependencyRatio;
        public float crossRowDependencyRatio;
        public float dependencyDirectionBalance;
        public float dependencyAxisBalance;
        public int multiDependentAnchorCount;
        public int multiRegionAnchorCount;
        public int excessAnchorDependentCount;
        public int controlledGateCount;
        public int maximumGateDependentCount;
        public float controlledGateEdgeRatio;
        public int controlledBurstWaveCount;
        public int gateWaveTransitionCount;
        public int oversizedUnlockWaveCount;
        public float singleArrowWaveRatio;
        public float averageBlockerDistance;
        public float nearBlockerRatio;
        public float remoteBlockerRatio;
        public float averagePlayableExitLane;
        public float shortPlayableExitLaneRatio;
        public float boundaryPlayableRatio;
        public float zeroImpactPlayableRatio;
        public float clusteredChoiceWaveRatio;
        public float distributedChoiceWaveRatio;
        public float averageChoiceSeparationRatio;
        public float averageWaveUnlockCount;
        public int solveTransitionCount;
        public float averageSolveJumpDistance;
        public float averageSolveHorizontalJumpDistance;
        public float solveLongHorizontalTransitionRatio;
        public float solveLeftToRightProgress;
        public float solveForwardAreaHandoffRatio;
        public float solveBackwardAreaHandoffRatio;
        public float solveSameAreaTransitionRatio;
        public float solveHorizontalAreaCoverage;
        public float solveHorizontalAreaOrderScore;
        public int longestSameAreaSolveRun;
        public float solveRegionTransitionRatio;
        public float solveCrossColumnTransitionRatio;
        public float solveOutwardResetRatio;
        public int longestInwardSolveRun;
        public float spatialDependencyScore;
        public float complexityScore;
        public bool usedFallback;
    }

    private readonly struct GeneratorExitCandidate
    {
        public readonly Vector2Int head;
        public readonly Vector2Int exitDirection;

        public GeneratorExitCandidate(Vector2Int head, Vector2Int exitDirection)
        {
            this.head = head;
            this.exitDirection = exitDirection;
        }
    }

    private class TestArrow
    {
        public int index;
        public string name;
        public Vector2Int head;
        public Vector2Int exitDirection;
        public readonly List<Vector2Int> points = new List<Vector2Int>();
        public readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
    }


    private static class RuntimeJsonFileDialog
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int MaxFilePathLength = 4096;
        private const int OfnReadonly = 0x00000001;
        private const int OfnOverwritePrompt = 0x00000002;
        private const int OfnHideReadonly = 0x00000004;
        private const int OfnNoChangeDir = 0x00000008;
        private const int OfnPathMustExist = 0x00000800;
        private const int OfnFileMustExist = 0x00001000;
        private const int OfnExplorer = 0x00080000;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpstrFilter;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpstrInitialDir;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpstrTitle;
            public int flags;
            public short nFileOffset;
            public short nFileExtension;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }

        [System.Runtime.InteropServices.DllImport("comdlg32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OpenFileName openFileName);

        [System.Runtime.InteropServices.DllImport("comdlg32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern bool GetSaveFileName(ref OpenFileName openFileName);
#endif

        public static bool TryOpenJsonFile(string initialDirectory, out string path)
        {
            path = null;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr fileBuffer = IntPtr.Zero;
            try
            {
                fileBuffer = CreateFileBuffer(null);
                OpenFileName openFileName = CreateBaseFileName(initialDirectory, fileBuffer, "Open Arrow Level JSON");
                openFileName.flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnHideReadonly | OfnReadonly;

                if (!GetOpenFileName(ref openFileName))
                {
                    return false;
                }

                path = System.Runtime.InteropServices.Marshal.PtrToStringAuto(fileBuffer);
                return !string.IsNullOrWhiteSpace(path);
            }
            finally
            {
                FreeFileBuffer(fileBuffer);
            }
#else
            return false;
#endif
        }

        public static bool TrySaveJsonFile(string initialDirectory, string suggestedFileName, out string path)
        {
            path = null;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr fileBuffer = IntPtr.Zero;
            try
            {
                fileBuffer = CreateFileBuffer(NormalizeJsonFileName(suggestedFileName));
                OpenFileName openFileName = CreateBaseFileName(initialDirectory, fileBuffer, "Save Arrow Level JSON");
                openFileName.flags = OfnExplorer | OfnPathMustExist | OfnNoChangeDir | OfnHideReadonly | OfnOverwritePrompt;

                if (!GetSaveFileName(ref openFileName))
                {
                    return false;
                }

                path = System.Runtime.InteropServices.Marshal.PtrToStringAuto(fileBuffer);
                if (!string.IsNullOrWhiteSpace(path) && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".json";
                }

                return !string.IsNullOrWhiteSpace(path);
            }
            finally
            {
                FreeFileBuffer(fileBuffer);
            }
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static OpenFileName CreateBaseFileName(string initialDirectory, IntPtr fileBuffer, string title)
        {
            return new OpenFileName
            {
                lStructSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(OpenFileName)),
                lpstrFilter = "JSON files\0*.json\0All files\0*.*\0",
                lpstrFile = fileBuffer,
                nMaxFile = MaxFilePathLength,
                lpstrInitialDir = Directory.Exists(initialDirectory) ? initialDirectory : Application.persistentDataPath,
                lpstrTitle = title,
                lpstrDefExt = "json"
            };
        }

        private static IntPtr CreateFileBuffer(string initialValue)
        {
            IntPtr fileBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(MaxFilePathLength * 2);
            for (int i = 0; i < MaxFilePathLength * 2; i++)
            {
                System.Runtime.InteropServices.Marshal.WriteByte(fileBuffer, i, 0);
            }

            if (!string.IsNullOrWhiteSpace(initialValue))
            {
                string clippedValue = initialValue.Trim();
                if (clippedValue.Length >= MaxFilePathLength)
                {
                    clippedValue = clippedValue.Substring(0, MaxFilePathLength - 1);
                }

                byte[] bytes = System.Text.Encoding.Unicode.GetBytes(clippedValue);
                int byteCount = Math.Min(bytes.Length, (MaxFilePathLength - 1) * 2);
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, fileBuffer, byteCount);
            }

            return fileBuffer;
        }

        private static void FreeFileBuffer(IntPtr fileBuffer)
        {
            if (fileBuffer != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(fileBuffer);
            }
        }

        private static string NormalizeJsonFileName(string suggestedFileName)
        {
            string safeName = string.IsNullOrWhiteSpace(suggestedFileName) ? "ArrowLevel.json" : suggestedFileName.Trim();
            return safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? safeName : safeName + ".json";
        }
#endif
    }
    private readonly struct GuiDisabledScope : IDisposable
    {
        private readonly bool previousEnabled;

        public GuiDisabledScope(bool disabled)
        {
            previousEnabled = GUI.enabled;
            GUI.enabled = !disabled;
        }

        public void Dispose()
        {
            GUI.enabled = previousEnabled;
        }
    }
}











