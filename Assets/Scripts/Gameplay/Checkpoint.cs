using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private bool activated;

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner)
            return;

        player.SetCheckpoint(transform.position + Vector3.up);

        int coins = GameManager.Instance != null ? GameManager.Instance.CoinCount : 0;
        SaveSystem.SaveCheckpoint(player.CheckpointPosition, coins);

        if (!activated)
        {
            activated = true;
            AudioManager.Instance?.PlayCheckpoint();
            EffectsManager.Instance?.PlaySparkle(transform.position);
        }
    }
}
