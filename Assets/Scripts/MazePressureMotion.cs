using UnityEngine;
using System.Collections;

public class MazePressureMotion : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] float interval = 5f;      // every 5 seconds
    [SerializeField] float tiltLerpTime = 5.5f;  // how fast it tilts
    [SerializeField] float holdTime = 6f;        // how long it stays tilted

        [Header("Tilt")]
    [SerializeField] float maxTiltX = 15f;        // degrees
    [SerializeField] float maxTiltZ = 15f;        // degrees

    [Header("Shake (rotation)")]
    [SerializeField] float shakeDuration = 1.2f;
    [SerializeField] float shakeStrength = 0.8f; // degrees
    [SerializeField] float shakeSpeed = 18f;

    Quaternion baseRotation;

    void Start()
    {
        baseRotation = transform.rotation;
        StartCoroutine(Routine());
    }

    IEnumerator Routine()
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);

            // Pick a new stressful tilt target (doesn't have to return to 0)
            float tx = Random.Range(-maxTiltX, maxTiltX);
            float tz = Random.Range(-maxTiltZ, maxTiltZ);
            Quaternion target = baseRotation * Quaternion.Euler(tx, 0f, tz);

            yield return RotateTo(target, tiltLerpTime);
            yield return ShakeRotational(target, shakeDuration);

            // Hold the tilted state for pressure
            yield return new WaitForSeconds(holdTime);

            // Optional: choose whether to drift back a bit or stay “worse”
            // baseRotation = Quaternion.Slerp(baseRotation, target, 0.35f); // gradual degradation
            // (or comment that out to keep baseRotation constant)
        }
    }

    IEnumerator RotateTo(Quaternion target, float duration)
    {
        Quaternion start = transform.rotation;
        float t = 0f;
        while (t < duration)
        {
            transform.rotation = Quaternion.Slerp(start, target, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.rotation = target;
    }

    IEnumerator ShakeRotational(Quaternion target, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            float n1 = Mathf.PerlinNoise(Time.time * shakeSpeed, 0.1f) * 2f - 1f;
            float n2 = Mathf.PerlinNoise(0.7f, Time.time * shakeSpeed) * 2f - 1f;

            Quaternion shake = Quaternion.Euler(n1 * shakeStrength, 0f, n2 * shakeStrength);
            transform.rotation = target * shake;

            t += Time.deltaTime;
            yield return null;
        }
        transform.rotation = target;
    }
}
