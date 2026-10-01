using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    private bool isDead;

    /// <summary>Se dispara una sola vez, cuando la vida llega a 0.</summary>
    public event Action OnDied;

    /// <summary>Se dispara cuando cambia la vida: (actual, maxima). Lo usa el HUD.</summary>
    public event Action<float, float> OnHealthChanged;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f) Die();
    }

    /// <summary>Cura sin pasar del maximo (botiquin).</summary>
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>Sube la vida maxima y cura esa misma cantidad (mejora de tienda: +25 HP).</summary>
    public void IncreaseMaxHealth(float amount, bool healSameAmount = true)
    {
        if (amount <= 0f) return;

        maxHealth += amount;
        if (healSameAmount) currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;

        OnDied?.Invoke();
    }
}
