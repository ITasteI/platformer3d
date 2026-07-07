using System.Collections;
using UnityEngine;

public class CrumblingPlatform : MonoBehaviour
{
    public float delayBeforeFall = 0.6f;
    public float shakeStrength = 0.08f;
    public float respawnDelay = 3f;

    private Vector3 startPos;
    private Collider solidCollider;
    private Renderer[] renderers;
    private bool triggered;

    void Awake()
    {
        startPos = transform.position;
        solidCollider = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>();

        BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        ColliderUtil.FitToRenderBounds(transform, trigger);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!triggered && other.CompareTag("Player"))
            StartCoroutine(CrumbleRoutine());
    }

    IEnumerator CrumbleRoutine()
    {
        triggered = true;
        float t = 0f;
        while (t < delayBeforeFall)
        {
            t += Time.deltaTime;
            transform.position = startPos + Random.insideUnitSphere * shakeStrength;
            yield return null;
        }

        transform.position = startPos;
        solidCollider.enabled = false;
        foreach (var r in renderers)
            r.enabled = false;

        yield return new WaitForSeconds(respawnDelay);

        // Show the platform again, but don't re-enable the solid collider while a player is
        // still standing in its volume - otherwise it would pop them upward / clip them.
        transform.position = startPos;
        foreach (var r in renderers)
            r.enabled = true;

        Bounds bounds = solidCollider.bounds;
        while (PlayerOverlaps(bounds))
            yield return null;

        solidCollider.enabled = true;
        triggered = false;
    }

    static bool PlayerOverlaps(Bounds bounds)
    {
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents + Vector3.up * 0.3f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.CompareTag("Player"))
                return true;
        return false;
    }
}
