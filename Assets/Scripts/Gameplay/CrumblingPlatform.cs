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

        transform.position = startPos;
        solidCollider.enabled = true;
        foreach (var r in renderers)
            r.enabled = true;
        triggered = false;
    }
}
