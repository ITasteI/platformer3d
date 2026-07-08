using UnityEngine;

public static class UITheme
{
    public static GUIStyle PanelStyle;
    public static GUIStyle ButtonStyle;
    public static GUIStyle TitleStyle;
    public static GUIStyle LabelStyle;
    public static GUIStyle HudStyle;

    // Added for the shop + reworked HUD.
    public static GUIStyle CardStyle;      // item row background
    public static GUIStyle PillStyle;      // HUD stat chip background
    public static GUIStyle CoinStyle;      // gold bold number
    public static GUIStyle SubtitleStyle;  // small heading
    public static GUIStyle TabStyle;
    public static GUIStyle TabActiveStyle;
    public static GUIStyle BuyStyle;
    public static GUIStyle EquipStyle;
    public static GUIStyle DangerButtonStyle; // destructive actions (Beenden, Neu starten)
    public static GUIStyle TagStyle;       // "Aktiv"/"Gekauft" marker
    public static GUIStyle RoundedFillStyle; // white rounded 9-slice; tint via GUI.color for coloured buttons

    public static Texture2D White;
    private static Texture2D barBgTex;
    private static Texture2D pillFillTex;   // rounded fill for chips/bars
    private static Texture2D glowTex;       // soft radial glow sprite (menu orbs / hover glow)

    // Refined, slightly richer palette for a more premium look.
    public static readonly Color Gold = new Color(1f, 0.83f, 0.35f);
    public static readonly Color Accent = new Color(0.36f, 0.74f, 1f);
    public static readonly Color Positive = new Color(0.38f, 0.86f, 0.5f);
    public static readonly Color Danger = new Color(0.98f, 0.44f, 0.44f);
    public static readonly Color Ink = new Color(0.9f, 0.94f, 1f);          // primary text
    public static readonly Color InkDim = new Color(0.62f, 0.68f, 0.8f);    // secondary text

    private static bool initialized;

    // ---- Resolution-independent UI ------------------------------------------------------------
    // Every OnGUI is authored for a 1080p canvas; this scales it up on 1440p/4K displays so the
    // interface never renders tiny (the practical core of a UI-framework migration, delivered
    // inside IMGUI). At <=1080p the scale is exactly 1 - nothing changes there.
    // Call BeginUI() at the TOP of every OnGUI, and use ScreenW/ScreenH instead of Screen.* for
    // layout. IMGUI transforms mouse hit-testing by GUI.matrix automatically.
    public static float UIScale { get; private set; } = 1f;
    public static float ScreenW => Screen.width / UIScale;
    public static float ScreenH => Screen.height / UIScale;

    // The UI font: Segoe UI (present on every Windows box) instead of IMGUI's default Arial -
    // the single cheapest "this looks like a real game now" switch. Falls back silently.
    public static Font MainFont { get; private set; }
    static bool fontTried;

    public static void BeginUI()
    {
        UIScale = Mathf.Max(1f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(UIScale, UIScale, 1f));

        if (!fontTried)
        {
            fontTried = true;
            try { MainFont = Font.CreateDynamicFontFromOSFont("Segoe UI", 16); }
            catch { MainFont = null; }
        }
        if (MainFont != null)
            GUI.skin.font = MainFont; // default font for every style that doesn't set its own
    }

    public static void EnsureInit()
    {
        if (initialized)
            return;
        initialized = true;

        White = MakeTex(Color.white);
        barBgTex = MakeRounded(22, 8, new Color(0f, 0f, 0f, 0.5f), default, 0f);
        pillFillTex = MakeRounded(22, 7, Color.white, default, 0f);

        // Rounded, subtly bordered backgrounds. The 9-slice border keeps corners crisp at any size.
        Texture2D panelTex = MakeRounded(40, 18, new Color(0.10f, 0.12f, 0.18f, 0.97f), new Color(0.42f, 0.55f, 0.85f, 0.35f), 2f);
        Texture2D cardTex = MakeRounded(32, 13, new Color(0.16f, 0.18f, 0.25f, 0.98f), new Color(1f, 1f, 1f, 0.06f), 1.5f);
        Texture2D pillTex = MakeRounded(28, 12, new Color(0.08f, 0.10f, 0.15f, 0.78f), new Color(1f, 1f, 1f, 0.08f), 1.5f);
        // Buttons get a subtle top-light VERTICAL GRADIENT - reads as depth instead of flat fills.
        Texture2D buttonTex = MakeRoundedGradient(30, 12, new Color(0.30f, 0.58f, 1f), new Color(0.15f, 0.40f, 0.85f), new Color(1f, 1f, 1f, 0.20f), 1.5f);
        Texture2D buttonHoverTex = MakeRoundedGradient(30, 12, new Color(0.42f, 0.70f, 1f), new Color(0.22f, 0.50f, 0.98f), new Color(1f, 1f, 1f, 0.36f), 1.5f);
        Texture2D buyTex = MakeRoundedGradient(30, 12, new Color(0.26f, 0.72f, 0.42f), new Color(0.14f, 0.52f, 0.28f), new Color(1f, 1f, 1f, 0.20f), 1.5f);
        Texture2D buyHoverTex = MakeRoundedGradient(30, 12, new Color(0.34f, 0.84f, 0.52f), new Color(0.2f, 0.64f, 0.36f), new Color(1f, 1f, 1f, 0.34f), 1.5f);
        Texture2D equipTex = MakeRoundedGradient(30, 12, new Color(0.68f, 0.56f, 0.18f), new Color(0.46f, 0.38f, 0.10f), new Color(1f, 1f, 1f, 0.20f), 1.5f);
        Texture2D equipHoverTex = MakeRoundedGradient(30, 12, new Color(0.84f, 0.70f, 0.24f), new Color(0.6f, 0.5f, 0.14f), new Color(1f, 1f, 1f, 0.34f), 1.5f);
        Texture2D dangerTex = MakeRoundedGradient(30, 12, new Color(0.88f, 0.36f, 0.38f), new Color(0.66f, 0.2f, 0.24f), new Color(1f, 1f, 1f, 0.20f), 1.5f);
        Texture2D dangerHoverTex = MakeRoundedGradient(30, 12, new Color(1f, 0.46f, 0.48f), new Color(0.8f, 0.28f, 0.32f), new Color(1f, 1f, 1f, 0.34f), 1.5f);
        Texture2D tabTex = MakeRounded(28, 11, new Color(0.13f, 0.15f, 0.21f, 1f), new Color(1f, 1f, 1f, 0.05f), 1.5f);
        Texture2D tabActiveTex = MakeRounded(28, 11, new Color(0.20f, 0.50f, 0.95f, 1f), new Color(1f, 1f, 1f, 0.25f), 1.5f);
        Texture2D roundedWhiteTex = MakeRounded(30, 12, Color.white, default, 0f); // tint via GUI.color
        glowTex = MakeGlow(64);

        RectOffset b18 = new RectOffset(18, 18, 18, 18);
        RectOffset b13 = new RectOffset(13, 13, 13, 13);
        RectOffset b12 = new RectOffset(12, 12, 12, 12);
        RectOffset b11 = new RectOffset(11, 11, 11, 11);

        PanelStyle = new GUIStyle(GUI.skin.box) { border = b18 };
        PanelStyle.normal.background = panelTex;
        CardStyle = new GUIStyle(GUI.skin.box) { border = b13 };
        CardStyle.normal.background = cardTex;
        PillStyle = new GUIStyle(GUI.skin.box) { border = b12 };
        PillStyle.normal.background = pillTex;
        RoundedFillStyle = new GUIStyle(GUI.skin.box) { border = b12 };
        RoundedFillStyle.normal.background = roundedWhiteTex;

        ButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            fixedHeight = 0,
            border = b12,
            padding = new RectOffset(14, 14, 10, 10),
            alignment = TextAnchor.MiddleCenter,
        };
        ButtonStyle.normal.background = buttonTex;
        ButtonStyle.normal.textColor = Color.white;
        ButtonStyle.hover.background = buttonHoverTex;
        ButtonStyle.hover.textColor = Color.white;
        ButtonStyle.active.background = buttonHoverTex;
        ButtonStyle.active.textColor = Color.white;

        BuyStyle = new GUIStyle(ButtonStyle) { fontSize = 14 };
        BuyStyle.normal.background = buyTex;
        BuyStyle.hover.background = buyHoverTex;
        BuyStyle.active.background = buyHoverTex;

        EquipStyle = new GUIStyle(ButtonStyle) { fontSize = 14 };
        EquipStyle.normal.background = equipTex;
        EquipStyle.hover.background = equipHoverTex;
        EquipStyle.active.background = equipHoverTex;

        DangerButtonStyle = new GUIStyle(ButtonStyle);
        DangerButtonStyle.normal.background = dangerTex;
        DangerButtonStyle.hover.background = dangerHoverTex;
        DangerButtonStyle.active.background = dangerHoverTex;

        TabStyle = new GUIStyle(ButtonStyle) { fontSize = 15, border = b11 };
        TabStyle.normal.background = tabTex;
        TabStyle.hover.background = buttonHoverTex;

        TabActiveStyle = new GUIStyle(ButtonStyle) { fontSize = 15, fontStyle = FontStyle.Bold, border = b11 };
        TabActiveStyle.normal.background = tabActiveTex;

        TitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        TitleStyle.normal.textColor = Gold;

        SubtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        SubtitleStyle.normal.textColor = InkDim;

        LabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        LabelStyle.normal.textColor = Ink;

        HudStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        HudStyle.normal.textColor = Ink;

        CoinStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        CoinStyle.normal.textColor = Gold;

        TagStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        TagStyle.normal.textColor = Positive;
    }

    // Draws a rounded horizontal progress bar (0..1) with a colored fill.
    public static void Bar(Rect rect, float fill, Color fillColor)
    {
        EnsureInit();
        GUI.DrawTexture(rect, barBgTex);
        fill = Mathf.Clamp01(fill);
        if (fill <= 0f)
            return;
        Color prev = GUI.color;
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fill, rect.height), pillFillTex, ScaleMode.StretchToFill, true);
        GUI.color = prev;
    }

    // Solid color rectangle helper (for accent swatches, icons).
    public static void Rect(Rect rect, Color color)
    {
        EnsureInit();
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, White);
        GUI.color = prev;
    }

    // Rounded filled rectangle (accent dots, small chips) - softer than a hard square.
    public static void RoundRect(Rect rect, Color color)
    {
        EnsureInit();
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, pillFillTex, ScaleMode.StretchToFill, true);
        GUI.color = prev;
    }

    // Draws a soft radial glow tinted by color (for menu orbs, hover glows, title bloom).
    public static void DrawGlow(Rect rect, Color color)
    {
        EnsureInit();
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, glowTex, ScaleMode.StretchToFill, true);
        GUI.color = prev;
    }

    private static Texture2D MakeGlow(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        Vector2 c = new Vector2((size - 1) / 2f, (size - 1) / 2f);
        float maxR = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a; // soft cubic falloff to a gentle halo
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeTex(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    // Signed distance from a pixel to a rounded-rect edge (<0 inside). Standard rounded-box SDF.
    private static float RoundedBoxSdf(float x, float y, float halfW, float halfH, float radius)
    {
        float px = Mathf.Abs(x) - (halfW - radius);
        float py = Mathf.Abs(y) - (halfH - radius);
        float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) + Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
        float inside = Mathf.Min(Mathf.Max(px, py), 0f);
        return outside + inside - radius;
    }

    // Rounded rect with a vertical top->bottom gradient fill (buttons read as lit from above).
    private static Texture2D MakeRoundedGradient(int size, int radius, Color top, Color bottom, Color border, float borderWidth)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        float half = size / 2f;
        bool hasBorder = border.a > 0.001f && borderWidth > 0f;
        for (int y = 0; y < size; y++)
        {
            Color fill = Color.Lerp(bottom, top, y / (float)(size - 1)); // texture rows run bottom-up
            for (int x = 0; x < size; x++)
            {
                float sdf = RoundedBoxSdf(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                float coverage = Mathf.Clamp01(0.5f - sdf);
                Color c = fill;
                if (hasBorder)
                {
                    float depth = -sdf;
                    if (depth < borderWidth)
                        c = Color.Lerp(border, fill, Mathf.Clamp01(depth / borderWidth));
                }
                c.a *= coverage;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    // Builds an anti-aliased rounded-rectangle texture with an optional inner border, for 9-slicing.
    private static Texture2D MakeRounded(int size, int radius, Color fill, Color border, float borderWidth)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        float half = size / 2f;
        bool hasBorder = border.a > 0.001f && borderWidth > 0f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sdf = RoundedBoxSdf(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                float coverage = Mathf.Clamp01(0.5f - sdf); // ~1 inside, ~0 outside, AA at the edge
                Color c = fill;
                if (hasBorder)
                {
                    float depth = -sdf; // how far inside the edge
                    if (depth < borderWidth)
                        c = Color.Lerp(border, fill, Mathf.Clamp01(depth / borderWidth));
                }
                c.a *= coverage;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }
}
