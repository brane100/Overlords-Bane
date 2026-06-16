using System;
using System.Reflection;
using UnityEngine;

/// <summary>Reflection trampoline for HeadFollowCameraBuilder.Build().</summary>
public static class RunHeadFollowCamera
{
    public static void Execute()
    {
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType("HeadFollowCameraBuilder");
            if (type != null) break;
        }
        if (type == null) { Debug.LogError("[RunHeadFollowCamera] type not found."); return; }
        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunHeadFollowCamera] Build() not found."); return; }
        Debug.Log("[RunHeadFollowCamera] " + method.Invoke(null, null));
    }
}
