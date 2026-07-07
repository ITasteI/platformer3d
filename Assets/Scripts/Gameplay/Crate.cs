using System.Collections;
using UnityEngine;

// A breakable item crate, Crash-Bandicoot style: smash it (touch it) for a coin reward plus a
// satisfying burst, then it respawns after a delay so the route stays rewarding on repeat runs.
[RequireComponent(typeof(Collider))]
public class Crate : MonoBehaviour
{
    public int coins = 5;
    public float respawnSeconds = 12f;
    public float bobHeight = 0.12f;
    public float bobSpeed = 1.6f;
    public float spinSpeed = 25f;

    private Collider col;
    private Renderer[] renderers;
    private bool broken;
    private Vector3 startPos;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        renderers = GetComponentsInChildren<Renderer>();
        startPos = transform.position;
    }

    void Update()
    {
        if (broken)
            return;
        // Gentle bob + slow spin so crates read as collectible, not static scenery.
        float y = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(startPos.x, y, startPos.z);
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (broken)
            return;

        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner)
            return;

        GameManager.Instance?.AddCoins(coins);
        EconomySystem.AddCoins(coins);
        AudioManager.Instance?.PlayCoin();
        EffectsManager.Instance?.PlaySparkle(transform.position);
        EffectsManager.Instance?.PlayDust(transform.position);

        StartCoroutine(BreakRoutine());
    }

    IEnumerator BreakRoutine()
    {
        broken = true;
        SetVisible(false);
        yield return new WaitForSeconds(respawnSeconds);
        transform.position = startPos;
        SetVisible(true);
        broken = false;
    }

    void SetVisible(bool visible)
    {
        col.enabled = visible;
        foreach (var r in renderers)
            r.enabled = visible;
    }
}
