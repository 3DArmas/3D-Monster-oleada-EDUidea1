using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private float shotForce = 1500f;
    [Tooltip("Segundos entre disparos (GDD pistola: 0.25 = 4 disparos por segundo).")]
    [SerializeField] private float shotRate = 0.25f;
    [SerializeField] private float maxAimDistance = 100f;

    [Header("Recarga")]
    [Tooltip("Recarga sola al quedarse sin balas.")]
    [SerializeField] private bool autoReloadWhenEmpty = true;

    [Header("Sonido")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioSource audioSource; // asigna un AudioSource (puede estar en esta misma arma)

    private InputAction fireAction;
    private float nextShotTime = 0f;

    private void Awake()
    {
        PlayerInput playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput != null) fireAction = playerInput.actions["Fire"];

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (ammo == null)
            ammo = GetComponent<WeaponAmmo>();
    }

    private void Update()
    {
        // Recargar con R. (De momento leido directo del teclado; se movera a las
        // acciones del proyecto cuando se revise el mapa de input en la Fase 3.)
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            Reload();

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
    }

    /// <summary>Pide la recarga al sistema de municion del arma.</summary>
    public void Reload()
    {
        if (ammo != null) ammo.StartReload();
    }

    private void Shoot()
    {
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

        GameObject newBullet = Instantiate(bullet, spawnPoint.position, Quaternion.LookRotation(shootDirection));
        newBullet.GetComponent<Rigidbody>().AddForce(shootDirection * shotForce);
        Destroy(newBullet, 2f);

        if (shootSound != null && audioSource != null)
            audioSource.PlayOneShot(shootSound);
    }
}