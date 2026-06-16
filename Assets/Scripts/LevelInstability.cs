using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Makes a whole level feel like a massive ancient structure becoming unstable.
/// Placed on a "LevelRoot" that parents all of the level's geometry, it applies:
///
///   • Continuous low-frequency tilting (smooth, randomized, non-repeating).
///   • A small high-frequency procedural vibration (shake) on top.
///
/// Both are driven by layered Perlin noise so the motion never repeats and
/// transitions between tilt directions are inherently smooth. Everything is
/// scaled by a single <see cref="intensity"/> (0..1) so the effect ramps cleanly
/// from Level 1 (subtle) to Level 5 (extreme) without per-scene tuning.
///
/// Because the structure rotates about LevelRoot's own origin (placed at the
/// level centre), the geometry sways as one rigid piece — nothing separates,
/// slides or drifts. The player is a CharacterController and does NOT inherit
/// platform motion on its own, so this component carries it: each frame it moves
/// the controller by the same delta the floor moved beneath its feet. It keys off
/// the generic CharacterController (found at runtime), not any specific prefab, so
/// swapping in a different 3D player model needs no changes here.
/// </summary>
public class LevelInstability : MonoBehaviour
{
    [Header("Intensity")]
    [Tooltip("Master scalar (0..1). Tilt and shake maxima are multiplied by this. " +
             "Set per level: L1≈0.05, L2≈0.10, L3≈0.175, L4≈0.25, L5≈0.30.")]
    [Range(0f, 1f)]
    [SerializeField] float intensity = 0.05f;

    [Tooltip("If true and intensity-from-scene mapping exists, derive intensity from the " +
             "scene name's trailing number (Level1..Level5) so it scales without manual edits.")]
    [SerializeField] bool autoIntensityFromSceneName = false;
    [Tooltip("Intensity per level number (index 0 = Level1). Used only when autoIntensityFromSceneName is on.")]
    [SerializeField] float[] perLevelIntensity = { 0.05f, 0.10f, 0.175f, 0.25f, 0.30f };

    [Header("Tilt (low-frequency sway)")]
    [Tooltip("Maximum tilt on each of X/Z, in degrees, at intensity = 1.")]
    [SerializeField] float maxTiltDegrees = 8f;
    [Tooltip("How fast the tilt direction drifts. Lower = slower, heavier sway.")]
    [SerializeField] float tiltFrequency = 0.08f;

    [Header("Shake (small vibration)")]
    [Tooltip("Maximum positional shake, in metres, at intensity = 1.")]
    [SerializeField] float maxShakeMeters = 0.15f;
    [Tooltip("How fast the vibration oscillates. Higher = buzzier.")]
    [SerializeField] float shakeFrequency = 6f;

    [Header("Player carry")]
    [Tooltip("When on, the CharacterController player is moved with the structure so it never slides.")]
    [SerializeField] bool carryPlayer = true;

    // Per-instance noise offsets so levels never move in lockstep.
    float _seedTX, _seedTZ, _seedSX, _seedSY, _seedSZ;

    Quaternion _baseRotation;
    Vector3 _basePosition;

    CharacterController _player;
    float _reacquireTimer;

    void Start()
    {
        _baseRotation = transform.rotation;
        _basePosition = transform.position;

        var rng = new System.Random(GetInstanceID());
        _seedTX = (float)rng.NextDouble() * 1000f;
        _seedTZ = (float)rng.NextDouble() * 1000f;
        _seedSX = (float)rng.NextDouble() * 1000f;
        _seedSY = (float)rng.NextDouble() * 1000f;
        _seedSZ = (float)rng.NextDouble() * 1000f;

        if (autoIntensityFromSceneName)
            intensity = ResolveIntensityFromScene(intensity);
    }

    void LateUpdate()
    {
        // 1) Remember where the structure was, so we can carry the player by its delta.
        Matrix4x4 prevMatrix = transform.localToWorldMatrix;

        // 2) Tilt: two octaves of Perlin per axis -> smooth, randomized, non-repeating.
        float tilt = maxTiltDegrees * intensity;
        float tx = (Layered(_seedTX, tiltFrequency) ) * tilt;
        float tz = (Layered(_seedTZ, tiltFrequency) ) * tilt;

        // 3) Shake: small, faster positional jitter.
        float shake = maxShakeMeters * intensity;
        Vector3 shakeOffset = new Vector3(
            Layered(_seedSX, shakeFrequency) * shake,
            Layered(_seedSY, shakeFrequency) * shake * 0.5f, // less vertical
            Layered(_seedSZ, shakeFrequency) * shake);

        transform.rotation = _baseRotation * Quaternion.Euler(tx, 0f, tz);
        transform.position = _basePosition + shakeOffset;

        // 4) Carry the player so it rides the structure instead of sliding off it.
        if (carryPlayer)
            CarryPlayer(prevMatrix);
    }

    // Two octaves of Perlin noise centred on 0, range roughly [-1, 1].
    float Layered(float seed, float freq)
    {
        float t = Time.time;
        float a = Mathf.PerlinNoise(seed + t * freq, seed * 0.37f) * 2f - 1f;
        float b = (Mathf.PerlinNoise(seed * 1.7f, seed + t * freq * 2.3f) * 2f - 1f) * 0.5f;
        return Mathf.Clamp((a + b) / 1.5f, -1f, 1f);
    }

    void CarryPlayer(Matrix4x4 prevMatrix)
    {
        if (_player == null)
        {
            _reacquireTimer -= Time.deltaTime;
            if (_reacquireTimer > 0f) return;
            _reacquireTimer = 0.5f;
            _player = FindFirstObjectByType<CharacterController>();
            if (_player == null) return;
        }

        // The point under the player, expressed in the structure's pre-move frame,
        // has moved to a new world position now that the structure has shifted.
        // Move the controller by that delta so it stays glued to the floor.
        Vector3 localUnderPlayer = prevMatrix.inverse.MultiplyPoint3x4(_player.transform.position);
        Vector3 newWorld = transform.TransformPoint(localUnderPlayer);
        Vector3 delta = newWorld - _player.transform.position;

        if (delta.sqrMagnitude > 0f && _player.enabled)
            _player.Move(delta);
    }

    int ResolveLevelNumber()
    {
        string name = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        var m = Regex.Match(name, @"(\d+)\s*$");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    float ResolveIntensityFromScene(float fallback)
    {
        int n = ResolveLevelNumber();
        if (n >= 1 && perLevelIntensity != null && n <= perLevelIntensity.Length)
            return perLevelIntensity[n - 1];
        return fallback;
    }
}
