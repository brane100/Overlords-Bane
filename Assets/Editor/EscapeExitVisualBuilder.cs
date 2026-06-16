using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds a glowing, 80%-transparent "escape" portal visual to Level 5's
/// ExitTrigger, using the existing Assets/escape.mat. Renderer-only child
/// (no collider) so the existing BoxCollider/ExitTrigger logic is untouched.
/// Idempotent. Run via menu or Build() (reflection trampoline).
/// </summary>
public static class EscapeExitVisualBuilder
{
    const string ScenePath    = "Assets/Scenes/Level5.unity";
    const string MaterialPath = "Assets/escape.mat";

    [MenuItem("Tools/Overlords Bane/Build Level 5 Escape Portal Visual")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "Cannot run in play mode.";

        var scene = EditorSceneManager.GetActiveScene().path == ScenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var exit = GameObject.Find("ExitTrigger");
        if (exit == null) return "ERROR: ExitTrigger not found in Level5.";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null) return "ERROR: escape.mat not found at " + MaterialPath;

        // 80% transparent -> alpha 0.2 on the surface colour.
        Color baseColor = mat.GetColor("_BaseColor");
        baseColor.a = 0.2f;
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_Color", baseColor);

        // Glow: emissive, driven from the same hue.
        Color glow = new Color(baseColor.r, baseColor.g, baseColor.b, 1f) * 2.2f;
        glow.a = 1f;
        mat.SetColor("_EmissionColor", glow);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        var bc = exit.GetComponent<BoxCollider>();
        Vector3 size = bc != null ? bc.size : new Vector3(6.4f, 3f, 6.4f);

        var visual = exit.transform.Find("EscapeVisual");
        GameObject visualGO;
        if (visual == null)
        {
            visualGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualGO.name = "EscapeVisual";
            var col = visualGO.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            visualGO.transform.SetParent(exit.transform, false);
        }
        else
        {
            visualGO = visual.gameObject;
        }

        visualGO.transform.localPosition = Vector3.zero;
        visualGO.transform.localRotation = Quaternion.identity;
        visualGO.transform.localScale = size * 0.92f;

        var mr = visualGO.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        var light = visualGO.GetComponent<Light>();
        if (light == null) light = visualGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(baseColor.r, baseColor.g, baseColor.b);
        light.intensity = 4f;
        light.range = 8f;
        light.shadows = LightShadows.None;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        return "EscapeVisual added to ExitTrigger in Level5 — escape.mat glowing, alpha 0.2 (80% transparent). Saved.";
    }
}
