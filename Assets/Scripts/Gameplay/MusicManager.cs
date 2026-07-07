using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public Transform player;
    public AudioClip lowZoneClip;
    public AudioClip midZoneClip;
    public AudioClip highZoneClip;
    public float topHeight = 500f;
    public float fadeSpeed = 0.5f;
    public float volume = 0.4f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioClip currentClip;

    void Awake()
    {
        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();
        sourceA.loop = true;
        sourceB.loop = true;
        sourceA.spatialBlend = 0f;
        sourceB.spatialBlend = 0f;
        sourceA.volume = 0f;
        sourceB.volume = 0f;
    }

    void Update()
    {
        if (player == null && GameManager.Instance != null)
            player = GameManager.Instance.player;

        if (player == null)
            return;

        float t = Mathf.Clamp01(player.position.y / topHeight);
        AudioClip target = t < 0.33f ? lowZoneClip : (t < 0.66f ? midZoneClip : highZoneClip);

        if (target != currentClip)
        {
            currentClip = target;
            AudioSource fadeIn = sourceA.isPlaying ? sourceB : sourceA;
            fadeIn.clip = target;
            fadeIn.Play();
        }

        AudioSource active = currentClip == sourceA.clip && sourceA.isPlaying ? sourceA : sourceB;
        AudioSource inactive = active == sourceA ? sourceB : sourceA;

        active.volume = Mathf.MoveTowards(active.volume, volume, fadeSpeed * Time.deltaTime);
        inactive.volume = Mathf.MoveTowards(inactive.volume, 0f, fadeSpeed * Time.deltaTime);
    }
}
