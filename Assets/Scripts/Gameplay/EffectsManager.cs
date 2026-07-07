using UnityEngine;

public class EffectsManager : MonoBehaviour
{
    public static EffectsManager Instance { get; private set; }

    public ParticleSystem dustTemplate;
    public ParticleSystem sparkleTemplate;

    void Awake()
    {
        Instance = this;
    }

    public void PlayDust(Vector3 pos) => Spawn(dustTemplate, pos);
    public void PlaySparkle(Vector3 pos) => Spawn(sparkleTemplate, pos);

    void Spawn(ParticleSystem template, Vector3 pos)
    {
        if (template == null)
            return;

        ParticleSystem instance = Instantiate(template, pos, Quaternion.identity);
        instance.gameObject.SetActive(true);
        instance.Play();
        Destroy(instance.gameObject, instance.main.duration + instance.main.startLifetime.constantMax + 0.5f);
    }
}
