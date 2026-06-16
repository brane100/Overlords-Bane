using System;
using System.Reflection;
using UnityEngine;

/// <summary>Reflection trampoline for Level345ExitBuilder.BuildAll().</summary>
public static class RunLevel345Exits
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("Level345ExitBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunLevel345Exits] type not found."); return; }
        var method = type.GetMethod("BuildAll", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunLevel345Exits] BuildAll() not found."); return; }
        Debug.Log("[RunLevel345Exits] " + method.Invoke(null, null));
    }
}
