using UnityEngine;
using UnityEditor;

public class TopDownView
{
    public static void Execute()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        // Look straight down at floor center from above the ceiling.
        Vector3 center = new Vector3(42.87f, 1.5f, 54.24f);
        sv.pivot = center;
        sv.rotation = Quaternion.Euler(18f, 35f, 0f); // low grazing angle across the floor
        sv.size = 80f;
        sv.Repaint();
    }

    public static void HideCeiling()
    {
        GameObject c = GameObject.Find("Ceiling");
        if (c != null) c.SetActive(false);
    }

    public static void ShowCeiling()
    {
        // Find inactive too
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == "Ceiling" && go.scene.IsValid()) go.SetActive(true);
    }
}
