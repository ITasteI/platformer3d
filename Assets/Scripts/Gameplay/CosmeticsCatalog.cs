using UnityEngine;

public struct SkinDef
{
    public string Id;
    public string Name;
    public int Price;
    public Color BaseColor;
    public bool HasEmission;
    public Color EmissionColor;

    // Which Kenney character mesh this skin uses (they share one rig, so the animator works on all).
    // Empty/null = the default base character. Lets skins be genuinely different shapes, not recolors.
    public string Model;
}

public struct EffectDef
{
    public string Id;
    public string Name;
    public int Price;
    public Color ColorA;
    public Color ColorB;

    // Per-effect look so each one reads as a genuinely different reward.
    public float TrailWidth;      // 0 = no trail
    public float TrailTime;       // trail length in seconds
    public float ParticleRate;    // 0 = no particles
    public float ParticleSize;
    public float ParticleSpeed;
    public float ParticleGravity; // negative = rises, positive = falls
    public float ParticleLife;

    // New distinctiveness dimensions: emission shape, spin, size-over-life and additive glow.
    public ParticleSystemShapeType Shape; // emission pattern (cone/sphere/circle/hemisphere)
    public float SpinDegPerSec;           // particle rotation over life (crackle/swirl)
    public float EndSizeMul;              // size at end of life (1 = constant, <1 shrink, >1 grow)
    public bool Additive;                 // glowing (additive) vs soft (alpha) particles
}

// All prices/colors for the shop live here in one place - purely cosmetic, no gameplay effect.
// Prices are tiered so higher cosmetics feel earned and premium.
public static class CosmeticsCatalog
{
    public static readonly SkinDef[] Skins =
    {
        new SkinDef { Id = "standard", Name = "Standard", Price = 0, BaseColor = Color.white, HasEmission = false, Model = "character-oobi" },
        // Basic recolors of the base character - matte, no glow.
        new SkinDef { Id = "green", Name = "Grün", Price = 40, BaseColor = new Color(0.25f, 0.75f, 0.3f), HasEmission = false, Model = "character-oobi" },
        new SkinDef { Id = "blue", Name = "Blau", Price = 40, BaseColor = new Color(0.25f, 0.5f, 0.95f), HasEmission = false, Model = "character-oobi" },
        new SkinDef { Id = "red", Name = "Rot", Price = 40, BaseColor = new Color(0.85f, 0.25f, 0.25f), HasEmission = false, Model = "character-oobi" },
        // Premium tiers - each a DIFFERENT character model plus glowing HDR emission.
        new SkinDef { Id = "gold", Name = "Goldwächter", Price = 150, BaseColor = new Color(0.95f, 0.8f, 0.35f), HasEmission = true, EmissionColor = new Color(1.1f, 0.75f, 0.1f), Model = "character-oopi" },
        new SkinDef { Id = "lava", Name = "Lavagolem", Price = 300, BaseColor = new Color(0.25f, 0.08f, 0.05f), HasEmission = true, EmissionColor = new Color(2.4f, 0.5f, 0.06f), Model = "character-oodi" },
        new SkinDef { Id = "ice", Name = "Eiswicht", Price = 300, BaseColor = new Color(0.7f, 0.9f, 0.98f), HasEmission = true, EmissionColor = new Color(0.45f, 0.85f, 1.2f), Model = "character-ooli" },
        new SkinDef { Id = "neon", Name = "Neonbot", Price = 500, BaseColor = new Color(0.05f, 0.05f, 0.08f), HasEmission = true, EmissionColor = new Color(2.0f, 0.15f, 2.4f), Model = "character-oozi" },
        new SkinDef { Id = "galaxy", Name = "Galaxywächter", Price = 750, BaseColor = new Color(0.12f, 0.05f, 0.25f), HasEmission = true, EmissionColor = new Color(0.9f, 0.35f, 1.9f), Model = "character-oopi" },
    };

    public static readonly EffectDef[] Effects =
    {
        new EffectDef { Id = "none", Name = "Kein Effekt", Price = 0 },

        // Fire: cone of glowing embers that rise and shrink, wide warm trail.
        new EffectDef { Id = "fire", Name = "Feuer", Price = 100, ColorA = new Color(1f, 0.7f, 0.15f), ColorB = new Color(0.9f, 0.1f, 0f),
            TrailWidth = 0.42f, TrailTime = 0.4f, ParticleRate = 40f, ParticleSize = 0.24f, ParticleSpeed = 1.9f, ParticleGravity = -0.6f, ParticleLife = 0.7f,
            Shape = ParticleSystemShapeType.Cone, SpinDegPerSec = 40f, EndSizeMul = 0.15f, Additive = true },

        // Ice: soft snow that drifts down slowly and lingers, thin cool trail.
        new EffectDef { Id = "ice", Name = "Eis", Price = 100, ColorA = new Color(0.8f, 0.97f, 1f), ColorB = new Color(0.3f, 0.6f, 0.95f),
            TrailWidth = 0.28f, TrailTime = 0.6f, ParticleRate = 22f, ParticleSize = 0.14f, ParticleSpeed = 0.4f, ParticleGravity = 0.6f, ParticleLife = 1.6f,
            Shape = ParticleSystemShapeType.Sphere, SpinDegPerSec = 0f, EndSizeMul = 0.7f, Additive = false },

        // Lightning: tight cone of fast, tiny, spinning bright sparks, very short thin trail.
        new EffectDef { Id = "lightning", Name = "Blitz", Price = 200, ColorA = new Color(1f, 1f, 0.6f), ColorB = new Color(0.7f, 0.85f, 1f),
            TrailWidth = 0.13f, TrailTime = 0.18f, ParticleRate = 55f, ParticleSize = 0.09f, ParticleSpeed = 3.6f, ParticleGravity = 0f, ParticleLife = 0.3f,
            Shape = ParticleSystemShapeType.Cone, SpinDegPerSec = 720f, EndSizeMul = 0.05f, Additive = true },

        // Rainbow: big soft multicolour puffs that grow, broad trail.
        new EffectDef { Id = "rainbow", Name = "Regenbogen", Price = 400, ColorA = new Color(1f, 0.25f, 0.3f), ColorB = new Color(0.3f, 0.5f, 1f),
            TrailWidth = 0.48f, TrailTime = 0.75f, ParticleRate = 24f, ParticleSize = 0.28f, ParticleSpeed = 0.8f, ParticleGravity = 0f, ParticleLife = 1.2f,
            Shape = ParticleSystemShapeType.Sphere, SpinDegPerSec = 30f, EndSizeMul = 1.8f, Additive = false },

        // Stars: sparse, large, slow twinkles from a hemisphere, thin trail, glowing.
        new EffectDef { Id = "stars", Name = "Sterne", Price = 550, ColorA = new Color(1f, 1f, 0.9f), ColorB = new Color(0.6f, 0.4f, 1f),
            TrailWidth = 0.16f, TrailTime = 0.6f, ParticleRate = 10f, ParticleSize = 0.3f, ParticleSpeed = 0.3f, ParticleGravity = -0.05f, ParticleLife = 1.9f,
            Shape = ParticleSystemShapeType.Hemisphere, SpinDegPerSec = 90f, EndSizeMul = 0.2f, Additive = true },

        // Galaxy: dense swirling dust from an orbital circle, deep purple broad trail, glowing.
        new EffectDef { Id = "galaxy", Name = "Galaxy", Price = 750, ColorA = new Color(0.7f, 0.2f, 1f), ColorB = new Color(0.15f, 0.4f, 0.95f),
            TrailWidth = 0.4f, TrailTime = 0.85f, ParticleRate = 48f, ParticleSize = 0.17f, ParticleSpeed = 1.2f, ParticleGravity = 0f, ParticleLife = 1.3f,
            Shape = ParticleSystemShapeType.Circle, SpinDegPerSec = 300f, EndSizeMul = 0.6f, Additive = true },
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
