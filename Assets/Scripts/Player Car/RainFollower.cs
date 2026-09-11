using UnityEngine;

public class RainFollower : MonoBehaviour
{
    [Tooltip("The object the rain should follow. Leave empty to follow the current parent (the car).")]
    [SerializeField] private Transform followTarget;

    [Tooltip("Keep the rain upright instead of inheriting the target's rotation.")]
    [SerializeField] private bool keepUpright = true;

    private Vector3 positionOffset;

    private void Start()
    {
        if (followTarget == null && transform.parent != null)
        {
            followTarget = transform.parent;
        }

        if (followTarget != null)
        {
            positionOffset = transform.position - followTarget.position;
            transform.SetParent(null, true);
        }
    }

    private void LateUpdate()
    {
        if (followTarget == null)
        {
            return;
        }

        transform.position = followTarget.position + positionOffset;

        if (keepUpright)
        {
            transform.rotation = Quaternion.identity;
        }
    }
}