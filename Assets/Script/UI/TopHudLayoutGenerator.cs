/*
Summary:
TopHudLayoutGenerator builds a clean mobile HUD hierarchy under an existing Canvas.
Add it to the Canvas and use the context menu to create SafeAreaRoot and TopHud.
*/

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class TopHudLayoutGenerator : MonoBehaviour
{
    [Header("Names")]
    [SerializeField] private string safeAreaRootName = "SafeAreaRoot";
    [SerializeField] private string topHudName = "TopHud";

    [Header("Top HUD")]
    [Min(1f)]
    [SerializeField] private float topHudHeight = 120f;

    [Header("Runtime")]
    [SerializeField] private bool generateOnStart = false;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateSafeTopHud();
        }
    }

    [ContextMenu("Generate Safe Top Hud")]
    public void GenerateSafeTopHud()
    {
        Canvas canvas = GetComponent<Canvas>();

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogWarning("TopHudLayoutGenerator needs to be placed on or under a Canvas.", this);
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;

        if (canvasRect == null)
        {
            Debug.LogWarning("The selected Canvas does not have a RectTransform.", this);
            return;
        }

        RectTransform safeAreaRoot = GetOrCreateChild(canvasRect, safeAreaRootName);
        ConfigureFullScreenStretch(safeAreaRoot);
        EnsureComponent<SafeAreaRectTransform>(safeAreaRoot.gameObject);

        RectTransform topHud = GetOrCreateChild(safeAreaRoot, topHudName);
        ConfigureTopHud(topHud);

#if UNITY_EDITOR
        EditorUtility.SetDirty(canvas.gameObject);
        EditorUtility.SetDirty(safeAreaRoot.gameObject);
        EditorUtility.SetDirty(topHud.gameObject);
#endif

        Debug.Log("Generated SafeAreaRoot > TopHud under the Canvas.", this);
    }

    private RectTransform GetOrCreateChild(RectTransform parent, string childName)
    {
        Transform existingChild = parent.Find(childName);

        if (existingChild != null && existingChild is RectTransform existingRect)
        {
            return existingRect;
        }

        GameObject childObject = new GameObject(childName, typeof(RectTransform));

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
        }
#endif

        RectTransform childRect = childObject.GetComponent<RectTransform>();
        childRect.SetParent(parent, false);
        return childRect;
    }

    private static T EnsureComponent<T>(GameObject targetObject) where T : Component
    {
        T component = targetObject.GetComponent<T>();

        if (component != null)
        {
            return component;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return Undo.AddComponent<T>(targetObject);
        }
#endif

        return targetObject.AddComponent<T>();
    }

    private static void ConfigureFullScreenStretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private void ConfigureTopHud(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.offsetMin = new Vector2(0f, -topHudHeight);
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }
}
