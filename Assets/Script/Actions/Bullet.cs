using UnityEngine;

/// <summary>
/// Bala fisica. Al chocar aplica dano al IDamageable del objeto golpeado y se destruye.
/// El dano lo fija el arma que la dispara (ver Shot).
/// </summary>
public class Bullet : MonoBehaviour
{
    [Header("Dano")]
    [Tooltip("Lo sobreescribe el arma al disparar (pistola 25, rifle 45).")]
    [SerializeField] private float damage = 25f;

    [Header("Vida")]
    [SerializeField] private float lifeTime = 3f;

    private bool hasHit;

    public void SetDamage(float value)
    {
        damage = Mathf.Max(0f, value);
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider other)
    {
        // Una bala hace dano UNA sola vez (Destroy tarda hasta el final del frame).
        if (hasHit || other == null) return;

        // La bala nace dentro del cuerpo del jugador: nunca debe hacerle dano.
        if (other.GetComponentInParent<PlayerHealth>() != null) return;

        hasHit = true;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null) target.TakeDamage(damage);

        Destroy(gameObject);
    }
}
