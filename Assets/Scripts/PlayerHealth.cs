using System;
using UnityEngine;

/// <summary>
/// Player vitality. Exposes TakeDamage()/Heal() for any future hazards, and
/// handles "out of bounds" (falling off the arena or leaving the floor) by
/// dealing fall damage and respawning at <see cref="respawnPoint"/>.
/// Reaching zero health raises <see cref="OnDeath"/> (drives the game-over screen).
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float currentHealth = 100f;

    [Header("Out of bounds")]
    [SerializeField] Transform respawnPoint;
    [SerializeField] float outOfBoundsY = -5f;
    [SerializeField] float fallDamage = 25f;
    [SerializeField] bool useHorizontalBounds = true;
    [SerializeField] Vector2 boundsMin; // world X / Z
    [SerializeField] Vector2 boundsMax; // world X / Z

    public float Max => maxHealth;
    public float Current => currentHealth;
    public float Normalized => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsDead { get; private set; }

    /// <summary>(current, max)</summary>
    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    CharacterController _cc;
    Rigidbody _rb;
    float _oobCooldown;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _rb = GetComponent<Rigidbody>();
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    void Start()
    {
        // Auto-locate SpawnPoint by name if not explicitly assigned (prefab usage)
        if (respawnPoint == null)
        {
            var sp = GameObject.Find("SpawnPoint");
            if (sp != null) respawnPoint = sp.transform;
        }
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Update()
    {
        if (IsDead) return;

        if (_oobCooldown > 0f)
        {
            _oobCooldown -= Time.unscaledDeltaTime;
            return;
        }

        Vector3 p = transform.position;
        bool outY = p.y < outOfBoundsY;
        bool outXZ = useHorizontalBounds &&
                     (p.x < boundsMin.x || p.x > boundsMax.x || p.z < boundsMin.y || p.z > boundsMax.y);
        if (outY || outXZ)
        {
            _oobCooldown = 0.5f;
            TakeDamage(fallDamage);
            if (!IsDead) Respawn();
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>Directly sets health (used to carry vitality across a level transition).</summary>
    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        IsDead = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Respawn()
    {
        if (respawnPoint == null) return;
        Vector3 pos = respawnPoint.position;

        if (_cc != null)
        {
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = true;
        }
        else if (_rb != null)
        {
            _rb.position = pos;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.position = pos;
        }
        else
        {
            transform.position = pos;
        }
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;
        OnHealthChanged?.Invoke(0f, maxHealth);
        OnDeath?.Invoke();
    }
}
