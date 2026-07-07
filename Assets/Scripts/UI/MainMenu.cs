using UnityEngine;
using UnityEngine.SceneManagement;

public enum MenuScreen
{
    NamePrompt,
    Main,
    Play,
    Settings,
    QuitConfirm,
    RestartConfirm,
    Hidden,
}

public class MainMenu : MonoBehaviour
{
    public static MenuScreen Current { get; private set; } = MenuScreen.Main;
    public static bool IsBlockingGameplay => Current != MenuScreen.Hidden;
    public static bool HasActiveGame { get; private set; }
    public static float ScreenChangedTime { get; private set; }

    const float FadeDuration = 0.25f;

    private string nameInput = "";
    private string nameError = "";

    void Awake()
    {
        nameInput = PlayerProfile.Name;
        SetScreen(PlayerProfile.HasName ? MenuScreen.Main : MenuScreen.NamePrompt);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (Current == MenuScreen.Hidden)
            SetScreen(MenuScreen.Main);
        else if (Current == MenuScreen.QuitConfirm || Current == MenuScreen.RestartConfirm
                 || Current == MenuScreen.Settings || Current == MenuScreen.Play)
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
        switch (Current)
        {
            case MenuScreen.NamePrompt:
                DrawNamePrompt();
                break;
            case MenuScreen.Main:
                DrawMainScreen();
                break;
            case MenuScreen.QuitConfirm:
                DrawQuitConfirm();
                break;
            case MenuScreen.RestartConfirm:
                DrawRestartConfirm();
                break;
        }
    }

    void DrawNamePrompt()
    {
        UITheme.EnsureInit();

        float w = 380f;
        float h = 200f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x, y + 15, w, 34), "Benutzernamen auswählen", UITheme.TitleStyle);

        GUI.SetNextControlName("NameField");
        nameInput = GUI.TextField(new Rect(x + 20, y + 65, w - 40, 30), nameInput, PlayerProfile.MaxNameLength);
        GUI.FocusControl("NameField");

        if (nameError.Length > 0)
        {
            var errorStyle = new GUIStyle(UITheme.LabelStyle) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
            GUI.Label(new Rect(x + 20, y + 100, w - 40, 22), nameError, errorStyle);
        }

        bool confirm = GUI.Button(new Rect(x + 30, y + h - 48, w - 60, 36), "Bestätigen", UITheme.ButtonStyle);
        bool enterPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;

        if (confirm || enterPressed)
        {
            if (PlayerProfile.TrySetName(nameInput, out string sanitized))
            {
                nameInput = sanitized;
                nameError = "";
                AudioManager.Instance?.PlayClick();
                SetScreen(MenuScreen.Main);
            }
            else
            {
                nameError = "Bitte einen gültigen Namen eingeben.";
            }
        }
    }

    void DrawMainScreen()
    {
        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 340f;
        float h = 300f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x, y + 15, w, 45), "TasteJump", UITheme.TitleStyle);

        bool hasSave = SaveSystem.HasSave();
        bool canContinue = HasActiveGame || hasSave;

        bool wasEnabled = GUI.enabled;
        GUI.enabled = canContinue;
        if (GUI.Button(new Rect(x + 30, y + 72, w - 60, 36), "▶ Fortsetzen", UITheme.ButtonStyle) && canContinue)
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(HasActiveGame ? MenuScreen.Hidden : MenuScreen.Play);
        }
        GUI.enabled = wasEnabled;

        if (GUI.Button(new Rect(x + 30, y + 114, w - 60, 36), "✚ Neues Spiel", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            if (hasSave || HasActiveGame)
                SetScreen(MenuScreen.RestartConfirm);
            else
                SetScreen(MenuScreen.Play);
        }

        if (GUI.Button(new Rect(x + 30, y + 156, w - 60, 36), "⚙ Einstellungen", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Settings);
        }

        if (GUI.Button(new Rect(x + 30, y + 198, w - 60, 36), "✕ Spiel Beenden", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.QuitConfirm);
        }

        GUI.color = prevColor;
    }

    void DrawQuitConfirm()
    {
        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 420f;
        float h = 210f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        var titleStyle = new GUIStyle(UITheme.TitleStyle) { fontSize = 22, wordWrap = true };
        var bodyStyle = new GUIStyle(UITheme.LabelStyle) { wordWrap = true, alignment = TextAnchor.MiddleCenter };

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x + 15, y + 15, w - 30, 40), "Spiel verlassen?", titleStyle);
        GUI.Label(new Rect(x + 20, y + 62, w - 40, 55), "Nicht gespeicherter Fortschritt seit dem letzten Checkpoint geht verloren.", bodyStyle);

        if (GUI.Button(new Rect(x + 20, y + h - 50, (w - 50) / 2, 36), "Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Main);
        }
        if (GUI.Button(new Rect(x + 30 + (w - 50) / 2, y + h - 50, (w - 50) / 2, 36), "Spiel verlassen", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            Application.Quit();
        }

        GUI.color = prevColor;
    }

    void DrawRestartConfirm()
    {
        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 420f;
        float h = 210f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        var titleStyle = new GUIStyle(UITheme.TitleStyle) { fontSize = 20, wordWrap = true };
        var bodyStyle = new GUIStyle(UITheme.LabelStyle) { wordWrap = true, alignment = TextAnchor.MiddleCenter };

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x + 15, y + 15, w - 30, 46), "Willst du wirklich komplett neu anfangen?", titleStyle);
        GUI.Label(new Rect(x + 20, y + 68, w - 40, 40), "Dein gespeicherter Fortschritt wird gelöscht.", bodyStyle);

        if (GUI.Button(new Rect(x + 20, y + h - 50, (w - 50) / 2, 36), "Abbrechen", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Main);
        }
        if (GUI.Button(new Rect(x + 30 + (w - 50) / 2, y + h - 50, (w - 50) / 2, 36), "Neustart", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SaveSystem.DeleteSave();
            if (HasActiveGame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            else
                SetScreen(MenuScreen.Play);
        }

        GUI.color = prevColor;
    }
}
