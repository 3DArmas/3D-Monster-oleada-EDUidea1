using System;
using UnityEngine;
using UnityEngine.AI;

public class ZombieController : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private float attackRate = 1f;

    [Header("Deteccion")]
    [SerializeField] private float detectionRange = 25f;
    [SerializeField] private float attackRange = 1.8f;

    [Header("Velocidades")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3.5f;

    [Header("Recompensa")]
    [Tooltip("Dinero que suelta al morir (GDD: normal 100, rapido 150, trepador 175, tanque 300, mini boss 1000).")]
    [SerializeField] private int reward = 100;

    [Header("Zonas de golpe")]
    [Tooltip("Multiplicador de dano al disparar a la CABEZA (2.5 = casi el triple).")]
    [SerializeField] private float multiplicadorCabeza = 2.5f;
    [Tooltip("Radio del collider de la cabeza.")]
    [SerializeField] private float radioCabeza = 0.15f;
    private float currentHealth;
    private NavMeshAgent agent;
    private Animator animator;
    private Transform player;
    private float nextAttackTime;

    /// <summary>Cerebro de horda (ZombieBrain). Si existe, el se encarga de moverse y decidir.</summary>
    private ZombieBrain cerebro;

    private enum State { Idle, Chase, Attack, Dead }
    private State currentState = State.Idle;

    /// <summary>Dinero que otorga al morir.</summary>
    public int Reward => reward;

    /// <summary>Dano por golpe ya escalado por la ronda. Lo usa el cerebro de horda.</summary>
    public float Dano => damage;

    /// <summary>True cuando ya ha muerto.</summary>
    public bool Muerto => currentState == State.Dead;

    /// <summary>Se dispara cuando muere cualquier zombie: lo escuchan RoundManager y GameManager.</summary>
    public static event Action<ZombieController> OnAnyZombieDied;

    /// <summary>Se dispara cuando ESTE zombie recibe dano. Lo usa el cerebro para reaccionar (flinch).</summary>
    public event Action<ZombieController, float> OnDamaged;

    /// <summary>Aplica el escalado de dificultad de la ronda. Lo llama RoundManager al instanciarlo.</summary>
    public void ApplyScaling(float healthMultiplier, float damageMultiplier)
    {
        maxHealth *= Mathf.Max(0.01f, healthMultiplier);
        damage *= Mathf.Max(0.01f, damageMultiplier);
        currentHealth = maxHealth;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        cerebro = GetComponent<ZombieBrain>();
        currentHealth = maxHealth;
        CrearZonasDeGolpe();
    }

    /// <summary>
    /// Crea las ZONAS DE GOLPE sobre los huesos: cabeza (x2.5, headshots) y torso (x1).
    ///
    /// Por que hace falta el torso: la capsula del cuerpo se queda corta a proposito
    /// (cadera y piernas) para que la cabeza quede por FUERA y se pueda acertar. Sin una
    /// zona en el pecho, los disparos al tronco no tocarian nada. Las zonas van pegadas a
    /// los huesos, asi que siguen a la animacion (el zombi agacha la cabeza al andar).
    ///
    /// Todo se crea por codigo para no tocar la jerarquia del modelo importado: asi
    /// sobrevive a cualquier reimportacion del FBX.
    /// </summary>
    private void CrearZonasDeGolpe()
    {
        if (animator == null || !animator.isHuman) return;

        CrearZona(animator.GetBoneTransform(HumanBodyBones.Head), radioCabeza, new Vector3(0f, 0.10f, 0f), multiplicadorCabeza, "CABEZA");
        CrearZona(animator.GetBoneTransform(HumanBodyBones.Spine), 0.32f, new Vector3(0f, 0.10f, 0f), 1f, "TORSO");
    }

    private void CrearZona(Transform hueso, float radio, Vector3 desplazamiento, float multiplicador, string etiqueta)
    {
        if (hueso == null || hueso.Find("Zona" + etiqueta) != null) return;

        var go = new GameObject("Zona" + etiqueta);
        go.layer = gameObject.layer;
        go.transform.SetParent(hueso, false);
        go.transform.localPosition = desplazamiento;

        var esfera = go.AddComponent<SphereCollider>();
        esfera.radius = radio;
        esfera.isTrigger = false;   // la bala es fisica (OnCollisionEnter): necesita colision real

        go.AddComponent<ZonaDeGolpe>().Configurar(multiplicador, etiqueta);
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void Update()
    {
        // Con un ZombieBrain en el objeto, el movimiento y las decisiones los lleva el
        // cerebro de horda (con reparto de turnos y ranuras alrededor del jugador).
        // Aqui quedan la vida, el dano recibido, la recompensa y la muerte.
        if (cerebro != null) return;

        if (currentState == State.Dead) return;
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= attackRange)
        {
            EnterAttackState();
        }
        else if (distance <= detectionRange)
        {
            EnterChaseState();
        }
        else
        {
            EnterIdleState();
        }

        UpdateAnimator();
    }

    /// <summary>El agente esta listo para recibir ordenes (activo y colocado sobre el NavMesh).</summary>
    private bool AgentReady => agent != null && agent.enabled && agent.isOnNavMesh;

    private void EnterIdleState()
    {
        if (currentState == State.Dead) return;
        currentState = State.Idle;

        if (AgentReady)
        {
            agent.speed = patrolSpeed;
            agent.isStopped = true;
        }
    }

    private void EnterChaseState()
    {
        if (currentState == State.Dead) return;
        currentState = State.Chase;

        // Sin esta comprobacion, un zombie fuera del NavMesh (o con el agente
        // desactivado) lanzaba dos errores por frame.
        if (!AgentReady) return;

        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    private void EnterAttackState()
    {
        if (currentState == State.Dead) return;
        currentState = State.Attack;
        if (AgentReady) agent.isStopped = true;

        Vector3 dir = (player.position - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

        if (Time.time >= nextAttackTime)
        {
            Attack();
            nextAttackTime = Time.time + 1f / attackRate;
        }
    }

    private void Attack()
    {
        IDamageable target = player.GetComponent<IDamageable>();
        target?.TakeDamage(damage);
    }

    public void TakeDamage(float amount)
    {
        if (currentState == State.Dead) return;

        currentHealth -= amount;
        OnDamaged?.Invoke(this, amount);
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        currentState = State.Dead;
        if (AgentReady) agent.isStopped = true;
        if (agent != null) agent.enabled = false;

        if (animator != null)
            animator.SetTrigger("Die");

        // Desactiva TODOS los colliders (el del root y los de los huesos) para que el
        // cadaver no bloquee al jugador y se pueda pasar por encima.
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;

        // Avisa a los sistemas de partida (dinero y recuento de la ronda).
        OnAnyZombieDied?.Invoke(this);

        Destroy(gameObject, 5f);
    }

    private void UpdateAnimator()
    {
        // Sin AnimatorController asignado, esto escupiria un warning por frame y por zombie.
        if (animator == null || animator.runtimeAnimatorController == null) return;

        animator.SetFloat("Speed", agent.velocity.magnitude);
        animator.SetBool("IsAttacking", currentState == State.Attack);
        animator.SetBool("IsDead", currentState == State.Dead);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
