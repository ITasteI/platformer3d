using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int CoinCount { get; private set; }
    public Transform player;

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

    public void ResetCoins()
    {
        CoinCount = 0;
    }

    void OnGUI()
    {
        var style = new GUIStyle { fontSize = 24, normal = new GUIStyleState { textColor = Color.white } };
        GUI.Label(new Rect(20, 20, 250, 30), $"Shards: {CoinCount}", style);

        if (player != null)
        {
            float height = Mathf.Max(0f, player.position.y);
            GUI.Label(new Rect(20, 50, 250, 30), $"Höhe: {height:0} m", style);
        }
    }
}
