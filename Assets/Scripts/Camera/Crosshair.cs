using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class Crosshair : MonoBehaviour
{
    void Update()
    {
        transform.position = Input.mousePosition;
    }
}
