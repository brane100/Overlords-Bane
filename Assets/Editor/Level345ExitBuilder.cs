using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds exit triggers to Levels 3, 4 and 5 in the exact style of Levels 1–2:
///   - FadeOverlay (CanvasGroup) under LevelUI
///   - OverlordEvent (ScreenEventController + AudioSource)
///   - ExitTrigger (BoxCollider trigger + ExitTrigger) at the maze's real exit cell
///
/// Does NOT touch each level's theme: L3 keeps its bespoke mirror-maze atmosphere,
/// and L4/L5 already carry their LevelThemeApplier from their maze builders.
/// The exit cell is found by decoding each level's PNG (open column on the south
/// edge), so the trigger always sits in a reachable corridor.
///
/// Run via menu or Level345ExitBuilder.BuildAll() (reflection trampoline).
/// Assign each level's Overlord AudioClip on its ExitTrigger afterwards.
/// </summary>
public static class Level345ExitBuilder
{
    const int SVG_MIN = 2, SVG_STEP = 16;

    [MenuItem("Tools/Overlords Bane/Build Level 3-5 Exit Triggers")]
    public static void Menu() => Debug.Log(BuildAll());

    public static string BuildAll()
    {
        var log = new StringBuilder();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { log.AppendLine("Cannot run in play mode."); return log.ToString(); }

        log.Append(BuildForScene("Assets/Scenes/Level3.unity", "Assets/mazes/Level 3 png.png", 210, 13, 2.8f, "L3 Noctyrr"));
        log.Append(BuildForScene("Assets/Scenes/Level4.unity", "Assets/mazes/Level 4 png.png", 226, 14, 8f,   "L4 Kharros"));
        log.Append(BuildForScene("Assets/Scenes/Level5.unity", "Assets/mazes/Level 5 png.png", 242, 15, 8f,   "L5 EidolonRex"));
        return log.ToString();
    }

    static string BuildForScene(string scenePath, string pngPath, int pngSize, int grid, float cell, string label)
    {
        var log = new StringBuilder();

        var scene = EditorSceneManager.GetActiveScene().path == scenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var levelUI = GameObject.Find("LevelUI");
        if (levelUI == null) { log.AppendLine(label + ": ERROR no LevelUI — skipped."); return log.ToString(); }

        var fadeCG  = BuildFadeOverlay(levelUI.transform);
        var secCtrl = BuildOverlordEvent(fadeCG);
        Vector3 exitPos = ResolveExitCell(pngPath, pngSize, grid, cell);
        BuildExitTrigger(secCtrl, exitPos, cell);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine(label + ": exit at " + exitPos + " — saved. Assign Overlord AudioClip on ExitTrigger.");
        return log.ToString();
    }

    // ---------------------------------------------------------------- pieces (mirror of Level2ExitBuilder)

    static CanvasGroup BuildFadeOverlay(Transform levelUI)
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

        var gameOver = levelUI.Find("GameOverScreen");
        if (gameOver != null) gameOver.SetAsLastSibling();
        return cg;
    }

    static ScreenEventController BuildOverlordEvent(CanvasGroup fadeCG)
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
        return sec;
    }

    static void BuildExitTrigger(ScreenEventController sec, Vector3 pos, float cell)
    {
        var go = GameObject.Find("ExitTrigger");
        if (go == null) go = new GameObject("ExitTrigger");
        go.transform.position = pos;

        var bc = go.GetComponent<BoxCollider>();
        if (bc == null) bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true; bc.center = Vector3.zero;
        bc.size = new Vector3(cell * 0.8f, 3f, cell * 0.8f);

        var trigger = go.GetComponent<ExitTrigger>();
        if (trigger == null) trigger = go.AddComponent<ExitTrigger>();

        var timer = Object.FindFirstObjectByType<Timer>();
        var so = new SerializedObject(trigger);
        so.FindProperty("screenEvent").objectReferenceValue = sec;
        if (timer != null) so.FindProperty("timer").objectReferenceValue = timer;
        if (so.FindProperty("bonusTime").floatValue == 0f) so.FindProperty("bonusTime").floatValue = 30f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- exit-cell from PNG

    static Vector3 ResolveExitCell(string pngPath, int pngSize, int grid, float cell)
    {
        Vector3 center = ResolveFloorCenter();
        float extent = grid * cell;
        Vector3 origin = new Vector3(center.x - extent / 2f, 0f, center.z - extent / 2f);

        // Default to bottom-centre in case decode fails.
        int exitCol = grid / 2;

        var tex = LoadPng(pngPath);
        if (tex != null)
        {
            for (int c = 0; c < grid; c++)
            {
                // South border (grid line j = grid) open at column c -> the exit gap.
                bool wall = SampleWall(tex, pngSize, SVG_MIN + SVG_STEP * c + SVG_STEP / 2, SVG_MIN + SVG_STEP * grid);
                if (!wall) { exitCol = c; break; }
            }
            Object.DestroyImmediate(tex);
        }

        float wx = origin.x + (exitCol + 0.5f) * cell;
        float wz = origin.z + 0.5f * cell; // bottom-row cell centre, just inside the south edge
        return new Vector3(wx, 1.5f, wz);
    }

    static Vector3 ResolveFloorCenter()
    {
        var floor = GameObject.Find("Floor");
        if (floor != null) { var p = floor.transform.position; return new Vector3(p.x, 0f, p.z); }
        return new Vector3(42.87f, 0f, 54.24f);
    }

    static Texture2D LoadPng(string assetPath)
    {
        string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(abs)) return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        return tex.LoadImage(File.ReadAllBytes(abs)) ? tex : null;
    }

    static bool SampleWall(Texture2D tex, int pngSize, int sx, int sy)
    {
        int tx = Mathf.Clamp(sx - 1, 0, tex.width - 1);
        int ty = Mathf.Clamp(pngSize - sy, 0, tex.height - 1);
        float darkest = 1f;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int px = Mathf.Clamp(tx + dx, 0, tex.width - 1);
                int py = Mathf.Clamp(ty + dy, 0, tex.height - 1);
                Color cc = tex.GetPixel(px, py);
                float g = (cc.r + cc.g + cc.b) / 3f;
                if (g < darkest) darkest = g;
            }
        return darkest < 0.5f;
    }
}
