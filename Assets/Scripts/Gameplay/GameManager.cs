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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        PlayTime = 0f;
    }

    void Update()
    {
        if (!IsPaused())
            PlayTime += Time.deltaTime;
    }

    static bool IsPaused()
    {
        return MainMenu.IsBlockingGameplay || WinScreen.HasWon || TutorialOverlay.IsVisible;
    }

    public void AddCoin()
    {
        CoinCount++;
    }

    public void SetCoinCount(int count)
    {
        CoinCount = count;
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

        GUI.Label(new Rect(20, 20, 300, 30), $"Shards: {CoinCount}", UITheme.HudStyle);

        int min = Mathf.FloorToInt(PlayTime / 60f);
        int sec = Mathf.FloorToInt(PlayTime % 60f);
        GUI.Label(new Rect(20, 48, 300, 30), $"Zeit: {min:00}:{sec:00}", UITheme.HudStyle);

        if (player != null)
        {
            float height = Mathf.Max(0f, player.position.y);
            GUI.Label(new Rect(20, 76, 300, 30), $"Höhe: {height:0} m", UITheme.HudStyle);

            float t = Mathf.Clamp01(height / topHeight);
            int stage = Mathf.Clamp(Mathf.FloorToInt(t * totalStages) + 1, 1, totalStages);
            GUI.Label(new Rect(20, 104, 300, 30), $"{GetWorldName(t)} — Abschnitt {stage}/{totalStages}", UITheme.HudStyle);
        }

        int players = ConnectedPlayers();
        if (players > 1)
        {
            var style = new GUIStyle(UITheme.HudStyle) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(Screen.width - 220, 48, 200, 30), $"Spieler: {players}", style);
        }
    }
}
