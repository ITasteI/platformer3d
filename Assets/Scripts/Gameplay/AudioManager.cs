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
    public AudioClip bounceClip;
    public AudioClip whooshClip;
    public AudioClip victoryClip;

    private AudioSource source;

    // Separate SFX volume (music has its own), persisted and shared. Loaded lazily so it works
    // before the settings menu is opened.
    const string SfxPrefKey = "SfxVolume";
    static float sfxScale = -1f;

    public static float SfxVolume
    {
        get
        {
            if (sfxScale < 0f)
                sfxScale = PlayerPrefs.GetFloat(SfxPrefKey, 1f);
            return sfxScale;
        }
    }

    public static void SetSfxVolume(float value)
    {
        sfxScale = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxPrefKey, sfxScale);
    }

    void Awake()
    {
        Instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
    }

    void Play(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
            source.PlayOneShot(clip, volume * SfxVolume);
    }

    public void PlayJump() => Play(jumpClip, 0.8f);
    public void PlayLand() => Play(landClip, 0.7f);
    public void PlayCoin() => Play(coinClip, 0.9f);
    public void PlayDeath() => Play(deathClip, 0.9f);
    public void PlayCheckpoint() => Play(checkpointClip, 0.9f);
    public void PlayClick() => Play(clickClip, 0.6f);
    public void PlayBounce() => Play(bounceClip, 0.8f);
    public void PlayWhoosh() => Play(whooshClip, 0.6f);
    public void PlayVictory() => Play(victoryClip, 0.9f);
    // A soft positive chime when an ability finishes cooling down (reuses the checkpoint clip).
    public void PlayAbilityReady() => Play(checkpointClip, 0.45f);

    public void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0)
            return;
        Play(footstepClips[Random.Range(0, footstepClips.Length)], 0.35f);
    }
}
