using System;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Sits on the maze's hidden exit cell. When the player enters, it runs the
/// Overlord (Axiom) sequence via <see cref="ScreenEventController"/>:
/// lock input → fade to black → play the Axiom voice clip → grant bonus time →
/// fade out → fire <see cref="OnLevelComplete"/> (the level-transition signal).
///
/// Fires exactly once, and only on the server/host (Netcode authority).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ExitTrigger : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] ScreenEventController screenEvent;
    [SerializeField] Timer timer;

    [Header("Overlord (Axiom) event")]
    [Tooltip("The Axiom voice clip played while the screen is black.")]
    [SerializeField] AudioClip axiomClip;
    [Tooltip("Seconds added to the Timer as the exit reward.")]
    [SerializeField] float bonusTime = 30f;

    /// <summary>Raised after the Overlord sequence completes. The LevelTransitionManager listens to this.</summary>
    public static event Action OnLevelComplete;

    bool _fired;

    void Reset()
    {
        // Make the collider a trigger by default when first added in the editor.
        var bc = GetComponent<BoxCollider>();
        if (bc != null) bc.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!other.CompareTag("Player")) return;

        // Server/host authority only — in a networked session, ignore client-side enters.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        _fired = true;

        if (timer == null) timer = FindFirstObjectByType<Timer>();

        if (screenEvent != null)
        {
            screenEvent.RunOverlordSequence(axiomClip, timer, bonusTime, FireComplete);
        }
        else
        {
            Debug.LogWarning("[ExitTrigger] No ScreenEventController assigned; firing OnLevelComplete directly.");
            FireComplete();
        }
    }

    void FireComplete()
    {
        Debug.Log("[ExitTrigger] OnLevelComplete");
        OnLevelComplete?.Invoke();
    }
}
