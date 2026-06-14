using UnityEngine;

/// <summary>
/// Per-level visual identity for Overlords' Bane. One asset per Overlord
/// (Axiom, Virex, Noctyrr, Kharros, Eidolon Rex). A <see cref="LevelThemeApplier"/>
/// reads this on scene start and drives RenderSettings (fog + ambient), the
/// scene directional light, and the floor/wall/ceiling shared materials.
/// </summary>
[CreateAssetMenu(fileName = "LevelTheme", menuName = "Overlords Bane/Level Theme", order = 0)]
public class LevelTheme : ScriptableObject
{
    [Header("Surface colors")]
    public Color floorColor = Color.gray;
    public Color wallColor = Color.gray;
    public Color ceilingColor = Color.gray;

    [Header("Environment lighting")]
    public Color ambientColor = new Color(0.2f, 0.2f, 0.25f);
    public Color fogColor = new Color(0.04f, 0.05f, 0.08f);
    public float fogStart = 6f;
    public float fogEnd = 45f;

    [Header("Directional light")]
    public Color directionalLightColor = Color.white;
    public float directionalLightIntensity = 1f;

    [Header("Accents")]
    [Tooltip("Emission tint applied to the wall material (if it has emission).")]
    public Color wallMaterialTint = Color.black;
    [Tooltip("Accent color used by sigils / the Axiom eyes for this level.")]
    public Color sigilAccentColor = new Color(0.545f, 0.361f, 0.965f); // #8B5CF6
}
