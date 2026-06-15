using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Reflection trampoline: the coplay dynamic compiler can't see the editor
/// assembly's Level2MazeBuilder type directly, so invoke it by reflection.
/// </summary>
public static class RunLevel2Build
{
    public static void Execute()
    {
        var type = FindType("Level2MazeBuilder");
        if (type == null) { Debug.LogError("[RunLevel2Build] Level2MazeBuilder type not found."); return; }

        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (method == null) { Debug.LogError("[RunLevel2Build] Build() not found."); return; }

        var result = method.Invoke(null, null);
        Debug.Log("[RunLevel2Build] " + result);
    }

    static Type FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }
}
