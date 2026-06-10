using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public class SetupTimerUI
{
    [MenuItem("Tools/Overlords Bane/Setup Timer UI")]
    public static void Run() => Debug.Log(Execute());

    public static object Execute()
    {
        // Canvas
        GameObject canvasGo = GameObject.Find("LevelUI");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("LevelUI");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create LevelUI");
        }

        var canvas = canvasGo.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            canvasGo.AddComponent<GraphicRaycaster>();

        // Timer text, top-center
        Transform textTr = canvasGo.transform.Find("TimerText");
        GameObject textGo;
        if (textTr == null)
        {
            textGo = new GameObject("TimerText");
            textGo.transform.SetParent(canvasGo.transform, false);
        }
        else textGo = textTr.gameObject;

        var rt = textGo.GetComponent<RectTransform>();
        if (rt == null) rt = textGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -24f);
        rt.sizeDelta = new Vector2(360f, 80f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "03:00";
        tmp.fontSize = 56f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.83f, 0.56f, 0.13f); // amber, matches torch palette
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;

        // Timer component on the canvas, wired to the text, 180 seconds
        var timer = canvasGo.GetComponent<Timer>();
        if (timer == null) timer = canvasGo.AddComponent<Timer>();
        var so = new SerializedObject(timer);
        so.FindProperty("timerText").objectReferenceValue = tmp;
        so.FindProperty("_remainingTime").floatValue = 180f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        EditorSceneManager.SaveScene(canvasGo.scene);
        return "Timer UI created: LevelUI/TimerText, 180s, scene saved (" + canvasGo.scene.path + ")";
    }
}
