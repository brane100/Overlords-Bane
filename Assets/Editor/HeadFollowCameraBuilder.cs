using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets up the realistic-but-jitter-free first-person camera on
/// Assets/Prefabs/PlayerArmature.prefab:
///   • Keeps PlayerCameraRoot OUTSIDE the animated skeleton (so it never inherits
///     the head bone's animated rotation).
///   • Adds HeadFollowCamera so the pivot follows the head bone's POSITION for
///     realistic bob, with the look rotation left clean.
///
/// Idempotent. Run via menu or Build() (reflection trampoline).
/// </summary>
public static class HeadFollowCameraBuilder
{
    const string PrefabPath = "Assets/Prefabs/PlayerArmature.prefab";

    [MenuItem("Tools/Overlords Bane/Setup Head-Follow Camera (realistic, no jitter)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var pivot = FindDeep(root.transform, "PlayerCameraRoot");
            if (pivot == null) return "ERROR: PlayerCameraRoot not found in " + PrefabPath;

            // Decouple from the skeleton (rotation must come only from mouse-look).
            if (pivot.parent != root.transform)
                pivot.SetParent(root.transform, false);
            pivot.localRotation = Quaternion.identity;

            var hf = pivot.GetComponent<HeadFollowCamera>();
            if (hf == null) hf = pivot.gameObject.AddComponent<HeadFollowCamera>();

            var animator = root.GetComponent<Animator>();
            var so = new SerializedObject(hf);
            var animProp = so.FindProperty("animator");
            if (animator != null && animProp != null) animProp.objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "HeadFollowCamera ready on PlayerCameraRoot (animator " +
                   (animator != null ? "wired" : "auto-find at runtime") + "). Saved " + PrefabPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
