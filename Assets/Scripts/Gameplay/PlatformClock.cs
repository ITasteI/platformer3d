using Unity.Netcode;
using UnityEngine;

// A time source that every client agrees on, so procedurally-animated platforms
// (moving/floating/swinging) sit at the same position for everyone. When connected we use
// Netcode's synchronized ServerTime; offline we fall back to local time.
public static class PlatformClock
{
    public static float Time
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
                return (float)nm.ServerTime.Time;
            return UnityEngine.Time.time;
        }
    }
}
