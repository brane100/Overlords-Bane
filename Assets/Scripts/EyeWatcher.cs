using UnityEngine;

/// <summary>
/// A 3D Axiom eyeball embedded in a maze wall.
/// The entire sphere rotates to track the player (clamped so it cannot spin
/// into the wall), so the iris quad child naturally stays front-and-center.
/// Glow, pulse, and blink remain as before.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class EyeWatcher : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Left null — found by 'Player' tag at runtime.")]
    [SerializeField] Transform target;

    [Header("Glow")]
    [SerializeField] Color accent = new Color(0.545f, 0.361f, 0.965f); // #8B5CF6
    [SerializeField] float minEmission = 0.5f;
    [SerializeField] float maxEmission = 1.5f;
    [SerializeField] float approachRadius = 16f;
    [SerializeField] float lightMin = 0.4f;
    [SerializeField] float lightMax = 1.8f;

    [Header("Eyeball rotation")]
    [Tooltip("Smooth tracking speed — higher feels snappier.")]
    [SerializeField] float trackSpeed = 3f;
    [Tooltip("Max degrees the eyeball can rotate from its resting wall-face direction.")]
    [SerializeField] float maxTrackAngle = 50f;

    [Header("Pulse / Blink")]
    [SerializeField] float baseScale = 1.0f;
    [SerializeField] float pulseAmplitude = 0.05f;
    [SerializeField] float pulseSpeed = 2.0f;
    [SerializeField] float blinkMinInterval = 3f;
    [SerializeField] float blinkMaxInterval = 8f;
    [SerializeField] float blinkDuration = 0.09f;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    MeshRenderer _sclera;
    MaterialPropertyBlock _mpb;
    Light _light;

    Quaternion _initialRotation;
    float _phase, _nextBlinkAt, _blinkTimer = -1f;

    void Awake()
    {
        _sclera = GetComponent<MeshRenderer>();
        _mpb = new MaterialPropertyBlock();

        var lightT = transform.Find("Light");
        _light = lightT != null ? lightT.GetComponent<Light>() : null;

        _initialRotation = transform.rotation;
        _phase = Random.value * Mathf.PI * 2f;
        ScheduleNextBlink();
    }

    public void SetAccent(Color c) => accent = c;

    void Update()
    {
        if (target == null) AcquireTarget();

        RotateEyeball();
        float blinkY = UpdateBlink();
        ApplyPulse(blinkY);
        ApplyGlow();
    }

    void AcquireTarget()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) target = go.transform;
    }

    void RotateEyeball()
    {
        if (target == null) return;

        Vector3 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude < 0.001f) return;

        Quaternion desired = Quaternion.LookRotation(toTarget, Vector3.up);

        // Clamp to maxTrackAngle from the resting wall-face pose so the sphere
        // never visually sinks into the terrain.
        Quaternion clamped = Quaternion.RotateTowards(_initialRotation, desired, maxTrackAngle);

        transform.rotation = Quaternion.Slerp(transform.rotation, clamped, Time.deltaTime * trackSpeed);
    }

    float UpdateBlink()
    {
        if (_blinkTimer < 0f)
        {
            if (Time.time >= _nextBlinkAt) _blinkTimer = 0f;
            return 1f;
        }
        _blinkTimer += Time.deltaTime;
        float full = blinkDuration * 2f;
        if (_blinkTimer >= full) { _blinkTimer = -1f; ScheduleNextBlink(); return 1f; }
        float t = _blinkTimer / blinkDuration;
        float closed = 1f - Mathf.PingPong(t, 1f);
        return Mathf.Lerp(1f, 0.08f, closed);
    }

    void ApplyPulse(float blinkY)
    {
        float pulse = baseScale * (1f + Mathf.Sin(Time.time * pulseSpeed + _phase) * pulseAmplitude);
        transform.localScale = new Vector3(pulse, pulse * blinkY, pulse);
    }

    void ApplyGlow()
    {
        float proximity = 0f;
        if (target != null)
        {
            float d = Vector3.Distance(target.position, transform.position);
            proximity = 1f - Mathf.Clamp01(d / Mathf.Max(0.01f, approachRadius));
        }

        if (_sclera == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        float emis = Mathf.Lerp(minEmission, maxEmission, proximity);
        _sclera.GetPropertyBlock(_mpb);
        _mpb.SetColor(EmissionColorId, accent * emis);
        _sclera.SetPropertyBlock(_mpb);

        if (_light != null)
        {
            _light.color = accent;
            _light.intensity = Mathf.Lerp(lightMin, lightMax, proximity);
        }
    }

    void ScheduleNextBlink() =>
        _nextBlinkAt = Time.time + Random.Range(blinkMinInterval, blinkMaxInterval);
}
