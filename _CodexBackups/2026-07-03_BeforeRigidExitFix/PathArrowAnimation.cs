/*
Summary:
PathArrowAnimation moves an arrow along its own path and then out of the grid. It
samples the original path by distance every frame so curved/turning arrows move
smoothly without relying on physics.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

public class PathArrowAnimation : MonoBehaviour
{
    private const float MinPointDistance = 0.001f;
    private const float CollinearDotThreshold = 0.9995f;

    private readonly List<Vector3> sourcePoints = new List<Vector3>();
    private readonly List<float> sourceDistances = new List<float>();
    private readonly List<Vector3> visiblePoints = new List<Vector3>();

    private LineRenderer lineRenderer;
    private Vector3 exitDirection = Vector3.right;
    private Rect exitBounds;
    private float exitMargin;
    private float speed = 6f;
    private float sourcePathLength;
    private int fullLengthSampleCount = 24;
    private float travelDistance;
    private float turnSnapDistance = 0.08f;
    private bool shortenBeforeExit = true;
    private float targetVisibleLength = 1.6f;
    private float shorteningTailSpeedMultiplier = 2.25f;
    private bool isPlaying;
    private bool hasExitBounds;
    private bool hasNotifiedHeadCrossedExitBounds;

    public bool IsPlaying => isPlaying;

    public event Action OnPositionsChanged;
    public event Action OnHeadCrossedExitBounds;
    public event Action OnCompleted;

    public void Initialize(
        LineRenderer targetLineRenderer,
        float moveSpeed,
        bool shortenPathBeforeExit = true,
        float exitTargetVisibleLength = 1.6f,
        float tailSpeedMultiplier = 2.25f)
    {
        lineRenderer = targetLineRenderer;
        speed = Mathf.Max(0.1f, moveSpeed);
        shortenBeforeExit = shortenPathBeforeExit;
        targetVisibleLength = Mathf.Max(0.1f, exitTargetVisibleLength);
        shorteningTailSpeedMultiplier = Mathf.Max(1f, tailSpeedMultiplier);
        turnSnapDistance = CalculateTurnSnapDistance();
        BuildSourcePathFromRenderer();
        UpdateFullLengthSampleCount();
        enabled = false;
    }

    public void PlayForward(Vector3 direction)
    {
        if (lineRenderer == null || lineRenderer.positionCount < 2)
        {
            return;
        }

        BuildSourcePathFromRenderer();
        UpdateFullLengthSampleCount();
        exitDirection = GetSafeExitDirection(direction);
        hasExitBounds = false;
        hasNotifiedHeadCrossedExitBounds = false;
        travelDistance = 0f;
        isPlaying = sourcePathLength > MinPointDistance;
        enabled = isPlaying;
    }

    public void PlayForward(Vector3 direction, Rect localExitBounds, float outsideMargin)
    {
        if (lineRenderer == null || lineRenderer.positionCount < 2)
        {
            return;
        }

        BuildSourcePathFromRenderer();
        UpdateFullLengthSampleCount();
        exitDirection = GetSafeExitDirection(direction);
        exitBounds = localExitBounds;
        exitMargin = Mathf.Max(0f, outsideMargin);
        hasExitBounds = true;
        hasNotifiedHeadCrossedExitBounds = false;
        travelDistance = 0f;
        isPlaying = sourcePathLength > MinPointDistance;
        enabled = isPlaying;
    }

    public void Stop()
    {
        isPlaying = false;
        enabled = false;
    }

    private void Update()
    {
        if (!isPlaying || lineRenderer == null)
        {
            Stop();
            return;
        }

        travelDistance += speed * Time.deltaTime;

        // Distances are measured along the original path, then sampled into visible points.
        float headDistance = sourcePathLength + travelDistance;
        float tailDistance = CalculateTailDistance(headDistance);

        if (!hasExitBounds && tailDistance >= headDistance - MinPointDistance)
        {
            CompleteAndClear();
            return;
        }

        BuildVisibleWindow(tailDistance, headDistance);
        ApplyVisiblePointsToRenderer();
        OnPositionsChanged?.Invoke();

        if (visiblePoints.Count < 2)
        {
            CompleteAndClear();
            return;
        }

        if (hasExitBounds && HasHeadCrossedExitBounds())
        {
            NotifyHeadCrossedExitBounds();
        }
    }

    private float CalculateTailDistance(float headDistance)
    {
        if (!hasExitBounds)
        {
            return GetDistanceSnappedPastNearbyTurn(travelDistance * 2f);
        }

        if (!shortenBeforeExit)
        {
            // Full-length exits must move continuously; snapping the tail creates visible pops at turns.
            return travelDistance;
        }

        // Shortening behavior: the tail can move faster until the visible length reaches the target.
        float fasterTailDistance = travelDistance * shorteningTailSpeedMultiplier;
        float targetLengthTailDistance = Mathf.Max(0f, headDistance - targetVisibleLength);
        return GetDistanceSnappedPastNearbyTurn(Mathf.Min(fasterTailDistance, targetLengthTailDistance));
    }

    private void BuildSourcePathFromRenderer()
    {
        sourcePoints.Clear();
        sourceDistances.Clear();
        sourcePathLength = 0f;

        if (lineRenderer == null || lineRenderer.positionCount == 0)
        {
            return;
        }

        Vector3 previousPoint = lineRenderer.GetPosition(0);
        sourcePoints.Add(previousPoint);
        sourceDistances.Add(0f);

        for (int i = 1; i < lineRenderer.positionCount; i++)
        {
            Vector3 point = lineRenderer.GetPosition(i);
            float segmentLength = Vector3.Distance(previousPoint, point);

            if (segmentLength <= MinPointDistance)
            {
                continue;
            }

            sourcePathLength += segmentLength;
            sourcePoints.Add(point);
            sourceDistances.Add(sourcePathLength);
            previousPoint = point;
        }
    }

    private Vector3 GetSafeExitDirection(Vector3 requestedDirection)
    {
        if (requestedDirection.sqrMagnitude > 0.0001f)
        {
            return requestedDirection.normalized;
        }

        if (sourcePoints.Count >= 2)
        {
            Vector3 fallback = sourcePoints[sourcePoints.Count - 1] - sourcePoints[sourcePoints.Count - 2];

            if (fallback.sqrMagnitude > 0.0001f)
            {
                return fallback.normalized;
            }
        }

        return Vector3.right;
    }

    private float GetDistanceSnappedPastNearbyTurn(float distance)
    {
        if (distance >= sourcePathLength)
        {
            return distance;
        }

        for (int i = 1; i < sourceDistances.Count; i++)
        {
            float turnDistance = sourceDistances[i];

            if (turnDistance <= distance)
            {
                continue;
            }

            if (turnDistance - distance <= turnSnapDistance)
            {
                // Snapping through tiny turn gaps reduces visual jitter around corners.
                return turnDistance;
            }

            return distance;
        }

        return distance;
    }

    private void BuildVisibleWindow(float tailDistance, float headDistance)
    {
        visiblePoints.Clear();

        if (headDistance <= tailDistance + MinPointDistance)
        {
            return;
        }

        if (hasExitBounds && !shortenBeforeExit)
        {
            BuildFullLengthVisibleWindow(tailDistance, headDistance);
            return;
        }

        // The visible arrow is a sliding window from tailDistance to headDistance.
        AddVisiblePoint(SamplePosition(tailDistance));

        float clampedHeadDistance = Mathf.Min(headDistance, sourcePathLength);

        for (int i = 1; i < sourcePoints.Count; i++)
        {
            float pointDistance = sourceDistances[i];

            if (pointDistance <= tailDistance + MinPointDistance)
            {
                continue;
            }

            if (pointDistance >= clampedHeadDistance - MinPointDistance)
            {
                break;
            }

            AddVisiblePoint(sourcePoints[i]);
        }

        AddVisiblePoint(SamplePosition(headDistance));
        RemoveStraightMiddlePoints();
    }

    private void BuildFullLengthVisibleWindow(float tailDistance, float headDistance)
    {
        // Full-length mode uses a fixed number of evenly spaced samples.
        // This avoids LineRenderer corner rebuilds and avoids zero-length duplicate segments.
        int sampleCount = Mathf.Max(2, fullLengthSampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 1f : i / (float)(sampleCount - 1);
            float sampleDistance = Mathf.Lerp(tailDistance, headDistance, t);
            visiblePoints.Add(SamplePosition(sampleDistance));
        }
    }

    private Vector3 SamplePosition(float distance)
    {
        if (sourcePoints.Count == 0)
        {
            return Vector3.zero;
        }

        if (distance <= 0f)
        {
            return sourcePoints[0];
        }

        if (distance >= sourcePathLength)
        {
            // Past the original path, continue straight in the final escape direction.
            return sourcePoints[sourcePoints.Count - 1] + exitDirection * (distance - sourcePathLength);
        }

        for (int i = 0; i < sourceDistances.Count - 1; i++)
        {
            float startDistance = sourceDistances[i];
            float endDistance = sourceDistances[i + 1];

            if (distance > endDistance)
            {
                continue;
            }

            float segmentLength = endDistance - startDistance;

            if (segmentLength <= MinPointDistance)
            {
                return sourcePoints[i + 1];
            }

            float t = Mathf.Clamp01((distance - startDistance) / segmentLength);
            return Vector3.LerpUnclamped(sourcePoints[i], sourcePoints[i + 1], t);
        }

        return sourcePoints[sourcePoints.Count - 1];
    }

    private void AddVisiblePoint(Vector3 point)
    {
        if (visiblePoints.Count > 0
            && Vector3.Distance(visiblePoints[visiblePoints.Count - 1], point) <= MinPointDistance)
        {
            return;
        }

        visiblePoints.Add(point);
    }

    private void RemoveStraightMiddlePoints()
    {
        for (int i = 1; i < visiblePoints.Count - 1; i++)
        {
            Vector3 before = visiblePoints[i] - visiblePoints[i - 1];
            Vector3 after = visiblePoints[i + 1] - visiblePoints[i];

            if (before.sqrMagnitude <= MinPointDistance * MinPointDistance
                || after.sqrMagnitude <= MinPointDistance * MinPointDistance)
            {
                visiblePoints.RemoveAt(i);
                i--;
                continue;
            }

            float dot = Vector3.Dot(before.normalized, after.normalized);

            if (dot > CollinearDotThreshold)
            {
                visiblePoints.RemoveAt(i);
                i--;
            }
        }
    }

    private float CalculateTurnSnapDistance()
    {
        if (lineRenderer == null)
        {
            return 0.08f;
        }

        float width = Mathf.Max(lineRenderer.startWidth, lineRenderer.endWidth);
        return Mathf.Clamp(width * 0.45f, 0.04f, 0.16f);
    }

    private void UpdateFullLengthSampleCount()
    {
        if (lineRenderer == null || sourcePathLength <= MinPointDistance)
        {
            fullLengthSampleCount = 2;
            return;
        }

        float width = Mathf.Max(lineRenderer.startWidth, lineRenderer.endWidth);
        float sampleSpacing = Mathf.Clamp(width * 0.75f, 0.14f, 0.28f);
        fullLengthSampleCount = Mathf.Clamp(Mathf.CeilToInt(sourcePathLength / sampleSpacing) + 1, 8, 120);
    }

    private void NotifyHeadCrossedExitBounds()
    {
        if (hasNotifiedHeadCrossedExitBounds)
        {
            return;
        }

        hasNotifiedHeadCrossedExitBounds = true;
        OnHeadCrossedExitBounds?.Invoke();
    }

    private void CompleteAndClear()
    {
        Stop();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }

        visiblePoints.Clear();
        OnPositionsChanged?.Invoke();
        OnCompleted?.Invoke();
    }

    private bool HasHeadCrossedExitBounds()
    {
        if (visiblePoints.Count == 0)
        {
            return true;
        }

        Vector3 head = visiblePoints[visiblePoints.Count - 1];

        // Compare only along the exit axis so side movement does not accidentally finish the arrow.
        if (Mathf.Abs(exitDirection.x) >= Mathf.Abs(exitDirection.y))
        {
            if (exitDirection.x > 0f)
            {
                return head.x > exitBounds.xMax + exitMargin;
            }

            return head.x < exitBounds.xMin - exitMargin;
        }

        if (exitDirection.y > 0f)
        {
            return head.y > exitBounds.yMax + exitMargin;
        }

        return head.y < exitBounds.yMin - exitMargin;
    }

    private void ApplyVisiblePointsToRenderer()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = visiblePoints.Count;

        for (int i = 0; i < visiblePoints.Count; i++)
        {
            lineRenderer.SetPosition(i, visiblePoints[i]);
        }
    }
}
