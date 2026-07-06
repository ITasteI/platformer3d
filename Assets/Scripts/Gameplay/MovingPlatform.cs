using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Vector3 moveOffset = new Vector3(3.5f, 0f, 0f);
    public float speed = 1f;

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
        transform.position = startPos + moveOffset * Mathf.Sin(Time.time * speed);
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
