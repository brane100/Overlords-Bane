using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class BuildMainMenu
{
    // ---- Palette ----
    static readonly Color32 BG_VOID      = new Color32(10, 12, 20, 255);     // #0A0C14
    static readonly Color32 CREAM        = new Color32(255, 253, 246, 255);  // #FFFDF6
    static readonly Color32 VIOLET       = new Color32(139, 92, 246, 255);   // #8B5CF6
    static readonly Color32 VIOLET_HOVER = new Color32(180, 142, 250, 255);  // #B48EFA
    static readonly Color32 MUTED        = new Color32(176, 180, 200, 255);  // #B0B4C8
    static readonly Color32 DIVIDER_COL  = new Color32(39, 48, 84, 255);     // #273054
    static readonly Color32 BRACKET_COL  = new Color32(54, 64, 112, 180);    // #364070 a180
    static readonly Color32 VERSION_COL  = new Color32(54, 64, 112, 255);    // #364070

    static TMP_FontAsset _font;
    static Type _hoverType;
    static StringBuilder _log;

    public static string Execute()
    {
        _log = new StringBuilder();

        // User-script types live in Assembly-CSharp, which the dynamic compiler
        // does not reference, so resolve & use them by reflection.
        _hoverType = FindType("MenuButtonHover");
        var ctrlType = FindType("MainMenuController");
        _log.AppendLine("MenuButtonHover: " + (_hoverType != null ? "found" : "MISSING"));
        _log.AppendLine("MainMenuController: " + (ctrlType != null ? "found" : "MISSING"));

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Cinzel/Cinzel-Bold SDF.asset");
        if (_font == null) _font = TMP_Settings.defaultFontAsset;
        _log.AppendLine("Font: " + (_font != null ? _font.name : "<null>"));

        // ---- New empty scene ----
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- Main Camera (UI-only backdrop) ----
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BG_VOID;
        cam.orthographic = true;
        cam.cullingMask = 0; // render no 3D objects
        camGO.transform.position = new Vector3(0, 0, -10);

        // ---- Canvas ----
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var canvasRT = canvasGO.GetComponent<RectTransform>();

        // ---- EventSystem (Input System aware) ----
        var esGO = new GameObject("EventSystem", typeof(EventSystem));
        var ism = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (ism != null) esGO.AddComponent(ism);
        else esGO.AddComponent<StandaloneInputModule>();
        _log.AppendLine("Input module: " + (ism != null ? "InputSystemUIInputModule" : "StandaloneInputModule"));

        // ---- Background (cover-fit: fills screen, crops overflow, top-biased) ----
        var bgHolderGO = new GameObject("BackgroundHolder", typeof(RectTransform), typeof(RectMask2D));
        bgHolderGO.transform.SetParent(canvasRT, false);
        StretchFull(bgHolderGO.GetComponent<RectTransform>());

        var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenuBG.png");
        var bg = NewImage("Background", bgHolderGO.transform);
        SetAnchors(bg.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);
        bg.sprite = bgSprite;
        bg.type = Image.Type.Simple;
        bg.preserveAspect = false; // BackgroundCoverFit governs aspect; RectMask2D crops overflow
        bg.color = new Color32(255, 255, 255, 235);
        bg.raycastTarget = false;
        var cover = bg.gameObject.AddComponent<BackgroundCoverFit>();
        cover.aspectRatio = bgSprite != null ? bgSprite.rect.width / bgSprite.rect.height : 500f / 1024f;
        cover.verticalAlign = 1f;   // 1 = top -> apex/top glow under the title
        cover.horizontalAlign = 0.5f;

        // ---- ScrimTop ----
        var scrimTop = NewImage("ScrimTop", canvasRT);
        SetAnchors(scrimTop.rectTransform, 0, 1, 1, 1, 0.5f, 1f);
        scrimTop.rectTransform.sizeDelta = new Vector2(0, 220);
        scrimTop.rectTransform.anchoredPosition = Vector2.zero;
        scrimTop.color = new Color32(10, 12, 20, 140);
        scrimTop.raycastTarget = false;

        // ---- ScrimBottom ----
        var scrimBot = NewImage("ScrimBottom", canvasRT);
        SetAnchors(scrimBot.rectTransform, 0, 0, 1, 0, 0.5f, 0f);
        scrimBot.rectTransform.sizeDelta = new Vector2(0, 360);
        scrimBot.rectTransform.anchoredPosition = Vector2.zero;
        scrimBot.color = new Color32(10, 12, 20, 217);
        scrimBot.raycastTarget = false;

        // ---- Title block ----
        var titleBlock = NewRect("TitleBlock", canvasRT);
        SetAnchors(titleBlock, 0.5f, 1, 0.5f, 1, 0.5f, 1f);
        titleBlock.anchoredPosition = new Vector2(0, -70);
        titleBlock.sizeDelta = new Vector2(1200, 200);

        var title = NewText("GameTitle", titleBlock, "OVERLORDS' BANE", 76, CREAM, TextAlignmentOptions.Center);
        SetAnchors(title.rectTransform, 0.5f, 1, 0.5f, 1, 0.5f, 1f);
        title.rectTransform.anchoredPosition = Vector2.zero;
        title.rectTransform.sizeDelta = new Vector2(1200, 95);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 16;
        ApplyTitleUnderlay(title);

        var tagline = NewText("Tagline", titleBlock, "ASCEND OR BE CONSUMED", 19, VIOLET, TextAlignmentOptions.Center);
        SetAnchors(tagline.rectTransform, 0.5f, 1, 0.5f, 1, 0.5f, 1f);
        tagline.rectTransform.anchoredPosition = new Vector2(0, -116);
        tagline.rectTransform.sizeDelta = new Vector2(1200, 30);
        tagline.characterSpacing = 34;

        // ---- Nav menu ----
        var navGO = new GameObject("NavMenu", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        navGO.transform.SetParent(canvasRT, false);
        var navRT = navGO.GetComponent<RectTransform>();
        SetAnchors(navRT, 0.5f, 0, 0.5f, 0, 0.5f, 0f);
        navRT.anchoredPosition = new Vector2(0, 56);
        navRT.sizeDelta = new Vector2(260, 0);
        var vlg = navGO.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 0;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        var csf = navGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var btnBegin    = NewNavButton(navRT, "BtnBeginAscent", "BEGIN ASCENT", 26, VIOLET, 56, true,  true);
        var btnContinue = NewNavButton(navRT, "BtnContinue",    "CONTINUE",     21, MUTED,  50, false, true);
        var btnSettings = NewNavButton(navRT, "BtnSettings",    "SETTINGS",     21, MUTED,  50, false, true);
        var btnQuit     = NewNavButton(navRT, "BtnQuit",        "QUIT",         21, MUTED,  50, false, false);

        // ---- Corner brackets ----
        MakeCorner("CornerTL", canvasRT, 0, 1);
        MakeCorner("CornerTR", canvasRT, 1, 1);
        MakeCorner("CornerBL", canvasRT, 0, 0);
        MakeCorner("CornerBR", canvasRT, 1, 0);

        // ---- Version label ----
        var version = NewText("VersionLabel", canvasRT, "v0.1.0 — AXIOM BUILD", 14, VERSION_COL, TextAlignmentOptions.BottomRight);
        SetAnchors(version.rectTransform, 1, 0, 1, 0, 1f, 0f);
        version.rectTransform.anchoredPosition = new Vector2(-20, 16);
        version.rectTransform.sizeDelta = new Vector2(360, 24);
        version.characterSpacing = 14;
        version.raycastTarget = false;

        // ---- MenuManager + wiring (reflection: controller is in Assembly-CSharp) ----
        var mgrGO = new GameObject("MenuManager");
        if (ctrlType != null)
        {
            var ctrl = mgrGO.AddComponent(ctrlType);
            WireClick(btnBegin,    ctrl, ctrlType, "OnBeginAscent");
            WireClick(btnContinue, ctrl, ctrlType, "OnContinue");
            WireClick(btnSettings, ctrl, ctrlType, "OnSettings");
            WireClick(btnQuit,     ctrl, ctrlType, "OnQuit");
        }

        // ---- Save scene ----
        const string scenePath = "Assets/Scenes/MainMenu.unity";
        bool saved = EditorSceneManager.SaveScene(scene, scenePath);
        _log.AppendLine("Scene saved: " + saved + " -> " + scenePath);

        // ---- Build settings index 0 ----
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == scenePath);
        list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        _log.AppendLine("Added to Build Settings at index 0. Total scenes: " + list.Count);

        AssetDatabase.SaveAssets();
        var result = _log.ToString();
        Debug.Log("[BuildMainMenu]\n" + result);
        return result;
    }

    // ---------- helpers ----------

    static Type FindType(string name)
    {
        var t = Type.GetType(name + ", Assembly-CSharp");
        if (t != null) return t;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    static void WireClick(Button btn, object target, Type type, string method)
    {
        var mi = type.GetMethod(method);
        if (mi == null) { _log.AppendLine("WARN: method missing " + method); return; }
        var call = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, mi);
        UnityEventTools.AddPersistentListener(btn.onClick, call);
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetAnchors(RectTransform rt, float ax, float ay, float bx, float by, float px, float py)
    {
        rt.anchorMin = new Vector2(ax, ay);
        rt.anchorMax = new Vector2(bx, by);
        rt.pivot = new Vector2(px, py);
    }

    static void ApplyTitleUnderlay(TextMeshProUGUI title)
    {
        if (title.font == null) return;
        var baseMat = title.font.material;
        if (baseMat == null) return;
        var mat = new Material(baseMat) { name = "Cinzel Title Underlay" };
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetFloat("_UnderlayOffsetX", 0f);
        mat.SetFloat("_UnderlayOffsetY", -1f);
        mat.SetFloat("_UnderlayDilate", 0.1f);
        mat.SetFloat("_UnderlaySoftness", 0.5f);
        mat.SetColor("_UnderlayColor", new Color32(0, 0, 0, 230));
        const string matPath = "Assets/Art/Fonts/Cinzel/Cinzel Title Underlay.mat";
        AssetDatabase.CreateAsset(mat, matPath);
        title.fontSharedMaterial = mat;
    }

    static Button NewNavButton(Transform parent, string name, string labelText, float fontSize, Color labelColor, float height, bool primary, bool divider)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        SetAnchors(rt, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);
        rt.sizeDelta = new Vector2(260, height);

        var img = go.GetComponent<Image>();
        img.color = new Color(1, 1, 1, 0); // invisible but raycastable
        img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        // label
        var label = NewText("Label", rt, labelText, fontSize, labelColor, TextAlignmentOptions.Center);
        StretchFull(label.rectTransform);
        label.characterSpacing = 24;
        label.raycastTarget = false;

        // hover (MenuButtonHover lives in Assembly-CSharp -> reflection)
        if (_hoverType != null)
        {
            var hover = go.AddComponent(_hoverType);
            SetField(_hoverType, hover, "label", label);
            SetField(_hoverType, hover, "defaultColor", (Color)labelColor);
            SetField(_hoverType, hover, "hoverColor", primary ? (Color)VIOLET_HOVER : (Color)CREAM);
            SetField(_hoverType, hover, "duration", 0.15f);
        }

        // divider child at bottom edge
        if (divider)
        {
            var dv = NewImage("Divider", rt);
            SetAnchors(dv.rectTransform, 0.5f, 0, 0.5f, 0, 0.5f, 0.5f);
            dv.rectTransform.sizeDelta = new Vector2(140, 1);
            dv.rectTransform.anchoredPosition = Vector2.zero;
            dv.color = DIVIDER_COL;
            dv.raycastTarget = false;
        }

        return btn;
    }

    static void SetField(Type type, object obj, string field, object value)
    {
        var fi = type.GetField(field);
        if (fi != null) fi.SetValue(obj, value);
        else _log.AppendLine("WARN: field missing " + field);
    }

    static void MakeCorner(string name, Transform parent, int cx, int cy)
    {
        var rt = NewRect(name, parent);
        SetAnchors(rt, cx, cy, cx, cy, cx, cy);
        float insetX = cx == 0 ? 18 : -18;
        float insetY = cy == 0 ? 18 : -18;
        rt.anchoredPosition = new Vector2(insetX, insetY);
        rt.sizeDelta = new Vector2(20, 20);

        var h = NewImage("H", rt);
        SetAnchors(h.rectTransform, cx, cy, cx, cy, cx, cy);
        h.rectTransform.sizeDelta = new Vector2(20, 1);
        h.rectTransform.anchoredPosition = Vector2.zero;
        h.color = BRACKET_COL;
        h.raycastTarget = false;

        var v = NewImage("V", rt);
        SetAnchors(v.rectTransform, cx, cy, cx, cy, cx, cy);
        v.rectTransform.sizeDelta = new Vector2(1, 20);
        v.rectTransform.anchoredPosition = Vector2.zero;
        v.color = BRACKET_COL;
        v.raycastTarget = false;
    }
}
