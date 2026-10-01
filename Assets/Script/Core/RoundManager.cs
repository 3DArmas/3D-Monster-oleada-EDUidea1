using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona las rondas: cuantos zombies salen, con que ritmo y cuando termina la ronda.
/// No instancia nada por su cuenta: se apoya en <see cref="ZombieSpawner"/>.
/// Los valores siguen el GDD v1.1 (docs/GDD.md).
/// </summary>
public class RoundManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private ZombieSpawner spawner;

    [Header("Rondas")]
    [SerializeField] private int startingRound = 1;
    [Tooltip("Zombies de las primeras rondas, en orden (GDD: 8, 12, 16, 22, 28).")]
    [SerializeField] private int[] zombiesPerFirstRounds = { 8, 12, 16, 22, 28 };
    [Tooltip("Crecimiento a partir de la ultima ronda de la lista (GDD: +30%).")]
    [SerializeField] private float growthAfterFirstRounds = 1.30f;

    [Header("Ritmo de aparicion")]
    [SerializeField] private float baseSpawnInterval = 0.5f;
    [SerializeField] private float minSpawnInterval = 0.25f;
    [SerializeField] private float spawnIntervalDecreasePerRound = 0.02f;
    [Tooltip("Empieza a reducir el intervalo a partir de esta ronda.")]
    [SerializeField] private int spawnIntervalDecreaseStartsAtRound = 5;
    [SerializeField] private int maxAlive = 24;

    [Header("Escalado de los enemigos")]
    [SerializeField] private int healthScaleStartsAtRound = 6;
    [SerializeField] private float healthScalePerRound = 0.08f;
    [SerializeField] private float damageScalePerRound = 0.05f;

    public int CurrentRound { get; private set; }
    public int TotalThisRound { get; private set; }
    public int AliveCount => alive.Count;
    public int PendingToSpawn => pendingToSpawn;
    public bool RoundInProgress { get; private set; }

    /// <summary>Numero de ronda que acaba de empezar.</summary>
    public event Action<int> OnRoundStarted;
    /// <summary>Ronda superada: todos los zombies han muerto.</summary>
    public event Action<int> OnRoundCleared;
    /// <summary>Cambio en el numero de zombies vivos (para el HUD).</summary>
    public event Action<int> OnAliveChanged;

    private readonly List<ZombieController> alive = new List<ZombieController>();
    private int pendingToSpawn;
    private Coroutine routine;

    private void OnEnable()
    {
        ZombieController.OnAnyZombieDied += HandleZombieDied;
    }

    private void OnDisable()
    {
        ZombieController.OnAnyZombieDied -= HandleZombieDied;
    }

    /// <summary>Empieza la siguiente ronda (o la primera).</summary>
    public void BeginNextRound()
    {
        int next = CurrentRound < startingRound ? startingRound : CurrentRound + 1;
        BeginRound(next);
    }

    public void BeginRound(int round)
    {
        CurrentRound = Mathf.Max(startingRound, round);
        TotalThisRound = ZombiesForRound(CurrentRound);
        pendingToSpawn = TotalThisRound;
        alive.Clear();
        RoundInProgress = true;

        OnRoundStarted?.Invoke(CurrentRound);
        OnAliveChanged?.Invoke(0);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(SpawnRoutine());
    }

    /// <summary>Detiene la ronda en curso (al morir el jugador).</summary>
    public void StopRound()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        RoundInProgress = false;
        pendingToSpawn = 0;
    }

    /// <summary>Zombies que componen una ronda concreta.</summary>
    public int ZombiesForRound(int round)
    {
        if (zombiesPerFirstRounds != null && zombiesPerFirstRounds.Length > 0)
        {
            int index = round - startingRound;
            if (index >= 0 && index < zombiesPerFirstRounds.Length)
                return zombiesPerFirstRounds[index];
        }

        // A partir de la ultima ronda listada, crece un 30% por ronda.
        int last = (zombiesPerFirstRounds != null && zombiesPerFirstRounds.Length > 0)
            ? zombiesPerFirstRounds[zombiesPerFirstRounds.Length - 1]
            : 8;
        int extraRounds = round - (startingRound + Mathf.Max(0, (zombiesPerFirstRounds?.Length ?? 1) - 1));

        float value = last;
        for (int i = 0; i < extraRounds; i++) value *= growthAfterFirstRounds;
        return Mathf.Max(1, Mathf.RoundToInt(value));
    }

    private float SpawnIntervalForRound(int round)
    {
        int steps = Mathf.Max(0, round - spawnIntervalDecreaseStartsAtRound);
        return Mathf.Max(minSpawnInterval, baseSpawnInterval - steps * spawnIntervalDecreasePerRound);
    }

    private float HealthMultiplierForRound(int round)
    {
        if (round < healthScaleStartsAtRound) return 1f;
        return 1f + healthScalePerRound * (round - healthScaleStartsAtRound);
    }

    private float DamageMultiplierForRound(int round)
    {
        return 1f + damageScalePerRound * (round - 1);
    }

    private IEnumerator SpawnRoutine()
    {
        float interval = SpawnIntervalForRound(CurrentRound);

        while (pendingToSpawn > 0)
        {
            if (alive.Count < maxAlive)
            {
                // Se descuenta ANTES de instanciar: asi el aviso al HUD (que sale
                // dentro de SpawnOne) ya cuenta el zombie como desplegado.
                pendingToSpawn--;
                SpawnOne();
            }
            yield return new WaitForSeconds(interval);
        }

        // Si ya no queda ninguno vivo, la ronda termina aqui mismo.
        if (alive.Count == 0) FinishRound();
    }

    private void SpawnOne()
    {
        if (spawner == null) return;

        GameObject go = spawner.SpawnOne();
        if (go == null) return;

        ZombieController zombie = go.GetComponent<ZombieController>();
        if (zombie == null) return;

        zombie.ApplyScaling(HealthMultiplierForRound(CurrentRound), DamageMultiplierForRound(CurrentRound));
        alive.Add(zombie);
        OnAliveChanged?.Invoke(alive.Count);
    }

    private void HandleZombieDied(ZombieController zombie)
    {
        if (!alive.Remove(zombie)) return;

        OnAliveChanged?.Invoke(alive.Count);

        if (RoundInProgress && alive.Count == 0 && pendingToSpawn == 0)
            FinishRound();
    }

    private void FinishRound()
    {
        if (!RoundInProgress) return;

        RoundInProgress = false;
        routine = null;
        OnRoundCleared?.Invoke(CurrentRound);
    }
}
