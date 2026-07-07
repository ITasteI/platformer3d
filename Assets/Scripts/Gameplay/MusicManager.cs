using UnityEngine;

// Plays a single background track on loop. (Previously crossfaded three zone tracks by height;
// now simplified to one continuous loop as requested.)
public class MusicManager : MonoBehaviour
{
    public AudioClip loopClip;
    public float volume = 0.4f;

    private AudioSource source;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.volume = volume;
    }

    void Start()
    {
        if (loopClip != null)
        {
            source.clip = loopClip;
            source.Play();
        }
    }
}
