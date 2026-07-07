using System.IO;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public Vector3 checkpointPosition;
    public int coinCount;
    public float bestTime = -1f;
}

public static class SaveSystem
{
    static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    public static bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public static SaveData Load()
    {
        if (!HasSave())
            return null;

        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
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
        Write(data);
    }

    public static void SaveBestTime(float time)
    {
        SaveData data = Load() ?? new SaveData();
        if (data.bestTime < 0f || time < data.bestTime)
            data.bestTime = time;
        Write(data);
    }

    public static void DeleteSave()
    {
        if (HasSave())
            File.Delete(SavePath);
    }

    static void Write(SaveData data)
    {
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
    }
}
