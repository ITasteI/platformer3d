using UnityEngine;

public enum MenuScreen
{
    Main,
    Play,
    Settings,
    Hidden,
}

public class MainMenu : MonoBehaviour
{
    public static MenuScreen Current { get; private set; } = MenuScreen.Main;
    public static bool IsBlockingGameplay => Current != MenuScreen.Hidden;
    public static bool HasActiveGame { get; private set; }
    public static float ScreenChangedTime { get; private set; }

    const float FadeDuration = 0.25f;

    void Awake()
    {
        SetScreen(MenuScreen.Main);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (Current == MenuScreen.Hidden)
            SetScreen(MenuScreen.Main);
        else if (HasActiveGame)
            SetScreen(MenuScreen.Hidden);
    }

    public static void SetScreen(MenuScreen screen)
    {
        Current = screen;
        ScreenChangedTime = Time.time;
        bool show = screen != MenuScreen.Hidden;
        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = show;
    }

    public static void NotifyGameStarted()
    {
        HasActiveGame = true;
        SetScreen(MenuScreen.Hidden);
    }

    public static void ReturnToMainAfterDisconnect()
    {
        HasActiveGame = false;
        SetScreen(MenuScreen.Main);
    }

    public static float FadeAlpha => Mathf.Clamp01((Time.time - ScreenChangedTime) / FadeDuration);

    void OnGUI()
    {
        if (Current != MenuScreen.Main)
            return;

        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 340f;
        float h = 250f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x, y + 15, w, 45), "TasteJump", UITheme.TitleStyle);

        string playLabel = (HasActiveGame ? "▶ Weiter spielen" : "▶ Spiel Starten");
        if (GUI.Button(new Rect(x + 30, y + 75, w - 60, 38), playLabel, UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(HasActiveGame ? MenuScreen.Hidden : MenuScreen.Play);
        }

        if (GUI.Button(new Rect(x + 30, y + 122, w - 60, 38), "⚙ Einstellungen", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Settings);
        }

        if (GUI.Button(new Rect(x + 30, y + 169, w - 60, 38), "✕ Spiel Beenden", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            Application.Quit();
        }

        GUI.color = prevColor;
    }
}
