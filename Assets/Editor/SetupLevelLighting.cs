using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

// Level 1 — The Initiation Labyrinth [Axiom]
// Cold stone + violet authority, amber torch breadcrumbs spread evenly across the maze floor.
public class SetupLevelLighting
{
    // --- Level 1 palette ---
    static readonly Color AMBIENT_VIOLET = Hex("#8B5CF6"); // faint sigil ambient
    static readonly Color TORCH_AMBER    = Hex("#D49020"); // torch breadcrumbs
    static readonly Color EXIT_VIOLET    = Hex("#6042C8"); // sigil / exit glow

    // Floor bounds (from Floor mesh): x[-12.13..97.87], z[-0.76..109.24], ceiling y=4.82
    const float X_MIN = -12.13f, X_MAX = 97.87f;
    const float Z_MIN =  -0.76f, Z_MAX = 109.24f;
    const float TORCH_Y = 3.4f;   // upper third, under the 4.82 ceiling
    const int   GRID    = 5;      // 5x5 = 25 evenly spread torches

    public static void Execute()
    {
        CleanupOld();

        // --- Ambient: faint violet authority (flat, dimmed) ---
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = AMBIENT_VIOLET * 0.5f; // faint violet, no pure-black gaps
        RenderSettings.ambientIntensity = 1f;

        // --- Directional light: subtle violet fill from above ---
        GameObject dirGO = GameObject.Find("Directional Light");
        if (dirGO != null)
        {
            Light dir = dirGO.GetComponent<Light>();
            if (dir != null)
            {
                dir.color     = AMBIENT_VIOLET;
                dir.intensity = 0.30f;     // faint — the maze is enclosed
                dir.shadows   = LightShadows.Soft;
            }
        }

        // --- Torch grid: amber breadcrumbs spread evenly across the whole floor ---
        GameObject torchRoot = new GameObject("Torches");
        float xStep = (X_MAX - X_MIN) / GRID;
        float zStep = (Z_MAX - Z_MIN) / GRID;
        int idx = 0;
        for (int ix = 0; ix < GRID; ix++)
        {
            for (int iz = 0; iz < GRID; iz++)
            {
                // cell-centered positions => even coverage, margin from edges
                float x = X_MIN + xStep * (ix + 0.5f);
                float z = Z_MIN + zStep * (iz + 0.5f);
                MakePoint($"Torch_{idx:00}", new Vector3(x, TORCH_Y, z),
                          TORCH_AMBER, intensity: 7f, range: 26f, parent: torchRoot.transform);
                idx++;
            }
        }

        // --- Central sigil / exit glow accent (floor center) ---
        Vector3 center = new Vector3((X_MIN + X_MAX) * 0.5f, 0.6f, (Z_MIN + Z_MAX) * 0.5f);
        MakePoint("Sigil_Glow", center, EXIT_VIOLET, intensity: 3.0f, range: 34f, parent: null);

        // --- Thin violet fog for depth ---
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = AMBIENT_VIOLET * 0.18f;
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance   = 80f;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[LevelLighting] {GRID * GRID} amber torches spread across floor + violet ambient applied.");
    }

    static void CleanupOld()
    {
        // Remove the previous mis-placed corner torches + any prior Torches parent.
        string[] stale = { "Torch_Left", "Torch_Right", "Torch_Back_Left", "Torch_Back_Right",
                           "Torches", "Sigil_Glow" };
        foreach (string n in stale)
        {
            GameObject go = GameObject.Find(n);
            while (go != null) { Object.DestroyImmediate(go); go = GameObject.Find(n); }
        }
    }

    static void MakePoint(string name, Vector3 pos, Color color, float intensity, float range, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        if (parent != null) go.transform.SetParent(parent, true);
        Light l = go.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = color;
        l.intensity = intensity;
        l.range     = range;
        l.shadows   = LightShadows.Soft;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
