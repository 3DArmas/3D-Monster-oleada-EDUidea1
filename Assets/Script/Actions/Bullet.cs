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
        HandleHit(collision);
    }

    private void HandleHit(Collision collision)
    {
        Collider other = collision.collider;

        // Una bala hace dano UNA sola vez (Destroy tarda hasta el final del frame).
        if (hasHit || other == null) return;

        // La bala nace dentro del cuerpo del jugador: nunca debe hacerle dano.
        if (other.GetComponentInParent<PlayerHealth>() != null) return;

        hasHit = true;

        // ZONA DE GOLPE: si le has dado a la cabeza (o a una extremidad), el dano se
        // multiplica. El multiplicador lo pone el componente ZonaDeGolpe.
        float multiplicador = 1f;
        ZonaDeGolpe zona = other.GetComponent<ZonaDeGolpe>();
        if (zona == null) zona = other.GetComponentInParent<ZonaDeGolpe>();
        if (zona != null) multiplicador = zona.Multiplicador;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null) target.TakeDamage(damage * multiplicador);

        // SANGRE justo en el punto del impacto, saliendo hacia fuera. Solo los zombis sangran.
        if (other.GetComponentInParent<ZombieController>() != null && collision.contactCount > 0)
        {
            ContactPoint contacto = collision.GetContact(0);
            SangreDeZombi.Salpicar(contacto.point, contacto.normal);
        }

        Destroy(gameObject);
    }
}
