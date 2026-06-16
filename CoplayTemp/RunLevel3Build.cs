using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Reflection trampoline for Level3MirrorMazeBuilder.Build() — the coplay dynamic
/// compiler can't see the editor assembly type directly, so invoke by reflection.
/// </summary>
public static class RunLevel3Build
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("Level3MirrorMazeBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunLevel3Build] type not found."); return; }

        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunLevel3Build] Build() not found."); return; }

        Debug.Log("[RunLevel3Build] " + method.Invoke(null, null));
    }
}
