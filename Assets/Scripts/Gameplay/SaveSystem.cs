using System.IO;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public Vector3 checkpointPosition;
    public int coinCount;
    public float bestTime = -1f;            // Klassisch best (legacy field name so old saves load)
    public float bestTimeZeitrennen = -1f;  // Zeitrennen best - the modes no longer share a record
    public float bestTimeHardcore = -1f;    // Hardcore best (one-life runs)
    // (GameMode) the run was saved in. Restored on resume - an Endless checkpoint above the base
    // tower only has ground under it when the Endless extension is active. Old saves default to 0
    // (Klassisch), which matches their behaviour before modes existed.
    public int gameMode;
    // Whether an actual run (checkpoint) is stored. "Neues Spiel" clears the run but keeps the
    // records above, so the file can exist with hasRun=false.
    public bool hasRun;
}

public static class SaveSystem
{
    static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    // True when a resumable RUN exists (not just stored records).
    public static bool HasSave()
    {
        SaveData d = Load();
        return d != null && d.hasRun;
    }

    public static SaveData Load()
    {
        if (!File.Exists(SavePath))
            return null;

        try
        {
            SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            // Migration: saves from before hasRun existed - a stored checkpoint means a run.
            if (d != null && !d.hasRun && d.checkpointPosition != Vector3.zero)
                d.hasRun = true;
            return d;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveCheckpoint(Vector3 position, int coinCount)
    {
        SaveData data = Load() ?? new SaveData();
        data.checkpointPosition = position;
        data.coinCount = coinCount;
        data.gameMode = (int)GameModeState.Current;
        data.hasRun = true;
        Write(data);
    }

    // Keeps the saved run's mode in sync when the player switches modes in the lobby while a
    // save exists (otherwise resume would silently flip back to the previously saved mode).
    public static void UpdateSavedMode()
    {
        SaveData data = Load();
        if (data == null)
            return;
        data.gameMode = (int)GameModeState.Current;
        Write(data);
    }

    // Per-mode best completion time (-1 = none yet). Endless has no completion, so it maps to
    // the height record in GameModeState instead.
    public static float BestTime(GameMode mode)
    {
        SaveData d = Load();
        if (d == null)
            return -1f;
        switch (mode)
        {
            case GameMode.Zeitrennen: return d.bestTimeZeitrennen;
            case GameMode.Hardcore: return d.bestTimeHardcore;
            default: return d.bestTime;
        }
    }

    public static void SaveBestTime(GameMode mode, float time)
    {
        SaveData data = Load() ?? new SaveData();
        if (mode == GameMode.Zeitrennen)
        {
            if (data.bestTimeZeitrennen < 0f || time < data.bestTimeZeitrennen)
                data.bestTimeZeitrennen = time;
        }
        else if (mode == GameMode.Hardcore)
        {
            if (data.bestTimeHardcore < 0f || time < data.bestTimeHardcore)
                data.bestTimeHardcore = time;
        }
        else if (data.bestTime < 0f || time < data.bestTime)
        {
            data.bestTime = time;
        }
        Write(data);
    }

    // "Neues Spiel": clears the RUN (checkpoint/coins/mode) but never wipes the best-time records.
    public static void DeleteSave()
    {
        SaveData data = Load();
        if (data == null)
            return;
        Write(new SaveData
        {
            bestTime = data.bestTime,
            bestTimeZeitrennen = data.bestTimeZeitrennen,
            bestTimeHardcore = data.bestTimeHardcore,
        });
    }

    static void Write(SaveData data)
    {
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
    }
}
