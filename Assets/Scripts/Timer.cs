using System;
using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI timerText;
    [Header("Remaining Time (seconds)")]
    [SerializeField] float _remainingTime = 180f;

    [Header("Low-time warning")]
    [SerializeField] float lowTimeThreshold = 30f;
    [SerializeField] Color normalColor = new Color(1f, 0.992f, 0.965f);  // cream #FFFDF6
    [SerializeField] Color lowColor = new Color(0.84f, 0.20f, 0.18f);    // crimson

    public float TotalTime { get; private set; }
    public float Remaining => Mathf.Max(0f, _remainingTime);
    public float Normalized => TotalTime > 0f ? Mathf.Clamp01(_remainingTime / TotalTime) : 0f;
    public bool IsUp { get; private set; }

    /// <summary>Fired once when the countdown reaches zero.</summary>
    public event Action OnTimeUp;

    bool _running = true;

    void Awake()
    {
        TotalTime = _remainingTime;
        // Pre-warm the TMP atlas with all digit + colon glyphs to prevent
        // a one-frame blank flash when new characters are rasterised mid-countdown.
        if (timerText != null)
        {
            timerText.text = "0123456789:";
            timerText.ForceMeshUpdate();
        }
    }

    void Update()
    {
        if (!_running) return;

        if (_remainingTime > 0f)
        {
            _remainingTime -= Time.deltaTime;
            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                TimeUp();
            }
        }

        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(_remainingTime / 60f);
        int seconds = Mathf.FloorToInt(_remainingTime % 60f);
        timerText.text = string.Format("{0:D2}:{1:D2}", minutes, seconds);
        timerText.color = _remainingTime <= lowTimeThreshold ? lowColor : normalColor;
    }

    void TimeUp()
    {
        if (IsUp) return;
        IsUp = true;
        _running = false;
        UpdateLabel();
        OnTimeUp?.Invoke();
    }

    /// <summary>Grants bonus seconds (e.g. the Overlord exit reward). Re-arms the
    /// countdown if it had expired, and keeps TotalTime >= remaining so Normalized stays valid.</summary>
    public void AddTime(float seconds)
    {
        if (seconds <= 0f) return;
        _remainingTime += seconds;
        if (_remainingTime > 0f)
        {
            IsUp = false;
            _running = true;
        }
        if (_remainingTime > TotalTime) TotalTime = _remainingTime;
        UpdateLabel();
    }

    /// <summary>Overwrites the remaining time (used when carrying time across a level transition).</summary>
    public void SetRemaining(float seconds)
    {
        _remainingTime = Mathf.Max(0f, seconds);
        if (_remainingTime > TotalTime) TotalTime = _remainingTime;
        IsUp = _remainingTime <= 0f;
        _running = !IsUp;
        UpdateLabel();
    }
}
