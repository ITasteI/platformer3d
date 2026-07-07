using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Persistent spendable currency + owned/equipped cosmetics. Stored in its own file, separate
// from SaveData (checkpoint progress) and ProfileData (username) - "Neues Spiel"/restart clears
// the run's progress but must never wipe purchased cosmetics or the coin wallet.
[System.Serializable]
class EconomyData
{
    public int coins;
    public int totalCoinsEarned;
    public List<string> ownedSkins = new List<string> { "standard" };
    public List<string> ownedEffects = new List<string> { "none" };
    public string equippedSkin = "standard";
    public string equippedEffect = "none";
}

public static class EconomySystem
{
    static string EconomyPath => Path.Combine(Application.persistentDataPath, "economy.json");
    static EconomyData cached;
    // Coins now respawn and can be farmed, so writing the file on every single pickup caused
    // synchronous disk I/O in the gameplay hot path. Instead we mark the wallet dirty and let a
    // driver (GameManager) flush it periodically / on quit. Purchases + equips still save at once.
    static bool dirty;

    public static int Coins => Data.coins;
    public static int TotalCoinsEarned => Data.totalCoinsEarned;
    public static string EquippedSkin => Data.equippedSkin;
    public static string EquippedEffect => Data.equippedEffect;

    static EconomyData Data
    {
        get
        {
            if (cached == null)
                Load();
            return cached;
        }
    }

    public static void AddCoins(int amount)
    {
        if (amount <= 0)
            return;
        Data.coins += amount;
        Data.totalCoinsEarned += amount;
        dirty = true;
    }

    // Persist the wallet to disk if it changed since the last write. Cheap no-op when clean, so
    // it's safe to call every few seconds and on application quit.
    public static void Flush()
    {
        if (dirty)
            Save();
    }

    public static bool IsOwned(string id, bool isSkin)
    {
        return (isSkin ? Data.ownedSkins : Data.ownedEffects).Contains(id);
    }

    public static bool TryPurchase(string id, int price, bool isSkin)
    {
        if (IsOwned(id, isSkin))
            return true;
        if (Data.coins < price)
            return false;

        Data.coins -= price;
        (isSkin ? Data.ownedSkins : Data.ownedEffects).Add(id);
        Save();
        return true;
    }

    public static void Equip(string id, bool isSkin)
    {
        if (!IsOwned(id, isSkin))
            return;

        if (isSkin)
            Data.equippedSkin = id;
        else
            Data.equippedEffect = id;
        Save();
    }

    static void Load()
    {
        try
        {
            cached = File.Exists(EconomyPath)
                ? JsonUtility.FromJson<EconomyData>(File.ReadAllText(EconomyPath))
                : new EconomyData();
        }
        catch
        {
            cached = new EconomyData();
        }

        if (cached == null)
            cached = new EconomyData();
    }

    static void Save()
    {
        File.WriteAllText(EconomyPath, JsonUtility.ToJson(cached, true));
        dirty = false;
    }
}
