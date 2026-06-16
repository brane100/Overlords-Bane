using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Idempotent builder for Level 5 (EidolonRex — the collapsing apex). Reads the
/// "Level 5 png" maze (15x15), decodes it by sampling wall pixels, and builds:
///
///   • Thick warm-stone walls under "Level5Maze", centered on the floor.
///   • Gold torches embedded in the walls (EidolonRex regal gold).
///   • Falling debris (FallingDebris) over random corridor cells — heavy blocks
///     that shudder, then slam down from the ceiling for 35 crushing damage.
///   • A LevelThemeApplier wired to Level5_EidolonRex.
///   • SpawnPoint moved to the maze entrance.
///
/// Re-runnable. Run via menu or Level5MazeBuilder.Build() (reflection trampoline).
/// </summary>
public static class Level5MazeBuilder
{
    const string PngPath   = "Assets/mazes/Level 5 png.png";
    const string ScenePath = "Assets/Scenes/Level5.unity";
    const string ThemePath = "Assets/ScriptableObjects/Themes/Level5_EidolonRex.asset";

    const int   GRID  = 15;      // 15x15 maze
    const float CELL  = 8f;
    const float THICK = 2.0f;
    const float FALLBACK_WALL_HEIGHT = 5f;

    const int SVG_MIN  = 2;
    const int SVG_STEP = 16;
    const int PNG_SIZE = 242;

    static readonly Color Gold = new Color(1f, 0.847f, 0.416f); // EidolonRex light

    const string MazeRootName   = "Level5Maze";
    const string TorchRootName  = "Torches";
    const string DebrisRootName = "FallingDebris";

    [MenuItem("Tools/Overlords Bane/Build Level 5 Maze (EidolonRex collapse)")]
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
        log.AppendLine("Decoded 15x15 maze from " + PngPath);

        // ---------- 2) Open scene ----------
        var scene = EditorSceneManager.GetActiveScene().path == ScenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var theme = AssetDatabase.LoadAssetAtPath<LevelTheme>(ThemePath);
        Color wallColor = theme != null ? theme.wallColor : new Color(0.227f, 0.188f, 0.094f);
        Color accent    = theme != null ? theme.directionalLightColor : Gold;

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
        Material wallMat   = MakeLit("Assets/Materials/Lvl 5/Level5Wall.mat",   wallColor, 0.1f, 0.25f);
        Material debrisMat = MakeLit("Assets/Materials/Lvl 5/Level5Debris.mat", new Color(0.12f, 0.10f, 0.07f), 0.0f, 0.15f);

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

        // ---------- 5) Gold torches ----------
        int torchCount = BuildTorches(wallFaces, wallHeight, accent, log);

        // ---------- 6) Falling debris ----------
        int debrisCount = BuildDebris(origin, debrisMat, wallHeight, log);

        // ---------- 7) Theme ----------
        WireTheme(theme, log);

        // ---------- 8) Spawn ----------
        MoveSpawnToEntrance(hWall, origin, log);

        // ---------- 9) Save ----------
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path + " — walls=" + wallCount +
                       " torches=" + torchCount + " debris=" + debrisCount + ".");
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

        int target = Mathf.Min(28, faces.Count);
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
            l.intensity = 6.5f;
            l.range = 26f;
            l.spotAngle = 55f;
            l.shadows = LightShadows.Soft;
            count++;
        }
        log.AppendLine("Torches placed: " + count + " (gold).");
        return count;
    }

    static int BuildDebris(Vector3 origin, Material debrisMat, float wallHeight, StringBuilder log)
    {
        var debrisRoot = GameObject.Find(DebrisRootName);
        if (debrisRoot != null) Object.DestroyImmediate(debrisRoot);
        debrisRoot = new GameObject(DebrisRootName);

        var rng = new System.Random(505);
        var chosen = new HashSet<int>();
        int wanted = 18, guard = 0;
        while (chosen.Count < wanted && guard++ < 600)
        {
            int c = rng.Next(GRID);
            int r = rng.Next(1, GRID); // skip entrance row
            chosen.Add(r * GRID + c);
        }

        float restY = wallHeight - 1.0f; // hangs near the ceiling
        int count = 0;
        foreach (int key in chosen)
        {
            int r = key / GRID, c = key % GRID;
            float wx = origin.x + (c + 0.5f) * CELL;
            float wz = origin.z + (GRID - r - 0.5f) * CELL;

            var trap = new GameObject("FallingDebris_" + count.ToString("00"));
            trap.transform.SetParent(debrisRoot.transform, false);
            trap.transform.position = new Vector3(wx, 0f, wz);

            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Block";
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.transform.SetParent(trap.transform, false);
            block.transform.localScale = new Vector3(CELL * 0.55f, 1.2f, CELL * 0.55f);
            block.transform.localPosition = new Vector3(0f, restY, 0f); // rest near ceiling
            var mr = block.GetComponent<MeshRenderer>();
            mr.sharedMaterial = debrisMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            trap.AddComponent<FallingDebris>();
            count++;
        }
        log.AppendLine("Falling debris placed: " + count + " blocks (35 crush, telegraphed).");
        return count;
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
        log.AppendLine("LevelThemeApplier -> Level5_EidolonRex" + (theme != null ? "" : " (MISSING)") + ".");
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
