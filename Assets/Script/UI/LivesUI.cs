using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Displays the current lives using Image objects already placed in the scene hierarchy.
public class LivesUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager manager = null;
    [SerializeField] private RectTransform container = null;
    [SerializeField] private bool collectChildImagesFromContainer = true;
    [SerializeField] private List<Image> lifeImages = new List<Image>();

    [Header("Sprites")]
    [SerializeField] private Sprite fullLifeSprite = null;
    [SerializeField] private Sprite emptyLifeSprite = null;

    [Header("Fallback Colors")]
    [SerializeField] private Color fullLifeColor = Color.white;
    [SerializeField] private Color emptyLifeColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("Lost Life Animation")]
    [SerializeField] private bool animateLostLife = true;
    [Min(0.05f)]
    [SerializeField] private float lostLifeAnimationDuration = 0.55f;
    [Min(1)]
    [SerializeField] private int lostLifeBlinkCount = 3;
    [SerializeField] private Color lostLifeBlinkColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Header("Health Lost Flash Panel")]
    [SerializeField] private Graphic healthLostFlashPanel = null;
    [SerializeField] private bool flashPanelOnLostLife = true;
    [Min(0f)]
    [SerializeField] private float healthLostFlashFadeInDuration = 0.05f;
    [Min(0.01f)]
    [SerializeField] private float healthLostFlashFadeOutDuration = 0.18f;
    [SerializeField] private Color healthLostFlashColor = new Color(1f, 0f, 0f, 0.22f);

    private readonly Dictionary<Image, Coroutine> lifeAnimations = new Dictionary<Image, Coroutine>();
    private Coroutine healthLostFlashRoutine;
    private int displayedLives = -1;
    private int displayedMaxLives = -1;
    private bool warnedAboutMissingImages;

    private void Awake()
    {
        ResolveManager();
        PrepareHealthLostFlashPanel();
    }

    private void OnEnable()
    {
        ResolveManager();

        if (manager != null)
        {
            manager.LivesChanged += HandleLivesChanged;
        }
    }

    private void Start()
    {
        RefreshImageReferences();
        PrepareHealthLostFlashPanel();

        if (manager != null)
        {
            ApplyLivesChanged(manager.CurrentLives, manager.MaxLives, false);
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.LivesChanged -= HandleLivesChanged;
        }

        StopAllLifeAnimations();
        StopHealthLostFlash();
    }

    private void ResolveManager()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<GameManager>();
        }
    }

    private void HandleLivesChanged(int currentLives, int maxLives)
    {
        RefreshImageReferences();
        ApplyLivesChanged(currentLives, maxLives, true);
    }

    [ContextMenu("Collect Life Images From Container")]
    private void CollectLifeImagesFromContainer()
    {
        if (container == null)
        {
            return;
        }

        lifeImages.Clear();
        Image[] childImages = container.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < childImages.Length; i++)
        {
            if (childImages[i] == null || childImages[i].transform == container)
            {
                continue;
            }

            lifeImages.Add(childImages[i]);
        }
    }

    private void RefreshImageReferences()
    {
        if (collectChildImagesFromContainer && container != null && lifeImages.Count == 0)
        {
            CollectLifeImagesFromContainer();
        }

        for (int i = lifeImages.Count - 1; i >= 0; i--)
        {
            if (lifeImages[i] == null)
            {
                lifeImages.RemoveAt(i);
            }
        }
    }

    private void ApplyLivesChanged(int currentLives, int maxLives, bool allowAnimation)
    {
        int safeMaxLives = Mathf.Max(1, maxLives);
        int safeCurrentLives = Mathf.Clamp(currentLives, 0, safeMaxLives);
        bool canAnimateLostLife = allowAnimation
            && animateLostLife
            && displayedLives >= 0
            && displayedMaxLives == safeMaxLives
            && safeCurrentLives < displayedLives;
        bool lostLife = allowAnimation
            && displayedLives >= 0
            && displayedMaxLives == safeMaxLives
            && safeCurrentLives < displayedLives;

        int lostStartIndex = canAnimateLostLife ? safeCurrentLives : -1;
        int lostEndIndex = canAnimateLostLife ? displayedLives - 1 : -1;

        if (!canAnimateLostLife)
        {
            StopAllLifeAnimations();
        }

        RefreshLifeImages(safeCurrentLives, safeMaxLives, lostStartIndex, lostEndIndex);

        if (canAnimateLostLife)
        {
            for (int i = lostStartIndex; i <= lostEndIndex && i < lifeImages.Count; i++)
            {
                PlayLostLifeAnimation(lifeImages[i]);
            }
        }

        if (lostLife)
        {
            PlayHealthLostFlash();
        }

        displayedLives = safeCurrentLives;
        displayedMaxLives = safeMaxLives;
    }

    private void RefreshLifeImages(int currentLives, int maxLives, int lostAnimationStartIndex = -1, int lostAnimationEndIndex = -1)
    {
        int safeMaxLives = Mathf.Max(1, maxLives);
        int safeCurrentLives = Mathf.Clamp(currentLives, 0, safeMaxLives);

        if (lifeImages.Count < safeMaxLives && !warnedAboutMissingImages)
        {
            warnedAboutMissingImages = true;
            Debug.LogWarning(
                $"LivesUI has {lifeImages.Count} life image(s), but the level needs {safeMaxLives}. Add more Image children under the LivesContainer or assign them in the Life Images list.",
                this);
        }

        for (int i = 0; i < lifeImages.Count; i++)
        {
            Image image = lifeImages[i];

            if (image == null)
            {
                continue;
            }

            image.gameObject.SetActive(i < safeMaxLives);

            if (i >= safeMaxLives)
            {
                StopLifeAnimation(image);
                continue;
            }

            image.preserveAspect = true;
            image.raycastTarget = false;

            bool isPlayingLostAnimation = i >= lostAnimationStartIndex && i <= lostAnimationEndIndex;

            if (isPlayingLostAnimation)
            {
                continue;
            }

            StopLifeAnimation(image);
            bool isFull = i < safeCurrentLives;
            SetLifeImageState(image, isFull);
        }
    }

    private void PlayLostLifeAnimation(Image image)
    {
        if (image == null)
        {
            return;
        }

        StopLifeAnimation(image);
        lifeAnimations[image] = StartCoroutine(LostLifeAnimationRoutine(image));
    }

    private IEnumerator LostLifeAnimationRoutine(Image image)
    {
        if (fullLifeSprite != null)
        {
            image.sprite = fullLifeSprite;
        }

        image.color = fullLifeColor;

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, lostLifeAnimationDuration);
        int blinkCount = Mathf.Max(1, lostLifeBlinkCount);

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float blink = Mathf.PingPong(progress * blinkCount * 2f, 1f);
            Color color = Color.Lerp(fullLifeColor, lostLifeBlinkColor, blink);
            color.a = Mathf.Lerp(fullLifeColor.a, 0f, progress);
            image.color = color;

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetLifeImageState(image, false);
        lifeAnimations.Remove(image);
    }

    private void SetLifeImageState(Image image, bool isFull)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = isFull ? fullLifeSprite : emptyLifeSprite;
        image.color = isFull ? fullLifeColor : emptyLifeColor;
    }

    private void StopLifeAnimation(Image image)
    {
        if (image == null || !lifeAnimations.TryGetValue(image, out Coroutine routine))
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        lifeAnimations.Remove(image);
    }

    private void StopAllLifeAnimations()
    {
        foreach (Coroutine routine in lifeAnimations.Values)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        lifeAnimations.Clear();
    }

    private void PrepareHealthLostFlashPanel()
    {
        if (healthLostFlashPanel == null)
        {
            return;
        }

        healthLostFlashPanel.raycastTarget = false;
        SetHealthLostFlashAlpha(0f);
    }

    private void PlayHealthLostFlash()
    {
        if (!flashPanelOnLostLife || healthLostFlashPanel == null || !isActiveAndEnabled)
        {
            return;
        }

        if (!healthLostFlashPanel.gameObject.activeSelf)
        {
            healthLostFlashPanel.gameObject.SetActive(true);
        }

        if (healthLostFlashRoutine != null)
        {
            StopCoroutine(healthLostFlashRoutine);
        }

        healthLostFlashRoutine = StartCoroutine(HealthLostFlashRoutine());
    }

    private IEnumerator HealthLostFlashRoutine()
    {
        SetHealthLostFlashAlpha(0f);
        yield return FadeHealthLostFlash(0f, healthLostFlashColor.a, healthLostFlashFadeInDuration);
        yield return FadeHealthLostFlash(healthLostFlashColor.a, 0f, healthLostFlashFadeOutDuration);
        SetHealthLostFlashAlpha(0f);
        healthLostFlashRoutine = null;
    }

    private IEnumerator FadeHealthLostFlash(float startAlpha, float endAlpha, float duration)
    {
        float safeDuration = Mathf.Max(0.001f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            SetHealthLostFlashAlpha(Mathf.Lerp(startAlpha, endAlpha, progress));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetHealthLostFlashAlpha(endAlpha);
    }

    private void SetHealthLostFlashAlpha(float alpha)
    {
        if (healthLostFlashPanel == null)
        {
            return;
        }

        Color color = healthLostFlashColor;
        color.a = alpha;
        healthLostFlashPanel.color = color;
    }

    private void StopHealthLostFlash()
    {
        if (healthLostFlashRoutine != null)
        {
            StopCoroutine(healthLostFlashRoutine);
            healthLostFlashRoutine = null;
        }

        SetHealthLostFlashAlpha(0f);
    }
}
