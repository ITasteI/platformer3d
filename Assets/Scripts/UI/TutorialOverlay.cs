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

    // First-time controls, kept in sync with the ACTUAL feature set (glide, bow, Tab shop, photo
    // mode) so new players learn the whole kit - not the v2.x subset.
    static readonly string[] Lines =
    {
        "WASD — Bewegen     •     Maus — Umsehen",
        "Leertaste — Springen (2× möglich)",
        "Leertaste halten (in der Luft) — Gleiten",
        "Umschalt — Dash     •     Strg (rennend) — Rutschen",
        "Q — Extra-Sprung (20s Abklingzeit)",
        "Mausrad — Bogen ziehen  ·  Linksklick — Schießen",
        "Tab — Shop/Inventar     •     P — Fotomodus",
        "Escape — Menü",
    };

    void OnGUI()
    {
        UITheme.BeginUI();
        if (!IsVisible)
            return;

        UITheme.EnsureInit();

        float w = 460f;
        // Sized to the line count so added controls never overlap the start button.
        float h = 55f + Lines.Length * 26f + 78f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x, y + 12, w, 32), "Steuerung", UITheme.TitleStyle);

        string[] lines = Lines;

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
