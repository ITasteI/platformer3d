using System;
using System.IO;
using UnityEngine;

// Free-fly photo mode (toggle with P): detaches the camera, hides the HUD and freezes the player so
// you can frame and capture the night vistas. Screenshots save to a "Screenshots" folder next to the
// game. Great for sharing shots - the moonlit world is the game's best marketing.
public class PhotoMode : MonoBehaviour
{
    public static bool Active { get; private set; }

    public KeyCode toggleKey = KeyCode.P;
    public float moveSpeed = 12f;
    public float fastMultiplier = 3f;
    public float lookSpeed = 2.6f;

    private Camera cam;
    private CameraFollow follow;
    private float yaw, pitch;
    private bool hideHint;

    void Update()
    {
        // Opening the menu / winning always leaves photo mode.
        if (MainMenu.IsBlockingGameplay || WinScreen.HasWon)
        {
            if (Active)
                Exit();
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (Active) Exit(); else Enter();
        }
        if (!Active || cam == null)
            return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Capture();
        if (Input.GetKeyDown(KeyCode.H))
            hideHint = !hideHint;

        // Mouse look.
        yaw += Input.GetAxis("Mouse X") * lookSpeed;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSpeed, -89f, 89f);
        cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Free flight (unscaled so it works even if the game is time-frozen).
        float sp = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 dir = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        Vector3 move = cam.transform.rotation * dir;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move += Vector3.down;
        cam.transform.position += move * (sp * Time.unscaledDeltaTime);
    }

    void Enter()
    {
        cam = Camera.main;
        if (cam == null)
            return;
        follow = cam.GetComponent<CameraFollow>();
        if (follow != null)
            follow.enabled = false;

        Vector3 e = cam.transform.eulerAngles;
        pitch = e.x > 180f ? e.x - 360f : e.x;
        yaw = e.y;
        Active = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Exit()
    {
        Active = false;
        if (follow != null)
            follow.enabled = true;
        // Don't steal the cursor back if a menu opened at the same moment (Esc race).
        if (!MainMenu.IsBlockingGameplay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Capture()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"TasteJump_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path, 2); // 2x supersample for a crisp shot
        }
        catch { }
    }

    void OnGUI()
    {
        UITheme.BeginUI();
        if (!Active || hideHint)
            return;
        UITheme.EnsureInit();
        var s = new GUIStyle(UITheme.LabelStyle) { alignment = TextAnchor.MiddleCenter };
        s.normal.textColor = UITheme.Ink;
        var bg = new Rect(0, UITheme.ScreenH - 40f, UITheme.ScreenW, 28f);
        UITheme.Rect(bg, new Color(0f, 0f, 0f, 0.4f));
        GUI.Label(new Rect(0, UITheme.ScreenH - 38f, UITheme.ScreenW, 24f),
            "📷 Fotomodus  ·  WASD + Maus fliegen  ·  E/Q hoch/runter  ·  Shift schneller  ·  Enter = Foto  ·  H = Hinweis aus  ·  P/Esc = beenden", s);
    }
}
