/*
Summary:
PathArrowLevelEditorWindow is a Unity Editor-only level builder for PathArrowLevelData
assets. It lets you create/load a level asset, edit board size, draw arrow paths on
a bottom-left-origin grid, paint optional non-rectangular board shapes, validate
overlaps/diagonals, and save the asset safely.
*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PathArrowLevelEditorWindow : EditorWindow
{
    private const float BaseCellSize = 28f;
    private const float BaseCellGap = 2f;
    private const float HeaderHeight = 22f;
    private const float BaseArrowLineWidth = 6f;

    private readonly Dictionary<Vector2Int, int> occupiedCellOwners = new Dictionary<Vector2Int, int>();
    private readonly HashSet<Vector2Int> activeCellLookup = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> shapePaintTrailCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> arrowDrawTrailCells = new HashSet<Vector2Int>();
    private readonly List<string> validationMessages = new List<string>();
    private readonly List<string> testerMessages = new List<string>();
    private readonly List<int> testerClearOrder = new List<int>();

    private PathArrowLevelData levelAsset;
    private SerializedObject levelObject;
    private SerializedProperty widthProperty;
    private SerializedProperty heightProperty;
    private SerializedProperty activeCellsProperty;
    private SerializedProperty arrowsProperty;
    private Vector2 leftScroll;
    private Vector2 gridScroll;
    private Vector2 rightScroll;
    private int selectedArrowIndex = -1;
    private string statusMessage = "Pick or create a level asset.";
    private bool showCoordinates = true;
    private bool editBoardShape;
    private bool isShapePaintDragging;
    private bool shapePaintSetActive;
    private Vector2Int shapePaintLastCell;
    private bool isArrowDrawDragging;
    private bool arrowDrawRecordedUndo;
    private Vector2Int arrowDrawLastCell;
    private bool useEditorPreviewColor = true;
    private Color editorPreviewArrowColor = new Color(0.1f, 0.95f, 0.65f, 1f);
    private Color gridTextColor = new Color(0.9f, 0.95f, 1f, 0.9f);
    private float gridZoom = 1.25f;
    private bool hasTesterResult;
    private bool testerSolved;
    private int testerHighlightedArrowIndex = -1;

    [MenuItem("Tools/Arrow Puzzle/Level Editor")]
    public static void Open()
    {
        GetWindow<PathArrowLevelEditorWindow>("Arrow Level Editor");
    }

    private void OnEnable()
    {
        Selection.selectionChanged += HandleSelectionChanged;

        if (levelAsset == null && Selection.activeObject is PathArrowLevelData selectedLevel)
        {
            SetLevelAsset(selectedLevel);
        }
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= HandleSelectionChanged;
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (levelAsset == null)
        {
            DrawEmptyState();
            return;
        }

        EnsureSerializedObject();

        if (levelObject == null)
        {
            EditorGUILayout.HelpBox("The selected object is not a valid PathArrowLevelData asset.", MessageType.Error);
            return;
        }

        levelObject.Update();
        RefreshActiveCellLookup();
        ValidateLevel();

        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawGridPanel();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();

        levelObject.ApplyModifiedProperties();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUI.BeginChangeCheck();
        PathArrowLevelData pickedAsset = (PathArrowLevelData)EditorGUILayout.ObjectField(
            levelAsset,
            typeof(PathArrowLevelData),
            false,
            GUILayout.MinWidth(220f));

        if (EditorGUI.EndChangeCheck())
        {
            SetLevelAsset(pickedAsset);
        }

        if (GUILayout.Button("Create", EditorStyles.toolbarButton, GUILayout.Width(64f)))
        {
            CreateLevelAsset();
        }

        using (new EditorGUI.DisabledScope(levelAsset == null))
        {
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                SaveLevel();
            }

            if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                EditorGUIUtility.PingObject(levelAsset);
                Selection.activeObject = levelAsset;
            }
        }

        GUILayout.FlexibleSpace();
        useEditorPreviewColor = GUILayout.Toggle(useEditorPreviewColor, "Bright Preview", EditorStyles.toolbarButton, GUILayout.Width(104f));
        using (new EditorGUI.DisabledScope(!useEditorPreviewColor))
        {
            editorPreviewArrowColor = EditorGUILayout.ColorField(GUIContent.none, editorPreviewArrowColor, false, false, false, GUILayout.Width(54f));
        }
        showCoordinates = GUILayout.Toggle(showCoordinates, "Coords", EditorStyles.toolbarButton, GUILayout.Width(64f));
        gridTextColor = EditorGUILayout.ColorField(GUIContent.none, gridTextColor, false, false, false, GUILayout.Width(54f));
        if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(24f)))
        {
            gridZoom = Mathf.Max(0.65f, gridZoom - 0.1f);
        }
        gridZoom = GUILayout.HorizontalSlider(gridZoom, 0.65f, 2.25f, GUILayout.Width(86f));
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24f)))
        {
            gridZoom = Mathf.Min(2.25f, gridZoom + 0.1f);
        }
        GUILayout.Label($"{gridZoom:0.0}x", EditorStyles.miniLabel, GUILayout.Width(34f));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEmptyState()
    {
        EditorGUILayout.Space(18f);
        EditorGUILayout.HelpBox(
            "Choose an existing PathArrowLevelData asset, select one in the Project window, or click Create.",
            MessageType.Info);
    }

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(300f));
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(widthProperty);
        EditorGUILayout.PropertyField(heightProperty);

        if (EditorGUI.EndChangeCheck())
        {
            ClearTesterResult();
            ClampBoardSize();
            PruneActiveCellsToBounds();
            ClampSelectedArrowIndex();
            SetStatus("Board size changed.");
        }

        DrawBoardShapeControls();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Arrows", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Arrow"))
        {
            AddArrow();
        }

        using (new EditorGUI.DisabledScope(!HasSelectedArrow()))
        {
            if (GUILayout.Button("Delete"))
            {
                DeleteSelectedArrow();
            }
        }

        EditorGUILayout.EndHorizontal();

        DrawArrowList();

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawBoardShapeControls()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Board Shape", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            HasCustomShape()
                ? $"Custom shape: {activeCellsProperty.arraySize} active cells. Turn on Edit Shape, then click or drag grid cells to paint holes."
                : "Full rectangle. Enable Custom Shape to make holes or non-rectangular boards.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();

        editBoardShape = GUILayout.Toggle(editBoardShape, "Edit Shape", "Button");

        if (GUILayout.Button("Enable Custom"))
        {
            EnableCustomShapeFromFullRectangle();
        }

        if (GUILayout.Button("Full Rectangle"))
        {
            UseFullRectangleShape();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawGridPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUILayout.LabelField(
            editBoardShape
                ? "Grid - shape edit mode. Click or drag cells to paint the board shape. 0,0 is bottom-left."
                : "Grid - click or drag cells to draw the selected arrow. 0,0 is bottom-left.",
            EditorStyles.boldLabel);

        Rect viewRect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        Rect contentRect = new Rect(
            0f,
            0f,
            HeaderHeight + GetWidth() * (GetCellSize() + GetCellGap()) + GetCellGap(),
            HeaderHeight + GetHeight() * (GetCellSize() + GetCellGap()) + GetCellGap());

        gridScroll = GUI.BeginScrollView(viewRect, gridScroll, contentRect);
        DrawGrid(contentRect);
        GUI.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(340f));
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

        EditorGUILayout.LabelField("Selected Arrow", EditorStyles.boldLabel);

        if (HasSelectedArrow())
        {
            DrawSelectedArrowInspector();
        }
        else
        {
            EditorGUILayout.HelpBox("Select an arrow or click Add Arrow.", MessageType.Info);
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        DrawValidationMessages();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Level Tester", EditorStyles.boldLabel);
        DrawLevelTester();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawArrowList()
    {
        if (arrowsProperty == null || arrowsProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No arrows yet.", MessageType.Info);
            return;
        }

        for (int i = 0; i < arrowsProperty.arraySize; i++)
        {
            SerializedProperty arrow = arrowsProperty.GetArrayElementAtIndex(i);
            SerializedProperty id = arrow.FindPropertyRelative("id");
            SerializedProperty color = arrow.FindPropertyRelative("color");
            SerializedProperty points = arrow.FindPropertyRelative("points");

            Rect rowRect = EditorGUILayout.GetControlRect(false, 24f);

            if (selectedArrowIndex == i)
            {
                EditorGUI.DrawRect(rowRect, new Color(0.25f, 0.45f, 0.85f, 0.32f));
            }

            Rect colorRect = new Rect(rowRect.x + 4f, rowRect.y + 4f, 16f, 16f);
            Rect labelRect = new Rect(rowRect.x + 26f, rowRect.y + 2f, rowRect.width - 30f, 20f);
            EditorGUI.DrawRect(colorRect, GetArrowPreviewColor(i, color.colorValue));

            string arrowName = string.IsNullOrWhiteSpace(id.stringValue) ? $"Arrow {i + 1}" : id.stringValue;
            string pointCount = points != null ? $"{points.arraySize} pts" : "0 pts";

            if (GUI.Button(labelRect, $"{i + 1}. {arrowName} ({pointCount})", EditorStyles.label))
            {
                selectedArrowIndex = i;
                Repaint();
            }
        }
    }

    private void DrawSelectedArrowInspector()
    {
        SerializedProperty arrow = GetSelectedArrowProperty();

        if (arrow == null)
        {
            selectedArrowIndex = -1;
            return;
        }

        SerializedProperty id = arrow.FindPropertyRelative("id");
        SerializedProperty color = arrow.FindPropertyRelative("color");
        SerializedProperty points = arrow.FindPropertyRelative("points");

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(id);
        EditorGUILayout.PropertyField(color);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Remove Last Point"))
        {
            RemoveLastPoint(points);
        }

        if (GUILayout.Button("Clear Points"))
        {
            ClearPoints(points);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Points", EditorStyles.boldLabel);

        for (int i = 0; i < points.arraySize; i++)
        {
            SerializedProperty point = points.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();

            point.vector2IntValue = EditorGUILayout.Vector2IntField($"Point {i}", point.vector2IntValue);

            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                RecordUndo("Remove Arrow Point");
                points.DeleteArrayElementAtIndex(i);
                SetStatus("Point removed.");
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        if (EditorGUI.EndChangeCheck())
        {
            ClearTesterResult();
            ClampSelectedArrowPoints(points);
            SetStatus("Arrow edited.");
        }
    }

    private void DrawValidationMessages()
    {
        if (validationMessages.Count == 0)
        {
            EditorGUILayout.HelpBox("No validation issues found.", MessageType.Info);
            return;
        }

        for (int i = 0; i < validationMessages.Count; i++)
        {
            EditorGUILayout.HelpBox(validationMessages[i], MessageType.Warning);
        }
    }

    private void DrawGrid(Rect contentRect)
    {
        Event currentEvent = Event.current;
        float cellSize = GetCellSize();
        float cellGap = GetCellGap();
        Rect gridBackground = new Rect(HeaderHeight, HeaderHeight, GetWidth() * (cellSize + cellGap), GetHeight() * (cellSize + cellGap));
        EditorGUI.DrawRect(gridBackground, new Color(0.14f, 0.14f, 0.14f));

        DrawCoordinateHeaders();
        HandleShapePaintInput(currentEvent, gridBackground);
        HandleArrowDrawInput(currentEvent, gridBackground);

        for (int y = 0; y < GetHeight(); y++)
        {
            for (int x = 0; x < GetWidth(); x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Rect cellRect = GetCellRect(cell);
                Color cellColor = GetCellColor(cell);

                EditorGUI.DrawRect(cellRect, cellColor);

                if (showCoordinates && cellSize >= 18f)
                {
                    GUI.Label(cellRect, $"{x},{y}", GetCenteredCellLabelStyle(cellColor));
                }

            }
        }

        DrawArrowPaths();

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 && gridBackground.Contains(currentEvent.mousePosition))
        {
            RemoveLastSelectedPoint();
            currentEvent.Use();
        }
    }

    private void DrawCoordinateHeaders()
    {
        GUIStyle headerStyle = GetGridAxisLabelStyle();

        for (int x = 0; x < GetWidth(); x++)
        {
            Rect headerRect = new Rect(HeaderHeight + x * (GetCellSize() + GetCellGap()), 0f, GetCellSize(), HeaderHeight);
            GUI.Label(headerRect, x.ToString(), headerStyle);
        }

        for (int y = 0; y < GetHeight(); y++)
        {
            Rect headerRect = new Rect(0f, HeaderHeight + (GetHeight() - 1 - y) * (GetCellSize() + GetCellGap()), HeaderHeight, GetCellSize());
            GUI.Label(headerRect, y.ToString(), headerStyle);
        }
    }

    private void DrawArrowPaths()
    {
        if (arrowsProperty == null)
        {
            return;
        }

        Handles.BeginGUI();

        for (int i = 0; i < arrowsProperty.arraySize; i++)
        {
            SerializedProperty arrow = arrowsProperty.GetArrayElementAtIndex(i);
            SerializedProperty colorProperty = arrow.FindPropertyRelative("color");
            SerializedProperty points = arrow.FindPropertyRelative("points");

            if (points == null || points.arraySize == 0)
            {
                continue;
            }

            Color arrowColor = GetArrowPreviewColor(i, colorProperty.colorValue);
            arrowColor.a = selectedArrowIndex == i ? 1f : 0.78f;

            Handles.color = arrowColor;

            for (int pointIndex = 0; pointIndex < points.arraySize - 1; pointIndex++)
            {
                Vector2Int a = points.GetArrayElementAtIndex(pointIndex).vector2IntValue;
                Vector2Int b = points.GetArrayElementAtIndex(pointIndex + 1).vector2IntValue;

                if (!IsInsideGrid(a) || !IsInsideGrid(b))
                {
                    continue;
                }

                float lineWidth = i == testerHighlightedArrowIndex ? GetArrowLineWidth() * 1.55f : GetArrowLineWidth();
                Handles.DrawAAPolyLine(lineWidth, GetCellCenter(a), GetCellCenter(b));
            }

            DrawArrowHead(points, arrowColor);
        }

        Handles.EndGUI();
    }

    private void DrawArrowHead(SerializedProperty points, Color arrowColor)
    {
        if (points.arraySize < 2)
        {
            return;
        }

        Vector2Int previous = points.GetArrayElementAtIndex(points.arraySize - 2).vector2IntValue;
        Vector2Int head = points.GetArrayElementAtIndex(points.arraySize - 1).vector2IntValue;

        if (!IsInsideGrid(previous) || !IsInsideGrid(head))
        {
            return;
        }

        Vector2 direction = (GetCellCenter(head) - GetCellCenter(previous)).normalized;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x);
        float headSize = GetArrowHeadSize();
        Vector2 tip = GetCellCenter(head) + direction * headSize;
        Vector2 baseCenter = GetCellCenter(head) - direction * (headSize * 0.7f);
        Vector3[] triangle =
        {
            tip,
            baseCenter + normal * (headSize * 0.7f),
            baseCenter - normal * (headSize * 0.7f)
        };

        Handles.color = arrowColor;
        Handles.DrawAAConvexPolygon(triangle);
    }

    private float GetCellSize()
    {
        return BaseCellSize * Mathf.Clamp(gridZoom, 0.65f, 2.25f);
    }

    private float GetCellGap()
    {
        return Mathf.Max(1f, BaseCellGap * Mathf.Clamp(gridZoom, 0.65f, 2.25f));
    }

    private float GetArrowLineWidth()
    {
        return BaseArrowLineWidth * Mathf.Clamp(gridZoom, 0.65f, 2.25f);
    }

    private float GetArrowHeadSize()
    {
        return 10f * Mathf.Clamp(gridZoom, 0.65f, 2.25f);
    }
    private Rect GetCellRect(Vector2Int cell)
    {
        float cellSize = GetCellSize();
        float cellGap = GetCellGap();
        float x = HeaderHeight + cell.x * (cellSize + cellGap);
        float y = HeaderHeight + (GetHeight() - 1 - cell.y) * (cellSize + cellGap);
        return new Rect(x, y, cellSize, cellSize);
    }

    private Vector2 GetCellCenter(Vector2Int cell)
    {
        Rect rect = GetCellRect(cell);
        return rect.center;
    }

    private Color GetArrowPreviewColor(int arrowIndex, Color storedColor)
    {
        if (arrowIndex == testerHighlightedArrowIndex)
        {
            return new Color(1f, 0.8f, 0.05f, 1f);
        }

        if (!useEditorPreviewColor)
        {
            return storedColor;
        }

        Color previewColor = editorPreviewArrowColor;

        if (arrowIndex == selectedArrowIndex)
        {
            previewColor = Color.Lerp(previewColor, Color.white, 0.25f);
        }

        previewColor.a = 1f;
        return previewColor;
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
            Color inactiveColor = new Color(0.075f, 0.075f, 0.08f);
            return isTrailCell ? Color.Lerp(inactiveColor, trailColor, 0.65f) : inactiveColor;
        }

        if (occupiedCellOwners.TryGetValue(cell, out int ownerIndex))
        {
            if (ownerIndex == testerHighlightedArrowIndex)
            {
                Color testerColor = new Color(1f, 0.8f, 0.05f, 0.55f);
                return isTrailCell ? Color.Lerp(testerColor, trailColor, 0.65f) : testerColor;
            }

            if (ownerIndex == selectedArrowIndex)
            {
                Color selectedColor = new Color(0.35f, 0.65f, 1f, 0.45f);
                return isTrailCell ? Color.Lerp(selectedColor, trailColor, 0.65f) : selectedColor;
            }

            Color occupiedColor = new Color(0.18f, 0.18f, 0.18f);
            return isTrailCell ? Color.Lerp(occupiedColor, trailColor, 0.65f) : occupiedColor;
        }

        Color baseColor = new Color(0.23f, 0.23f, 0.23f);
        return isTrailCell
            ? Color.Lerp(baseColor, trailColor, 0.65f)
            : baseColor;
    }

    private GUIStyle GetGridAxisLabelStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            normal = { textColor = gridTextColor }
        };

        return style;
    }
    private GUIStyle GetCenteredCellLabelStyle(Color cellColor)
    {
        GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(GetCellSize() * 0.32f), 8, 14),
            normal = { textColor = gridTextColor }
        };

        return style;
    }

    private void HandleCellClick(Vector2Int cell)
    {
        TryAppendArrowDrawCell(cell);
    }

    private bool TryAppendArrowDrawCell(Vector2Int cell)
    {
        if (!IsCellActive(cell))
        {
            SetStatus($"Cell {cell.x},{cell.y} is outside the active board shape.");
            return false;
        }

        if (!HasSelectedArrow())
        {
            if (IsCellOwnedByOtherArrow(cell, out int ownerIndex))
            {
                SetStatus($"Cell {cell.x},{cell.y} is already used by Arrow {ownerIndex + 1}.");
                return false;
            }

            EnsureArrowDrawUndoRecorded();
            AddArrow(false);
        }

        SerializedProperty points = GetSelectedPointsProperty();

        if (points == null)
        {
            return false;
        }

        if (points.arraySize == 0)
        {
            if (IsCellOwnedByOtherArrow(cell, out int ownerIndex))
            {
                SetStatus($"Cell {cell.x},{cell.y} is already used by Arrow {ownerIndex + 1}.");
                return false;
            }
        }
        else
        {
            Vector2Int lastPoint = points.GetArrayElementAtIndex(points.arraySize - 1).vector2IntValue;

            if (lastPoint == cell)
            {
                SetStatus("That point is already the arrow head.");
                RefreshArrowDrawTrail(points);
                return true;
            }

            if (TryBacktrackArrowDrawCell(points, cell))
            {
                return true;
            }

            Vector2Int delta = cell - lastPoint;

            if (delta.x != 0 && delta.y != 0)
            {
                SetStatus("Diagonal segments are not allowed. Pick a cell in the same row or column.");
                return false;
            }

            if (WouldSegmentCrossInactiveCell(lastPoint, cell, out Vector2Int inactiveCell))
            {
                SetStatus($"That segment crosses inactive cell {inactiveCell.x},{inactiveCell.y}.");
                return false;
            }

            if (WouldNewSegmentSelfOverlap(points, lastPoint, cell))
            {
                SetStatus("That segment crosses this arrow's own body. Remove points or choose another direction.");
                return false;
            }

            if (WouldNewSegmentOverlapOtherArrow(lastPoint, cell, out Vector2Int blockedCell, out int ownerIndex))
            {
                SetStatus($"That segment crosses Arrow {ownerIndex + 1} at {blockedCell.x},{blockedCell.y}.");
                return false;
            }
        }

        EnsureArrowDrawUndoRecorded();

        if (ShouldExtendLastArrowSegment(points, cell))
        {
            points.GetArrayElementAtIndex(points.arraySize - 1).vector2IntValue = cell;
        }
        else
        {
            points.InsertArrayElementAtIndex(points.arraySize);
            points.GetArrayElementAtIndex(points.arraySize - 1).vector2IntValue = cell;
        }

        RefreshArrowDrawTrail(points);
        levelObject.ApplyModifiedProperties();
        SetStatus($"Added point {cell.x},{cell.y}.");
        Repaint();
        return true;
    }

    private bool TryBacktrackArrowDrawCell(SerializedProperty points, Vector2Int cell)
    {
        if (points == null || points.arraySize < 2)
        {
            return false;
        }

        Vector2Int previous = points.GetArrayElementAtIndex(points.arraySize - 2).vector2IntValue;
        Vector2Int head = points.GetArrayElementAtIndex(points.arraySize - 1).vector2IntValue;

        if (!IsCellOnSegment(cell, previous, head))
        {
            return false;
        }

        EnsureArrowDrawUndoRecorded();

        if (cell == previous)
        {
            points.DeleteArrayElementAtIndex(points.arraySize - 1);
        }
        else
        {
            points.GetArrayElementAtIndex(points.arraySize - 1).vector2IntValue = cell;
        }

        levelObject.ApplyModifiedProperties();
        RefreshArrowDrawTrail(points);
        ClearTesterResult();
        SetStatus($"Backtracked arrow to {cell.x},{cell.y}.");
        Repaint();
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

    private void RefreshArrowDrawTrail(SerializedProperty points)
    {
        CollectArrowCells(points, arrowDrawTrailCells);
    }

    private bool ShouldExtendLastArrowSegment(SerializedProperty points, Vector2Int nextCell)
    {
        if (points == null || points.arraySize < 2)
        {
            return false;
        }

        Vector2Int previous = points.GetArrayElementAtIndex(points.arraySize - 2).vector2IntValue;
        Vector2Int head = points.GetArrayElementAtIndex(points.arraySize - 1).vector2IntValue;
        Vector2Int oldDirection = new Vector2Int(Sign(head.x - previous.x), Sign(head.y - previous.y));
        Vector2Int newDirection = new Vector2Int(Sign(nextCell.x - head.x), Sign(nextCell.y - head.y));

        return oldDirection != Vector2Int.zero && oldDirection == newDirection;
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
    private bool WouldNewSegmentSelfOverlap(SerializedProperty points, Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> candidatePoints = CopyPoints(points);

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

    private void BeginArrowDraw(Vector2Int cell)
    {
        arrowDrawTrailCells.Clear();
        arrowDrawRecordedUndo = false;
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
        arrowDrawRecordedUndo = false;
        arrowDrawTrailCells.Clear();
        Repaint();
    }

    private void EnsureArrowDrawUndoRecorded()
    {
        if (arrowDrawRecordedUndo)
        {
            return;
        }

        RecordUndo("Draw Arrow Path");
        arrowDrawRecordedUndo = true;
    }

    private void CollectArrowCells(SerializedProperty points, HashSet<Vector2Int> cells)
    {
        cells.Clear();

        if (points == null || points.arraySize == 0)
        {
            return;
        }

        if (points.arraySize == 1)
        {
            cells.Add(points.GetArrayElementAtIndex(0).vector2IntValue);
            return;
        }

        for (int i = 0; i < points.arraySize - 1; i++)
        {
            Vector2Int start = points.GetArrayElementAtIndex(i).vector2IntValue;
            Vector2Int end = points.GetArrayElementAtIndex(i + 1).vector2IntValue;
            Vector2Int delta = end - start;

            if (delta == Vector2Int.zero || (delta.x != 0 && delta.y != 0))
            {
                continue;
            }

            Vector2Int step = new Vector2Int(Sign(delta.x), Sign(delta.y));
            int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

            for (int distance = 0; distance <= length; distance++)
            {
                cells.Add(start + step * distance);
            }
        }
    }


    private List<Vector2Int> CopyPoints(SerializedProperty points)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        if (points == null)
        {
            return result;
        }

        for (int i = 0; i < points.arraySize; i++)
        {
            result.Add(points.GetArrayElementAtIndex(i).vector2IntValue);
        }

        return result;
    }
    private void AddArrow(bool recordUndo = true)
    {
        EnsureSerializedObject();

        if (arrowsProperty == null)
        {
            return;
        }

        if (recordUndo)
        {
            RecordUndo("Add Arrow");
        }

        int newIndex = arrowsProperty.arraySize;
        arrowsProperty.InsertArrayElementAtIndex(newIndex);

        SerializedProperty arrow = arrowsProperty.GetArrayElementAtIndex(newIndex);
        arrow.FindPropertyRelative("id").stringValue = $"Arrow {newIndex + 1}";
        arrow.FindPropertyRelative("color").colorValue = Color.black;
        arrow.FindPropertyRelative("points").ClearArray();

        selectedArrowIndex = newIndex;
        levelObject.ApplyModifiedProperties();
        SetStatus("Arrow added. Click or drag grid cells from tail to head.");
        Repaint();
    }

    private void DeleteSelectedArrow()
    {
        if (!HasSelectedArrow())
        {
            return;
        }

        RecordUndo("Delete Arrow");
        arrowsProperty.DeleteArrayElementAtIndex(selectedArrowIndex);
        selectedArrowIndex = Mathf.Clamp(selectedArrowIndex, -1, arrowsProperty.arraySize - 1);
        levelObject.ApplyModifiedProperties();
        SetStatus("Arrow deleted.");
        Repaint();
    }

    private void RemoveLastSelectedPoint()
    {
        SerializedProperty points = GetSelectedPointsProperty();

        if (points == null)
        {
            return;
        }

        RemoveLastPoint(points);
    }

    private void RemoveLastPoint(SerializedProperty points)
    {
        if (points == null || points.arraySize == 0)
        {
            SetStatus("No point to remove.");
            return;
        }

        RecordUndo("Remove Last Arrow Point");
        points.DeleteArrayElementAtIndex(points.arraySize - 1);
        levelObject.ApplyModifiedProperties();
        SetStatus("Last point removed.");
        Repaint();
    }

    private void ClearPoints(SerializedProperty points)
    {
        if (points == null)
        {
            return;
        }

        RecordUndo("Clear Arrow Points");
        points.ClearArray();
        levelObject.ApplyModifiedProperties();
        SetStatus("Arrow points cleared.");
        Repaint();
    }

    private void DrawLevelTester()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Test Level"))
        {
            RunLevelTest();
        }

        using (new EditorGUI.DisabledScope(!hasTesterResult || testerClearOrder.Count == 0))
        {
            if (GUILayout.Button("Highlight First"))
            {
                SelectTesterArrow(testerClearOrder[0]);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (!hasTesterResult)
        {
            EditorGUILayout.HelpBox("Run a test to check if the current level can be cleared.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            testerSolved ? $"Solvable in {testerClearOrder.Count} moves." : "Level is stuck before all arrows can escape.",
            testerSolved ? MessageType.Info : MessageType.Warning);

        if (testerClearOrder.Count > 0)
        {
            EditorGUILayout.LabelField("Clear Order", EditorStyles.boldLabel);

            for (int i = 0; i < testerClearOrder.Count; i++)
            {
                int arrowIndex = testerClearOrder[i];
                Rect rowRect = EditorGUILayout.GetControlRect(false, 22f);

                if (testerHighlightedArrowIndex == arrowIndex)
                {
                    EditorGUI.DrawRect(rowRect, new Color(1f, 0.78f, 0.12f, 0.28f));
                }

                Rect labelRect = new Rect(rowRect.x + 4f, rowRect.y + 2f, rowRect.width - 86f, 18f);
                Rect buttonRect = new Rect(rowRect.xMax - 78f, rowRect.y + 1f, 74f, 19f);
                GUI.Label(labelRect, $"{i + 1}. {GetArrowDisplayName(arrowIndex)}");

                if (GUI.Button(buttonRect, "Highlight"))
                {
                    SelectTesterArrow(arrowIndex);
                }
            }
        }

        for (int i = 0; i < testerMessages.Count; i++)
        {
            EditorGUILayout.HelpBox(testerMessages[i], testerSolved ? MessageType.Info : MessageType.Warning);
        }
    }

    private void RunLevelTest()
    {
        hasTesterResult = true;
        testerSolved = false;
        testerMessages.Clear();
        testerClearOrder.Clear();
        testerHighlightedArrowIndex = -1;

        levelObject?.ApplyModifiedProperties();
        ValidateLevel();

        if (validationMessages.Count > 0)
        {
            testerMessages.Add("Fix validation issues before running the solvability test.");
            SetStatus("Level test failed: validation issues found.");
            return;
        }

        List<TestArrow> testArrows = BuildTestArrows();

        if (testArrows.Count == 0)
        {
            testerMessages.Add("No arrows to test.");
            SetStatus("Level test found no arrows.");
            return;
        }

        Dictionary<Vector2Int, TestArrow> occupiedCells = BuildTestOccupiedCells(testArrows);
        HashSet<int> removedArrowIndices = new HashSet<int>();

        while (removedArrowIndices.Count < testArrows.Count)
        {
            TestArrow escapedArrow = null;

            for (int i = 0; i < testArrows.Count; i++)
            {
                TestArrow arrow = testArrows[i];

                if (removedArrowIndices.Contains(arrow.Index))
                {
                    continue;
                }

                if (CanTestArrowEscape(arrow, occupiedCells, removedArrowIndices, out _, out _))
                {
                    escapedArrow = arrow;
                    break;
                }
            }

            if (escapedArrow == null)
            {
                testerSolved = false;
                BuildTesterStuckMessages(testArrows, occupiedCells, removedArrowIndices);
                SetStatus("Level test failed: puzzle gets stuck.");
                return;
            }

            testerClearOrder.Add(escapedArrow.Index);
            removedArrowIndices.Add(escapedArrow.Index);

            foreach (Vector2Int cell in escapedArrow.Cells)
            {
                occupiedCells.Remove(cell);
            }
        }

        testerSolved = true;
        testerMessages.Add("Every arrow can escape using the shown order.");
        SetStatus($"Level test passed: {testerClearOrder.Count} moves.");
    }

    private List<TestArrow> BuildTestArrows()
    {
        List<TestArrow> testArrows = new List<TestArrow>();

        if (arrowsProperty == null)
        {
            return testArrows;
        }

        for (int arrowIndex = 0; arrowIndex < arrowsProperty.arraySize; arrowIndex++)
        {
            SerializedProperty arrowProperty = arrowsProperty.GetArrayElementAtIndex(arrowIndex);
            SerializedProperty pointsProperty = arrowProperty.FindPropertyRelative("points");

            if (pointsProperty == null || pointsProperty.arraySize < 2)
            {
                continue;
            }

            TestArrow arrow = new TestArrow
            {
                Index = arrowIndex,
                Name = GetArrowDisplayName(arrowIndex)
            };

            for (int pointIndex = 0; pointIndex < pointsProperty.arraySize; pointIndex++)
            {
                arrow.Points.Add(pointsProperty.GetArrayElementAtIndex(pointIndex).vector2IntValue);
            }

            arrow.Head = arrow.Points[arrow.Points.Count - 1];
            Vector2Int previous = arrow.Points[arrow.Points.Count - 2];
            Vector2Int exitDelta = arrow.Head - previous;
            arrow.ExitDirection = new Vector2Int(Sign(exitDelta.x), Sign(exitDelta.y));
            FillTestArrowCells(arrow);
            testArrows.Add(arrow);
        }

        return testArrows;
    }

    private Dictionary<Vector2Int, TestArrow> BuildTestOccupiedCells(List<TestArrow> testArrows)
    {
        Dictionary<Vector2Int, TestArrow> occupiedCells = new Dictionary<Vector2Int, TestArrow>();

        for (int i = 0; i < testArrows.Count; i++)
        {
            TestArrow arrow = testArrows[i];

            foreach (Vector2Int cell in arrow.Cells)
            {
                occupiedCells[cell] = arrow;
            }
        }

        return occupiedCells;
    }

    private void FillTestArrowCells(TestArrow arrow)
    {
        arrow.Cells.Clear();

        for (int i = 0; i < arrow.Points.Count - 1; i++)
        {
            Vector2Int start = arrow.Points[i];
            Vector2Int end = arrow.Points[i + 1];
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
                    arrow.Cells.Add(cell);
                }
            }
        }
    }

    private bool CanTestArrowEscape(
        TestArrow arrow,
        Dictionary<Vector2Int, TestArrow> occupiedCells,
        HashSet<int> removedArrowIndices,
        out TestArrow blocker,
        out Vector2Int blockerCell)
    {
        blocker = null;
        blockerCell = Vector2Int.zero;
        Vector2Int checkPosition = arrow.Head + arrow.ExitDirection;

        // Disabled shape cells are empty gaps; arrows still collide with any
        // active board section farther along the same ray.
        while (IsInsideGridBounds(checkPosition))
        {
            if (occupiedCells.TryGetValue(checkPosition, out TestArrow possibleBlocker)
                && !removedArrowIndices.Contains(possibleBlocker.Index))
            {
                blocker = possibleBlocker;
                blockerCell = checkPosition;
                return false;
            }

            checkPosition += arrow.ExitDirection;
        }

        return true;
    }

    private void BuildTesterStuckMessages(
        List<TestArrow> testArrows,
        Dictionary<Vector2Int, TestArrow> occupiedCells,
        HashSet<int> removedArrowIndices)
    {
        testerMessages.Add($"Cleared {removedArrowIndices.Count} of {testArrows.Count} arrows before getting stuck.");

        for (int i = 0; i < testArrows.Count; i++)
        {
            TestArrow arrow = testArrows[i];

            if (removedArrowIndices.Contains(arrow.Index))
            {
                continue;
            }

            if (CanTestArrowEscape(arrow, occupiedCells, removedArrowIndices, out TestArrow blocker, out Vector2Int blockerCell))
            {
                testerMessages.Add($"{arrow.Name}: should be movable, but was not selected. Re-run the test after checking the level data.");
            }
            else if (blocker != null)
            {
                string blockerName = blocker.Index == arrow.Index ? "its own body" : blocker.Name;
                testerMessages.Add($"{arrow.Name}: blocked by {blockerName} at {blockerCell.x},{blockerCell.y}.");
            }
            else
            {
                testerMessages.Add($"{arrow.Name}: blocked, but no blocker cell was found.");
            }
        }
    }

    private void SelectTesterArrow(int arrowIndex)
    {
        if (arrowsProperty == null || arrowIndex < 0 || arrowIndex >= arrowsProperty.arraySize)
        {
            return;
        }

        testerHighlightedArrowIndex = arrowIndex;
        SetStatus($"Highlighted {GetArrowDisplayName(arrowIndex)} from the clear order.");
        Repaint();
    }

    private string GetArrowDisplayName(int arrowIndex)
    {
        if (arrowsProperty == null || arrowIndex < 0 || arrowIndex >= arrowsProperty.arraySize)
        {
            return $"Arrow {arrowIndex + 1}";
        }

        SerializedProperty arrow = arrowsProperty.GetArrayElementAtIndex(arrowIndex);
        SerializedProperty id = arrow.FindPropertyRelative("id");
        return string.IsNullOrWhiteSpace(id.stringValue) ? $"Arrow {arrowIndex + 1}" : id.stringValue;
    }
    private void ValidateLevel()
    {
        validationMessages.Clear();
        occupiedCellOwners.Clear();

        if (arrowsProperty == null)
        {
            return;
        }

        for (int arrowIndex = 0; arrowIndex < arrowsProperty.arraySize; arrowIndex++)
        {
            SerializedProperty arrow = arrowsProperty.GetArrayElementAtIndex(arrowIndex);
            SerializedProperty id = arrow.FindPropertyRelative("id");
            SerializedProperty points = arrow.FindPropertyRelative("points");
            string arrowName = string.IsNullOrWhiteSpace(id.stringValue) ? $"Arrow {arrowIndex + 1}" : id.stringValue;

            ValidateArrowPoints(arrowIndex, arrowName, points);
        }
    }

    private void ValidateArrowPoints(int arrowIndex, string arrowName, SerializedProperty points)
    {
        if (points == null || points.arraySize < 2)
        {
            validationMessages.Add($"{arrowName}: needs at least 2 points.");
            return;
        }

        for (int pointIndex = 0; pointIndex < points.arraySize; pointIndex++)
        {
            Vector2Int point = points.GetArrayElementAtIndex(pointIndex).vector2IntValue;

            if (!IsInsideGrid(point))
            {
                validationMessages.Add($"{arrowName}: point {pointIndex} ({point.x},{point.y}) is outside the board.");
            }
        }

        List<Vector2Int> pathPoints = CopyPoints(points);
        HashSet<Vector2Int> arrowCells = new HashSet<Vector2Int>();

        for (int i = 0; i < points.arraySize - 1; i++)
        {
            Vector2Int start = points.GetArrayElementAtIndex(i).vector2IntValue;
            Vector2Int end = points.GetArrayElementAtIndex(i + 1).vector2IntValue;
            Vector2Int delta = end - start;

            if (delta == Vector2Int.zero)
            {
                validationMessages.Add($"{arrowName}: segment {i} has the same start and end point.");
                continue;
            }

            if (delta.x != 0 && delta.y != 0)
            {
                validationMessages.Add($"{arrowName}: segment {i} is diagonal.");
                continue;
            }

            Vector2Int step = new Vector2Int(Sign(delta.x), Sign(delta.y));
            int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

            for (int distance = 0; distance <= length; distance++)
            {
                Vector2Int cell = start + step * distance;

                if (!IsInsideGrid(cell))
                {
                    validationMessages.Add($"{arrowName}: segment {i} crosses inactive/outside cell {cell.x},{cell.y}.");
                    continue;
                }

                bool isSharedSegmentEndpoint = distance == 0 && i > 0;

                if (!isSharedSegmentEndpoint && !arrowCells.Add(cell))
                {
                    validationMessages.Add($"{arrowName}: cell {cell.x},{cell.y} is used more than once by the same arrow.");
                }

                if (occupiedCellOwners.TryGetValue(cell, out int ownerIndex) && ownerIndex != arrowIndex)
                {
                    validationMessages.Add($"{arrowName}: cell {cell.x},{cell.y} overlaps Arrow {ownerIndex + 1}.");
                }
                else
                {
                    occupiedCellOwners[cell] = arrowIndex;
                }
            }
        }

        Vector2Int previous = points.GetArrayElementAtIndex(points.arraySize - 2).vector2IntValue;
        Vector2Int head = points.GetArrayElementAtIndex(points.arraySize - 1).vector2IntValue;
        Vector2Int exitDelta = head - previous;

        if (exitDelta == Vector2Int.zero || (exitDelta.x != 0 && exitDelta.y != 0))
        {
            validationMessages.Add($"{arrowName}: last segment must be a straight line so exit direction is valid.");
        }

        if (PathArrowUtility.TryFindOwnExitBlock(pathPoints, GetWidth(), GetHeight(), GetActiveCellsForUtility(), out Vector2Int ownExitBlockCell))
        {
            validationMessages.Add($"{arrowName}: exit path hits its own body at {ownExitBlockCell.x},{ownExitBlockCell.y}.");
        }
    }

    private void ClampBoardSize()
    {
        widthProperty.intValue = Mathf.Max(1, widthProperty.intValue);
        heightProperty.intValue = Mathf.Max(1, heightProperty.intValue);
    }

    private void PruneActiveCellsToBounds()
    {
        if (activeCellsProperty == null)
        {
            return;
        }

        for (int i = activeCellsProperty.arraySize - 1; i >= 0; i--)
        {
            Vector2Int cell = activeCellsProperty.GetArrayElementAtIndex(i).vector2IntValue;
            if (!IsInsideGridBounds(cell))
            {
                activeCellsProperty.DeleteArrayElementAtIndex(i);
            }
        }

        RefreshActiveCellLookup();
    }

    private void ClampSelectedArrowPoints(SerializedProperty points)
    {
        if (points == null)
        {
            return;
        }

        for (int i = 0; i < points.arraySize; i++)
        {
            SerializedProperty point = points.GetArrayElementAtIndex(i);
            Vector2Int value = point.vector2IntValue;
            value.x = Mathf.Clamp(value.x, 0, GetWidth() - 1);
            value.y = Mathf.Clamp(value.y, 0, GetHeight() - 1);
            point.vector2IntValue = value;
        }
    }

    private bool IsInsideGrid(Vector2Int cell)
    {
        return IsCellActive(cell);
    }

    private bool IsInsideGridBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < GetWidth() && cell.y < GetHeight();
    }

    private bool IsCellActive(Vector2Int cell)
    {
        if (!IsInsideGridBounds(cell))
        {
            return false;
        }

        return !HasCustomShape() || activeCellLookup.Contains(cell);
    }

    private bool HasCustomShape()
    {
        return activeCellsProperty != null && activeCellsProperty.arraySize > 0;
    }

    private IReadOnlyCollection<Vector2Int> GetActiveCellsForUtility()
    {
        return HasCustomShape() ? activeCellLookup : null;
    }

    private void RefreshActiveCellLookup()
    {
        activeCellLookup.Clear();

        if (activeCellsProperty == null)
        {
            return;
        }

        for (int i = 0; i < activeCellsProperty.arraySize; i++)
        {
            Vector2Int cell = activeCellsProperty.GetArrayElementAtIndex(i).vector2IntValue;
            if (IsInsideGridBounds(cell))
            {
                activeCellLookup.Add(cell);
            }
        }
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
            SetStatus($"Cell {cell.x},{cell.y} is used by an arrow. Remove the arrow point before making it inactive.");
            return;
        }

        RecordUndo("Toggle Board Shape Cell");

        int existingIndex = FindActiveCellIndex(cell);
        if (existingIndex >= 0)
        {
            activeCellsProperty.DeleteArrayElementAtIndex(existingIndex);
            SetStatus($"Cell {cell.x},{cell.y} set inactive.");
        }
        else
        {
            int index = activeCellsProperty.arraySize;
            activeCellsProperty.InsertArrayElementAtIndex(index);
            activeCellsProperty.GetArrayElementAtIndex(index).vector2IntValue = cell;
            SetStatus($"Cell {cell.x},{cell.y} set active.");
        }

        levelObject.ApplyModifiedProperties();
        RefreshActiveCellLookup();
        ClearTesterResult();
        Repaint();
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
        int x = Mathf.FloorToInt((mousePosition.x - HeaderHeight) / pitch);
        int rowFromTop = Mathf.FloorToInt((mousePosition.y - HeaderHeight) / pitch);
        int y = GetHeight() - 1 - rowFromTop;
        cell = new Vector2Int(x, y);
        return IsInsideGridBounds(cell);
    }

    private void BeginShapePaint(Vector2Int cell)
    {
        shapePaintTrailCells.Clear();
        shapePaintSetActive = !IsCellActive(cell);
        shapePaintLastCell = cell;
        isShapePaintDragging = true;

        RecordUndo("Paint Board Shape");

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
        Repaint();
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
        if (!IsInsideGridBounds(cell) || activeCellsProperty == null)
        {
            return;
        }

        shapePaintTrailCells.Add(cell);

        if (!shapePaintSetActive && occupiedCellOwners.ContainsKey(cell))
        {
            SetStatus($"Cell {cell.x},{cell.y} is used by an arrow. Remove the arrow point before making it inactive.");
            return;
        }

        int existingIndex = FindActiveCellIndex(cell);

        if (shapePaintSetActive)
        {
            if (existingIndex >= 0)
            {
                return;
            }

            int index = activeCellsProperty.arraySize;
            activeCellsProperty.InsertArrayElementAtIndex(index);
            activeCellsProperty.GetArrayElementAtIndex(index).vector2IntValue = cell;
            SetStatus($"Painting active cells. Last cell: {cell.x},{cell.y}.");
        }
        else
        {
            if (existingIndex < 0)
            {
                return;
            }

            activeCellsProperty.DeleteArrayElementAtIndex(existingIndex);
            SetStatus($"Painting inactive cells. Last cell: {cell.x},{cell.y}.");
        }

        levelObject.ApplyModifiedProperties();
        RefreshActiveCellLookup();
        ClearTesterResult();
        Repaint();
    }

    private int FindActiveCellIndex(Vector2Int cell)
    {
        if (activeCellsProperty == null)
        {
            return -1;
        }

        for (int i = 0; i < activeCellsProperty.arraySize; i++)
        {
            if (activeCellsProperty.GetArrayElementAtIndex(i).vector2IntValue == cell)
            {
                return i;
            }
        }

        return -1;
    }

    private void EnableCustomShapeFromFullRectangle(bool recordUndo = true)
    {
        if (activeCellsProperty == null)
        {
            return;
        }

        if (recordUndo)
        {
            RecordUndo("Enable Custom Board Shape");
        }

        activeCellsProperty.ClearArray();

        for (int y = 0; y < GetHeight(); y++)
        {
            for (int x = 0; x < GetWidth(); x++)
            {
                int index = activeCellsProperty.arraySize;
                activeCellsProperty.InsertArrayElementAtIndex(index);
                activeCellsProperty.GetArrayElementAtIndex(index).vector2IntValue = new Vector2Int(x, y);
            }
        }

        levelObject.ApplyModifiedProperties();
        RefreshActiveCellLookup();
        editBoardShape = true;
        ClearTesterResult();
        SetStatus("Custom shape enabled. Click or drag grid cells to paint holes.");
        Repaint();
    }

    private void UseFullRectangleShape()
    {
        if (activeCellsProperty == null || activeCellsProperty.arraySize == 0)
        {
            editBoardShape = false;
            return;
        }

        RecordUndo("Use Full Rectangle Board Shape");
        activeCellsProperty.ClearArray();
        levelObject.ApplyModifiedProperties();
        RefreshActiveCellLookup();
        editBoardShape = false;
        ClearTesterResult();
        SetStatus("Board shape reset to full rectangle.");
        Repaint();
    }

    private int GetWidth()
    {
        return widthProperty != null ? Mathf.Max(1, widthProperty.intValue) : 1;
    }

    private int GetHeight()
    {
        return heightProperty != null ? Mathf.Max(1, heightProperty.intValue) : 1;
    }

    private SerializedProperty GetSelectedArrowProperty()
    {
        if (!HasSelectedArrow())
        {
            return null;
        }

        return arrowsProperty.GetArrayElementAtIndex(selectedArrowIndex);
    }

    private SerializedProperty GetSelectedPointsProperty()
    {
        SerializedProperty arrow = GetSelectedArrowProperty();
        return arrow?.FindPropertyRelative("points");
    }

    private bool HasSelectedArrow()
    {
        return arrowsProperty != null && selectedArrowIndex >= 0 && selectedArrowIndex < arrowsProperty.arraySize;
    }

    private void ClampSelectedArrowIndex()
    {
        if (arrowsProperty == null || arrowsProperty.arraySize == 0)
        {
            selectedArrowIndex = -1;
            return;
        }

        selectedArrowIndex = Mathf.Clamp(selectedArrowIndex, 0, arrowsProperty.arraySize - 1);
    }

    private void SetLevelAsset(PathArrowLevelData newLevelAsset)
    {
        levelAsset = newLevelAsset;
        selectedArrowIndex = -1;
        ClearTesterResult();
        levelObject = null;
        EnsureSerializedObject();
        SetStatus(levelAsset == null ? "Pick or create a level asset." : $"Loaded {levelAsset.name}.");
        Repaint();
    }

    private void EnsureSerializedObject()
    {
        if (levelAsset == null)
        {
            levelObject = null;
            widthProperty = null;
            heightProperty = null;
            activeCellsProperty = null;
            arrowsProperty = null;
            return;
        }

        if (levelObject == null || levelObject.targetObject != levelAsset)
        {
            levelObject = new SerializedObject(levelAsset);
            widthProperty = levelObject.FindProperty("width");
            heightProperty = levelObject.FindProperty("height");
            activeCellsProperty = levelObject.FindProperty("activeCells");
            arrowsProperty = levelObject.FindProperty("arrows");
            ClampSelectedArrowIndex();
        }
    }

    private void CreateLevelAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Path Arrow Level",
            "PathArrowLevel_New",
            "asset",
            "Choose where to save the new level asset.",
            "Assets/LevelsData");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        PathArrowLevelData createdLevel = CreateInstance<PathArrowLevelData>();
        AssetDatabase.CreateAsset(createdLevel, path);
        AssetDatabase.SaveAssets();
        SetLevelAsset(createdLevel);
        Selection.activeObject = createdLevel;
        SetStatus("Created new level asset.");
    }

    private void SaveLevel()
    {
        if (levelAsset == null)
        {
            return;
        }

        levelObject?.ApplyModifiedProperties();
        EditorUtility.SetDirty(levelAsset);
        AssetDatabase.SaveAssets();
        SetStatus("Level saved.");
    }

    private void RecordUndo(string actionName)
    {
        if (levelAsset == null)
        {
            return;
        }

        ClearTesterResult();
        Undo.RecordObject(levelAsset, actionName);
        EditorUtility.SetDirty(levelAsset);
    }

    private void ClearTesterResult()
    {
        hasTesterResult = false;
        testerSolved = false;
        testerMessages.Clear();
        testerClearOrder.Clear();
        testerHighlightedArrowIndex = -1;
    }
    private void SetStatus(string message)
    {
        statusMessage = message;
    }

    private void HandleSelectionChanged()
    {
        if (Selection.activeObject is PathArrowLevelData selectedLevel && selectedLevel != levelAsset)
        {
            SetLevelAsset(selectedLevel);
        }
    }

    private class TestArrow
    {
        public int Index;
        public string Name;
        public Vector2Int Head;
        public Vector2Int ExitDirection;
        public readonly List<Vector2Int> Points = new List<Vector2Int>();
        public readonly HashSet<Vector2Int> Cells = new HashSet<Vector2Int>();
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
}
