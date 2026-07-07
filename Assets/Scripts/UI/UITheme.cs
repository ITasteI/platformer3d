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
    public static GUIStyle TagStyle;       // "Aktiv"/"Gekauft" marker

    public static Texture2D White;
    private static Texture2D barBgTex;

    public static readonly Color Gold = new Color(1f, 0.82f, 0.32f);
    public static readonly Color Accent = new Color(0.34f, 0.72f, 1f);
    public static readonly Color Positive = new Color(0.36f, 0.85f, 0.45f);
    public static readonly Color Danger = new Color(0.95f, 0.4f, 0.4f);

    private static bool initialized;

    public static void EnsureInit()
    {
        if (initialized)
            return;
        initialized = true;

        White = MakeTex(Color.white);
        barBgTex = MakeTex(new Color(0f, 0f, 0f, 0.45f));

        Texture2D panelTex = MakeTex(new Color(0.07f, 0.08f, 0.11f, 0.94f));
        Texture2D cardTex = MakeTex(new Color(0.14f, 0.16f, 0.21f, 0.96f));
        Texture2D pillTex = MakeTex(new Color(0.06f, 0.07f, 0.1f, 0.72f));
        Texture2D buttonTex = MakeTex(new Color(0.16f, 0.45f, 0.85f, 1f));
        Texture2D buttonHoverTex = MakeTex(new Color(0.24f, 0.56f, 0.95f, 1f));
        Texture2D buyTex = MakeTex(new Color(0.2f, 0.6f, 0.32f, 1f));
        Texture2D buyHoverTex = MakeTex(new Color(0.27f, 0.72f, 0.4f, 1f));
        Texture2D equipTex = MakeTex(new Color(0.5f, 0.42f, 0.12f, 1f));
        Texture2D equipHoverTex = MakeTex(new Color(0.66f, 0.55f, 0.16f, 1f));
        Texture2D tabTex = MakeTex(new Color(0.12f, 0.13f, 0.17f, 1f));
        Texture2D tabActiveTex = MakeTex(new Color(0.16f, 0.45f, 0.85f, 1f));

        PanelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTex } };
        CardStyle = new GUIStyle(GUI.skin.box) { normal = { background = cardTex } };
        PillStyle = new GUIStyle(GUI.skin.box) { normal = { background = pillTex } };

        ButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 0 };
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

        TabStyle = new GUIStyle(ButtonStyle) { fontSize = 15 };
        TabStyle.normal.background = tabTex;
        TabStyle.hover.background = buttonHoverTex;

        TabActiveStyle = new GUIStyle(ButtonStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
        TabActiveStyle.normal.background = tabActiveTex;

        TitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        TitleStyle.normal.textColor = Gold;

        SubtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        SubtitleStyle.normal.textColor = Color.white;

        LabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        LabelStyle.normal.textColor = Color.white;

        HudStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        HudStyle.normal.textColor = Color.white;

        CoinStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        CoinStyle.normal.textColor = Gold;

        TagStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        TagStyle.normal.textColor = Positive;
    }

    // Draws a simple horizontal progress bar (0..1) with a colored fill.
    public static void Bar(Rect rect, float fill, Color fillColor)
    {
        EnsureInit();
        GUI.DrawTexture(rect, barBgTex);
        fill = Mathf.Clamp01(fill);
        if (fill <= 0f)
            return;
        Color prev = GUI.color;
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fill, rect.height), White);
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

    private static Texture2D MakeTex(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}
