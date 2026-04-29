using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class DiagLightLayers
{
    public static void Execute()
    {
        // 1) Report Floor renderer state
        var floor = GameObject.Find("Floor");
        if (floor != null)
        {
            var r = floor.GetComponent<Renderer>();
            Debug.Log($"[Diag] Floor renderingLayerMask = {r.renderingLayerMask} (0x{r.renderingLayerMask:X}); " +
                      $"shader = {r.sharedMaterial?.shader.name}; enabled = {r.enabled}");
        }

        // 2) Report a torch + directional light
        foreach (var name in new[] { "Sigil_Glow", "Directional Light", "Torches/Torch_05" })
        {
            var go = GameObject.Find(name) ?? FindByPath(name);
            if (go == null) { Debug.Log($"[Diag] missing {name}"); continue; }
            var l = go.GetComponent<Light>();
            if (l == null) { Debug.Log($"[Diag] {name} no Light"); continue; }
            Debug.Log($"[Diag] {name}: enabled={l.enabled} type={l.type} intensity={l.intensity} " +
                      $"range={l.range} renderingLayerMask={l.renderingLayerMask} cullingMask={l.cullingMask}");
        }

        // 3) URP supports light layers?
        var rp = QualitySettings.renderPipeline as RenderPipelineAsset;
        if (rp != null)
        {
            var so = new SerializedObject(rp);
            var sl = so.FindProperty("m_SupportsLightLayers");
            Debug.Log($"[Diag] URP m_SupportsLightLayers = {(sl != null ? sl.boolValue.ToString() : "n/a")}");
        }
    }

    public static void Fix()
    {
        // Force every renderer in the scene to accept all light layers.
        int fixedCount = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            r.renderingLayerMask = 0xFFFFFFFF;
            EditorUtility.SetDirty(r);
            fixedCount++;
        }

        // Also make sure every light targets all layers.
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            l.renderingLayerMask = -1; // all layers
            EditorUtility.SetDirty(l);
        }

        // Disable light-layer gating on the URP asset (solo project, not needed).
        var rp = QualitySettings.renderPipeline as RenderPipelineAsset;
        if (rp != null)
        {
            var so = new SerializedObject(rp);
            var sl = so.FindProperty("m_SupportsLightLayers");
            if (sl != null) sl.boolValue = false;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rp);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[Diag] Fix applied to {fixedCount} renderers + all lights; light-layers gating disabled.");
    }

    static GameObject FindByPath(string path)
    {
        var t = GameObject.Find(path.Split('/')[0])?.transform;
        return t == null ? null : (GameObject.Find(path) ?? (t.Find(path.Substring(path.IndexOf('/') + 1))?.gameObject));
    }
}
