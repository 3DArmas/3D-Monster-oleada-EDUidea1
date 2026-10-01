using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Servicio de aparicion de zombies: instancia en los puntos configurados.
/// En la Fase 1 lo controla el RoundManager (deja autoStartWaves DESACTIVADO).
/// </summary>
public class ZombieSpawner : MonoBehaviour
{
    [Header("Prefab Zombie")]
    [SerializeField] private GameObject zombiePrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Oleadas (modo autonomo, sin RoundManager)")]
    [SerializeField] private int zombiesPerWave = 5;
    [SerializeField] private float waveDelay = 10f;
    [SerializeField] private float spawnInterval = 0.5f;
    [SerializeField] private int maxZombiesAlive = 15;

    [Header("Modo de uso")]
    [Tooltip("Activalo solo si quieres las oleadas antiguas. Con el RoundManager debe quedar DESACTIVADO.")]
    [SerializeField] private bool autoStartWaves = false;

    private int currentWave;
    private List<GameObject> aliveZombies = new List<GameObject>();
    private bool isSpawning;

    private void Start()
    {
        if (autoStartWaves) StartCoroutine(WaveLoop());
    }

    /// <summary>
    /// Instancia un zombie en un punto de spawn aleatorio y lo devuelve.
    /// Es el punto de entrada que usa el RoundManager.
    /// </summary>
    public GameObject SpawnOne()
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || zombiePrefab == null) return null;

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject zombie = Instantiate(zombiePrefab, point.position, point.rotation);
        aliveZombies.Add(zombie);

        var zc = zombie.GetComponent<ZombieController>();
        if (zc != null) zc.enabled = true;

        return zombie;
    }

    /// <summary>Numero de puntos de spawn configurados.</summary>
    public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;
    public int MaxZombiesAlive => maxZombiesAlive;

    // ------------------------------------------------------------------
    // Modo autonomo (comportamiento original). Solo si autoStartWaves = true.
    // ------------------------------------------------------------------

    private IEnumerator WaveLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(waveDelay);
            currentWave++;
            int count = zombiesPerWave + (currentWave - 1) * 2;
            yield return StartCoroutine(SpawnWave(count));
        }
    }

    private IEnumerator SpawnWave(int count)
    {
        if (isSpawning) yield break;
        isSpawning = true;

        for (int i = 0; i < count; i++)
        {
            if (aliveZombies.Count >= maxZombiesAlive)
            {
                yield return new WaitUntil(() => aliveZombies.Count < maxZombiesAlive);
            }

            SpawnOne();
            yield return new WaitForSeconds(spawnInterval);
        }

        isSpawning = false;
    }

    private void Update()
    {
        aliveZombies.RemoveAll(z => z == null);
    }

    public int CurrentWave => currentWave;
    public int AliveCount => aliveZombies.Count;
}
