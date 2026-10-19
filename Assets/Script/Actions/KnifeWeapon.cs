using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Ataque cuerpo a cuerpo del cuchillo.
///
/// No usa balas ni municion: al pulsar el boton de disparo lanza un golpe instantaneo
/// desde el centro de la camara (la mira) a corta distancia y aplica el dano al
/// IDamageable que encuentre. Respeta las ZonaDeGolpe del zombi, igual que las balas.
///
/// Usa la MISMA accion "Fire" que las armas de fuego, asi el mismo boton sirve para
/// todo y no hay que aprender controles nuevos. Se coloca en el prefab del cuchillo,
/// que el WeaponSwitcher cuelga del ArmaSocket como cualquier otra arma.
/// </summary>
public class KnifeWeapon : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Camara del jugador. Si se deja vacia se busca la principal.")]
    [SerializeField] private Camera playerCamera;

    [Header("Golpe")]
    [Tooltip("Dano por cuchillada. 65 mata a un zombi de 100 en dos golpes.")]
    [SerializeField] private float damage = 65f;
    [Tooltip("Alcance del cuchillo en metros (mas alla no corta).")]
    [SerializeField] private float alcance = 2.2f;
    [Tooltip("Segundos entre cuchilladas.")]
    [SerializeField] private float ataqueRate = 0.45f;
    [Tooltip("Radio del golpe: un cuchillo corta un volumen, no un punto.")]
    [SerializeField] private float radioGolpe = 0.35f;

    [Header("Animacion del golpe")]
    [Tooltip("Grados que gira el cuchillo al cortar (eje X local).")]
    [SerializeField] private float anguloCorte = 55f;
    [SerializeField] private float duracionCorte = 0.18f;

    [Header("Sonido")]
    [SerializeField] private AudioClip swingSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioSource audioSource;

    private InputAction fireAction;
    private float nextAttackTime;
    private float corteInicio = -10f;
    private Quaternion rotacionBase;
    private Vector3 posicionBase;
    private bool baseGuardada;
    private WeaponAnim animacion;

    /// <summary>Nombre que muestra el HUD para el arma cuerpo a cuerpo.</summary>
    public string WeaponName => "CUCHILLO";

    private void Awake()
    {
        PlayerInput playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput != null) fireAction = playerInput.actions.FindAction("Fire", false);

        // Igual que en Shot/WeaponAmmo: el AudioSource tiene que ser el de ESTE arma.
        if (audioSource == null || audioSource.gameObject != gameObject)
            audioSource = GetComponent<AudioSource>();

        if (playerCamera == null) playerCamera = Camera.main;

        // Si el cuchillo lleva WeaponAnim, el arco del corte lo dibuja ese componente
        // (y ademas puede sumar el saque del arma). Si no, se usa el de aqui.
        animacion = GetComponent<WeaponAnim>();
    }

    private void Start()
    {
        // La pose base se toma en Start, DESPUES de que el WeaponSwitcher haya colocado
        // el cuchillo en el socket (eso ocurre en su Awake, al instanciarlo).
        rotacionBase = transform.localRotation;
        posicionBase = transform.localPosition;
        baseGuardada = true;
    }

    private void Update()
    {
        AnimarCorte();

        // Con la interfaz al mando (pausa, tienda, game over) el clic no corta.
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (PauseMenu.IsPaused) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (ShopUI.IsAnyOpen || ShopInteractable.PointerOnShop) return;

        if (Time.time < nextAttackTime) return;
        if (fireAction == null || !fireAction.IsPressed()) return;

        nextAttackTime = Time.time + ataqueRate;
        Atacar();
    }

    /// <summary>Lanza la cuchillada desde el centro de la camara.</summary>
    private void Atacar()
    {
        corteInicio = Time.time;
        if (animacion != null) animacion.Tajo();
        if (swingSound != null && audioSource != null) audioSource.PlayOneShot(swingSound);

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // SphereCast: el cuchillo corta una zona, no un punto exacto.
        if (!Physics.SphereCast(ray, radioGolpe, out RaycastHit hit, alcance, ~0, QueryTriggerInteraction.Ignore))
            return;

        Collider other = hit.collider;
        if (other == null) return;

        // El golpe nace dentro del jugador: nunca debe cortarse a si mismo.
        if (other.GetComponentInParent<PlayerHealth>() != null) return;

        // Multiplicador por zona (cabeza 2.5, extremidad 0.75), igual que las balas.
        float multiplicador = 1f;
        ZonaDeGolpe zona = other.GetComponent<ZonaDeGolpe>();
        if (zona == null) zona = other.GetComponentInParent<ZonaDeGolpe>();
        if (zona != null) multiplicador = zona.Multiplicador;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null) return;

        target.TakeDamage(damage * multiplicador);
        if (hitSound != null && audioSource != null) audioSource.PlayOneShot(hitSound);

        if (other.GetComponentInParent<ZombieController>() != null)
            SangreDeZombi.Salpicar(hit.point, hit.normal);
    }

    /// <summary>Golpe visual: el cuchillo corta en diagonal y vuelve a su pose.</summary>
    private void AnimarCorte()
    {
        if (!baseGuardada) return;

        // El arco lo lleva WeaponAnim (LateUpdate): aqui no se toca la transformada.
        if (animacion != null) return;

        float t = (Time.time - corteInicio) / Mathf.Max(0.01f, duracionCorte);
        if (t >= 1f)
        {
            if (transform.localRotation != rotacionBase) transform.localRotation = rotacionBase;
            if (transform.localPosition != posicionBase) transform.localPosition = posicionBase;
            return;
        }

        // Entra rapido y vuelve suave (una cuchillada, no un giro continuo).
        float curva = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        transform.localRotation = rotacionBase * Quaternion.Euler(anguloCorte * curva, 0f, anguloCorte * 0.35f * curva);
        transform.localPosition = posicionBase + new Vector3(0f, 0f, -0.06f * curva);
    }
}
