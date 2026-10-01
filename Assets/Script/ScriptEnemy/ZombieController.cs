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

    private float currentHealth;
    private NavMeshAgent agent;
    private Animator animator;
    private Transform player;
    private float nextAttackTime;

    private enum State { Idle, Chase, Attack, Dead }
    private State currentState = State.Idle;

    /// <summary>Dinero que otorga al morir.</summary>
    public int Reward => reward;

    /// <summary>Se dispara cuando muere cualquier zombie: lo escuchan RoundManager y GameManager.</summary>
    public static event Action<ZombieController> OnAnyZombieDied;

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
        currentHealth = maxHealth;
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void Update()
    {
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
