using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Run in Level 2 after the transition to verify carried timer + health values.
/// Expected: timer ≈ 90s (minus scene load time), health = 75.
/// </summary>
public static class CheckCarry
{
    public static void Execute()
    {
        Debug.Log("[CheckCarry] Scene: " + SceneManager.GetActiveScene().name);

        var timer = Object.FindFirstObjectByType<Timer>();
        if (timer != null) Debug.Log($"[CheckCarry] Timer.Remaining = {timer.Remaining:0.0}s  (expected ~90)");
        else Debug.LogWarning("[CheckCarry] Timer not found.");

        var ph = Object.FindFirstObjectByType<PlayerHealth>();
        if (ph != null) Debug.Log($"[CheckCarry] PlayerHealth.Current = {ph.Current:0}  (expected 75)");
        else Debug.LogWarning("[CheckCarry] PlayerHealth not found — may not have spawned yet, wait a second and re-run.");

        var ltm = Object.FindFirstObjectByType<LevelTransitionManager>();
        Debug.Log("[CheckCarry] LevelTransitionManager present: " + (ltm != null));
    }
}
