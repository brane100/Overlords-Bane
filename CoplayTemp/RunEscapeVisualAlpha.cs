using UnityEditor;
using UnityEngine;

/// <summary>One-off: dial escape.mat down to 10% visible (90% see-through).</summary>
public static class RunEscapeVisualAlpha
{
    public static void Execute()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/escape.mat");
        if (mat == null) { Debug.LogError("[RunEscapeVisualAlpha] escape.mat not found."); return; }

        Color baseColor = mat.GetColor("_BaseColor");
        baseColor.a = 0.1f;
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_Color", baseColor);

        Color glow = new Color(baseColor.r, baseColor.g, baseColor.b, 1f) * 2.2f;
        glow.a = 1f;
        mat.SetColor("_EmissionColor", glow);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[RunEscapeVisualAlpha] escape.mat alpha set to 0.1 (10% visible).");
    }
}
