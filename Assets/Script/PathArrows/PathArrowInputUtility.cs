/*
Summary:
PathArrowInputUtility contains shared mobile-friendly hit testing for path arrows.
It lets touch input use a wider radius than mouse input so tapping thin arrow lines
feels responsive on phones.
*/

using UnityEngine;

public static class PathArrowInputUtility
{
    public static bool TryGetArrowAtScreenPosition(
        Camera inputCamera,
        Vector2 screenPosition,
        float hitRadiusPixels,
        bool requirePressableArrow,
        out PathArrow arrow)
    {
        arrow = null;

        if (inputCamera == null)
        {
            return false;
        }

        Vector3 worldPosition = ScreenToWorldPosition(inputCamera, screenPosition);
        float worldRadius = ScreenPixelsToWorldRadius(inputCamera, screenPosition, hitRadiusPixels);
        Collider2D[] hits = worldRadius > 0f
            ? Physics2D.OverlapCircleAll(worldPosition, worldRadius)
            : Physics2D.OverlapPointAll(worldPosition);
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];

            if (hit == null)
            {
                continue;
            }

            PathArrowColliderProxy proxy = hit.GetComponent<PathArrowColliderProxy>();

            if (proxy == null || proxy.Owner == null)
            {
                continue;
            }

            if (requirePressableArrow && !proxy.Owner.CanStartPress)
            {
                continue;
            }

            Vector2 closestPoint = hit.ClosestPoint(worldPosition);
            float distance = ((Vector2)worldPosition - closestPoint).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                arrow = proxy.Owner;
            }
        }

        return arrow != null;
    }

    public static bool IsScreenPositionOverArrow(
        Camera inputCamera,
        Vector2 screenPosition,
        PathArrow targetArrow,
        float hitRadiusPixels)
    {
        if (!TryGetArrowAtScreenPosition(inputCamera, screenPosition, hitRadiusPixels, false, out PathArrow arrow))
        {
            return false;
        }

        return targetArrow == null || arrow == targetArrow;
    }

    public static Vector3 ScreenToWorldPosition(Camera inputCamera, Vector2 screenPosition)
    {
        Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(inputCamera.transform.position.z));
        return inputCamera.ScreenToWorldPoint(screenPoint);
    }

    private static float ScreenPixelsToWorldRadius(Camera inputCamera, Vector2 screenPosition, float radiusPixels)
    {
        if (inputCamera == null || radiusPixels <= 0f)
        {
            return 0f;
        }

        if (inputCamera.orthographic)
        {
            int screenHeight = Mathf.Max(1, Screen.height);
            return inputCamera.orthographicSize * 2f * radiusPixels / screenHeight;
        }

        Vector3 center = ScreenToWorldPosition(inputCamera, screenPosition);
        Vector3 edge = ScreenToWorldPosition(inputCamera, screenPosition + Vector2.right * radiusPixels);
        return Vector3.Distance(center, edge);
    }
}
