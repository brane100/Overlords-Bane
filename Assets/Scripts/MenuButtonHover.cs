using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Lerps a TMP label's color between a default and hover color on pointer enter/exit.
/// Uses unscaled time so it animates even when the game is paused.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("The TMP label to tint. Auto-found in children if left empty.")]
    public TMP_Text label;

    [Tooltip("Resting color of the label.")]
    public Color defaultColor = new Color(0.690196f, 0.705882f, 0.784314f, 1f); // #B0B4C8

    [Tooltip("Color while the pointer is over the button.")]
    public Color hoverColor = new Color(1f, 0.992157f, 0.964706f, 1f);          // #FFFDF6

    [Tooltip("Lerp duration in seconds (unscaled).")]
    public float duration = 0.15f;

    Color _from;
    Color _to;
    float _t = 1f;
    bool _animating;

    void Reset()
    {
        label = GetComponentInChildren<TMP_Text>();
    }

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = defaultColor;
    }

    public void OnPointerEnter(PointerEventData eventData) => StartLerp(hoverColor);

    public void OnPointerExit(PointerEventData eventData) => StartLerp(defaultColor);

    void StartLerp(Color target)
    {
        if (label == null) return;
        _from = label.color;
        _to = target;
        _t = 0f;
        _animating = true;
    }

    void Update()
    {
        if (!_animating || label == null) return;
        _t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
        if (_t >= 1f)
        {
            _t = 1f;
            _animating = false;
        }
        label.color = Color.Lerp(_from, _to, _t);
    }
}
