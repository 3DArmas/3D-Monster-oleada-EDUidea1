using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;
    }

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
}
