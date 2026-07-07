using UnityEngine;

[DefaultExecutionOrder(-10)]
public class SwingingPlatform : MonoBehaviour, IMovingSurface
{
    public float armLength = 3f;
    public float swingAngleDeg = 45f;
    public float speed = 1f;

    public Vector3 FrameDelta { get; private set; }

    private Vector3 pivot;
    private float baseAngle;

    void Awake()
    {
        pivot = transform.position + new Vector3(0f, armLength, 0f);
        baseAngle = 0f;

        BoxCollider rideTrigger = gameObject.AddComponent<BoxCollider>();
        rideTrigger.isTrigger = true;
        ColliderUtil.FitToRenderBounds(transform, rideTrigger);
    }

    void Update()
    {
        float angle = baseAngle + Mathf.Sin(Time.time * speed) * swingAngleDeg;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Sin(rad) * armLength, -Mathf.Cos(rad) * armLength, 0f);
        Vector3 newPos = pivot + offset;
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
