using UnityEngine;
using UnityEngine.InputSystem;

public class Shot : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject bullet;
    [SerializeField] private Transform spawnPoint;

    [Header("Disparo")]
    [SerializeField] private float shotForce = 1500f;
    [SerializeField] private float shotRate = 0.5f; // segundos entre disparos
    private float nextShotTime = 0f;

    private InputAction fireAction;

    private void Awake()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        fireAction = playerInput.actions["Fire"];
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
        GameObject newBullet = Instantiate(bullet, spawnPoint.position, spawnPoint.rotation);
        newBullet.GetComponent<Rigidbody>().AddForce(spawnPoint.forward * shotForce);
        Destroy(newBullet, 2f);
    }
}