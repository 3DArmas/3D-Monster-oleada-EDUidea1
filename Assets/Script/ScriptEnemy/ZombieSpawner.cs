using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Prefab Zombie")]
    [SerializeField] private GameObject zombiePrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Oleadas")]
    [SerializeField] private int zombiesPerWave = 5;
    [SerializeField] private float waveDelay = 10f;
    [SerializeField] private float spawnInterval = 0.5f;
    [SerializeField] private int maxZombiesAlive = 15;

    private int currentWave;
    private List<GameObject> aliveZombies = new List<GameObject>();
    private bool isSpawning;

    private void Start()
    {
        StartCoroutine(WaveLoop());
    }

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

            SpawnZombie();
            yield return new WaitForSeconds(spawnInterval);
        }

        isSpawning = false;
    }

    private void SpawnZombie()
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || zombiePrefab == null) return;

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject zombie = Instantiate(zombiePrefab, point.position, point.rotation);
        aliveZombies.Add(zombie);

        var zc = zombie.GetComponent<ZombieController>();
        if (zc != null) zc.enabled = true;
    }

    private void Update()
    {
        aliveZombies.RemoveAll(z => z == null);
    }

    public int CurrentWave => currentWave;
    public int AliveCount => aliveZombies.Count;
}
