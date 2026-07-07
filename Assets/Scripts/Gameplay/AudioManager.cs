using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioClip[] footstepClips;
    public AudioClip jumpClip;
    public AudioClip landClip;
    public AudioClip coinClip;
    public AudioClip deathClip;
    public AudioClip checkpointClip;
    public AudioClip clickClip;

    private AudioSource source;

    void Awake()
    {
        Instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
    }

    void Play(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
            source.PlayOneShot(clip, volume);
    }

    public void PlayJump() => Play(jumpClip, 0.8f);
    public void PlayLand() => Play(landClip, 0.7f);
    public void PlayCoin() => Play(coinClip, 0.9f);
    public void PlayDeath() => Play(deathClip, 0.9f);
    public void PlayCheckpoint() => Play(checkpointClip, 0.9f);
    public void PlayClick() => Play(clickClip, 0.6f);

    public void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0)
            return;
        Play(footstepClips[Random.Range(0, footstepClips.Length)], 0.35f);
    }
}
