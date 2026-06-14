using UnityEngine;

/// <summary>
/// A 3D Axiom eyeball embedded in a maze wall:
///   • Root sphere = glowing sclera (emissive, lit so it reads as a 3D ball)
///   • "Iris" child = iris/pupil quad that rotates across the eyeball surface to
///     track the player (clamped to the outward-facing hemisphere so it never
///     slides into the wall)
///   • "Light" child = point light bleeding violet onto the corridor
///   • Scale pulse + vertical blink; sclera glow + light brighten as the player nears.
///
/// The eyeball is a sphere, so it is visible and clearly 3D from every angle —
/// only the iris tracks, the body stays fixed in the wall.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class EyeWatcher : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Left null — found by 'Player' tag at runtime.")]
    [SerializeField] Transform target;

    [Header("Glow")]
    [SerializeField] Color accent = new Color(0.545f, 0.361f, 0.965f); // #8B5CF6
    [Tooltip("Sclera emission multiplier at max distance.")]
    [SerializeField] float minEmission = 0.5f;
    [Tooltip("Sclera emission multiplier when player is within approachRadius.")]
    [SerializeField] float maxEmission = 1.5f;
    [SerializeField] float approachRadius = 16f;
    [SerializeField] float lightMin = 0.4f;
    [SerializeField] float lightMax = 1.8f;

    [Header("Iris tracking")]
    [Tooltip("How far the iris sits from the eyeball centre (local units).")]
    [SerializeField] float irisDistance = 0.52f;
    [Tooltip("Minimum forward dot — keeps the iris on the visible hemisphere.")]
    [SerializeField] float minForwardDot = 0.35f;

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
    Transform _iris;
    Light _light;

    Vector3 _outwardLocal = Vector3.forward; // eyeball's outward axis in local space
    float _phase, _nextBlinkAt, _blinkTimer = -1f;

    void Awake()
    {
        _sclera = GetComponent<MeshRenderer>();
        _mpb = new MaterialPropertyBlock();

        var irisT = transform.Find("Iris");
        _iris = irisT;
        var lightT = transform.Find("Light");
        _light = lightT != null ? lightT.GetComponent<Light>() : null;

        _phase = Random.value * Mathf.PI * 2f;
        ScheduleNextBlink();
    }

    public void SetAccent(Color c) => accent = c;

    void Update()
    {
        if (target == null) AcquireTarget();

        TrackIris();
        float blinkY = UpdateBlink();
        ApplyPulse(blinkY);
        ApplyGlow();
    }

    void AcquireTarget()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) target = go.transform;
    }

    // Rotate the iris across the eyeball surface toward the player, clamped so it
    // never slides past the outward hemisphere into the wall.
    void TrackIris()
    {
        if (_iris == null || target == null) return;

        // Direction to player in the eyeball's local space.
        Vector3 dl = transform.InverseTransformPoint(target.position).normalized;

        // Clamp to forward (+Z local = outward) hemisphere.
        if (dl.z < minForwardDot)
        {
            dl.z = minForwardDot;
            dl = dl.normalized;
        }

        _iris.localPosition = dl * irisDistance;
        _iris.localRotation = Quaternion.LookRotation(dl, Vector3.up);
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
