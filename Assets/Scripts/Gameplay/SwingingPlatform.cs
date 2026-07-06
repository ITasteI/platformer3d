using UnityEngine;

public class SwingingPlatform : MonoBehaviour
{
    public float armLength = 3f;
    public float swingAngleDeg = 45f;
    public float speed = 1f;

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
        transform.position = pivot + offset;
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && player.IsOwner)
            other.transform.SetParent(transform);
    }

    void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && player.IsOwner)
            other.transform.SetParent(null);
    }
}
