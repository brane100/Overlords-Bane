using UnityEngine;
using UnityEditor;

public class MazeTopView
{
    // Straight-down view of the whole maze floor, ceiling hidden.
    public static void Execute()
    {
        GameObject c = GameObject.Find("Ceiling");
        if (c != null) c.SetActive(false);

        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        sv.in2DMode = false;
        sv.orthographic = true;
        sv.pivot = new Vector3(42.87f, 0f, 54.24f);
        sv.rotation = Quaternion.Euler(90f, 0f, 0f);
        sv.size = 70f;
        sv.Repaint();
    }

    public static void Restore()
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == "Ceiling" && go.scene.IsValid()) go.SetActive(true);
    }
}
