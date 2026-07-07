using UnityEngine;
using UnityEngine.SceneManagement;

public class WinScreen : MonoBehaviour
{
    public static WinScreen Instance { get; private set; }
    public static bool HasWon { get; private set; }

    private float finishTime;

    void Awake()
    {
        Instance = this;
    }

    public void TriggerWin()
    {
        if (HasWon)
            return;

        HasWon = true;
        finishTime = GameManager.Instance != null ? Time.time - GameManager.Instance.StartTime : 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        if (!HasWon)
            return;

        UITheme.EnsureInit();

        float w = 380f;
        float h = 260f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x, y + 15, w, 45), "Geschafft!", UITheme.TitleStyle);

        int minutes = Mathf.FloorToInt(finishTime / 60f);
        int seconds = Mathf.FloorToInt(finishTime % 60f);
        GUI.Label(new Rect(x + 20, y + 75, w - 40, 25), $"Zeit: {minutes:00}:{seconds:00}", UITheme.LabelStyle);

        int coins = GameManager.Instance != null ? GameManager.Instance.CoinCount : 0;
        GUI.Label(new Rect(x + 20, y + 100, w - 40, 25), $"Shards: {coins}", UITheme.LabelStyle);

        if (GUI.Button(new Rect(x + 30, y + 160, w - 60, 40), "Neu starten", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
