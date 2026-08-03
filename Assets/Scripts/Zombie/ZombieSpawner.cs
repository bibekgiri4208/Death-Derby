using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Prefabs & References")]
    [Tooltip("The Zombie Prefab to spawn (must have ZombieAI script attached).")]
    public GameObject zombiePrefab;

    [Tooltip("Drag the Player Car Transform here, or leave empty to auto-find by Tag.")]
    public Transform playerCar;
    public string playerTag = "Player";

    [Header("Spawn Locations")]
    [Tooltip("Array of transforms where zombies can spawn.")]
    public Transform[] spawnPoints;

    [Header("Spawner Settings")]
    [Tooltip("Time in seconds between each zombie spawn.")]
    public float spawnInterval = 2f;

    [Tooltip("Maximum number of alive zombies allowed in the scene at once.")]
    public int maxZombiesInScene = 20;

    [Tooltip("Total zombies to spawn for this wave/round (Set to -1 for infinite).")]
    public int totalZombiesToSpawn = -1;

    [Header("Runtime Info (Read Only)")]
    public int currentAliveZombies = 0;
    public int totalSpawnedSoFar = 0;

    private void Start()
    {
        if (playerCar == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
            {
                playerCar = playerObj.transform;
            }
            else
            {
                Debug.LogWarning("ZombieSpawner: No Player Car found with tag '" + playerTag + "'!");
            }
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("ZombieSpawner: No spawn points assigned! Using Spawner's position instead.");
        }

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (totalZombiesToSpawn == -1 || totalSpawnedSoFar < totalZombiesToSpawn)
        {
            yield return new WaitForSeconds(spawnInterval);

            CleanUpDeadCount();

            if (currentAliveZombies < maxZombiesInScene)
            {
                SpawnZombie();
            }
        }
    }

    private void SpawnZombie()
    {
        if (zombiePrefab == null) return;

        Transform chosenSpawnPoint = transform;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            chosenSpawnPoint = spawnPoints[randomIndex];
        }

        GameObject newZombie = Instantiate(zombiePrefab, chosenSpawnPoint.position, chosenSpawnPoint.rotation);

        ZombieAI zombieScript = newZombie.GetComponent<ZombieAI>();
        if (zombieScript != null)
        {
            zombieScript.playerCar = playerCar;
            zombieScript.playerTag = playerTag;
        }

        currentAliveZombies++;
        totalSpawnedSoFar++;
    }

    private void CleanUpDeadCount()
    {
        // Unity 2023+ / Unity 6 safe method call
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

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;

        Gizmos.color = Color.red;
        foreach (Transform sp in spawnPoints)
        {
            if (sp != null)
            {
                Gizmos.DrawWireSphere(sp.position, 0.75f);
                Gizmos.DrawRay(sp.position, sp.forward * 1.5f);
            }
        }
    }
}