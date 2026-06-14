using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RunEyeBuilder
{
    public static void Execute()
    {
        Type t = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType("EyeAssetBuilder");
            if (t != null) break;
        }
        if (t == null) { Debug.LogError("[RunEyeBuilder] EyeAssetBuilder type not found."); return; }

        var m = t.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        try
        {
            var result = m.Invoke(null, null) as string;
            Debug.Log("[RunEyeBuilder] OK\n" + result);
        }
        catch (TargetInvocationException tie)
        {
            var inner = tie.InnerException;
            Debug.LogError("[RunEyeBuilder] INNER: " + (inner != null ? inner.GetType().Name + ": " + inner.Message + "\n" + inner.StackTrace : "null"));
        }
    }
}
