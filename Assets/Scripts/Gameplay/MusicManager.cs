using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public Transform player;
    public AudioClip lowZoneClip;
    public AudioClip midZoneClip;
    public AudioClip highZoneClip;
    public float topHeight = 500f;
    public float fadeSpeed = 0.6f;
    public float volume = 0.4f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;   // currently fading toward full volume
    private AudioClip targetClip;

    void Awake()
    {
        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { sourceA, sourceB })
        {
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
        }
        activeSource = sourceA;
    }

    // Three tracks mapped across the five visual zones so the music changes roughly where the
    // world does: low = Wiese+Vulkan, mid = Wolken, high = Eis+Sterne.
    AudioClip ClipForHeight(float t)
    {
        if (t < 0.4f) return lowZoneClip;
        if (t < 0.6f) return midZoneClip;
        return highZoneClip;
    }

    void Update()
    {
        if (player == null && GameManager.Instance != null)
            player = GameManager.Instance.player;
        if (player == null)
            return;

        float t = Mathf.Clamp01(player.position.y / topHeight);
        AudioClip desired = ClipForHeight(t);

        if (desired != targetClip)
        {
            targetClip = desired;
            // Swap: the currently-active source becomes the one fading out, the other fades in.
            activeSource = activeSource == sourceA ? sourceB : sourceA;
            activeSource.clip = desired;
            if (desired != null)
                activeSource.Play();
        }

        AudioSource fadingOut = activeSource == sourceA ? sourceB : sourceA;
        activeSource.volume = Mathf.MoveTowards(activeSource.volume, targetClip != null ? volume : 0f, fadeSpeed * Time.deltaTime);
        fadingOut.volume = Mathf.MoveTowards(fadingOut.volume, 0f, fadeSpeed * Time.deltaTime);
        if (fadingOut.volume <= 0.001f && fadingOut.isPlaying)
            fadingOut.Stop();
    }
}
