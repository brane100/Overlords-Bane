using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the animated victory ("YOU ESCAPED") screen in Level 5 only, styled to
/// match the existing HUD/GameOver UI (Cinzel font, void scrim), tinted with the
/// EidolonRex gold. Wires a WinScreenController that reveals it when the final
/// exit is reached. Idempotent; touches nothing outside the WinScreen subtree.
/// </summary>
public static class Level5WinScreenBuilder
{
    const string ScenePath = "Assets/Scenes/Level5.unity";

    static readonly Color32 VOID   = new Color32(12, 10, 8, 240);
    static readonly Color32 CREAM  = new Color32(255, 253, 246, 255);
    static readonly Color32 GOLD   = new Color32(255, 216, 106, 255);   // EidolonRex
    static readonly Color32 MUTED  = new Color32(196, 178, 140, 255);
    static readonly Color32 CREAM_HOVER = new Color32(255, 255, 255, 255);
    static readonly Color32 DIVIDER_COL = new Color32(84, 64, 39, 180);

    static TMP_FontAsset _font;
    static Sprite _uiSprite;
    static Material _underlay;

    [MenuItem("Tools/Overlords Bane/Build Level 5 Win Screen")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "Cannot run in play mode.";

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Cinzel/Cinzel-Bold SDF.asset");
        if (_font == null) _font = TMP_Settings.defaultFontAsset;
        _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _underlay = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fonts/Cinzel/Cinzel Title Underlay.mat");

        var scene = EditorSceneManager.GetActiveScene().path == ScenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var levelUI = GameObject.Find("LevelUI");
        if (levelUI == null) return "ERROR: no LevelUI canvas in Level5.";
        var canvasRT = (RectTransform)levelUI.transform;

        // ---- panel ----
        var root = GetOrCreateChild(canvasRT, "WinScreen");
        root.transform.SetAsLastSibling();
        var rrt = root.GetComponent<RectTransform>();
        Stretch(rrt);
        var cg = root.GetComponent<CanvasGroup>(); if (cg == null) cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

        // scrim
        var scrim = EnsureImage(GetOrCreateChild(rrt, "Scrim"));
        Stretch(scrim.rectTransform);
        scrim.color = VOID; scrim.sprite = null; scrim.type = Image.Type.Simple; scrim.raycastTarget = true;

        // title
        var title = EnsureTMP(GetOrCreateChild(rrt, "Title"));
        ConfigTMP(title, "YOU ESCAPED", 100, GOLD, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold; title.characterSpacing = 18;
        if (_underlay != null) title.fontSharedMaterial = _underlay;
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0, 150); trt.sizeDelta = new Vector2(1300, 170);

        // subtitle (own CanvasGroup so it can fade independently)
        var subGO = GetOrCreateChild(rrt, "Subtitle");
        var subCG = subGO.GetComponent<CanvasGroup>(); if (subCG == null) subCG = subGO.AddComponent<CanvasGroup>();
        subCG.alpha = 0f;
        var sub = EnsureTMP(subGO);
        ConfigTMP(sub, "THE OVERLORDS' BANE IS BROKEN", 24, MUTED, TextAlignmentOptions.Center);
        sub.characterSpacing = 18;
        var srt = sub.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f); srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0, 52); srt.sizeDelta = new Vector2(1200, 40);

        // buttons column (own CanvasGroup)
        var col = GetOrCreateChild(rrt, "Buttons");
        var colCG = col.GetComponent<CanvasGroup>(); if (colCG == null) colCG = col.AddComponent<CanvasGroup>();
        colCG.alpha = 0f;
        EnsureLayout(col);
        var colRT = col.GetComponent<RectTransform>();
        colRT.anchorMin = colRT.anchorMax = new Vector2(0.5f, 0.5f); colRT.pivot = new Vector2(0.5f, 0.5f);
        colRT.anchoredPosition = new Vector2(0, -150); colRT.sizeDelta = new Vector2(360, 0);

        var btnMenu = MakeButton(colRT, "BtnMainMenu", "MAIN MENU", 26, GOLD, 60, true, true);
        var btnQuit = MakeButton(colRT, "BtnQuit", "QUIT", 22, MUTED, 54, false, false);

        // ---- controller ----
        var ctrl = root.GetComponent<WinScreenController>();
        if (ctrl == null) ctrl = root.AddComponent<WinScreenController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("group").objectReferenceValue = cg;
        so.FindProperty("title").objectReferenceValue = trt;
        so.FindProperty("subtitle").objectReferenceValue = subCG;
        so.FindProperty("buttons").objectReferenceValue = colCG;
        so.FindProperty("menuButton").objectReferenceValue = btnMenu;
        so.FindProperty("quitButton").objectReferenceValue = btnQuit;
        so.FindProperty("mainMenuScene").stringValue = "MainMenu";
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Level 5 Win Screen built (YOU ESCAPED + Main Menu/Quit, animated) and wired. Saved " + scene.path;
    }

    // ---------------- helpers (mirrors LevelHudBuilder) ----------------

    static Button MakeButton(Transform parent, string name, string label, float size, Color labelColor, float height, bool primary, bool divider)
    {
        var go = GetOrCreateChild(parent, name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360, height);

        var img = EnsureImage(go);
        img.sprite = null; img.type = Image.Type.Simple; img.color = new Color(1, 1, 1, 0); img.raycastTarget = true;

        var btn = go.GetComponent<Button>(); if (btn == null) btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None; btn.targetGraphic = img;

        var le = go.GetComponent<LayoutElement>(); if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = height; le.preferredHeight = height;

        var lbl = EnsureTMP(GetOrCreateChild(rt, "Label"));
        ConfigTMP(lbl, label, size, labelColor, TextAlignmentOptions.Center);
        lbl.characterSpacing = 22; lbl.raycastTarget = false;
        Stretch(lbl.rectTransform);

        var hover = go.GetComponent<MenuButtonHover>(); if (hover == null) hover = go.AddComponent<MenuButtonHover>();
        hover.label = lbl; hover.defaultColor = labelColor;
        hover.hoverColor = primary ? (Color)CREAM_HOVER : (Color)CREAM;
        hover.duration = 0.15f;

        if (divider)
        {
            var dv = EnsureImage(GetOrCreateChild(rt, "Divider"));
            dv.sprite = null; dv.type = Image.Type.Simple; dv.color = DIVIDER_COL; dv.raycastTarget = false;
            var drt = dv.rectTransform;
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0f); drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(180, 1); drt.anchoredPosition = Vector2.zero;
        }
        return btn;
    }

    static GameObject GetOrCreateChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image EnsureImage(GameObject go)
    {
        var img = go.GetComponent<Image>(); if (img == null) img = go.AddComponent<Image>();
        return img;
    }

    static TextMeshProUGUI EnsureTMP(GameObject go)
    {
        var t = go.GetComponent<TextMeshProUGUI>(); if (t == null) t = go.AddComponent<TextMeshProUGUI>();
        return t;
    }

    static void ConfigTMP(TextMeshProUGUI t, string text, float size, Color color, TextAlignmentOptions align)
    {
        if (_font != null) t.font = _font;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.enableWordWrapping = false; t.raycastTarget = false;
    }

    static void EnsureLayout(GameObject go)
    {
        var vlg = go.GetComponent<VerticalLayoutGroup>(); if (vlg == null) vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter; vlg.spacing = 0;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        var csf = go.GetComponent<ContentSizeFitter>(); if (csf == null) csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
