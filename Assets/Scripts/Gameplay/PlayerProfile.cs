using System.IO;
using System.Linq;
using UnityEngine;

// Player identity (display name) is intentionally stored separately from SaveData/progress:
// deleting the level-progress save ("Neues Spiel" / restart) must not also erase the name.
[System.Serializable]
class ProfileData
{
    public string displayName = "";
}

public static class PlayerProfile
{
    public const int MaxNameLength = 16;

    static string ProfilePath => Path.Combine(Application.persistentDataPath, "profile.json");
    static ProfileData cached;

    public static bool HasName => !string.IsNullOrEmpty(Name);

    public static string Name
    {
        get
        {
            if (cached == null)
                Load();
            return cached.displayName;
        }
    }

    public static bool TrySetName(string rawName, out string sanitized)
    {
        sanitized = Sanitize(rawName);
        if (sanitized.Length == 0)
            return false;

        if (cached == null)
            Load();
        cached.displayName = sanitized;
        Save();
        return true;
    }

    public static string Sanitize(string rawName)
    {
        if (string.IsNullOrEmpty(rawName))
            return "";

        string trimmed = rawName.Trim();
        string filtered = new string(trimmed.Where(c => !char.IsControl(c)).ToArray());
        if (filtered.Length > MaxNameLength)
            filtered = filtered.Substring(0, MaxNameLength);
        return filtered.Trim();
    }

    static void Load()
    {
        try
        {
            cached = File.Exists(ProfilePath)
                ? JsonUtility.FromJson<ProfileData>(File.ReadAllText(ProfilePath))
                : new ProfileData();
        }
        catch
        {
            cached = new ProfileData();
        }

        if (cached == null)
            cached = new ProfileData();
    }

    static void Save()
    {
        File.WriteAllText(ProfilePath, JsonUtility.ToJson(cached, true));
    }
}
