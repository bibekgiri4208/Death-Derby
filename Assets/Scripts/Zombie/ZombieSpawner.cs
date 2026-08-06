using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Prefabs & Targets")]
    public GameObject zombiePrefab;
    public Transform playerCar;
    public string playerTag = "Player";

    [Header("Spawn Points")]
    [Tooltip("Drag all your designated Spawn Point GameObjects here.")]
    public Transform[] spawnPoints;

    [Header("Spawn Controls")]
    public int maxZombiesInScene = 15;
    public float spawnInterval = 1.5f;

    [Header("Distance Filters")]
    public float minDistanceFromPlayer = 10f;
    public float maxDistanceFromPlayer = 60f;

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

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("ZombieSpawner: No spawn points assigned in the Inspector!");
            return;
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
                SpawnFromSpawnPoint();
            }
        }
    }

    private void SpawnFromSpawnPoint()
    {
        if (zombiePrefab == null || spawnPoints.Length == 0) return;

        List<Transform> validSpawnPoints = new List<Transform>();

        // Find spawn points within distance range and not blocked by another zombie
        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;

            float distToPlayer = Vector3.Distance(sp.position, playerCar.position);

            if (distToPlayer >= minDistanceFromPlayer && distToPlayer <= maxDistanceFromPlayer)
            {
                Collider[] overlaps = Physics.OverlapSphere(sp.position, 1.5f);
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
                    validSpawnPoints.Add(sp);
                }
            }
        }

        // Fallback if no spawn point passed distance filter
        if (validSpawnPoints.Count == 0)
        {
            foreach (Transform sp in spawnPoints)
            {
                if (sp == null) continue;

                Collider[] overlaps = Physics.OverlapSphere(sp.position, 1.5f);
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
                    validSpawnPoints.Add(sp);
                }
            }
        }

        // Pick a spawn point and instantiate
        if (validSpawnPoints.Count > 0)
        {
            int randomIndex = Random.Range(0, validSpawnPoints.Count);
            Transform chosenPoint = validSpawnPoints[randomIndex];

            if (NavMesh.SamplePosition(chosenPoint.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                // Calculate center height for a single Unity default primitive capsule (+1.0 Y offset)
                Vector3 targetSpawnPos = hit.position + Vector3.up * 1.0f;

                // Instantiate prefab
                GameObject newZombie = Instantiate(zombiePrefab, targetSpawnPos, chosenPoint.rotation);

                // Fix frame-0 auto-snap race condition
                NavMeshAgent agent = newZombie.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                    newZombie.transform.position = targetSpawnPos;
                    agent.enabled = true;
                    agent.Warp(targetSpawnPos);
                }

                ZombieAI ai = newZombie.GetComponent<ZombieAI>();
                if (ai != null)
                {
                    ai.playerCar = playerCar;
                    ai.playerTag = playerTag;
                }

                currentAliveZombies++;
            }
        }
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