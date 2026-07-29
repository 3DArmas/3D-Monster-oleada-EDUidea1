using UnityEngine;
using UnityEngine.InputSystem;

public class Shot : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject bullet;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Camera playerCamera; // asigna aquí tu Main Camera (hija de CameraHolder)

    [Header("Disparo")]
    [SerializeField] private float shotForce = 1500f;
    [SerializeField] private float shotRate = 0.5f; // segundos entre disparos
    [SerializeField] private float maxAimDistance = 100f;
    private float nextShotTime = 0f;

    [Header("Sonido")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioSource audioSource; // asigna un AudioSource (puede estar en esta misma arma)

    private InputAction fireAction;

    private void Awake()
    {
        PlayerInput playerInput = GetComponentInParent<PlayerInput>();
        fireAction = playerInput.actions["Fire"];

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (fireAction.IsPressed() && Time.time >= nextShotTime)
        {
            Shoot();
            nextShotTime = Time.time + shotRate;
        }
    }

    private void Shoot()
    {
        // 1. Calculamos hacia dónde apunta el centro de la cámara (la mira)
        Vector3 aimPoint;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance))
        {
            aimPoint = hit.point; // le pegó a algo: apuntamos exactamente ahí
        }
        else
        {
            aimPoint = ray.GetPoint(maxAimDistance); // no le pegó a nada: apuntamos lejos en esa dirección
        }

        // 2. La bala nace en la punta del arma, pero su dirección va hacia aimPoint
        Vector3 shootDirection = (aimPoint - spawnPoint.position).normalized;

        GameObject newBullet = Instantiate(bullet, spawnPoint.position, Quaternion.LookRotation(shootDirection));
        newBullet.GetComponent<Rigidbody>().AddForce(shootDirection * shotForce);
        Destroy(newBullet, 2f);

        if (shootSound != null && audioSource != null)
            audioSource.PlayOneShot(shootSound);
    }
}