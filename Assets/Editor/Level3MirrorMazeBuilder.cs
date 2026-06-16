using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Idempotent builder for Level 3 (the mirror maze): reads "Level 3 png" from
/// Assets/mazes, decodes its 13x13 orthogonal layout by sampling wall pixels,
/// and constructs a claustrophobic mirror labyrinth in Level3.unity:
///
///   • THIN mirror walls (cubes, highly metallic/smooth) — tight, pane-like.
///   • DRASTICALLY tighter corridors (~2.4m — about three people shoulder to
///     shoulder, vs ~6m on other levels) so the player feels boxed in.
///   • Hidden point lights gridded just under the ceiling — no visible fixtures,
///     but bright everywhere so the mirrors read as bright reflective surfaces.
///   • A realtime, box-projected reflection probe so the mirrors reflect the
///     lit corridors and the moving player.
///   • SpawnPoint moved to the maze entrance.
///
/// Re-runnable: clears its own root, the hidden-light grid, and old torches.
/// Run via menu, or Level3MirrorMazeBuilder.Build() through the reflection trampoline.
/// </summary>
public static class Level3MirrorMazeBuilder
{
    const string PngPath   = "Assets/mazes/Level 3 png.png";
    const string ScenePath = "Assets/Scenes/Level3.unity";
    const string MirrorMatPath = "Assets/Materials/MirrorWall.mat";

    const int   GRID  = 13;      // 13x13 maze
    const float CELL  = 2.8f;    // TIGHT pitch -> ~2.4m corridors
    const float THICK = 0.4f;    // thin, pane-like mirror walls
    const float FALLBACK_WALL_HEIGHT = 5f;

    // PNG/SVG sampling: edges at 2 + 16*i, 16px cells. PNG is 210x210.
    const int SVG_MIN  = 2;
    const int SVG_STEP = 16;
    const int PNG_SIZE = 210;

    const string MazeRootName  = "Level3Maze";
    const string LightRootName = "HiddenLights";
    const string TorchRootName = "Torches";
    const string ProbeName     = "MirrorMaze_ReflectionProbe";

    [MenuItem("Tools/Overlords Bane/Build Level 3 Mirror Maze (PNG)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new StringBuilder();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { log.AppendLine("Cannot run in play mode."); return log.ToString(); }

        // ---------- 1) Decode the PNG into wall flags ----------
        Texture2D tex = LoadPng(PngPath, log);
        if (tex == null) return log.ToString();

        bool[,] vWall = new bool[GRID + 1, GRID]; // vertical wall on grid line i, row r
        bool[,] hWall = new bool[GRID, GRID + 1]; // horizontal wall on grid line j, column c

        for (int i = 0; i <= GRID; i++)
            for (int r = 0; r < GRID; r++)
                vWall[i, r] = SampleWall(tex, SVG_MIN + SVG_STEP * i,
                                              SVG_MIN + SVG_STEP * r + SVG_STEP / 2);
        for (int j = 0; j <= GRID; j++)
            for (int c = 0; c < GRID; c++)
                hWall[c, j] = SampleWall(tex, SVG_MIN + SVG_STEP * c + SVG_STEP / 2,
                                              SVG_MIN + SVG_STEP * j);
        Object.DestroyImmediate(tex);
        log.AppendLine("Decoded 13x13 maze from " + PngPath);

        // ---------- 2) Open scene, resolve placement ----------
        var scene = EditorSceneManager.GetActiveScene().path == ScenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Vector3 center = ResolveFloorCenter();
        float wallHeight = ResolveWallHeight(scene);
        log.AppendLine("Center " + center + ", wall height " + wallHeight + ".");

        // Flatten any leftover terrain so it doesn't poke through the tight floor.
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain != null)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            td.SetHeights(0, 0, new float[res, res]);
            log.AppendLine("Terrain flattened.");
        }

        // ---------- 3) Mirror material ----------
        Material mirror = MakeMirrorMaterial();

        // ---------- 4) Build thin mirror walls ----------
        float extent = GRID * CELL;
        Vector3 origin = new Vector3(center.x - extent / 2f, 0f, center.z - extent / 2f);

        var existing = GameObject.Find(MazeRootName);
        if (existing != null) Object.DestroyImmediate(existing);
        // Also nuke the older procedural mirror maze if it lingers.
        var legacy = GameObject.Find("MirrorMaze_13x13");
        if (legacy != null) Object.DestroyImmediate(legacy);

        var root = new GameObject(MazeRootName);
        int wallCount = 0;

        for (int i = 0; i <= GRID; i++)
            for (int r = 0; r < GRID; r++)
            {
                if (!vWall[i, r]) continue;
                float wx = origin.x + i * CELL;
                float wz = origin.z + (GRID - r - 0.5f) * CELL;
                CreateWall(root.transform, mirror, "Mirror_V",
                    new Vector3(wx, wallHeight / 2f, wz),
                    new Vector3(THICK, wallHeight, CELL + THICK));
                wallCount++;
            }
        for (int j = 0; j <= GRID; j++)
            for (int c = 0; c < GRID; c++)
            {
                if (!hWall[c, j]) continue;
                float wx = origin.x + (c + 0.5f) * CELL;
                float wz = origin.z + (GRID - j) * CELL;
                CreateWall(root.transform, mirror, "Mirror_H",
                    new Vector3(wx, wallHeight / 2f, wz),
                    new Vector3(CELL + THICK, wallHeight, THICK));
                wallCount++;
            }
        log.AppendLine("Mirror walls built: " + wallCount + " (thickness " + THICK +
                       ", corridor ~" + (CELL - THICK).ToString("0.0") + "m).");

        // ---------- 5) Hidden bright light grid ----------
        int lightCount = BuildHiddenLights(center, extent, wallHeight, log);

        // ---------- 6) Bright environment so the mirrors glow ----------
        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.74f); // bright cool grey
        RenderSettings.reflectionIntensity = 1f;
        log.AppendLine("Ambient brightened, fog off.");

        // ---------- 7) Realtime reflection probe ----------
        BuildReflectionProbe(center, extent, wallHeight, log);

        // ---------- 8) Spawn at entrance ----------
        MoveSpawnToEntrance(hWall, origin, log);

        // ---------- 9) Save ----------
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Lightmapping.BakeAsync();
        log.AppendLine("Saved " + scene.path + " — walls=" + wallCount +
                       " hiddenLights=" + lightCount + ".");
        return log.ToString();
    }

    // ---------------------------------------------------------------- PNG sampling

    static Texture2D LoadPng(string assetPath, StringBuilder log)
    {
        string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(abs)) { log.AppendLine("ERROR: PNG not found at " + abs); return null; }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(abs)))
        { log.AppendLine("ERROR: failed to decode PNG."); return null; }
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

    // ---------------------------------------------------------------- geometry / lights

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

    static int BuildHiddenLights(Vector3 center, float extent, float wallHeight, StringBuilder log)
    {
        // Remove old visible torches and any previous hidden-light grid.
        var oldTorches = GameObject.Find(TorchRootName);
        if (oldTorches != null) Object.DestroyImmediate(oldTorches);
        var oldLights = GameObject.Find(LightRootName);
        if (oldLights != null) Object.DestroyImmediate(oldLights);

        var lightRoot = new GameObject(LightRootName);

        // Point lights gridded just under the ceiling — out of the player's
        // forward sightline, so the corridors are evenly bright with no fixtures.
        float spacing = CELL * 3f;                 // ~8.4m apart
        float y = wallHeight - 0.4f;               // tucked under the ceiling
        float half = extent / 2f + spacing * 0.5f;

        int count = 0;
        for (float dx = -half; dx <= half; dx += spacing)
            for (float dz = -half; dz <= half; dz += spacing)
            {
                var go = new GameObject("HiddenLight_" + count.ToString("000"));
                go.transform.SetParent(lightRoot.transform, false);
                go.transform.position = new Vector3(center.x + dx, y, center.z + dz);

                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.95f, 0.97f, 1f);  // crisp cool white
                l.intensity = 3.2f;
                l.range = spacing * 2.2f;               // generous overlap = no dark spots
                l.shadows = LightShadows.None;          // even fill, cheaper
                count++;
            }
        log.AppendLine("Hidden lights placed: " + count + " (under ceiling, no fixtures).");
        return count;
    }

    static void BuildReflectionProbe(Vector3 center, float extent, float wallHeight, StringBuilder log)
    {
        var go = GameObject.Find(ProbeName);
        if (go == null) go = new GameObject(ProbeName);
        go.transform.position = center + Vector3.up * (wallHeight / 2f);

        var probe = go.GetComponent<ReflectionProbe>();
        if (probe == null) probe = go.AddComponent<ReflectionProbe>();
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = new Color(0.5f, 0.54f, 0.62f); // bright fallback, never black
        probe.renderDynamicObjects = true;     // reflect the player
        probe.boxProjection = true;
        probe.farClipPlane = 120f;
        probe.size = new Vector3(extent + 4f, wallHeight + 4f, extent + 4f);
        probe.resolution = 256;
        log.AppendLine("Reflection probe set (realtime, box projection, dynamic).");
    }

    // ---------------------------------------------------------------- helpers

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
        if (floor != null)
        {
            var p = floor.transform.position;
            return new Vector3(p.x, 0f, p.z);
        }
        return new Vector3(42.87f, 0f, 54.24f); // matches the other levels' floor
    }

    static float ResolveWallHeight(UnityEngine.SceneManagement.Scene scene)
    {
        var ceiling = GameObject.Find("Ceiling");
        if (ceiling == null)
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == "Ceiling" && go.scene == scene) { ceiling = go; break; }
        if (ceiling != null)
        {
            float y = ceiling.transform.position.y;
            if (y > 1f && y < 30f) return y;
        }
        return FALLBACK_WALL_HEIGHT;
    }

    static Material MakeMirrorMaterial()
    {
        EnsureFolder("Assets/Materials");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MirrorMatPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MirrorMatPath); }
        else mat.shader = shader;
        mat.SetColor("_BaseColor", new Color(0.784f, 0.831f, 0.902f)); // pale silver-blue
        mat.SetFloat("_Metallic", 0.95f);
        mat.SetFloat("_Smoothness", 0.97f);
        mat.SetFloat("_EnvironmentReflections", 1f);
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
