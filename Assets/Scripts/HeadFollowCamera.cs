using UnityEngine;

/// <summary>
/// First-person camera pivot that follows the player's head BONE position for
/// realistic head-bob, while leaving rotation entirely to the look controller.
///
/// Rigidly parenting a camera to the animated Head bone makes it inherit the
/// head's animated ROTATION (idle sway, walk/turn bob), which fights mouse-look
/// and shows up as horizontal jitter when looking around. Instead this component
/// lives on a pivot OUTSIDE the skeleton and, in LateUpdate (after the Animator
/// has posed the bones), copies only the head's world POSITION. The result is
/// natural positional bob with a perfectly clean, jitter-free look rotation.
///
/// The eye offset is applied in the player BODY's space (not the head's), so the
/// head's animated rotation never leaks into the camera through the offset lever.
/// </summary>
public class HeadFollowCamera : MonoBehaviour
{
    [Tooltip("Animator that owns the humanoid head bone. Auto-found from parents if left empty.")]
    [SerializeField] Animator animator;

    [Tooltip("Eye offset from the head bone, applied in the body's space (stable, unaffected by head animation).")]
    [SerializeField] Vector3 eyeOffset = new Vector3(0f, 0.05f, 0f);

    [Tooltip("Optional smoothing of the bob (0 = follow the head exactly).")]
    [SerializeField] float positionSmoothing = 0f;

    Transform _head;
    Transform _body;
    Vector3 _vel;

    void Start()
    {
        if (animator == null) animator = GetComponentInParent<Animator>();
        if (animator != null)
        {
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _body = animator.transform;
        }
        if (_head == null)
            Debug.LogWarning("[HeadFollowCamera] No humanoid Head bone found; camera will not follow the head.");
    }

    void LateUpdate()
    {
        if (_head == null) return;

        Vector3 offset = _body != null ? _body.rotation * eyeOffset : eyeOffset;
        Vector3 target = _head.position + offset;

        transform.position = positionSmoothing > 0f
            ? Vector3.SmoothDamp(transform.position, target, ref _vel, positionSmoothing)
            : target;

        // Rotation is intentionally NOT touched here — the look controller owns it.
    }
}
