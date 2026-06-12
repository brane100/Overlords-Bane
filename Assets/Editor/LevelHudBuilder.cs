using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the in-level HUD (health bar + styled timer) and the game-over
/// overlay, and wires PlayerHealth / Timer / GameOverController.
/// Idempotent: re-running replaces the elements it owns.
/// </summary>
public static class LevelHudBuilder
{
    static readonly Color32 VOID         = new Color32(10, 12, 20, 255);
    static readonly Color32 CREAM        = new Color32(255, 253, 246, 255);
    static readonly Color32 VIOLET       = new Color32(139, 92, 246, 255);
    static readonly Color32 VIOLET_HOVER = new Color32(180, 142, 250, 255);
    static readonly Color32 MUTED        = new Color32(176, 180, 200, 255);
    static readonly Color32 DIVIDER_COL  = new Color32(39, 48, 84, 255);
    static readonly Color32 BRACKET_COL  = new Color32(54, 64, 112, 180);

    static TMP_FontAsset _font;
    static Sprite _uiSprite;
    static Material _underlay;
    static StringBuilder _log;

    [MenuItem("Tools/Overlords Bane/Build Level HUD (active scene)")]
    public static void MenuActive() => Debug.Log(BuildActive());

    [MenuItem("Tools/Overlords Bane/Build Level HUD (all levels)")]
    public static void MenuAll() => Debug.Log(BuildAll());

    public static string BuildAll()
    {
        var sb = new StringBuilder();
        string[] scenes =
        {
            "Assets/Scenes/Level1.unity", "Assets/Scenes/Level2.unity",
            "Assets/Scenes/Level3.unity", "Assets/Scenes/Level4.unity",
            "Assets/Scenes/Level5.unity",
        };
        foreach (var s in scenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(s) == null) { sb.AppendLine("SKIP (missing): " + s); continue; }
            EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
            sb.AppendLine("== " + s + " ==");
            sb.AppendLine(BuildActive());
        }
        return sb.ToString();
    }

    public static string BuildActive()
    {
        _log = new StringBuilder();
        Init();

        EnsureEventSystem();

        var levelUI = GameObject.Find("LevelUI");
        if (levelUI == null) { _log.AppendLine("ERROR: no LevelUI canvas in scene"); return _log.ToString(); }
        var canvasRT = levelUI.GetComponent<RectTransform>();
        var levelCanvas = levelUI.GetComponent<Canvas>();
        if (levelCanvas != null) levelCanvas.sortingOrder = 10;

        var playerHealth = SetupPlayerHealth();
        var timer = levelUI.GetComponent<Timer>();
        StyleTimer(levelUI.transform, canvasRT);
        var healthUI = BuildHealthBar(canvasRT, playerHealth);
        var (overlayCG, subtitle, btnRetry, btnMenu, btnQuit) = BuildGameOverOverlay(canvasRT);
        WireGameManager(overlayCG, subtitle, timer, playerHealth, btnRetry, btnMenu, btnQuit);

        var scene = levelUI.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        _log.AppendLine("Saved " + scene.path);
        return _log.ToString();
    }

    static void Init()
    {
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Cinzel/Cinzel-Bold SDF.asset");
        if (_font == null) _font = TMP_Settings.defaultFontAsset;
        _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _underlay = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fonts/Cinzel/Cinzel Title Underlay.mat");
    }

    // ---------------- Player health ----------------

    static PlayerHealth SetupPlayerHealth()
    {
        // Add PlayerHealth to the PlayerArmature prefab (the actual spawned player with camera).
        // HealthBarUI and GameOverController find it at runtime via FindFirstObjectByType.
        const string prefabPath = "Assets/Prefabs/PlayerArmature.prefab";
        var prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabContents != null)
        {
            var ph = prefabContents.GetComponent<PlayerHealth>();
            if (ph == null) ph = prefabContents.AddComponent<PlayerHealth>();
            var so = new SerializedObject(ph);
            // respawnPoint left null — PlayerHealth.Start() finds "SpawnPoint" by name at runtime
            so.FindProperty("outOfBoundsY").floatValue = -5f;
            so.FindProperty("fallDamage").floatValue = 25f;
            so.FindProperty("useHorizontalBounds").boolValue = false; // Y-bounds only; bounds are scene-specific
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabContents);
            _log.AppendLine("PlayerHealth added to PlayerArmature prefab (respawn auto-finds SpawnPoint at runtime).");
        }
        else
        {
            _log.AppendLine("WARN: PlayerArmature prefab not found at " + prefabPath);
        }

        // Remove stale PlayerHealth from in-scene Player placeholder (it has no camera/movement)
        var scenePlaceholder = GameObject.Find("Player");
        if (scenePlaceholder != null)
        {
            var oldPh = scenePlaceholder.GetComponent<PlayerHealth>();
            if (oldPh != null) { Object.DestroyImmediate(oldPh); _log.AppendLine("Removed stale PlayerHealth from scene Player placeholder."); }
        }

        // Return null — HealthBarUI/GameOverController will lazy-find via FindFirstObjectByType
        return null;
    }

    // ---------------- Timer styling ----------------

    static void StyleTimer(Transform levelUI, RectTransform canvasRT)
    {
        var tt = levelUI.Find("TimerText");
        if (tt != null)
        {
            var tmp = tt.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                if (_font != null) tmp.font = _font;
                tmp.fontSize = 52;
                tmp.color = CREAM;
                tmp.characterSpacing = 6;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                var rt = tmp.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -38);
                rt.sizeDelta = new Vector2(360, 70);
            }
        }

        // "TIME" caption above the clock
        var cap = GetOrCreateChild(canvasRT, "TimerCaption");
        var capTMP = EnsureTMP(cap);
        ConfigTMP(capTMP, "TIME", 16, VIOLET, TextAlignmentOptions.Center);
        capTMP.characterSpacing = 14;
        var crt = capTMP.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0, -16);
        crt.sizeDelta = new Vector2(220, 22);
        _log.AppendLine("Timer styled (Cinzel cream + caption).");
    }

    // ---------------- Health bar ----------------

    static HealthBarUI BuildHealthBar(RectTransform canvasRT, PlayerHealth ph)
    {
        var group = GetOrCreateChild(canvasRT, "HealthGroup");
        var grt = group.GetComponent<RectTransform>();
        grt.anchorMin = grt.anchorMax = new Vector2(0f, 1f);
        grt.pivot = new Vector2(0f, 1f);
        grt.anchoredPosition = new Vector2(28, -24);
        grt.sizeDelta = new Vector2(430, 64);

        // caption
        var capTMP = EnsureTMP(GetOrCreateChild(grt, "Caption"));
        ConfigTMP(capTMP, "VITALITY", 15, VIOLET, TextAlignmentOptions.Left);
        capTMP.characterSpacing = 8;
        Place(capTMP.rectTransform, 2, 0, 220, 20);

        // border
        var border = EnsureImage(GetOrCreateChild(grt, "BarBorder"));
        border.sprite = _uiSprite; border.type = Image.Type.Sliced; border.color = BRACKET_COL; border.raycastTarget = false;
        Place(border.rectTransform, 0, -24, 364, 24);

        // background
        var bg = EnsureImage(GetOrCreateChild(grt, "BarBG"));
        bg.sprite = _uiSprite; bg.type = Image.Type.Sliced; bg.color = new Color32(10, 12, 20, 210); bg.raycastTarget = false;
        Place(bg.rectTransform, 2, -26, 360, 20);

        // fill
        var fill = EnsureImage(GetOrCreateChild(grt, "HealthFill"));
        fill.sprite = _uiSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.color = VIOLET;
        fill.raycastTarget = false;
        Place(fill.rectTransform, 2, -26, 360, 20);

        // numeric value
        var val = EnsureTMP(GetOrCreateChild(grt, "HealthValue"));
        ConfigTMP(val, "100", 16, CREAM, TextAlignmentOptions.Left);
        Place(val.rectTransform, 372, -26, 56, 22);

        var ui = group.GetComponent<HealthBarUI>();
        if (ui == null) ui = group.AddComponent<HealthBarUI>();
        var so = new SerializedObject(ui);
        if (ph != null) so.FindProperty("playerHealth").objectReferenceValue = ph;
        so.FindProperty("fillImage").objectReferenceValue = fill;
        so.FindProperty("valueLabel").objectReferenceValue = val;
        so.ApplyModifiedPropertiesWithoutUndo();

        _log.AppendLine("Health bar built (top-left).");
        return ui;
    }

    // ---------------- Game over overlay ----------------

    static (CanvasGroup, TextMeshProUGUI, Button, Button, Button) BuildGameOverOverlay(RectTransform canvasRT)
    {
        var root = GetOrCreateChild(canvasRT, "GameOverScreen");
        root.transform.SetAsLastSibling();
        var rrt = root.GetComponent<RectTransform>();
        Stretch(rrt);
        var cg = root.GetComponent<CanvasGroup>();
        if (cg == null) cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

        // scrim
        var scrim = EnsureImage(GetOrCreateChild(rrt, "Scrim"));
        Stretch(scrim.rectTransform);
        scrim.color = new Color32(10, 12, 20, 235);
        scrim.sprite = null; scrim.type = Image.Type.Simple; scrim.raycastTarget = true;

        // title
        var title = EnsureTMP(GetOrCreateChild(rrt, "Title"));
        ConfigTMP(title, "CONSUMED", 96, CREAM, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 18;
        if (_underlay != null) title.fontSharedMaterial = _underlay;
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0, 150); trt.sizeDelta = new Vector2(1200, 160);

        // subtitle
        var sub = EnsureTMP(GetOrCreateChild(rrt, "Subtitle"));
        ConfigTMP(sub, "THE CLIMB HAS ENDED", 22, VIOLET, TextAlignmentOptions.Center);
        sub.characterSpacing = 20;
        var srt = sub.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f); srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0, 56); srt.sizeDelta = new Vector2(1100, 40);

        // buttons column
        var col = GetOrCreateChild(rrt, "Buttons");
        EnsureLayout(col);
        var colRT = col.GetComponent<RectTransform>();
        colRT.anchorMin = colRT.anchorMax = new Vector2(0.5f, 0.5f); colRT.pivot = new Vector2(0.5f, 0.5f);
        colRT.anchoredPosition = new Vector2(0, -150); colRT.sizeDelta = new Vector2(360, 0);

        var btnRetry = MakeButton(colRT, "BtnRetry", "RETRY", 26, VIOLET, 60, true, true);
        var btnMenu  = MakeButton(colRT, "BtnMainMenu", "MAIN MENU", 22, MUTED, 54, false, true);
        var btnQuit  = MakeButton(colRT, "BtnQuit", "QUIT", 22, MUTED, 54, false, false);

        _log.AppendLine("Game-over overlay built (CONSUMED + Retry/Menu/Quit).");
        return (cg, sub, btnRetry, btnMenu, btnQuit);
    }

    static void WireGameManager(CanvasGroup overlay, TextMeshProUGUI subtitle, Timer timer, PlayerHealth ph,
                                Button retry, Button menu, Button quit)
    {
        var gm = GameObject.Find("GameManager");
        if (gm == null) gm = new GameObject("GameManager");
        var ctrl = gm.GetComponent<GameOverController>();
        if (ctrl == null) ctrl = gm.AddComponent<GameOverController>();

        var so = new SerializedObject(ctrl);
        so.FindProperty("overlay").objectReferenceValue = overlay;
        so.FindProperty("subtitle").objectReferenceValue = subtitle;
        if (timer != null) so.FindProperty("timer").objectReferenceValue = timer;
        if (ph != null) so.FindProperty("playerHealth").objectReferenceValue = ph;
        so.FindProperty("retryButton").objectReferenceValue = retry;
        so.FindProperty("menuButton").objectReferenceValue = menu;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("mainMenuScene").stringValue = "MainMenu";
        so.ApplyModifiedPropertiesWithoutUndo();

        _log.AppendLine("GameManager wired (timer + health -> overlay, buttons).");
    }

    // ---------------- EventSystem ----------------

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            _log.AppendLine("EventSystem already present.");
            return;
        }
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();

        // Prefer InputSystemUIInputModule from Unity Input System package
        System.Type isiType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            isiType = asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (isiType != null) break;
        }
        if (isiType != null)
            go.AddComponent(isiType);
        else
            go.AddComponent<StandaloneInputModule>();

        _log.AppendLine("EventSystem created" + (isiType != null ? " (InputSystemUIInputModule)" : " (StandaloneInputModule)") + ".");
    }

    // ---------------- helpers ----------------

    static Button MakeButton(Transform parent, string name, string label, float size, Color labelColor, float height, bool primary, bool divider)
    {
        var go = GetOrCreateChild(parent, name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360, height);

        var img = EnsureImage(go);
        img.sprite = null; img.type = Image.Type.Simple; img.color = new Color(1, 1, 1, 0); img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;

        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = height; le.preferredHeight = height;

        var lbl = EnsureTMP(GetOrCreateChild(rt, "Label"));
        ConfigTMP(lbl, label, size, labelColor, TextAlignmentOptions.Center);
        lbl.characterSpacing = 22;
        lbl.raycastTarget = false;
        Stretch(lbl.rectTransform);

        var hover = go.GetComponent<MenuButtonHover>();
        if (hover == null) hover = go.AddComponent<MenuButtonHover>();
        hover.label = lbl;
        hover.defaultColor = labelColor;
        hover.hoverColor = primary ? (Color)VIOLET_HOVER : (Color)CREAM;
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
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        return img;
    }

    static TextMeshProUGUI EnsureTMP(GameObject go)
    {
        var t = go.GetComponent<TextMeshProUGUI>();
        if (t == null) t = go.AddComponent<TextMeshProUGUI>();
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
        var vlg = go.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 0;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        var csf = go.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
