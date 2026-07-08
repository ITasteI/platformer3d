using UnityEngine;

// Which way you're playing the same tower:
//  Klassisch  - climb to the flag, race your best completion time.
//  Zeitrennen - reach the flag before a countdown runs out (Time-Attack).
//  Endlos     - no goal, just climb as high as you can; your best height is saved.
//  Hardcore   - ONE life: the first death ends the run.
public enum GameMode { Klassisch, Zeitrennen, Endlos, Hardcore }

public static class GameModeState
{
    public static GameMode Current = GameMode.Klassisch;

    public const float TimeAttackLimit = 1200f; // 20 minutes

    public static string Name(GameMode m)
    {
        switch (m)
        {
            case GameMode.Zeitrennen: return "Zeitrennen";
            case GameMode.Endlos: return "Endlos";
            case GameMode.Hardcore: return "Hardcore";
            default: return "Klassisch";
        }
    }

    // Best height reached in Endless mode (persisted).
    public static float BestHeight => PlayerPrefs.GetFloat("EndlessBestHeight", 0f);

    public static void ReportHeight(float h)
    {
        if (h > BestHeight)
        {
            PlayerPrefs.SetFloat("EndlessBestHeight", h);
            PlayerPrefs.Save();
        }
    }
}
