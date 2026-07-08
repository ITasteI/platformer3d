using UnityEngine;

// A hostile floating orb that patrols back and forth (with a gentle bob + spin) and resets the player
// on contact. Telegraphed by its red glow and avoidable by timing - an obstacle, not a fight. Placed
// only in the higher zones to ramp up tension as you climb.
[RequireComponent(typeof(Collider))]
public class PatrolHazard : MonoBehaviour
{
    public Vector3 patrolOffset = new Vector3(2.5f, 0f, 0f); // half of the back-and-forth travel
    public float speed = 1.3f;
    public float bob = 0.3f;
    public float spinSpeed = 80f;

    private Vector3 center;
    private float phase;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        center = transform.position;
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        Vector3 pos = center + patrolOffset * Mathf.Sin(Time.time * speed + phase);
        pos.y += Mathf.Sin(Time.time * 2f + phase) * bob;
        transform.position = pos;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && player.IsOwner)
            player.Die();
    }
}
