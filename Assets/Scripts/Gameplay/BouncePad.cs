using UnityEngine;

public class BouncePad : MonoBehaviour
{
    public float bounceForce = 16f;

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && player.IsOwner)
            player.ApplyBounce(bounceForce);
    }
}
