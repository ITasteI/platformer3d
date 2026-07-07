using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Hazard : MonoBehaviour
{
    public float rotationSpeed = 180f;
    public bool spins = true;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (spins)
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && player.IsOwner)
            player.Die();
    }
}
