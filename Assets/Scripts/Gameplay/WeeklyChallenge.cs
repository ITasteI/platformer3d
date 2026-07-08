using UnityEngine;

// A WEEKLY goal alongside the daily one: bigger targets, bigger reward, and progress that
// accumulates ACROSS runs during the week (persisted in PlayerPrefs, keyed by the week stamp).
// Deterministic from the calendar week - no backend, everyone gets the same challenge.
public static class WeeklyChallenge
{
    public enum Kind { CoinsTotal, KillsTotal, ReachHeight, WinRuns }

    public struct Def
    {
        public Kind Kind;
        public int Target;
        public string Text;
        public int Reward;
    }

    static int WeekStamp
    {
        get
        {
            var d = System.DateTime.Now;
            return d.Year * 100 + (d.DayOfYear - 1) / 7;
        }
    }

    static string K(string s) => "weekly_" + s + "_" + WeekStamp;

    public static Def Current
    {
        get
        {
            int seed = WeekStamp;
            switch (seed % 4)
            {
                case 0:
                {
                    int c = 150 + (seed / 4 % 3) * 50; // 150..250 coins over the week
                    return new Def { Kind = Kind.CoinsTotal, Target = c, Text = $"Sammle {c} Münzen diese Woche", Reward = 300 };
                }
                case 1:
                {
                    int k = 12 + (seed / 4 % 3) * 4; // 12..20 wraiths over the week
                    return new Def { Kind = Kind.KillsTotal, Target = k, Text = $"Besiege {k} Schatten diese Woche", Reward = 300 };
                }
                case 2:
                {
                    int h = 300 + (seed / 4 % 3) * 100; // 300..500 m in one run
                    return new Def { Kind = Kind.ReachHeight, Target = h, Text = $"Erreiche {h} m in einem Lauf", Reward = 350 };
                }
                default:
                    return new Def { Kind = Kind.WinRuns, Target = 2, Text = "Erreiche 2× die Gipfel-Flagge", Reward = 400 };
            }
        }
    }

    public static bool Done => PlayerPrefs.GetInt(K("done"), 0) == 1;

    // Cross-run counters, fed by gameplay events (cheap increments, saved with PlayerPrefs).
    public static void NoteKill() => Bump("kills");
    public static void NoteWin() => Bump("wins");

    static void Bump(string counter)
    {
        PlayerPrefs.SetInt(K(counter), PlayerPrefs.GetInt(K(counter), 0) + 1);
    }

    // Current progress toward the week's goal (for the menu display).
    public static int Progress(float height)
    {
        Def def = Current;
        switch (def.Kind)
        {
            case Kind.CoinsTotal: return Mathf.Max(0, EconomySystem.TotalCoinsEarned - CoinBaseline());
            case Kind.KillsTotal: return PlayerPrefs.GetInt(K("kills"), 0);
            case Kind.ReachHeight: return Mathf.RoundToInt(height);
            default: return PlayerPrefs.GetInt(K("wins"), 0);
        }
    }

    // Coins are measured against the week's starting wallet total, captured on first check.
    static int CoinBaseline()
    {
        if (!PlayerPrefs.HasKey(K("coinbase")))
        {
            PlayerPrefs.SetInt(K("coinbase"), EconomySystem.TotalCoinsEarned);
            PlayerPrefs.Save();
        }
        return PlayerPrefs.GetInt(K("coinbase"), 0);
    }

    // Polled ~1/s by GameManager; pays out exactly once per week.
    public static void Check(float height)
    {
        if (Done)
            return;

        Def def = Current;
        if (Progress(height) < def.Target)
            return;

        PlayerPrefs.SetInt(K("done"), 1);
        PlayerPrefs.Save();
        GameManager.Instance?.AddCoins(def.Reward);
        EconomySystem.AddCoins(def.Reward);
        AudioManager.Instance?.PlayVictory();
        MilestoneTracker.Instance?.ShowToast($"Wochen-Challenge geschafft!  +{def.Reward} Münzen", UITheme.Gold, "★");
    }
}
