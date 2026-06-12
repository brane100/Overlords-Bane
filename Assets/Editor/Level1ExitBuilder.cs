using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Idempotent builder for the Level 1 exit + Overlord (Axiom) event:
///   - FadeOverlay (full-screen black CanvasGroup under LevelUI)
///   - OverlordEvent (ScreenEventController + AudioSource)
///   - ExitTrigger (BoxCollider trigger) at the hidden exit cell
/// Re-running repairs/rewires the elements it owns without duplicating them.
/// </summary>
public static class Level1ExitBuilder
{
    // Default exit location — a far edge of the maze, well away from the spawn (42.5, 1, 51.9).
    // Reposition the ExitTrigger in the Scene view to the actual hidden exit cell if needed.
    static readonly Vector3 ExitPosition = new Vector3(42.5f, 1.5f, 95f);
    static readonly Vector3 ExitSize     = new Vector3(4f, 3f, 4f);

    [MenuItem("Tools/Overlords Bane/Build Level 1 Exit + Overlord Event")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new StringBuilder();

        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Level1.unity")
            EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);

        var levelUI = GameObject.Find("LevelUI");
        if (levelUI == null) { log.AppendLine("ERROR: no LevelUI canvas in Level1"); return log.ToString(); }

        // 1. Full-screen black FadeOverlay (CanvasGroup) under LevelUI
        var fadeCG = BuildFadeOverlay(levelUI.transform, log);

        // 2. OverlordEvent: ScreenEventController + AudioSource
        var screenEvent = BuildOverlordEvent(fadeCG, log);

        // 3. ExitTrigger box at the hidden exit cell
        BuildExitTrigger(screenEvent, log);

        var scene = levelUI.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path);
        return log.ToString();
    }

    static CanvasGroup BuildFadeOverlay(Transform levelUI, StringBuilder log)
    {
        var go = FindChild(levelUI, "FadeOverlay");
        if (go == null)
        {
            go = new GameObject("FadeOverlay", typeof(RectTransform));
            go.transform.SetParent(levelUI, false);
        }
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = null;
        img.color = Color.black;          // opaque black; CanvasGroup.alpha drives visibility
        img.raycastTarget = true;

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // Keep the game-over overlay above the fade if it exists.
        var gameOver = FindChild(levelUI, "GameOverScreen");
        if (gameOver != null) gameOver.transform.SetAsLastSibling();

        log.AppendLine("FadeOverlay ready (full-screen black, alpha 0).");
        return cg;
    }

    static ScreenEventController BuildOverlordEvent(CanvasGroup fadeCG, StringBuilder log)
    {
        var go = GameObject.Find("OverlordEvent");
        if (go == null) go = new GameObject("OverlordEvent");

        var audio = go.GetComponent<AudioSource>();
        if (audio == null) audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;     // 2D — the Overlord speaks directly to the player
        audio.loop = false;

        var sec = go.GetComponent<ScreenEventController>();
        if (sec == null) sec = go.AddComponent<ScreenEventController>();

        var so = new SerializedObject(sec);
        so.FindProperty("fadeOverlay").objectReferenceValue = fadeCG;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("fadeTime").floatValue = 0.6f;
        // playerInput left null — resolved at runtime once PlayerArmature spawns
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine("OverlordEvent ready (ScreenEventController + AudioSource, fade wired).");
        return sec;
    }

    static void BuildExitTrigger(ScreenEventController screenEvent, StringBuilder log)
    {
        var go = GameObject.Find("ExitTrigger");
        if (go == null) go = new GameObject("ExitTrigger");
        go.transform.position = ExitPosition;

        var bc = go.GetComponent<BoxCollider>();
        if (bc == null) bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center = Vector3.zero;
        bc.size = ExitSize;

        var trigger = go.GetComponent<ExitTrigger>();
        if (trigger == null) trigger = go.AddComponent<ExitTrigger>();

        var timer = Object.FindFirstObjectByType<Timer>();

        var so = new SerializedObject(trigger);
        so.FindProperty("screenEvent").objectReferenceValue = screenEvent;
        if (timer != null) so.FindProperty("timer").objectReferenceValue = timer;
        // axiomClip left for the user to assign in the inspector
        if (so.FindProperty("bonusTime").floatValue == 0f)
            so.FindProperty("bonusTime").floatValue = 30f;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine("ExitTrigger ready at " + ExitPosition + " (BoxCollider trigger " + ExitSize +
                       "). >>> Assign the Axiom AudioClip in the inspector, and reposition to the real exit cell if needed.");
    }

    static GameObject FindChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        return t != null ? t.gameObject : null;
    }
}
