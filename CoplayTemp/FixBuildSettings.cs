using System.Collections.Generic;
using UnityEditor;

public static class FixBuildSettings
{
    public static string Execute()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int fixed_ = 0;
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == "Assets/Game.unity")
            {
                scenes[i] = new EditorBuildSettingsScene("Assets/Level1.unity", scenes[i].enabled);
                fixed_++;
            }
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        return "Fixed " + fixed_ + " entry/entries. Total scenes: " + scenes.Count;
    }
}
