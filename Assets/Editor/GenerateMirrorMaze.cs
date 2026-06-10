using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Text;

public class GenerateMirrorMaze
{
    const int SIZE = 13;
    const int SEED = 1303;
    const float CELL = 8f;
    const float WALL_THICKNESS = 0.5f;
    const float FALLBACK_WALL_HEIGHT = 5f;

    // Floor area of the level (from Level 1 lighting setup)
    static readonly Vector3 FloorCenter = new Vector3(42.87f, 0f, 54.24f);

    [MenuItem("Tools/Overlords Bane/Generate Mirror Maze (Level3)")]
    public static void Run() => Debug.Log(Execute());

    public static object Execute()
    {
        var log = new StringBuilder();

        // ---------- 1) Seeded DFS layout ----------
        // walls[x,z] = bitmask: 1=W (−x), 2=E (+x), 4=S (−z), 8=N (+z)
        int[,] walls = new int[SIZE, SIZE];
        for (int x = 0; x < SIZE; x++)
            for (int z = 0; z < SIZE; z++)
                walls[x, z] = 15;

        var rng = new System.Random(SEED);
        var visited = new bool[SIZE, SIZE];
        var stack = new Stack<Vector2Int>();
        var cur = new Vector2Int(0, 0);
        visited[0, 0] = true;
        stack.Push(cur);
        int carved = 0;

        int[] dx = { -1, 1, 0, 0 };
        int[] dz = { 0, 0, -1, 1 };
        int[] bit = { 1, 2, 4, 8 };
        int[] opp = { 2, 1, 8, 4 };

        while (stack.Count > 0)
        {
            cur = stack.Peek();
            var options = new List<int>();
            for (int d = 0; d < 4; d++)
            {
                int nx = cur.x + dx[d], nz = cur.y + dz[d];
                if (nx >= 0 && nx < SIZE && nz >= 0 && nz < SIZE && !visited[nx, nz])
                    options.Add(d);
            }
            if (options.Count == 0) { stack.Pop(); continue; }

            int dir = options[rng.Next(options.Count)];
            int cx = cur.x + dx[dir], cz = cur.y + dz[dir];
            walls[cur.x, cur.y] &= ~bit[dir];
            walls[cx, cz] &= ~opp[dir];
            visited[cx, cz] = true;
            stack.Push(new Vector2Int(cx, cz));
            carved++;
        }

        // Entrance on south edge, exit on north edge (deterministic from seed)
        int entranceX = rng.Next(SIZE);
        int exitX = rng.Next(SIZE);
        walls[entranceX, 0] &= ~4;          // open S wall of entrance cell
        walls[exitX, SIZE - 1] &= ~8;       // open N wall of exit cell
        log.AppendLine($"Layout: {carved} passages carved, entrance at x={entranceX} (south), exit at x={exitX} (north)");

        // ---------- 2) Mirror material ----------
        const string matPath = "Assets/Materials/MirrorWall.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetColor("_BaseColor", new Color(0.784f, 0.831f, 0.902f)); // pale silver-blue #C8D4E6
        mat.SetFloat("_Metallic", 0.95f);
        mat.SetFloat("_Smoothness", 0.97f);
        mat.SetFloat("_EnvironmentReflections", 1f);
        EditorUtility.SetDirty(mat);
        log.AppendLine("Material: " + matPath);

        // ---------- 3) Open Level3 ----------
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level3.unity", OpenSceneMode.Single);

        // wall height from Ceiling
        float wallHeight = FALLBACK_WALL_HEIGHT;
        var ceiling = GameObject.Find("Ceiling");
        if (ceiling == null)
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == "Ceiling" && go.scene == scene) { ceiling = go; break; }
        if (ceiling != null)
        {
            float ceilY = ceiling.transform.position.y;
            if (ceilY > 1f && ceilY < 30f) wallHeight = ceilY;
        }
        log.AppendLine($"Wall height: {wallHeight}");

        // ---------- 4) Flatten the terrain-stamped maze ----------
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain != null)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            var flat = new float[res, res];
            td.SetHeights(0, 0, flat);
            log.AppendLine("Terrain flattened (" + res + "x" + res + ")");
        }
        else log.AppendLine("WARNING: no Terrain found to flatten");

        // ---------- 5) Build maze geometry ----------
        var existing = GameObject.Find("MirrorMaze_13x13");
        if (existing != null) Object.DestroyImmediate(existing);

        var root = new GameObject("MirrorMaze_13x13");
        float extent = SIZE * CELL;
        Vector3 origin = new Vector3(FloorCenter.x - extent / 2f, 0f, FloorCenter.z - extent / 2f);
        root.transform.position = origin;

        int wallCount = 0;
        // Horizontal walls (run along x): south edges z=0..SIZE, vertical walls similar.
        // Build south walls of every cell, plus the north wall of the top row.
        for (int z = 0; z <= SIZE; z++)
        {
            for (int x = 0; x < SIZE; x++)
            {
                bool present = z < SIZE ? (walls[x, z] & 4) != 0 : (walls[x, SIZE - 1] & 8) != 0;
                if (!present) continue;
                CreateWall(root.transform, mat,
                    new Vector3((x + 0.5f) * CELL, wallHeight / 2f, z * CELL),
                    new Vector3(CELL + WALL_THICKNESS, wallHeight, WALL_THICKNESS)); // overlap seals corners
                wallCount++;
            }
        }
        // Vertical walls (run along z): west edges x=0..SIZE
        for (int x = 0; x <= SIZE; x++)
        {
            for (int z = 0; z < SIZE; z++)
            {
                bool present = x < SIZE ? (walls[x, z] & 1) != 0 : (walls[SIZE - 1, z] & 2) != 0;
                if (!present) continue;
                CreateWall(root.transform, mat,
                    new Vector3(x * CELL, wallHeight / 2f, (z + 0.5f) * CELL),
                    new Vector3(WALL_THICKNESS, wallHeight, CELL + WALL_THICKNESS));
                wallCount++;
            }
        }
        log.AppendLine($"Walls built: {wallCount}");

        // ---------- 6) Save prefab, keep instance connected ----------
        const string prefabPath = "Assets/Prefabs/MirrorMaze_13x13.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
        log.AppendLine("Prefab: " + prefabPath);

        // ---------- 7) SpawnPoint at entrance ----------
        var spawn = GameObject.Find("SpawnPoint");
        if (spawn != null)
        {
            spawn.transform.position = origin + new Vector3((entranceX + 0.5f) * CELL, 1f, 0.5f * CELL);
            log.AppendLine("SpawnPoint moved to entrance cell");
        }

        // ---------- 8) Silver/blue mood ----------
        var coldBlue = new Color(0.624f, 0.722f, 0.910f); // #9FB8E8
        int retinted = 0;
        foreach (var lt in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (lt.type == LightType.Point && lt.transform.parent != null && lt.transform.parent.name == "Torches")
            { lt.color = coldBlue; retinted++; }
        }
        RenderSettings.ambientLight = new Color(0.42f, 0.48f, 0.60f) * 0.5f; // steel blue, 50%
        var dirLight = GameObject.Find("Directional Light");
        if (dirLight != null && dirLight.TryGetComponent(out Light dl)) dl.color = coldBlue;
        log.AppendLine($"Re-tinted {retinted} torch lights + ambient + directional to silver/blue");

        // ---------- 9) Realtime reflection probe ----------
        // Realtime (not baked): baked probes need a lighting bake to exist and never
        // include dynamic objects, so mirrors fell back to the bright skybox and the
        // player was never reflected.
        var probeGo = GameObject.Find("MirrorMaze_ReflectionProbe");
        if (probeGo == null) probeGo = new GameObject("MirrorMaze_ReflectionProbe");
        probeGo.transform.position = FloorCenter + Vector3.up * (wallHeight / 2f);
        var probe = probeGo.GetComponent<ReflectionProbe>();
        if (probe == null) probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 0f); // never reflect the sky
        probe.renderDynamicObjects = true; // include the player in reflections
        probe.farClipPlane = 150f;
        probe.boxProjection = true;
        probe.size = new Vector3(112f, wallHeight + 4f, 112f);
        probe.resolution = 256;
        log.AppendLine("Reflection probe placed (realtime, box projection, dynamic objects)");

        // ---------- 10) Save ----------
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Scene saved: " + scene.path);

        // kick a lighting/probe bake asynchronously
        Lightmapping.BakeAsync();
        log.AppendLine("Reflection probe bake started (async)");

        return log.ToString();
    }

    static void CreateWall(Transform parent, Material mat, Vector3 localPos, Vector3 size)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "MirrorWall";
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPos;
        wall.transform.localScale = size;
        wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        wall.isStatic = true;
    }
}
