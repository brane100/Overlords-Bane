using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Idempotent builder for Level 2 (Virex):
///   - LevelThemeApplier assigned Level2_Virex
///   - FadeOverlay under LevelUI
///   - OverlordEvent (ScreenEventController + AudioSource)
///   - ExitTrigger at the far edge
/// </summary>
public static class Level2ExitBuilder
{
    const string ThemePath  = "Assets/ScriptableObjects/Themes/Level2_Virex.asset";
    static readonly Vector3 ExitPosition = new Vector3(42.5f, 1.5f, 95f);
    static readonly Vector3 ExitSize     = new Vector3(4f, 3f, 4f);

    [MenuItem("Tools/Overlords Bane/Build Level 2 Exit + Theme")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new StringBuilder();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { log.AppendLine("Cannot run in play mode."); return log.ToString(); }

        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Level2.unity")
            EditorSceneManager.OpenScene("Assets/Scenes/Level2.unity", OpenSceneMode.Single);

        var levelUI = GameObject.Find("LevelUI");
        if (levelUI == null) { log.AppendLine("ERROR: No LevelUI in Level2."); return log.ToString(); }

        var fadeCG   = BuildFadeOverlay(levelUI.transform, log);
        var secCtrl  = BuildOverlordEvent(fadeCG, log);
        BuildExitTrigger(secCtrl, log);
        BuildThemeApplier(log);

        var scene = levelUI.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path);
        return log.ToString();
    }

    static CanvasGroup BuildFadeOverlay(Transform levelUI, StringBuilder log)
    {
        var go = levelUI.Find("FadeOverlay")?.gameObject;
        if (go == null)
        {
            go = new GameObject("FadeOverlay", typeof(RectTransform));
            go.transform.SetParent(levelUI, false);
        }
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>(); if (img == null) img = go.AddComponent<Image>();
        img.color = Color.black; img.raycastTarget = true;

        var cg = go.GetComponent<CanvasGroup>(); if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

        // Keep GameOverScreen on top.
        var gameOver = levelUI.Find("GameOverScreen");
        if (gameOver != null) gameOver.SetAsLastSibling();

        log.AppendLine("FadeOverlay ready.");
        return cg;
    }

    static ScreenEventController BuildOverlordEvent(CanvasGroup fadeCG, StringBuilder log)
    {
        var go = GameObject.Find("OverlordEvent");
        if (go == null) go = new GameObject("OverlordEvent");

        var audio = go.GetComponent<AudioSource>();
        if (audio == null) audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 0f; audio.loop = false;

        var sec = go.GetComponent<ScreenEventController>();
        if (sec == null) sec = go.AddComponent<ScreenEventController>();

        var so = new SerializedObject(sec);
        so.FindProperty("fadeOverlay").objectReferenceValue = fadeCG;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("fadeTime").floatValue = 0.6f;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine("OverlordEvent (Virex) ready.");
        return sec;
    }

    static void BuildExitTrigger(ScreenEventController sec, StringBuilder log)
    {
        var go = GameObject.Find("ExitTrigger");
        if (go == null) go = new GameObject("ExitTrigger");
        go.transform.position = ExitPosition;

        var bc = go.GetComponent<BoxCollider>();
        if (bc == null) bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true; bc.center = Vector3.zero; bc.size = ExitSize;

        var trigger = go.GetComponent<ExitTrigger>();
        if (trigger == null) trigger = go.AddComponent<ExitTrigger>();

        var timer = Object.FindFirstObjectByType<Timer>();
        var so = new SerializedObject(trigger);
        so.FindProperty("screenEvent").objectReferenceValue = sec;
        if (timer != null) so.FindProperty("timer").objectReferenceValue = timer;
        if (so.FindProperty("bonusTime").floatValue == 0f) so.FindProperty("bonusTime").floatValue = 30f;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine("ExitTrigger ready at " + ExitPosition + ". Assign Virex AudioClip in inspector.");
    }

    static void BuildThemeApplier(StringBuilder log)
    {
        var go = GameObject.Find("LevelThemeApplier");
        if (go == null) go = new GameObject("LevelThemeApplier");
        var applier = go.GetComponent<LevelThemeApplier>();
        if (applier == null) applier = go.AddComponent<LevelThemeApplier>();
        var theme = AssetDatabase.LoadAssetAtPath<LevelTheme>(ThemePath);

        var so = new SerializedObject(applier);
        so.FindProperty("theme").objectReferenceValue = theme;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine("LevelThemeApplier -> Level2_Virex" + (theme != null ? "" : " (MISSING ASSET)") + ".");
    }
}
