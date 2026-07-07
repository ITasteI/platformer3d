using UnityEngine;

// Cosmetic shop: buy/equip skins and trail effects with earned coins. Reads/writes EconomySystem
// (persistent) and pushes equip changes live to the local player's PlayerCosmetics so they show
// immediately and sync in multiplayer. Purely cosmetic - nothing here affects gameplay.
public class ShopMenu : MonoBehaviour
{
    enum Tab { Skins, Effects, Inventory }
    private Tab tab = Tab.Skins;
    private Vector2 scroll;
    private Vector2 invScroll;
    private string status = "";

    void OnGUI()
    {
        if (MainMenu.Current != MenuScreen.Shop)
            return;

        UITheme.EnsureInit();

        float w = 560f;
        float h = 470f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x, y + 12, w, 34), "Shop", UITheme.TitleStyle);

        // Coin balance, top-right.
        GUI.Box(new Rect(x + w - 170, y + 16, 150, 30), "", UITheme.PillStyle);
        UITheme.Rect(new Rect(x + w - 160, y + 24, 14, 14), UITheme.Gold);
        GUI.Label(new Rect(x + w - 140, y + 18, 120, 26), EconomySystem.Coins.ToString(), UITheme.CoinStyle);

        // Tabs.
        float tabW = (w - 60) / 3f;
        DrawTab(new Rect(x + 20, y + 52, tabW, 32), "Skins", Tab.Skins);
        DrawTab(new Rect(x + 30 + tabW, y + 52, tabW, 32), "Effekte", Tab.Effects);
        DrawTab(new Rect(x + 40 + tabW * 2, y + 52, tabW, 32), "Meine Gegenstände", Tab.Inventory);

        Rect listArea = new Rect(x + 20, y + 94, w - 40, h - 150);
        if (tab == Tab.Skins)
            DrawSkinList(listArea, false);
        else if (tab == Tab.Effects)
            DrawEffectList(listArea, false);
        else
            DrawInventory(listArea);

        if (status.Length > 0)
            GUI.Label(new Rect(x + 20, y + h - 52, w - 200, 22), status, UITheme.LabelStyle);

        if (GUI.Button(new Rect(x + w - 150, y + h - 52, 130, 32), "↩ Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            MainMenu.SetScreen(MenuScreen.Main);
        }
    }

    void DrawTab(Rect r, string label, Tab t)
    {
        if (GUI.Button(r, label, tab == t ? UITheme.TabActiveStyle : UITheme.TabStyle))
        {
            AudioManager.Instance?.PlayClick();
            tab = t;
            scroll = Vector2.zero;
        }
    }

    void DrawSkinList(Rect area, bool ownedOnly)
    {
        var skins = CosmeticsCatalog.Skins;
        int shown = 0;
        for (int i = 0; i < skins.Length; i++)
            if (!ownedOnly || EconomySystem.IsOwned(skins[i].Id, true)) shown++;

        Rect view = new Rect(0, 0, area.width - 20, shown * 62f);
        scroll = GUI.BeginScrollView(area, scroll, view);
        float row = 0;
        foreach (var s in skins)
        {
            if (ownedOnly && !EconomySystem.IsOwned(s.Id, true))
                continue;
            DrawItemRow(new Rect(0, row, view.width, 56), s.Name, s.Price, s.BaseColor, s.HasEmission,
                s.Id, true, EconomySystem.EquippedSkin);
            row += 62f;
        }
        GUI.EndScrollView();
    }

    void DrawEffectList(Rect area, bool ownedOnly)
    {
        var effects = CosmeticsCatalog.Effects;
        int shown = 0;
        for (int i = 0; i < effects.Length; i++)
            if (!ownedOnly || EconomySystem.IsOwned(effects[i].Id, false)) shown++;

        Rect view = new Rect(0, 0, area.width - 20, shown * 62f);
        scroll = GUI.BeginScrollView(area, scroll, view);
        float row = 0;
        foreach (var e in effects)
        {
            if (ownedOnly && !EconomySystem.IsOwned(e.Id, false))
                continue;
            DrawItemRow(new Rect(0, row, view.width, 56), e.Name, e.Price, e.ColorA, true,
                e.Id, false, EconomySystem.EquippedEffect);
            row += 62f;
        }
        GUI.EndScrollView();
    }

    // Single scroll view over all owned skins + effects, so the whole inventory scrolls as one
    // list (the old two-nested-scrollviews layout couldn't be scrolled properly).
    void DrawInventory(Rect area)
    {
        var ownedSkins = new System.Collections.Generic.List<SkinDef>();
        foreach (var s in CosmeticsCatalog.Skins)
            if (EconomySystem.IsOwned(s.Id, true)) ownedSkins.Add(s);
        var ownedEffects = new System.Collections.Generic.List<EffectDef>();
        foreach (var eff in CosmeticsCatalog.Effects)
            if (EconomySystem.IsOwned(eff.Id, false)) ownedEffects.Add(eff);

        const float rowH = 62f, headerH = 30f;
        float contentH = headerH + ownedSkins.Count * rowH + headerH + ownedEffects.Count * rowH + 10f;
        Rect view = new Rect(0, 0, area.width - 20, Mathf.Max(contentH, area.height));

        invScroll = GUI.BeginScrollView(area, invScroll, view);
        float y = 0f;

        GUI.Label(new Rect(0, y, 200, 24), "Skins", UITheme.SubtitleStyle);
        y += headerH;
        foreach (var s in ownedSkins)
        {
            DrawItemRow(new Rect(0, y, view.width, 56), s.Name, s.Price, s.BaseColor, s.HasEmission,
                s.Id, true, EconomySystem.EquippedSkin);
            y += rowH;
        }

        GUI.Label(new Rect(0, y, 200, 24), "Effekte", UITheme.SubtitleStyle);
        y += headerH;
        foreach (var eff in ownedEffects)
        {
            DrawItemRow(new Rect(0, y, view.width, 56), eff.Name, eff.Price, eff.ColorA, true,
                eff.Id, false, EconomySystem.EquippedEffect);
            y += rowH;
        }

        GUI.EndScrollView();
    }

    void DrawItemRow(Rect r, string name, int price, Color swatch, bool emissive, string id, bool isSkin, string equippedId)
    {
        GUI.Box(r, "", UITheme.CardStyle);

        // Color swatch.
        UITheme.Rect(new Rect(r.x + 10, r.y + 12, 32, 32), swatch);
        if (emissive)
            UITheme.Rect(new Rect(r.x + 10, r.y + 40, 32, 4), swatch * 1.5f);

        GUI.Label(new Rect(r.x + 54, r.y + 8, 200, 24), name, UITheme.SubtitleStyle);

        bool owned = EconomySystem.IsOwned(id, isSkin);
        bool equipped = id == equippedId;

        if (!owned)
        {
            var priceStyle = new GUIStyle(UITheme.LabelStyle);
            priceStyle.normal.textColor = UITheme.Gold;
            GUI.Label(new Rect(r.x + 54, r.y + 30, 200, 20), "● " + price, priceStyle);
        }
        else
        {
            GUI.Label(new Rect(r.x + 54, r.y + 30, 200, 20), equipped ? "Aktiv" : "Gekauft",
                equipped ? UITheme.TagStyle : UITheme.LabelStyle);
        }

        Rect btn = new Rect(r.xMax - 130, r.y + 12, 118, 32);
        if (!owned)
        {
            if (GUI.Button(btn, "Kaufen", UITheme.BuyStyle))
                Buy(id, price, isSkin, name);
        }
        else if (!equipped)
        {
            if (GUI.Button(btn, "Ausrüsten", UITheme.EquipStyle))
                Equip(id, isSkin);
        }
        else
        {
            GUI.Label(new Rect(btn.x, btn.y + 4, btn.width, 24), "✓ Ausgerüstet", UITheme.TagStyle);
        }
    }

    void Buy(string id, int price, bool isSkin, string name)
    {
        if (EconomySystem.TryPurchase(id, price, isSkin))
        {
            AudioManager.Instance?.PlayCoin();
            status = name + " gekauft!";
            Equip(id, isSkin);
        }
        else
        {
            status = "Nicht genug Coins für " + name + ".";
        }
    }

    void Equip(string id, bool isSkin)
    {
        AudioManager.Instance?.PlayClick();
        EconomySystem.Equip(id, isSkin);
        var pc = LocalCosmetics();
        if (pc != null)
            pc.SetEquipped(EconomySystem.EquippedSkin, EconomySystem.EquippedEffect);
    }

    PlayerCosmetics LocalCosmetics()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            return GameManager.Instance.player.GetComponent<PlayerCosmetics>();
        return null;
    }
}
