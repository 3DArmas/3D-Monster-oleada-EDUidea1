using UnityEngine;

/// <summary>
/// Da "vida" al arma en primera persona: se balancea al mover la mirada (sway),
/// se mueve al caminar (bob), retrocede al disparar (recoil), baja al recargar y
/// da un pequeño golpe a la camara.
///
/// Se coloca en el objeto que CONTIENE las armas (WeaponHolder), que siempre esta
/// activo: asi vale para todas las armas a la vez.
/// </summary>
public class WeaponFeel : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Camara de la que se lee el giro. Si se deja vacio se busca la principal.")]
    [SerializeField] private Transform camara;

    [Header("Sway (al girar la mirada)")]
    [SerializeField] private float swayPosicion = 0.030f;
    [SerializeField] private float swayRotacion = 2.2f;
    [SerializeField] private float swayLimite = 0.05f;
    [SerializeField] private float swaySuavizado = 7f;

    [Header("Bob (al caminar)")]
    [SerializeField] private float bobFrecuencia = 8.5f;
    [SerializeField] private float bobVertical = 0.011f;
    [SerializeField] private float bobLateral = 0.008f;
    [SerializeField] private float bobVelocidadMinima = 0.4f;

    [Header("Retroceso al disparar")]
    [SerializeField] private Vector3 retrocesoPosicion = new Vector3(0f, 0.010f, -0.055f);
    [SerializeField] private Vector3 retrocesoRotacion = new Vector3(-3.2f, 0.9f, 0f);
    [SerializeField] private float retrocesoVuelta = 10f;
    [Tooltip("Golpecito de la camara hacia atras al disparar.")]
    [SerializeField] private float golpeCamara = 0.012f;

    [Header("Recarga")]
    [SerializeField] private Vector3 recargaPosicion = new Vector3(0f, -0.10f, 0.03f);
    [SerializeField] private Vector3 recargaRotacion = new Vector3(26f, 0f, 7f);
    [SerializeField] private float recargaVelocidad = 7f;

    private Vector3 posicionBase;
    private Quaternion rotacionBase;
    private Vector3 posicionCamaraBase;

    private Vector3 swayActual;
    private float faseBob;
    private Vector3 retroceso;
    private Vector3 retrocesoRot;
    private float golpeActual;
    private float yawAnterior;
    private float pitchAnterior;
    private float recargaMezcla;

    private CharacterController controlador;
    private WeaponAmmo arma;
    private float siguienteBusqueda;

    private void Start()
    {
        posicionBase = transform.localPosition;
        rotacionBase = transform.localRotation;
        if (camara == null && Camera.main != null) camara = Camera.main.transform;
        if (camara != null) posicionCamaraBase = camara.localPosition;
        if (camara != null)
        {
            yawAnterior = transform.root.eulerAngles.y;
            pitchAnterior = camara.localEulerAngles.x;
        }
        BuscarJugador();
    }

    private void OnEnable()
    {
        Shot.OnAnyShot += AlDisparar;
    }

    private void OnDisable()
    {
        Shot.OnAnyShot -= AlDisparar;
    }

    private void BuscarJugador()
    {
        controlador = GetComponentInParent<CharacterController>();
        arma = GetComponentInParent<WeaponAmmo>();
        if (arma == null)
        {
            foreach (WeaponAmmo w in FindObjectsByType<WeaponAmmo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (w.gameObject.activeInHierarchy) { arma = w; break; }
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // --- Sway: la mirada arrastra el arma ---
        float dYaw = 0f, dPitch = 0f;
        if (camara != null)
        {
            float yaw = transform.root.eulerAngles.y;
            float pitch = camara.localEulerAngles.x;
            dYaw = Mathf.DeltaAngle(yawAnterior, yaw);
            dPitch = Mathf.DeltaAngle(pitchAnterior, pitch);
            yawAnterior = yaw;
            pitchAnterior = pitch;
        }
        Vector3 swayObjetivo = new Vector3(
            Mathf.Clamp(-dYaw * swayPosicion * 0.1f, -swayLimite, swayLimite),
            Mathf.Clamp(-dPitch * swayPosicion * 0.1f, -swayLimite, swayLimite),
            0f);
        swayActual = Vector3.Lerp(swayActual, swayObjetivo, dt * swaySuavizado);

        // --- Bob: el arma acompaña los pasos ---
        float velocidad = 0f;
        if (controlador != null)
        {
            Vector3 v = controlador.velocity;
            v.y = 0f;
            velocidad = v.magnitude;
        }
        float bobX = 0f, bobY = 0f;
        if (velocidad > bobVelocidadMinima)
        {
            faseBob += dt * bobFrecuencia * Mathf.Clamp(velocidad / 5f, 0.6f, 1.6f);
            bobX = Mathf.Cos(faseBob) * bobLateral;
            bobY = Mathf.Abs(Mathf.Sin(faseBob)) * bobVertical;
        }
        else
        {
            faseBob = Mathf.Lerp(faseBob, Mathf.Round(faseBob / Mathf.PI) * Mathf.PI, dt * 6f);
        }

        // --- Recarga ---
        bool recargando = arma != null && arma.IsReloading;
        if (recargando && arma == null) recargando = false;
        if (arma == null && Time.time > siguienteBusqueda) { siguienteBusqueda = Time.time + 0.5f; BuscarJugador(); }
        recargaMezcla = Mathf.Lerp(recargaMezcla, recargando ? 1f : 0f, dt * recargaVelocidad);

        // --- Retroceso ---
        retroceso = Vector3.Lerp(retroceso, Vector3.zero, dt * retrocesoVuelta);
        retrocesoRot = Vector3.Lerp(retrocesoRot, Vector3.zero, dt * retrocesoVuelta);
        golpeActual = Mathf.Lerp(golpeActual, 0f, dt * retrocesoVuelta);

        // --- Aplicar todo ---
        Vector3 posicion = posicionBase + swayActual + new Vector3(bobX, bobY, 0f) + retroceso
                         + recargaPosicion * recargaMezcla;
        Quaternion rotacion = rotacionBase
                            * Quaternion.Euler(-dPitch * swayRotacion * 0.1f, dYaw * swayRotacion * 0.1f, 0f)
                            * Quaternion.Euler(retrocesoRot)
                            * Quaternion.Euler(recargaRotacion * recargaMezcla);
        transform.localPosition = posicion;
        transform.localRotation = rotacion;

        if (camara != null) camara.localPosition = posicionCamaraBase + new Vector3(0f, 0f, -golpeActual);
    }

    private void AlDisparar()
    {
        retroceso += retrocesoPosicion;
        retrocesoRot += retrocesoRotacion;
        golpeActual = Mathf.Min(golpeActual + golpeCamara, golpeCamara * 2.5f);
    }
}
