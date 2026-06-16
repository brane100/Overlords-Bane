using System;
using System.Reflection;
using UnityEngine;

/// <summary>Reflection trampoline for Level4MazeBuilder.Build().</summary>
public static class RunLevel4Build
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("Level4MazeBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunLevel4Build] type not found."); return; }
        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunLevel4Build] Build() not found."); return; }
        Debug.Log("[RunLevel4Build] " + method.Invoke(null, null));
    }
}
