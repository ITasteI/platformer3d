using UnityEngine;

// Blinks solid/gone on a fixed cycle. Flickers for a short warning window before
// vanishing so disappearing is telegraphed rather than a surprise.
public class TimedPlatform : MonoBehaviour
{
    public float solidDuration = 3.2f;
    public float goneDuration = 1.2f;
    public float phaseOffset = 0f;
    public float warningTime = 0.6f;

    private Collider solidCollider;
    private Renderer[] renderers;

    void Awake()
    {
        solidCollider = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        float cycle = solidDuration + goneDuration;
        float phase = (Time.time + phaseOffset) % cycle;
        bool solid = phase < solidDuration;
        bool warning = solid && (solidDuration - phase) < warningTime;

        if (solidCollider != null)
            solidCollider.enabled = solid;

        bool visible = solid && (!warning || Mathf.PingPong(Time.time * 8f, 1f) > 0.3f);
        foreach (var r in renderers)
            r.enabled = visible;
    }
}
