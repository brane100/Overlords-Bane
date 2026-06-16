using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Performance-friendly "mirror" for the Level 3 mirror maze. Instead of
/// rendering the whole world, it spawns a single ghostly CLONE of the player and
/// reflects it across whichever mirror wall the player faces. Seen through the
/// dark obsidian glass, the clone reads as the player's reflection.
///
/// Runs in LateUpdate so it samples the player AFTER movement + animation each
/// frame (smooth, no stutter). Escalating phases progressively corrupt the
/// reflection so the player slides from curiosity into dread:
///
///   P1 Delayed      — reflection lags 0.2–0.5s after a trust period.
///   P2 Independent  — occasionally the head turns toward the player on its own.
///   P3 False anim   — occasionally the reflection plays a different animation.
///   P4 Vanishing    — fades out when the player stands still, snaps back on move.
///   P5 Entities     — a figure only visible in the mirror, gone when looked at.
///   P6 Desync       — the reflection leads/lags spatially, moving on its own.
///
/// Each phase unlocks on its own timer and then layers on top of the others.
/// </summary>
public class MirrorReflection : MonoBehaviour
{
    [Header("Clone source")]
    [SerializeField] GameObject clonePrefab;
    [SerializeField] Material ghostMaterial;

    [Header("Facing mirror")]
    [SerializeField] LayerMask wallMask = ~0;
    [SerializeField] float maxMirrorDistance = 7f;

    [Header("P1 — Delayed Reflection")]
    [SerializeField] float trustDuration = 25f;
    [SerializeField] float delayRampTime = 25f;
    [SerializeField] Vector2 delayRange = new Vector2(0.2f, 0.5f);

    [Header("P4 — Vanishing Reflection")]
    [SerializeField] float vanishStartTime = 50f;
    [SerializeField] float stillSpeedThreshold = 0.15f;
    [SerializeField] float stillTimeToVanish = 1.0f;
    [SerializeField] float fadeSpeed = 2.5f;

    [Header("P2 — Independent head")]
    [SerializeField] float phase2StartTime = 70f;
    [SerializeField] float headEventChancePerSec = 0.05f;
    [SerializeField] Vector2 headEventDuration = new Vector2(2f, 4f);
    [SerializeField] float headTurnDegPerSec = 120f;
    [SerializeField] float headMaxYaw = 90f;

    [Header("P3 — False animations")]
    [SerializeField] float phase3StartTime = 95f;
    [SerializeField] float falseAnimChancePerSec = 0.04f;
    [SerializeField] Vector2 falseAnimDuration = new Vector2(2f, 5f);

    [Header("P5 — Hidden entities")]
    [SerializeField] float phase5StartTime = 120f;
    [SerializeField] Material entityMaterial;
    [SerializeField] float entityChancePerSec = 0.02f;
    [SerializeField] Vector2 entityDuration = new Vector2(3f, 6f);
    [Tooltip("Look angle (deg) within which the entity is 'directly observed' and vanishes.")]
    [SerializeField] float entityObserveAngle = 28f;

    [Header("P6 — Desynchronisation")]
    [SerializeField] float phase6StartTime = 150f;
    [SerializeField] Vector2 desyncDelayRange = new Vector2(1.2f, 3f);
    [SerializeField] float desyncLeadDistance = 2.5f;

    [Header("Audio (assign clips to enable)")]
    [SerializeField] AudioClip footstepClip;
    [SerializeField] AudioClip breathClip;
    [SerializeField] AudioClip whisperClip;
    [SerializeField] float footstepVolume = 0.4f;
    [SerializeField] float breathVolume = 0.5f;
    [SerializeField] float whisperVolume = 0.6f;
    [SerializeField] float footstepInterval = 0.5f;
    [SerializeField] float whisperChancePerSec = 0.08f;

    /// <summary>Highest unlocked phase (0 = flawless). Drives nothing else externally yet.</summary>
    public int CurrentPhase { get; private set; }

    // ---- runtime ----
    Transform _player;
    Animator _playerAnim;
    Animator _cloneAnim;
    AnimatorControllerParameter[] _params;
    Transform _clone;
    Transform _headBone;
    readonly List<Renderer> _cloneRenderers = new List<Renderer>();
    Material _ghostInstance;
    Color _ghostBaseColor = Color.white;
    Camera _cam;
    AudioSource _audio;

    struct Sample { public float t; public Vector3 pos; public Vector3 fwd; public Vector3 up; }
    Sample[] _buffer = new Sample[256];
    int _bufHead = -1;
    int _bufCount;

    float _levelTime;
    Vector3 _lastPlayerPos;
    Vector3 _cloneLastPos;
    float _stillTimer;
    float _currentDelay, _targetDelay, _retargetTimer;
    float _alpha = 1f;
    bool _hasFacingMirror;

    // phase gates
    bool _pDelay, _pVanish, _headEnabled, _falseAnimEnabled, _entityEnabled, _desyncEnabled;
    bool _desyncActive; float _desyncOffset;

    // head event
    bool _headEventActive; float _headEventEnd, _headYaw;
    // false anim
    bool _falseAnimActive; float _falseAnimEnd; int _falseAnimKind;
    // entity
    GameObject _entity; bool _entityActive; float _entityEnd;
    // audio
    float _footstepTimer;

    void LateUpdate()
    {
        _levelTime += Time.deltaTime;

        if (_player == null) { TryAcquirePlayer(); return; }
        if (_clone == null) return;
        if (_cam == null) _cam = Camera.main;

        RecordSample();
        UpdatePhaseTimers();
        UpdateFalseAnimEvent();
        CopyAnimation();
        PlaceReflection();
        UpdateHeadEvent();      // after animation/placement so it overrides the final pose
        UpdateEntityEvent();
        UpdateVanish();
        ApplyAlpha();
        UpdateAudio();
    }

    // ---------------------------------------------------------------- acquisition

    void TryAcquirePlayer()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return;
        _player = go.transform;
        _playerAnim = go.GetComponentInChildren<Animator>();
        _lastPlayerPos = _player.position;
        _cam = Camera.main;
        BuildClone();
    }

    void BuildClone()
    {
        if (clonePrefab == null) { Debug.LogWarning("[MirrorReflection] No clonePrefab assigned."); return; }

        var holder = new GameObject("MirrorCloneHolder");
        holder.SetActive(false);

        var cloneGo = Instantiate(clonePrefab, holder.transform);
        cloneGo.tag = "Untagged";
        StripToHusk(cloneGo);

        _cloneAnim = cloneGo.GetComponentInChildren<Animator>();
        if (_cloneAnim != null)
        {
            _cloneAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (_cloneAnim.GetComponent<ReflectionAnimEventSink>() == null)
                _cloneAnim.gameObject.AddComponent<ReflectionAnimEventSink>();
            if (_cloneAnim.isHuman) _headBone = _cloneAnim.GetBoneTransform(HumanBodyBones.Head);
        }

        if (ghostMaterial != null)
        {
            _ghostInstance = new Material(ghostMaterial);
            if (_ghostInstance.HasProperty("_BaseColor")) _ghostBaseColor = _ghostInstance.GetColor("_BaseColor");
        }
        cloneGo.GetComponentsInChildren(true, _cloneRenderers);
        foreach (var r in _cloneRenderers)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (_ghostInstance != null)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = _ghostInstance;
                r.sharedMaterials = mats;
            }
        }

        _audio = cloneGo.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 1f;
        _audio.rolloffMode = AudioRolloffMode.Linear;
        _audio.minDistance = 2f;
        _audio.maxDistance = 14f;

        cloneGo.transform.SetParent(null, false);
        Destroy(holder);

        _clone = cloneGo.transform;
        _clone.name = "PlayerReflection";
        _cloneLastPos = _clone.position;

        if (_playerAnim != null) _params = _playerAnim.parameters;
    }

    /// <summary>Reduce the prefab to an animated husk. MUST use DestroyImmediate (clone is
    /// built inactive and activated this frame) or the player scripts + a second Camera
    /// would wake and steal input / Camera.main, inverting the real player's controls.</summary>
    static void StripToHusk(GameObject root)
    {
        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
        foreach (var no in root.GetComponentsInChildren<NetworkObject>(true)) if (no != null) DestroyImmediate(no);
        foreach (var cc in root.GetComponentsInChildren<CharacterController>(true)) if (cc != null) DestroyImmediate(cc);
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) DestroyImmediate(rb);
        foreach (var col in root.GetComponentsInChildren<Collider>(true)) if (col != null) DestroyImmediate(col);
        // Camera + AudioListener are NOT MonoBehaviours, so the sweep above misses them.
        foreach (var camChild in root.GetComponentsInChildren<Camera>(true)) if (camChild != null) DestroyImmediate(camChild.gameObject);
        foreach (var al in root.GetComponentsInChildren<AudioListener>(true)) if (al != null) DestroyImmediate(al);
    }

    // ---------------------------------------------------------------- sampling

    void RecordSample()
    {
        _bufHead = (_bufHead + 1) % _buffer.Length;
        _buffer[_bufHead] = new Sample { t = _levelTime, pos = _player.position, fwd = _player.forward, up = _player.up };
        if (_bufCount < _buffer.Length) _bufCount++;
    }

    Sample SampleAt(float time)
    {
        for (int i = 0; i < _bufCount; i++)
        {
            int idx = (_bufHead - i + _buffer.Length) % _buffer.Length;
            if (_buffer[idx].t <= time) return _buffer[idx];
        }
        return _buffer[(_bufHead - _bufCount + 1 + _buffer.Length) % _buffer.Length];
    }

    // ---------------------------------------------------------------- phases

    void UpdatePhaseTimers()
    {
        _pDelay         = _levelTime >= trustDuration;
        _pVanish        = _levelTime >= vanishStartTime;
        _headEnabled    = _levelTime >= phase2StartTime;
        _falseAnimEnabled = _levelTime >= phase3StartTime;
        _entityEnabled  = _levelTime >= phase5StartTime;
        _desyncEnabled  = _levelTime >= phase6StartTime;

        CurrentPhase = _desyncEnabled ? 6 : _entityEnabled ? 5 : _falseAnimEnabled ? 3
                      : _headEnabled ? 2 : _pVanish ? 4 : _pDelay ? 1 : 0;

        // Delay target (larger & slower once desynchronised).
        float ramp = Mathf.Clamp01((_levelTime - trustDuration) / Mathf.Max(0.01f, delayRampTime));
        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            Vector2 r = _desyncEnabled ? desyncDelayRange : delayRange;
            _targetDelay = Random.Range(r.x, r.y);
            _retargetTimer = Random.Range(2f, 5f);
        }
        float desired = _pDelay ? _targetDelay * ramp : 0f;
        _currentDelay = Mathf.MoveTowards(_currentDelay, desired, Time.deltaTime * 0.5f);

        // Smooth spatial lead/lag for desync (sine = never jerky).
        _desyncActive = _desyncEnabled;
        _desyncOffset = _desyncActive ? Mathf.Sin(_levelTime * 0.5f) * desyncLeadDistance : 0f;
    }

    void CopyAnimation()
    {
        if (_cloneAnim == null || _playerAnim == null || _params == null) return;
        if (_falseAnimActive) { ApplyFalseAnim(); return; }
        _cloneAnim.speed = 1f;
        CopyParamsRaw();
    }

    void CopyParamsRaw()
    {
        foreach (var p in _params)
        {
            switch (p.type)
            {
                case AnimatorControllerParameterType.Float:
                    _cloneAnim.SetFloat(p.nameHash, _playerAnim.GetFloat(p.nameHash)); break;
                case AnimatorControllerParameterType.Bool:
                    _cloneAnim.SetBool(p.nameHash, _playerAnim.GetBool(p.nameHash)); break;
                case AnimatorControllerParameterType.Int:
                    _cloneAnim.SetInteger(p.nameHash, _playerAnim.GetInteger(p.nameHash)); break;
            }
        }
    }

    void PlaceReflection()
    {
        Vector3 origin = _cam != null ? _cam.transform.position : _player.position + Vector3.up * 1.6f;
        Vector3 dir    = _cam != null ? _cam.transform.forward  : _player.forward;

        _hasFacingMirror = Physics.Raycast(origin, dir, out var hit, maxMirrorDistance, wallMask, QueryTriggerInteraction.Ignore);
        if (!_hasFacingMirror) { SetRenderers(false); return; }

        Vector3 n = hit.normal;
        Vector3 planePoint = hit.point;

        Sample s = SampleAt(_levelTime - _currentDelay);
        Vector3 srcPos = s.pos + (_desyncActive ? s.fwd * _desyncOffset : Vector3.zero);

        _clone.position = Reflect(srcPos, planePoint, n);
        Vector3 fwd = ReflectDir(s.fwd, n);
        Vector3 up  = ReflectDir(s.up, n);
        if (fwd.sqrMagnitude > 0.0001f) _clone.rotation = Quaternion.LookRotation(fwd, up);

        SetRenderers(_alpha > 0.02f);
    }

    // P2 — head turns toward the player on its own, layered over the animation.
    void UpdateHeadEvent()
    {
        if (_headBone == null) return;

        if (_headEnabled && !_headEventActive && Random.value < headEventChancePerSec * Time.deltaTime)
        {
            _headEventActive = true;
            _headEventEnd = _levelTime + Random.Range(headEventDuration.x, headEventDuration.y);
        }
        if (_headEventActive && (_levelTime >= _headEventEnd || !_headEnabled)) _headEventActive = false;

        float targetYaw = 0f;
        if (_headEventActive)
        {
            Vector3 toPlayer = (_cam != null ? _cam.transform.position : _player.position) - _headBone.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
                targetYaw = Mathf.Clamp(Vector3.SignedAngle(_clone.forward, toPlayer.normalized, Vector3.up), -headMaxYaw, headMaxYaw);
        }
        _headYaw = Mathf.MoveTowards(_headYaw, targetYaw, headTurnDegPerSec * Time.deltaTime);
        if (Mathf.Abs(_headYaw) > 0.05f)
            _headBone.rotation = Quaternion.AngleAxis(_headYaw, Vector3.up) * _headBone.rotation;
    }

    // P3 — occasionally drive the clone with a different animation than the player.
    void UpdateFalseAnimEvent()
    {
        if (!_falseAnimEnabled) { _falseAnimActive = false; return; }
        if (_falseAnimActive) { if (_levelTime >= _falseAnimEnd) EndFalseAnim(); return; }
        if (Random.value < falseAnimChancePerSec * Time.deltaTime)
        {
            _falseAnimActive = true;
            _falseAnimKind = Random.Range(0, 3);
            _falseAnimEnd = _levelTime + Random.Range(falseAnimDuration.x, falseAnimDuration.y);
        }
    }

    void ApplyFalseAnim()
    {
        switch (_falseAnimKind)
        {
            case 0: // walk in place while you may be still
                _cloneAnim.speed = 1f;
                _cloneAnim.SetFloat("Speed", 2f);
                _cloneAnim.SetFloat("MotionSpeed", 1f);
                _cloneAnim.SetBool("Grounded", true);
                _cloneAnim.SetBool("Jump", false);
                _cloneAnim.SetBool("FreeFall", false);
                break;
            case 1: // freeze mid-motion
                _cloneAnim.speed = 0f;
                break;
            default: // sluggish / heavy — body mirrors but in slow motion
                _cloneAnim.speed = 0.5f;
                CopyParamsRaw();
                break;
        }
    }

    void EndFalseAnim()
    {
        _falseAnimActive = false;
        if (_cloneAnim != null) _cloneAnim.speed = 1f;
    }

    // P5 — a figure that exists only "in the mirror" and vanishes when stared at.
    void UpdateEntityEvent()
    {
        if (!_entityEnabled) { if (_entity != null && _entity.activeSelf) _entity.SetActive(false); return; }

        if (!_entityActive)
        {
            if (Random.value < entityChancePerSec * Time.deltaTime) ShowEntity();
            return;
        }

        if (_levelTime >= _entityEnd || ObservedDirectly()) { if (_entity != null) _entity.SetActive(false); _entityActive = false; return; }
        PlaceEntity();
    }

    void ShowEntity()
    {
        if (!_hasFacingMirror) return;
        if (_entity == null) _entity = BuildEntity();
        _entity.SetActive(true);
        _entityActive = true;
        _entityEnd = _levelTime + Random.Range(entityDuration.x, entityDuration.y);
        PlaceEntity();
    }

    void PlaceEntity()
    {
        // Reflected position of a spot just behind the player -> appears in the mirror
        // standing behind the player's own reflection.
        Vector3 origin = _cam != null ? _cam.transform.position : _player.position + Vector3.up * 1.6f;
        Vector3 dir    = _cam != null ? _cam.transform.forward  : _player.forward;
        if (!Physics.Raycast(origin, dir, out var hit, maxMirrorDistance, wallMask, QueryTriggerInteraction.Ignore)) return;

        Vector3 behind = _player.position - _player.forward * 2.5f;
        _entity.transform.position = Reflect(behind, hit.point, hit.normal);
        Vector3 face = ReflectDir(_player.forward, hit.normal);
        if (face.sqrMagnitude > 0.0001f) _entity.transform.rotation = Quaternion.LookRotation(face, Vector3.up);
    }

    bool ObservedDirectly()
    {
        if (_entity == null || _cam == null) return false;
        Vector3 toEnt = _entity.transform.position - _cam.transform.position;
        return Vector3.Angle(_cam.transform.forward, toEnt) < entityObserveAngle;
    }

    GameObject BuildEntity()
    {
        var mat = entityMaterial != null ? entityMaterial : _ghostInstance;

        var root = new GameObject("MirrorEntity");
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.5f, 1.4f, 0.5f); // tall, gaunt
        body.transform.localPosition = new Vector3(0f, 1.4f, 0f);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(root.transform, false);
        head.transform.localScale = Vector3.one * 0.45f;
        head.transform.localPosition = new Vector3(0f, 2.9f, 0f);

        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (mat != null) r.sharedMaterial = mat;
        }
        return root;
    }

    // P4 — vanish while still.
    void UpdateVanish()
    {
        float speed = (_player.position - _lastPlayerPos).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
        _lastPlayerPos = _player.position;

        bool enabled = _levelTime >= vanishStartTime;
        if (enabled && speed < stillSpeedThreshold) _stillTimer += Time.deltaTime; else _stillTimer = 0f;

        float target = (enabled && _stillTimer > stillTimeToVanish) ? 0f : 1f;
        if (target > _alpha && _stillTimer == 0f) _alpha = 1f;               // snap back on move
        else _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime * fadeSpeed);
    }

    void ApplyAlpha()
    {
        if (_ghostInstance == null || !_ghostInstance.HasProperty("_BaseColor")) return;
        var c = _ghostBaseColor;
        c.a = _ghostBaseColor.a * _alpha;
        _ghostInstance.SetColor("_BaseColor", c);
    }

    // Reflection audio — silent until clips are assigned in the inspector.
    void UpdateAudio()
    {
        if (_audio == null) return;

        float cloneSpeed = (_clone.position - _cloneLastPos).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
        _cloneLastPos = _clone.position;

        // Breathing loop once unsettling phases begin.
        if (breathClip != null)
        {
            bool wantBreath = _falseAnimEnabled && _alpha > 0.1f;
            if (wantBreath && (!_audio.isPlaying || _audio.clip != breathClip))
            { _audio.clip = breathClip; _audio.loop = true; _audio.volume = breathVolume; _audio.Play(); }
            else if (!wantBreath && _audio.loop) { _audio.Stop(); _audio.loop = false; }
        }

        // Late footsteps from the (already-delayed) reflection.
        if (footstepClip != null && _alpha > 0.1f && cloneSpeed > stillSpeedThreshold)
        {
            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer <= 0f) { _audio.PlayOneShot(footstepClip, footstepVolume); _footstepTimer = footstepInterval; }
        }

        // Whispers near mirrors once the player starts to suspect.
        if (whisperClip != null && _headEnabled && _hasFacingMirror &&
            Random.value < whisperChancePerSec * Time.deltaTime)
            _audio.PlayOneShot(whisperClip, whisperVolume);
    }

    void SetRenderers(bool on)
    {
        for (int i = 0; i < _cloneRenderers.Count; i++)
            if (_cloneRenderers[i] != null && _cloneRenderers[i].enabled != on)
                _cloneRenderers[i].enabled = on;
    }

    static Vector3 Reflect(Vector3 p, Vector3 planePoint, Vector3 n) => p - 2f * Vector3.Dot(p - planePoint, n) * n;
    static Vector3 ReflectDir(Vector3 v, Vector3 n) => v - 2f * Vector3.Dot(v, n) * n;
}

/// <summary>Empty receiver for StarterAssets animation events (OnFootstep / OnLand) the
/// reflection clone's clips still fire after its controller was stripped — keeps the console clean.</summary>
public class ReflectionAnimEventSink : MonoBehaviour
{
    public void OnFootstep(AnimationEvent _) { }
    public void OnLand(AnimationEvent _) { }
}
