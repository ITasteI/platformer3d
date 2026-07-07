using UnityEngine;

[DefaultExecutionOrder(-10)]
public class MovingPlatform : MonoBehaviour, IMovingSurface
{
    public Vector3 moveOffset = new Vector3(3.5f, 0f, 0f);
    public float speed = 1f;

    public Vector3 FrameDelta { get; private set; }

    private Vector3 startPos;

    void Awake()
    {
        startPos = transform.position;

        BoxCollider rideTrigger = gameObject.AddComponent<BoxCollider>();
        rideTrigger.isTrigger = true;
        ColliderUtil.FitToRenderBounds(transform, rideTrigger);
    }

    void Update()
    {
        Vector3 newPos = startPos + moveOffset * Mathf.Sin(PlatformClock.Time * speed);
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
