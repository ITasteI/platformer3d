using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int CoinCount { get; private set; }
    public Transform player;
    public float topHeight = 500f;
    public int totalStages = 8;
    public float StartTime { get; private set; }

    static readonly string[] WorldNames = { "Schrottplatz", "Industrieanlage", "Orbit-Basis" };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        StartTime = Time.time;
    }

    public void AddCoin()
    {
        CoinCount++;
    }

    public static string GetWorldName(float t)
    {
        int index = t < 0.33f ? 0 : (t < 0.66f ? 1 : 2);
        return WorldNames[index];
    }

    void OnGUI()
    {
        if (MainMenu.IsBlockingGameplay || WinScreen.HasWon)
            return;

        UITheme.EnsureInit();

        GUI.Label(new Rect(20, 20, 300, 30), $"Shards: {CoinCount}", UITheme.HudStyle);

        if (player != null)
        {
            float height = Mathf.Max(0f, player.position.y);
            GUI.Label(new Rect(20, 48, 300, 30), $"Höhe: {height:0} m", UITheme.HudStyle);

            float t = Mathf.Clamp01(height / topHeight);
            int stage = Mathf.Clamp(Mathf.FloorToInt(t * totalStages) + 1, 1, totalStages);
            GUI.Label(new Rect(20, 76, 300, 30), $"{GetWorldName(t)} — Abschnitt {stage}/{totalStages}", UITheme.HudStyle);
        }
    }
}
