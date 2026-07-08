using System.Collections;
using UnityEngine;

public enum CoinType
{
    Normal,
    Rare,
    Epic,
    Legendary,
}

[RequireComponent(typeof(Collider))]
public class Coin : MonoBehaviour
{
    public CoinType type = CoinType.Normal;
    public float spinSpeed = 90f;
    public float bobHeight = 0.25f;
    public float bobSpeed = 2f;
    // Longer respawn so coins can't be farmed as fast - earning cosmetics should take a while.
    public float respawnSeconds = 20f;
    // Set once by SceneBuilder right after instantiation, before Awake's first frame runs.
    [HideInInspector] public float legendaryBaseScale = 1f;

    public static int ValueFor(CoinType t)
    {
        switch (t)
        {
            case CoinType.Rare: return 5;
            case CoinType.Epic: return 10;
            case CoinType.Legendary: return 25;
            default: return 1;
        }
    }

    private Vector3 startPos;
    private Collider col;
    private Renderer[] renderers;
    private bool collected;
    private int perfSeed;   // staggers the distance checks across frames
    private bool farAway;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        renderers = GetComponentsInChildren<Renderer>();
        startPos = transform.position;
        // Position-derived stagger seed (GetInstanceID is deprecated in Unity 6).
        perfSeed = (Mathf.RoundToInt(transform.position.x * 3f) ^ Mathf.RoundToInt(transform.position.y * 3f)) & 15;

        if (type == CoinType.Rare)
        {
            spinSpeed *= 1.4f;
        }
        else if (type == CoinType.Epic)
        {
            spinSpeed *= 1.7f;
            bobHeight *= 1.3f;
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
        // Skip all animation while collected/hidden - the coin is invisible and non-interactive
        // during its respawn wait, so there's nothing to spin or bob.
        if (collected)
            return;

        // Perf: coins beyond ~60 m don't animate (fog hides them; ~550 coins tick in Endless).
        // The distance check itself is staggered so only 1/16 of coins measure per frame.
        if ((Time.frameCount + perfSeed) % 16 == 0)
        {
            var p = GameManager.Instance != null ? GameManager.Instance.player : null;
            farAway = p != null && (transform.position - p.position).sqrMagnitude > 3600f;
        }
        if (farAway)
            return;

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        float y = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(startPos.x, y, startPos.z);

        if (type == CoinType.Legendary)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.12f;
            transform.localScale = Vector3.one * pulse * legendaryBaseScale;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner)
            return;

        // Apply the combo multiplier from a fast pickup chain (x1 when no GameManager / no chain).
        int multiplier = GameManager.Instance != null ? GameManager.Instance.RegisterCoinPickup() : 1;
        int value = ValueFor(type) * multiplier;
        GameManager.Instance?.AddCoins(value);
        EconomySystem.AddCoins(value);
        AudioManager.Instance?.PlayCoin();
        EffectsManager.Instance?.PlaySparkle(transform.position);
        // Legendary coins are a lucky roadside find, not a hidden secret - "ms_secret" stays
        // exclusive to the glide-only SecretShards so that achievement keeps its meaning.
        if (type == CoinType.Legendary)
            MilestoneTracker.Instance?.Unlock("ms_lucky", "Glücksfund - legendäre Münze!");

        // Instead of destroying, hide + disable and respawn after a delay so coins can be farmed.
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        collected = true;
        SetVisible(false);
        yield return new WaitForSeconds(respawnSeconds);
        SetVisible(true);
        collected = false;
    }

    void SetVisible(bool visible)
    {
        col.enabled = visible;
        foreach (var r in renderers)
            r.enabled = visible;
    }
}
