using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Coin : MonoBehaviour
{
    public float spinSpeed = 90f;
    public float bobHeight = 0.25f;
    public float bobSpeed = 2f;

    private Vector3 startPos;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        startPos = transform.position;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        float y = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner)
            return;

        GameManager.Instance.AddCoin();
        AudioManager.Instance?.PlayCoin();
        Destroy(gameObject);
    }
}
