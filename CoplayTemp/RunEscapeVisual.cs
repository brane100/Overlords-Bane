using System;
using System.Reflection;
using UnityEngine;

/// <summary>Reflection trampoline for EscapeExitVisualBuilder.Build().</summary>
public static class RunEscapeVisual
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("EscapeExitVisualBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunEscapeVisual] type not found."); return; }
        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunEscapeVisual] Build() not found."); return; }
        Debug.Log("[RunEscapeVisual] " + method.Invoke(null, null));
    }
}
