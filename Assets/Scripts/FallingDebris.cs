using System.Collections;
using UnityEngine;

/// <summary>
/// A Level 5 (EidolonRex) collapsing-spire hazard. A heavy stone block hangs near
/// the ceiling above a corridor cell. At random intervals it shudders (telegraph),
/// then SLAMS down to the floor — crushing any player beneath it for heavy damage —
/// before grinding back up. As the apex collapses, debris rains down.
///
/// Damage uses an OverlapBox (no physical collider) so the block never traps the
/// player; the shudder warns them to keep moving.
/// </summary>
public class FallingDebris : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] float damage = 35f;
    [SerializeField] Vector3 footprint = new Vector3(2.6f, 1.6f, 2.6f);

    [Header("Timing (seconds)")]
    [SerializeField] float minInterval = 3f;
    [SerializeField] float maxInterval = 8f;
    [Tooltip("Shudder warning before the block drops.")]
    [SerializeField] float telegraphTime = 0.9f;
    [SerializeField] float shakeAmount = 0.12f;
    [Tooltip("How fast the block slams down.")]
    [SerializeField] float slamTime = 0.12f;
    [SerializeField] float holdTime = 0.4f;
    [Tooltip("How long the block grinds back up.")]
    [SerializeField] float retractTime = 1.2f;

    [Tooltip("Local Y the block rests at when slammed (block half-height above the floor).")]
    [SerializeField] float impactLocalY = 0.6f;

    Transform _block;
    Vector3 _restPos;
    Vector3 _impactPos;

    void Awake()
    {
        _block = transform.Find("Block");
        if (_block != null)
        {
            _restPos = _block.localPosition;
            _impactPos = new Vector3(_restPos.x, impactLocalY, _restPos.z);
        }
    }

    void OnEnable() { if (_block != null) StartCoroutine(Loop()); }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(Random.Range(0f, maxInterval)); // desync the field

        while (true)
        {
            _block.localPosition = _restPos;
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            // Shudder telegraph.
            float t = 0f;
            while (t < telegraphTime)
            {
                t += Time.deltaTime;
                _block.localPosition = _restPos + Random.insideUnitSphere * shakeAmount;
                yield return null;
            }

            // Slam.
            t = 0f;
            while (t < slamTime)
            {
                t += Time.deltaTime;
                _block.localPosition = Vector3.Lerp(_restPos, _impactPos, t / slamTime);
                yield return null;
            }
            _block.localPosition = _impactPos;
            TryDamage();

            yield return new WaitForSeconds(holdTime);

            // Grind back up.
            t = 0f;
            while (t < retractTime)
            {
                t += Time.deltaTime;
                _block.localPosition = Vector3.Lerp(_impactPos, _restPos, t / retractTime);
                yield return null;
            }
            _block.localPosition = _restPos;
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
        Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * footprint.y, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, footprint * 2f);
    }
}
