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
    public ParticleSystem jumpRingTemplate;

    private readonly Dictionary<ParticleSystem, List<ParticleSystem>> pools =
        new Dictionary<ParticleSystem, List<ParticleSystem>>();

    // Equipped action-effect tint (shop cosmetic slot 3): null = default template colours.
    public static Color? FxTint { get; private set; }

    public static void RefreshFxTint()
    {
        string id = EconomySystem.EquippedActionFx;
        if (id == "none" || !CosmeticsCatalog.TryGetActionFx(id, out ActionFxDef fx))
            FxTint = null;
        else
            FxTint = fx.Color;
    }

    void Awake()
    {
        Instance = this;
        RefreshFxTint();
    }

    public void PlayDust(Vector3 pos) => Spawn(dustTemplate, pos);
    public void PlaySparkle(Vector3 pos) => Spawn(sparkleTemplate, pos);

    // Expanding ring puff at the feet when the mid-air (second) jump fires - a clear, satisfying tell.
    public void PlayJumpRing(Vector3 pos) => Spawn(jumpRingTemplate, pos);

    // Landing dust whose size/speed scales with impact speed, so a hard fall kicks up a real poof
    // while a gentle step barely puffs.
    public void PlayLandingDust(Vector3 pos, float impactSpeed)
    {
        if (dustTemplate == null)
            return;
        // Only real drops kick up dust, and even a hard landing stays subtle (the big poof read as
        // distracting). Gentle steps barely puff.
        float s = Mathf.Clamp01((impactSpeed - 9f) / 22f);
        ParticleSystem inst = GetFree(dustTemplate);
        inst.transform.position = pos;
        ApplyTint(inst, dustTemplate); // colour first - the size below must not be overridden
        var main = inst.main;
        main.startSizeMultiplier = (0.45f + s * 0.6f) * (FxTint.HasValue ? TintedFxScale : 1f);
        main.startSpeedMultiplier = 0.6f + s * 0.5f;
        inst.Clear();
        inst.Play();
    }

    void Spawn(ParticleSystem template, Vector3 pos)
    {
        if (template == null)
            return;

        ParticleSystem instance = GetFree(template);
        instance.transform.position = pos;
        ApplyTint(instance, template);
        instance.Clear();
        instance.Play();
    }

    // Tinted cosmetic effects are EXTREMELY small and translucent - a hint of colour per action,
    // never a bright cloud (full-size coloured puffs read as annoying). Untinted instances restore
    // the template defaults, so pooled instances never keep a stale tint/size after unequipping.
    const float TintedFxScale = 0.35f;

    static void ApplyTint(ParticleSystem instance, ParticleSystem template)
    {
        var main = instance.main;
        if (FxTint.HasValue)
        {
            Color c = FxTint.Value;
            c.a = 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(c);
            main.startSizeMultiplier = template.main.startSizeMultiplier * TintedFxScale;
        }
        else
        {
            main.startColor = template.main.startColor;
            main.startSizeMultiplier = template.main.startSizeMultiplier;
        }
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
