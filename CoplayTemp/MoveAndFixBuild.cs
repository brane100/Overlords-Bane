using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MoveAndFixBuild
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        // Move Assets/Level1.unity -> Assets/Scenes/Level1.unity
        string error = AssetDatabase.MoveAsset("Assets/Level1.unity", "Assets/Scenes/Level1.unity");
        if (string.IsNullOrEmpty(error))
            sb.AppendLine("Moved Level1.unity to Assets/Scenes/");
        else
            sb.AppendLine("Move error: " + error);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Fix build settings:
        //  - replace Assets/Level1.unity with Assets/Scenes/Level1.unity
        //  - remove stale Assets/Scenes/Game.unity (broken ref from the rename)
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var result = new List<EditorBuildSettingsScene>();
        var seen = new HashSet<string>();

        foreach (var s in scenes)
        {
            string path = s.path;

            if (path == "Assets/Level1.unity")
            {
                path = "Assets/Scenes/Level1.unity";
                sb.AppendLine("Updated path: Assets/Level1.unity -> Assets/Scenes/Level1.unity");
            }

            // Skip stale Scenes/Game.unity if it no longer exists as an asset
            if (path == "Assets/Scenes/Game.unity")
            {
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (asset == null)
                {
                    sb.AppendLine("Removed stale entry: " + path);
                    continue;
                }
            }

            if (!seen.Contains(path))
            {
                result.Add(new EditorBuildSettingsScene(path, s.enabled));
                seen.Add(path);
            }
        }

        EditorBuildSettings.scenes = result.ToArray();
        AssetDatabase.SaveAssets();

        sb.AppendLine("\nFinal build order:");
        for (int i = 0; i < result.Count; i++)
            sb.AppendLine($"  [{i}] {result[i].path}");

        return sb.ToString();
    }
}
