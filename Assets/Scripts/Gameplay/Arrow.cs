using UnityEngine;

// A flying arrow projectile. Moves along its forward each frame (raycasting the step so it never
// tunnels through fast), deals damage to the first Enemy it hits, and sticks briefly on solid geometry.
// Passes through non-enemy triggers (coins, checkpoints). Spawned by PlayerController on left-click.
public class Arrow : MonoBehaviour
{
    public float speed = 42f;
    public float damage = 20f;
    public float life = 3.5f;

    private bool stuck;

    void Start() => Destroy(gameObject, life);

    void Update()
    {
        if (stuck)
            return;

        float step = speed * Time.deltaTime;
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Collide))
        {
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, hit.point); // upper-body hits count as headshots (x2)
                Destroy(gameObject);
                return;
            }
            // Never stick to ANY player (including the shooter's own capsule right at spawn).
            if (hit.collider.GetComponentInParent<PlayerController>() != null)
            {
                transform.position += transform.forward * step;
                return;
            }
            if (!hit.collider.isTrigger)
            {
                // Solid hit: stick where it landed and fade out shortly after.
                transform.position = hit.point;
                stuck = true;
                Destroy(gameObject, 1.2f);
                return;
            }
            // Non-enemy trigger (coin, checkpoint, ...): ignore and keep flying.
        }

        transform.position += transform.forward * step;
    }
}
