using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

public class SetupLevelLighting
{
    public static void Execute()
    {
        // --- Ambient: rich violet, visible ---
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.07f, 0.40f, 1f);
        RenderSettings.ambientIntensity = 1.5f;

        // --- Directional Light: vivid violet, much brighter ---
        GameObject dirLightGO = GameObject.Find("Directional Light");
        if (dirLightGO != null)
        {
            Light dirLight = dirLightGO.GetComponent<Light>();
            if (dirLight != null)
            {
                dirLight.color     = new Color(0.60f, 0.30f, 1.0f, 1f);
                dirLight.intensity = 1.2f;
                dirLight.shadows   = LightShadows.Soft;
            }
        }

        // --- Central sigil glow: intense violet point light at floor center ---
        SetupPointLight("Sigil_Glow", new Vector3(0f, 0.3f, 0f),
            new Color(0.55f, 0.0f, 1.0f, 1f), intensity: 4.0f, range: 14f);

        // --- Torch lights: warm amber, bright ---
        SetupPointLight("Torch_Left",       new Vector3(-6f, 1.8f,  4f), new Color(1.0f, 0.52f, 0.08f, 1f), 3.5f, 9f);
        SetupPointLight("Torch_Right",      new Vector3( 6f, 1.8f,  4f), new Color(1.0f, 0.52f, 0.08f, 1f), 3.5f, 9f);
        SetupPointLight("Torch_Back_Left",  new Vector3(-6f, 1.8f, -4f), new Color(1.0f, 0.52f, 0.08f, 1f), 3.5f, 9f);
        SetupPointLight("Torch_Back_Right", new Vector3( 6f, 1.8f, -4f), new Color(1.0f, 0.52f, 0.08f, 1f), 3.5f, 9f);

        // --- Thin violet fog ---
        RenderSettings.fog          = true;
        RenderSettings.fogMode      = FogMode.Linear;
        RenderSettings.fogColor     = new Color(0.12f, 0.04f, 0.25f, 1f);
        RenderSettings.fogStartDistance = 12f;
        RenderSettings.fogEndDistance   = 50f;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[LevelLighting] Done.");
    }

    static void SetupPointLight(string name, Vector3 pos, Color color, float intensity, float range)
    {
        GameObject existing = GameObject.Find(name);
        GameObject go = existing != null ? existing : new GameObject(name);
        go.transform.position = pos;
        Light l = go.GetComponent<Light>();
        if (l == null) l = go.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = color;
        l.intensity = intensity;
        l.range     = range;
        l.shadows   = LightShadows.Soft;
    }
}
