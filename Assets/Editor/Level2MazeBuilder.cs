using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Idempotent builder for Level 2: reads the "Level 2 png" maze image from
/// Assets/mazes, decodes its 12x12 orthogonal layout by sampling wall pixels,
/// and constructs the level in Level2.unity:
///
///   • Thick stone walls (cubes) under a "Level2Maze" root, centered on the floor.
///   • Wall-mounted torches (orange spotlights, matching the existing level style),
///     embedded in wall faces and shining into the corridors.
///   • Floor spikes (FloorSpike) scattered on random corridor cells — they pop up
///     at random and deal 30 damage.
///   • SpawnPoint moved to the maze entrance.
///
/// Re-runnable: each run clears the previously built root, torches, and spikes.
/// Run via menu, or Level2MazeBuilder.Build() through the reflection trampoline.
/// </summary>
public static class Level2MazeBuilder
{
    const string PngPath   = "Assets/mazes/Level 2 png.png";
    const string ScenePath = "Assets/Scenes/Level2.unity";

    const int   GRID  = 12;      // 12x12 maze
    const float CELL  = 8f;      // corridor pitch (world units per cell)
    const float THICK = 2.0f;    // thick walls
    const float FALLBACK_WALL_HEIGHT = 5f;

    // SVG/PNG sampling: edges at 2 + 16*i, cells 16px. PNG is 194x194.
    const int   SVG_MIN  = 2;
    const int   SVG_STEP = 16;
    const int   PNG_SIZE = 194;

    static readonly Vector3 FloorCenter = new Vector3(42.87f, 0f, 54.24f);
    static readonly Color TorchColor = new Color(0.83137f, 0.56470f, 0.12549f, 1f); // matches existing torches

    const string MazeRootName  = "Level2Maze";
    const string TorchRootName = "Torches";
    const string SpikeRootName = "FloorSpikes";

    [MenuItem("Tools/Overlords Bane/Build Level 2 Maze (PNG + spikes)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new StringBuilder();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { log.AppendLine("Cannot run in play mode."); return log.ToString(); }

        // ---------- 1) Decode the PNG into wall flags ----------
        // vWall[i, r] : vertical wall on grid line i (0..GRID), row r (0..GRID-1)
        // hWall[c, j] : horizontal wall on grid line j (0..GRID), column c (0..GRID-1)
        Texture2D tex = LoadPng(PngPath, log);
        if (tex == null) return log.ToString();

        bool[,] vWall = new bool[GRID + 1, GRID];
        bool[,] hWall = new bool[GRID, GRID + 1];

        for (int i = 0; i <= GRID; i++)
            for (int r = 0; r < GRID; r++)
                vWall[i, r] = SampleWall(tex, SVG_MIN + SVG_STEP * i,
                                              SVG_MIN + SVG_STEP * r + SVG_STEP / 2);

        for (int j = 0; j <= GRID; j++)
            for (int c = 0; c < GRID; c++)
                hWall[c, j] = SampleWall(tex, SVG_MIN + SVG_STEP * c + SVG_STEP / 2,
                                              SVG_MIN + SVG_STEP * j);

        Object.DestroyImmediate(tex);
        log.AppendLine("Decoded 12x12 maze from " + PngPath);

        // ---------- 2) Open scene ----------
        var scene = EditorSceneManager.GetActiveScene().path == ScenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        float wallHeight = ResolveWallHeight(scene);
        log.AppendLine("Wall height: " + wallHeight);

        // Flatten any leftover terrain-stamped maze so it doesn't fight the new walls.
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain != null)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            td.SetHeights(0, 0, new float[res, res]);
            log.AppendLine("Terrain flattened.");
        }

        // ---------- 3) Materials ----------
        Material wallMat  = MakeMaterial("Assets/Materials/Lvl 2/Level2Wall.mat",  new Color(0.22f, 0.23f, 0.27f), 0.15f, 0.1f);
        Material spikeMat = MakeMaterial("Assets/Materials/Lvl 2/Level2Spike.mat", new Color(0.55f, 0.57f, 0.62f), 0.9f, 0.6f);

        // ---------- 4) Build walls ----------
        float extent = GRID * CELL;
        Vector3 origin = new Vector3(FloorCenter.x - extent / 2f, 0f, FloorCenter.z - extent / 2f);

        var existing = GameObject.Find(MazeRootName);
        if (existing != null) Object.DestroyImmediate(existing);
        var root = new GameObject(MazeRootName);

        // Track corridor cells for spike placement; collect wall faces for torches.
        var wallFaces = new List<(Vector3 pos, Vector3 corridorDir)>();
        int wallCount = 0;

        // Vertical walls (run along Z): grid line i, row r.
        for (int i = 0; i <= GRID; i++)
        {
            for (int r = 0; r < GRID; r++)
            {
                if (!vWall[i, r]) continue;
                // Row r (0 = top) maps to high Z so the maze reads north-up.
                float wx = origin.x + i * CELL;
                float wz = origin.z + (GRID - r - 0.5f) * CELL;
                CreateWall(root.transform, wallMat, "Wall_V",
                    new Vector3(wx, wallHeight / 2f, wz),
                    new Vector3(THICK, wallHeight, CELL + THICK));
                wallCount++;

                // Corridor side: interior cells exist on both sides; pick the in-grid one.
                Vector3 dir = (i == 0) ? Vector3.right : (i == GRID ? Vector3.left : Vector3.right);
                wallFaces.Add((new Vector3(wx, wallHeight, wz), dir));
            }
        }

        // Horizontal walls (run along X): grid line j, column c.
        for (int j = 0; j <= GRID; j++)
        {
            for (int c = 0; c < GRID; c++)
            {
                if (!hWall[c, j]) continue;
                float wx = origin.x + (c + 0.5f) * CELL;
                float wz = origin.z + (GRID - j) * CELL;
                CreateWall(root.transform, wallMat, "Wall_H",
                    new Vector3(wx, wallHeight / 2f, wz),
                    new Vector3(CELL + THICK, wallHeight, THICK));
                wallCount++;

                Vector3 dir = (j == 0) ? Vector3.back : (j == GRID ? Vector3.forward : Vector3.back);
                wallFaces.Add((new Vector3(wx, wallHeight, wz), dir));
            }
        }
        log.AppendLine("Walls built: " + wallCount + " (thickness " + THICK + ").");

        // ---------- 5) Torches embedded in walls ----------
        int torchCount = BuildTorches(wallFaces, wallHeight, log);

        // ---------- 6) Floor spikes on random corridor cells ----------
        int spikeCount = BuildSpikes(origin, spikeMat, log);

        // ---------- 7) Move spawn to entrance (top-edge opening) ----------
        MoveSpawnToEntrance(vWall, hWall, origin, log);

        // ---------- 8) Save ----------
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path + " — walls=" + wallCount +
                       " torches=" + torchCount + " spikes=" + spikeCount + ".");
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

    /// <summary>True if the maze image is dark (a wall line) at SVG coordinate (sx, sy).</summary>
    static bool SampleWall(Texture2D tex, int sx, int sy)
    {
        // SVG x increases right; texture x increases right.  SVG y increases down;
        // texture y increases up, so flip: ty = size - sy.
        int tx = Mathf.Clamp(sx - 1, 0, tex.width - 1);
        int ty = Mathf.Clamp(PNG_SIZE - sy, 0, tex.height - 1);

        // Sample a small neighbourhood; any dark pixel = wall (robust to 2px lines / AA).
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

    static int BuildTorches(List<(Vector3 pos, Vector3 corridorDir)> faces, float wallHeight, StringBuilder log)
    {
        var torchRoot = GameObject.Find(TorchRootName);
        if (torchRoot == null) torchRoot = new GameObject(TorchRootName);
        // Clear previous torches.
        for (int i = torchRoot.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(torchRoot.transform.GetChild(i).gameObject);

        if (faces.Count == 0) return 0;

        // Spread ~24 torches evenly across the available wall faces.
        int target = Mathf.Min(24, faces.Count);
        int stride = Mathf.Max(1, faces.Count / target);
        float mountY = wallHeight * 0.65f;

        int count = 0;
        for (int idx = 0; idx < faces.Count && count < target; idx += stride)
        {
            var (pos, dir) = faces[idx];
            var go = new GameObject("Torch_" + count.ToString("00"));
            go.transform.SetParent(torchRoot.transform, false);
            // Sit on the corridor-facing surface of the wall.
            go.transform.position = new Vector3(pos.x, mountY, pos.z) + dir * (THICK * 0.5f + 0.2f);
            // Aim into the corridor and slightly downward.
            Vector3 aim = (dir + Vector3.down * 0.4f).normalized;
            go.transform.rotation = Quaternion.LookRotation(aim, Vector3.up);

            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = TorchColor;
            light.intensity = 7f;
            light.range = 26f;
            light.spotAngle = 50f;
            light.shadows = LightShadows.Soft;
            count++;
        }
        log.AppendLine("Torches placed: " + count + " (wall-mounted spotlights).");
        return count;
    }

    static int BuildSpikes(Vector3 origin, Material spikeMat, StringBuilder log)
    {
        var spikeRoot = GameObject.Find(SpikeRootName);
        if (spikeRoot != null) Object.DestroyImmediate(spikeRoot);
        spikeRoot = new GameObject(SpikeRootName);

        Mesh coneMesh = BuildConeMesh(0.55f, 2.0f, 10);

        var rng = new System.Random(2025);
        // Pick ~14 distinct corridor cells, skip the entrance row (top) so the
        // player isn't speared on spawn.
        var chosen = new HashSet<int>();
        int wanted = 14;
        int guard = 0;
        while (chosen.Count < wanted && guard++ < 500)
        {
            int c = rng.Next(GRID);
            int r = rng.Next(1, GRID); // skip row 0 (entrance edge)
            chosen.Add(r * GRID + c);
        }

        int count = 0;
        foreach (int key in chosen)
        {
            int r = key / GRID, c = key % GRID;
            float wx = origin.x + (c + 0.5f) * CELL;
            float wz = origin.z + (GRID - r - 0.5f) * CELL;

            var trap = new GameObject("FloorSpike_" + count.ToString("00"));
            trap.transform.SetParent(spikeRoot.transform, false);
            trap.transform.position = new Vector3(wx, 0f, wz);

            // Moving cluster — starts retracted below the floor.
            var spikes = new GameObject("Spikes");
            spikes.transform.SetParent(trap.transform, false);
            spikes.transform.localPosition = new Vector3(0f, -2.3f, 0f);

            // 4 cones in a 2x2 so it reads as a spike bed.
            float o = 1.4f;
            Vector3[] offs = {
                new Vector3(-o, 0, -o), new Vector3(o, 0, -o),
                new Vector3(-o, 0,  o), new Vector3(o, 0,  o)
            };
            foreach (var off in offs)
            {
                var cone = new GameObject("Spike");
                cone.transform.SetParent(spikes.transform, false);
                cone.transform.localPosition = off;
                cone.AddComponent<MeshFilter>().sharedMesh = coneMesh;
                cone.AddComponent<MeshRenderer>().sharedMaterial = spikeMat;
            }

            trap.AddComponent<FloorSpike>();
            count++;
        }
        log.AppendLine("Floor spikes placed: " + count + " traps (30 dmg, random timing).");
        return count;
    }

    static Mesh BuildConeMesh(float radius, float height, int seg)
    {
        var mesh = new Mesh { name = "SpikeCone" };
        var verts = new Vector3[seg + 2];
        verts[0] = Vector3.zero;                  // base centre
        verts[seg + 1] = new Vector3(0f, height, 0f); // apex
        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }
        var tris = new List<int>();
        for (int i = 0; i < seg; i++)
        {
            int next = (i + 1) % seg;
            tris.Add(seg + 1); tris.Add(i + 1); tris.Add(next + 1); // side
            tris.Add(0);       tris.Add(next + 1); tris.Add(i + 1); // base
        }
        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------- helpers

    static void MoveSpawnToEntrance(bool[,] vWall, bool[,] hWall, Vector3 origin, StringBuilder log)
    {
        var spawn = GameObject.Find("SpawnPoint");
        if (spawn == null) return;

        // Entrance = a column on the top edge (row 0) whose north wall is open.
        for (int c = 0; c < GRID; c++)
        {
            if (!hWall[c, 0])
            {
                float wx = origin.x + (c + 0.5f) * CELL;
                float wz = origin.z + (GRID - 0 - 0.5f) * CELL;
                spawn.transform.position = new Vector3(wx, 1f, wz);
                log.AppendLine("SpawnPoint moved to entrance column " + c + ".");
                return;
            }
        }
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

    static Material MakeMaterial(string path, Color baseColor, float metallic, float smoothness)
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
