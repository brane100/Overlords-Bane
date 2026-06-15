using UnityEngine;

/// <summary>
/// Attached to the EyeWatcher prefab. Deals continuous damage to the player
/// while they are within range and in line of sight, and renders a jagged
/// lightning bolt from the eye to the player for the frames it is zapping.
///
/// The bolt colour is pulled from the eye's per-level accent (set by EyeSpawner
/// from the LevelTheme), so it automatically matches each level — violet #8B5CF6
/// for Level 1 (Axiom).
/// </summary>
[RequireComponent(typeof(EyeWatcher))]
public class EyeZap : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] float range = 5f;
    [SerializeField] float damagePerSecond = 8f;
    [Tooltip("Raises the bolt's strike point from the player's feet toward the chest.")]
    [SerializeField] float targetHeightOffset = 1f;

    [Header("Zap timing")]
    [Tooltip("Shortest gap between random bolt flashes while in range.")]
    [SerializeField] float zapMinInterval = 0.12f;
    [Tooltip("Longest gap between random bolt flashes while in range.")]
    [SerializeField] float zapMaxInterval = 0.45f;
    [Tooltip("How long each individual bolt flash stays visible.")]
    [SerializeField] float boltFlashDuration = 0.08f;

    [Header("Bolt look")]
    [Tooltip("Number of kinks in the bolt — more = more jagged.")]
    [SerializeField] int segments = 12;
    [Tooltip("Max sideways jitter of the bolt midsection (world units).")]
    [SerializeField] float jitter = 0.35f;
    [SerializeField] float startWidth = 0.09f;
    [SerializeField] float endWidth = 0.035f;
    [Tooltip("HDR multiplier so the bolt blooms.")]
    [SerializeField] float emissionIntensity = 3f;

    [Header("Hit sound")]
    [Tooltip("Looping crackle played while the eye is actively zapping the player.")]
    [SerializeField] AudioClip zapClip;
    [SerializeField] float zapVolume = 0.7f;
    [Tooltip("Distance (world units) at which the zap sound starts attenuating.")]
    [SerializeField] float soundMinDistance = 3f;
    [Tooltip("Distance (world units) at which the zap sound is fully silent.")]
    [SerializeField] float soundMaxDistance = 14f;

    EyeWatcher _eye;
    Transform _target;

    float _nextZapAt;
    float _boltOffAt;

    LineRenderer _lr;
    Material _boltMat;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    AudioSource _audio;

    void Awake()
    {
        _eye = GetComponent<EyeWatcher>();
    }

    void Update()
    {
        if (_target == null)
        {
            var g = GameObject.FindGameObjectWithTag("Player");
            if (g != null) _target = g.transform;
            HideBolt();
            StopZapSound();
            return;
        }

        Vector3 strike = _target.position + Vector3.up * targetHeightOffset;
        float dist = Vector3.Distance(transform.position, strike);
        bool inRange = dist <= range;

        if (inRange)
        {
            // Consistent damage the entire time the player is in range.
            var ph = _target.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(damagePerSecond * Time.deltaTime);

            // Audio crackles continuously while the player is being zapped.
            StartZapSound();

            // Random intermittent bolt flashes for the electric look — the
            // damage above is steady, only the visible arc strobes.
            if (Time.time >= _nextZapAt)
            {
                DrawBolt(transform.position, strike);
                _boltOffAt = Time.time + boltFlashDuration;
                _nextZapAt = Time.time + Random.Range(zapMinInterval, zapMaxInterval);
            }
            else if (Time.time >= _boltOffAt)
            {
                HideBolt();
            }
        }
        else
        {
            HideBolt();
            StopZapSound();
        }
    }

    void EnsureBolt()
    {
        if (_lr != null) return;

        var go = new GameObject("ZapBolt");
        go.transform.SetParent(transform, false);

        _lr = go.AddComponent<LineRenderer>();
        _lr.useWorldSpace = true;              // immune to the eye's pulse scaling
        _lr.numCapVertices = 2;
        _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows = false;
        _lr.alignment = LineAlignment.View;    // ribbon always faces the camera
        _lr.textureMode = LineTextureMode.Stretch;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        _boltMat = new Material(shader);
        _lr.material = _boltMat;

        _lr.enabled = false;
    }

    void DrawBolt(Vector3 start, Vector3 end)
    {
        EnsureBolt();

        Color accent = _eye != null ? _eye.Accent : new Color(0.545f, 0.361f, 0.965f);
        Color hdr = accent * emissionIntensity;
        if (_boltMat != null) _boltMat.SetColor(BaseColorId, hdr);
        _lr.startColor = hdr;
        _lr.endColor = hdr;

        // Slight per-frame width flicker so the bolt feels electric.
        float flick = 1f + Random.Range(-0.25f, 0.25f);
        _lr.startWidth = startWidth * flick;
        _lr.endWidth = endWidth * flick;

        // Two axes perpendicular to the bolt's direction, to jitter the midsection.
        Vector3 dir = (end - start).normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(dir, Vector3.right);
        side.Normalize();
        Vector3 up = Vector3.Cross(dir, side);

        int count = Mathf.Max(2, segments);
        _lr.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            // Envelope: zero displacement at both ends, max in the middle.
            float env = Mathf.Sin(t * Mathf.PI);
            float jx = Random.Range(-jitter, jitter) * env;
            float jy = Random.Range(-jitter, jitter) * env;
            _lr.SetPosition(i, p + side * jx + up * jy);
        }

        _lr.enabled = true;
    }

    void HideBolt()
    {
        if (_lr != null && _lr.enabled) _lr.enabled = false;
    }

    void EnsureAudio()
    {
        if (_audio != null) return;

        var go = new GameObject("ZapAudio");
        go.transform.SetParent(transform, false);

        _audio = go.AddComponent<AudioSource>();
        _audio.clip = zapClip;
        _audio.loop = true;
        _audio.playOnAwake = false;
        _audio.volume = zapVolume;
        _audio.spatialBlend = 1f;              // fully 3D — louder the closer you are
        _audio.rolloffMode = AudioRolloffMode.Linear;
        _audio.minDistance = soundMinDistance;
        _audio.maxDistance = soundMaxDistance;
    }

    void StartZapSound()
    {
        if (zapClip == null) return;
        EnsureAudio();
        if (!_audio.isPlaying) _audio.Play();
    }

    void StopZapSound()
    {
        if (_audio != null && _audio.isPlaying) _audio.Stop();
    }

    void OnDestroy()
    {
        if (_boltMat != null) Destroy(_boltMat);
    }
}
