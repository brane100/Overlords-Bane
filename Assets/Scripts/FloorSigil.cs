using System.Collections;
using UnityEngine;

/// <summary>
/// A Level 4 (Kharros) cursed sigil trap. A glowing rune lies flush on the floor,
/// dim while dormant. At random intervals it telegraphs by brightening, then
/// IGNITES — a hot flash that burns any player standing on it for a single hit,
/// before fading back to dormant. The brightening warns the player, so deaths
/// feel earned rather than random.
///
/// Damage is resolved with an OverlapBox (no physical collider), so the sigil
/// never blocks movement — it is a glow + a damage volume.
/// </summary>
public class FloorSigil : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] float damage = 25f;
    [Tooltip("Half-extents box used to catch the player standing on the rune.")]
    [SerializeField] Vector3 footprint = new Vector3(2.6f, 1.6f, 2.6f);

    [Header("Timing (seconds)")]
    [SerializeField] float minInterval = 3f;
    [SerializeField] float maxInterval = 7f;
    [Tooltip("How long the rune brightens as a warning before it ignites.")]
    [SerializeField] float telegraphTime = 1.2f;
    [Tooltip("How long the burning flash lasts (damage lands at its start).")]
    [SerializeField] float igniteTime = 0.5f;

    [Header("Glow")]
    [SerializeField] Color accent = new Color(1f, 0.478f, 0.227f); // Kharros ember
    [SerializeField] float dimEmission = 0.5f;
    [SerializeField] float hotEmission = 7f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    Renderer _rend;
    MaterialPropertyBlock _mpb;
    Light _flare;

    void Awake()
    {
        _rend = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
        var flareT = transform.Find("Flare");
        if (flareT != null) _flare = flareT.GetComponent<Light>();
        SetGlow(dimEmission);
    }

    public void SetAccent(Color c) { accent = c; SetGlow(dimEmission); }

    void OnEnable() => StartCoroutine(Loop());

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(Random.Range(0f, maxInterval)); // desync the field

        while (true)
        {
            SetGlow(dimEmission);
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            // Telegraph — brighten so the player can step off.
            float t = 0f;
            while (t < telegraphTime)
            {
                t += Time.deltaTime;
                SetGlow(Mathf.Lerp(dimEmission, hotEmission, t / telegraphTime));
                yield return null;
            }

            // Ignite — hot flash + the burn.
            SetGlow(hotEmission * 1.3f);
            TryDamage();
            yield return new WaitForSeconds(igniteTime);
        }
    }

    void SetGlow(float emission)
    {
        if (_rend != null)
        {
            _rend.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, accent * emission);
            _rend.SetPropertyBlock(_mpb);
        }
        if (_flare != null)
        {
            _flare.color = accent;
            _flare.intensity = Mathf.Clamp(emission * 0.6f, 0f, 8f);
        }
    }

    void TryDamage()
    {
        Vector3 center = transform.position + Vector3.up * footprint.y;
        var hits = Physics.OverlapBox(center, footprint, transform.rotation, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            var ph = hits[i].GetComponentInParent<PlayerHealth>();
            if (ph != null) { ph.TakeDamage(damage); return; }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * footprint.y, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, footprint * 2f);
    }
}
