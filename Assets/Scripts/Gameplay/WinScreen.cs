using UnityEngine;

public class WinScreen : MonoBehaviour
{
    public static WinScreen Instance { get; private set; }
    public static bool HasWon { get; private set; }

    public static void ClearWon() => HasWon = false;

    public const int RunBonus = 300; // coins awarded for finishing a run

    // Cached GUI styles (avoid per-frame allocations while the win screen is up).
    private static GUIStyle bonusStyle, deathStyle;

    private float finishTime;
    private bool isNewBest;
    private float bestTime;
    private int finishDeaths;
    private bool timedOut;
    private bool hardcoreDeath;
    private float finishHeight;

    static float PlayerHeight()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.player != null ? Mathf.Max(0f, gm.player.position.y) : 0f;
    }

    void Awake()
    {
        Instance = this;
        // HasWon is static and otherwise survives a scene reload ("Neu starten"),
        // leaving the win screen stuck on top of the fresh run forever.
        HasWon = false;
    }

    public void TriggerWin()
    {
        if (HasWon)
            return;

        HasWon = true;
        timedOut = false;
        finishTime = GameManager.Instance != null ? GameManager.Instance.PlayTime : 0f;
        finishDeaths = GameManager.Instance != null ? GameManager.Instance.RunDeaths : 0;
        finishHeight = PlayerHeight();
        if (finishDeaths == 0)
            MilestoneTracker.Instance?.Unlock("ms_nodeath", "Gipfel ohne einen einzigen Tod!");

        // Completion bonus.
        GameManager.Instance?.AddCoins(RunBonus);
        EconomySystem.AddCoins(RunBonus);
        WeeklyChallenge.NoteWin();

        // Best times are tracked PER MODE - a Zeitrennen win no longer overwrites the Klassisch record.
        bestTime = SaveSystem.BestTime(GameModeState.Current);
        isNewBest = bestTime < 0f || finishTime < bestTime;
        SaveSystem.SaveBestTime(GameModeState.Current, finishTime);
        if (isNewBest)
            bestTime = finishTime;

        AudioManager.Instance?.PlayVictory();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Fired by GameManager when the Zeitrennen countdown runs out before you reach the flag.
    public void TriggerTimeUp()
    {
        if (HasWon)
            return;
        HasWon = true;
        timedOut = true;
        hardcoreDeath = false;
        finishTime = GameModeState.TimeAttackLimit;
        finishDeaths = GameManager.Instance != null ? GameManager.Instance.RunDeaths : 0;
        finishHeight = PlayerHeight();
        AudioManager.Instance?.PlayDeath();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hardcore: the ONE life is spent - the run ends where you died.
    public void TriggerGameOver()
    {
        if (HasWon)
            return;
        HasWon = true;
        timedOut = true;      // reuses the "run over" layout below
        hardcoreDeath = true;
        finishTime = GameManager.Instance != null ? GameManager.Instance.PlayTime : 0f;
        finishDeaths = 1;
        finishHeight = PlayerHeight();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        UITheme.BeginUI();
        if (!HasWon)
            return;
        // The escape menu draws on top of everything - hide this screen while any menu is open,
        // so the two never fight over the same clicks (made the game-over dialog feel "stuck").
        if (MainMenu.IsBlockingGameplay)
            return;

        UITheme.EnsureInit();

        float w = 380f;
        float h = 300f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        MainMenu.DrawScreenChrome(new Rect(x, y, w, h),
            hardcoreDeath ? "Gestorben!" : (timedOut ? "Zeit abgelaufen!" : "Geschafft!"));

        int coins = GameManager.Instance != null ? GameManager.Instance.CoinCount : 0;
        if (timedOut)
        {
            GUI.Label(new Rect(x + 20, y + 75, w - 40, 25), $"Höhe erreicht: {finishHeight:0} m", UITheme.LabelStyle);
            GUI.Label(new Rect(x + 20, y + 100, w - 40, 25),
                hardcoreDeath ? "Hardcore: ein Leben - der Lauf ist vorbei." : "Die Zeit war abgelaufen - versuch's nochmal!",
                UITheme.LabelStyle);
            GUI.Label(new Rect(x + 20, y + 125, w - 40, 25), $"Münzen: {coins}", UITheme.LabelStyle);
        }
        else
        {
            int minutes = Mathf.FloorToInt(finishTime / 60f);
            int seconds = Mathf.FloorToInt(finishTime % 60f);
            GUI.Label(new Rect(x + 20, y + 75, w - 40, 25), $"Zeit: {minutes:00}:{seconds:00}", UITheme.LabelStyle);

            int bestMinutes = Mathf.FloorToInt(bestTime / 60f);
            int bestSeconds = Mathf.FloorToInt(bestTime % 60f);
            string bestLabel = isNewBest ? "Neue Bestzeit!" : $"Bestzeit: {bestMinutes:00}:{bestSeconds:00}";
            GUI.Label(new Rect(x + 20, y + 100, w - 40, 25), bestLabel, UITheme.LabelStyle);

            if (bonusStyle == null)
            {
                bonusStyle = new GUIStyle(UITheme.LabelStyle);
                bonusStyle.normal.textColor = UITheme.Gold;
            }
            GUI.Label(new Rect(x + 20, y + 125, w - 40, 25), $"Münzen: {coins}   (+{RunBonus} Abschluss-Bonus!)", bonusStyle);
        }

        if (deathStyle == null)
            deathStyle = new GUIStyle(UITheme.LabelStyle);
        deathStyle.normal.textColor = finishDeaths == 0 ? UITheme.Gold : UITheme.Ink;
        GUI.Label(new Rect(x + 20, y + 150, w - 40, 25),
            finishDeaths == 0 ? "Tode: 0 — ohne einen Tod! ⭐" : $"Tode: {finishDeaths}", deathStyle);

        if (GUI.Button(new Rect(x + 30, y + 190, w - 60, 40),
                hardcoreDeath ? "Nochmal versuchen" : "Neu starten", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            // In-place reset instead of SceneManager.LoadScene: reloading the scene while the
            // Netcode host is running (and DontDestroyOnLoad) left a duplicate NetworkManager
            // and no player, freezing the game. RestartRun resets the run without a reload.
            GameManager.Instance?.RestartRun();
        }

        if (GUI.Button(new Rect(x + 30, y + 240, w - 60, 40), "↩ Hauptmenü", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            // Clear the win state so the overlay closes, then open the menu (which frees the cursor).
            // The run stays finished at the top; "Fortsetzen" simply hides the menu again.
            ClearWon();
            MainMenu.SetScreen(MenuScreen.Main);
        }
    }
}
