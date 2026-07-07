using UnityEngine;

public class DistanceCuller : MonoBehaviour
{
    public float maxDistance = 120f;

    private Renderer[] renderers;
    private Transform cam;
    private float timer;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;
        timer = 0.5f;

        if (cam == null)
        {
            if (Camera.main == null)
                return;
            cam = Camera.main.transform;
        }

        bool visible = Vector3.Distance(cam.position, transform.position) < maxDistance;
        foreach (var r in renderers)
            r.enabled = visible;
    }
}
