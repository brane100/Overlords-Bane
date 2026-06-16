using UnityEditor;
using UnityEngine;

/// <summary>
/// Fixes the "view lags/distorts when looking left-right" bug.
///
/// In Assets/Prefabs/PlayerArmature.prefab the first-person camera pivot
/// (PlayerCameraRoot) was parented INSIDE the animated Head bone
/// (Skeleton/.../Neck/Head/PlayerCameraRoot). The Animator drives the Head bone
/// every LateUpdate, so the camera inherited all head animation (idle sway,
/// walk/turn head-bob) on top of the mouse-look the controller applies — which
/// reads as a laggy, swimming distortion when turning.
///
/// This reparents PlayerCameraRoot to the prefab root at eye height, decoupling
/// it from the skeleton so only mouse-look + body motion drive it. The
/// ThirdPersonController keeps working: it references the pivot by object and
/// only sets its rotation, which is parent-independent.
///
/// Idempotent (no-op if already a root child). Run via menu or Build() trampoline.
/// </summary>
public static class FirstPersonCameraFixBuilder
{
    const string PrefabPath = "Assets/Prefabs/PlayerArmature.prefab";
    static readonly Vector3 EyeLocalPos = new Vector3(0f, 1.45f, 0f);

    [MenuItem("Tools/Overlords Bane/Fix First-Person Camera (decouple from head bone)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var pivot = FindDeep(root.transform, "PlayerCameraRoot");
            if (pivot == null) return "ERROR: PlayerCameraRoot not found in " + PrefabPath;

            if (pivot.parent == root.transform)
                return "PlayerCameraRoot already a root child — no change.";

            string oldParent = pivot.parent != null ? pivot.parent.name : "(none)";
            pivot.SetParent(root.transform, false);
            pivot.localPosition = EyeLocalPos;
            pivot.localRotation = Quaternion.identity;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "PlayerCameraRoot reparented from '" + oldParent + "' to prefab root at " +
                   EyeLocalPos + " — saved " + PrefabPath;
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
