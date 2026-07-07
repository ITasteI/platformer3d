using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Records the local player's path during a run and replays the best run as a translucent "ghost"
// that races alongside you. Because the tower is identical every run, the ghost's height at a given
// elapsed time is a direct "am I ahead of my best?" readout. Purely visual - never affects gameplay.
//
// Recording and playback are both indexed off GameManager.PlayTime (which already pauses in menus),
// so the two stay perfectly aligned without storing timestamps. Only a new best time overwrites the
// saved ghost.
public class GhostRunner : MonoBehaviour
{
    public static GhostRunner Instance { get; private set; }

    const string FileName = "ghost.json";
    const float SampleInterval = 0.15f;

    [System.Serializable]
    class GhostData
    {
        public float interval = SampleInterval;
        public List<Vector3> points = new List<Vector3>();
    }

    private readonly List<Vector3> recording = new List<Vector3>();
    private GhostData best;
    private GameObject ghost;
    private float lastPlayTime;

    static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // For the HUD pace indicator: is the ghost currently on screen, and how high is it?
    public bool GhostVisible => ghost != null && ghost.activeSelf;
    public float GhostHeight => ghost != null ? ghost.transform.position.y : 0f;

    void Awake()
    {
        Instance = this;
        best = Load();
        CreateGhostVisual();
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.player == null)
        {
            if (ghost != null) ghost.SetActive(false);
            return;
        }

        float playTime = GameManager.Instance.PlayTime;

        // A run reset (Neu starten / restart) rewinds PlayTime - drop the old recording so the next
        // run is captured fresh.
        if (playTime < lastPlayTime - 0.5f)
            recording.Clear();
        lastPlayTime = playTime;

        RecordUpToNow(playTime);
        DrivePlayback(playTime);
    }

    // Backfills recording so recording[i] corresponds to time i*interval. Keyed to PlayTime so it
    // pauses exactly when the run timer pauses.
    void RecordUpToNow(float playTime)
    {
        bool blocked = MainMenu.IsBlockingGameplay || WinScreen.HasWon || TutorialOverlay.IsVisible;
        if (blocked)
            return;

        Vector3 pos = GameManager.Instance.player.position;
        int targetCount = Mathf.FloorToInt(playTime / SampleInterval) + 1;
        while (recording.Count < targetCount)
            recording.Add(pos);
    }

    void DrivePlayback(float playTime)
    {
        if (ghost == null)
            return;

        bool blocked = MainMenu.IsBlockingGameplay || WinScreen.HasWon || TutorialOverlay.IsVisible;
        if (blocked || best == null || best.points.Count < 2)
        {
            ghost.SetActive(false);
            return;
        }

        float f = playTime / best.interval;
        int i = Mathf.FloorToInt(f);
        if (i < 0 || i >= best.points.Count - 1)
        {
            // Before the run starts or after the ghost already finished its best run.
            ghost.SetActive(false);
            return;
        }

        ghost.transform.position = Vector3.Lerp(best.points[i], best.points[i + 1], f - i);
        ghost.SetActive(true);
    }

    // Called by WinScreen when a run finishes; only a new best overwrites the saved ghost.
    public void OnWin(bool isNewBest)
    {
        if (!isNewBest || recording.Count < 2)
            return;

        best = new GhostData { interval = SampleInterval, points = new List<Vector3>(recording) };
        Save(best);
    }

    void CreateGhostVisual()
    {
        ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        ghost.name = "BestTimeGhost";
        var col = ghost.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        ghost.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);

        var rend = ghost.GetComponent<Renderer>();
        rend.material = CreateGhostMaterial();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        ghost.SetActive(false);
    }

    static Material CreateGhostMaterial()
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // Transparent surface setup for URP Lit.
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_Surface", 1f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        m.SetColor("_BaseColor", new Color(0.45f, 0.85f, 1f, 0.35f));
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", new Color(0.2f, 0.55f, 0.85f) * 0.7f);
        return m;
    }

    static GhostData Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;
            GhostData data = JsonUtility.FromJson<GhostData>(File.ReadAllText(FilePath));
            return (data != null && data.points != null && data.points.Count >= 2) ? data : null;
        }
        catch
        {
            return null;
        }
    }

    static void Save(GhostData data)
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data));
        }
        catch { }
    }
}
