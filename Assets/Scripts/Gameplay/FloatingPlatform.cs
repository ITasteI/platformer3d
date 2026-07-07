using UnityEngine;

[DefaultExecutionOrder(-10)]
public class FloatingPlatform : MonoBehaviour, IMovingSurface
{
    public float amplitude = 0.4f;
    public float speed = 1f;

    public Vector3 FrameDelta { get; private set; }

    private Vector3 basePos;

    void Awake()
    {
        basePos = transform.position;

        BoxCollider rideTrigger = gameObject.AddComponent<BoxCollider>();
        rideTrigger.isTrigger = true;
        ColliderUtil.FitToRenderBounds(transform, rideTrigger);
    }

    void Update()
    {
        Vector3 newPos = basePos + Vector3.up * (Mathf.Sin(PlatformClock.Time * speed) * amplitude);
        FrameDelta = newPos - transform.position;
        transform.position = newPos;
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && player.IsOwner)
            player.SetCurrentPlatform(this);
    }

    void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && player.IsOwner)
            player.ClearCurrentPlatform(this);
    }
}
