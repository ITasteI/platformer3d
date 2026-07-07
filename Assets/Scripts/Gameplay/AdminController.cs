using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Developer-only test tools. Gated entirely behind AdminAuth.IsAdmin (a local secret key file),
// never the visible player name. Every action here only affects this client's own player -
// no networked admin authority exists, so a tampered client can't affect other players.
public class AdminController : MonoBehaviour
{
    public PlayerController player;

    private bool menuOpen;
    private bool flyMode;
    private bool invincible;
    private float speedMultiplier = 1f;
    private Vector2 scrollPos;
    private List<Checkpoint> checkpoints = new List<Checkpoint>();

    void Update()
    {
        if (!AdminAuth.IsAdmin || player == null || !player.IsOwner)
            return;

        if (Input.GetKeyDown(KeyCode.F1))
            menuOpen = !menuOpen;

        if (Input.GetKeyDown(KeyCode.F2))
            ToggleFly();
    }

    void ToggleFly()
    {
        flyMode = !flyMode;
        player.SetAdminFly(flyMode);
    }

    void ToggleInvincible()
    {
        invincible = !invincible;
        player.SetAdminInvincible(invincible);
    }

    void RefreshCheckpoints()
    {
        checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None)
            .OrderBy(c => c.transform.position.y)
            .ToList();
    }

    void OnGUI()
    {
        if (!AdminAuth.IsAdmin || player == null || !player.IsOwner)
            return;

        UITheme.EnsureInit();
        GUI.Label(new Rect(Screen.width - 170, 10, 160, 24), "ADMIN (F1)", UITheme.LabelStyle);

        if (!menuOpen)
            return;

        float w = 300f;
        float h = 380f;
        float x = Screen.width - w - 20f;
        float y = 40f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x + 10, y + 8, w - 20, 26), "Admin-Testmenü", UITheme.LabelStyle);
        float curY = y + 40f;

        if (GUI.Button(new Rect(x + 15, curY, w - 30, 30), flyMode ? "Fly Mode: AN (F2)" : "Fly Mode: AUS (F2)", UITheme.ButtonStyle))
            ToggleFly();
        curY += 36f;

        if (GUI.Button(new Rect(x + 15, curY, w - 30, 30), invincible ? "Unsterblich: AN" : "Unsterblich: AUS", UITheme.ButtonStyle))
            ToggleInvincible();
        curY += 36f;

        GUI.Label(new Rect(x + 15, curY, w - 30, 18), $"Geschwindigkeit: {speedMultiplier:0.0}x", UITheme.LabelStyle);
        curY += 18f;
        float newSpeed = GUI.HorizontalSlider(new Rect(x + 15, curY + 6, w - 30, 20), speedMultiplier, 0.5f, 4f);
        if (!Mathf.Approximately(newSpeed, speedMultiplier))
        {
            speedMultiplier = newSpeed;
            player.SetAdminSpeedMultiplier(speedMultiplier);
        }
        curY += 32f;

        if (GUI.Button(new Rect(x + 15, curY, w - 30, 28), "Respawn am Checkpoint", UITheme.ButtonStyle))
            player.AdminRespawn();
        curY += 34f;

        if (GUI.Button(new Rect(x + 15, curY, w - 30, 28), "Level neu laden", UITheme.ButtonStyle))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        curY += 36f;

        GUI.Label(new Rect(x + 15, curY, w - 30, 18), "Teleport zu Checkpoint:", UITheme.LabelStyle);
        curY += 20f;

        RefreshCheckpoints();
        float listHeight = y + h - curY - 12f;
        Rect viewRect = new Rect(x + 15, curY, w - 30, listHeight);
        Rect contentRect = new Rect(0, 0, w - 50, checkpoints.Count * 28f);
        scrollPos = GUI.BeginScrollView(viewRect, scrollPos, contentRect);
        for (int i = 0; i < checkpoints.Count; i++)
        {
            Vector3 pos = checkpoints[i].transform.position;
            if (GUI.Button(new Rect(0, i * 28f, w - 70, 24), $"CP {i + 1}  (Höhe {pos.y:0})", UITheme.ButtonStyle))
                player.AdminTeleport(pos + Vector3.up);
        }
        GUI.EndScrollView();
    }
}
