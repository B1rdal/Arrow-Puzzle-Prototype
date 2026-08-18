/*
Summary:
PathArrowLevelData is the editable ScriptableObject for a puzzle level. Each arrow is
stored as tail-to-head grid points, where the last segment decides the escape
direction. Levels can optionally define activeCells for non-rectangular boards.
hasCustomShape tells the runtime whether activeCells is being used as a board mask.
*/

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PathArrowData
{
    [SerializeField] private string id = "Path Arrow";
    [SerializeField] private Color color = new Color(0.25f, 0.65f, 1f);
    [SerializeField] private List<Vector2Int> points = new List<Vector2Int>();

    public string Id => string.IsNullOrWhiteSpace(id) ? "Path Arrow" : id;
    public Color Color => color;
    public IReadOnlyList<Vector2Int> Points => points;

    public PathArrowData()
    {
    }

    public PathArrowData(string id, Color color, IEnumerable<Vector2Int> points)
    {
        this.id = id;
        this.color = color;
        this.points = new List<Vector2Int>(points);
    }
}

[CreateAssetMenu(fileName = "LevelData", menuName = "Arrow Escape/Level Data")]
public class PathArrowLevelData : ScriptableObject
{
    private static readonly Vector2Int[] EmptyActiveCells = new Vector2Int[0];

    [Min(1)]
    [SerializeField] private int width = 8;

    [Min(1)]
    [SerializeField] private int height = 8;

    [SerializeField] private List<PathArrowData> arrows = new List<PathArrowData>();
    [SerializeField] private bool hasCustomShape;
    [SerializeField] private List<Vector2Int> activeCells = new List<Vector2Int>();

    public int Width => Mathf.Max(1, width);
    public int Height => Mathf.Max(1, height);
    public IReadOnlyList<PathArrowData> Arrows => arrows;
    public IReadOnlyList<Vector2Int> ActiveCells
    {
        get
        {
            if (activeCells != null)
            {
                return activeCells;
            }

            return EmptyActiveCells;
        }
    }
    public bool HasCustomShape => hasCustomShape || (activeCells != null && activeCells.Count > 0);

    public bool IsCellActive(Vector2Int cell)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x >= Width || cell.y >= Height)
        {
            return false;
        }

        if (!HasCustomShape)
        {
            return true;
        }

        for (int i = 0; i < activeCells.Count; i++)
        {
            if (activeCells[i] == cell)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        // Keep assets valid even if values are typed manually in the Inspector.
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        if (arrows == null)
        {
            arrows = new List<PathArrowData>();
        }

        if (activeCells == null)
        {
            activeCells = new List<Vector2Int>();
        }

        RemoveInvalidOrDuplicateActiveCells();

        if (activeCells.Count > 0)
        {
            hasCustomShape = true;
        }
    }

    private void RemoveInvalidOrDuplicateActiveCells()
    {
        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();

        for (int i = activeCells.Count - 1; i >= 0; i--)
        {
            Vector2Int cell = activeCells[i];

            if (cell.x < 0 || cell.y < 0 || cell.x >= Width || cell.y >= Height || !uniqueCells.Add(cell))
            {
                activeCells.RemoveAt(i);
            }
        }
    }
}
