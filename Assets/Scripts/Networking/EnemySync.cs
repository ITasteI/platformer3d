using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Keeps enemy DEATHS in sync across co-op peers without turning every baked enemy into a
// NetworkObject: enemies register under a deterministic position-derived key; whoever kills one
// tells the server, the server applies it, forwards it to everyone else, and replays the current
// dead-list to players who join later. Partial HP is deliberately NOT synced - a half-hurt wraith
// nobody notices, a dead one walking around ("ghost enemy") everybody does.
public static class EnemySync
{
    const string MsgName = "TJ_EnemyKilled";

    static readonly Dictionary<int, Enemy> enemies = new Dictionary<int, Enemy>();
    static readonly HashSet<int> deadKeys = new HashSet<int>(); // server-side, for late joiners
    static bool registered;

    // The baked tower is identical on every peer, so an enemy's home position is a stable ID.
    public static int KeyFor(Vector3 homePos)
    {
        return Mathf.RoundToInt(homePos.x * 4f) * 73856093
             ^ Mathf.RoundToInt(homePos.y * 4f) * 19349663
             ^ Mathf.RoundToInt(homePos.z * 4f) * 83492791;
    }

    public static void Register(int key, Enemy enemy) => enemies[key] = enemy;

    // For the central health-bar drawer (GameManager draws all bars in one OnGUI pass).
    public static Dictionary<int, Enemy>.ValueCollection AllEnemies => enemies.Values;

    public static void Unregister(int key)
    {
        enemies.Remove(key);
        deadKeys.Remove(key);
    }

    // Server bookkeeping: a respawned enemy is alive again for late joiners too.
    public static void MarkRespawned(int key) => deadKeys.Remove(key);

    // Called every frame by GameManager - registers the message handler once a session is live,
    // and re-arms itself when the session ends so the next one registers again.
    public static void EnsureRegistered()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            registered = false;
            return;
        }
        if (registered || nm.CustomMessagingManager == null)
            return;
        registered = true;
        nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgName, OnMessage);
        if (nm.IsServer)
            nm.OnClientConnectedCallback += OnClientConnected;
    }

    static void OnClientConnected(ulong clientId)
    {
        // Replay every currently-dead enemy to the newcomer so they don't meet ghosts.
        foreach (int key in deadKeys)
            Send(key, clientId);
    }

    // A LOCAL kill happened (already applied + rewarded locally): spread the word.
    public static void NotifyKilled(int key)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
            return;

        if (nm.IsServer)
        {
            deadKeys.Add(key);
            foreach (ulong id in nm.ConnectedClientsIds)
                if (id != nm.LocalClientId)
                    Send(key, id);
        }
        else
        {
            Send(key, NetworkManager.ServerClientId);
        }
    }

    static void Send(int key, ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        using var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
        writer.WriteValueSafe(key);
        nm.CustomMessagingManager.SendNamedMessage(MsgName, clientId, writer, NetworkDelivery.ReliableSequenced);
    }

    static void OnMessage(ulong senderClientId, FastBufferReader payload)
    {
        payload.ReadValueSafe(out int key);

        // Mirror the death locally (no coin reward here - the killer already got it).
        if (enemies.TryGetValue(key, out Enemy enemy) && enemy != null)
            enemy.DieRemote();

        // The server relays a client's kill to everyone else.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer)
        {
            deadKeys.Add(key);
            foreach (ulong id in nm.ConnectedClientsIds)
                if (id != nm.LocalClientId && id != senderClientId)
                    Send(key, id);
        }
    }
}
