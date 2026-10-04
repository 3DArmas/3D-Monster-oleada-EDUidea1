using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Disparo del arma: apunta al centro de la camara y lanza una bala fisica.
/// Consume municion de WeaponAmmo si el arma la tiene.
/// </summary>
public class Shot : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject bullet;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Camera playerCamera; // asigna aqui tu Main Camera (hija de CameraHolder)
    [Tooltip("Municion del arma. Si se deja vacio se busca en este mismo objeto.")]
    [SerializeField] private WeaponAmmo ammo;

    [Header("Disparo")]
    [Tooltip("Velocidad de la bala en m/s. Se le da directamente (con AddForce la bala arrancaba parada y fallaba).")]
    [SerializeField] private float bulletSpeed = 60f;
    [Tooltip("Segundos entre disparos (GDD pistola: 0.25 = 4 disparos por segundo).")]
    [SerializeField] private float shotRate = 0.25f;
    [SerializeField] private float maxAimDistance = 100f;

    [Header("Dano")]
    [Tooltip("Dano por bala. GDD: pistola 25, rifle 45 (4 disparos para matar a un zombie de 100).")]
    [SerializeField] private float damage = 25f;

    [Header("Recarga")]
    [Tooltip("Recarga sola al quedarse sin balas.")]
    [SerializeField] private bool autoReloadWhenEmpty = true;

    [Header("Sonido")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioSource audioSource; // asigna un AudioSource (puede estar en esta misma arma)

    private InputAction fireAction;
    private InputAction reloadAction;
    private float nextShotTime = 0f;

    /// <summary>Avisa cada vez que un arma dispara (lo usan el retroceso y el fogonazo).</summary>
    public static event System.Action OnAnyShot;

    /// <summary>La tienda sube el dano de este arma (1.10 = +10 %).</summary>
    public void MejorarDano(float factor)
    {
        damage *= factor;
    }

    private void Awake()
    {
        PlayerInput playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput != null)
        {
            fireAction = playerInput.actions.FindAction("Fire");
            reloadAction = playerInput.actions.FindAction("Reload");
        }

        // El AudioSource tiene que ser el de ESTA arma. Al copiar el Shot de otra arma la
        // referencia puede quedar apuntando al arma original; y como esa arma esta
        // desactivada cuando no es la equipada, su PlayOneShot no suena: el arma dispara
        // en silencio. Por eso no basta con comprobar si es null.
        if (audioSource == null || audioSource.gameObject != gameObject)
            audioSource = GetComponent<AudioSource>();

        if (ammo == null)
            ammo = GetComponent<WeaponAmmo>();

        // El punto de disparo tiene que ser un hijo DE ESTE prefab. Al copiar el Shot de
        // otra arma la referencia apunta fuera y se pierde al guardar, asi que si falta
        // se busca el hijo llamado SPANWPOINT y, en ultimo caso, el propio arma.
        if (spawnPoint == null)
        {
            foreach (Transform hijo in GetComponentsInChildren<Transform>(true))
                if (hijo.name == "SPANWPOINT" || hijo.name == "SpawnPoint") { spawnPoint = hijo; break; }
        }
        if (spawnPoint == null) spawnPoint = transform;

        // Las armas ahora son PREFABS y un prefab no puede guardar la referencia a la
        // camara de la escena (se perderia al guardarlo). Asi que el arma se busca la
        // camara sola. Sin esto, Shoot() avisaba de que playerCamera no estaba asignada
        // y el arma no disparaba.
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        // Si la INTERFAZ tiene el control (tienda, pausa o game over), el clic no dispara.
        // Esto era lo que hacía que el botón REINTENTAR disparase el arma.
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Recargar: accion "Reload" del asset de input (tecla R), igual que Fire.
        // Antes se leia el teclado directo y no llegaba a detectarse la pulsacion.
        bool reloadPressed = reloadAction != null
            ? reloadAction.WasPressedThisFrame()
            : (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);

        if (reloadPressed) Reload();

        // Con la tienda abierta, o apuntando al cubo de la tienda, el clic sirve
        // para interactuar: no se dispara.
        if (ShopUI.IsAnyOpen || ShopInteractable.PointerOnShop) return;

        if (fireAction == null || !fireAction.IsPressed()) return;
        if (Time.time < nextShotTime) return;

        if (ammo != null && !ammo.TryConsume())
        {
            // Click seco: pequena pausa para no spamear el sonido.
            nextShotTime = Time.time + 0.2f;
            if (autoReloadWhenEmpty) Reload();
            return;
        }

        Shoot();
        nextShotTime = Time.time + shotRate;
        OnAnyShot?.Invoke();
    }

    /// <summary>Pide la recarga al sistema de municion del arma.</summary>
    public void Reload()
    {
        if (ammo != null) ammo.StartReload();
    }

    private void Shoot()
    {
        // Red de seguridad: si el arma se creo en tiempo de ejecucion y aun no tiene
        // camara, se busca aqui para no perder el disparo.
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return;

        // 1. Calculamos hacia donde apunta el centro de la camara (la mira)
        Vector3 aimPoint;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance))
        {
            aimPoint = hit.point; // le pego a algo: apuntamos exactamente ahi
        }
        else
        {
            aimPoint = ray.GetPoint(maxAimDistance); // no le pego a nada: apuntamos lejos
        }

        // 2. La bala nace en la punta del arma, pero su direccion va hacia aimPoint
        Vector3 shootDirection = (aimPoint - spawnPoint.position).normalized;

        // 3. La bala nace un poco por delante del muzzle y sale con velocidad inmediata
        GameObject newBullet = Instantiate(bullet, spawnPoint.position + shootDirection * 0.2f, Quaternion.LookRotation(shootDirection));

        Rigidbody rb = newBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = shootDirection * bulletSpeed;
        }

        // 4. La bala aplica el dano del arma al chocar con un IDamageable
        Bullet bulletLogic = newBullet.GetComponent<Bullet>();
        if (bulletLogic != null) bulletLogic.SetDamage(damage);

        Destroy(newBullet, 3f);

        if (shootSound != null && audioSource != null)
            audioSource.PlayOneShot(shootSound);
    }
}