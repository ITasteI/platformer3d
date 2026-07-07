using UnityEngine;

// Spins in place around its vertical axis. Safe to stand on: rotation around Y keeps
// the top surface horizontal, so it never displaces the player. Used sparingly.
public class RotatingPlatform : MonoBehaviour
{
    public float rotationSpeed = 25f;

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }
}
