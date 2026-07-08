using UnityEngine;

// A small DAILY goal, generated deterministically from today's date - no backend needed, every
// player gets the same challenge on the same day. Completing it once per day pays a coin bonus.
// Progress is polled by GameManager from per-run stats; completion is a PlayerPrefs flag per date.
public static class DailyChallenge
{
    public enum Kind { ReachHeight, CollectCoins, KillEnemies, FindSecret }

    public struct Def
    {
        public Kind Kind;
        public int Target;
        public string Text;
        public int Reward;
    }

    public static Def Today
    {
        get
        {
            var d = System.DateTime.Now;
            int seed = d.Year * 1000 + d.DayOfYear;
            switch (seed % 4)
            {
                case 0:
                {
                    int h = 120 + (seed / 4 % 4) * 40; // 120..240 m
                    return new Def { Kind = Kind.ReachHeight, Target = h, Text = $"Erreiche {h} m Höhe", Reward = 100 };
                }
                case 1:
                {
                    int c = 30 + (seed / 4 % 3) * 10; // 30..50 coins in one run
                    return new Def { Kind = Kind.CollectCoins, Target = c, Text = $"Sammle {c} Münzen in einem Lauf", Reward = 100 };
                }
                case 2:
                {
                    int k = 3 + (seed / 4 % 3); // 3..5 wraiths
                    return new Def { Kind = Kind.KillEnemies, Target = k, Text = $"Besiege {k} Schatten in einem Lauf", Reward = 120 };
                }
                default:
                    return new Def { Kind = Kind.FindSecret, Target = 1, Text = "Plündere eine Schatzkiste", Reward = 150 };
            }
        }
    }

    static string DoneKey => "daily_done_" + System.DateTime.Now.ToString("yyyyMMdd");

    public static bool DoneToday => PlayerPrefs.GetInt(DoneKey, 0) == 1;

    // Polled by GameManager with the current run's stats; fires the reward exactly once per day.
    public static void Check(float height, int runCoins, int runKills, int runSecrets)
    {
        if (DoneToday)
            return;

        Def def = Today;
        bool done = def.Kind switch
        {
            Kind.ReachHeight => height >= def.Target,
            Kind.CollectCoins => runCoins >= def.Target,
            Kind.KillEnemies => runKills >= def.Target,
            _ => runSecrets >= def.Target,
        };
        if (!done)
            return;

        PlayerPrefs.SetInt(DoneKey, 1);
        PlayerPrefs.Save();
        GameManager.Instance?.AddCoins(def.Reward);
        EconomySystem.AddCoins(def.Reward);
        AudioManager.Instance?.PlayVictory();
        MilestoneTracker.Instance?.ShowToast($"Tages-Challenge geschafft!  +{def.Reward} Münzen", UITheme.Gold, "☀");
    }
}
