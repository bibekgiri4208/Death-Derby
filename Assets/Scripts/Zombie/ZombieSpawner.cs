using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Prefabs & References")]
    public GameObject zombiePrefab;
    public Transform playerCar;
    public string playerTag = "Player";

    [Header("Spawn Settings")]
    public int maxZombiesInScene = 20;
    public float spawnInterval = 1.5f;
    public float minDistanceFromPlayer = 12f;
    public float maxDistanceFromPlayer = 35f;

    [Header("Runtime Info (Read Only)")]
    public int currentAliveZombies = 0;

    private void Start()
    {
        // Auto-assign player car by tag if missing
        if (playerCar == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
            {
                playerCar = playerObj.transform;
            }
            else
            {
                Debug.LogWarning($"ZombieSpawner on {gameObject.name}: No GameObject found with tag '{playerTag}'!");
            }
        }

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // Clean up count of dead/destroyed zombies
            CleanUpDeadCount();

            // Only spawn if below active scene cap
            if (currentAliveZombies < maxZombiesInScene && playerCar != null)
            {
                SpawnZombie();
            }
        }
    }

    private void SpawnZombie()
    {
        if (zombiePrefab == null || playerCar == null) return;

        Vector3 spawnPos = GetRandomNavMeshPositionNearPlayer();

        if (spawnPos != Vector3.zero)
        {
            // Instantiate prefab slightly offset upward so pivot doesn't bury model
            GameObject newZombie = Instantiate(zombiePrefab, spawnPos + Vector3.up * 0.1f, Quaternion.identity);

            // Snap agent cleanly to NavMesh surface feet level
            NavMeshAgent agent = newZombie.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.Warp(spawnPos);
            }

            // Assign target player car
            ZombieAI zombieScript = newZombie.GetComponent<ZombieAI>();
            if (zombieScript != null)
            {
                zombieScript.playerCar = playerCar;
                zombieScript.playerTag = playerTag;
            }

            currentAliveZombies++;
        }
    }

    private Vector3 GetRandomNavMeshPositionNearPlayer()
    {
        // Try up to 10 random positions around the player car
        for (int i = 0; i < 10; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
            Vector3 randomPos = playerCar.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Sample nearest NavMesh point within 5 units
            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return Vector3.zero; // Couldn't locate valid point this frame
    }

    private void CleanUpDeadCount()
    {
        ZombieAI[] allZombies = FindObjectsByType<ZombieAI>(FindObjectsInactive.Exclude);
        int activeCount = 0;

        foreach (ZombieAI z in allZombies)
        {
            if (!z.isDead)
            {
                activeCount++;
            }
        }

        currentAliveZombies = activeCount;
    }
}