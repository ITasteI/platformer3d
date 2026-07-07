using UnityEngine;

// Loops a single background track with a smooth crossfade at the loop point: as the track nears
// its end, a second AudioSource starts the clip from the beginning and the two are crossfaded, so
// the end flows seamlessly back into the start (no hard seam).
public class MusicManager : MonoBehaviour
{
    public AudioClip loopClip;
    public float volume = 0.4f;
    public float crossfade = 5f;

    private AudioSource a;
    private AudioSource b;
    private AudioSource active;
    private bool crossfading;

    void Awake()
    {
        a = gameObject.AddComponent<AudioSource>();
        b = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { a, b })
        {
            s.loop = false;
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            s.volume = 0f;
        }
        active = a;
    }

    void Start()
    {
        if (loopClip == null)
            return;
        active.clip = loopClip;
        active.volume = volume;
        active.time = 0f;
        active.Play();
    }

    void Update()
    {
        if (loopClip == null || active == null)
            return;

        float fade = Mathf.Min(crossfade, loopClip.length * 0.4f);

        if (!crossfading)
        {
            float remaining = loopClip.length - active.time;
            if (active.isPlaying && remaining <= fade)
            {
                AudioSource next = active == a ? b : a;
                next.clip = loopClip;
                next.time = 0f;
                next.volume = 0f;
                next.Play();
                crossfading = true;
            }
        }
        else
        {
            AudioSource next = active == a ? b : a;
            float p = Mathf.Clamp01(next.time / fade);
            active.volume = volume * (1f - p);
            next.volume = volume * p;

            if (p >= 1f)
            {
                active.Stop();
                active.volume = 0f;
                active = next;
                active.volume = volume;
                crossfading = false;
            }
        }
    }
}
