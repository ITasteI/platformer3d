using UnityEngine;

public static class UITheme
{
    public static GUIStyle PanelStyle;
    public static GUIStyle ButtonStyle;
    public static GUIStyle TitleStyle;
    public static GUIStyle LabelStyle;
    public static GUIStyle HudStyle;

    private static bool initialized;

    public static void EnsureInit()
    {
        if (initialized)
            return;
        initialized = true;

        Texture2D panelTex = MakeTex(new Color(0.07f, 0.08f, 0.11f, 0.92f));
        Texture2D buttonTex = MakeTex(new Color(0.16f, 0.45f, 0.85f, 1f));
        Texture2D buttonHoverTex = MakeTex(new Color(0.24f, 0.56f, 0.95f, 1f));

        PanelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTex } };

        ButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fixedHeight = 0,
        };
        ButtonStyle.normal.background = buttonTex;
        ButtonStyle.normal.textColor = Color.white;
        ButtonStyle.hover.background = buttonHoverTex;
        ButtonStyle.hover.textColor = Color.white;
        ButtonStyle.active.background = buttonHoverTex;
        ButtonStyle.active.textColor = Color.white;

        TitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        TitleStyle.normal.textColor = new Color(1f, 0.82f, 0.3f);

        LabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        LabelStyle.normal.textColor = Color.white;

        HudStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        HudStyle.normal.textColor = Color.white;
    }

    private static Texture2D MakeTex(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}
