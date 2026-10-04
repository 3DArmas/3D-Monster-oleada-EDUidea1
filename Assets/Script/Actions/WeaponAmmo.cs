using System;
using UnityEngine;

/// <summary>
/// Municion de un arma: cargador, reserva y recarga.
/// Se coloca en el mismo objeto que <see cref="Shot"/>.
/// Valores del GDD v1.1: pistola 12 / 96 / 1.8 s.
/// </summary>
public class WeaponAmmo : MonoBehaviour
{
    [Header("Identidad")]
    [SerializeField] private string weaponName = "PISTOLA";

    [Header("Municion")]
    [SerializeField] private int magazineSize = 12;
    [SerializeField] private int startingReserve = 96;
    [SerializeField] private int maxReserve = 96;
    [SerializeField] private float reloadTime = 1.8f;

    [Header("Sonido")]
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip emptySound;
    [SerializeField] private AudioSource audioSource;

    public string WeaponName => weaponName;
    public int Magazine { get; private set; }
    public int Reserve { get; private set; }
    public int MagazineSize => magazineSize;
    public int MaxReserve => maxReserve;
    public bool IsReloading { get; private set; }
    public bool IsMagazineFull => Magazine >= magazineSize;
    public bool IsFull => IsMagazineFull && Reserve >= maxReserve;

    /// <summary>Se dispara con (cargador, reserva) cada vez que cambia la municion.</summary>
    public event Action<int, int> OnAmmoChanged;
    /// <summary>Se dispara cuando se intenta disparar sin balas.</summary>
    public event Action OnEmptyTrigger;

    [Header("Recarga automatica")]
    [Tooltip("Recarga sola en cuanto el cargador se queda vacio.")]
    [SerializeField] private bool autoReloadWhenEmpty = true;
    [Tooltip("Retardo antes de recargar sola (deja sonar el click seco).")]
    [SerializeField] private float autoReloadDelay = 0.25f;

    private float emptySince = -1f;

    // La recarga NO puede ser una corrutina: al cambiar de arma, WeaponSwitcher
    // desactiva el GameObject y la corrutina moria a medias, dejando IsReloading en
    // true para siempre (pose de recarga congelada y el arma sin poder disparar).
    // Con una marca de tiempo el estado es claro y se puede cerrar o cancelar.
    private float recargaTerminaEn = -1f;

    private void Update()
    {
        // Cerrar la recarga en curso aunque el arma se haya guardado mientras recargaba.
        if (IsReloading && Time.time >= recargaTerminaEn) TerminarRecarga();

        if (!autoReloadWhenEmpty) return;

        if (IsReloading || Magazine > 0 || Reserve <= 0)
        {
            emptySince = -1f;
            return;
        }

        if (emptySince < 0f)
        {
            emptySince = Time.time;
            return;
        }

        if (Time.time - emptySince >= autoReloadDelay)
        {
            emptySince = -1f;
            StartReload();
        }
    }

    private void OnDisable()
    {
        // Al guardar el arma se CANCELA la recarga en curso. Es lo correcto por dos
        // motivos: nada de poses de recarga congeladas al volver a sacarla, y nada de
        // "recargas zombis" (cambiar de arma no puede servir para saltarse la recarga).
        // Un GameObject desactivado NO ejecuta Update, asi que cerrar la recarga aqui
        // es la unica forma de que nunca se quede a medias.
        CancelReload();
    }

    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        // Un arma que empieza desactivada no ejecuta Awake hasta que se equipa:
        // inicializamos tambien aqui para que nunca arranque con 0 balas.
        Initialize();
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;

        Magazine = magazineSize;
        Reserve = Mathf.Clamp(startingReserve, 0, maxReserve);

        // Igual que en Shot: si la referencia apunta al AudioSource de OTRA arma (pasa al
        // copiar el componente), los sonidos de recarga y de cargador vacio no suenan
        // porque ese objeto esta desactivado.
        if (audioSource == null || audioSource.gameObject != gameObject)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        Notify();
    }

    /// <summary>Consume una bala. Devuelve false si no habia (dispara el click seco).</summary>
    public bool TryConsume()
    {
        if (IsReloading) return false;

        if (Magazine <= 0)
        {
            PlaySound(emptySound);
            OnEmptyTrigger?.Invoke();
            return false;
        }

        Magazine--;
        Notify();
        return true;
    }

    public void StartReload()
    {
        if (IsReloading || IsMagazineFull || Reserve <= 0) return;

        IsReloading = true;
        recargaTerminaEn = Time.time + reloadTime;
        PlaySound(reloadSound);
    }

    /// <summary>La recarga ha llegado a su fin: se pasa la municion de la reserva.</summary>
    private void TerminarRecarga()
    {
        int needed = magazineSize - Magazine;
        int taken = Mathf.Min(needed, Reserve);
        Magazine += taken;
        Reserve -= taken;

        IsReloading = false;
        recargaTerminaEn = -1f;
        Notify();
    }

    /// <summary>Cancela la recarga en curso (reinicio de ronda, relleno completo...).</summary>
    public void CancelReload()
    {
        if (!IsReloading) return;
        IsReloading = false;
        recargaTerminaEn = -1f;
    }

    /// <summary>Anade municion a la reserva (compras de tienda). Devuelve lo que realmente entro.</summary>
    public int AddReserve(int amount)
    {
        if (amount <= 0) return 0;

        int before = Reserve;
        Reserve = Mathf.Min(maxReserve, Reserve + amount);
        int added = Reserve - before;

        if (added > 0) Notify();
        return added;
    }

    /// <summary>Rellena cargador y reserva al maximo (botiquin de municion / reinicio).</summary>
    public void RefillAll()
    {
        CancelReload();
        Magazine = magazineSize;
        Reserve = maxReserve;
        Notify();
    }

    /// <summary>La tienda acorta el tiempo de recarga (0.75 = 25 % mas rapido).</summary>
    public void MejorarRecarga(float factor)
    {
        reloadTime = Mathf.Max(0.2f, reloadTime * factor);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    private void Notify()
    {
        OnAmmoChanged?.Invoke(Magazine, Reserve);
    }
}
