/*
Summary:
PathArrowUtility contains grid-only helper logic shared by level building and move
validation. It turns an arrow's corner points into occupied cells and finds the
arrow's exit direction from its final segment, and checks for invalid self-crossing
paths. Optional active cells let non-rectangular boards treat holes as outside.
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
                return false;
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

    // Reports the first grid cell that the same arrow path visits twice.
    public static bool TryFindSelfOverlap(IReadOnlyList<Vector2Int> points, out Vector2Int overlapCell, out int segmentIndex)
    {
        overlapCell = Vector2Int.zero;
        segmentIndex = -1;

        if (points == null || points.Count < 2)
        {
            return false;
        }

        HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2Int start = points[i];
            Vector2Int end = points[i + 1];
            Vector2Int delta = end - start;

            if (delta == Vector2Int.zero || (delta.x != 0 && delta.y != 0))
            {
                return false;
            }

            Vector2Int step = new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
            int length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            int firstDistance = i == 0 ? 0 : 1;

            for (int distance = firstDistance; distance <= length; distance++)
            {
                Vector2Int cell = start + step * distance;

                if (!visitedCells.Add(cell))
                {
                    overlapCell = cell;
                    segmentIndex = i;
                    return true;
                }
            }
        }

        return false;
    }

    // Reports the first own-body cell sitting in front of the arrow head.
    public static bool TryFindOwnExitBlock(IReadOnlyList<Vector2Int> points, int boardWidth, int boardHeight, out Vector2Int blockedCell)
    {
        return TryFindOwnExitBlock(points, boardWidth, boardHeight, null, out blockedCell);
    }

    public static bool TryFindOwnExitBlock(
        IReadOnlyList<Vector2Int> points,
        int boardWidth,
        int boardHeight,
        IReadOnlyCollection<Vector2Int> activeCells,
        out Vector2Int blockedCell)
    {
        bool hasCustomShape = activeCells != null && activeCells.Count > 0;
        return TryFindOwnExitBlock(points, boardWidth, boardHeight, hasCustomShape, activeCells, out blockedCell);
    }

    public static bool TryFindOwnExitBlock(
        IReadOnlyList<Vector2Int> points,
        int boardWidth,
        int boardHeight,
        bool hasCustomShape,
        IReadOnlyCollection<Vector2Int> activeCells,
        out Vector2Int blockedCell)
    {
        blockedCell = Vector2Int.zero;

        if (points == null || points.Count < 2)
        {
            return false;
        }

        Vector2Int exitDirection = GetExitDirection(points);

        if (exitDirection == Vector2Int.zero)
        {
            return false;
        }

        List<Vector2Int> occupiedCells = new List<Vector2Int>();

        if (!TryBuildOccupiedCells(points, occupiedCells))
        {
            return false;
        }

        HashSet<Vector2Int> occupiedLookup = new HashSet<Vector2Int>(occupiedCells);
        Vector2Int checkPosition = points[points.Count - 1] + exitDirection;

        // Custom-shape holes do not stop an exit ray. The arrow keeps travelling
        // through them until it leaves the rectangular board bounds.
        while (IsInsideGrid(checkPosition, boardWidth, boardHeight))
        {
            if (occupiedLookup.Contains(checkPosition))
            {
                blockedCell = checkPosition;
                return true;
            }

            checkPosition += exitDirection;
        }

        return false;
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

    private static bool IsInsideGrid(Vector2Int cell, int boardWidth, int boardHeight)
    {
        return cell.x >= 0
            && cell.y >= 0
            && cell.x < Mathf.Max(1, boardWidth)
            && cell.y < Mathf.Max(1, boardHeight);
    }

    private static bool IsPlayableCell(
        Vector2Int cell,
        int boardWidth,
        int boardHeight,
        IReadOnlyCollection<Vector2Int> activeCells)
    {
        bool hasCustomShape = activeCells != null && activeCells.Count > 0;
        return IsPlayableCell(cell, boardWidth, boardHeight, hasCustomShape, activeCells);
    }

    private static bool IsPlayableCell(
        Vector2Int cell,
        int boardWidth,
        int boardHeight,
        bool hasCustomShape,
        IReadOnlyCollection<Vector2Int> activeCells)
    {
        if (!IsInsideGrid(cell, boardWidth, boardHeight))
        {
            return false;
        }

        if (!hasCustomShape)
        {
            return true;
        }

        if (activeCells == null || activeCells.Count == 0)
        {
            return false;
        }

        foreach (Vector2Int activeCell in activeCells)
        {
            if (activeCell == cell)
            {
                return true;
            }
        }

        return false;
    }
}
