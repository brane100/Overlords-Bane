using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Groups each level's geometry under a single "LevelRoot" and attaches a
/// <see cref="LevelInstability"/> component so the whole structure tilts and
/// shakes as one rigid piece, ramping in intensity from Level 1 to Level 5.
///
/// Reparenting uses worldPositionStays = true, so every existing world-space
/// relationship is preserved — no layout, geometry, lighting or trigger position
/// changes. Only structural objects are grouped; managers, UI, network, theme,
/// enemy spawners and the player are deliberately left as scene roots.
///
/// Idempotent. Run via menu or LevelInstabilityBuilder.BuildAll() (trampoline).
/// </summary>
public static class LevelInstabilityBuilder
{
    // Objects that make up the rigid structure and should sway together.
    static readonly string[] StructuralNames =
    {
        "Floor", "Ceiling", "Terrain", "Torches", "SparseLights",
        "Level1Maze", "Level2Maze", "Level3Maze", "Level4Maze", "Level5Maze",
        "MirrorMaze",
        "FloorSpikes", "FloorSigils", "FallingDebris",
        "ExitTrigger", "Sigil_Glow", "SpawnPoint",
    };

    static readonly (string scene, float intensity)[] Levels =
    {
        ("Assets/Scenes/Level1.unity", 0.05f),
        ("Assets/Scenes/Level2.unity", 0.10f),
        ("Assets/Scenes/Level3.unity", 0.175f),
        ("Assets/Scenes/Level4.unity", 0.25f),
        ("Assets/Scenes/Level5.unity", 0.30f),
    };

    [MenuItem("Tools/Overlords Bane/Build Level Instability (tilt + shake)")]
    public static void Menu() => Debug.Log(BuildAll());

    public static string BuildAll()
    {
        var log = new StringBuilder();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { log.AppendLine("Cannot run in play mode."); return log.ToString(); }

        foreach (var (scene, intensity) in Levels)
            log.Append(BuildForScene(scene, intensity));

        return log.ToString();
    }

    static string BuildForScene(string scenePath, float intensity)
    {
        var log = new StringBuilder();

        var scene = EditorSceneManager.GetActiveScene().path == scenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        string label = System.IO.Path.GetFileNameWithoutExtension(scenePath);

        // LevelRoot sits at the level centre so rotation pivots about the middle.
        Vector3 center = ResolveFloorCenter();
        var rootGO = GameObject.Find("LevelRoot");
        if (rootGO == null) rootGO = new GameObject("LevelRoot");
        rootGO.transform.position = center;
        rootGO.transform.rotation = Quaternion.identity;
        rootGO.transform.localScale = Vector3.one;

        int reparented = 0;
        foreach (var name in StructuralNames)
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            if (go == rootGO) continue;
            if (go.transform.IsChildOf(rootGO.transform)) continue;
            // Only top-level objects; keep their world transform.
            go.transform.SetParent(rootGO.transform, true);
            reparented++;
        }

        var inst = rootGO.GetComponent<LevelInstability>();
        if (inst == null) inst = rootGO.AddComponent<LevelInstability>();

        var so = new SerializedObject(inst);
        so.FindProperty("intensity").floatValue = intensity;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine(label + ": LevelRoot at " + center + ", " + reparented +
                       " objects grouped (" + rootGO.transform.childCount +
                       " total children), intensity " + intensity + " — saved.");
        return log.ToString();
    }

    static Vector3 ResolveFloorCenter()
    {
        var floor = GameObject.Find("Floor");
        if (floor != null) { var p = floor.transform.position; return new Vector3(p.x, 0f, p.z); }
        return new Vector3(42.87f, 0f, 54.24f);
    }
}
