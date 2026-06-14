using System; using System.Reflection; using UnityEngine;
public static class RunLevel2Builder
{
    public static void Execute()
    {
        Type t = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) { t = asm.GetType("Level2ExitBuilder"); if (t != null) break; }
        if (t == null) { Debug.LogError("[RunLevel2Builder] Type not found."); return; }
        try { Debug.Log("[RunLevel2Builder]\n" + t.GetMethod("Build", BindingFlags.Public | BindingFlags.Static).Invoke(null, null)); }
        catch (TargetInvocationException tie) { Debug.LogError("[RunLevel2Builder] " + tie.InnerException?.Message + "\n" + tie.InnerException?.StackTrace); }
    }
}
