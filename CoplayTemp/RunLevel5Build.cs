using System;
using System.Reflection;
using UnityEngine;

/// <summary>Reflection trampoline for Level5MazeBuilder.Build().</summary>
public static class RunLevel5Build
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("Level5MazeBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunLevel5Build] type not found."); return; }
        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunLevel5Build] Build() not found."); return; }
        Debug.Log("[RunLevel5Build] " + method.Invoke(null, null));
    }
}
