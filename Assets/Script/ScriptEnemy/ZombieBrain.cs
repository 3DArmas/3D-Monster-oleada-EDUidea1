using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// CEREBRO DE ZOMBI: sentidos, decisiones y movimiento de UN zombi.
///
/// Trabaja en dos ritmos para no matar los FPS:
///   - Update (cada frame, baratísimo): separación entre zombis, girar hacia el
///     objetivo y avisar al Animator.
///   - TickLento (lo llama HordeDirector por turnos): percibir al jugador, decidir
///     el estado y recalcular la ruta (con límite de frecuencia).
///
/// Estados: Vagar → Perseguir → Atacar, y Buscar cuando pierde de vista al jugador.
///
/// IMPORTANTE: convive con ZombieController. Ese se sigue encargando de la vida, el
/// daño que recibe, la recompensa y la muerte; este cerebro se encarga de moverse y
/// de decidir. Si existe este componente, ZombieController deja de mover al zombi.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ZombieBrain : MonoBehaviour
{
    public enum Estado { Vagar, Perseguir, Atacar, Buscar, Muerto }

    [Header("Sentidos")]
    [Tooltip("Distancia máxima a la que ve al jugador.")]
    [SerializeField] private float rangoDeVision = 24f;
    [Tooltip("Ángulo del cono de visión (grados, total).")]
    [Range(20f, 360f)][SerializeField] private float anguloDeVision = 140f;
    [Tooltip("Hasta dónde le llega un aviso de la horda (disparos, gritos...).")]
    [SerializeField] private float rangoDeOido = 22f;
    [Tooltip("Punto desde el que mira (si se deja vacío se usa la cabeza o este objeto).")]
    [SerializeField] private Transform ojos;
    [Tooltip("Capas que tapan la visión (muros, rocas...).")]
    [SerializeField] private LayerMask capasQueTapanLaVista = ~0;
    [Tooltip("Altura a la que apunta al jugador (pecho).")]
    [SerializeField] private float alturaDelObjetivo = 1.2f;

    [Header("Movimiento")]
    [SerializeField] private float velocidadVagar = 1.4f;
    [SerializeField] private float velocidadPerseguir = 3.4f;
    [SerializeField] private float distanciaDeAtaque = 1.9f;
    [Tooltip("Distancia a la que frena el agente (stoppingDistance). Evita el baile de empujones.")]
    [SerializeField] private float distanciaDeParada = 1.3f;
    [Tooltip("Cada cuánto, como mínimo, se recalcula la ruta.")]
    [SerializeField] private float cadaRecalculoDeRuta = 0.35f;
    [Tooltip("Si el destino se movió más que esto, se recalcula aunque no toque.")]
    [SerializeField] private float movimientoMinimoParaRecalcular = 0.8f;

    [Header("Evasion entre zombis")]
    [Tooltip("A qué distancia empieza a apartarse de sus compañeros.")]
    [SerializeField] private float radioDeSeparacion = 1.4f;
    [SerializeField] private float fuerzaDeSeparacion = 2.6f;
    [SerializeField] private float cadaSeparacion = 0.12f;
    [SerializeField] private LayerMask capaDeZombis;

    [Header("Anti-atasco")]
    [Tooltip("Si no avanza nada durante este tiempo, intenta desatascarse.")]
    [SerializeField] private float tiempoParaConsiderarAtascado = 1.2f;
    [Tooltip("Movimiento por debajo del cual se considera que está clavado.")]
    [SerializeField] private float avanceMinimo = 0.25f;

    [Header("Vagar (cuando no hay alerta)")]
    [SerializeField] private float radioDeVagar = 12f;
    [SerializeField] private float duracionDeUnPaseo = 5f;

    [Header("Combate")]
    [Tooltip("Daño por golpe. Si hay ZombieController se usa el suyo (ya escalado por ronda).")]
    [SerializeField] private float dano = 15f;
    [SerializeField] private float cadenciaDeAtaque = 1f;
    [SerializeField] private bool usarDanoDelControlador = true;

    [Header("Depuración")]
    [SerializeField] private bool dibujarSentidos = false;

    private NavMeshAgent agente;
    private ZombieController controlador;
    private Animator animador;
    private Transform jugador;

    private Estado estado = Estado.Vagar;
    private Vector3 origen;
    private Vector3 ultimoDestino;
    private float siguienteRuta;
    private float siguienteSeparacion;
    private float siguienteAtaque;
    private float siguientePaseo;
    private Vector3 posicionAnterior;
    private float tiempoQuieto;
    private bool alertado;

    private static readonly Collider[] vecinos = new Collider[16];

    public Estado EstadoActual => estado;
    public bool VeAlJugador { get; private set; }

    // ------------------------------------------------------------------

    private void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        controlador = GetComponent<ZombieController>();
        animador = GetComponent<Animator>();
        origen = transform.position;
        posicionAnterior = transform.position;

        if (ojos == null)
        {
            // Si hay avatar humanoide se usa la cabeza; si no, un poco por encima del pivote.
            if (animador != null && animador.isHuman)
            {
                Transform cabeza = animador.GetBoneTransform(HumanBodyBones.Head);
                if (cabeza != null) ojos = cabeza;
            }
            if (ojos == null)
            {
                GameObject punto = new GameObject("Ojos");
                punto.transform.SetParent(transform, false);
                punto.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                ojos = punto.transform;
            }
        }

        // Cada zombi con una prioridad distinta: rompe los bloqueos simétricos
        // (dos zombis empujándose de frente se quedaban clavados).
        if (agente != null)
        {
            agente.avoidancePriority = Random.Range(20, 80);
            agente.stoppingDistance = distanciaDeParada;
            agente.autoBraking = true;
            agente.obstacleAvoidanceType = ObstacleAvoidanceType.GoodQualityObstacleAvoidance;
        }
    }

    private void OnEnable()
    {
        if (HordeDirector.Instancia != null) HordeDirector.Instancia.Registrar(this);
    }

    private void OnDisable()
    {
        if (HordeDirector.Instancia != null) HordeDirector.Instancia.Desregistrar(this);
    }

    private void Start()
    {
        if (HordeDirector.Instancia == null)
        {
            Debug.LogWarning("[ZombieBrain] No hay HordeDirector en la escena: el zombi funcionará sin director.");
        }
        else
        {
            if (HordeDirector.Instancia.Jugador != null) jugador = HordeDirector.Instancia.Jugador;
            if (!HordeDirector.Instancia.EstaRegistrado(this)) HordeDirector.Instancia.Registrar(this);
        }
    }

    // ------------------------------------------------------------------
    // Ritmo rápido: cada frame
    // ------------------------------------------------------------------

    private void Update()
    {
        if (estado == Estado.Muerto || !AgenteListo) return;

        AplicarSeparacion();
        EncararSiAtaca();
        ActualizarAnimador();
    }

    private bool AgenteListo => agente != null && agente.enabled && agente.isOnNavMesh;

    /// <summary>Aparta suavemente al zombi de sus compañeros para que no se solapen.</summary>
    private void AplicarSeparacion()
    {
        if (Time.time < siguienteSeparacion) return;
        siguienteSeparacion = Time.time + cadaSeparacion;

        int n = Physics.OverlapSphereNonAlloc(transform.position, radioDeSeparacion, vecinos, capaDeZombis);
        Vector3 empuje = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Collider otro = vecinos[i];
            if (otro == null) continue;
            if (otro.transform.root == transform.root) continue;

            Vector3 diferencia = transform.position - otro.transform.position;
            diferencia.y = 0f;
            float distancia = diferencia.magnitude;
            if (distancia < 0.01f)
            {
                // Justo encima: se aparta en una dirección aleatoria estable.
                diferencia = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
                distancia = 0.01f;
            }
            if (distancia < radioDeSeparacion)
                empuje += diferencia.normalized * (1f - distancia / radioDeSeparacion);
        }

        if (empuje.sqrMagnitude > 0.0001f)
        {
            // Move respeta el NavMesh: no los saca del suelo ni atraviesa paredes.
            Vector3 desplazamiento = empuje.normalized * fuerzaDeSeparacion * cadaSeparacion;
            agente.Move(desplazamiento);
        }
    }

    private void EncararSiAtaca()
    {
        if (estado != Estado.Atacar || jugador == null) return;
        agente.updateRotation = false;
        Vector3 direccion = jugador.position - transform.position;
        direccion.y = 0f;
        if (direccion.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direccion), 8f * Time.deltaTime);
    }

    private void ActualizarAnimador()
    {
        if (animador == null || animador.runtimeAnimatorController == null) return;
        animador.SetFloat("Speed", agente.velocity.magnitude);
        animador.SetBool("IsAttacking", estado == Estado.Atacar);
        animador.SetBool("IsDead", estado == Estado.Muerto);
    }

    // ------------------------------------------------------------------
    // Ritmo lento: lo llama HordeDirector por turnos
    // ------------------------------------------------------------------

    /// <summary>Sentidos + decisión + ruta. Se ejecuta por turnos, no cada frame.</summary>
    public void TickLento()
    {
        if (estado == Estado.Muerto) return;

        // Auto-reparación: si no está registrado en el director (por una recarga de
        // dominio, por ejemplo), se registra aquí. Así nunca se queda "sin cerebro".
        if (HordeDirector.Instancia != null && !HordeDirector.Instancia.EstaRegistrado(this))
            HordeDirector.Instancia.Registrar(this);

        if (controlador != null && controlador.Muerto) { Morir(); return; }
        if (!AgenteListo) return;
        if (jugador == null && HordeDirector.Instancia != null) jugador = HordeDirector.Instancia.Jugador;
        if (jugador == null) return;

        ComprobarAtasco();

        VeAlJugador = PercibirAlJugador();

        // --- Decisión ---
        if (VeAlJugador)
        {
            alertado = true;
            if (HordeDirector.Instancia != null) HordeDirector.Instancia.AlertarCercanos(jugador.position, rangoDeOido);
        }

        float distancia = Vector3.Distance(transform.position, jugador.position);

        if (VeAlJugador && distancia <= distanciaDeAtaque) estado = Estado.Atacar;
        else if (VeAlJugador) estado = Estado.Perseguir;
        else if (DebeBuscar()) estado = Estado.Buscar;
        else estado = Estado.Vagar;

        // --- Actuación ---
        switch (estado)
        {
            case Estado.Perseguir: Perseguir(); break;
            case Estado.Atacar: Atacar(distancia); break;
            case Estado.Buscar: Buscar(); break;
            default: Vagar(); break;
        }
    }

    /// <summary>¿Hay motivo para ir a la última posición conocida del jugador?</summary>
    private bool DebeBuscar()
    {
        if (HordeDirector.Instancia == null || !HordeDirector.Instancia.HayAlerta) { alertado = false; return false; }
        Vector3 ultima = HordeDirector.Instancia.UltimaPosicionConocida;
        return alertado || (ultima - transform.position).sqrMagnitude <= rangoDeOido * rangoDeOido;
    }

    /// <summary>Un compañero (o un disparo) ha avisado: este zombi se pone en alerta.</summary>
    public void AlertaRecibida()
    {
        alertado = true;
    }

    // ------------------------------------------------------------------
    // Sentidos
    // ------------------------------------------------------------------

    private bool PercibirAlJugador()
    {
        Vector3 objetivo = jugador.position + Vector3.up * alturaDelObjetivo;
        Vector3 desde = ojos != null ? ojos.position : transform.position + Vector3.up * 1.5f;
        Vector3 hacia = objetivo - desde;
        float distancia = hacia.magnitude;

        if (distancia > rangoDeVision) return false;

        // Cono de visión (si ya está alertado, mira en todas direcciones: ya sabe dónde estás).
        if (!alertado)
        {
            Vector3 plano = hacia; plano.y = 0f;
            float angulo = Vector3.Angle(transform.forward, plano);
            if (angulo > anguloDeVision * 0.5f) return false;
            // Los muy cerca los "siente" aunque no los vea.
            if (distancia > 2.5f)
            {
                if (Physics.Raycast(desde, hacia.normalized, out RaycastHit golpe, distancia, capasQueTapanLaVista, QueryTriggerInteraction.Ignore))
                {
                    if (golpe.collider.GetComponentInParent<PlayerHealth>() == null) return false;
                }
            }
        }
        return true;
    }

    // ------------------------------------------------------------------
    // Estados
    // ------------------------------------------------------------------

    private void Perseguir()
    {
        if (HordeDirector.Instancia != null && HordeDirector.Instancia.HayAlerta && !VeAlJugador)
        {
            Buscar();
            return;
        }
        // La ranura reparte a los zombis alrededor del jugador: rodean, no se apilan.
        Vector3 destino = HordeDirector.Instancia != null
            ? HordeDirector.Instancia.RanuraPara(this)
            : jugador.position;
        IrA(destino, velocidadPerseguir);
    }

    private void Atacar(float distancia)
    {
        agente.isStopped = true;
        agente.velocity = Vector3.zero;
        if (Time.time < siguienteAtaque) return;
        siguienteAtaque = Time.time + 1f / Mathf.Max(0.05f, cadenciaDeAtaque);

        IDamageable objetivo = jugador.GetComponent<IDamageable>();
        if (objetivo == null) objetivo = jugador.GetComponentInParent<IDamageable>();
        float valor = (usarDanoDelControlador && controlador != null) ? controlador.Dano : dano;
        objetivo?.TakeDamage(valor);
    }

    private void Buscar()
    {
        if (HordeDirector.Instancia == null) { Vagar(); return; }
        Vector3 ultima = HordeDirector.Instancia.UltimaPosicionConocida;
        if ((ultima - transform.position).sqrMagnitude < 4f)
        {
            // Ha llegado al sitio donde lo vio y no hay nadie: vuelve a vagar.
            alertado = false;
            Vagar();
            return;
        }
        IrA(ultima, velocidadPerseguir);
    }

    private void Vagar()
    {
        agente.updateRotation = true;
        if (Time.time < siguientePaseo && !agente.isStopped) return;

        Vector3 candidato = origen + new Vector3(Random.Range(-radioDeVagar, radioDeVagar), 0f, Random.Range(-radioDeVagar, radioDeVagar));
        if (NavMesh.SamplePosition(candidato, out NavMeshHit golpe, 4f, NavMesh.AllAreas))
        {
            IrA(golpe.position, velocidadVagar);
            siguientePaseo = Time.time + duracionDeUnPaseo;
        }
        else siguientePaseo = Time.time + 1f;
    }

    /// <summary>Manda al agente a un destino, pero SIN recalcular la ruta cada frame.</summary>
    private void IrA(Vector3 destino, float velocidad)
    {
        agente.updateRotation = true;
        if (!Mathf.Approximately(agente.speed, velocidad)) agente.speed = velocidad;
        if (agente.isStopped) agente.isStopped = false;

        bool tocaPorTiempo = Time.time >= siguienteRuta;
        bool seMovioElDestino = (destino - ultimoDestino).sqrMagnitude > movimientoMinimoParaRecalcular * movimientoMinimoParaRecalcular;
        if (!tocaPorTiempo && !seMovioElDestino) return;

        agente.SetDestination(destino);
        ultimoDestino = destino;
        siguienteRuta = Time.time + cadaRecalculoDeRuta;
    }

    // ------------------------------------------------------------------
    // Anti-atasco
    // ------------------------------------------------------------------

    private void ComprobarAtasco()
    {
        float avance = Vector3.Distance(transform.position, posicionAnterior);
        posicionAnterior = transform.position;

        bool deberiaMoverse = estado != Estado.Atacar && !agente.isStopped && agente.hasPath;
        if (deberiaMoverse && avance < avanceMinimo)
        {
            tiempoQuieto += cadaRecalculoDeRuta;
            if (tiempoQuieto >= tiempoParaConsiderarAtascado)
            {
                Desatascar();
                tiempoQuieto = 0f;
            }
        }
        else tiempoQuieto = 0f;
    }

    /// <summary>
    /// Se ha quedado clavado (esquina, montón de zombis, hueco estrecho). Se rompe el
    /// bloqueo: nueva prioridad de evitación, ruta limpia y un empujoncito a un lado.
    /// </summary>
    private void Desatascar()
    {
        agente.avoidancePriority = Random.Range(20, 80);
        agente.ResetPath();

        Vector3 lado = Random.value < 0.5f ? transform.right : -transform.right;
        if (Random.value < 0.35f) lado = -transform.forward;
        agente.Move(lado.normalized * 0.5f);

        // Y busca un punto de NavMesh cercano para retomar desde ahí.
        if (NavMesh.SamplePosition(transform.position + lado * 1.2f, out NavMeshHit golpe, 3f, NavMesh.AllAreas))
        {
            agente.SetDestination(golpe.position);
            ultimoDestino = golpe.position;
            siguienteRuta = Time.time + cadaRecalculoDeRuta;
        }
    }

    // ------------------------------------------------------------------

    private void Morir()
    {
        estado = Estado.Muerto;
        if (AgenteListo) agente.isStopped = true;
        ActualizarAnimador();
        if (HordeDirector.Instancia != null) HordeDirector.Instancia.Desregistrar(this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!dibujarSentidos) return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, rangoDeVision);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radioDeSeparacion);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanciaDeAtaque);
    }
}
