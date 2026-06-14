using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Idempotent builder for the Axiom 3D eyeball assets:
///   1. A procedurally painted IRIS texture (transparent outside the iris circle)
///   2. AxiomSclera.mat — emissive lit material for the glowing eyeball sphere
///   3. AxiomIris.mat   — unlit transparent material for the tracking iris quad
///   4. EyeWatcher prefab — sphere (sclera) + Iris quad child + point Light child
///   5. EyeSpawner wired into Level 1 (prefab + terrain + Level1_Axiom theme)
///
/// A glowing sphere embedded in the wall is unmistakably 3D, attached, and visible
/// from every angle; only the iris tracks the player. Sigil violet #8B5CF6.
/// </summary>
public static class EyeAssetBuilder
{
    const string IrisTexPath  = "Assets/Art/Eyes/AxiomIris.png";
    const string ScleraMatPath = "Assets/Materials/Lvl 1/AxiomSclera.mat";
    const string IrisMatPath   = "Assets/Materials/Lvl 1/AxiomIris.mat";
    const string PrefabPath = "Assets/Prefabs/EyeWatcher.prefab";
    const string ThemePath  = "Assets/ScriptableObjects/Themes/Level1_Axiom.asset";
    const int TexSize = 256;

    static readonly Color Violet = new Color(0.545f, 0.361f, 0.965f); // #8B5CF6

    [MenuItem("Tools/Overlords Bane/Build Axiom Eyes (3D eyeball)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new StringBuilder();

        var irisTex   = BuildIrisTexture(log);
        var scleraMat = BuildScleraMaterial(log);
        var irisMat   = BuildIrisMaterial(irisTex, log);
        BuildPrefab(scleraMat, irisMat, log);
        WireLevel1Spawner(log);

        return log.ToString();
    }

    // ---------------------------------------------------------------- iris texture

    static Texture2D BuildIrisTexture(StringBuilder log)
    {
        EnsureFolder("Assets/Art/Eyes");

        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        var px = new Color[TexSize * TexSize];

        const float irisR  = 0.92f;                 // iris fills most of the quad
        const float pupilA = 0.16f, pupilB = 0.70f; // vertical slit pupil

        Color irisDeep = new Color(0.30f, 0.16f, 0.62f); // outer iris
        Color irisBright = new Color(0.72f, 0.52f, 1.0f); // inner iris
        Color pupil = new Color(0.02f, 0.01f, 0.05f);

        for (int y = 0; y < TexSize; y++)
        {
            float v = (y / (float)(TexSize - 1)) * 2f - 1f;
            for (int x = 0; x < TexSize; x++)
            {
                float u = (x / (float)(TexSize - 1)) * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float slit = (u * u) / (pupilA * pupilA) + (v * v) / (pupilB * pupilB);

                Color c; float a;

                if (r <= irisR)
                {
                    if (slit < 1f)
                    {
                        c = pupil; a = 1f;
                    }
                    else
                    {
                        float t = r / irisR;                          // 0 center .. 1 rim
                        c = Color.Lerp(irisBright, irisDeep, t);
                        if (t > 0.88f) c *= 0.5f;                     // dark limbal ring
                        // radial fibre streaks for life
                        float streak = 0.85f + 0.15f * Mathf.Sin(Mathf.Atan2(v, u) * 18f);
                        c *= streak;
                        a = 1f - Mathf.SmoothStep(0.95f, 1f, t);
                    }

                    // upper-left specular catchlight
                    float hl = Vector2.Distance(new Vector2(u, v), new Vector2(-0.28f, 0.32f));
                    if (hl < 0.16f) { float k = 1f - hl / 0.16f; c = Color.Lerp(c, Color.white, k * 0.9f); }
                }
                else
                {
                    c = Color.clear; a = 0f;
                }

                c.a = a;
                px[y * TexSize + x] = c;
            }
        }

        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(IrisTexPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(IrisTexPath, ImportAssetOptions.ForceUpdate);

        var imp = AssetImporter.GetAtPath(IrisTexPath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }

        Object.DestroyImmediate(tex);
        log.AppendLine("Iris texture painted -> " + IrisTexPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(IrisTexPath);
    }

    // ---------------------------------------------------------------- sclera material

    static Material BuildScleraMaterial(StringBuilder log)
    {
        EnsureFolder("Assets/Materials/Lvl 1");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { log.AppendLine("ERROR: URP/Lit not found."); return null; }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(ScleraMatPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, ScleraMatPath); }
        else mat.shader = shader;

        // Opaque, lit (so the sphere shows 3D roundness) + emissive glow.
        mat.SetColor("_BaseColor", new Color(0.55f, 0.50f, 0.72f)); // muted lavender sclera
        mat.SetFloat("_Smoothness", 0.5f);
        mat.SetFloat("_Metallic", 0f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Violet * 0.9f); // EyeWatcher overrides per-eye
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
        log.AppendLine("Sclera material -> " + ScleraMatPath + " (URP/Lit emissive).");
        return mat;
    }

    // ---------------------------------------------------------------- iris material

    static Material BuildIrisMaterial(Texture2D tex, StringBuilder log)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) { log.AppendLine("ERROR: URP/Unlit not found."); return null; }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(IrisMatPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, IrisMatPath); }
        else mat.shader = shader;

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f); // double-sided so winding never hides it
        // Decal-style: ignore depth so the iris always paints on the front of the
        // eyeball instead of being occluded by the sclera sphere it sits on.
        mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50; // over the sclera

        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", new Color(1.1f, 1.1f, 1.1f, 1f)); // detail intact

        EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
        log.AppendLine("Iris material -> " + IrisMatPath + " (URP/Unlit transparent, double-sided).");
        return mat;
    }

    // ---------------------------------------------------------------- prefab

    static void BuildPrefab(Material scleraMat, Material irisMat, StringBuilder log)
    {
        EnsureFolder("Assets/Prefabs");

        // Root = sclera sphere.
        var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "EyeWatcher";
        var col = root.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        var mr = root.GetComponent<MeshRenderer>();
        mr.sharedMaterial = scleraMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        if (root.GetComponent<EyeWatcher>() == null)
            root.AddComponent<EyeWatcher>();

        // Iris quad child — sits on the front surface, tracked at runtime.
        var iris = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iris.name = "Iris";
        var iCol = iris.GetComponent<Collider>();
        if (iCol != null) Object.DestroyImmediate(iCol);
        iris.transform.SetParent(root.transform, false);
        // Just in front of the sphere surface (radius 0.5) so the opaque sclera
        // never occludes it; small enough to stay within the eyeball silhouette.
        iris.transform.localPosition = new Vector3(0f, 0f, 0.52f);
        iris.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
        var iMr = iris.GetComponent<MeshRenderer>();
        iMr.sharedMaterial = irisMat;
        iMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        iMr.receiveShadows = false;

        // Point light child.
        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0f, 0.8f);
        var pl = lightGo.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.color = Violet;
        pl.range = 6f;
        pl.intensity = 1.0f;
        pl.shadows = LightShadows.None;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        log.AppendLine("Prefab rebuilt -> " + PrefabPath + " (sclera sphere + iris quad + point light).");
    }

    // ---------------------------------------------------------------- scene wiring

    static void WireLevel1Spawner(StringBuilder log)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            log.AppendLine("WARNING: in play mode — skipped Level1 EyeSpawner wiring. Stop play and re-run.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Level1.unity")
            EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);

        var go = GameObject.Find("EyeSpawner");
        if (go == null) go = new GameObject("EyeSpawner");

        var spawner = go.GetComponent<EyeSpawner>();
        if (spawner == null) spawner = go.AddComponent<EyeSpawner>();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var terrain = Object.FindFirstObjectByType<Terrain>();
        var theme = AssetDatabase.LoadAssetAtPath<LevelTheme>(ThemePath);

        var so = new SerializedObject(spawner);
        so.FindProperty("eyePrefab").objectReferenceValue = prefab;
        if (terrain != null) so.FindProperty("terrain").objectReferenceValue = terrain;
        if (theme != null) so.FindProperty("theme").objectReferenceValue = theme;
        // Sphere-appropriate placement: ~1m eyeball whose centre sits just off the
        // wall face so roughly 60% emerges (the rest embedded in the wall).
        so.FindProperty("eyeScale").floatValue = 1.0f;
        so.FindProperty("surfaceOffset").floatValue = 0.1f;
        so.FindProperty("eyeMinHeight").floatValue = 1.4f;
        so.FindProperty("eyeMaxHeight").floatValue = 2.8f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var scene = go.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("EyeSpawner wired in Level1 (prefab=" + (prefab != null) +
                       ", terrain=" + (terrain != null) + ", theme=" + (theme != null) + "). Saved " + scene.path);
    }

    // ---------------------------------------------------------------- helpers

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
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
