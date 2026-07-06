using System.Collections.Generic;
using UnityEngine;

public class SettingsMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetOpen(!IsOpen);

        if (IsOpen && Input.GetMouseButtonDown(1))
            SetOpen(false);
    }

    void SetOpen(bool open)
    {
        IsOpen = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }

    void OnGUI()
    {
        if (!IsOpen)
            return;

        float w = 380f;
        float h = 340f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "Einstellungen");
        float curY = y + 35f;

        GUI.Label(new Rect(x + 20, curY, w - 40, 20), "Monitor:");
        curY += 22f;
        var displays = new List<DisplayInfo>();
        Screen.GetDisplayLayout(displays);
        for (int i = 0; i < displays.Count; i++)
        {
            DisplayInfo info = displays[i];
            if (GUI.Button(new Rect(x + 20 + i * 90, curY, 80, 28), "Monitor " + (i + 1)))
                Screen.MoveMainWindowTo(in info, Vector2Int.zero);
        }
        curY += 40f;

        GUI.Label(new Rect(x + 20, curY, w - 40, 20), "Auflösung:");
        curY += 22f;
        if (GUI.Button(new Rect(x + 20, curY, 110, 28), "1280x720"))
            Screen.SetResolution(1280, 720, Screen.fullScreenMode);
        if (GUI.Button(new Rect(x + 140, curY, 110, 28), "1920x1080"))
            Screen.SetResolution(1920, 1080, Screen.fullScreenMode);
        if (GUI.Button(new Rect(x + 260, curY, 100, 28), "2560x1440"))
            Screen.SetResolution(2560, 1440, Screen.fullScreenMode);
        curY += 40f;

        bool fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
        bool newFullscreen = GUI.Toggle(new Rect(x + 20, curY, w - 40, 24), fullscreen, " Vollbild");
        if (newFullscreen != fullscreen)
            Screen.fullScreenMode = newFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        curY += 34f;

        GUI.Label(new Rect(x + 20, curY, w - 40, 20), "Grafikqualität:");
        curY += 22f;
        string[] qualityNames = QualitySettings.names;
        for (int i = 0; i < qualityNames.Length; i++)
        {
            if (GUI.Button(new Rect(x + 20 + i * 90, curY, 80, 28), qualityNames[i]))
                QualitySettings.SetQualityLevel(i, true);
        }
        curY += 45f;

        if (GUI.Button(new Rect(x + 20, curY, w - 40, 32), "Weiter spielen"))
            SetOpen(false);
    }
}
