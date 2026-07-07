using System.Collections.Generic;
using UnityEngine;

// Small goals to chase: fires a one-time celebratory toast + chime when the player first crosses a
// height or total-coins-earned milestone. Each fires once ever (persisted in PlayerPrefs), so they
// feel like real achievements rather than repeating spam. Purely presentational.
public class MilestoneTracker : MonoBehaviour
{
    public static MilestoneTracker Instance { get; private set; }

    static readonly (float height, string text)[] HeightMilestones =
    {
        (50f,  "50 Meter erreicht!"),
        (100f, "100 Meter - stark!"),
        (200f, "200 Meter hoch hinaus!"),
        (350f, "350 Meter - schwindelerregend!"),
        (500f, "500 Meter - fast oben!"),
    };

    static readonly (int coins, string text)[] CoinMilestones =
    {
        (100,  "100 Münzen gesammelt!"),
        (500,  "500 Münzen - Sammler!"),
        (1000, "1000 Münzen - reich!"),
        (2500, "2500 Münzen - Legende!"),
    };

    class Toast { public string text; public float spawn; }
    readonly List<Toast> toasts = new List<Toast>();
    const float ToastLife = 3.5f;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
        {
            float h = GameManager.Instance.player.position.y;
            foreach (var m in HeightMilestones)
                if (h >= m.height)
                    Fire("ms_h_" + (int)m.height, m.text);
        }

        int earned = EconomySystem.TotalCoinsEarned;
        foreach (var m in CoinMilestones)
            if (earned >= m.coins)
                Fire("ms_c_" + m.coins, m.text);

        toasts.RemoveAll(t => Time.time - t.spawn > ToastLife);
    }

    void Fire(string key, string text)
    {
        if (PlayerPrefs.GetInt(key, 0) == 1)
            return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        toasts.Add(new Toast { text = text, spawn = Time.time });
        AudioManager.Instance?.PlayAbilityReady(); // soft positive chime
    }

    void OnGUI()
    {
        if (toasts.Count == 0 || MainMenu.IsBlockingGameplay || WinScreen.HasWon)
            return;

        UITheme.EnsureInit();

        const float w = 340f;
        float y = 88f;
        foreach (var t in toasts)
        {
            float age = Time.time - t.spawn;
            float a = Mathf.Min(Mathf.Clamp01(age / 0.3f), Mathf.Clamp01((ToastLife - age) / 0.6f));
            float x = (Screen.width - w) / 2f;
            float rise = Mathf.Clamp01(age * 2f) * 8f;

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Box(new Rect(x, y - rise, w, 46), "", UITheme.PanelStyle);
            UITheme.Rect(new Rect(x + 14, y - rise + 15, 16, 16), UITheme.Gold);
            var style = new GUIStyle(UITheme.SubtitleStyle) { alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = UITheme.Gold;
            GUI.Label(new Rect(x + 40, y - rise + 4, w - 50, 38), "★ " + t.text, style);
            GUI.color = prev;
            y += 54f;
        }
    }
}
