using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Shows the game-over overlay when the <see cref="Timer"/> runs out or the
/// <see cref="PlayerHealth"/> dies. Pauses the game (Time.timeScale = 0) and
/// offers Retry / Main Menu / Quit.
/// </summary>
public class GameOverController : MonoBehaviour
{
    [SerializeField] CanvasGroup overlay;
    [SerializeField] TMP_Text subtitle;
    [SerializeField] Timer timer;
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] Button retryButton;
    [SerializeField] Button menuButton;
    [SerializeField] Button quitButton;
    [SerializeField] string mainMenuScene = "MainMenu";
    [SerializeField] float fadeTime = 0.6f;

    bool _shown;
    bool _healthSubscribed;

    void Awake()
    {
        if (overlay != null)
        {
            overlay.alpha = 0f;
            overlay.blocksRaycasts = false;
            overlay.interactable = false;
        }
    }

    void Start()
    {
        if (timer != null) timer.OnTimeUp += OnTimeUp;
        TrySubscribeHealth();
        if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
        if (menuButton != null) menuButton.onClick.AddListener(OnMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
    }

    void Update()
    {
        if (!_healthSubscribed) TrySubscribeHealth();
    }

    void TrySubscribeHealth()
    {
        if (_healthSubscribed) return;
        if (playerHealth == null)
            playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        if (playerHealth == null) return;
        playerHealth.OnDeath += OnPlayerDeath;
        _healthSubscribed = true;
    }

    void OnDestroy()
    {
        if (timer != null) timer.OnTimeUp -= OnTimeUp;
        if (playerHealth != null) playerHealth.OnDeath -= OnPlayerDeath;
    }

    void OnTimeUp() => Show("THE HOURGLASS HAS EMPTIED");
    void OnPlayerDeath() => Show("YOUR VITALITY IS SPENT");

    public void Show(string reason)
    {
        if (_shown) return;
        _shown = true;

        if (subtitle != null) subtitle.text = reason;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Best-effort: stop player input if the Input System is in use.
        foreach (var pi in FindObjectsByType<UnityEngine.InputSystem.PlayerInput>(FindObjectsSortMode.None))
            pi.DeactivateInput();

        if (overlay != null) StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        overlay.blocksRaycasts = true;
        overlay.interactable = true;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            overlay.alpha = Mathf.Clamp01(t / fadeTime);
            yield return null;
        }
        overlay.alpha = 1f;
    }

    public void OnRetry()
    {
        Time.timeScale = 1f;
        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        ShutdownNetworkThenLoad(() => SceneManager.LoadScene(buildIndex));
    }

    public void OnMainMenu()
    {
        Time.timeScale = 1f;
        string scene = mainMenuScene;
        ShutdownNetworkThenLoad(() => SceneManager.LoadScene(scene));
    }

    public void OnQuit()
    {
        Time.timeScale = 1f;
        ShutdownNetworkThenLoad(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

    void ShutdownNetworkThenLoad(System.Action load)
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            nm.Shutdown();
            // Give Netcode one frame to finish its teardown before loading
            StartCoroutine(LoadNextFrame(load));
        }
        else
        {
            load();
        }
    }

    IEnumerator LoadNextFrame(System.Action load)
    {
        yield return null;
        load();
    }
}
