using System;
using System.Collections;
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

    private void Update()
    {
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

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
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
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        IsReloading = true;
        PlaySound(reloadSound);

        yield return new WaitForSeconds(reloadTime);

        int needed = magazineSize - Magazine;
        int taken = Mathf.Min(needed, Reserve);
        Magazine += taken;
        Reserve -= taken;

        IsReloading = false;
        Notify();
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
        Magazine = magazineSize;
        Reserve = maxReserve;
        Notify();
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
