using UnityEngine;

// Telegraphed pop-up spikes (Crash-Bandicoot style): they rise and retract on a shared timer, and
// only hurt while actually extended - so the danger is readable, you wait for them to drop and pass.
// Synced across clients via PlatformClock. Placed to the SIDE of platforms (not blocking the main
// path), so they add tension without unfair forced-timing deaths.
[DefaultExecutionOrder(-10)]
public class PopupSpike : MonoBehaviour
{
    public float upDuration = 1.0f;
    public float downDuration = 1.5f;
    public float riseHeight = 0.7f;
    public float phaseOffset = 0f;
    public float moveSpeed = 9f;

    private Vector3 downPos;
    private Vector3 upPos;
    private bool isUp;

    void Awake()
    {
        upPos = transform.position;
        downPos = transform.position - Vector3.up * riseHeight;
        transform.position = downPos;

        var col = GetComponentInChildren<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void Update()
    {
        float cycle = upDuration + downDuration;
        float t = Mathf.Repeat((float)PlatformClock.Time + phaseOffset, cycle);
        isUp = t < upDuration;
        Vector3 target = isUp ? upPos : downPos;
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other) => TryKill(other);
    void OnTriggerStay(Collider other) => TryKill(other);

    void TryKill(Collider other)
    {
        // Only lethal when genuinely extended (not mid-retract), so it's fair.
        if (!isUp || transform.position.y < upPos.y - riseHeight * 0.4f)
            return;
        var player = other.GetComponent<PlayerController>();
        if (player != null && player.IsOwner)
            player.Die();
    }
}
