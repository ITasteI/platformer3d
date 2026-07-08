using System.Collections;
using UnityEngine;

// FOUR parkours are baked into the scene - one per game mode - each as a root full of activation
// chunks. This component watches GameModeState and switches to the current mode's tower: chunks
// are toggled a few per frame (no one-frame hitch), except for ForceApply (save-restore needs the
// ground to exist immediately). It also feeds the mode's summit height to the HUD/zone logic.
public class ModeWorld : MonoBehaviour
{
    public GameObject klassischRoot, zeitrennenRoot, endlosRoot, hardcoreRoot;
    public float klassischTop, zeitrennenTop, endlosTop, hardcoreTop;

    bool applied;
    GameMode lastMode;
    Coroutine toggling;

    GameObject RootFor(GameMode m) => m switch
    {
        GameMode.Zeitrennen => zeitrennenRoot,
        GameMode.Endlos => endlosRoot,
        GameMode.Hardcore => hardcoreRoot,
        _ => klassischRoot,
    };

    float TopFor(GameMode m) => m switch
    {
        GameMode.Zeitrennen => zeitrennenTop,
        GameMode.Endlos => endlosTop,
        GameMode.Hardcore => hardcoreTop,
        _ => klassischTop,
    };

    void Start() => Apply(false);

    void Update()
    {
        if (!applied || GameModeState.Current != lastMode)
            Apply(false);

        // The HUD's zone bar / splits key off the ACTIVE tower's summit (GameManager may spawn
        // after us, so keep this in sync cheaply every frame).
        if (GameManager.Instance != null)
            GameManager.Instance.topHeight = TopFor(GameModeState.Current);
    }

    // Save-restore calls this before teleporting onto a checkpoint - must be synchronous.
    public void ForceApply() => Apply(true);

    void Apply(bool immediate)
    {
        GameObject target = RootFor(GameModeState.Current);

        if (toggling != null)
        {
            StopCoroutine(toggling);
            toggling = null;
        }

        if (immediate || !isActiveAndEnabled)
        {
            SetTower(klassischRoot, klassischRoot == target);
            SetTower(zeitrennenRoot, zeitrennenRoot == target);
            SetTower(endlosRoot, endlosRoot == target);
            SetTower(hardcoreRoot, hardcoreRoot == target);
        }
        else
        {
            toggling = StartCoroutine(ToggleRoutine(target));
        }

        lastMode = GameModeState.Current;
        applied = true;
    }

    static void SetTower(GameObject root, bool on)
    {
        if (root == null)
            return;
        foreach (Transform child in root.transform)
            if (child.gameObject.activeSelf != on)
                child.gameObject.SetActive(on);
    }

    IEnumerator ToggleRoutine(GameObject target)
    {
        // Deactivate the other towers first (cheap), then bring the target up chunk by chunk.
        GameObject[] all = { klassischRoot, zeitrennenRoot, endlosRoot, hardcoreRoot };
        foreach (var root in all)
            if (root != null && root != target)
                SetTower(root, false);

        if (target != null)
        {
            const int perFrame = 2;
            int done = 0;
            foreach (Transform child in target.transform)
            {
                if (child.gameObject.activeSelf)
                    continue;
                child.gameObject.SetActive(true);
                if (++done >= perFrame)
                {
                    done = 0;
                    yield return null;
                }
            }
        }
        toggling = null;
    }
}
