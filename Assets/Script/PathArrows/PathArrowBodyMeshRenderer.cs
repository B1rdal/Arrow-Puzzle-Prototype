/*
Summary:
PathArrowBodyMeshRenderer draws the visible arrow body from the hidden LineRenderer
positions. It builds straight quads for each segment and small caps at joins, which
avoids LineRenderer corner artifacts when a moving arrow passes through turns.
*/

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PathArrowBodyMeshRenderer : MonoBehaviour
{
    private const float MinSegmentLength = 0.001f;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();

    [SerializeField] private LineRenderer sourceLineRenderer;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [Min(3)]
    [SerializeField] private int capSegments = 10;

    private Mesh bodyMesh;
    private float fallbackThickness = 0.28f;

    public void Initialize(LineRenderer source, Material material, float lineThickness)
    {
        sourceLineRenderer = source;
        fallbackThickness = Mathf.Max(0.01f, lineThickness);
        EnsureMeshObjects();
        SetMaterial(material);
        CopySortingFromSource();
        Refresh();
    }

    public void SetMaterial(Material material)
    {
        EnsureMeshObjects();
        meshRenderer.sharedMaterial = material;
    }

    public void Refresh()
    {
        EnsureMeshObjects();

        if (sourceLineRenderer == null || sourceLineRenderer.positionCount < 2)
        {
            ClearMesh();
            return;
        }

        BuildBodyMesh();
    }

    public void ForceRendererRefresh()
    {
        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.enabled = false;
        meshRenderer.enabled = true;
    }

    private void EnsureMeshObjects()
    {
        if (meshFilter != null && meshRenderer != null)
        {
            return;
        }

        Transform bodyTransform = transform.Find("ArrowBodyMesh");

        if (bodyTransform == null)
        {
            GameObject bodyObject = new GameObject("ArrowBodyMesh");
            bodyTransform = bodyObject.transform;
            bodyTransform.SetParent(transform, false);
        }

        bodyTransform.localPosition = Vector3.zero;
        bodyTransform.localRotation = Quaternion.identity;
        bodyTransform.localScale = Vector3.one;

        meshFilter = bodyTransform.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = bodyTransform.gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = bodyTransform.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = bodyTransform.gameObject.AddComponent<MeshRenderer>();
        }

        if (bodyMesh == null)
        {
            bodyMesh = new Mesh();
            bodyMesh.name = "PathArrowBodyMesh";
        }

        meshFilter.sharedMesh = bodyMesh;
    }

    private void CopySortingFromSource()
    {
        if (sourceLineRenderer == null || meshRenderer == null)
        {
            return;
        }

        meshRenderer.sortingLayerID = sourceLineRenderer.sortingLayerID;
        meshRenderer.sortingOrder = sourceLineRenderer.sortingOrder;
    }

    private void BuildBodyMesh()
    {
        vertices.Clear();
        triangles.Clear();

        float thickness = Mathf.Max(
            fallbackThickness,
            sourceLineRenderer != null ? Mathf.Max(sourceLineRenderer.startWidth, sourceLineRenderer.endWidth) : 0f);
        float radius = thickness * 0.5f;

        for (int i = 0; i < sourceLineRenderer.positionCount - 1; i++)
        {
            Vector3 a = sourceLineRenderer.GetPosition(i);
            Vector3 b = sourceLineRenderer.GetPosition(i + 1);
            AddSegmentQuad(a, b, radius);
        }

        for (int i = 0; i < sourceLineRenderer.positionCount; i++)
        {
            AddCap(sourceLineRenderer.GetPosition(i), radius);
        }

        bodyMesh.Clear();
        bodyMesh.SetVertices(vertices);
        bodyMesh.SetTriangles(triangles, 0);
        bodyMesh.RecalculateBounds();
    }

    private void AddSegmentQuad(Vector3 a, Vector3 b, float radius)
    {
        Vector3 direction = b - a;

        if (direction.sqrMagnitude <= MinSegmentLength * MinSegmentLength)
        {
            return;
        }

        direction.Normalize();
        Vector3 normal = new Vector3(-direction.y, direction.x, 0f) * radius;
        int startIndex = vertices.Count;

        vertices.Add(a + normal);
        vertices.Add(a - normal);
        vertices.Add(b + normal);
        vertices.Add(b - normal);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);
    }

    private void AddCap(Vector3 center, float radius)
    {
        int safeCapSegments = Mathf.Max(3, capSegments);
        int centerIndex = vertices.Count;
        vertices.Add(center);

        for (int i = 0; i < safeCapSegments; i++)
        {
            float angle = (i / (float)safeCapSegments) * Mathf.PI * 2f;
            vertices.Add(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }

        for (int i = 0; i < safeCapSegments; i++)
        {
            int current = centerIndex + 1 + i;
            int next = centerIndex + 1 + ((i + 1) % safeCapSegments);
            triangles.Add(centerIndex);
            triangles.Add(next);
            triangles.Add(current);
        }
    }

    private void ClearMesh()
    {
        if (bodyMesh != null)
        {
            bodyMesh.Clear();
        }
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && bodyMesh != null)
        {
            Destroy(bodyMesh);
        }
    }
}
