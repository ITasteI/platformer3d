using UnityEngine;

// Night fauna: a glowing sky-jellyfish drifting slowly up and down through the cloud layer, body
// gently pulsing. Pure atmosphere - no collider, culled by distance.
public class JellyfishDrift : MonoBehaviour
{
    public float riseSpeed = 0.5f;
    public float range = 12f;

    private Vector3 home;
    private Vector3 baseScale;
    private float phase;

    void Start()
    {
        home = transform.position;
        baseScale = transform.localScale;
        phase = Random.value * 6.2831f;
    }

    void Update()
    {
        float t = Time.time;
        float y = home.y + Mathf.PingPong(t * riseSpeed + phase * 3f, range);
        transform.position = new Vector3(
            home.x + Mathf.Sin(t * 0.4f + phase) * 1.6f,
            y,
            home.z + Mathf.Cos(t * 0.33f + phase) * 1.6f);

        // Bell pulse: the body squeezes rhythmically like a swimming jelly.
        float pulse = 1f + Mathf.Sin(t * 2.1f + phase) * 0.12f;
        transform.localScale = new Vector3(baseScale.x, baseScale.y * pulse, baseScale.z);
    }
}
