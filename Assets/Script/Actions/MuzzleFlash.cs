using UnityEngine;

/// <summary>
/// Fogonazo del cañón: enciende una luz y un destello durante unas centésimas al
/// disparar. Se coloca en un objeto DENTRO del arma (hijo de su SpawnPoint).
///
/// OJO: el objeto se queda siempre activo y lo que se enciende y apaga es el
/// render y la luz. Si se desactivara el objeto, al volver a activarlo se
/// perdería la suscripción al evento de disparo.
/// </summary>
public class MuzzleFlash : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Renderer destello;
    [SerializeField] private Light luz;

    [Header("Ajustes")]
    [SerializeField] private float duracion = 0.045f;
    [SerializeField] private float intensidadLuz = 3.2f;
    [SerializeField] private float alcanceLuz = 5f;
    [SerializeField] private float escalaMin = 0.7f;
    [SerializeField] private float escalaMax = 1.15f;

    private float apagarEn;
    private float escalaBase = 1f;
    private bool encendido;

    private void Awake()
    {
        if (destello == null) destello = GetComponentInChildren<Renderer>(true);
        if (luz == null) luz = GetComponentInChildren<Light>(true);
        if (destello != null) escalaBase = destello.transform.localScale.x;
        Apagar();
    }

    private void OnEnable() { Shot.OnAnyShot += AlDisparar; }
    private void OnDisable() { Shot.OnAnyShot -= AlDisparar; }

    private void Update()
    {
        if (encendido && Time.time >= apagarEn) Apagar();
    }

    private void AlDisparar()
    {
        // Solo el arma que esta en la mano (la otra tiene el objeto inactivo).
        if (!gameObject.activeInHierarchy) return;

        encendido = true;
        apagarEn = Time.time + duracion;

        if (destello != null)
        {
            destello.enabled = true;
            float escala = Random.Range(escalaMin, escalaMax) * escalaBase;
            destello.transform.localScale = new Vector3(escala, escala, escala);
            destello.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }
        if (luz != null)
        {
            luz.enabled = true;
            luz.intensity = intensidadLuz * Random.Range(0.85f, 1.15f);
            luz.range = alcanceLuz;
        }
    }

    private void Apagar()
    {
        encendido = false;
        if (destello != null) destello.enabled = false;
        if (luz != null) luz.enabled = false;
    }
}
