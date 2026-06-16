using System;
using System.Reflection;
using UnityEngine;

/// <summary>Reflection trampoline for FirstPersonCameraFixBuilder.Build().</summary>
public static class RunFirstPersonCameraFix
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("FirstPersonCameraFixBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunFirstPersonCameraFix] type not found."); return; }
        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunFirstPersonCameraFix] Build() not found."); return; }
        Debug.Log("[RunFirstPersonCameraFix] " + method.Invoke(null, null));
    }
}
