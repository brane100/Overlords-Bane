using UnityEditor;

public static class ListBuildScenes
{
    public static string Execute()
    {
        var sb = new System.Text.StringBuilder();
        var scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
            sb.AppendLine($"[{i}] {(scenes[i].enabled ? "ON " : "OFF")} {scenes[i].path}");
        return sb.ToString();
    }
}
