using UnityEngine;

public class TutorialOverlay : MonoBehaviour
{
    const string ShownKey = "TutorialShown";
    public static bool IsVisible { get; private set; }

    public static void ShowIfFirstTime()
    {
        if (PlayerPrefs.GetInt(ShownKey, 0) == 1)
            return;

        IsVisible = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        if (!IsVisible)
            return;

        UITheme.EnsureInit();

        float w = 440f;
        float h = 366f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x, y + 12, w, 32), "Steuerung", UITheme.TitleStyle);

        string[] lines =
        {
            "WASD — Bewegen     •     Maus — Umsehen",
            "Leertaste — Springen (2× möglich)",
            "Umschalt — Dash     •     Strg / C — Ducken",
            "Q — Extra-Sprung (20s Abklingzeit)",
            "Münzen schnell sammeln = Combo-Bonus (bis x5)",
            "Ziel: Klettere bis zur Flagge ganz oben!",
            "Escape — Menü",
        };

        float ly = y + 55f;
        foreach (var line in lines)
        {
            GUI.Label(new Rect(x + 24, ly, w - 48, 22), line, UITheme.LabelStyle);
            ly += 26f;
        }

        if (GUI.Button(new Rect(x + 30, y + h - 48, w - 60, 36), "Los geht's!", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            IsVisible = false;
            PlayerPrefs.SetInt(ShownKey, 1);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
