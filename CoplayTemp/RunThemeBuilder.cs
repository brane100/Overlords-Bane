using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RunThemeBuilder
{
    public static void Execute()
    {
        Type t = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType("LevelThemeBuilder");
            if (t != null) break;
        }
        if (t == null) { Debug.LogError("[RunThemeBuilder] LevelThemeBuilder type not found."); return; }

        var m = t.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (m == null) { Debug.LogError("[RunThemeBuilder] Build() not found."); return; }

        try
        {
            var result = m.Invoke(null, null) as string;
            Debug.Log("[RunThemeBuilder] OK\n" + result);
        }
        catch (TargetInvocationException tie)
        {
            var inner = tie.InnerException;
            Debug.LogError("[RunThemeBuilder] INNER: " + (inner != null ? inner.GetType().Name + ": " + inner.Message + "\n" + inner.StackTrace : "null"));
        }
    }
}
