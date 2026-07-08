using System.Collections;
using UnityEngine;

// The three wraith kinds. Wraith = melee guard (bobs in place, resets you on touch). Patrol =
// circles its platform, forcing you to time your landing. Shooter = lobs slow, dodgeable bolts,
// forcing you to keep moving while you line up your own shot.
public enum EnemyKind { Wraith, Patrol, Shooter }

// A fightable shadow wraith: 40 HP, killed in 2 arrow hits (20 dmg each) - or ONE headshot (top of
// the body = double damage). It threatens on contact (resets you, on a cooldown), punches its scale
// when hit, and bursts into a coin reward when defeated - then RESPAWNS after 5 minutes. Health
// bars are drawn centrally by GameManager (one pass for all enemies instead of ~115 OnGUI calls).
public class Enemy : MonoBehaviour
{
    public EnemyKind kind = EnemyKind.Wraith;
    public float maxHealth = 40f; // 2 arrow hits (20 dmg each) = dead; 1 headshot (x2) = dead
    public int coinReward = 12;
    public float contactCooldown = 1.2f;
    public float respawnSeconds = 300f; // 5 minutes

    [Header("Patrol")]
    public float patrolRadius = 2.2f;
    public float patrolSpeed = 1.2f;

    [Header("Shooter")]
    public float shootRange = 24f;
    public float shootInterval = 3f;
    public float boltSpeed = 7.5f;

    [Header("Health Bar")]
    public float barWidth = 58f;   // bosses get a wider bar
    public float barLift = 1.7f;   // world-space height of the bar above the base

    private float health;
    private float punch;
    private float lastContact = -99f;
    private Vector3 homePos;
    private float phase;
    private bool dead;
    private Renderer[] renderers;
    private Collider col;
    private int syncKey; // deterministic co-op identity (see EnemySync)
    private float shootTimer = 1.5f;
    private Vector3 baseScale = Vector3.one; // bosses are scaled up; punch must respect that

    public bool IsDead => dead;
    public float HealthFrac => Mathf.Clamp01(health / maxHealth);

    void Awake()
    {
        health = maxHealth;
        homePos = transform.position;
        phase = Random.value * 6.2831f;
        renderers = GetComponentsInChildren<Renderer>();
        col = GetComponent<Collider>();
        syncKey = EnemySync.KeyFor(homePos);
        EnemySync.Register(syncKey, this);
        baseScale = transform.localScale;
    }

    void OnDestroy() => EnemySync.Unregister(syncKey);

    void Update()
    {
        if (dead)
            return;

        var player = GameManager.Instance != null ? GameManager.Instance.player : null;

        // Perf: far-away wraiths sleep (the night fog hides them anyway) - with the Endless tower
        // there are ~115 of them, and their bobbing/turning is pure waste beyond ~70 m.
        if (player != null && (transform.position - player.position).sqrMagnitude > 4900f)
            return;

        if (kind == EnemyKind.Patrol)
        {
            // Circle the home point so the wraith sweeps across its platform - landing needs timing.
            float a = Time.time * patrolSpeed + phase;
            Vector3 p = homePos + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * patrolRadius;
            p.y = homePos.y + Mathf.Sin(Time.time * 1.6f + phase) * 0.18f;
            transform.position = p;
            Vector3 travel = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(travel), 6f * Time.deltaTime);
        }
        else
        {
            Vector3 p = homePos;
            p.y += Mathf.Sin(Time.time * 1.6f + phase) * 0.28f;
            transform.position = p;

            if (player != null)
            {
                Vector3 to = player.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.02f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 3f * Time.deltaTime);
            }
        }

        // Shooter: lob a slow, dodgeable bolt at the player while they're in range.
        if (kind == EnemyKind.Shooter && player != null)
        {
            shootTimer -= Time.deltaTime;
            if (shootTimer <= 0f
                && Vector3.Distance(player.position, transform.position) < shootRange
                && !MainMenu.IsBlockingGameplay && !WinScreen.HasWon && !PhotoMode.Active)
            {
                shootTimer = shootInterval;
                punch = Mathf.Max(punch, 0.7f); // recoil doubles as the fire telegraph
                AudioManager.Instance?.PlayWhoosh();
                Vector3 origin = transform.position + Vector3.up * 0.9f + transform.forward * 0.5f;
                EnemyProjectile.Spawn(origin, player.position + Vector3.up * 1.0f, boltSpeed);
            }
        }

        if (punch > 0f)
            punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 3.5f);
        transform.localScale = baseScale * (1f + punch * 0.28f);
    }

    void OnTriggerEnter(Collider other) => TryContact(other);
    void OnTriggerStay(Collider other) => TryContact(other);

    void TryContact(Collider other)
    {
        if (dead || Time.time - lastContact < contactCooldown)
            return;
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc.IsOwner)
        {
            lastContact = Time.time;
            pc.Die();
        }
    }

    // Called by an arrow that hits this enemy. Hits on the upper body (the head with the glowing
    // eyes) count as HEADSHOTS and deal double damage - one clean headshot kills.
    public void TakeDamage(float dmg, Vector3 hitPoint)
    {
        if (dead || health <= 0f)
            return;

        bool headshot = hitPoint.y > transform.position.y + 0.8f;
        if (headshot)
        {
            dmg *= 2f;
            punch = 1.5f;
            EffectsManager.Instance?.PlaySparkle(hitPoint);
            AudioManager.Instance?.PlayAbilityReady(); // bright chime = "that was a headshot"
        }
        else
        {
            punch = 1f;
            AudioManager.Instance?.PlayWhoosh();
        }

        health -= dmg;
        EffectsManager.Instance?.PlaySparkle(transform.position + Vector3.up * 0.8f);
        if (health <= 0f)
            Die();
    }

    void Die()
    {
        dead = true;
        EffectsManager.Instance?.PlaySparkle(transform.position + Vector3.up * 0.6f);
        EffectsManager.Instance?.PlayDust(transform.position);
        GameManager.Instance?.AddCoins(coinReward);
        EconomySystem.AddCoins(coinReward);
        GameManager.Instance?.RegisterKill(); // feeds the daily challenge
        AudioManager.Instance?.PlayCoin();
        // The summit GUARDIANS (boss-sized, 200+ HP) have their own achievement.
        if (maxHealth >= 200f)
            MilestoneTracker.Instance?.Unlock("ms_boss", "Gipfelwächter besiegt!");
        SetVisible(false);
        StartCoroutine(RespawnRoutine());
        // Tell the other co-op peers so this wraith dies on their side too (no ghost enemies).
        EnemySync.NotifyKilled(syncKey);
    }

    // A peer killed this enemy: mirror the death WITHOUT paying the coin reward again here.
    public void DieRemote()
    {
        if (dead)
            return;
        dead = true;
        health = 0f;
        EffectsManager.Instance?.PlaySparkle(transform.position + Vector3.up * 0.6f);
        EffectsManager.Instance?.PlayDust(transform.position);
        SetVisible(false);
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnSeconds);
        health = maxHealth;
        transform.position = homePos;
        transform.localScale = baseScale;
        dead = false;
        SetVisible(true);
        EnemySync.MarkRespawned(syncKey);
    }

    void SetVisible(bool v)
    {
        if (col != null)
            col.enabled = v;
        if (renderers != null)
            foreach (var r in renderers)
                if (r != null)
                    r.enabled = v;
    }

    // Drawn by GameManager in ONE pass for all enemies (a per-enemy OnGUI cost real CPU at ~115
    // enemies). GameManager already guards menu/win/photo states before calling this.
    public void DrawHealthBar(Camera cam)
    {
        if (dead || health <= 0f)
            return;
        Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * barLift);
        if (sp.z <= 0.5f || sp.z > 45f)
            return;

        // Camera pixels -> the scaled UI canvas (GameManager's OnGUI runs under UITheme.BeginUI).
        sp /= UITheme.UIScale;

        float bw = barWidth;
        const float bh = 7f;
        float sx = sp.x - bw * 0.5f;
        float sy = UITheme.ScreenH - sp.y;
        UITheme.Rect(new Rect(sx - 1.5f, sy - 1.5f, bw + 3f, bh + 3f), new Color(0f, 0f, 0f, 0.6f));
        UITheme.Rect(new Rect(sx, sy, bw, bh), new Color(0.22f, 0.06f, 0.08f, 0.95f));
        UITheme.Rect(new Rect(sx, sy, bw * HealthFrac, bh), new Color(0.96f, 0.26f, 0.32f));
    }
}
