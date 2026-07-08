using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int CoinCount { get; private set; }
    public int RunDeaths { get; private set; }
    public int RunKills { get; private set; }
    public int RunSecrets { get; private set; }

    public void RegisterKill()
    {
        RunKills++;
        WeeklyChallenge.NoteKill();
    }
    public void RegisterSecret() => RunSecrets++;
    const string TotalDeathsKey = "TotalDeaths";
    public static int TotalDeaths => PlayerPrefs.GetInt(TotalDeathsKey, 0);
    public Transform player;
    public float topHeight = 500f;
    public int totalStages = 8;

    // Accumulated active play time. Unlike (Time.time - StartTime) this pauses whenever the
    // menu, tutorial or win screen is up, so the run timer / best time stays fair.
    public float PlayTime { get; private set; }

    static readonly string[] WorldNames = { "Wiesenland", "Vulkanfeld", "Wolkenreich", "Eiskristall", "Sternenkrone" };

    // HUD coin animation: tracks the persistent wallet so pickups pop the counter and spawn a
    // floating "+N".
    private int lastWalletCoins;
    private float coinPop = 1f;
    private struct CoinPopup { public int amount; public float spawn; }
    private readonly List<CoinPopup> popups = new List<CoinPopup>();

    // Drives the batched wallet save so farmed coins don't hit the disk on every pickup.
    private const float EconomyFlushInterval = 5f;
    private float economyFlushTimer = EconomyFlushInterval;

    // Coin combo: collecting coins in quick succession stacks a multiplier, rewarding a fast,
    // continuous climb. The chain resets if you go too long without a pickup.
    private const float ComboWindow = 2.5f;
    private const int MaxComboMultiplier = 3;
    public int Combo { get; private set; }
    private float lastCoinTime = -99f;

    public int ComboMultiplier => Mathf.Clamp(1 + (Combo - 1) / 2, 1, MaxComboMultiplier);

    // Smoothed frame rate for the HUD FPS counter (unscaled, so it reads true even while paused).
    private float fpsSmooth = 60f;

    // Cached GUI styles - allocating "new GUIStyle" every OnGUI frame produced steady GC garbage.
    private static GUIStyle fpsLabelStyle;
    private static GUIStyle popStyle;
    private static GUIStyle comboStyle;

    // Called by a coin on pickup: advances the chain and returns the multiplier to apply.
    public int RegisterCoinPickup()
    {
        Combo = (Time.time - lastCoinTime <= ComboWindow) ? Combo + 1 : 1;
        lastCoinTime = Time.time;
        return ComboMultiplier;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        PlayTime = 0f;
        lastWalletCoins = EconomySystem.Coins;
    }

    void Update()
    {
        // Arm the co-op enemy-death sync as soon as a network session is live (cheap no-op after).
        EnemySync.EnsureRegistered();

        float dt = Time.unscaledDeltaTime;
        if (dt > 0.0001f)
            fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / dt, 0.1f);

        if (!IsPaused())
            PlayTime += Time.deltaTime;

        int wallet = EconomySystem.Coins;
        if (wallet > lastWalletCoins)
        {
            popups.Add(new CoinPopup { amount = wallet - lastWalletCoins, spawn = Time.time });
            coinPop = 1.5f;
        }
        lastWalletCoins = wallet;
        coinPop = Mathf.Lerp(coinPop, 1f, Time.deltaTime * 7f);
        popups.RemoveAll(p => Time.time - p.spawn > 1.1f);

        economyFlushTimer -= Time.deltaTime;
        if (economyFlushTimer <= 0f)
        {
            economyFlushTimer = EconomyFlushInterval;
            EconomySystem.Flush();
        }

        // Expire the combo chain once the window lapses so the HUD indicator clears.
        if (Combo > 0 && Time.time - lastCoinTime > ComboWindow)
            Combo = 0;

        // Mode-specific run logic.
        if (!IsPaused())
        {
            if (GameModeState.Current == GameMode.Endlos && player != null)
                GameModeState.ReportHeight(player.position.y);
            else if (GameModeState.Current == GameMode.Zeitrennen && !WinScreen.HasWon && PlayTime >= GameModeState.TimeAttackLimit)
                WinScreen.Instance?.TriggerTimeUp();

            // Daily/weekly challenge progress (throttled - a per-frame check would be wasted work).
            dailyCheckTimer -= Time.deltaTime;
            if (dailyCheckTimer <= 0f)
            {
                dailyCheckTimer = 1f;
                float h = player != null ? player.position.y : 0f;
                DailyChallenge.Check(h, CoinCount, RunKills, RunSecrets);
                WeeklyChallenge.Check(h);
            }

            // Zone splits: entering a new world shows the elapsed time - lightweight run splits.
            if (player != null && !WinScreen.HasWon)
            {
                float t = Mathf.Clamp01(Mathf.Max(0f, player.position.y) / topHeight);
                int zone = t < 0.2f ? 0 : (t < 0.4f ? 1 : (t < 0.6f ? 2 : (t < 0.8f ? 3 : 4)));
                if (zone > lastZoneIndex)
                {
                    if (lastZoneIndex >= 0 && zone >= 1 && PlayTime > 3f)
                    {
                        int zm = Mathf.FloorToInt(PlayTime / 60f), zs = Mathf.FloorToInt(PlayTime % 60f);
                        MilestoneTracker.Instance?.ShowToast($"{WorldNames[zone]} erreicht — {zm:00}:{zs:00}", UITheme.Accent, "⚑");
                    }
                    lastZoneIndex = zone;
                }
            }
        }
    }

    private int lastZoneIndex = -1;
    private float dailyCheckTimer = 1f;

    void OnApplicationQuit() => EconomySystem.Flush();

    void OnApplicationPause(bool paused)
    {
        if (paused)
            EconomySystem.Flush();
    }

    static bool IsPaused()
    {
        return MainMenu.IsBlockingGameplay || WinScreen.HasWon || TutorialOverlay.IsVisible;
    }

    public void AddCoins(int amount)
    {
        CoinCount += amount;
    }

    // Counts a death for the run + all-time total (persisted) and shows a brief toast.
    public void RegisterDeath()
    {
        RunDeaths++;
        PlayerPrefs.SetInt(TotalDeathsKey, PlayerPrefs.GetInt(TotalDeathsKey, 0) + 1);
        PlayerPrefs.Save();
        MilestoneTracker.Instance?.ShowToast($"Tod #{RunDeaths} · zurück zum Checkpoint", new Color(0.95f, 0.5f, 0.5f), "☠");
    }

    public void SetCoinCount(int count)
    {
        CoinCount = count;
    }

    // Restart the current run from the bottom without reloading the scene (a reload breaks the
    // running Netcode host). Clears the save, resets counters, and teleports the local player
    // back to spawn. Note: already-collected shards stay collected for this session.
    public void RestartRun()
    {
        SaveSystem.DeleteSave();
        CoinCount = 0;
        RunDeaths = 0;
        RunKills = 0;
        RunSecrets = 0;
        PlayTime = 0f;
        WinScreen.ClearWon();
        GoalTrigger.ResetEndlessReward();
        lastZoneIndex = -1;

        if (player != null)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
                pc.RestartFromBeginning();
        }

        MainMenu.SetScreen(MenuScreen.Hidden);
    }

    public static string GetWorldName(float t)
    {
        int index = t < 0.2f ? 0 : (t < 0.4f ? 1 : (t < 0.6f ? 2 : (t < 0.8f ? 3 : 4)));
        return WorldNames[index];
    }

    static int ConnectedPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
            return nm.ConnectedClientsList.Count;
        return 1;
    }

    void OnGUI()
    {
        UITheme.BeginUI();
        if (MainMenu.IsBlockingGameplay || WinScreen.HasWon || PhotoMode.Active)
            return;

        UITheme.EnsureInit();

        // Coin chip (gold), with a pop scale when coins come in.
        Rect coinRect = new Rect(18, 16, 150, 40);
        GUI.Box(coinRect, "", UITheme.PillStyle);
        Matrix4x4 prevMatrix = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(coinPop, coinPop), new Vector2(coinRect.x + 34, coinRect.center.y));
        UITheme.RoundRect(new Rect(coinRect.x + 12, coinRect.y + 12, 16, 16), UITheme.Gold);
        GUI.Label(new Rect(coinRect.x + 36, coinRect.y + 8, 110, 26), EconomySystem.Coins.ToString(), UITheme.CoinStyle);
        GUI.matrix = prevMatrix;

        // Floating "+N" pickups rising from the coin chip.
        if (popStyle == null)
            popStyle = new GUIStyle(UITheme.CoinStyle) { alignment = TextAnchor.MiddleLeft };
        foreach (var p in popups)
        {
            float age = Time.time - p.spawn;
            float a = Mathf.Clamp01(1f - age / 1.1f);
            Color prev = GUI.color;
            GUI.color = new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, a);
            GUI.Label(new Rect(coinRect.xMax - 4, coinRect.y + 6 - age * 30f, 80, 26), "+" + p.amount, popStyle);
            GUI.color = prev;
        }

        // Combo multiplier badge (only while an actual multiplier is active), to the right of coins.
        if (ComboMultiplier > 1)
        {
            if (comboStyle == null)
            {
                comboStyle = new GUIStyle(UITheme.CoinStyle) { fontStyle = FontStyle.Bold };
                comboStyle.normal.textColor = UITheme.Positive;
            }
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.06f;
            Matrix4x4 cm = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pulse, pulse), new Vector2(coinRect.xMax + 40, coinRect.center.y));
            GUI.Label(new Rect(coinRect.xMax + 12, coinRect.y + 8, 90, 26), $"x{ComboMultiplier} Combo", comboStyle);
            GUI.matrix = cm;
        }

        // Time chip - a countdown in Zeitrennen, elapsed time otherwise.
        if (GameModeState.Current == GameMode.Zeitrennen)
        {
            float remain = Mathf.Max(0f, GameModeState.TimeAttackLimit - PlayTime);
            int rmin = Mathf.FloorToInt(remain / 60f), rsec = Mathf.FloorToInt(remain % 60f);
            DrawChip(new Rect(18, 62, 150, 34), remain < 30f ? UITheme.Danger : UITheme.Accent, $"⏱ {rmin:00}:{rsec:00}");
        }
        else
        {
            int min = Mathf.FloorToInt(PlayTime / 60f);
            int sec = Mathf.FloorToInt(PlayTime % 60f);
            DrawChip(new Rect(18, 62, 150, 34), UITheme.Accent, $"{min:00}:{sec:00}");
        }

        if (player != null)
        {
            float height = Mathf.Max(0f, player.position.y);
            DrawChip(new Rect(18, 102, 150, 34), UITheme.Positive, $"{height:0} m");

            // Zone progress ribbon: world name, fill bar AND tick marks at the zone boundaries,
            // so the bar reads as "5 worlds" instead of an anonymous percentage.
            float t = Mathf.Clamp01(height / topHeight);
            GUI.Label(new Rect(20, 142, 300, 22), GetWorldName(t) + "  ·  " + GameModeState.Name(GameModeState.Current), UITheme.SubtitleStyle);
            UITheme.Bar(new Rect(20, 166, 240, 12), t, UITheme.Gold);
            for (int z = 1; z < 5; z++)
                UITheme.Rect(new Rect(20 + 240 * (z * 0.2f) - 1f, 165, 2f, 14f), new Color(1f, 1f, 1f, 0.35f));

            // Death counter for this run (only once you've died, to avoid early clutter).
            if (RunDeaths > 0)
                DrawChip(new Rect(18, 190, 150, 34), new Color(0.95f, 0.5f, 0.5f), $"☠ {RunDeaths}");

            // Endless: show the height record to beat.
            if (GameModeState.Current == GameMode.Endlos)
                DrawChip(new Rect(18, RunDeaths > 0 ? 228 : 190, 180, 34), UITheme.Gold, $"Rekord: {GameModeState.BestHeight:0} m");
        }

        // (The mode now lives in the zone line top-left - the bottom-left label was clutter.)

        // All enemy health bars in ONE pass (a per-enemy OnGUI cost real CPU with ~115 wraiths).
        Camera cam = Camera.main;
        if (cam != null)
            foreach (var e in EnemySync.AllEnemies)
                if (e != null && e.isActiveAndEnabled)
                    e.DrawHealthBar(cam);

        // Top-right chips stack vertically so they never overlap: [Spieler] → [FPS].
        float trX = UITheme.ScreenW - 168f;
        float trY = 16f;

        int players = ConnectedPlayers();
        if (players > 1)
        {
            Rect pr = new Rect(trX, trY, 150, 34);
            GUI.Box(pr, "", UITheme.PillStyle);
            UITheme.Rect(new Rect(pr.x + 12, pr.y + 11, 14, 14), UITheme.Accent);
            GUI.Label(new Rect(pr.x + 34, pr.y + 5, 110, 26), $"Spieler: {players}", UITheme.HudStyle);
            trY += 40f;
        }

        // Small FPS counter, colour-coded green/gold/red; sized to fit up to 4 digits.
        if (fpsLabelStyle == null)
            fpsLabelStyle = new GUIStyle(UITheme.HudStyle) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        int fps = Mathf.Clamp(Mathf.RoundToInt(fpsSmooth), 0, 9999);
        const float fpsW = 118f;
        Rect fpsRect = new Rect(UITheme.ScreenW - fpsW - 18f, trY, fpsW, 34);
        GUI.Box(fpsRect, "", UITheme.PillStyle);
        Color fpsColor = fps >= 50 ? UITheme.Positive : (fps >= 30 ? UITheme.Gold : UITheme.Danger);
        UITheme.RoundRect(new Rect(fpsRect.x + 12, fpsRect.y + 11, 6, 14), fpsColor);
        GUI.Label(new Rect(fpsRect.x + 26, fpsRect.y + 6, fpsW - 34f, 24), $"{fps} FPS", fpsLabelStyle);
    }

    static void DrawChip(Rect r, Color accent, string text)
    {
        GUI.Box(r, "", UITheme.PillStyle);
        UITheme.RoundRect(new Rect(r.x + 12, r.y + 10, 6, 16), accent);
        GUI.Label(new Rect(r.x + 28, r.y + 5, r.width - 34, 26), text, UITheme.HudStyle);
    }
}
