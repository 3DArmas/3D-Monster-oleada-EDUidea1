using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DIRECTOR DE HORDA — el "cerebro colectivo" de los zombis.
///
/// ¿Por qué hace falta? Si cada zombi decide por su cuenta (y encima todos apuntan
/// a la MISMA posición del jugador), se produce el efecto embudo: 20 zombis
/// empujándose hacia un solo punto, atascándose entre ellos y en las esquinas.
///
/// Este director resuelve tres cosas:
///   1. RANURAS: reparte posiciones alrededor del jugador (un anillo) para que los
///      zombis lo RODEEN en vez de amontonarse en un punto.
///   2. TURNOS POR LOTES: no actualiza los 30 cerebros en el mismo fotograma; los
///      reparte, de modo que el coste de percepción + rutas queda acotado por frame.
///   3. ALERTA COMPARTIDA: si uno ve (u oye) al jugador, avisa a los de su alrededor.
///
/// Colócalo UNA vez en la escena (por ejemplo en el objeto "Managers").
/// </summary>
[DefaultExecutionOrder(-50)]
public class HordeDirector : MonoBehaviour
{
    private static HordeDirector instancia;

    /// <summary>
    /// Acceso al director. Si la referencia estática se ha perdido (recarga de
    /// dominio, cambio de escena...), se vuelve a buscar solo: así los cerebros
    /// nunca se quedan sin director.
    /// </summary>
    public static HordeDirector Instancia
    {
        get
        {
            if (instancia == null) instancia = FindFirstObjectByType<HordeDirector>(FindObjectsInactive.Include);
            return instancia;
        }
        private set { instancia = value; }
    }

    [Header("Rendimiento")]
    [Tooltip("Cuántos cerebros hacen su actualización 'lenta' (sentidos + decisión + ruta) por fotograma.")]
    [SerializeField] private int cerebrosPorFotograma = 6;
    [Tooltip("Si hay más zombis que este número, los lejanos se actualizan menos.")]
    [SerializeField] private int zombisAntesDeEscatimar = 18;

    [Header("Ranuras alrededor del jugador")]
    [Tooltip("Cuántos huecos hay en el anillo que rodea al jugador.")]
    [SerializeField] private int numeroDeRanuras = 12;
    [Tooltip("Radio del anillo interior (los que van a atacar).")]
    [SerializeField] private float radioInterior = 1.9f;
    [Tooltip("Radio del anillo exterior (los que esperan su turno).")]
    [SerializeField] private float radioExterior = 4.2f;
    [Tooltip("Cada cuánto se recalcula el anillo.")]
    [SerializeField] private float intervaloAnillo = 0.75f;
    [Tooltip("Desorden aleatorio del anillo, para que no vayan en fila india.")]
    [SerializeField] private float desordenAnillo = 0.45f;

    [Header("Alerta compartida")]
    [Tooltip("A qué distancia se avisan entre ellos al detectar al jugador.")]
    [SerializeField] private float radioDeAlerta = 20f;
    [Tooltip("Cuánto tiempo recuerdan la última posición conocida del jugador.")]
    [SerializeField] private float memoriaDelJugador = 7f;

    [Header("Físicas")]
    [Tooltip("Evita que los colliders de los zombis se empujen entre sí (la separación la hacen los cerebros).")]
    [SerializeField] private bool desactivarColisionEntreZombis = true;
    [SerializeField] private string capaZombi = "Zombie";

    [Header("Depuración")]
    [SerializeField] private bool dibujarAnillo = false;

    private readonly List<ZombieBrain> cerebros = new List<ZombieBrain>();
    private readonly List<Vector3> ranuras = new List<Vector3>();
    private readonly List<ZombieBrain> duenosDeRanura = new List<ZombieBrain>();
    private int siguienteIndice;
    private float siguienteRecalculoAnillo;

    /// <summary>Transform del jugador (se busca por la etiqueta "Player").</summary>
    public Transform Jugador { get; private set; }
    /// <summary>Última posición donde se le vio u oyó.</summary>
    public Vector3 UltimaPosicionConocida { get; private set; }
    /// <summary>Momento (Time.time) de la última alerta.</summary>
    public float UltimaVezDetectado { get; private set; } = -999f;
    /// <summary>True si la horda todavía "recuerda" al jugador.</summary>
    public bool HayAlerta => Time.time - UltimaVezDetectado < memoriaDelJugador;
    /// <summary>Cuántos zombis están registrados ahora mismo.</summary>
    public int NumeroDeZombis => cerebros.Count;

    private void Awake()
    {
        Instancia = this;

        if (desactivarColisionEntreZombis)
        {
            int capa = LayerMask.NameToLayer(capaZombi);
            if (capa >= 0)
            {
                // Los zombis no se empujan entre sí: de la separación se encargan los
                // cerebros con suavidad, no la física (que es lo que los dejaba clavados).
                Physics.IgnoreLayerCollision(capa, capa, true);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    private void Start()
    {
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        Jugador = jugador != null ? jugador.transform : null;
        if (Jugador == null) Debug.LogWarning("[HordeDirector] No encuentro al jugador (etiqueta 'Player').");
    }

    private void OnEnable() { Shot.OnAnyShot += AlDisparo; }
    private void OnDisable() { Shot.OnAnyShot -= AlDisparo; }

    private void Update()
    {
        // Auto-reparación: si el jugador no está localizado (o cambió de escena), se busca.
        if (Jugador == null)
        {
            GameObject jugador = GameObject.FindGameObjectWithTag("Player");
            if (jugador != null) Jugador = jugador.transform;
        }
        if (instancia != this) Instancia = this;

        RepartirTurnos();
        ActualizarAnillo();
    }

    // ------------------------------------------------------------------
    // Registro
    // ------------------------------------------------------------------

    public void Registrar(ZombieBrain cerebro)
    {
        if (cerebro != null && !cerebros.Contains(cerebro)) cerebros.Add(cerebro);
    }

    public void Desregistrar(ZombieBrain cerebro)
    {
        int indice = cerebros.IndexOf(cerebro);
        if (indice >= 0) cerebros.RemoveAt(indice);
        for (int i = 0; i < duenosDeRanura.Count; i++) if (duenosDeRanura[i] == cerebro) duenosDeRanura[i] = null;
    }

    /// <summary>True si ese cerebro ya está en la lista.</summary>
    public bool EstaRegistrado(ZombieBrain cerebro) => cerebro != null && cerebros.Contains(cerebro);

    // ------------------------------------------------------------------
    // Turnos por lotes
    // ------------------------------------------------------------------

    private void RepartirTurnos()
    {
        if (cerebros.Count == 0) return;

        // Con muchos zombis se actualizan menos por frame: el juego sigue fluido y
        // el comportamiento se mantiene porque las decisiones no cambian tan rápido.
        int porFrame = cerebrosPorFotograma;
        if (cerebros.Count > zombisAntesDeEscatimar)
            porFrame = Mathf.Max(2, Mathf.RoundToInt(cerebrosPorFotograma * zombisAntesDeEscatimar / (float)cerebros.Count));

        int hechos = 0;
        while (hechos < porFrame && cerebros.Count > 0)
        {
            if (siguienteIndice >= cerebros.Count) siguienteIndice = 0;
            ZombieBrain cerebro = cerebros[siguienteIndice];
            siguienteIndice++;
            hechos++;
            if (cerebro != null) cerebro.TickLento();
        }
    }

    // ------------------------------------------------------------------
    // Anillo de ranuras alrededor del jugador
    // ------------------------------------------------------------------

    private void ActualizarAnillo()
    {
        if (Jugador == null || Time.time < siguienteRecalculoAnillo) return;
        siguienteRecalculoAnillo = Time.time + intervaloAnillo;

        ranuras.Clear();
        int porAnillo = Mathf.Max(1, numeroDeRanuras / 2);
        for (int i = 0; i < numeroDeRanuras; i++)
        {
            bool exterior = i % 2 == 1;
            float radio = exterior ? radioExterior : radioInterior;
            float angulo = (i / 2) * (360f / porAnillo) + (exterior ? 180f / porAnillo : 0f);
            float desorden = (Mathf.PerlinNoise(i * 0.7f, Time.time * 0.35f) - 0.5f) * desordenAnillo * 2f;
            float rad = (angulo + desorden * 30f) * Mathf.Deg2Rad;
            Vector3 punto = Jugador.position + new Vector3(Mathf.Cos(rad) * (radio + desorden), 0f, Mathf.Sin(rad) * (radio + desorden));

            // Se ajusta al NavMesh: si el punto cae fuera del mapa, se pega al borde más cercano.
            UnityEngine.AI.NavMeshHit golpe;
            if (UnityEngine.AI.NavMesh.SamplePosition(punto, out golpe, 3f, UnityEngine.AI.NavMesh.AllAreas))
                ranuras.Add(golpe.position);
        }

        if (duenosDeRanura.Count != ranuras.Count)
        {
            duenosDeRanura.Clear();
            for (int i = 0; i < ranuras.Count; i++) duenosDeRanura.Add(null);
        }
    }

    /// <summary>
    /// Devuelve la ranura libre más cercana a este zombi (o la posición del jugador
    /// si no hay ninguna). Así se rodea al jugador en vez de amontonarse.
    /// </summary>
    public Vector3 RanuraPara(ZombieBrain cerebro)
    {
        if (ranuras.Count == 0) return Jugador != null ? Jugador.position : cerebro.transform.position;

        int mejor = -1;
        float mejorDistancia = float.MaxValue;
        for (int i = 0; i < ranuras.Count; i++)
        {
            if (duenosDeRanura[i] != null && duenosDeRanura[i] != cerebro) continue;
            float d = (ranuras[i] - cerebro.transform.position).sqrMagnitude;
            if (d < mejorDistancia) { mejorDistancia = d; mejor = i; }
        }

        if (mejor < 0) return Jugador != null ? Jugador.position : cerebro.transform.position;
        duenosDeRanura[mejor] = cerebro;
        return ranuras[mejor];
    }

    // ------------------------------------------------------------------
    // Alerta y oído
    // ------------------------------------------------------------------

    /// <summary>El jugador ha sido visto (o se cree que está) en esta posición.</summary>
    public void Alertar(Vector3 posicion)
    {
        UltimaPosicionConocida = posicion;
        UltimaVezDetectado = Time.time;
    }

    /// <summary>Un disparo hace ruido: la horda se entera de dónde estás.</summary>
    private void AlDisparo()
    {
        if (Jugador == null) return;
        Alertar(Jugador.position);
    }

    /// <summary>Avisa a los zombis cercanos a un suceso (lo usan los propios cerebros).</summary>
    public void AlertarCercanos(Vector3 posicion, float radio)
    {
        Alertar(posicion);
        float r2 = radio * radio;
        for (int i = 0; i < cerebros.Count; i++)
        {
            ZombieBrain c = cerebros[i];
            if (c == null) continue;
            if ((c.transform.position - posicion).sqrMagnitude <= r2) c.AlertaRecibida();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!dibujarAnillo || ranuras == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        for (int i = 0; i < ranuras.Count; i++)
        {
            Gizmos.DrawWireSphere(ranuras[i], 0.25f);
            if (duenosDeRanura.Count > i && duenosDeRanura[i] != null)
                Gizmos.DrawLine(ranuras[i], duenosDeRanura[i].transform.position);
        }
    }
}
