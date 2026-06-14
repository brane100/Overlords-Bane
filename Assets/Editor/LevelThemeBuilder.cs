using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Idempotent builder for the five per-level <see cref="LevelTheme"/> assets and
/// for wiring a <see cref="LevelThemeApplier"/> into Level 1.
///
/// Each theme is keyed to its Overlord and built from thematic hex values derived
/// from the project palette. Re-running rewrites the assets in place (no dupes)
/// and re-wires the Level 1 applier without adding a second one.
/// </summary>
public static class LevelThemeBuilder
{
    const string ThemeFolder = "Assets/ScriptableObjects/Themes";

    struct ThemeDef
    {
        public string assetName;
        public string floor, wall, ceiling, ambient, fog, dirLight, wallTint, sigil;
        public float fogStart, fogEnd, lightIntensity;
    }

    // Per-level palettes. Level 1 is the canonical void/violet palette; the others
    // shift hue per Overlord while keeping the same dark, fog-heavy structure.
    static readonly ThemeDef[] Themes =
    {
        new ThemeDef { assetName = "Level1_Axiom",      floor="#0A0C14", wall="#273054", ceiling="#05060B", ambient="#1A1830", fog="#0A0C14", fogStart=6f,  fogEnd=45f, dirLight="#8B5CF6", lightIntensity=0.90f, wallTint="#2A1A4A", sigil="#8B5CF6" },
        new ThemeDef { assetName = "Level2_Virex",      floor="#0A140C", wall="#1A3A28", ceiling="#060C08", ambient="#16261A", fog="#0A140C", fogStart=5f,  fogEnd=40f, dirLight="#3BE08A", lightIntensity=0.85f, wallTint="#103A24", sigil="#4FFFA0" },
        new ThemeDef { assetName = "Level3_Noctyrr",    floor="#080C18", wall="#16284A", ceiling="#04060F", ambient="#101C30", fog="#060A14", fogStart=4f,  fogEnd=35f, dirLight="#4A7CFF", lightIntensity=0.80f, wallTint="#122A55", sigil="#6FA8FF" },
        new ThemeDef { assetName = "Level4_Kharros",    floor="#140806", wall="#3A1810", ceiling="#0C0604", ambient="#281410", fog="#140806", fogStart=4f,  fogEnd=32f, dirLight="#FF5A2A", lightIntensity=0.95f, wallTint="#4A1808", sigil="#FF7A3A" },
        new ThemeDef { assetName = "Level5_EidolonRex", floor="#14110A", wall="#3A3018", ceiling="#0C0A06", ambient="#2A2414", fog="#100D08", fogStart=8f,  fogEnd=55f, dirLight="#FFD86A", lightIntensity=1.10f, wallTint="#4A3A10", sigil="#FFE8A0" },
    };

    [MenuItem("Tools/Overlords Bane/Build Level Themes + Apply to Level 1")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new StringBuilder();

        EnsureFolder(ThemeFolder);

        foreach (var def in Themes)
            BuildTheme(def, log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireLevel1Applier(log);

        return log.ToString();
    }

    static void BuildTheme(ThemeDef def, StringBuilder log)
    {
        string path = ThemeFolder + "/" + def.assetName + ".asset";
        var theme = AssetDatabase.LoadAssetAtPath<LevelTheme>(path);
        bool created = false;
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<LevelTheme>();
            AssetDatabase.CreateAsset(theme, path);
            created = true;
        }

        theme.floorColor               = Hex(def.floor);
        theme.wallColor                = Hex(def.wall);
        theme.ceilingColor             = Hex(def.ceiling);
        theme.ambientColor             = Hex(def.ambient);
        theme.fogColor                 = Hex(def.fog);
        theme.fogStart                 = def.fogStart;
        theme.fogEnd                   = def.fogEnd;
        theme.directionalLightColor    = Hex(def.dirLight);
        theme.directionalLightIntensity= def.lightIntensity;
        theme.wallMaterialTint         = Hex(def.wallTint);
        theme.sigilAccentColor         = Hex(def.sigil);

        EditorUtility.SetDirty(theme);
        log.AppendLine((created ? "Created " : "Updated ") + path);
    }

    static void WireLevel1Applier(StringBuilder log)
    {
        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Level1.unity")
            EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);

        var applierGo = GameObject.Find("LevelThemeApplier");
        if (applierGo == null) applierGo = new GameObject("LevelThemeApplier");

        var applier = applierGo.GetComponent<LevelThemeApplier>();
        if (applier == null) applier = applierGo.AddComponent<LevelThemeApplier>();

        var theme = AssetDatabase.LoadAssetAtPath<LevelTheme>(ThemeFolder + "/Level1_Axiom.asset");

        var so = new SerializedObject(applier);
        so.FindProperty("theme").objectReferenceValue = theme;
        // floor/wall/ceiling renderers + light left null -> resolved by name at runtime.
        so.ApplyModifiedPropertiesWithoutUndo();

        var scene = applierGo.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine(theme != null
            ? "Wired LevelThemeApplier in Level1 with Level1_Axiom. Saved " + scene.path
            : "WARNING: Level1_Axiom asset not found; applier added but theme unassigned.");
    }

    static Color Hex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
        Debug.LogWarning("[LevelThemeBuilder] Bad hex: " + hex);
        return Color.magenta;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
