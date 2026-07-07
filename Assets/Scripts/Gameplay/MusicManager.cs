using UnityEngine;

// Loops a single background track with a smooth crossfade at the loop point: as the track nears
// its end, a second AudioSource starts the clip from the beginning and the two are crossfaded, so
// the end flows seamlessly back into the start (no hard seam).
public class MusicManager : MonoBehaviour
{
    public AudioClip loopClip;
    public float volume = 0.4f;
    public float crossfade = 5f;

    // Separate, live-adjustable music volume (persisted), independent of SFX. Settings writes it.
    const string MusicPrefKey = "MusicVolume";
    static float musicScale = -1f;

    public static float MusicVolume
    {
        get
        {
            if (musicScale < 0f)
                musicScale = PlayerPrefs.GetFloat(MusicPrefKey, 1f);
            return musicScale;
        }
    }

    public static void SetMusicVolume(float value)
    {
        musicScale = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicPrefKey, musicScale);
    }

    // Effective output volume = base track volume * user music setting.
    private float Vol => volume * MusicVolume;

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
        active.volume = Vol;
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
            active.volume = Vol; // apply live music-volume changes while a single track plays
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
            active.volume = Vol * (1f - p);
            next.volume = Vol * p;

            if (p >= 1f)
            {
                active.Stop();
                active.volume = 0f;
                active = next;
                active.volume = Vol;
                crossfading = false;
            }
        }
    }
}
