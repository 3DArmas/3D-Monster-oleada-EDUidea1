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

    private float currentHealth;
    private NavMeshAgent agent;
    private Animator animator;
    private Transform player;
    private float nextAttackTime;

    private enum State { Idle, Chase, Attack, Dead }
    private State currentState = State.Idle;

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

    private void EnterIdleState()
    {
        if (currentState == State.Dead) return;
        currentState = State.Idle;
        agent.speed = patrolSpeed;
        agent.isStopped = true;
    }

    private void EnterChaseState()
    {
        if (currentState == State.Dead) return;
        currentState = State.Chase;
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    private void EnterAttackState()
    {
        if (currentState == State.Dead) return;
        currentState = State.Attack;
        agent.isStopped = true;

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
        agent.isStopped = true;
        agent.enabled = false;

        if (animator != null)
            animator.SetTrigger("Die");

        GetComponent<Collider>().enabled = false;
        Destroy(gameObject, 5f);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

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
