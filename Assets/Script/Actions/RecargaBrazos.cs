using UnityEngine;

/// <summary>
/// ANIMACION DE RECARGA DE LAS MANOS.
///
/// Antes esto escuchaba la tecla R por su cuenta: las manos hacían la recarga del rifle
/// aunque llevaras el cuchillo, o aunque el cargador estuviera lleno y no se recargara
/// nada. Ahora sigue el estado REAL de la munición del arma equipada:
///
///   - Solo anima cuando el arma equipada tiene munición y está recargando de verdad.
///   - Con el cuchillo en la mano no hay recarga, así que las manos no hacen nada.
///   - La velocidad del clip se ajusta para que dure lo mismo que la recarga del arma
///     (la pistola recarga en 1,35 s y el rifle en 1,85 s: las manos van a ese ritmo).
///
/// El arma además tiene su propia animación (WeaponAnim), que la hunde y la gira un poco
/// mientras las manos trabajan el cargador.
/// </summary>
public class RecargaBrazos : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Parte final del nombre del clip de recarga.")]
    [SerializeField] private string nombreClipRecarga = "RELOAD1";

    private static readonly int ReloadHash = Animator.StringToHash("Reload");

    private WeaponSwitcher selector;
    private WeaponAmmo municionActual;
    private float duracionClip = 2.2f;
    private bool recargabaAntes;

    /// <summary>True mientras el arma equipada está recargando.</summary>
    public bool Recargando => municionActual != null && municionActual.IsReloading;

    private void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        selector = GetComponentInParent<WeaponSwitcher>();
        if (selector == null) selector = FindFirstObjectByType<WeaponSwitcher>();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name.EndsWith(nombreClipRecarga)) { duracionClip = clip.length; break; }
        }
    }

    private void Update()
    {
        // Qué arma lleva el jugador ahora mismo. El cuchillo no tiene munición.
        GameObject arma = selector != null ? selector.ArmaActual : null;
        municionActual = arma != null ? arma.GetComponent<WeaponAmmo>() : null;

        bool recargando = municionActual != null && municionActual.IsReloading;

        if (recargando && !recargabaAntes) EmpezarRecarga();
        else if (!recargando && recargabaAntes) TerminarRecarga();

        recargabaAntes = recargando;
    }

    private void EmpezarRecarga()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // El clip dura lo suyo; se estira o se encoge para durar lo mismo que la recarga
        // real de ese arma. Así el cargador entra justo cuando el arma ya puede disparar.
        float duracionReal = Mathf.Max(0.2f, municionActual.ReloadDuration);
        animator.speed = Mathf.Clamp(duracionClip / duracionReal, 0.6f, 2.5f);

        animator.SetTrigger(ReloadHash);
    }

    private void TerminarRecarga()
    {
        if (animator != null) animator.speed = 1f;
    }

    private void OnDisable()
    {
        recargabaAntes = false;
        if (animator != null) animator.speed = 1f;
    }
}
