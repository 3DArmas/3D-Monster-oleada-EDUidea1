using UnityEngine;

/// <summary>
/// ANIMACION DEL ARMA EN LAS MANOS (por arma).
///
/// Las manos son un solo esqueleto para todas las armas, asi que una unica animacion
/// de recarga (la del rifle) se veia mal en la pistola y no servia para el cuchillo.
/// Este componente le da a CADA arma su propia animacion, ajustada a su forma:
///
///   - SAQUE: al equiparla, el arma sube desde abajo hasta la pose de las manos.
///   - RETROCESO: patada corta hacia atras y hacia arriba en cada disparo.
///   - RECARGA: la pistola baja y gira menos y mas rapido que el rifle; la escopeta
///     bascula; el sniper se hunde mas. Se dispara sola cuando WeaponAmmo recarga.
///   - CUCHILLADA: el arco del cuchillo (lo llama KnifeWeapon).
///
/// Todo se aplica en LateUpdate, DESPUES de WeaponSwitcher y de KnifeWeapon, para que
/// nadie pelee por la transformada. La pose base se toma en Start porque es cuando el
/// WeaponSwitcher ya ha colocado el arma en el socket.
/// </summary>
public class WeaponAnim : MonoBehaviour
{
    public enum TipoArma { Pistola, Rifle, Escopeta, Subfusil, Sniper, Cuchillo }

    [Header("Tipo de arma (define su animacion)")]
    [SerializeField] private TipoArma tipo = TipoArma.Rifle;

    [Header("Saque del arma")]
    [Tooltip("Segundos que tarda el arma en subir desde abajo al equiparla.")]
    [SerializeField] private float tiempoDeSaque = 0.22f;
    [Tooltip("Cuanto empieza de hundida (metros, eje Z del socket = abajo).")]
    [SerializeField] private float caidaAlSacar = 0.14f;
    [Tooltip("Grados de inclinacion con los que entra el arma.")]
    [SerializeField] private float inclinacionAlSacar = 18f;

    [Header("Retroceso del disparo")]
    [Tooltip("Cuanto retrocede el arma (metros, eje Y del socket = adelante).")]
    [SerializeField] private float retrocesoAtras = 0.032f;
    [Tooltip("Grados que sube la punta en el retroceso.")]
    [SerializeField] private float retrocesoArriba = 5f;
    [Tooltip("Cuanto mas grande, mas seco vuelve a su sitio.")]
    [SerializeField] private float retrocesoDureza = 17f;

    [Header("Recarga")]
    [Tooltip("Cuanto baja el arma (metros).")]
    [SerializeField] private float recargaBajada = 0.055f;
    [Tooltip("Cuanto se acerca a la camara (metros).")]
    [SerializeField] private float recargaAtras = 0.03f;
    [Tooltip("Grados que baja la punta del arma.")]
    [SerializeField] private float recargaPitch = 24f;
    [Tooltip("Grados que gira el arma sobre si misma (deja ver el cargador).")]
    [SerializeField] private float recargaRoll = -26f;
    [Tooltip("Sacudida de las manos durante la recarga (metros).")]
    [SerializeField] private float recargaTraqueteo = 0.012f;

    [Header("Cuchillada (solo cuchillo)")]
    [SerializeField] private float anguloCorte = 55f;
    [SerializeField] private float duracionCorte = 0.18f;

    // --- estado ---
    private Vector3 posicionBase;
    private Quaternion rotacionBase;
    private bool baseLista;

    private WeaponAmmo municion;

    private float saqueInicio = -100f;
    private float golpeInicio = -100f;
    private float retroceso;        // 0..1, se apaga solo
    private float retrocesoPico = 1f;

    private bool recargandoAntes;
    private float recargaInicio = -100f;
    private float recargaDuracion = 1.5f;

    private void Awake()
    {
        municion = GetComponent<WeaponAmmo>();
    }

    private void Start()
    {
        // El WeaponSwitcher coloca el arma en su Awake, al instanciarla. Por eso la pose
        // buena se guarda aqui y no en Awake (se guardaria la del prefab, sin colocar).
        posicionBase = transform.localPosition;
        rotacionBase = transform.localRotation;
        baseLista = true;
        saqueInicio = Time.time;
    }

    private void OnEnable()
    {
        // Al volver a equipar el arma, se repite el saque.
        if (baseLista) saqueInicio = Time.time;
        retroceso = 0f;
        recargandoAntes = false;
    }

    /// <summary>Patada del disparo. Lo llama Shot en cada tiro.</summary>
    public void Disparar()
    {
        retroceso = 1f;
        // Cada disparo sale un poco distinto (si todos son iguales suena a metralleta).
        retrocesoPico = Random.Range(0.85f, 1.15f);
    }

    /// <summary>Arco de la cuchillada. Lo llama KnifeWeapon al cortar.</summary>
    public void Tajo()
    {
        golpeInicio = Time.time;
    }

    public bool Recargando => municion != null && municion.IsReloading;

    private void LateUpdate()
    {
        if (!baseLista) return;

        float ahora = Time.time;

        // ---------------------------------------------------------------
        // 1. RECARGA: se detecta sola mirando el estado de la municion.
        // ---------------------------------------------------------------
        bool recargando = municion != null && municion.IsReloading;
        if (recargando && !recargandoAntes)
        {
            recargaInicio = ahora;
            recargaDuracion = Mathf.Max(0.2f, municion.ReloadDuration);
        }
        recargandoAntes = recargando;

        Vector3 desplazamiento = Vector3.zero;
        Vector3 giro = Vector3.zero;

        if (recargando)
        {
            float u = Mathf.Clamp01((ahora - recargaInicio) / recargaDuracion);
            float env = EnvolventeDeRecarga(u);

            // La pistola recarga con el arma casi a la vista; el rifle/escopeta la hunden
            // mas porque el cargador es largo y pesado.
            float factor = FactorDeRecarga();

            desplazamiento += new Vector3(0f, -recargaAtras * env * factor, recargaBajada * env * factor);
            giro += new Vector3(recargaPitch * env * factor, 0f, recargaRoll * env * factor);

            // Traqueteo: las manos trabajan el cargador. Se nota sobre todo en la pistola.
            float traqueteo = Mathf.Sin(u * Mathf.PI * 9f) * recargaTraqueteo * env;
            desplazamiento += new Vector3(0f, traqueteo * 0.5f, traqueteo);
            giro += new Vector3(0f, 0f, traqueteo * 220f);
        }

        // ---------------------------------------------------------------
        // 2. RETROCESO del disparo (muelle que se apaga solo).
        // ---------------------------------------------------------------
        if (retroceso > 0.001f)
        {
            retroceso = Mathf.Exp(-retrocesoDureza * Time.deltaTime) * retroceso;
            float r = retroceso * retrocesoPico;
            desplazamiento += new Vector3(0f, -retrocesoAtras * r, 0f);
            giro += new Vector3(-retrocesoArriba * r, 0f, 0f);
        }

        // ---------------------------------------------------------------
        // 3. SAQUE: el arma sube desde abajo al equiparla.
        // ---------------------------------------------------------------
        float tSaque = (ahora - saqueInicio) / Mathf.Max(0.01f, tiempoDeSaque);
        if (tSaque < 1f)
        {
            float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tSaque));
            desplazamiento += new Vector3(0f, 0f, caidaAlSacar * k);
            giro += new Vector3(inclinacionAlSacar * k, 0f, 0f);
        }

        // ---------------------------------------------------------------
        // 4. CUCHILLADA (solo cuchillo): arco que entra rapido y vuelve suave.
        // ---------------------------------------------------------------
        if (tipo == TipoArma.Cuchillo)
        {
            float t = (ahora - golpeInicio) / Mathf.Max(0.01f, duracionCorte);
            if (t < 1f && t >= 0f)
            {
                float curva = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                desplazamiento += new Vector3(0f, 0f, -0.06f * curva);
                transform.localRotation = rotacionBase * Quaternion.Euler(anguloCorte * curva, 0f, anguloCorte * 0.35f * curva);

                // El giro se aplica aqui mismo porque va en el eje del CUCHILLO, no en el
                // del socket: es el mismo calculo que hacia KnifeWeapon.
                transform.localPosition = posicionBase + desplazamiento;
                return;
            }
        }

        // El giro de recarga/retroceso/saque va en el espacio del SOCKET (pre-multiplicado),
        // asi funciona igual para cualquier arma sin depender de como este orientada.
        transform.localPosition = posicionBase + desplazamiento;
        transform.localRotation = Quaternion.Euler(giro) * rotacionBase;
    }

    /// <summary>
    /// Curva de la recarga: baja en el primer 20 %, se queda abajo mientras se cambia el
    /// cargador y sube en el ultimo 25 %. Los bordes son suaves para que no de tirones.
    /// </summary>
    private static float EnvolventeDeRecarga(float u)
    {
        if (u < 0.20f) return Mathf.SmoothStep(0f, 1f, u / 0.20f);
        if (u > 0.75f) return 1f - Mathf.SmoothStep(0f, 1f, (u - 0.75f) / 0.25f);
        return 1f;
    }

    /// <summary>Cuanto mueve la recarga segun el arma. La pistola, poco y rapido.</summary>
    private float FactorDeRecarga()
    {
        switch (tipo)
        {
            case TipoArma.Pistola: return 0.75f;
            case TipoArma.Rifle: return 1f;
            case TipoArma.Escopeta: return 1.05f;
            case TipoArma.Subfusil: return 0.9f;
            case TipoArma.Sniper: return 1.2f;
            default: return 0f;       // el cuchillo no recarga
        }
    }
}
