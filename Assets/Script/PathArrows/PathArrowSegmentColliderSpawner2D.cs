using System.Collections.Generic;
using UnityEngine;

// Builds one BoxCollider2D per LineRenderer segment so the whole arrow path is clickable.
public class PathArrowSegmentColliderSpawner2D : MonoBehaviour
{
    private readonly List<GameObject> spawnedSegments = new List<GameObject>();

    private LineRenderer lineRenderer;
    private PathArrow owner;
    private float thickness = 0.3f;
    private float extraLength = 0.08f;

    public void Initialize(LineRenderer targetLineRenderer, PathArrow pathArrow, float lineThickness)
    {
        lineRenderer = targetLineRenderer;
        owner = pathArrow;
        thickness = Mathf.Max(0.05f, lineThickness);
        RebuildSegments();
    }

    public void UpdateSegments()
    {
        if (lineRenderer == null || lineRenderer.positionCount < 2)
        {
            ClearSegments();
            return;
        }

        int segmentCount = lineRenderer.positionCount - 1;

        if (spawnedSegments.Count != segmentCount)
        {
            // The animation can remove path points, so the collider count must follow it.
            RebuildSegments();
            return;
        }

        for (int i = 0; i < segmentCount; i++)
        {
            UpdateSegment(i, spawnedSegments[i]);
        }
    }

    public void ClearSegments()
    {
        for (int i = spawnedSegments.Count - 1; i >= 0; i--)
        {
            if (spawnedSegments[i] != null)
            {
                DestroySegment(spawnedSegments[i]);
            }
        }

        spawnedSegments.Clear();
    }

    private void RebuildSegments()
    {
        ClearSegments();

        if (lineRenderer == null || lineRenderer.positionCount < 2)
        {
            return;
        }

        for (int i = 0; i < lineRenderer.positionCount - 1; i++)
        {
            GameObject segment = new GameObject($"SegmentCollider_{i}");
            segment.transform.SetParent(transform, false);

            BoxCollider2D collider = segment.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            PathArrowColliderProxy proxy = segment.AddComponent<PathArrowColliderProxy>();
            proxy.Initialize(owner);

            spawnedSegments.Add(segment);
            UpdateSegment(i, segment);
        }
    }

    private void UpdateSegment(int index, GameObject segment)
    {
        if (segment == null || lineRenderer == null || index >= lineRenderer.positionCount - 1)
        {
            return;
        }

        Vector3 a = lineRenderer.GetPosition(index);
        Vector3 b = lineRenderer.GetPosition(index + 1);
        Vector3 direction = b - a;
        float length = direction.magnitude;

        segment.transform.localPosition = (a + b) * 0.5f;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        segment.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        BoxCollider2D collider = segment.GetComponent<BoxCollider2D>();

        if (collider != null)
        {
            // Collider length follows the rendered segment, with a little padding for easier taps.
            collider.size = new Vector2(length + extraLength, thickness * 1.5f);
            collider.offset = Vector2.zero;
        }
    }

    private static void DestroySegment(GameObject segment)
    {
        if (Application.isPlaying)
        {
            Destroy(segment);
        }
        else
        {
            DestroyImmediate(segment);
        }
    }

    private void OnDestroy()
    {
        ClearSegments();
    }
}
