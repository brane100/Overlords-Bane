using System.Reflection;
using UnityEngine;

/// <summary>Play-mode test: trigger the win screen reveal without walking to the exit.</summary>
public static class RunInvokeWinShow
{
    public static void Execute()
    {
        var ctrl = Object.FindFirstObjectByType(System.Type.GetType("WinScreenController, Assembly-CSharp"));
        if (ctrl == null) { Debug.LogError("[RunInvokeWinShow] WinScreenController not found (is the game playing on Level5?)."); return; }
        var m = ctrl.GetType().GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) { Debug.LogError("[RunInvokeWinShow] Show() not found."); return; }
        m.Invoke(ctrl, null);
        Debug.Log("[RunInvokeWinShow] Show() invoked.");
    }
}
