using UnityEngine;

// Night fauna: a small flock of bat silhouettes circling a point in the sky, wings flapping via a
// scale pulse. Pure atmosphere - no colliders, no interaction, culled by distance.
public class BatFlock : MonoBehaviour
{
    public float radius = 22f;
    public float speed = 0.5f;

    private Transform[] bats;
    private float[] phases;

    void Start()
    {
        int n = transform.childCount;
        bats = new Transform[n];
        phases = new float[n];
        for (int i = 0; i < n; i++)
        {
            bats[i] = transform.GetChild(i);
            phases[i] = i * (6.2831f / Mathf.Max(1, n));
        }
    }

    void Update()
    {
        if (bats == null)
            return;
        float t = Time.time;
        for (int i = 0; i < bats.Length; i++)
        {
            float a = t * speed + phases[i];
            Vector3 p = new Vector3(Mathf.Sin(a) * radius, Mathf.Sin(t * 0.7f + phases[i]) * 2.5f, Mathf.Cos(a) * radius);
            bats[i].localPosition = p;
            // Face travel direction, flap by squashing X.
            bats[i].localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)));
            float flap = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(t * 9f + phases[i]));
            bats[i].localScale = new Vector3(flap, 1f, 1f);
        }
    }
}
