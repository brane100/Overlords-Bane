using UnityEngine;

/// <summary>
/// Applies a <see cref="LevelTheme"/> to the scene on Start: RenderSettings fog
/// (linear, color, start/end), ambient light, the scene directional light
/// (color + intensity), and tints the floor/wall/ceiling shared materials.
///
/// Idempotent: applying the same theme repeatedly produces the same result
/// (it sets absolute values, never accumulates). Call <see cref="Apply"/> from
/// the editor to preview without entering play mode.
/// </summary>
[ExecuteAlways]
public class LevelThemeApplier : MonoBehaviour
{
    [SerializeField] LevelTheme theme;

    [Header("Surfaces (optional — auto-found by name if left empty)")]
    [SerializeField] Renderer floorRenderer;
    [SerializeField] Renderer wallRenderer;
    [SerializeField] Renderer ceilingRenderer;

    [Header("Directional light (optional — auto-found if empty)")]
    [SerializeField] Light directionalLight;

    [Tooltip("Re-apply every frame in edit mode for live preview. Off by default.")]
    [SerializeField] bool continuousEditPreview = false;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        Apply();
    }

    void Update()
    {
        if (continuousEditPreview && !Application.isPlaying)
            Apply();
    }

    /// <summary>Applies the assigned theme. Safe to call repeatedly.</summary>
    public void Apply()
    {
        if (theme == null) return;

        ApplyFog();
        ApplyAmbient();
        ApplyDirectionalLight();

        TintMaterial(ResolveRenderer(ref floorRenderer, "Floor"), theme.floorColor, false, default);
        TintMaterial(ResolveRenderer(ref wallRenderer, "Wall"), theme.wallColor, true, theme.wallMaterialTint);
        TintMaterial(ResolveRenderer(ref ceilingRenderer, "Ceiling"), theme.ceilingColor, false, default);
    }

    void ApplyFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = theme.fogColor;
        RenderSettings.fogStartDistance = theme.fogStart;
        RenderSettings.fogEndDistance = theme.fogEnd;
    }

    void ApplyAmbient()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = theme.ambientColor;
    }

    void ApplyDirectionalLight()
    {
        var light = directionalLight;
        if (light == null) light = FindSceneDirectionalLight();
        if (light == null) return;
        directionalLight = light;
        light.color = theme.directionalLightColor;
        light.intensity = theme.directionalLightIntensity;
    }

    Light FindSceneDirectionalLight()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return l;
        return null;
    }

    // Resolve a renderer reference, caching the first GameObject whose name
    // contains the given keyword (case-insensitive).
    Renderer ResolveRenderer(ref Renderer cached, string nameContains)
    {
        if (cached != null) return cached;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r.gameObject.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cached = r;
                return r;
            }
        }
        return null;
    }

    void TintMaterial(Renderer r, Color color, bool applyEmission, Color emission)
    {
        if (r == null) return;
        var mat = r.sharedMaterial;
        if (mat == null) return;

        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
        else if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);

        if (applyEmission && mat.HasProperty(EmissionColorId) && emission.maxColorComponent > 0.001f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(EmissionColorId, emission);
        }
    }
}
