using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Prefabs & Targets")]
    public GameObject zombiePrefab;
    public Transform playerCar;
    public string playerTag = "Player";

    [Header("Spawn Controls")]
    public int maxZombiesInScene = 15;
    public float spawnInterval = 1.5f;
    public float minSpawnRadius = 15f;
    public float maxSpawnRadius = 30f;

    [Header("Runtime Status (Read Only)")]
    public int currentAliveZombies = 0;

    private void Start()
    {
        if (playerCar == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
            {
                playerCar = playerObj.transform;
            }
        }

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            UpdateAliveCount();

            if (currentAliveZombies < maxZombiesInScene && playerCar != null)
            {
                TrySpawnZombie();
            }
        }
    }

    private void TrySpawnZombie()
    {
        if (zombiePrefab == null || playerCar == null) return;

        Vector3 spawnPosition;
        if (GetClearSpawnPosition(out spawnPosition))
        {
            // 1. Instantiate prefab
            GameObject newZombie = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);

            // 2. Lock NavMeshAgent position explicitly
            NavMeshAgent agent = newZombie.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.Warp(spawnPosition);
                agent.nextPosition = spawnPosition; // Sync internal agent position with transform
            }

            // 3. Assign target
            ZombieAI ai = newZombie.GetComponent<ZombieAI>();
            if (ai != null)
            {
                ai.playerCar = playerCar;
                ai.playerTag = playerTag;
            }

            currentAliveZombies++;
        }
    }

    private bool GetClearSpawnPosition(out Vector3 result)
    {
        result = Vector3.zero;

        for (int i = 0; i < 15; i++)
        {
            // Pick random point in ring around player
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector3 candidatePos = playerCar.position + new Vector3(circle.x, 0f, circle.y);

            // Sample clean point on the flat terrain NavMesh
            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                // Check if any other zombie is standing within 1.5 meters of this point
                Collider[] overlaps = Physics.OverlapSphere(hit.position, 1.5f);
                bool occupied = false;

                foreach (Collider col in overlaps)
                {
                    if (col.GetComponent<ZombieAI>() != null)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied)
                {
                    result = hit.position;
                    return true;
                }
            }
        }

        return false;
    }

    private void UpdateAliveCount()
    {
        ZombieAI[] zombies = FindObjectsByType<ZombieAI>(FindObjectsInactive.Exclude);
        int count = 0;
        foreach (ZombieAI z in zombies)
        {
            if (!z.isDead) count++;
        }
        currentAliveZombies = count;
    }
}