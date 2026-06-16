using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Retunes Level 3 from a bright mirror maze into the oppressive obsidian
/// reflection maze, and installs the mirror-clone reflection system:
///
///   • Mirror walls -> dark semi-transparent OBSIDIAN glass (faint blue, very
///     low brightness) so the player can glimpse a clone "inside" the glass.
///   • Deep blue ambient + heavy fog + a few SPARSE white lights (replacing the
///     bright grid) so reflections emerge from darkness and long sightlines die.
///   • A ghost material for the reflection clone.
///   • A "MirrorManager" running MirrorReflection, wired to the PlayerArmature
///     prefab + ghost material.
///   • Removes the old bright reflection probe.
///
/// Idempotent. Run via menu or Level3ReflectionBuilder.Build() (trampoline).
/// </summary>
public static class Level3ReflectionBuilder
{
    const string ScenePath     = "Assets/Scenes/Level3.unity";
    const string MirrorMatPath = "Assets/Materials/MirrorWall.mat";
    const string GhostMatPath  = "Assets/Materials/Lvl 3/ReflectionGhost.mat";
    const string PlayerPrefab  = "Assets/Prefabs/PlayerArmature.prefab";

    [MenuItem("Tools/Overlords Bane/Build Level 3 Reflection System")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new StringBuilder();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { log.AppendLine("Cannot run in play mode."); return log.ToString(); }

        var scene = EditorSceneManager.GetActiveScene().path == ScenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        TuneObsidianMirror(log);
        Material ghost = MakeGhostMaterial(log);
        TuneAtmosphere(log);
        BuildSparseLights(log);
        RemoveBrightProbe(log);
        WireManager(ghost, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path + ".");
        return log.ToString();
    }

    // ---------------------------------------------------------------- mirror glass

    static void TuneObsidianMirror(StringBuilder log)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MirrorMatPath);
        if (mat == null) { log.AppendLine("WARN: MirrorWall.mat missing."); return; }

        // URP/Lit, Transparent surface so you can see the clone 'inside' the glass,
        // but ZWrite ON so the maze still occludes itself and stays readable.
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetFloat("_Surface", 1f);             // transparent
        mat.SetFloat("_Blend", 0f);               // alpha blend
        mat.SetFloat("_ZWrite", 1f);              // keep depth so distant walls occlude
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        mat.SetColor("_BaseColor", new Color(0.020f, 0.028f, 0.045f, 0.80f)); // near-black, faint blue
        mat.SetFloat("_Metallic", 0.55f);
        mat.SetFloat("_Smoothness", 0.92f);       // glassy obsidian sheen
        mat.SetFloat("_EnvironmentReflections", 1f);

        // Faint internal blue glow so the glass isn't dead black.
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", new Color(0.0f, 0.012f, 0.04f));

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        log.AppendLine("Mirror walls -> dark transparent obsidian glass.");
    }

    static Material MakeGhostMaterial(StringBuilder log)
    {
        EnsureFolder("Assets/Materials/Lvl 3");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, GhostMatPath); }
        else mat.shader = shader;

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        // Cold, pale, translucent — a reflection emerging from the dark.
        mat.SetColor("_BaseColor", new Color(0.45f, 0.58f, 0.72f, 0.85f));
        mat.SetFloat("_Metallic", 0.1f);
        mat.SetFloat("_Smoothness", 0.6f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.06f, 0.10f, 0.16f));

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        log.AppendLine("Ghost reflection material ready.");
        return mat;
    }

    // ---------------------------------------------------------------- mood / lights

    static void TuneAtmosphere(StringBuilder log)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.015f, 0.022f, 0.050f); // deep blue
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.010f, 0.015f, 0.030f);
        RenderSettings.fogDensity = 0.075f;
        RenderSettings.reflectionIntensity = 0.5f;

        var dir = GameObject.Find("Directional Light");
        if (dir != null && dir.TryGetComponent(out Light dl))
        {
            dl.color = new Color(0.2f, 0.3f, 0.5f);
            dl.intensity = 0.08f; // almost off — the maze is lit by sparse points
        }
        log.AppendLine("Atmosphere -> deep blue ambient, heavy fog, near-dark directional.");
    }

    static void BuildSparseLights(StringBuilder log)
    {
        // Remove the previous bright grid.
        var grid = GameObject.Find("HiddenLights");
        if (grid != null) Object.DestroyImmediate(grid);
        var oldTorches = GameObject.Find("Torches");
        if (oldTorches != null) Object.DestroyImmediate(oldTorches);

        var root = GameObject.Find("SparseLights");
        if (root != null) Object.DestroyImmediate(root);
        root = new GameObject("SparseLights");

        // A handful of cold white pools scattered across the maze footprint.
        Vector3 center = new Vector3(42.87f, 0f, 54.24f);
        var floor = GameObject.Find("Floor");
        if (floor != null) center = new Vector3(floor.transform.position.x, 0f, floor.transform.position.z);

        float ceilingY = 4.5f;
        var ceiling = GameObject.Find("Ceiling");
        if (ceiling != null && ceiling.transform.position.y > 1f && ceiling.transform.position.y < 30f)
            ceilingY = ceiling.transform.position.y - 0.5f;

        var rng = new System.Random(73);
        float reach = 16f;
        int count = 8;
        for (int i = 0; i < count; i++)
        {
            float dx = (float)(rng.NextDouble() * 2 - 1) * reach;
            float dz = (float)(rng.NextDouble() * 2 - 1) * reach;
            var go = new GameObject("SparseLight_" + i.ToString("00"));
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(center.x + dx, ceilingY, center.z + dz);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.85f, 0.9f, 1f);
            l.intensity = 1.4f;
            l.range = 11f;
            l.shadows = LightShadows.Soft;
        }
        log.AppendLine("Sparse lights placed: " + count + " cold white pools.");
    }

    static void RemoveBrightProbe(StringBuilder log)
    {
        var probe = GameObject.Find("MirrorMaze_ReflectionProbe");
        if (probe != null) { Object.DestroyImmediate(probe); log.AppendLine("Removed bright reflection probe."); }
    }

    // ---------------------------------------------------------------- manager wiring

    static void WireManager(Material ghost, StringBuilder log)
    {
        var go = GameObject.Find("MirrorManager");
        if (go == null) go = new GameObject("MirrorManager");

        var mr = go.GetComponent<MirrorReflection>();
        if (mr == null) mr = go.AddComponent<MirrorReflection>();

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);

        var so = new SerializedObject(mr);
        so.FindProperty("clonePrefab").objectReferenceValue = playerPrefab;
        so.FindProperty("ghostMaterial").objectReferenceValue = ghost;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine("MirrorManager wired (clonePrefab=" + (playerPrefab != null) +
                       ", ghost=" + (ghost != null) + ").");
    }

    // ---------------------------------------------------------------- helpers

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
