using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    // Endless summit payout: generous one-time bonus per run (the climb is ~4x the classic one).
    // Static + reset on restart so re-entering the flag trigger can't be farmed.
    public const int EndlessBonus = 500;
    static bool endlessRewarded;
    public static void ResetEndlessReward() => endlessRewarded = false;

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner)
            return;

        MilestoneTracker.Instance?.Unlock("ms_summit", "Gipfel erreicht — du bist ganz oben!");

        if (GameModeState.Current == GameMode.Endlos)
        {
            // No win screen in Endless - the run stays open for the height record. But the true
            // summit (this flag only exists at the very top of the 5x tower) is celebrated properly.
            if (!endlessRewarded)
            {
                endlessRewarded = true;
                GameManager.Instance?.AddCoins(EndlessBonus);
                EconomySystem.AddCoins(EndlessBonus);
                AudioManager.Instance?.PlayVictory();
                MilestoneTracker.Instance?.Unlock("ms_endless_summit", "Endlos-Krone — ganz oben im Endlos-Turm!");
                MilestoneTracker.Instance?.ShowToast($"Endlos-Gipfel erreicht!  +{EndlessBonus} Münzen", UITheme.Gold, "★");
            }
        }
        else
        {
            WinScreen.Instance?.TriggerWin();
        }
    }
}
