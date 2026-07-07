using System.Collections.Generic;
using UnityEngine;

public class SettingsMenu : MonoBehaviour
{
    const string VolumePrefKey = "MasterVolume";
    const string QualityPrefKey = "QualityLevel";
    const string ResWidthPrefKey = "ResWidth";
    const string ResHeightPrefKey = "ResHeight";
    const string FullscreenPrefKey = "Fullscreen";

    private float volume = 1f;
    private string nameInput = "";
    private string nameStatus = "";

    void Awake()
    {
        volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
        AudioListener.volume = volume;
        nameInput = PlayerProfile.Name;
        ApplySavedDisplaySettings();
    }

    static void ApplySavedDisplaySettings()
    {
        if (PlayerPrefs.HasKey(QualityPrefKey))
            QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualityPrefKey), 0, QualitySettings.names.Length - 1), true);

        FullScreenMode mode = PlayerPrefs.GetInt(FullscreenPrefKey, 1) == 1
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        if (PlayerPrefs.HasKey(ResWidthPrefKey) && PlayerPrefs.HasKey(ResHeightPrefKey))
            Screen.SetResolution(PlayerPrefs.GetInt(ResWidthPrefKey), PlayerPrefs.GetInt(ResHeightPrefKey), mode);
        else
            Screen.fullScreenMode = mode;
    }

    static void SaveResolution(int width, int height)
    {
        PlayerPrefs.SetInt(ResWidthPrefKey, width);
        PlayerPrefs.SetInt(ResHeightPrefKey, height);
        Screen.SetResolution(width, height, Screen.fullScreenMode);
    }

    void OnGUI()
    {
        if (MainMenu.Current != MenuScreen.Settings)
            return;

        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, MainMenu.FadeAlpha);

        float w = 380f;
        float h = 528f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x, y + 12, w, 28), "Einstellungen", UITheme.TitleStyle);
        float curY = y + 55f;

        GUI.Label(new Rect(x + 20, curY, w - 40, 20), "Benutzername:", UITheme.LabelStyle);
        curY += 22f;
        nameInput = GUI.TextField(new Rect(x + 20, curY, w - 130, 26), nameInput, PlayerProfile.MaxNameLength);
        if (GUI.Button(new Rect(x + w - 100, curY, 80, 26), "Speichern", UITheme.ButtonStyle))
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
        curY += 26f;
        if (nameStatus.Length > 0)
            GUI.Label(new Rect(x + 20, curY, w - 40, 18), nameStatus, UITheme.LabelStyle);
        curY += 26f;

        GUI.Label(new Rect(x + 20, curY, w - 40, 20), $"Lautstärke: {Mathf.RoundToInt(volume * 100f)}%", UITheme.LabelStyle);
        curY += 22f;
        float newVolume = GUI.HorizontalSlider(new Rect(x + 20, curY + 8, w - 40, 20), volume, 0f, 1f);
        if (!Mathf.Approximately(newVolume, volume))
        {
            volume = newVolume;
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(VolumePrefKey, volume);
        }
        curY += 36f;

        GUI.Label(new Rect(x + 20, curY, w - 40, 20), $"Maussensitivität: {CameraFollow.Sensitivity:0.0}", UITheme.LabelStyle);
        curY += 22f;
        float newSens = GUI.HorizontalSlider(new Rect(x + 20, curY + 8, w - 40, 20), CameraFollow.Sensitivity, 0.5f, 10f);
        if (!Mathf.Approximately(newSens, CameraFollow.Sensitivity))
            CameraFollow.SetSensitivity(newSens);
        curY += 36f;

        GUI.Label(new Rect(x + 20, curY, w - 40, 20), "Monitor:", UITheme.LabelStyle);
        curY += 22f;
        var displays = new List<DisplayInfo>();
        Screen.GetDisplayLayout(displays);
        for (int i = 0; i < displays.Count; i++)
        {
            DisplayInfo info = displays[i];
            if (GUI.Button(new Rect(x + 20 + i * 90, curY, 80, 28), "Monitor " + (i + 1), UITheme.ButtonStyle))
                Screen.MoveMainWindowTo(in info, Vector2Int.zero);
        }
        curY += 40f;

        GUI.Label(new Rect(x + 20, curY, w - 40, 20), "Auflösung:", UITheme.LabelStyle);
        curY += 22f;
        if (GUI.Button(new Rect(x + 20, curY, 110, 28), "1280x720", UITheme.ButtonStyle))
            SaveResolution(1280, 720);
        if (GUI.Button(new Rect(x + 140, curY, 110, 28), "1920x1080", UITheme.ButtonStyle))
            SaveResolution(1920, 1080);
        if (GUI.Button(new Rect(x + 260, curY, 100, 28), "2560x1440", UITheme.ButtonStyle))
            SaveResolution(2560, 1440);
        curY += 40f;

        bool fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
        bool newFullscreen = GUI.Toggle(new Rect(x + 20, curY, w - 40, 24), fullscreen, " Vollbild");
        if (newFullscreen != fullscreen)
        {
            Screen.fullScreenMode = newFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            PlayerPrefs.SetInt(FullscreenPrefKey, newFullscreen ? 1 : 0);
        }
        curY += 34f;

        GUI.Label(new Rect(x + 20, curY, w - 40, 20), "Grafikqualität:", UITheme.LabelStyle);
        curY += 22f;
        string[] qualityNames = QualitySettings.names;
        for (int i = 0; i < qualityNames.Length; i++)
        {
            if (GUI.Button(new Rect(x + 20 + i * 90, curY, 80, 28), qualityNames[i], UITheme.ButtonStyle))
            {
                QualitySettings.SetQualityLevel(i, true);
                PlayerPrefs.SetInt(QualityPrefKey, i);
            }
        }
        curY += 45f;

        if (GUI.Button(new Rect(x + 20, curY, w - 40, 32), "↩ Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            MainMenu.SetScreen(MenuScreen.Main);
        }

        GUI.color = prevColor;
    }
}
