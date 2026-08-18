/*
Summary:
StartupLoadingScreen creates a temporary full-screen cover before the first scene is
shown. It hides Android build startup frames while UI scripts reset their images,
then fades away after the scene has had a few frames to settle.
*/

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public class StartupLoadingScreen : MonoBehaviour
{
    private static StartupLoadingScreen runtimeInstance;

    [Header("Timing")]
    [Min(1)]
    [SerializeField] private int coveredStartupFrames = 8;
    [Min(0f)]
    [SerializeField] private float fadeOutDuration = 0.18f;

    [Header("Visual")]
    [SerializeField] private Color coverColor = Color.black;

    private Canvas coverCanvas;
    private CanvasGroup coverGroup;
    private Image coverImage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeSceneLoad()
    {
        if (runtimeInstance != null)
        {
            return;
        }

        GameObject loaderObject = new GameObject("StartupLoadingScreen");
        runtimeInstance = loaderObject.AddComponent<StartupLoadingScreen>();
        DontDestroyOnLoad(loaderObject);
    }

    private void Awake()
    {
        if (runtimeInstance != null && runtimeInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        runtimeInstance = this;
        CreateCover();
    }

    private IEnumerator Start()
    {
        int framesToCover = Mathf.Max(1, coveredStartupFrames);

        for (int i = 0; i < framesToCover; i++)
        {
            ForceStartupUiState();
            yield return null;
        }

        ForceStartupUiState();
        Canvas.ForceUpdateCanvases();
        yield return FadeOutCover();
        DestroyCover();
        Destroy(gameObject);
    }

    private void CreateCover()
    {
        GameObject canvasObject = new GameObject("StartupLoadingCover");
        DontDestroyOnLoad(canvasObject);

        coverCanvas = canvasObject.AddComponent<Canvas>();
        coverCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        coverCanvas.overrideSorting = true;
        coverCanvas.sortingOrder = short.MaxValue;

        coverGroup = canvasObject.AddComponent<CanvasGroup>();
        coverGroup.alpha = 1f;
        coverGroup.blocksRaycasts = true;
        coverGroup.interactable = false;

        GameObject imageObject = new GameObject("Cover");
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.anchoredPosition = Vector2.zero;
        imageRect.sizeDelta = Vector2.zero;

        imageObject.AddComponent<CanvasRenderer>();
        coverImage = imageObject.AddComponent<Image>();
        coverImage.color = coverColor;
        coverImage.raycastTarget = true;
    }

    private static void ForceStartupUiState()
    {
        LivesUI[] livesUis = FindObjectsByType<LivesUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < livesUis.Length; i++)
        {
            if (livesUis[i] != null)
            {
                livesUis[i].ForceStartupRefresh();
            }
        }

        GameManager[] managers = FindObjectsByType<GameManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null)
            {
                managers[i].ForceArrowVisualRefresh(true);
            }
        }

        Graphic[] graphics = FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
            {
                continue;
            }

            graphics[i].SetAllDirty();

            if (graphics[i].canvasRenderer != null)
            {
                graphics[i].canvasRenderer.SetColor(graphics[i].color);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator FadeOutCover()
    {
        if (coverGroup == null || fadeOutDuration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            float progress = Mathf.Clamp01(elapsed / fadeOutDuration);
            coverGroup.alpha = Mathf.Lerp(1f, 0f, progress);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        coverGroup.alpha = 0f;
    }

    private void DestroyCover()
    {
        if (coverCanvas != null)
        {
            Destroy(coverCanvas.gameObject);
        }

        coverCanvas = null;
        coverGroup = null;
        coverImage = null;
        runtimeInstance = null;
    }
}
