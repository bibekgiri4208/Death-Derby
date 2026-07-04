using UnityEngine;

public class CarSelectionManager : MonoBehaviour
{
    [Header("Car Data")]
    public GameObject[] carPrefabs; // Drop your low-poly vehicle prefabs here
    private int currentCarIndex = 0;
    private GameObject spawnedCar;

    [Header("Spawn Settings")]
    public Transform spawnLocation; // The invisible object 5 meters in the air
    public float dropHeight = 5.0f;

    void Start()
    {
        // Spawns the first car when you press Play
        CycleCar(currentCarIndex);
    }

    public void NextCarButton()
    {
        // Moves to the next car index and loops back to 0 at the end
        currentCarIndex = (currentCarIndex + 1) % carPrefabs.Length;

        // Triggers the car switch
        CycleCar(currentCarIndex);
    }

    void CycleCar(int index)
    {
        // 1. Clear out the old car if it exists
        if (spawnedCar != null)
        {
            Destroy(spawnedCar);
        }

        // 2. Calculate the drop coordinates
        Vector3 dropPosition = spawnLocation.position + new Vector3(0, dropHeight, 0);

        // 3. Spawn the new car at those coordinates
        spawnedCar = Instantiate(carPrefabs[index], dropPosition, Quaternion.identity);

        // 4. Force the physics engine to make it drop
        Rigidbody rb = spawnedCar.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
        }
    }
}