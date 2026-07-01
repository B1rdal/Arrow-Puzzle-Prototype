using UnityEngine;

// Shared visual settings for generated path arrows.
[CreateAssetMenu(fileName = "PathArrowStyleData", menuName = "Arrow Escape/Path Arrow Style Data")]
public class PathArrowStyleData : ScriptableObject
{
    [Header("Colors")]
    [SerializeField] private Color arrowColor = new Color(0.25f, 0.65f, 1f, 1f);
    [SerializeField] private Color blockedColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] private Color holdHighlightColor = Color.white;
    [Range(0f, 1f)]
    [SerializeField] private float holdHighlightBlend = 0.35f;

    [Header("Blocked Feedback")]
    [Min(0f)]
    [SerializeField] private float blockedShakeDistance = 0.08f;
    [Min(0.01f)]
    [SerializeField] private float blockedFeedbackDuration = 0.18f;

    [Header("Arrow Head")]
    [Min(0.1f)]
    [SerializeField] private float headSizeMultiplier = 2.2f;
    [Min(0.05f)]
    [SerializeField] private float headTipLength = 0.56f;
    [Min(0f)]
    [SerializeField] private float headBaseBackLength = 0.22f;
    [Min(0.02f)]
    [SerializeField] private float headHalfWidth = 0.23f;

    [Header("Hold Preview")]
    [Min(1f)]
    [SerializeField] private float holdLineWidthMultiplier = 1.18f;
    [Min(1f)]
    [SerializeField] private float holdHeadScaleMultiplier = 1.12f;
    [Min(1f)]
    [SerializeField] private float previewBeamLength = 30f;
    [Range(0.05f, 1f)]
    [SerializeField] private float previewBeamWidthMultiplier = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float previewBeamAlpha = 0.24f;

    public Color ArrowColor => arrowColor;
    public Color BlockedColor => blockedColor;
    public Color HoldHighlightColor => holdHighlightColor;
    public float HoldHighlightBlend => holdHighlightBlend;
    public float BlockedShakeDistance => blockedShakeDistance;
    public float BlockedFeedbackDuration => blockedFeedbackDuration;
    public float HeadSizeMultiplier => headSizeMultiplier;
    public float HeadTipLength => headTipLength;
    public float HeadBaseBackLength => headBaseBackLength;
    public float HeadHalfWidth => headHalfWidth;
    public float HoldLineWidthMultiplier => holdLineWidthMultiplier;
    public float HoldHeadScaleMultiplier => holdHeadScaleMultiplier;
    public float PreviewBeamLength => previewBeamLength;
    public float PreviewBeamWidthMultiplier => previewBeamWidthMultiplier;
    public float PreviewBeamAlpha => previewBeamAlpha;

    private void OnValidate()
    {
        blockedShakeDistance = Mathf.Max(0f, blockedShakeDistance);
        blockedFeedbackDuration = Mathf.Max(0.01f, blockedFeedbackDuration);
        headSizeMultiplier = Mathf.Max(0.1f, headSizeMultiplier);
        headTipLength = Mathf.Max(0.05f, headTipLength);
        headBaseBackLength = Mathf.Max(0f, headBaseBackLength);
        headHalfWidth = Mathf.Max(0.02f, headHalfWidth);
        holdLineWidthMultiplier = Mathf.Max(1f, holdLineWidthMultiplier);
        holdHeadScaleMultiplier = Mathf.Max(1f, holdHeadScaleMultiplier);
        previewBeamLength = Mathf.Max(1f, previewBeamLength);
    }
}
