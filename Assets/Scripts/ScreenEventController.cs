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
            Debug.LogError("[ScreenEvent] PlayerInput is NULL");

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