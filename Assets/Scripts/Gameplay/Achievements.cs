using UnityEngine;

// Central registry of every in-game achievement, so both the toast system (MilestoneTracker) and the
// in-game Achievements menu speak the same language. Unlock state is a simple PlayerPrefs flag per key
// (the same keys MilestoneTracker sets), so it persists and can also be read by the launcher.
public static class Achievements
{
    public struct Def
    {
        public string Key;
        public string Title;
        public string Desc;
    }

    public static readonly Def[] All =
    {
        new Def { Key = "ms_h_50",   Title = "Erste Schritte",     Desc = "Erreiche 50 Meter Höhe" },
        new Def { Key = "ms_h_100",  Title = "Höhenluft",          Desc = "Erreiche 100 Meter Höhe" },
        new Def { Key = "ms_h_200",  Title = "Hoch hinaus",        Desc = "Erreiche 200 Meter Höhe" },
        new Def { Key = "ms_h_350",  Title = "Schwindelfrei",      Desc = "Erreiche 350 Meter Höhe" },
        // The base tower tops out at ~362 m - everything higher is only reachable in Endless.
        new Def { Key = "ms_h_500",  Title = "Endlos-Luft",        Desc = "Erreiche 500 Meter Höhe (Endlos-Modus)" },
        new Def { Key = "ms_h_1000", Title = "Höhenrausch",        Desc = "Erreiche 1000 Meter Höhe (Endlos-Modus)" },
        new Def { Key = "ms_summit", Title = "Gipfelstürmer",      Desc = "Erreiche die Spitze der Sternenkrone" },
        new Def { Key = "ms_endless_summit", Title = "Endlos-Krone", Desc = "Erreiche die Flagge ganz oben im Endlos-Turm" },
        new Def { Key = "ms_boss",   Title = "Wächter-Bezwinger", Desc = "Besiege einen Gipfelwächter" },
        new Def { Key = "ms_nodeath",Title = "Makellos",           Desc = "Erreiche den Gipfel ohne einen einzigen Tod" },
        new Def { Key = "ms_secret", Title = "Schatzsucher",       Desc = "Plündere eine Schatzkiste (nur per Zipline!)" },
        new Def { Key = "ms_lucky",  Title = "Glücksfund",         Desc = "Sammle eine legendäre Münze" },
        new Def { Key = "ms_c_100",  Title = "Sammler",            Desc = "Sammle insgesamt 100 Münzen" },
        new Def { Key = "ms_c_500",  Title = "Wohlhabend",         Desc = "Sammle insgesamt 500 Münzen" },
        new Def { Key = "ms_c_1000", Title = "Reich",              Desc = "Sammle insgesamt 1000 Münzen" },
        new Def { Key = "ms_c_2500", Title = "Münz-Legende",       Desc = "Sammle insgesamt 2500 Münzen" },
        new Def { Key = "ms_allskins",Title = "Modeikone",         Desc = "Schalte alle Skins frei" },
        new Def { Key = "ms_allacc", Title = "Voll ausgestattet",  Desc = "Sammle alle Accessoires" },
    };

    public static bool IsUnlocked(string key) => PlayerPrefs.GetInt(key, 0) == 1;

    public static int UnlockedCount()
    {
        int c = 0;
        foreach (var a in All)
            if (IsUnlocked(a.Key))
                c++;
        return c;
    }
}
