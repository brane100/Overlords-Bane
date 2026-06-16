using System;
using System.Reflection;
using UnityEngine;

/// <summary>Reflection trampoline for Level5WinScreenBuilder.Build().</summary>
public static class RunLevel5WinScreen
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("Level5WinScreenBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunLevel5WinScreen] type not found."); return; }
        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunLevel5WinScreen] Build() not found."); return; }
        Debug.Log("[RunLevel5WinScreen] " + method.Invoke(null, null));
    }
}
