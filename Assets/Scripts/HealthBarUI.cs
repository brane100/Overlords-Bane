using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives a filled health bar Image (and optional numeric label) from a
/// <see cref="PlayerHealth"/>. The fill smoothly lerps and tints from
/// violet (full) toward crimson (empty).
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] Image fillImage;          // Image Type = Filled, Horizontal
    [SerializeField] TMP_Text valueLabel;
    [SerializeField] Color fullColor = new Color(0.545f, 0.361f, 0.965f); // violet #8B5CF6
    [SerializeField] Color lowColor = new Color(0.75f, 0.18f, 0.18f);     // crimson
    [SerializeField] float lerpSpeed = 6f;

    float _target = 1f;
    float _display = 1f;
    bool _subscribed;

    void Start()
    {
        TrySubscribe();
    }

    void TrySubscribe()
    {
        if (_subscribed) return;
        if (playerHealth == null)
            playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged += HandleChanged;
        HandleChanged(playerHealth.Current, playerHealth.Max);
        _display = _target;
        _subscribed = true;
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= HandleChanged;
    }

    void HandleChanged(float current, float max)
    {
        _target = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (valueLabel != null) valueLabel.text = Mathf.CeilToInt(current).ToString();
    }

    void Update()
    {
        if (!_subscribed) TrySubscribe();
        _display = Mathf.MoveTowards(_display, _target, lerpSpeed * Time.unscaledDeltaTime);
        if (fillImage != null)
        {
            fillImage.fillAmount = _display;
            fillImage.color = Color.Lerp(lowColor, fullColor, _display);
        }
    }
}
