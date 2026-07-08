using UnityEngine;

public enum MenuScreen
{
    NamePrompt,
    Main,
    Play,
    Settings,
    Shop,
    Controls,
    GameHelp,
    Achievements,
    Stats,
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
    private bool nameFieldFocused;

    // Hidden G.I.G easter-egg popup, triggered by a tiny button on the right of the main menu.
    private bool showEasterEgg;
    private float easterEggTime;
    private Vector2 achScroll;

    // Cached animated-title style (white text; per-letter colour is driven via GUI.color).
    private static GUIStyle titleAnimStyle;

    void Awake()
    {
        nameInput = PlayerProfile.Name;
        SetScreen(PlayerProfile.HasName ? MenuScreen.Main : MenuScreen.NamePrompt);
    }

    // Where the shop returns to when closed: Tab from gameplay goes back to gameplay, Tab from
    // the main menu goes back to the main menu (so the shop is reachable before the first run).
    static MenuScreen shopReturn = MenuScreen.Main;

    public static void OpenShop()
    {
        shopReturn = Current;
        SetScreen(MenuScreen.Shop);
    }

    public static void CloseShop()
    {
        // Never "return" into gameplay when no run is active.
        if (shopReturn == MenuScreen.Hidden && !HasActiveGame)
            shopReturn = MenuScreen.Main;
        SetScreen(shopReturn);
    }

    void Update()
    {
        // Tab opens/closes the shop & inventory - over gameplay AND from the main menu.
        if (Input.GetKeyDown(KeyCode.Tab) && !showEasterEgg)
        {
            if (Current == MenuScreen.Shop)
                CloseShop();
            else if ((Current == MenuScreen.Hidden && HasActiveGame) || Current == MenuScreen.Main)
                OpenShop();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        // Escape first dismisses the easter egg if it's open.
        if (showEasterEgg)
        {
            showEasterEgg = false;
            return;
        }

        if (Current == MenuScreen.Hidden)
            SetScreen(MenuScreen.Main);
        else if (Current == MenuScreen.QuitConfirm || Current == MenuScreen.RestartConfirm
                 || Current == MenuScreen.Settings || Current == MenuScreen.Play
                 || Current == MenuScreen.Shop || Current == MenuScreen.Controls
                 || Current == MenuScreen.GameHelp || Current == MenuScreen.Achievements
                 || Current == MenuScreen.Stats)
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
        UITheme.BeginUI();
        // While the easter egg is up, draw the menu behind it non-interactive so clicks hit the popup.
        bool prevEnabled = GUI.enabled;
        if (showEasterEgg)
            GUI.enabled = false;

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
            case MenuScreen.Controls:
                DrawControls();
                break;
            case MenuScreen.GameHelp:
                DrawGameHelp();
                break;
            case MenuScreen.Stats:
                DrawStats();
                break;
            case MenuScreen.Achievements:
                DrawAchievements();
                break;
        }

        GUI.enabled = prevEnabled;

        if (showEasterEgg)
            DrawEasterEgg();
    }

    // ---------------------------------------------------------------------------------------------
    // Animated title: each glyph rides a gentle sine wave and shimmers gold, with a soft drop shadow.
    // Reused for the "TasteJump" header and the "G.I.G" easter egg.
    static void DrawAnimatedTitle(Rect area, string text)
    {
        UITheme.EnsureInit();
        if (titleAnimStyle == null)
            titleAnimStyle = new GUIStyle(UITheme.TitleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow, // per-letter rects must never clip the bobbing glyphs
                wordWrap = false,
            };

        var widths = new float[text.Length];
        float total = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            widths[i] = titleAnimStyle.CalcSize(new GUIContent(text[i].ToString())).x;
            total += widths[i];
        }

        Color prev = GUI.color;
        float a = prev.a;
        float t = Time.time;
        float px = area.x + (area.width - total) * 0.5f;

        for (int i = 0; i < text.Length; i++)
        {
            string ch = text[i].ToString();
            float lw = widths[i];
            float bob = Mathf.Sin(t * 3f + i * 0.5f) * 3f;
            Rect lr = new Rect(px, area.y + bob, lw, area.height);

            GUI.color = new Color(0f, 0f, 0f, 0.35f * a);
            titleAnimStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(lr.x + 2f, lr.y + 3f, lr.width, lr.height), ch, titleAnimStyle);

            float sh = 0.5f + 0.5f * Mathf.Sin(t * 2.2f + i * 0.6f);
            GUI.color = new Color(1f, 1f, 1f, a);
            titleAnimStyle.normal.textColor = Color.Lerp(UITheme.Gold, new Color(1f, 0.97f, 0.76f), sh);
            GUI.Label(lr, ch, titleAnimStyle);

            px += lw;
        }

        GUI.color = prev;
    }

    // Cached label style for the coloured buttons (no background - the button bg is drawn separately).
    private static GUIStyle btnLabelStyle;
    private static GUIStyle dailyStyle;

    static string FormatTime(float t)
    {
        if (t < 0f)
            return "—";
        return $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
    }

    static float Frac(float v) => v - Mathf.Floor(v);

    // Full-screen animated backdrop: a soft dim plus gold/blue/violet glowing orbs drifting slowly
    // upward and twinkling, so the whole menu feels alive and magical.
    static void DrawMenuBackdrop()
    {
        float fade = FadeAlpha;
        UITheme.Rect(new Rect(0, 0, UITheme.ScreenW, UITheme.ScreenH), new Color(0.03f, 0.04f, 0.09f, 0.62f * fade));

        float t = Time.time;
        const int N = 16;
        float span = UITheme.ScreenH + 160f;
        for (int i = 0; i < N; i++)
        {
            float seed = i * 127.13f;
            float rx = Frac(Mathf.Sin(seed) * 43758.5453f);
            float rspeed = Frac(Mathf.Sin(seed + 1.3f) * 24634.63f);
            float rsize = Frac(Mathf.Sin(seed + 2.7f) * 13214.12f);
            float rphase = Frac(Mathf.Sin(seed + 4.1f) * 98741.23f);

            float speed = 12f + rspeed * 26f;
            float yy = UITheme.ScreenH + 80f - ((t * speed + rphase * span) % span);
            float xx = rx * UITheme.ScreenW + Mathf.Sin(t * 0.5f + i) * 22f;
            float size = 26f + rsize * 52f;
            Color c = (i % 3 == 0) ? UITheme.Gold : (i % 3 == 1) ? UITheme.Accent : new Color(0.62f, 0.42f, 1f);
            float aa = (0.08f + 0.10f * (0.5f + 0.5f * Mathf.Sin(t * 1.3f + i))) * fade;
            UITheme.DrawGlow(new Rect(xx - size * 0.5f, yy - size * 0.5f, size, size), new Color(c.r, c.g, c.b, aa));
        }
    }

    // Shared premium chrome for EVERY menu screen: animated night backdrop, pulsing blue-violet
    // halo, panel, glowing gold title and a soft divider - so sub-screens (Steuerung, Einstellungen,
    // Shop, Dialoge, Lobby) look exactly as polished as the main screen. Pass null to skip the title
    // (screens with their own header, e.g. the shop).
    static GUIStyle chromeTitleStyle;

    public static void DrawScreenChrome(Rect panel, string title)
    {
        UITheme.EnsureInit();
        DrawMenuBackdrop();

        float t = Time.time;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.4f);
        Color halo = Color.Lerp(UITheme.Accent, new Color(0.6f, 0.4f, 1f), pulse);
        UITheme.DrawGlow(new Rect(panel.x - 42f, panel.y - 42f, panel.width + 84f, panel.height + 84f),
            new Color(halo.r, halo.g, halo.b, 0.18f * FadeAlpha));

        // Soft drop shadow under the panel - windows read as floating layers, not flat rectangles.
        UITheme.DrawGlow(new Rect(panel.x - 24f, panel.y - 10f, panel.width + 48f, panel.height + 52f),
            new Color(0f, 0f, 0f, 0.5f * FadeAlpha));

        GUI.Box(panel, "", UITheme.PanelStyle);

        if (string.IsNullOrEmpty(title))
            return;

        UITheme.DrawGlow(new Rect(panel.x + panel.width * 0.5f - 110f, panel.y + 2f, 220f, 56f),
            new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, (0.16f + 0.06f * Mathf.Sin(t * 2f)) * FadeAlpha));
        if (chromeTitleStyle == null)
            chromeTitleStyle = new GUIStyle(UITheme.TitleStyle) { fontSize = 26 };
        GUI.Label(new Rect(panel.x, panel.y + 12f, panel.width, 32f), title, chromeTitleStyle);
        UITheme.Rect(new Rect(panel.x + 30f, panel.y + 50f, panel.width - 60f, 1f), new Color(1f, 1f, 1f, 0.09f));
    }

    // A vividly coloured menu button: fades + slides in staggered, glows in its own colour on hover,
    // brightens + scales on hover, dims when disabled. Text has a shadow for readability.
    static bool ColorButton(Rect r, string label, int index, Color col)
    {
        if (btnLabelStyle == null)
            btnLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
            };

        float st = Time.time - ScreenChangedTime;
        float appear = Mathf.Clamp01((st - index * 0.05f) / 0.22f);
        appear = 1f - (1f - appear) * (1f - appear); // ease-out

        Color prev = GUI.color;
        float a = prev.a * appear;
        Rect ar = new Rect(r.x + (1f - appear) * 26f, r.y, r.width, r.height);
        bool en = GUI.enabled;
        bool hover = en && ar.Contains(Event.current.mousePosition);

        if (hover)
            UITheme.DrawGlow(new Rect(ar.x - 14f, ar.y - 14f, ar.width + 28f, ar.height + 28f), new Color(col.r, col.g, col.b, 0.5f * a));

        Matrix4x4 m = GUI.matrix;
        if (hover)
            GUIUtility.ScaleAroundPivot(new Vector2(1.03f, 1.06f), ar.center);

        Color bg = hover ? Color.Lerp(col, Color.white, 0.16f) : col;
        if (!en)
            bg = Color.Lerp(bg, new Color(0.34f, 0.36f, 0.42f), 0.65f);
        GUI.color = new Color(bg.r, bg.g, bg.b, a);
        GUI.Box(ar, GUIContent.none, UITheme.RoundedFillStyle);

        GUI.color = new Color(0f, 0f, 0f, 0.4f * a);
        GUI.Label(new Rect(ar.x, ar.y + 2f, ar.width, ar.height), label, btnLabelStyle);
        GUI.color = new Color(1f, 1f, 1f, (en ? 1f : 0.7f) * a);
        GUI.Label(ar, label, btnLabelStyle);

        bool clicked = GUI.Button(ar, GUIContent.none, GUIStyle.none);
        GUI.matrix = m;
        GUI.color = prev;
        return clicked;
    }

    void DrawMainScreen()
    {
        UITheme.EnsureInit();
        DrawMenuBackdrop();

        Color prevColor = GUI.color;
        float fade = FadeAlpha;
        GUI.color = new Color(1f, 1f, 1f, fade);

        float w = 360f;
        float h = 566f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;
        float t = Time.time;

        // Pulsing halo behind the panel, colour drifting between blue and violet.
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.4f);
        Color halo = Color.Lerp(UITheme.Accent, new Color(0.6f, 0.4f, 1f), pulse);
        UITheme.DrawGlow(new Rect(x - 48f, y - 48f, w + 96f, h + 96f), new Color(halo.r, halo.g, halo.b, 0.22f * fade));

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);

        // Title with a soft golden bloom behind it.
        float tg = 0.32f + 0.14f * Mathf.Sin(t * 2f);
        UITheme.DrawGlow(new Rect(x + w * 0.5f - 140f, y + 16f, 280f, 78f), new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, tg * fade));
        DrawAnimatedTitle(new Rect(x, y + 30f, w, 50f), "TasteJump");

        var tag = new GUIStyle(UITheme.SubtitleStyle) { alignment = TextAnchor.MiddleCenter };
        tag.normal.textColor = UITheme.InkDim;
        GUI.Label(new Rect(x, y + 82f, w, 20f), "✦ Nur nach oben! ✦", tag);

        bool hasSave = SaveSystem.HasSave();
        bool canContinue = HasActiveGame || hasSave;

        float bx0 = x + 34f, bw = w - 68f, bh = 40f;
        float by = y + 110f, step = 46f;

        bool wasEnabled = GUI.enabled;
        GUI.enabled = canContinue;
        if (ColorButton(new Rect(bx0, by, bw, bh), "▶ Fortsetzen", 0, new Color(0.22f, 0.62f, 0.36f)) && canContinue)
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(HasActiveGame ? MenuScreen.Hidden : MenuScreen.Play);
        }
        GUI.enabled = wasEnabled;

        if (ColorButton(new Rect(bx0, by + step, bw, bh), "✚ Neues Spiel", 1, new Color(0.22f, 0.52f, 0.95f)))
        {
            AudioManager.Instance?.PlayClick();
            if (hasSave || HasActiveGame)
            {
                pendingMode = GameModeState.Current; // dialog starts from the actual current mode
                SetScreen(MenuScreen.RestartConfirm);
            }
            else
            {
                SetScreen(MenuScreen.Play);
            }
        }

        if (ColorButton(new Rect(bx0, by + step * 2f, bw, bh), "🏆 Errungenschaften", 2, new Color(0.85f, 0.66f, 0.2f)))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Achievements);
        }

        if (ColorButton(new Rect(bx0, by + step * 3f, bw, bh), "📊 Statistiken", 3, new Color(0.24f, 0.5f, 0.9f)))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Stats);
        }

        if (ColorButton(new Rect(bx0, by + step * 4f, bw, bh), "📖 Spielhilfe", 4, new Color(0.2f, 0.55f, 0.45f)))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.GameHelp);
        }

        if (ColorButton(new Rect(bx0, by + step * 5f, bw, bh), "🎮 Steuerung", 5, new Color(0.16f, 0.58f, 0.62f)))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Controls);
        }

        if (ColorButton(new Rect(bx0, by + step * 6f, bw, bh), "⚙ Einstellungen", 6, new Color(0.48f, 0.36f, 0.78f)))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Settings);
        }

        if (ColorButton(new Rect(bx0, by + step * 7f, bw, bh), "✕ Spiel Beenden", 7, new Color(0.82f, 0.30f, 0.34f)))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.QuitConfirm);
        }

        // Daily/weekly challenge teasers - records/statistics live in their own screen now (📊).
        if (dailyStyle == null)
        {
            dailyStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            dailyStyle.normal.textColor = UITheme.Gold;
        }
        float ry = by + step * 7f + bh + 10f;
        UITheme.Rect(new Rect(x + 40f, ry, w - 80f, 1f), new Color(1f, 1f, 1f, 0.09f * fade));
        var daily = DailyChallenge.Today;
        GUI.Label(new Rect(x, ry + 6f, w, 18f),
            DailyChallenge.DoneToday ? "☀ Tages-Challenge erledigt ✓" : $"☀ Heute: {daily.Text}  (+{daily.Reward})",
            dailyStyle);
        var weekly = WeeklyChallenge.Current;
        GUI.Label(new Rect(x, ry + 24f, w, 18f),
            WeeklyChallenge.Done
                ? "★ Wochen-Challenge erledigt ✓"
                : $"★ Woche: {weekly.Text}  ({WeeklyChallenge.Progress(0f)}/{weekly.Target} · +{weekly.Reward})",
            dailyStyle);

        // Easter-egg teaser to the RIGHT of the panel: a glowing "Klick mich!" label + a bouncing
        // arrow pointing at a clearly visible star button. Clicking it reveals the G.I.G popup.
        Rect eggBtn = new Rect(x + w + 64f, y + h * 0.5f - 24f, 48f, 48f);
        UITheme.DrawGlow(new Rect(eggBtn.x - 16f, eggBtn.y - 16f, eggBtn.width + 32f, eggBtn.height + 32f),
            new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, (0.3f + 0.2f * Mathf.Sin(t * 3f)) * fade));

        var eggLabel = new GUIStyle(UITheme.SubtitleStyle) { alignment = TextAnchor.MiddleCenter };
        eggLabel.normal.textColor = UITheme.Gold;
        GUI.Label(new Rect(eggBtn.center.x - 90f, eggBtn.y - 26f, 180f, 20f), "Klick mich!", eggLabel);

        var arrowStyle = new GUIStyle(UITheme.TitleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 30, clipping = TextClipping.Overflow };
        arrowStyle.normal.textColor = UITheme.Gold;
        float abx = Mathf.Abs(Mathf.Sin(t * 4f)) * 9f;
        GUI.Label(new Rect(eggBtn.x - 44f + abx, eggBtn.y + 4f, 40f, 40f), "→", arrowStyle);

        var eggBtnStyle = new GUIStyle(UITheme.ButtonStyle) { fontSize = 24 };
        if (GUI.Button(eggBtn, "★", eggBtnStyle))
        {
            showEasterEgg = true;
            easterEggTime = Time.time;
            AudioManager.Instance?.PlayClick();
        }

        GUI.color = prevColor;
    }

    // The G.I.G reveal: dim backdrop + a pop-in panel with the animated initials.
    void DrawEasterEgg()
    {
        UITheme.Rect(new Rect(0, 0, UITheme.ScreenW, UITheme.ScreenH), new Color(0.02f, 0.03f, 0.06f, 0.72f));

        float pop = Mathf.Clamp01((Time.time - easterEggTime) / 0.32f);
        float sc = 1f - (1f - pop) * (1f - pop) * (1f - pop); // ease-out cubic

        float w = 340f, h = 224f;
        float cx = UITheme.ScreenW / 2f, cy = UITheme.ScreenH / 2f;
        Rect panel = new Rect(cx - w / 2f, cy - h / 2f, w, h);

        Matrix4x4 m = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), new Vector2(cx, cy));

        float gp = 0.4f + 0.2f * Mathf.Sin(Time.time * 2.5f);
        UITheme.DrawGlow(new Rect(panel.x - 50f, panel.y - 50f, panel.width + 100f, panel.height + 100f),
            new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, gp * 0.5f));
        GUI.Box(panel, "", UITheme.PanelStyle);
        DrawAnimatedTitle(new Rect(panel.x, panel.y + 42f, panel.width, 62f), "G.I.G");
        var sub = new GUIStyle(UITheme.SubtitleStyle) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(panel.x, panel.y + 116f, panel.width, 24f), "✦ Easter Egg gefunden ✦", sub);
        if (GUI.Button(new Rect(cx - 70f, panel.yMax - 54f, 140f, 36f), "Cool!", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            showEasterEgg = false;
        }

        GUI.matrix = m;

        // Click-anywhere-else backstop (drawn last so the "Cool!" button gets its click first).
        if (GUI.Button(new Rect(0, 0, UITheme.ScreenW, UITheme.ScreenH), GUIContent.none, GUIStyle.none))
            showEasterEgg = false;
    }

    static readonly string[] ControlLines =
    {
        "WASD — Bewegen",
        "Maus — Umsehen",
        "Leertaste — Springen (2× möglich)",
        "Leertaste halten (in der Luft) — Gleiten",
        "E am Seil-Mast — Zipline fahren (Leertaste = abspringen)",
        "Umschalt — Dash",
        "Mausrad — Bogen ziehen / verstauen",
        "Linksklick — Pfeil schießen (bei gezogenem Bogen)",
        "Strg / C — Ducken · rennend: Rutschen (Slide-Jump)",
        "Q — Extra-Sprung (20s Abklingzeit)",
        "P — Fotomodus (frei fliegen & Fotos machen)",
        "Tab — Shop / Inventar öffnen",
        "Escape — Menü",
    };

    // ---- Spielhilfe: explains WHAT everything in the game does (not the keys - that's Steuerung).
    static readonly (string Title, string Body)[] HelpSections =
    {
        ("Das Ziel",
         "Klettere den Turm hinauf bis zur Flagge auf dem Gipfel. Fällst du zu weit unter deinen höchsten Punkt, geht es zurück zum letzten Checkpoint (die Schilder am Weg). Extra große Plattformen sind sichere Rastplätze — manche mit Laterne, Kristallen und Münzen."),
        ("Die vier Modi",
         "Klassisch: jage deine Bestzeit zum Gipfel. Zeitrennen: erreiche die Flagge in unter 20 Minuten — bewegliche Plattformen sind hier schneller! Endlos: der Turm ist 5× so lang, deine Rekord-Höhe wird gespeichert, ganz oben wartet die Endlos-Krone. Hardcore: EIN Leben — der erste Tod beendet den Lauf; Bröckel-Plattformen zerfallen schneller und Gegner sind aggressiver."),
        ("Gleiten",
         "Halte in der Luft die Sprungtaste: du segelst weit statt zu fallen. Manche Lücken im Weg sind NUR gleitend zu schaffen — du erkennst sie an den cyanfarbenen Leucht-Punkten über der Lücke und der extra großen Lande-Plattform dahinter."),
        ("Ziplines & Schatzkisten",
         "Neben dem Weg stehen leuchtende Seil-Masten (die erste gleich an der Spawn-Wiese!): stell dich an den grünen Griff und drücke E — du fährst am Seil zu einer fernen Schatz-Insel, die nur die Zipline erreicht. Dort wartet eine goldene Schatzkiste (+100 Münzen + Erfolg). Am zweiten Mast der Insel bringt dich E zurück auf den Weg. Mit der Sprungtaste springst du jederzeit ab."),
        ("Boost-Ringe, Windsäulen & Eis",
         "Grüne Leucht-Ringe: hindurchspringen oder -gleiten gibt einen kräftigen Schub nach oben. Im Wolkenreich tragen dich sichtbare Windsäulen aufwärts — mit Gleiten reiten! Im Eiskristall sind manche Plattformen spiegelglatt: kaum Grip, der Schwung rutscht weiter."),
        ("Gegner & der Bogen",
         "Ziehe den Bogen mit dem Mausrad und schieße mit Linksklick. Kopftreffer machen doppelten Schaden — ein sauberer Headshot legt normale Schatten sofort um. Drei Arten: Wächter (Berührung = Reset), Patrouille (kreist über der Plattform), Schütze (weiche seinen roten Bolzen aus!). Ganz oben bewacht der große GIPFELWÄCHTER die Flagge — besiege ihn für 150 Münzen und einen Erfolg."),
        ("Münzen, Combo & Shop",
         "Sammle Münzen auf den Plattformen — schnell hintereinander eingesammelt stapelt sich ein Combo-Bonus bis ×3. Im Shop (Tab) kaufst du Skins, Accessoires und Aktions-Effekte (färben Sprungring, Dash-Schweif & Staub). Ein abgeschlossener Lauf bringt 300 Bonus-Münzen, Gegner und Kisten zahlen extra."),
        ("Challenges & Erfolge",
         "Jeden Tag und jede Woche gibt es eine Aufgabe mit Münz-Belohnung — beide stehen unten im Hauptmenü, der Wochen-Fortschritt zählt über mehrere Läufe. Dazu 18 Erfolge (im Menü und im Launcher) und deine Bestzeiten pro Modus als Rekorde."),
        ("Extras",
         "Fotomodus (P): frei fliegen, HUD aus, Schnappschüsse in den Screenshots-Ordner. Koop über LAN: zusammen klettern — der Host wählt den Modus, Münzen zählen pro Spieler getrennt. Der Bestzeit-Vergleich läuft über die Records im Menü."),
    };

    Vector2 helpScroll;
    static GUIStyle helpTitleStyle, helpBodyStyle;

    void DrawGameHelp()
    {
        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 560f, h = 580f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        DrawScreenChrome(new Rect(x, y, w, h), "Spielhilfe");

        if (helpTitleStyle == null)
        {
            helpTitleStyle = new GUIStyle(UITheme.SubtitleStyle) { fontSize = 15 };
            helpTitleStyle.normal.textColor = UITheme.Gold;
            helpBodyStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 13, wordWrap = true };
        }

        Rect area = new Rect(x + 18, y + 60, w - 36, h - 122);
        float contentW = area.width - 22f;

        // Measure total height (word-wrapped bodies vary), then draw inside one scroll view.
        float total = 0f;
        foreach (var s in HelpSections)
            total += 26f + helpBodyStyle.CalcHeight(new GUIContent(s.Body), contentW - 24f) + 22f;

        helpScroll = GUI.BeginScrollView(area, helpScroll, new Rect(0, 0, contentW, total));
        float cy = 0f;
        foreach (var s in HelpSections)
        {
            float bodyH = helpBodyStyle.CalcHeight(new GUIContent(s.Body), contentW - 24f);
            GUI.Box(new Rect(0, cy, contentW, 26f + bodyH + 14f), "", UITheme.CardStyle);
            GUI.Label(new Rect(12, cy + 6f, contentW - 24f, 20f), s.Title, helpTitleStyle);
            GUI.Label(new Rect(12, cy + 28f, contentW - 24f, bodyH), s.Body, helpBodyStyle);
            cy += 26f + bodyH + 22f;
        }
        GUI.EndScrollView();

        if (GUI.Button(new Rect(x + 30, y + h - 48, w - 60, 36), "↩ Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Main);
        }

        GUI.color = prevColor;
    }

    // ---- Statistiken: mode records, lifetime stats and challenge status on proper cards -------
    static GUIStyle statValueStyle, statSubStyle, statMiniValueStyle, challengeStatusStyle;

    void DrawStats()
    {
        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 560f, h = 566f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;
        DrawScreenChrome(new Rect(x, y, w, h), "Statistiken");

        if (statValueStyle == null)
        {
            statValueStyle = new GUIStyle(UITheme.CoinStyle) { fontSize = 26 };
            statSubStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 11 };
            statSubStyle.normal.textColor = UITheme.InkDim;
            statMiniValueStyle = new GUIStyle(UITheme.CoinStyle) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            challengeStatusStyle = new GUIStyle(UITheme.SubtitleStyle) { fontSize = 13, alignment = TextAnchor.MiddleRight };
        }

        // Best-per-mode record cards (2x2).
        float cw = (w - 40f - 12f) / 2f, ch = 84f;
        DrawStatCard(new Rect(x + 20, y + 62, cw, ch), new Color(0.35f, 0.78f, 0.45f), "Klassisch",
            FormatTime(SaveSystem.BestTime(GameMode.Klassisch)), "Bestzeit zum Gipfel");
        DrawStatCard(new Rect(x + 32 + cw, y + 62, cw, ch), new Color(0.4f, 0.68f, 1f), "Zeitrennen",
            FormatTime(SaveSystem.BestTime(GameMode.Zeitrennen)), "Bestzeit (20-Minuten-Limit)");
        DrawStatCard(new Rect(x + 20, y + 62 + ch + 12, cw, ch), new Color(0.76f, 0.52f, 1f), "Endlos",
            $"{GameModeState.BestHeight:0} m", "Rekord-Höhe");
        DrawStatCard(new Rect(x + 32 + cw, y + 62 + ch + 12, cw, ch), new Color(0.95f, 0.45f, 0.48f), "Hardcore",
            FormatTime(SaveSystem.BestTime(GameMode.Hardcore)), "Bestzeit mit einem Leben");

        // Lifetime totals.
        float cy2 = y + 62 + 2 * ch + 12 + 18;
        GUI.Label(new Rect(x + 22, cy2, w - 40, 20), "GESAMT", statSubStyle);
        cy2 += 24;
        float chipW3 = (w - 40 - 20) / 3f;
        DrawMiniStat(new Rect(x + 20, cy2, chipW3, 58), UITheme.Gold, EconomySystem.TotalCoinsEarned.ToString(), "Münzen gesamt");
        DrawMiniStat(new Rect(x + 30 + chipW3, cy2, chipW3, 58), new Color(0.95f, 0.5f, 0.5f), GameManager.TotalDeaths.ToString(), "Tode gesamt");
        DrawMiniStat(new Rect(x + 40 + chipW3 * 2, cy2, chipW3, 58), UITheme.Accent,
            $"{Achievements.UnlockedCount()}/{Achievements.All.Length}", "Erfolge");

        // Challenge status.
        float cy3 = cy2 + 58 + 18;
        GUI.Label(new Rect(x + 22, cy3, w - 40, 20), "CHALLENGES", statSubStyle);
        cy3 += 24;
        var daily = DailyChallenge.Today;
        DrawChallengeRow(new Rect(x + 20, cy3, w - 40, 40), "☀", DailyChallenge.DoneToday, daily.Text, daily.Reward, -1, -1);
        var weekly = WeeklyChallenge.Current;
        DrawChallengeRow(new Rect(x + 20, cy3 + 46, w - 40, 40), "★", WeeklyChallenge.Done, weekly.Text, weekly.Reward,
            WeeklyChallenge.Progress(0f), weekly.Target);

        if (GUI.Button(new Rect(x + 30, y + h - 48, w - 60, 36), "↩ Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Main);
        }

        GUI.color = prevColor;
    }

    static void DrawStatCard(Rect r, Color accent, string title, string value, string sub)
    {
        GUI.Box(r, "", UITheme.CardStyle);
        UITheme.RoundRect(new Rect(r.x + 12, r.y + 12, 6, r.height - 24), accent);
        GUI.Label(new Rect(r.x + 28, r.y + 8, r.width - 36, 20), title, UITheme.SubtitleStyle);
        statValueStyle.normal.textColor = accent;
        GUI.Label(new Rect(r.x + 28, r.y + 28, r.width - 36, 30), value, statValueStyle);
        GUI.Label(new Rect(r.x + 28, r.y + 58, r.width - 36, 16), sub, statSubStyle);
    }

    static void DrawMiniStat(Rect r, Color accent, string value, string label)
    {
        GUI.Box(r, "", UITheme.CardStyle);
        statMiniValueStyle.normal.textColor = accent;
        GUI.Label(new Rect(r.x, r.y + 8, r.width, 24), value, statMiniValueStyle);
        var c = new GUIStyle(statSubStyle) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(r.x, r.y + 34, r.width, 16), label, c);
    }

    static void DrawChallengeRow(Rect r, string icon, bool done, string text, int reward, int progress, int target)
    {
        GUI.Box(r, "", UITheme.CardStyle);
        Color accent = done ? UITheme.Positive : UITheme.Gold;
        UITheme.RoundRect(new Rect(r.x + 12, r.y + 10, 20, 20), accent);
        GUI.Label(new Rect(r.x + 40, r.y + 9, r.width - 170, 22), text, UITheme.LabelStyle);
        challengeStatusStyle.normal.textColor = accent;
        string status = done ? "✓ erledigt"
            : (target > 0 ? $"{progress}/{target}  ·  +{reward}" : $"+{reward}");
        GUI.Label(new Rect(r.xMax - 160, r.y + 9, 148, 22), status, challengeStatusStyle);
    }

    static GUIStyle controlKeyStyle, controlDescStyle;

    void DrawControls()
    {
        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 540f;
        // Sized to the number of control lines so nothing is clipped or overlaps the back button.
        float h = 66f + ControlLines.Length * 27f + 62f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        DrawScreenChrome(new Rect(x, y, w, h), "Steuerung");

        if (controlKeyStyle == null)
        {
            controlKeyStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 13, fontStyle = FontStyle.Bold };
            controlKeyStyle.normal.textColor = UITheme.Gold;
            controlDescStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 13 };
        }

        // Two-column layout (gold key -> description) with subtle zebra rows - reads like a real
        // keybinding table instead of a wall of text.
        float ly = y + 62f;
        for (int i = 0; i < ControlLines.Length; i++)
        {
            if (i % 2 == 0)
                UITheme.RoundRect(new Rect(x + 18f, ly - 3f, w - 36f, 25f), new Color(1f, 1f, 1f, 0.035f));

            string line = ControlLines[i];
            int sep = line.IndexOf('—');
            if (sep > 0)
            {
                GUI.Label(new Rect(x + 30, ly, 200, 22), line.Substring(0, sep).Trim(), controlKeyStyle);
                GUI.Label(new Rect(x + 240, ly, w - 268, 22), line.Substring(sep + 1).Trim(), controlDescStyle);
            }
            else
            {
                GUI.Label(new Rect(x + 30, ly, w - 60, 22), line, controlDescStyle);
            }
            ly += 27f;
        }

        if (GUI.Button(new Rect(x + 30, y + h - 48, w - 60, 36), "↩ Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Main);
        }

        GUI.color = prevColor;
    }

    void DrawAchievements()
    {
        UITheme.EnsureInit();
        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 480f, h = 540f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        DrawScreenChrome(new Rect(x, y, w, h), "Errungenschaften");

        int unlocked = Achievements.UnlockedCount();
        var countStyle = new GUIStyle(UITheme.SubtitleStyle) { alignment = TextAnchor.MiddleCenter };
        countStyle.normal.textColor = UITheme.Gold;
        GUI.Label(new Rect(x, y + 56, w, 22), $"{unlocked} / {Achievements.All.Length} freigeschaltet", countStyle);

        Rect area = new Rect(x + 16, y + 88, w - 32, h - 150);
        const float rowH = 62f;
        Rect view = new Rect(0, 0, area.width - 20, Achievements.All.Length * rowH);
        achScroll = GUI.BeginScrollView(area, achScroll, view);
        float ry = 0f;
        foreach (var a in Achievements.All)
        {
            bool got = Achievements.IsUnlocked(a.Key);
            Rect card = new Rect(0, ry, view.width, rowH - 8f);

            Color prev = GUI.color;
            if (!got)
                GUI.color = new Color(1f, 1f, 1f, FadeAlpha * 0.5f);
            GUI.Box(card, "", UITheme.CardStyle);
            UITheme.RoundRect(new Rect(card.x + 14, card.y + 15, 26, 26), got ? UITheme.Gold : new Color(0.4f, 0.42f, 0.5f));
            var iconStyle = new GUIStyle(UITheme.SubtitleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            iconStyle.normal.textColor = got ? new Color(0.1f, 0.09f, 0.12f) : UITheme.InkDim;
            GUI.Label(new Rect(card.x + 14, card.y + 13, 26, 26), got ? "★" : "?", iconStyle);

            var tStyle = new GUIStyle(UITheme.SubtitleStyle);
            tStyle.normal.textColor = got ? UITheme.Gold : UITheme.InkDim;
            GUI.Label(new Rect(card.x + 52, card.y + 8, card.width - 64, 22), a.Title, tStyle);
            GUI.Label(new Rect(card.x + 52, card.y + 30, card.width - 64, 20), a.Desc, UITheme.LabelStyle);
            GUI.color = prev;
            ry += rowH;
        }
        GUI.EndScrollView();

        if (GUI.Button(new Rect(x + 30, y + h - 50, w - 60, 36), "↩ Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Main);
        }

        GUI.color = prevColor;
    }

    void DrawNamePrompt()
    {
        UITheme.EnsureInit();

        float w = 380f;
        float h = 200f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        DrawScreenChrome(new Rect(x, y, w, h), "Benutzername");

        GUI.SetNextControlName("NameField");
        nameInput = GUI.TextField(new Rect(x + 20, y + 65, w - 40, 30), nameInput, PlayerProfile.MaxNameLength);
        if (!nameFieldFocused)
        {
            GUI.FocusControl("NameField");
            nameFieldFocused = true;
        }

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

    void DrawQuitConfirm()
    {
        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 420f;
        float h = 210f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        var bodyStyle = new GUIStyle(UITheme.LabelStyle) { wordWrap = true, alignment = TextAnchor.MiddleCenter };

        DrawScreenChrome(new Rect(x, y, w, h), "Spiel verlassen?");
        GUI.Label(new Rect(x + 20, y + 62, w - 40, 55), "Nicht gespeicherter Fortschritt seit dem letzten Checkpoint geht verloren.", bodyStyle);

        if (GUI.Button(new Rect(x + 20, y + h - 50, (w - 50) / 2, 36), "Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Main);
        }
        if (GUI.Button(new Rect(x + 30 + (w - 50) / 2, y + h - 50, (w - 50) / 2, 36), "Spiel verlassen", UITheme.DangerButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            Application.Quit();
        }

        GUI.color = prevColor;
    }

    // The dialog's mode selection is TENTATIVE: clicking a mode only marks it here; it becomes
    // the real mode when "Neu starten" is confirmed. (Previously a stray click on e.g. Hardcore
    // followed by "Abbrechen" silently switched the running game's mode - every death then popped
    // the hardcore game-over screen.)
    static GameMode pendingMode = GameMode.Klassisch;

    static void DrawModeButton(Rect r, GameMode m)
    {
        bool sel = pendingMode == m;
        if (GUI.Button(r, GameModeState.Name(m), sel ? UITheme.TabActiveStyle : UITheme.TabStyle))
        {
            AudioManager.Instance?.PlayClick();
            pendingMode = m;
        }
    }

    void DrawRestartConfirm()
    {
        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, FadeAlpha);

        float w = 430f;
        float h = 340f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        var bodyStyle = new GUIStyle(UITheme.LabelStyle) { wordWrap = true, alignment = TextAnchor.MiddleCenter };

        DrawScreenChrome(new Rect(x, y, w, h), "Neues Spiel");
        GUI.Label(new Rect(x + 20, y + 58, w - 40, 24), "Dein gespeicherter Fortschritt wird gelöscht.", bodyStyle);

        // Full mode selection every time you start a new game (2x2 grid: four modes now).
        GUI.Label(new Rect(x + 20, y + 96, w - 40, 20), "Modus wählen:", UITheme.SubtitleStyle);
        float mw = (w - 40f - 8f) / 2f;
        DrawModeButton(new Rect(x + 20, y + 120, mw, 36), GameMode.Klassisch);
        DrawModeButton(new Rect(x + 28 + mw, y + 120, mw, 36), GameMode.Zeitrennen);
        DrawModeButton(new Rect(x + 20, y + 162, mw, 36), GameMode.Endlos);
        DrawModeButton(new Rect(x + 28 + mw, y + 162, mw, 36), GameMode.Hardcore);

        if (GUI.Button(new Rect(x + 20, y + h - 52, (w - 50) / 2, 38), "Abbrechen", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            SetScreen(MenuScreen.Main);
        }
        if (GUI.Button(new Rect(x + 30 + (w - 50) / 2, y + h - 52, (w - 50) / 2, 38), "Neu starten", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            GameModeState.Current = pendingMode; // commit the tentative mode selection NOW
            SaveSystem.DeleteSave();
            if (HasActiveGame && GameManager.Instance != null)
                GameManager.Instance.RestartRun();   // in-place reset, no scene reload
            else
                SetScreen(MenuScreen.Play);
        }

        GUI.color = prevColor;
    }
}
