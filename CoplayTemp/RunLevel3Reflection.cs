using System;
using System.Reflection;
using UnityEngine;

/// <summary>Reflection trampoline for Level3ReflectionBuilder.Build().</summary>
public static class RunLevel3Reflection
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("Level3ReflectionBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunLevel3Reflection] type not found."); return; }
        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunLevel3Reflection] Build() not found."); return; }
        Debug.Log("[RunLevel3Reflection] " + method.Invoke(null, null));
    }
}
