using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class FixUrpLights
{
    public static void Execute()
    {
        // Active asset for current quality level (this is what the Scene View renders with).
        var active = QualitySettings.renderPipeline as RenderPipelineAsset;
        var def    = GraphicsSettings.defaultRenderPipeline;
        Debug.Log($"[FixUrpLights] Quality level {QualitySettings.GetQualityLevel()} '" +
                  $"{QualitySettings.names[QualitySettings.GetQualityLevel()]}'");
        Debug.Log($"[FixUrpLights] QualitySettings.renderPipeline = " +
                  (active != null ? AssetDatabase.GetAssetPath(active) : "<null, falls back to default>"));
        Debug.Log($"[FixUrpLights] GraphicsSettings.defaultRenderPipeline = " +
                  (def != null ? AssetDatabase.GetAssetPath(def) : "<null>"));

        // Patch every URP asset in the project so whichever is active is fixed.
        string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (rp == null) continue;
            Patch(rp, path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[FixUrpLights] Done patching all URP assets.");
    }

    static void Patch(RenderPipelineAsset rp, string path)
    {
        var so = new SerializedObject(rp);
        int before = Get(so, "m_AdditionalLightsRenderingMode");
        SetEnum(so, "m_AdditionalLightsRenderingMode", 2); // PerPixel
        SetInt (so, "m_AdditionalLightsPerObjectLimit", 8);
        SetEnum(so, "m_MainLightRenderingMode", 1);         // PerPixel
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(rp);
        Debug.Log($"[FixUrpLights] Patched {path} (addLightsMode {before} -> 2)");
    }

    static int Get(SerializedObject so, string p)
    {
        var prop = so.FindProperty(p);
        if (prop == null) return -1;
        return prop.propertyType == SerializedPropertyType.Enum ? prop.enumValueIndex : prop.intValue;
    }
    static void SetEnum(SerializedObject so, string p, int v)
    { var prop = so.FindProperty(p); if (prop != null) prop.enumValueIndex = v; }
    static void SetInt(SerializedObject so, string p, int v)
    { var prop = so.FindProperty(p); if (prop != null) prop.intValue = v; }
}
