using UnityEngine;

/// <summary>
/// Spawns the car that was selected in the Garage as the player car in the
/// Desert. The prefab order must match the Garage's CarSelection "Cars" array.
/// Runs in Awake so every Start (including CarFollowCamera) sees the new car.
/// </summary>
public class DesertCarSpawner : MonoBehaviour
{
    private const string PlayerTag = "Player";
    private const string CarIndexKey = "CarIndexValue";

    [Header("Player Cars")]
    [Tooltip("Must match the order of the Garage CarSelection 'Cars' array.")]
    [SerializeField] private GameObject[] carPrefabs;

    [Header("Spawn")]
    [Tooltip("Optional. When empty, spawnPosition/spawnEulerAngles are used instead.")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector3 spawnPosition = new Vector3(500f, 0.13f, 500f);
    [SerializeField] private Vector3 spawnEulerAngles = Vector3.zero;

    [Header("Player")]
    [SerializeField] private int playerIndex = 0;

    void Awake()
    {
        SpawnSelectedCar();
    }

    private void SpawnSelectedCar()
    {
        if (carPrefabs == null || carPrefabs.Length == 0)
        {
            Debug.LogError("DesertCarSpawner: no car prefabs assigned.", this);
            return;
        }

        int index = Mathf.Clamp(PlayerPrefs.GetInt(CarIndexKey, 0), 0, carPrefabs.Length - 1);
        GameObject prefab = carPrefabs[index];

        if (prefab == null)
        {
            Debug.LogError("DesertCarSpawner: car prefab at index " + index + " is missing.", this);
            return;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : spawnPosition;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.Euler(spawnEulerAngles);

        GameObject car = Instantiate(prefab, position, rotation);
        car.name = prefab.name;
        car.tag = PlayerTag;

        CarController controller = car.GetComponent<CarController>();
        if (controller != null)
        {
            controller.playerIndex = playerIndex;
        }

        AssignCameraTarget(car.transform);
    }

    private void AssignCameraTarget(Transform carTransform)
    {
        foreach (CarFollowCamera camera in FindObjectsByType<CarFollowCamera>(FindObjectsInactive.Include))
        {
            camera.target = carTransform;
        }
    }
}
