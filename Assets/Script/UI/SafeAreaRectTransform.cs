/*
Summary:
SafeAreaRectTransform turns a UI RectTransform into a mobile safe-area container.
Place it on a full-screen UI parent under a Canvas, then anchor child UI elements
inside that parent. The parent will shrink to avoid notches, rounded corners, and
home indicator areas while the children keep their normal Rect Transform layout.
*/

using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaRectTransform : MonoBehaviour
{
    [Header("Edges")]
    [SerializeField] private bool applyLeft = true;
    [SerializeField] private bool applyRight = true;
    [SerializeField] private bool applyTop = true;
    [SerializeField] private bool applyBottom = true;

    [Header("Extra Padding")]
    [Min(0f)]
    [SerializeField] private float extraLeftPadding = 0f;
    [Min(0f)]
    [SerializeField] private float extraRightPadding = 0f;
    [Min(0f)]
    [SerializeField] private float extraTopPadding = 0f;
    [Min(0f)]
    [SerializeField] private float extraBottomPadding = 0f;

    private RectTransform rectTransform;
    private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private Vector2Int lastScreenSize = new Vector2Int(-1, -1);

    private void Awake()
    {
        ResolveRectTransform();
        ApplySafeAreaIfNeeded(true);
    }

    private void OnEnable()
    {
        ResolveRectTransform();
        ApplySafeAreaIfNeeded(true);
    }

    private void Update()
    {
        ApplySafeAreaIfNeeded(false);
    }

    [ContextMenu("Apply Safe Area Now")]
    private void ApplySafeAreaNow()
    {
        ResolveRectTransform();
        ApplySafeAreaIfNeeded(true);
    }

    private void ResolveRectTransform()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    }

    private void ApplySafeAreaIfNeeded(bool force)
    {
        if (rectTransform == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

        if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
        {
            return;
        }

        ApplySafeArea(safeArea);
        lastSafeArea = safeArea;
        lastScreenSize = screenSize;
    }

    private void ApplySafeArea(Rect safeArea)
    {
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        if (!applyLeft)
        {
            anchorMin.x = 0f;
        }

        if (!applyBottom)
        {
            anchorMin.y = 0f;
        }

        if (!applyRight)
        {
            anchorMax.x = 1f;
        }

        if (!applyTop)
        {
            anchorMax.y = 1f;
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;

        // Offsets become extra designer padding inside the safe-area anchors.
        rectTransform.offsetMin = new Vector2(extraLeftPadding, extraBottomPadding);
        rectTransform.offsetMax = new Vector2(-extraRightPadding, -extraTopPadding);
    }
}
