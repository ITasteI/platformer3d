using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int CoinCount { get; private set; }
    public Transform player;
    public float topHeight = 500f;
    public int totalStages = 8;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddCoin()
    {
        CoinCount++;
    }

    void OnGUI()
    {
        if (MainMenu.IsBlockingGameplay)
            return;

        UITheme.EnsureInit();

        GUI.Label(new Rect(20, 20, 300, 30), $"Shards: {CoinCount}", UITheme.HudStyle);

        if (player != null)
        {
            float height = Mathf.Max(0f, player.position.y);
            GUI.Label(new Rect(20, 48, 300, 30), $"Höhe: {height:0} m", UITheme.HudStyle);

            int stage = Mathf.Clamp(Mathf.FloorToInt(height / topHeight * totalStages) + 1, 1, totalStages);
            GUI.Label(new Rect(20, 76, 300, 30), $"Abschnitt {stage}/{totalStages}", UITheme.HudStyle);
        }
    }
}
