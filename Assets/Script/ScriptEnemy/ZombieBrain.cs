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

    [Header("Anti-solape (sin deslizarse)")]
    [Tooltip("Distancia mínima entre zombis: SOLO evita que dos ocupen el mismo sitio. " +
             "No los hace deslizarse a los lados: se aprietan y se empujan de frente, como una manda.")]
    [SerializeField] private float radioDeSeparacion = 0.8f;
    [Tooltip("Fuerza del tope anti-solape. Baja a propósito: es un tope, no un baile.")]
    [SerializeField] private float fuerzaDeSeparacion = 1.1f;
    [SerializeField] private float cadaSeparacion = 0.12f;
    [SerializeField] private LayerMask capaDeZombis;

    [Header("Ataque con la mano")]
    [Tooltip("Radio del collider de la mano que golpea.")]
    [SerializeField] private float radioDelGolpe = 0.34f;
    [Tooltip("Fracción de la animación de ataque donde el golpe puede conectar (0 = inicio, 1 = fin).")]
    [Range(0f, 1f)][SerializeField] private float impactoDesde = 0.3f;
    [Range(0f, 1f)][SerializeField] private float impactoHasta = 0.55f;

    [Header("Reacción al daño (flinch)")]
    [Tooltip("Cuánto se queda sacudido al recibir un disparo.")]
    [SerializeField] private float duracionDelFlinch = 0.35f;
    [Tooltip("Empujón hacia atrás al recibir el disparo (m/s).")]
    [SerializeField] private float fuerzaDelFlinch = 1.4f;

    [Header("Anti-atasco")]
    [Tooltip("Ventana de tiempo (segundos) en la que se mide si el zombi se ha movido.")]
    [SerializeField] private float ventanaDeAtasco = 1.2f;
    [Tooltip("Distancia mínima que debe recorrer en esa ventana para no considerarse atascado.")]
    [SerializeField] private float distanciaMinimaEnVentana = 0.35f;

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
    private bool alertado;

    // --- Control de tiempos (imprescindible para que no vayan "a lo loco") ---
    [Header("Ritmo de pensamiento")]
    [Tooltip("Cada cuánto piensa este zombi como máximo (segundos). El director reparte los turnos encima de esto.")]
    [SerializeField] private float intervaloDePensamiento = 0.2f;

    private float siguientePensamiento;
    private float inicioDeVentana;
    private float distanciaAcumulada;

    // Empujes suaves: se calculan cada cierto tiempo pero se APLICAN cada frame
    // multiplicados por Time.deltaTime (si no, el movimiento depende de los FPS).
    private Vector3 empujeSeparacion;
    private Vector3 empujeExtra;
    private float empujeExtraHasta;
    private float aturdidoHasta;
    private Transform manoDeGolpe;
    private SphereCollider colliderDeMano;
    private bool golpeAplicado;

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
        inicioDeVentana = Time.time;
        // Desfase aleatorio: así los zombis no piensan todos en el mismo frame.
        siguientePensamiento = Time.time + Random.Range(0f, intervaloDePensamiento);

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

        // Collider del golpe: una esfera en el hueso de la mano. Se queda creado pero
        // DESACTIVADO, y solo se enciende dentro de la ventana de impacto de la animación:
        // así el daño llega cuando el brazo llega de verdad (y se puede esquivar).
        if (animador != null && animador.isHuman)
        {
            Transform mano = animador.GetBoneTransform(HumanBodyBones.RightHand);
            if (mano != null)
            {
                manoDeGolpe = mano;
                GameObject nodo = new GameObject("GolpeMano");
                nodo.transform.SetParent(mano, false);
                colliderDeMano = nodo.AddComponent<SphereCollider>();
                colliderDeMano.isTrigger = true;
                colliderDeMano.radius = radioDelGolpe;
                colliderDeMano.enabled = false;
            }
        }
        if (controlador != null) controlador.OnDamaged += AlRecibirDano;
    }

    private void OnEnable()
    {
        if (HordeDirector.Instancia != null) HordeDirector.Instancia.Registrar(this);
    }

    private void OnDisable()
    {
        if (controlador != null) controlador.OnDamaged -= AlRecibirDano;
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
        if (estado == Estado.Atacar) ComprobarGolpe();
        ActualizarAnimador();
    }

    private bool AgenteListo => agente != null && agente.enabled && agente.isOnNavMesh;

    /// <summary>
    /// Aparta suavemente al zombi de sus compañeros.
    /// El cálculo de vecinos (que es lo caro) se hace cada cierto intervalo, pero el
    /// empuje se APLICA cada frame multiplicado por deltaTime: así el movimiento es
    /// fluido y no depende de los FPS. Antes se aplicaba un salto de golpe cada 0,12 s
    /// y se veía como un tirón lateral.
    /// </summary>
    private void AplicarSeparacion()
    {
        // 1) Recalcular la dirección del empuje cada cierto intervalo
        if (Time.time >= siguienteSeparacion)
        {
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
                    // Justo encima: se aparta en una dirección aleatoria.
                    diferencia = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
                    distancia = 0.01f;
                }
                if (distancia < radioDeSeparacion)
                    empuje += diferencia.normalized * (1f - distancia / radioDeSeparacion);
            }
            empujeSeparacion = empuje.sqrMagnitude > 0.0001f ? empuje.normalized : Vector3.zero;
        }

        // 2) Aplicarlo CADA frame, escalado por el tiempo real
        Vector3 total = empujeSeparacion * fuerzaDeSeparacion;

        // Atacando se aparta menos: si no, temblaría alrededor del jugador.
        if (estado == Estado.Atacar) total *= 0.35f;

        // Empujón de desatasco (dura unas décimas y se apaga solo)
        if (Time.time < empujeExtraHasta) total += empujeExtra;
        else empujeExtra = Vector3.zero;

        if (total.sqrMagnitude > 0.0001f)
        {
            // Move respeta el NavMesh: no los saca del suelo ni atraviesa paredes.
            agente.Move(total * Time.deltaTime);
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

        // Ritmo de pensamiento limitado POR RELOJ (no por número de ticks). Sin esto,
        // con muchos zombis cada cerebro pensaba decenas de veces por segundo y el
        // detector de atascos se disparaba sin parar: el zombi iba a los lados.
        if (Time.time < siguientePensamiento) return;
        siguientePensamiento = Time.time + intervaloDePensamiento * Random.Range(0.85f, 1.15f);

        // Auto-reparación: si no está registrado en el director (por una recarga de
        // dominio, por ejemplo), se registra aquí. Así nunca se queda "sin cerebro".
        if (HordeDirector.Instancia != null && !HordeDirector.Instancia.EstaRegistrado(this))
            HordeDirector.Instancia.Registrar(this);

        if (controlador != null && controlador.Muerto) { Morir(); return; }

        // Flinch: al recibir un disparo se queda sacudido un momento (y retrocede un poco).
        if (Time.time < aturdidoHasta)
        {
            agente.isStopped = true;
            return;
        }
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

    /// <summary>
    /// El zombi ataca: se para, gira hacia el jugador y marca el inicio de un golpe.
    /// El daño NO se aplica aquí: lo aplica el collider de la mano (ComprobarGolpe)
    /// solo dentro de la ventana de impacto de la animación. Así, si te apartas, falla.
    /// </summary>
    private void Atacar(float distancia)
    {
        // IMPORTANTE: sigue arrimándose MIENTRAS ataca. Si se parase a 2 m, el brazo no
        // llegaría nunca al jugador y el golpe fallaría siempre (era el caso).
        if (HordeDirector.Instancia != null) IrA(HordeDirector.Instancia.RanuraPara(this), velocidadPerseguir);
        else agente.isStopped = true;

        if (Time.time < siguienteAtaque) return;
        siguienteAtaque = Time.time + 1f / Mathf.Max(0.05f, cadenciaDeAtaque);
        golpeAplicado = false;
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
        float ahora = Time.time;

        // Se acumula lo que ha recorrido desde el tick anterior...
        distanciaAcumulada += Vector3.Distance(transform.position, posicionAnterior);
        posicionAnterior = transform.position;

        // ...y cada ventana de tiempo REAL se decide si está clavado.
        if (ahora - inicioDeVentana < ventanaDeAtasco) return;
        inicioDeVentana = ahora;

        bool deberiaMoverse = estado != Estado.Atacar && !agente.isStopped;
        if (deberiaMoverse && distanciaAcumulada < distanciaMinimaEnVentana) Desatascar();
        distanciaAcumulada = 0f;
    }

    /// <summary>
    /// Se ha quedado clavado (esquina, montón de zombis, hueco estrecho). Se rompe el
    /// bloqueo: nueva prioridad de evitación, ruta limpia y un empujón a un lado que se
    /// aplica de forma suave durante unas décimas (antes era un salto instantáneo de
    /// medio metro, y se veía como un tirón).
    /// </summary>
    private void Desatascar()
    {
        // Se rompe el bloqueo SIN apartarse a los lados (eso era el "esquivar"):
        // nueva prioridad de evitación, ruta limpia y se replantea el destino.
        agente.avoidancePriority = Random.Range(20, 80);
        agente.ResetPath();

        Vector3 hacia = ultimoDestino - transform.position;
        hacia.y = 0f;
        if (hacia.sqrMagnitude < 0.01f) hacia = transform.forward;

        Vector3 candidato = transform.position + hacia.normalized * 1.0f;
        if (NavMesh.SamplePosition(candidato, out NavMeshHit golpe, 3f, NavMesh.AllAreas))
        {
            agente.SetDestination(golpe.position);
            ultimoDestino = golpe.position;
            siguienteRuta = Time.time + cadaRecalculoDeRuta;
        }
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Comprueba si el golpe del brazo conecta con el jugador. Solo cuenta DENTRO de la
    /// ventana de impacto de la animación (el brazo tiene que llegar de verdad) y como
    /// mucho UNA vez por golpe. Si el jugador se aparta, no recibe daño.
    /// </summary>
    private void ComprobarGolpe()
    {
        if (jugador == null) return;

        // ¿Por qué punto del golpe va la animación?
        float t = 0f;
        if (animador != null && animador.runtimeAnimatorController != null)
        {
            AnimatorStateInfo info = animador.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName("Attack"))
            {
                if (colliderDeMano != null) colliderDeMano.enabled = false;
                return;
            }
            t = Mathf.Repeat(info.normalizedTime, 1f);
        }

        bool enVentana = t >= impactoDesde && t <= impactoHasta;

        // El collider de la mano solo está activo durante la ventana de impacto.
        if (colliderDeMano != null) colliderDeMano.enabled = enVentana;

        // Antes de la ventana se vuelve a "armar": un golpe por swing.
        if (!enVentana)
        {
            if (t < impactoDesde) golpeAplicado = false;
            return;
        }
        if (golpeAplicado) return;

        // ¿Toca al jugador? Se comprueba la FÍSICA desde la mano, no la distancia.
        Vector3 centro = manoDeGolpe != null ? manoDeGolpe.position : transform.position + Vector3.up * 1.2f;
        Collider[] tocados = Physics.OverlapSphere(centro, radioDelGolpe, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider tocado in tocados)
        {
            if (tocado == null) continue;
            if (tocado.GetComponentInParent<PlayerHealth>() == null) continue;

            IDamageable objetivo = tocado.GetComponentInParent<IDamageable>();
            if (objetivo == null) continue;

            float valor = (usarDanoDelControlador && controlador != null) ? controlador.Dano : dano;
            objetivo.TakeDamage(valor);
            golpeAplicado = true;
            if (colliderDeMano != null) colliderDeMano.enabled = false;
            return;
        }
    }

    /// <summary>Al recibir un disparo se sacude y retrocede un poco (flinch).</summary>
    private void AlRecibirDano(ZombieController quien, float cantidad)
    {
        if (estado == Estado.Muerto) return;
        aturdidoHasta = Time.time + duracionDelFlinch;
        empujeExtra = -transform.forward * fuerzaDelFlinch;
        empujeExtraHasta = Time.time + duracionDelFlinch;
    }

    /// <summary>
    /// Gira la cabeza hacia el jugador. Necesita que la capa del AnimatorController
    /// tenga marcado "IK Pass"; si no, Unity no llama a este método.
    /// </summary>
    private void OnAnimatorIK(int layerIndex)
    {
        if (animador == null || jugador == null) return;
        if (estado == Estado.Muerto) { animador.SetLookAtWeight(0f); return; }

        float peso = estado == Estado.Vagar ? 0.35f : 1f;
        animador.SetLookAtWeight(peso, 0f, 1f, 0f, 0.65f);
        animador.SetLookAtPosition(jugador.position + Vector3.up * alturaDelObjetivo);
    }

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
