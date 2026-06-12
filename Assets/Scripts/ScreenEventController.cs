using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScreenEventController : MonoBehaviour
{
    [Header("Fade")]
    public CanvasGroup fadeOverlay;
    public float fadeTime = 0.4f;
    const float TARGET_ALPHA = 148f / 255f;

    [Header("Input")]
    public PlayerInput playerInput;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip voiceClip;

    bool running;

    void Awake()
    {
        Debug.Log("[ScreenEvent] Awake");

        if (fadeOverlay == null)
            Debug.LogError("[ScreenEvent] FadeOverlay is NULL");
        else
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
        }

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerInput == null)
            Debug.LogWarning("[ScreenEvent] PlayerInput not assigned (will be located at runtime once the player spawns)");

        if (audioSource == null)
            Debug.LogError("[ScreenEvent] AudioSource is NULL");

        if (voiceClip == null)
            Debug.LogWarning("[ScreenEvent] VoiceClip is NULL (will rely on AudioSource Resource)");
    }

    public void TriggerEvent()
    {
        Debug.Log("[ScreenEvent] TriggerEvent called");

        if (running)
        {
            Debug.Log("[ScreenEvent] Already running, ignoring trigger");
            return;
        }

        StartCoroutine(EventRoutine());
    }

    /// <summary>
    /// The Overlord (Axiom) exit sequence: lock input → fade to FULL black →
    /// play the supplied voice clip → grant bonus time → fade out → invoke onComplete.
    /// </summary>
    public void RunOverlordSequence(AudioClip clip, Timer timer, float bonusTime, System.Action onComplete)
    {
        if (running)
        {
            Debug.Log("[ScreenEvent] Overlord sequence already running, ignoring");
            return;
        }
        StartCoroutine(OverlordRoutine(clip, timer, bonusTime, onComplete));
    }

    IEnumerator OverlordRoutine(AudioClip clip, Timer timer, float bonusTime, System.Action onComplete)
    {
        running = true;
        Debug.Log("[ScreenEvent] Overlord sequence START");

        // 1. Lock input (resolve the runtime-spawned player if not pre-assigned)
        var pi = playerInput != null ? playerInput : FindFirstObjectByType<PlayerInput>();
        if (pi != null) pi.DeactivateInput();

        // 2. Fade to full black
        yield return Fade(fadeOverlay != null ? fadeOverlay.alpha : 0f, 1f);

        // 3. Play the Axiom voice
        float waitTime = 1f;
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
            waitTime = clip.length;
        }
        else if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
            waitTime = audioSource.clip.length;
        }

        // 4. Grant bonus time while the screen is black
        if (timer != null && bonusTime > 0f)
        {
            timer.AddTime(bonusTime);
            Debug.Log("[ScreenEvent] Granted " + bonusTime + "s bonus time");
        }

        // 5. Hold on black for the voice line
        yield return new WaitForSecondsRealtime(waitTime);

        // 6. Fade back out and restore input
        yield return Fade(fadeOverlay != null ? fadeOverlay.alpha : 1f, 0f);
        if (pi != null) pi.ActivateInput();

        running = false;
        Debug.Log("[ScreenEvent] Overlord sequence END");

        onComplete?.Invoke();
    }

    IEnumerator EventRoutine()
    {
        running = true;
        Debug.Log("[ScreenEvent] EventRoutine START");

        // 1️⃣ Freeze input
        if (playerInput != null)
        {
            playerInput.DeactivateInput();
            Debug.Log("[ScreenEvent] Player input DEACTIVATED");
        }

        // 2️⃣ Fade in
        Debug.Log("[ScreenEvent] Fading IN");
        yield return Fade(0f, TARGET_ALPHA);
        Debug.Log("[ScreenEvent] Fade IN complete");

        // 3️⃣ Play audio
        float waitTime = 1f;

        if (audioSource != null)
        {
            if (voiceClip != null)
            {
                audioSource.PlayOneShot(voiceClip);
                waitTime = voiceClip.length;
                Debug.Log("[ScreenEvent] Playing voice via PlayOneShot, length = " + waitTime);
            }
            else if (audioSource.clip != null)
            {
                audioSource.Play();
                waitTime = audioSource.clip.length;
                Debug.Log("[ScreenEvent] Playing voice via AudioSource Resource, length = " + waitTime);
            }
            else
            {
                Debug.LogWarning("[ScreenEvent] AudioSource has NO clip or resource");
            }
        }

        // 4️⃣ Wait for voice
        Debug.Log("[ScreenEvent] Waiting for " + waitTime + " seconds");
        yield return new WaitForSecondsRealtime(waitTime);

        // 5️⃣ Fade out
        Debug.Log("[ScreenEvent] Fading OUT");
        yield return Fade(fadeOverlay.alpha, 0f);
        Debug.Log("[ScreenEvent] Fade OUT complete");

        // 6️⃣ Unfreeze input
        if (playerInput != null)
        {
            playerInput.ActivateInput();
            Debug.Log("[ScreenEvent] Player input ACTIVATED");
        }

        running = false;
        Debug.Log("[ScreenEvent] EventRoutine END");
    }

    IEnumerator Fade(float from, float to)
    {
        if (fadeOverlay == null)
            yield break;

        fadeOverlay.blocksRaycasts = true;

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }

        fadeOverlay.alpha = to;
        fadeOverlay.blocksRaycasts = to > 0.01f;
    }
}