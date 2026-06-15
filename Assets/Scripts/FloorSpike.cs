using System.Collections;
using UnityEngine;

/// <summary>
/// A Level 2 floor spike trap. Stays retracted below the floor, then at random
/// intervals thrusts a cluster of spikes up through the floor. Any player whose
/// body is over the trap while the spikes are up takes a single 30-damage hit
/// per strike, then the spikes retract and the cycle repeats on a fresh random
/// timer (each trap is desynced so the field strikes chaotically).
///
/// Damage is resolved with an OverlapBox over the trap footprint rather than a
/// physical collider, so the spikes never shove the player or trap them — they
/// are purely visual + a damage volume.
/// </summary>
public class FloorSpike : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Damage dealt once per strike to a player standing on the trap.")]
    [SerializeField] float damage = 30f;
    [Tooltip("Half-extents box (X/Z from cell size, Y tall enough to catch the capsule) used to detect the player.")]
    [SerializeField] Vector3 footprint = new Vector3(3.2f, 1.6f, 3.2f);

    [Header("Timing (seconds)")]
    [SerializeField] float minInterval = 2.0f;
    [SerializeField] float maxInterval = 5.5f;
    [Tooltip("How long the spikes take to shoot up.")]
    [SerializeField] float riseTime = 0.10f;
    [Tooltip("How long the spikes stay fully up.")]
    [SerializeField] float upDwell = 0.65f;
    [Tooltip("How long the spikes take to sink back down.")]
    [SerializeField] float retractTime = 0.40f;
    [Tooltip("Vertical travel of the 'Spikes' child between retracted and fully up.")]
    [SerializeField] float riseHeight = 2.4f;

    Transform _spikes;          // the child cluster that travels up/down
    Vector3 _downLocalPos;      // retracted rest position
    bool _struckThisCycle;      // gates damage to one hit per strike

    void Awake()
    {
        _spikes = transform.Find("Spikes");
        if (_spikes != null) _downLocalPos = _spikes.localPosition;
    }

    void OnEnable()
    {
        if (_spikes != null) StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        // Random initial offset so the whole field doesn't fire in unison.
        yield return new WaitForSeconds(Random.Range(0f, maxInterval));

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
            yield return Strike();
        }
    }

    IEnumerator Strike()
    {
        _struckThisCycle = false;
        Vector3 up = _downLocalPos + Vector3.up * riseHeight;

        // Shoot up — damage is applied the instant the spikes are emerging.
        float t = 0f;
        while (t < riseTime)
        {
            t += Time.deltaTime;
            _spikes.localPosition = Vector3.Lerp(_downLocalPos, up, t / riseTime);
            TryDamage();
            yield return null;
        }
        _spikes.localPosition = up;
        TryDamage();

        // Hold up.
        yield return new WaitForSeconds(upDwell);

        // Retract.
        t = 0f;
        while (t < retractTime)
        {
            t += Time.deltaTime;
            _spikes.localPosition = Vector3.Lerp(up, _downLocalPos, t / retractTime);
            yield return null;
        }
        _spikes.localPosition = _downLocalPos;
    }

    void TryDamage()
    {
        if (_struckThisCycle) return;

        Vector3 center = transform.position + Vector3.up * footprint.y;
        var hits = Physics.OverlapBox(center, footprint, transform.rotation,
                                      ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            // Don't require a "Player" tag — the runtime PlayerArmature may be
            // untagged. Any collider whose hierarchy owns a PlayerHealth counts.
            var ph = hits[i].GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(damage);
                _struckThisCycle = true;
                return;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * footprint.y,
                                      transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, footprint * 2f);
    }
}
