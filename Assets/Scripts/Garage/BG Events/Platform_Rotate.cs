using UnityEngine;

public class PlatformRotate : MonoBehaviour
{
    public void FixedUpdate()
    {
        transform.Rotate(0, 0.2f, 0);
    }
}
