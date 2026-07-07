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
}

// All prices/colors for the shop live here in one place - purely cosmetic, no gameplay effect.
public static class CosmeticsCatalog
{
    public static readonly SkinDef[] Skins =
    {
        new SkinDef { Id = "standard", Name = "Standard", Price = 0, BaseColor = Color.white, HasEmission = false },
        new SkinDef { Id = "green", Name = "Grün", Price = 100, BaseColor = new Color(0.25f, 0.75f, 0.3f), HasEmission = false },
        new SkinDef { Id = "blue", Name = "Blau", Price = 100, BaseColor = new Color(0.25f, 0.5f, 0.95f), HasEmission = false },
        new SkinDef { Id = "red", Name = "Rot", Price = 100, BaseColor = new Color(0.85f, 0.25f, 0.25f), HasEmission = false },
        new SkinDef { Id = "gold", Name = "Gold", Price = 300, BaseColor = new Color(0.95f, 0.8f, 0.35f), HasEmission = true, EmissionColor = new Color(0.6f, 0.45f, 0.05f) },
        new SkinDef { Id = "lava", Name = "Lava", Price = 500, BaseColor = new Color(0.25f, 0.08f, 0.05f), HasEmission = true, EmissionColor = new Color(1.4f, 0.35f, 0.05f) },
        new SkinDef { Id = "ice", Name = "Eis", Price = 500, BaseColor = new Color(0.7f, 0.9f, 0.98f), HasEmission = true, EmissionColor = new Color(0.3f, 0.55f, 0.7f) },
        new SkinDef { Id = "neon", Name = "Neon", Price = 750, BaseColor = new Color(0.05f, 0.05f, 0.08f), HasEmission = true, EmissionColor = new Color(1.2f, 0.1f, 1.4f) },
        new SkinDef { Id = "galaxy", Name = "Galaxy", Price = 1000, BaseColor = new Color(0.12f, 0.05f, 0.25f), HasEmission = true, EmissionColor = new Color(0.5f, 0.2f, 1.1f) },
    };

    public static readonly EffectDef[] Effects =
    {
        new EffectDef { Id = "none", Name = "Kein Effekt", Price = 0 },
        new EffectDef { Id = "fire", Name = "Feuer-Trail", Price = 200, ColorA = new Color(1f, 0.6f, 0.1f), ColorB = new Color(0.8f, 0.1f, 0f) },
        new EffectDef { Id = "ice", Name = "Eis-Trail", Price = 200, ColorA = new Color(0.7f, 0.95f, 1f), ColorB = new Color(0.3f, 0.6f, 0.9f) },
        new EffectDef { Id = "lightning", Name = "Blitz-Trail", Price = 300, ColorA = new Color(1f, 1f, 0.6f), ColorB = new Color(0.9f, 0.9f, 1f) },
        new EffectDef { Id = "rainbow", Name = "Regenbogen-Trail", Price = 500, ColorA = new Color(1f, 0.3f, 0.3f), ColorB = new Color(0.4f, 0.5f, 1f) },
        new EffectDef { Id = "stars", Name = "Sternen-Trail", Price = 600, ColorA = new Color(0.9f, 0.9f, 1f), ColorB = new Color(0.4f, 0.3f, 0.8f) },
        new EffectDef { Id = "galaxy", Name = "Galaxy-Trail", Price = 800, ColorA = new Color(0.6f, 0.2f, 0.9f), ColorB = new Color(0.1f, 0.3f, 0.8f) },
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
