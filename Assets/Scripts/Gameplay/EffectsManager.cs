using System.Collections.Generic;
using UnityEngine;

// Spawns one-shot burst effects (dust on jump/land/dash, sparkle on coin/checkpoint). These fire
// constantly during play, so instead of Instantiate+Destroy per effect (which churned the GC on
// every single jump) we keep a small reusable pool per template and replay finished instances.
public class EffectsManager : MonoBehaviour
{
    public static EffectsManager Instance { get; private set; }

    public ParticleSystem dustTemplate;
    public ParticleSystem sparkleTemplate;

    private readonly Dictionary<ParticleSystem, List<ParticleSystem>> pools =
        new Dictionary<ParticleSystem, List<ParticleSystem>>();

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

        ParticleSystem instance = GetFree(template);
        instance.transform.position = pos;
        instance.Clear();
        instance.Play();
    }

    // Returns a finished (idle) pooled instance for this template, growing the pool by one if all
    // current instances are still playing. The pool size settles at the peak concurrent count.
    ParticleSystem GetFree(ParticleSystem template)
    {
        if (!pools.TryGetValue(template, out var list))
        {
            list = new List<ParticleSystem>();
            pools[template] = list;
        }

        foreach (var ps in list)
        {
            if (ps != null && !ps.IsAlive(true))
                return ps;
        }

        ParticleSystem created = Instantiate(template);
        created.gameObject.SetActive(true);
        created.Stop();
        list.Add(created);
        return created;
    }
}
