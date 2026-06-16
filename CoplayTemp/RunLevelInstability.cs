using System;
using System.Reflection;
using UnityEngine;

/// <summary>Reflection trampoline for LevelInstabilityBuilder.BuildAll().</summary>
public static class RunLevelInstability
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("LevelInstabilityBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunLevelInstability] type not found."); return; }
        var method = type.GetMethod("BuildAll", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunLevelInstability] BuildAll() not found."); return; }
        Debug.Log("[RunLevelInstability] " + method.Invoke(null, null));
    }
}
