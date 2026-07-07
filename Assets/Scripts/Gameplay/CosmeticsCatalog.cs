using UnityEngine;

public struct SkinDef
{
    public string Id;
    public string Name;
    public int Price;
    public Color BaseColor;
    public bool HasEmission;
    public Color EmissionColor;
}

public struct EffectDef
{
    public string Id;
    public string Name;
    public int Price;
    public Color ColorA;
    public Color ColorB;

    // Per-effect look so each one is visually distinct (trail shape + particle behaviour).
    public float TrailWidth;      // 0 = no trail
    public float TrailTime;       // trail length in seconds
    public float ParticleRate;    // 0 = no particles
    public float ParticleSize;
    public float ParticleSpeed;
    public float ParticleGravity; // negative = rises, positive = falls
    public float ParticleLife;
}

// All prices/colors for the shop live here in one place - purely cosmetic, no gameplay effect.
public static class CosmeticsCatalog
{
    public static readonly SkinDef[] Skins =
    {
        new SkinDef { Id = "standard", Name = "Standard", Price = 0, BaseColor = Color.white, HasEmission = false },
        new SkinDef { Id = "green", Name = "Grün", Price = 25, BaseColor = new Color(0.25f, 0.75f, 0.3f), HasEmission = false },
        new SkinDef { Id = "blue", Name = "Blau", Price = 25, BaseColor = new Color(0.25f, 0.5f, 0.95f), HasEmission = false },
        new SkinDef { Id = "red", Name = "Rot", Price = 25, BaseColor = new Color(0.85f, 0.25f, 0.25f), HasEmission = false },
        new SkinDef { Id = "gold", Name = "Gold", Price = 75, BaseColor = new Color(0.95f, 0.8f, 0.35f), HasEmission = true, EmissionColor = new Color(0.6f, 0.45f, 0.05f) },
        new SkinDef { Id = "lava", Name = "Lava", Price = 120, BaseColor = new Color(0.25f, 0.08f, 0.05f), HasEmission = true, EmissionColor = new Color(1.4f, 0.35f, 0.05f) },
        new SkinDef { Id = "ice", Name = "Eis", Price = 120, BaseColor = new Color(0.7f, 0.9f, 0.98f), HasEmission = true, EmissionColor = new Color(0.3f, 0.55f, 0.7f) },
        new SkinDef { Id = "neon", Name = "Neon", Price = 180, BaseColor = new Color(0.05f, 0.05f, 0.08f), HasEmission = true, EmissionColor = new Color(1.2f, 0.1f, 1.4f) },
        new SkinDef { Id = "galaxy", Name = "Galaxy", Price = 250, BaseColor = new Color(0.12f, 0.05f, 0.25f), HasEmission = true, EmissionColor = new Color(0.5f, 0.2f, 1.1f) },
    };

    public static readonly EffectDef[] Effects =
    {
        new EffectDef { Id = "none", Name = "Kein Effekt", Price = 0 },
        // Fire: wide warm trail + fast embers that rise.
        new EffectDef { Id = "fire", Name = "Feuer", Price = 50, ColorA = new Color(1f, 0.65f, 0.12f), ColorB = new Color(0.85f, 0.12f, 0f),
            TrailWidth = 0.4f, TrailTime = 0.4f, ParticleRate = 34f, ParticleSize = 0.22f, ParticleSpeed = 1.6f, ParticleGravity = -0.5f, ParticleLife = 0.7f },
        // Ice: cool trail + slow snow that drifts down.
        new EffectDef { Id = "ice", Name = "Eis", Price = 50, ColorA = new Color(0.75f, 0.96f, 1f), ColorB = new Color(0.3f, 0.6f, 0.95f),
            TrailWidth = 0.3f, TrailTime = 0.55f, ParticleRate = 26f, ParticleSize = 0.16f, ParticleSpeed = 0.5f, ParticleGravity = 0.5f, ParticleLife = 1.3f },
        // Lightning: thin, short, bright trail + fast crackling sparks.
        new EffectDef { Id = "lightning", Name = "Blitz", Price = 80, ColorA = new Color(1f, 1f, 0.55f), ColorB = new Color(0.75f, 0.85f, 1f),
            TrailWidth = 0.15f, TrailTime = 0.22f, ParticleRate = 46f, ParticleSize = 0.1f, ParticleSpeed = 3.2f, ParticleGravity = 0f, ParticleLife = 0.35f },
        // Rainbow: broad trail + big multicolour puffs.
        new EffectDef { Id = "rainbow", Name = "Regenbogen", Price = 130, ColorA = new Color(1f, 0.25f, 0.3f), ColorB = new Color(0.3f, 0.5f, 1f),
            TrailWidth = 0.45f, TrailTime = 0.7f, ParticleRate = 22f, ParticleSize = 0.3f, ParticleSpeed = 0.9f, ParticleGravity = 0f, ParticleLife = 1.1f },
        // Stars: thin trail + sparse slow twinkles.
        new EffectDef { Id = "stars", Name = "Sterne", Price = 160, ColorA = new Color(1f, 1f, 0.9f), ColorB = new Color(0.6f, 0.4f, 1f),
            TrailWidth = 0.18f, TrailTime = 0.6f, ParticleRate = 12f, ParticleSize = 0.26f, ParticleSpeed = 0.35f, ParticleGravity = -0.1f, ParticleLife = 1.6f },
        // Galaxy: deep purple trail + dense swirling dust.
        new EffectDef { Id = "galaxy", Name = "Galaxy", Price = 220, ColorA = new Color(0.65f, 0.2f, 0.95f), ColorB = new Color(0.15f, 0.35f, 0.9f),
            TrailWidth = 0.38f, TrailTime = 0.8f, ParticleRate = 40f, ParticleSize = 0.18f, ParticleSpeed = 1.3f, ParticleGravity = 0f, ParticleLife = 1.2f },
    };

    public static bool TryGetSkin(string id, out SkinDef skin)
    {
        foreach (var s in Skins)
        {
            if (s.Id == id) { skin = s; return true; }
        }
        skin = Skins[0];
        return false;
    }

    public static bool TryGetEffect(string id, out EffectDef effect)
    {
        foreach (var e in Effects)
        {
            if (e.Id == id) { effect = e; return true; }
        }
        effect = Effects[0];
        return false;
    }
}
