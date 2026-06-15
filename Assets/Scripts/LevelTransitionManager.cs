using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Persistent (DontDestroyOnLoad) singleton that drives level-to-level transitions.
///
/// Listens to <see cref="ExitTrigger.OnLevelComplete"/>. When the player reaches the
/// exit it captures the remaining timer value and current health, shuts the
/// NetworkManager down cleanly, loads the next level async, and re-applies the
/// carried timer + health to the freshly spawned PlayerArmature and Timer.
///
/// Self-bootstrapping: no per-scene wiring required.
/// </summary>
public class LevelTransitionManager : MonoBehaviour
{
    public static LevelTransitionManager Instance { get; private set; }

    // --- carried state ---
    bool _hasCarry;
    bool _hasTime;
    bool _hasHealth;
    float _carriedTime;
    float _carriedHealth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("LevelTransitionManager");
        go.AddComponent<LevelTransitionManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ExitTrigger.OnLevelComplete += HandleLevelComplete;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ExitTrigger.OnLevelComplete -= HandleLevelComplete;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    // ---------------------------------------------------------------- transition

    void HandleLevelComplete()
    {
        string next = ResolveNextScene();
        if (next == null)
        {
            Debug.Log("[LevelTransition] No next level after '" + SceneManager.GetActiveScene().name +
                      "' — final level reached.");
            return;
        }
        LoadNextLevel(next);
    }

    /// <summary>
    /// Captures the current timer + health, shuts down Netcode, and loads
    /// <paramref name="sceneName"/> asynchronously (Single mode).
    /// </summary>
    public void LoadNextLevel(string sceneName)
    {
        CaptureCarry();
        StartCoroutine(LoadRoutine(sceneName));
    }

    void CaptureCarry()
    {
        _hasTime = _hasHealth = false;

        var timer = FindFirstObjectByType<Timer>();
        if (timer != null) { _carriedTime = timer.Remaining; _hasTime = true; }

        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null) { _carriedHealth = ph.Current; _hasHealth = true; }

        _hasCarry = _hasTime || _hasHealth;
        Debug.Log("[LevelTransition] Captured carry: time=" + (_hasTime ? _carriedTime.ToString("0.0") : "-") +
                  " health=" + (_hasHealth ? _carriedHealth.ToString("0") : "-"));
    }

    IEnumerator LoadRoutine(string sceneName)
    {
        // Single mode (not additive): each level carries its own NetworkManager +
        // AutoStartHost, so additive loading would duplicate them. Shut the running
        // host down first so the next scene's AutoStartHost can start cleanly.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            nm.Shutdown();
            yield return null; // let Netcode finish its teardown
        }

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (op != null && !op.isDone)
            yield return null;
    }

    // ---------------------------------------------------------------- apply carry

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_hasCarry) return;

        // Timer exists in the scene immediately — apply now.
        // Every level starts with 3 minutes base plus whatever time was carried in.
        const float LevelBaseTime = 180f;
        if (_hasTime)
        {
            var timer = FindFirstObjectByType<Timer>();
            if (timer != null)
            {
                float startTime = LevelBaseTime + _carriedTime;
                timer.SetRemaining(startTime);
                Debug.Log("[LevelTransition] Applied time " + LevelBaseTime + "s base + " +
                          _carriedTime.ToString("0.0") + "s carry = " + startTime.ToString("0.0") + "s");
            }
        }

        // PlayerArmature spawns a few frames after load (Netcode) — wait for it.
        if (_hasHealth)
            StartCoroutine(ApplyHealthWhenReady(_carriedHealth));

        _hasCarry = false;
    }

    IEnumerator ApplyHealthWhenReady(float health)
    {
        float timeout = 10f;
        PlayerHealth ph = null;
        while (timeout > 0f)
        {
            ph = FindFirstObjectByType<PlayerHealth>();
            if (ph != null) break;
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (ph != null)
        {
            ph.SetHealth(health);
            Debug.Log("[LevelTransition] Applied carried health " + health.ToString("0"));
        }
        else
        {
            Debug.LogWarning("[LevelTransition] PlayerHealth never appeared; carried health not applied.");
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>"Level3" -> "Level4" if that scene is in Build Settings, else null.</summary>
    static string ResolveNextScene()
    {
        string current = SceneManager.GetActiveScene().name;
        var m = Regex.Match(current, @"^(.*?)(\d+)$");
        if (!m.Success) return null;

        string prefix = m.Groups[1].Value;
        int n = int.Parse(m.Groups[2].Value);
        string next = prefix + (n + 1);

        return Application.CanStreamedLevelBeLoaded(next) ? next : null;
    }
}
