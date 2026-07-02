/*
Summary:
GridCenterDotFactory builds small mesh dots at cell centers. GameManager uses these
dots as a visual grid guide while testing and tuning levels.
*/

using UnityEngine;

public static class GridCenterDotFactory
{
    private const int SegmentCount = 20;

    // useLocalPosition lets both world-space and parent-local board systems reuse the same factory.
    public static GameObject CreateDot(Transform parent, string name, Vector3 position, float radius, Color color, int sortingOrder, bool useLocalPosition)
    {
        GameObject dot = new GameObject(name);
        dot.transform.SetParent(parent, false);

        if (useLocalPosition)
        {
            dot.transform.localPosition = position;
        }
        else
        {
            dot.transform.position = position;
        }

        MeshFilter meshFilter = dot.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateCircleMesh(radius);

        MeshRenderer meshRenderer = dot.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CreateDotMaterial(color);
        meshRenderer.sortingOrder = sortingOrder;

        return dot;
    }

    private static Mesh CreateCircleMesh(float radius)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[SegmentCount + 1];
        int[] triangles = new int[SegmentCount * 3];

        // Triangle fan: one center vertex, then points around the circle.
        vertices[0] = Vector3.zero;

        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = (float)i / SegmentCount * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        for (int i = 0; i < SegmentCount; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == SegmentCount - 1 ? 1 : i + 2;
        }

        mesh.name = "GridCenterDotMesh";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateDotMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        return material;
    }
}
