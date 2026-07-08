using System.Collections.Generic;
using UnityEngine;

public class SettingsMenu : MonoBehaviour
{
    const string VolumePrefKey = "MasterVolume";
    const string QualityPrefKey = "QualityLevel";
    const string ResWidthPrefKey = "ResWidth";
    const string ResHeightPrefKey = "ResHeight";
    const string FullscreenPrefKey = "Fullscreen";
    const string FpsCapPrefKey = "FpsCap";

    // FPS choices: -1 = VSync (sync to the monitor's refresh rate), 0 = truly unlimited (vSync
    // off), otherwise a manual cap via Application.targetFrameRate (vSync off so it takes).
    static readonly int[] FpsOptions = { -1, 0, 30, 60, 120, 144 };

    // Settings are grouped into tabs so each page stays short and tidy instead of one long list.
    enum SettingsTab { Allgemein, Anzeige, Grafik }
    private SettingsTab tab = SettingsTab.Allgemein;

    private float volume = 1f;
    private float musicVol = 1f;
    private float sfxVol = 1f;
    private string nameInput = "";
    private string nameStatus = "";

    void Awake()
    {
        volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
        AudioListener.volume = volume;
        musicVol = MusicManager.MusicVolume;
        sfxVol = AudioManager.SfxVolume;
        nameInput = PlayerProfile.Name;
        ApplySavedDisplaySettings();
        ApplySavedFpsCap();
    }

    static void ApplySavedDisplaySettings()
    {
        if (PlayerPrefs.HasKey(QualityPrefKey))
            QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualityPrefKey), 0, QualitySettings.names.Length - 1), true);

        // First launch (nothing saved yet): start in a clean, closeable 720p window instead of
        // borderless fullscreen. Borderless has no window close button and on some displays the UI
        // scales wrong on the very first frame; a normal window avoids both. Persisted so it sticks;
        // the player can switch to fullscreen in Settings afterwards.
        if (!PlayerPrefs.HasKey(FullscreenPrefKey) && !PlayerPrefs.HasKey(ResWidthPrefKey))
        {
            PlayerPrefs.SetInt(FullscreenPrefKey, 0);
            PlayerPrefs.SetInt(ResWidthPrefKey, 1280);
            PlayerPrefs.SetInt(ResHeightPrefKey, 720);
            PlayerPrefs.Save();
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            return;
        }

        FullScreenMode mode = PlayerPrefs.GetInt(FullscreenPrefKey, 0) == 1
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        if (PlayerPrefs.HasKey(ResWidthPrefKey) && PlayerPrefs.HasKey(ResHeightPrefKey))
            Screen.SetResolution(PlayerPrefs.GetInt(ResWidthPrefKey), PlayerPrefs.GetInt(ResHeightPrefKey), mode);
        else
            Screen.fullScreenMode = mode;
    }

    // Applies the saved FPS cap. Only touches vSync/targetFrameRate if the player actually chose one,
    // so the default experience keeps the quality level's vSync behaviour untouched.
    public static void ApplySavedFpsCap()
    {
        if (!PlayerPrefs.HasKey(FpsCapPrefKey))
            return;
        ApplyFpsCapValue(PlayerPrefs.GetInt(FpsCapPrefKey, -1));
    }

    static void SetFpsCap(int cap)
    {
        PlayerPrefs.SetInt(FpsCapPrefKey, cap);
        PlayerPrefs.Save();
        ApplyFpsCapValue(cap);
    }

    // Choosing VSync restores vSyncCount, so "once capped, tearing forever" can't happen anymore
    // (previously any cap choice switched vSync off permanently, with no way back in the UI).
    static void ApplyFpsCapValue(int cap)
    {
        if (cap < 0)
        {
            QualitySettings.vSyncCount = 1;      // sync to refresh rate - no tearing
            Application.targetFrameRate = -1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;      // vSync must be off for a manual/unlimited cap
            Application.targetFrameRate = cap == 0 ? -1 : cap;
        }
    }

    static void SaveResolution(int width, int height)
    {
        PlayerPrefs.SetInt(ResWidthPrefKey, width);
        PlayerPrefs.SetInt(ResHeightPrefKey, height);
        Screen.SetResolution(width, height, Screen.fullScreenMode);
    }

    void OnGUI()
    {
        UITheme.BeginUI();
        if (MainMenu.Current != MenuScreen.Settings)
            return;

        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, MainMenu.FadeAlpha);

        float w = 520f;
        float h = 486f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        MainMenu.DrawScreenChrome(new Rect(x, y, w, h), "Einstellungen");

        // Tabs.
        float tabW = (w - 40f - 2 * 8f) / 3f;
        DrawSettingsTab(new Rect(x + 20, y + 58, tabW, 32), "Allgemein", SettingsTab.Allgemein);
        DrawSettingsTab(new Rect(x + 28 + tabW, y + 58, tabW, 32), "Anzeige", SettingsTab.Anzeige);
        DrawSettingsTab(new Rect(x + 36 + tabW * 2, y + 58, tabW, 32), "Grafik", SettingsTab.Grafik);

        float cy = y + 104f;
        switch (tab)
        {
            case SettingsTab.Allgemein: DrawGeneral(x, cy, w); break;
            case SettingsTab.Anzeige: DrawDisplay(x, cy, w); break;
            case SettingsTab.Grafik: DrawGraphics(x, cy, w); break;
        }

        if (GUI.Button(new Rect(x + 20, y + h - 50, w - 40, 34), "↩ Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            MainMenu.SetScreen(MenuScreen.Main);
        }

        GUI.color = prevColor;
    }

    void DrawSettingsTab(Rect r, string label, SettingsTab t)
    {
        if (GUI.Button(r, label, tab == t ? UITheme.TabActiveStyle : UITheme.TabStyle))
        {
            AudioManager.Instance?.PlayClick();
            tab = t;
        }
    }

    // ---- Allgemein: name, audio, sensitivity ----------------------------------------------------
    void DrawGeneral(float x, float cy, float w)
    {
        GUI.Label(new Rect(x + 20, cy, w - 40, 20), "Benutzername:", UITheme.LabelStyle);
        cy += 24f;
        nameInput = GUI.TextField(new Rect(x + 20, cy, w - 140, 28), nameInput, PlayerProfile.MaxNameLength);
        if (GUI.Button(new Rect(x + w - 110, cy, 90, 28), "Speichern", UITheme.ButtonStyle))
        {
            if (PlayerProfile.TrySetName(nameInput, out string sanitized))
            {
                nameInput = sanitized;
                nameStatus = "Gespeichert.";
                AudioManager.Instance?.PlayClick();
            }
            else
            {
                nameStatus = "Ungültiger Name.";
            }
        }
        cy += 32f;
        if (nameStatus.Length > 0)
            GUI.Label(new Rect(x + 20, cy, w - 40, 18), nameStatus, UITheme.LabelStyle);
        cy += 26f;

        cy = Slider(x, cy, w, $"Gesamt-Lautstärke: {Mathf.RoundToInt(volume * 100f)}%", volume, 0f, 1f, v =>
        {
            volume = v;
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(VolumePrefKey, v);
        });
        cy = Slider(x, cy, w, $"Musik: {Mathf.RoundToInt(musicVol * 100f)}%", musicVol, 0f, 1f, v =>
        {
            musicVol = v;
            MusicManager.SetMusicVolume(v);
        });
        cy = Slider(x, cy, w, $"Effekte: {Mathf.RoundToInt(sfxVol * 100f)}%", sfxVol, 0f, 1f, v =>
        {
            sfxVol = v;
            AudioManager.SetSfxVolume(v);
        });
        cy = Slider(x, cy, w, $"Maussensitivität: {CameraFollow.Sensitivity:0.0}", CameraFollow.Sensitivity, 0.5f, 10f,
            v => CameraFollow.SetSensitivity(v));
    }

    // ---- Anzeige: monitor, resolution, fullscreen -----------------------------------------------
    void DrawDisplay(float x, float cy, float w)
    {
        GUI.Label(new Rect(x + 20, cy, w - 40, 20), "Monitor:", UITheme.LabelStyle);
        cy += 24f;
        var displays = new List<DisplayInfo>();
        Screen.GetDisplayLayout(displays);
        int mn = Mathf.Max(1, displays.Count);
        float mw = (w - 40f - (mn - 1) * 8f) / mn;
        for (int i = 0; i < displays.Count; i++)
        {
            DisplayInfo info = displays[i];
            if (GUI.Button(new Rect(x + 20 + i * (mw + 8f), cy, mw, 32), "Monitor " + (i + 1), UITheme.ButtonStyle))
                Screen.MoveMainWindowTo(in info, Vector2Int.zero);
        }
        cy += 46f;

        GUI.Label(new Rect(x + 20, cy, w - 40, 20), "Auflösung:", UITheme.LabelStyle);
        cy += 26f;
        string[] resLabels = { "1280×720", "1920×1080", "2560×1440" };
        int[,] resVals = { { 1280, 720 }, { 1920, 1080 }, { 2560, 1440 } };
        float rw = (w - 40f - 2 * 8f) / 3f;
        for (int i = 0; i < 3; i++)
            if (GUI.Button(new Rect(x + 20 + i * (rw + 8f), cy, rw, 32), resLabels[i], UITheme.ButtonStyle))
                SaveResolution(resVals[i, 0], resVals[i, 1]);
        cy += 48f;

        bool fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
        bool newFullscreen = GUI.Toggle(new Rect(x + 20, cy, w - 40, 26), fullscreen, " Vollbild");
        if (newFullscreen != fullscreen)
        {
            Screen.fullScreenMode = newFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            PlayerPrefs.SetInt(FullscreenPrefKey, newFullscreen ? 1 : 0);
        }
    }

    // ---- Grafik: quality, FPS cap ---------------------------------------------------------------
    void DrawGraphics(float x, float cy, float w)
    {
        GUI.Label(new Rect(x + 20, cy, w - 40, 20), "Grafikqualität:", UITheme.LabelStyle);
        cy += 26f;
        string[] qualityNames = QualitySettings.names;
        int qn = Mathf.Max(1, qualityNames.Length);
        float qw = (w - 40f - (qn - 1) * 6f) / qn;
        var qStyle = new GUIStyle(UITheme.ButtonStyle) { fontSize = qn > 6 ? 12 : 13, padding = new RectOffset(3, 3, 8, 8) };
        int curQuality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < qn; i++)
        {
            if (GUI.Button(new Rect(x + 20 + i * (qw + 6f), cy, qw, 32), qualityNames[i], i == curQuality ? UITheme.TabActiveStyle : qStyle))
            {
                QualitySettings.SetQualityLevel(i, true);
                PlayerPrefs.SetInt(QualityPrefKey, i);
                ApplySavedFpsCap(); // SetQualityLevel resets vSync, which would undo the FPS cap
            }
        }
        cy += 52f;

        float curFov = Camera.main != null ? Camera.main.fieldOfView : PlayerPrefs.GetFloat("CameraFov", 60f);
        cy = Slider(x, cy, w, $"Sichtfeld (FOV): {curFov:0}°", curFov, 50f, 90f, v => CameraFollow.SetFov(v));

        GUI.Label(new Rect(x + 20, cy, w - 40, 20), "FPS-Limit:", UITheme.LabelStyle);
        cy += 26f;
        string[] fpsLabels = { "VSync", "Max", "30", "60", "120", "144" };
        int curFps = PlayerPrefs.GetInt(FpsCapPrefKey, -1);
        int fn = FpsOptions.Length;
        float fw = (w - 40f - (fn - 1) * 6f) / fn;
        var fpsStyle = new GUIStyle(UITheme.ButtonStyle) { fontSize = 13, padding = new RectOffset(3, 3, 8, 8) };
        for (int i = 0; i < fn; i++)
        {
            bool sel = FpsOptions[i] == curFps;
            if (GUI.Button(new Rect(x + 20 + i * (fw + 6f), cy, fw, 32), fpsLabels[i], sel ? UITheme.TabActiveStyle : fpsStyle))
            {
                AudioManager.Instance?.PlayClick();
                SetFpsCap(FpsOptions[i]);
            }
        }
    }

    // A labelled horizontal slider; invokes onChange when the value moves. Returns the next Y.
    static float Slider(float x, float cy, float w, string label, float value, float min, float max, System.Action<float> onChange)
    {
        GUI.Label(new Rect(x + 20, cy, w - 40, 20), label, UITheme.LabelStyle);
        cy += 22f;
        float nv = GUI.HorizontalSlider(new Rect(x + 20, cy + 8, w - 40, 20), value, min, max);
        if (!Mathf.Approximately(nv, value))
            onChange(nv);
        return cy + 36f;
    }
}
