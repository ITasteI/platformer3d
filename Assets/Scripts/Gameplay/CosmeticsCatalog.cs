using UnityEngine;

// A "skin" is a whole different CHARACTER look on the shared Kenney humanoid: its own texture
// (skaterMale, cyborg, ...) plus an optional tint and themed aura. Unlocked with coins.
public struct SkinDef
{
    public string Id;
    public string Name;
    public int Price;

    // Kenney protagonist skin texture (PNG name under KenneyProtagonists/Skins) = the character look.
    public string Texture;

    // Optional multiply tint on the texture (default/zero-alpha = untinted).
    public Color Tint;

    // Optional themed particle aura: "fire", "ice", "lightning", "nature", "shadow" (empty = none).
    public string AuraTheme;
}

// The equippable head accessory kinds (built procedurally in CosmeticApplier). Replaces the old
// particle "effects" - a worn cosmetic that sits on the character instead of distracting trails.
public enum AccessoryKind { None, TopHat, Cap, Crown, PartyHat, WizardHat, Helmet, Halo }

public struct AccessoryDef
{
    public string Id;
    public string Name;
    public int Price;
    public AccessoryKind Kind;
    public Color Color;   // main color
    public Color Color2;  // accent/trim color
}

// An action-bound particle tint (third cosmetic slot) - see CosmeticsCatalog.ActionFx.
public struct ActionFxDef
{
    public string Id;
    public string Name;
    public int Price;
    public Color Color;
}

// Item rarity, derived from price - drives the coloured stripes/labels in the shop so the
// catalogue reads as a real progression ladder instead of a flat list.
public static class Rarity
{
    public static (string Name, Color Color) For(int price)
    {
        if (price <= 0) return ("Standard", new Color(0.62f, 0.67f, 0.78f));
        if (price < 500) return ("Gewöhnlich", new Color(0.55f, 0.82f, 0.55f));
        if (price < 1200) return ("Selten", new Color(0.42f, 0.7f, 1f));
        if (price < 2200) return ("Episch", new Color(0.76f, 0.52f, 1f));
        return ("Legendär", new Color(1f, 0.82f, 0.35f));
    }
}

// All prices/colors for the shop live here in one place - purely cosmetic, no gameplay effect.
// Prices are tiered so higher cosmetics feel earned and premium.
public static class CosmeticsCatalog
{
    public static readonly SkinDef[] Skins =
    {
        // Real, distinct humanoid characters (Kenney "Animated Characters Protagonists"), unlocked
        // with coins. Each is a different look, not a recolor. Premium ones add a themed aura.
        // Prices raised: the economy gained many new sources (boss bounties, chest islands,
        // daily/weekly challenges, run bonus) - cosmetics should stay a long-term goal.
        new SkinDef { Id = "adventurer", Name = "Abenteurer", Price = 0, Texture = "skaterMaleA" },
        new SkinDef { Id = "explorer", Name = "Entdeckerin", Price = 400, Texture = "skaterFemaleA" },
        // Auras removed on request - these were the leftover particle "effects" baked into the skins.
        new SkinDef { Id = "rogue", Name = "Schurke", Price = 1000, Texture = "criminalMaleA" },
        new SkinDef { Id = "cyborg", Name = "Cyborg", Price = 2000, Texture = "cyborgFemaleA" },
    };

    // Wearable head accessories (built procedurally, sit on the character's head). Unlocked with
    // coins. Purely cosmetic, non-intrusive - no view-blocking particles.
    public static readonly AccessoryDef[] Accessories =
    {
        new AccessoryDef { Id = "none", Name = "Kein Accessoire", Price = 0, Kind = AccessoryKind.None },
        new AccessoryDef { Id = "cap", Name = "Cap", Price = 300, Kind = AccessoryKind.Cap,
            Color = new Color(0.85f, 0.2f, 0.25f), Color2 = new Color(0.7f, 0.15f, 0.2f) },
        new AccessoryDef { Id = "party", Name = "Partyhut", Price = 500, Kind = AccessoryKind.PartyHat,
            Color = new Color(1f, 0.35f, 0.6f), Color2 = new Color(1f, 0.9f, 0.3f) },
        new AccessoryDef { Id = "tophat", Name = "Zylinder", Price = 800, Kind = AccessoryKind.TopHat,
            Color = new Color(0.09f, 0.09f, 0.12f), Color2 = new Color(0.7f, 0.15f, 0.2f) },
        new AccessoryDef { Id = "helmet", Name = "Wikingerhelm", Price = 1200, Kind = AccessoryKind.Helmet,
            Color = new Color(0.62f, 0.66f, 0.72f), Color2 = new Color(0.9f, 0.85f, 0.7f) },
        new AccessoryDef { Id = "wizard", Name = "Zauberhut", Price = 1600, Kind = AccessoryKind.WizardHat,
            Color = new Color(0.28f, 0.2f, 0.55f), Color2 = new Color(1f, 0.85f, 0.3f) },
        new AccessoryDef { Id = "crown", Name = "Krone", Price = 2200, Kind = AccessoryKind.Crown,
            Color = new Color(1f, 0.82f, 0.25f), Color2 = new Color(0.9f, 0.3f, 0.4f) },
        new AccessoryDef { Id = "halo", Name = "Heiligenschein", Price = 2800, Kind = AccessoryKind.Halo,
            Color = new Color(1f, 0.92f, 0.5f), Color2 = new Color(1f, 0.85f, 0.3f) },
    };

    // The third cosmetic slot: ACTION EFFECTS. Purely visual tints applied to the player's own
    // action particles (jump ring, dash streak, landing dust, footstep puffs) - bound to actions,
    // never floating around the character, so they can't obstruct the view.
    public static readonly ActionFxDef[] ActionFx =
    {
        new ActionFxDef { Id = "none",   Name = "Kein Effekt",   Price = 0,    Color = Color.white },
        new ActionFxDef { Id = "fire",   Name = "Feuerspur",     Price = 600,  Color = new Color(1f, 0.45f, 0.15f) },
        new ActionFxDef { Id = "frost",  Name = "Frostspur",     Price = 1000, Color = new Color(0.45f, 0.85f, 1f) },
        new ActionFxDef { Id = "nature", Name = "Naturpfad",     Price = 1500, Color = new Color(0.45f, 1f, 0.5f) },
        new ActionFxDef { Id = "star",   Name = "Sternenglanz",  Price = 2200, Color = new Color(1f, 0.85f, 0.35f) },
    };

    public static bool TryGetActionFx(string id, out ActionFxDef fx)
    {
        foreach (var f in ActionFx)
        {
            if (f.Id == id) { fx = f; return true; }
        }
        fx = ActionFx[0];
        return false;
    }

    public static bool TryGetSkin(string id, out SkinDef skin)
    {
        foreach (var s in Skins)
        {
            if (s.Id == id) { skin = s; return true; }
        }
        skin = Skins[0];
        return false;
    }

    public static bool TryGetAccessory(string id, out AccessoryDef accessory)
    {
        foreach (var a in Accessories)
        {
            if (a.Id == id) { accessory = a; return true; }
        }
        accessory = Accessories[0];
        return false;
    }
}
