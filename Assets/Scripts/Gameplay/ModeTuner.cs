using System.Collections.Generic;
using UnityEngine;

// Makes each MODE feel different on the SAME baked tower - the parkour itself changes character:
//   Klassisch  - baseline.
//   Zeitrennen - hectic: moving/swinging platforms 35% faster, timed platforms cycle quicker.
//   Endlos     - baseline pace (its identity is the 5x length).
//   Hardcore   - mean: crumbling platforms collapse much faster, dynamic platforms a bit quicker,
//                shooters fire faster and patrols sweep harder.
// Base values are cached the first time a component is touched, so switching modes is lossless.
public class ModeTuner : MonoBehaviour
{
    private GameMode applied = (GameMode)(-1);

    private readonly Dictionary<MovingPlatform, float> baseMove = new();
    private readonly Dictionary<SwingingPlatform, float> baseSwing = new();
    private readonly Dictionary<TimedPlatform, Vector2> baseTimed = new();
    private readonly Dictionary<CrumblingPlatform, float> baseCrumble = new();
    private readonly Dictionary<Enemy, Vector2> baseEnemy = new(); // (shootInterval, patrolSpeed)

    void Update()
    {
        if (GameModeState.Current == applied)
            return;
        Apply(GameModeState.Current);
    }

    void Apply(GameMode mode)
    {
        float moveF = 1f, timedF = 1f, crumbleF = 1f, shootF = 1f, patrolF = 1f;
        switch (mode)
        {
            case GameMode.Zeitrennen:
                moveF = 1.35f;
                timedF = 0.75f;
                break;
            case GameMode.Hardcore:
                moveF = 1.12f;
                timedF = 0.85f;
                crumbleF = 0.6f;
                shootF = 0.75f;
                patrolF = 1.3f;
                break;
        }

        // Include inactive objects so the (initially dormant) Endless chunks get tuned too.
        foreach (var p in Object.FindObjectsByType<MovingPlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!baseMove.TryGetValue(p, out float b)) baseMove[p] = b = p.speed;
            p.speed = b * moveF;
        }
        foreach (var p in Object.FindObjectsByType<SwingingPlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!baseSwing.TryGetValue(p, out float b)) baseSwing[p] = b = p.speed;
            p.speed = b * moveF;
        }
        foreach (var p in Object.FindObjectsByType<TimedPlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!baseTimed.TryGetValue(p, out Vector2 b)) baseTimed[p] = b = new Vector2(p.solidDuration, p.goneDuration);
            p.solidDuration = b.x * timedF;
            p.goneDuration = b.y * timedF;
        }
        foreach (var p in Object.FindObjectsByType<CrumblingPlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!baseCrumble.TryGetValue(p, out float b)) baseCrumble[p] = b = p.delayBeforeFall;
            p.delayBeforeFall = b * crumbleF;
        }
        foreach (var e in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!baseEnemy.TryGetValue(e, out Vector2 b)) baseEnemy[e] = b = new Vector2(e.shootInterval, e.patrolSpeed);
            e.shootInterval = b.x * shootF;
            e.patrolSpeed = b.y * patrolF;
        }

        applied = mode;
    }
}
