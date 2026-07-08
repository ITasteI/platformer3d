using UnityEngine;

// The reward core of a TREASURE CHEST on a zipline-only secret island (see SceneBuilder's
// CreateZipSecret). Collecting pays a chunk of coins + fires the "secret found" achievement;
// only this glow orb hides on pickup, so the chest itself stays as a looted landmark.
[RequireComponent(typeof(Collider))]
public class SecretShard : MonoBehaviour
{
    public int coinReward = 60;

    private Collider col;
    private Renderer[] renderers;
    private bool taken;
    private float phase;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        renderers = GetComponentsInChildren<Renderer>();
        phase = Random.value * 6.2831f;
    }

    void Update()
    {
        if (taken)
            return;
        transform.Rotate(Vector3.up, 55f * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (taken)
            return;
        var pc = other.GetComponent<PlayerController>();
        if (pc == null || !pc.IsOwner)
            return;

        taken = true;
        GameManager.Instance?.AddCoins(coinReward);
        EconomySystem.AddCoins(coinReward);
        GameManager.Instance?.RegisterSecret(); // feeds the daily challenge
        AudioManager.Instance?.PlayCoin();
        EffectsManager.Instance?.PlaySparkle(transform.position);
        MilestoneTracker.Instance?.Unlock("ms_secret", "Schatzkiste gefunden!");
        MilestoneTracker.Instance?.ShowToast($"Schatzkiste geplündert!  +{coinReward} Münzen", UITheme.Gold, "✦");

        if (renderers != null)
            foreach (var r in renderers)
                if (r != null)
                    r.enabled = false;
        col.enabled = false;
    }
}
