using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int CoinCount { get; private set; }
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
    }

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
        PlayTime = 0f;
        WinScreen.ClearWon();

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
        if (MainMenu.IsBlockingGameplay || WinScreen.HasWon)
            return;

        UITheme.EnsureInit();

        // Coin chip (gold), with a pop scale when coins come in.
        Rect coinRect = new Rect(18, 16, 150, 40);
        GUI.Box(coinRect, "", UITheme.PillStyle);
        Matrix4x4 prevMatrix = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(coinPop, coinPop), new Vector2(coinRect.x + 34, coinRect.center.y));
        UITheme.Rect(new Rect(coinRect.x + 12, coinRect.y + 13, 16, 16), UITheme.Gold);
        GUI.Label(new Rect(coinRect.x + 36, coinRect.y + 8, 110, 26), EconomySystem.Coins.ToString(), UITheme.CoinStyle);
        GUI.matrix = prevMatrix;

        // Floating "+N" pickups rising from the coin chip.
        var popStyle = new GUIStyle(UITheme.CoinStyle) { alignment = TextAnchor.MiddleLeft };
        foreach (var p in popups)
        {
            float age = Time.time - p.spawn;
            float a = Mathf.Clamp01(1f - age / 1.1f);
            Color prev = GUI.color;
            GUI.color = new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, a);
            GUI.Label(new Rect(coinRect.xMax - 4, coinRect.y + 6 - age * 30f, 80, 26), "+" + p.amount, popStyle);
            GUI.color = prev;
        }

        // Time chip.
        int min = Mathf.FloorToInt(PlayTime / 60f);
        int sec = Mathf.FloorToInt(PlayTime % 60f);
        DrawChip(new Rect(18, 62, 150, 34), UITheme.Accent, $"{min:00}:{sec:00}");

        if (player != null)
        {
            float height = Mathf.Max(0f, player.position.y);
            DrawChip(new Rect(18, 102, 150, 34), UITheme.Positive, $"{height:0} m");

            // Zone progress bar with the world name above it.
            float t = Mathf.Clamp01(height / topHeight);
            GUI.Label(new Rect(20, 142, 260, 22), GetWorldName(t), UITheme.SubtitleStyle);
            UITheme.Bar(new Rect(20, 166, 240, 12), t, UITheme.Gold);
        }

        int players = ConnectedPlayers();
        if (players > 1)
        {
            Rect pr = new Rect(Screen.width - 168, 16, 150, 34);
            GUI.Box(pr, "", UITheme.PillStyle);
            UITheme.Rect(new Rect(pr.x + 12, pr.y + 11, 14, 14), UITheme.Accent);
            GUI.Label(new Rect(pr.x + 34, pr.y + 5, 110, 26), $"Spieler: {players}", UITheme.HudStyle);
        }
    }

    static void DrawChip(Rect r, Color accent, string text)
    {
        GUI.Box(r, "", UITheme.PillStyle);
        UITheme.Rect(new Rect(r.x + 12, r.y + 11, 6, 14), accent);
        GUI.Label(new Rect(r.x + 28, r.y + 5, r.width - 34, 26), text, UITheme.HudStyle);
    }
}
