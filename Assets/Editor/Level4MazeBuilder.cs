using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Idempotent builder for Level 4 (Kharros — the cursed sigil vaults). Reads the
/// "Level 4 png" maze (14x14), decodes it by sampling wall pixels, and builds:
///
///   • Thick rust-stone walls under "Level4Maze", centered on the floor.
///   • Ember torches embedded in the walls (Kharros orange).
///   • Cursed floor sigils (FloorSigil) on random corridor cells — glowing runes
///     that telegraph, then ignite for 25 burn damage.
///   • A LevelThemeApplier wired to Level4_Kharros (fog/ambient/light/material tint).
///   • SpawnPoint moved to the maze entrance.
///
/// Re-runnable. Run via menu or Level4MazeBuilder.Build() (reflection trampoline).
/// </summary>
public static class Level4MazeBuilder
{
    const string PngPath   = "Assets/mazes/Level 4 png.png";
    const string ScenePath = "Assets/Scenes/Level4.unity";
    const string ThemePath = "Assets/ScriptableObjects/Themes/Level4_Kharros.asset";
    const string SigilTexPath = "Assets/Art/Sigils/KharrosSigil.png";

    const int   GRID  = 14;      // 14x14 maze
    const float CELL  = 8f;
    const float THICK = 2.0f;    // thick walls
    const float FALLBACK_WALL_HEIGHT = 5f;

    const int SVG_MIN  = 2;
    const int SVG_STEP = 16;
    const int PNG_SIZE = 226;
    const int TEX = 256;

    static readonly Color Ember = new Color(1f, 0.478f, 0.227f); // Kharros sigil accent

    const string MazeRootName  = "Level4Maze";
    const string TorchRootName = "Torches";
    const string SigilRootName = "FloorSigils";

    [MenuItem("Tools/Overlords Bane/Build Level 4 Maze (Kharros sigils)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new StringBuilder();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { log.AppendLine("Cannot run in play mode."); return log.ToString(); }

        // ---------- 1) Decode PNG ----------
        Texture2D tex = LoadPng(PngPath, log);
        if (tex == null) return log.ToString();

        bool[,] vWall = new bool[GRID + 1, GRID];
        bool[,] hWall = new bool[GRID, GRID + 1];
        for (int i = 0; i <= GRID; i++)
            for (int r = 0; r < GRID; r++)
                vWall[i, r] = SampleWall(tex, SVG_MIN + SVG_STEP * i, SVG_MIN + SVG_STEP * r + SVG_STEP / 2);
        for (int j = 0; j <= GRID; j++)
            for (int c = 0; c < GRID; c++)
                hWall[c, j] = SampleWall(tex, SVG_MIN + SVG_STEP * c + SVG_STEP / 2, SVG_MIN + SVG_STEP * j);
        Object.DestroyImmediate(tex);
        log.AppendLine("Decoded 14x14 maze from " + PngPath);

        // ---------- 2) Open scene ----------
        var scene = EditorSceneManager.GetActiveScene().path == ScenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var theme = AssetDatabase.LoadAssetAtPath<LevelTheme>(ThemePath);
        Color wallColor = theme != null ? theme.wallColor : new Color(0.227f, 0.094f, 0.063f);
        Color accent    = theme != null ? theme.sigilAccentColor : Ember;

        float wallHeight = ResolveWallHeight(scene);
        Vector3 center = ResolveFloorCenter();
        log.AppendLine("Center " + center + ", wall height " + wallHeight + ".");

        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain != null)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            td.SetHeights(0, 0, new float[res, res]);
            log.AppendLine("Terrain flattened.");
        }

        // ---------- 3) Materials ----------
        Material wallMat  = MakeLit("Assets/Materials/Lvl 4/Level4Wall.mat",  wallColor, 0.1f, 0.2f);
        Texture2D sigilTex = MakeSigilTexture(log);
        Material sigilMat = MakeSigilMaterial(sigilTex, accent, log);

        // ---------- 4) Walls ----------
        float extent = GRID * CELL;
        Vector3 origin = new Vector3(center.x - extent / 2f, 0f, center.z - extent / 2f);

        var existing = GameObject.Find(MazeRootName);
        if (existing != null) Object.DestroyImmediate(existing);
        var root = new GameObject(MazeRootName);

        var wallFaces = new List<(Vector3 pos, Vector3 dir)>();
        int wallCount = 0;

        for (int i = 0; i <= GRID; i++)
            for (int r = 0; r < GRID; r++)
            {
                if (!vWall[i, r]) continue;
                float wx = origin.x + i * CELL;
                float wz = origin.z + (GRID - r - 0.5f) * CELL;
                CreateWall(root.transform, wallMat, "Wall_V",
                    new Vector3(wx, wallHeight / 2f, wz), new Vector3(THICK, wallHeight, CELL + THICK));
                wallCount++;
                wallFaces.Add((new Vector3(wx, wallHeight, wz), (i == 0) ? Vector3.right : (i == GRID ? Vector3.left : Vector3.right)));
            }
        for (int j = 0; j <= GRID; j++)
            for (int c = 0; c < GRID; c++)
            {
                if (!hWall[c, j]) continue;
                float wx = origin.x + (c + 0.5f) * CELL;
                float wz = origin.z + (GRID - j) * CELL;
                CreateWall(root.transform, wallMat, "Wall_H",
                    new Vector3(wx, wallHeight / 2f, wz), new Vector3(CELL + THICK, wallHeight, THICK));
                wallCount++;
                wallFaces.Add((new Vector3(wx, wallHeight, wz), (j == 0) ? Vector3.back : (j == GRID ? Vector3.forward : Vector3.back)));
            }
        log.AppendLine("Walls built: " + wallCount + ".");

        // ---------- 5) Ember torches ----------
        int torchCount = BuildTorches(wallFaces, wallHeight, accent, log);

        // ---------- 6) Cursed sigils ----------
        int sigilCount = BuildSigils(origin, sigilMat, accent, log);

        // ---------- 7) Theme applier ----------
        WireTheme(theme, log);

        // ---------- 8) Spawn ----------
        MoveSpawnToEntrance(hWall, origin, log);

        // ---------- 9) Save ----------
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path + " — walls=" + wallCount +
                       " torches=" + torchCount + " sigils=" + sigilCount + ".");
        return log.ToString();
    }

    // ---------------------------------------------------------------- PNG

    static Texture2D LoadPng(string assetPath, StringBuilder log)
    {
        string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(abs)) { log.AppendLine("ERROR: PNG not found at " + abs); return null; }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(abs))) { log.AppendLine("ERROR: decode failed."); return null; }
        return tex;
    }

    static bool SampleWall(Texture2D tex, int sx, int sy)
    {
        int tx = Mathf.Clamp(sx - 1, 0, tex.width - 1);
        int ty = Mathf.Clamp(PNG_SIZE - sy, 0, tex.height - 1);
        float darkest = 1f;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int px = Mathf.Clamp(tx + dx, 0, tex.width - 1);
                int py = Mathf.Clamp(ty + dy, 0, tex.height - 1);
                Color c = tex.GetPixel(px, py);
                float g = (c.r + c.g + c.b) / 3f;
                if (g < darkest) darkest = g;
            }
        return darkest < 0.5f;
    }

    // ---------------------------------------------------------------- geometry

    static void CreateWall(Transform parent, Material mat, string name, Vector3 pos, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        wall.isStatic = true;
    }

    static int BuildTorches(List<(Vector3 pos, Vector3 dir)> faces, float wallHeight, Color accent, StringBuilder log)
    {
        var torchRoot = GameObject.Find(TorchRootName);
        if (torchRoot == null) torchRoot = new GameObject(TorchRootName);
        for (int i = torchRoot.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(torchRoot.transform.GetChild(i).gameObject);
        if (faces.Count == 0) return 0;

        int target = Mathf.Min(26, faces.Count);
        int stride = Mathf.Max(1, faces.Count / target);
        float mountY = wallHeight * 0.65f;
        int count = 0;
        for (int idx = 0; idx < faces.Count && count < target; idx += stride)
        {
            var (pos, dir) = faces[idx];
            var go = new GameObject("Torch_" + count.ToString("00"));
            go.transform.SetParent(torchRoot.transform, false);
            go.transform.position = new Vector3(pos.x, mountY, pos.z) + dir * (THICK * 0.5f + 0.2f);
            go.transform.rotation = Quaternion.LookRotation((dir + Vector3.down * 0.4f).normalized, Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = accent;
            l.intensity = 6f;
            l.range = 24f;
            l.spotAngle = 55f;
            l.shadows = LightShadows.Soft;
            count++;
        }
        log.AppendLine("Torches placed: " + count + " (ember).");
        return count;
    }

    static int BuildSigils(Vector3 origin, Material sigilMat, Color accent, StringBuilder log)
    {
        var sigilRoot = GameObject.Find(SigilRootName);
        if (sigilRoot != null) Object.DestroyImmediate(sigilRoot);
        sigilRoot = new GameObject(SigilRootName);

        var rng = new System.Random(404);
        var chosen = new HashSet<int>();
        int wanted = 16, guard = 0;
        while (chosen.Count < wanted && guard++ < 600)
        {
            int c = rng.Next(GRID);
            int r = rng.Next(1, GRID); // skip entrance row
            chosen.Add(r * GRID + c);
        }

        int count = 0;
        foreach (int key in chosen)
        {
            int r = key / GRID, c = key % GRID;
            float wx = origin.x + (c + 0.5f) * CELL;
            float wz = origin.z + (GRID - r - 0.5f) * CELL;

            var trap = new GameObject("FloorSigil_" + count.ToString("00"));
            trap.transform.SetParent(sigilRoot.transform, false);
            trap.transform.position = new Vector3(wx, 0.03f, wz); // just above floor

            // Flat glowing rune quad.
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Rune";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(trap.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, face up
            quad.transform.localScale = Vector3.one * (CELL * 0.7f);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = sigilMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Flare light that pulses with the rune.
            var flare = new GameObject("Flare");
            flare.transform.SetParent(trap.transform, false);
            flare.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var fl = flare.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.color = accent;
            fl.range = 7f;
            fl.intensity = 0.3f;
            fl.shadows = LightShadows.None;

            var sigil = trap.AddComponent<FloorSigil>();
            sigil.SetAccent(accent);
            count++;
        }
        log.AppendLine("Cursed sigils placed: " + count + " (25 burn, telegraphed).");
        return count;
    }

    // ---------------------------------------------------------------- sigil art

    static Texture2D MakeSigilTexture(StringBuilder log)
    {
        EnsureFolder("Assets/Art/Sigils");
        var tex = new Texture2D(TEX, TEX, TextureFormat.RGBA32, false);
        var px = new Color[TEX * TEX];

        for (int y = 0; y < TEX; y++)
            for (int x = 0; x < TEX; x++)
            {
                float u = (x / (float)(TEX - 1)) * 2f - 1f;
                float v = (y / (float)(TEX - 1)) * 2f - 1f;
                float rr = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);

                float a = 0f;
                // two concentric rings
                a = Mathf.Max(a, Ring(rr, 0.92f, 0.03f));
                a = Mathf.Max(a, Ring(rr, 0.62f, 0.02f));
                // radial spokes
                float spokes = Mathf.Abs(Mathf.Cos(ang * 6f));
                if (rr > 0.62f && rr < 0.92f && spokes > 0.97f) a = 1f;
                // central rune cross
                if (rr < 0.5f && (Mathf.Abs(u) < 0.05f || Mathf.Abs(v) < 0.05f)) a = 1f;
                if (Ring(rr, 0.28f, 0.02f) > 0f) a = Mathf.Max(a, Ring(rr, 0.28f, 0.02f));

                px[y * TEX + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }

        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), SigilTexPath), tex.EncodeToPNG());
        AssetDatabase.ImportAsset(SigilTexPath, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(SigilTexPath) as TextureImporter;
        if (imp != null) { imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport(); }
        Object.DestroyImmediate(tex);
        log.AppendLine("Sigil rune texture painted.");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(SigilTexPath);
    }

    static float Ring(float r, float radius, float halfWidth)
    {
        float d = Mathf.Abs(r - radius);
        return d < halfWidth ? 1f - d / halfWidth : 0f;
    }

    static Material MakeSigilMaterial(Texture2D tex, Color accent, StringBuilder log)
    {
        EnsureFolder("Assets/Materials/Lvl 4");
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Lvl 4/KharrosSigil.mat");
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, "Assets/Materials/Lvl 4/KharrosSigil.mat"); }
        else mat.shader = shader;
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 1f); // additive-ish glow
        mat.SetFloat("_ZWrite", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", accent); // FloorSigil drives HDR brightness per-instance
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        log.AppendLine("Sigil material ready.");
        return mat;
    }

    // ---------------------------------------------------------------- theme / spawn

    static void WireTheme(LevelTheme theme, StringBuilder log)
    {
        var go = GameObject.Find("LevelThemeApplier");
        if (go == null) go = new GameObject("LevelThemeApplier");
        var applier = go.GetComponent<LevelThemeApplier>();
        if (applier == null) applier = go.AddComponent<LevelThemeApplier>();
        var so = new SerializedObject(applier);
        so.FindProperty("theme").objectReferenceValue = theme;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("LevelThemeApplier -> Level4_Kharros" + (theme != null ? "" : " (MISSING)") + ".");
    }

    static void MoveSpawnToEntrance(bool[,] hWall, Vector3 origin, StringBuilder log)
    {
        var spawn = GameObject.Find("SpawnPoint");
        if (spawn == null) return;
        for (int c = 0; c < GRID; c++)
            if (!hWall[c, 0])
            {
                float wx = origin.x + (c + 0.5f) * CELL;
                float wz = origin.z + (GRID - 0.5f) * CELL;
                spawn.transform.position = new Vector3(wx, 1f, wz);
                log.AppendLine("SpawnPoint moved to entrance column " + c + ".");
                return;
            }
    }

    static Vector3 ResolveFloorCenter()
    {
        var floor = GameObject.Find("Floor");
        if (floor != null) { var p = floor.transform.position; return new Vector3(p.x, 0f, p.z); }
        return new Vector3(42.87f, 0f, 54.24f);
    }

    static float ResolveWallHeight(UnityEngine.SceneManagement.Scene scene)
    {
        var ceiling = GameObject.Find("Ceiling");
        if (ceiling == null)
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == "Ceiling" && go.scene == scene) { ceiling = go; break; }
        if (ceiling != null) { float y = ceiling.transform.position.y; if (y > 1f && y < 30f) return y; }
        return FALLBACK_WALL_HEIGHT;
    }

    static Material MakeLit(string path, Color baseColor, float metallic, float smoothness)
    {
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = shader;
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

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
