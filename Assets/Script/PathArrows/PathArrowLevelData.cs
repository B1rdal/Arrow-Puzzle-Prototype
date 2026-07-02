/*
Summary:
PathArrowLevelData is the editable ScriptableObject for a puzzle level. Each arrow is
stored as tail-to-head grid points, where the last segment decides the escape
direction.
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

[CreateAssetMenu(fileName = "PathArrowLevelData", menuName = "Arrow Escape/Level Data")]
public class PathArrowLevelData : ScriptableObject
{
    [Min(1)]
    [SerializeField] private int width = 8;

    [Min(1)]
    [SerializeField] private int height = 8;

    [SerializeField] private List<PathArrowData> arrows = new List<PathArrowData>();

    public int Width => Mathf.Max(1, width);
    public int Height => Mathf.Max(1, height);
    public IReadOnlyList<PathArrowData> Arrows => arrows;

    private void OnValidate()
    {
        // Keep assets valid even if values are typed manually in the Inspector.
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        if (arrows == null)
        {
            arrows = new List<PathArrowData>();
        }
    }
}
