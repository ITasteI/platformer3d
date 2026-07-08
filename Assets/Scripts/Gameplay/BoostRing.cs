using UnityEngine;

// A glowing air ring: pass through it (jump or glide) and it launches you upward - the sky's
// answer to a bounce pad. Placed as optional shortcuts, so it rewards spotting them, never punishes.
[RequireComponent(typeof(Collider))]
public class BoostRing : MonoBehaviour
{
    public float boostVelocity = 15f;
    private float lastUse = -9f;

    void Update() => transform.Rotate(Vector3.up, 35f * Time.deltaTime, Space.World);

    void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastUse < 0.3f)
            return;
        var pc = other.GetComponent<PlayerController>();
        if (pc == null || !pc.IsOwner)
            return;

        lastUse = Time.time;
        pc.ApplyBounce(boostVelocity);
        AudioManager.Instance?.PlayWhoosh();
        EffectsManager.Instance?.PlaySparkle(transform.position);
    }
}
