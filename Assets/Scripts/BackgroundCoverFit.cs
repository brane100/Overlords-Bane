using UnityEngine;

/// <summary>
/// Sizes this RectTransform to fully cover its parent while preserving the
/// source aspect ratio (like CSS background-size: cover), then biases the
/// crop via <see cref="verticalAlign"/> / <see cref="horizontalAlign"/>.
/// 1 = show the top/right edge, 0.5 = center, 0 = show the bottom/left edge.
/// Runs in edit mode so the framing is visible without entering Play.
/// Put a RectMask2D on the parent to clip the overflow.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class BackgroundCoverFit : MonoBehaviour
{
    [Tooltip("Source aspect ratio (width / height).")]
    public float aspectRatio = 500f / 1024f;

    [Range(0f, 1f)]
    [Tooltip("Vertical crop bias. 1 = top, 0.5 = center, 0 = bottom.")]
    public float verticalAlign = 1f;

    [Range(0f, 1f)]
    [Tooltip("Horizontal crop bias. 1 = right, 0.5 = center, 0 = left.")]
    public float horizontalAlign = 0.5f;

    RectTransform _rt;
    RectTransform _parent;

    void OnEnable() { Cache(); Apply(); }
    void OnRectTransformDimensionsChange() { Apply(); }

#if UNITY_EDITOR
    void Update() { if (!Application.isPlaying) Apply(); }
#endif

    void Cache()
    {
        _rt = (RectTransform)transform;
        _parent = _rt.parent as RectTransform;
    }

    /// <summary>Recompute size and position. Cheap; only writes when values change.</summary>
    public void Apply()
    {
        if (_rt == null || _parent == null) Cache();
        if (_rt == null || _parent == null || aspectRatio <= 0f) return;

        Vector2 p = _parent.rect.size;
        if (p.x <= 0f || p.y <= 0f) return;

        float w, h;
        if (p.x / p.y > aspectRatio) { w = p.x; h = w / aspectRatio; } // parent wider -> fill width
        else { h = p.y; w = h * aspectRatio; }                        // parent taller -> fill height

        float freeX = w - p.x;
        float freeY = h - p.y;
        Vector2 pos = new Vector2((0.5f - horizontalAlign) * freeX, (0.5f - verticalAlign) * freeY);
        Vector2 size = new Vector2(w, h);

        var center = new Vector2(0.5f, 0.5f);
        if (_rt.anchorMin != center) _rt.anchorMin = center;
        if (_rt.anchorMax != center) _rt.anchorMax = center;
        if (_rt.pivot != center) _rt.pivot = center;
        if ((_rt.sizeDelta - size).sqrMagnitude > 0.01f) _rt.sizeDelta = size;
        if ((_rt.anchoredPosition - pos).sqrMagnitude > 0.01f) _rt.anchoredPosition = pos;
    }
}
