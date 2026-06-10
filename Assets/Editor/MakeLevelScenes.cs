using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class MakeLevelScenes
{
    public static object Execute()
    {
        var log = new System.Text.StringBuilder();

        // 1) Restore the Ceiling (an editor helper left it inactive) and re-save Level 1.
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "Ceiling" && go.scene.IsValid() && go.scene.isLoaded)
            {
                if (!go.activeSelf) { go.SetActive(true); log.AppendLine("Re-activated Ceiling in " + go.scene.name); }
            }
        }
        var active = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);
        log.AppendLine("Saved source scene: " + active.path);

        // 2) Duplicate the source scene into 4 new level scenes.
        string source = active.path; // "Assets/Game.unity"
        string[] targets =
        {
            "Assets/Scenes/Level2.unity",
            "Assets/Scenes/Level3.unity",
            "Assets/Scenes/Level4.unity",
            "Assets/Scenes/Level5.unity",
        };

        var created = new List<string>();
        foreach (var dst in targets)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(dst) != null)
            {
                log.AppendLine("Skip (already exists): " + dst);
                created.Add(dst);
                continue;
            }
            if (AssetDatabase.CopyAsset(source, dst))
            {
                log.AppendLine("Created: " + dst);
                created.Add(dst);
            }
            else
            {
                log.AppendLine("FAILED to copy -> " + dst);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3) Add the new scenes to Build Settings (after the existing ones, no duplicates).
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var present = new HashSet<string>();
        foreach (var s in scenes) present.Add(s.path);
        foreach (var dst in created)
        {
            if (!present.Contains(dst))
            {
                scenes.Add(new EditorBuildSettingsScene(dst, true));
                present.Add(dst);
                log.AppendLine("Added to build settings: " + dst);
            }
        }
        EditorBuildSettings.scenes = scenes.ToArray();

        return log.ToString();
    }
}
