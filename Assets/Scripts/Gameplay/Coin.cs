using UnityEngine;

public enum CoinType
{
    Normal,
    Rare,
    Legendary,
}

[RequireComponent(typeof(Collider))]
public class Coin : MonoBehaviour
{
    public CoinType type = CoinType.Normal;
    public float spinSpeed = 90f;
    public float bobHeight = 0.25f;
    public float bobSpeed = 2f;
    // Set once by SceneBuilder right after instantiation, before Awake's first frame runs.
    [HideInInspector] public float legendaryBaseScale = 1f;

    public static int ValueFor(CoinType t)
    {
        switch (t)
        {
            case CoinType.Rare: return 5;
            case CoinType.Legendary: return 25;
            default: return 1;
        }
    }

    private Vector3 startPos;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        startPos = transform.position;

        if (type == CoinType.Rare)
        {
            spinSpeed *= 1.4f;
        }
        else if (type == CoinType.Legendary)
        {
            spinSpeed *= 2f;
            bobHeight *= 1.6f;
            bobSpeed *= 1.5f;
        }
    }

    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        float y = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        if (type == CoinType.Legendary)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.12f;
            transform.localScale = Vector3.one * pulse * legendaryBaseScale;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner)
            return;

        int value = ValueFor(type);
        GameManager.Instance.AddCoins(value);
        EconomySystem.AddCoins(value);
        AudioManager.Instance?.PlayCoin();
        EffectsManager.Instance?.PlaySparkle(transform.position);
        Destroy(gameObject);
    }
}
