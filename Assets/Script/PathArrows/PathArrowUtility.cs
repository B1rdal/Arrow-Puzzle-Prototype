/*
Summary:
PathArrowUtility contains grid-only helper logic shared by level building and move
validation. It turns an arrow's corner points into occupied cells and finds the
arrow's exit direction from its final segment.
*/

using System.Collections.Generic;
using UnityEngine;

public static class PathArrowUtility
{
    // Expands tail-to-head corner points into every grid cell occupied by the arrow path.
    public static bool TryBuildOccupiedCells(IReadOnlyList<Vector2Int> points, List<Vector2Int> cells)
    {
        cells.Clear();

        if (points == null || points.Count < 2)
        {
            return false;
        }

        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2Int start = points[i];
            Vector2Int end = points[i + 1];
            Vector2Int delta = end - start;

            if (delta == Vector2Int.zero)
            {
                continue;
            }

            if (delta.x != 0 && delta.y != 0)
            {
                // Diagonal segments do not fit the current grid-escape rule.
                return false;
            }

            Vector2Int step = new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
            int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

            for (int distance = 0; distance <= length; distance++)
            {
                uniqueCells.Add(start + step * distance);
            }
        }

        cells.AddRange(uniqueCells);
        return cells.Count > 0 && GetExitDirection(points) != Vector2Int.zero;
    }

    public static Vector2Int GetExitDirection(IReadOnlyList<Vector2Int> points)
    {
        if (points == null || points.Count < 2)
        {
            return Vector2Int.zero;
        }

        Vector2Int previous = points[points.Count - 2];
        Vector2Int head = points[points.Count - 1];
        Vector2Int delta = head - previous;

        if (delta.x != 0 && delta.y != 0)
        {
            return Vector2Int.zero;
        }

        // The final segment direction is the direction the arrow exits the board.
        return new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
    }
}
