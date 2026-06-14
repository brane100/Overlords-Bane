using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Play-mode test helper. Sets known timer+health values, then teleports the
/// player into the Level 1 ExitTrigger so LevelTransitionManager carries them into Level 2.
/// Run this script a couple of seconds after entering play mode (so PlayerArmature has spawned).
/// </summary>
public static class TriggerExit
{
    public static void Execute()
    {
        if (SceneManager.GetActiveScene().name != "Level1")
        {
            Debug.LogWarning("[TriggerExit] Not in Level1; current scene: " + SceneManager.GetActiveScene().name);
            return;
        }

        // --- Stamp known values ---
        const float KnownTime   = 90f;
        const float KnownHealth = 75f;

        var timer = Object.FindFirstObjectByType<Timer>();
        if (timer != null) { timer.SetRemaining(KnownTime); Debug.Log($"[TriggerExit] Timer set to {KnownTime}s"); }
        else Debug.LogWarning("[TriggerExit] Timer not found.");

        var ph = Object.FindFirstObjectByType<PlayerHealth>();
        if (ph != null) { ph.SetHealth(KnownHealth); Debug.Log($"[TriggerExit] Health set to {KnownHealth}"); }
        else Debug.LogWarning("[TriggerExit] PlayerHealth not found.");

        // --- Teleport player into the ExitTrigger ---
        // Level1ExitBuilder places ExitTrigger at (42.5, 1.5, 95).
        var exitGo = GameObject.Find("ExitTrigger");
        if (exitGo == null) { Debug.LogWarning("[TriggerExit] ExitTrigger not found."); return; }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogWarning("[TriggerExit] No tagged Player found; PlayerArmature may not have spawned yet."); return; }

        // Teleport via CharacterController (must disable it first).
        var cc = player.GetComponent<CharacterController>();
        Vector3 dest = exitGo.transform.position + Vector3.forward * 0.5f; // just inside trigger
        if (cc != null)
        {
            cc.enabled = false;
            player.transform.position = dest;
            cc.enabled = true;
        }
        else player.transform.position = dest;

        Debug.Log($"[TriggerExit] Teleported {player.name} to {dest}. Transition should fire momentarily.");
    }
}
