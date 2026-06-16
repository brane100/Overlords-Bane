using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Shows an animated "you won" victory screen when the final level's exit is
/// reached. Lives only in Level 5 and listens to the static
/// <see cref="ExitTrigger.OnLevelComplete"/> signal, which fires after the exit
/// sequence — so nothing else (ExitTrigger, transitions, other levels) is touched.
///
/// Reveal is fully procedural (no Animator asset): backdrop fades in, the title
/// pops with an ease-out-back overshoot, subtitle and buttons fade in, then the
/// title breathes with a gentle pulse.
/// </summary>
public class WinScreenController : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] CanvasGroup group;       // whole panel
    [SerializeField] RectTransform title;      // popped + pulsed
    [SerializeField] CanvasGroup subtitle;
    [SerializeField] CanvasGroup buttons;
    [SerializeField] Button menuButton;
    [SerializeField] Button quitButton;
    [SerializeField] string mainMenuScene = "MainMenu";

    [Header("Animation (seconds)")]
    [SerializeField] float backdropFade = 0.8f;
    [SerializeField] float titlePopTime = 0.7f;
    [SerializeField] float elementFade = 0.5f;
    [SerializeField] float titleStartScale = 0.5f;

    [Header("Pulse")]
    [SerializeField] float pulseAmplitude = 0.03f;
    [SerializeField] float pulseSpeed = 1.6f;

    bool _shown;

    void Awake()
    {
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
        if (subtitle != null) subtitle.alpha = 0f;
        if (buttons != null) buttons.alpha = 0f;
    }

    void OnEnable() { ExitTrigger.OnLevelComplete += Show; }
    void OnDisable() { ExitTrigger.OnLevelComplete -= Show; }

    void Start()
    {
        if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
    }

    void Show()
    {
        if (_shown) return;
        _shown = true;
        StartCoroutine(Reveal());
    }

    IEnumerator Reveal()
    {
        // Stop the player and free the cursor so the buttons are usable.
        var pi = FindFirstObjectByType<PlayerInput>();
        if (pi != null) pi.DeactivateInput();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (group != null) { group.blocksRaycasts = true; group.interactable = true; }

        if (title != null) title.localScale = new Vector3(titleStartScale, titleStartScale, 1f);

        yield return FadeGroup(group, 0f, 1f, backdropFade);
        if (title != null) yield return Pop(title, titleStartScale, 1f, titlePopTime);
        yield return FadeGroup(subtitle, 0f, 1f, elementFade);
        yield return FadeGroup(buttons, 0f, 1f, elementFade);

        // Gentle breathing pulse on the title.
        if (title != null)
        {
            float t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime * pulseSpeed;
                float s = 1f + Mathf.Sin(t) * pulseAmplitude;
                title.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }
    }

    IEnumerator FadeGroup(CanvasGroup g, float a, float b, float dur)
    {
        if (g == null) yield break;
        g.alpha = a;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(a, b, t / dur);
            yield return null;
        }
        g.alpha = b;
    }

    IEnumerator Pop(RectTransform rt, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float eased = EaseOutBack(Mathf.Clamp01(t / dur));
            float s = Mathf.LerpUnclamped(from, to, eased);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = new Vector3(to, to, 1f);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void GoToMenu()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(mainMenuScene))
            SceneManager.LoadScene(mainMenuScene);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
