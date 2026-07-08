using UnityEngine;

// Cosmetic shop: buy/equip skins and worn accessories (hats/crowns) with earned coins. Reads/writes
// EconomySystem (persistent) and pushes equip changes live to the local player's PlayerCosmetics so
// they show immediately and sync in multiplayer. Purely cosmetic - nothing here affects gameplay.
public class ShopMenu : MonoBehaviour
{
    enum Tab { Skins, Accessories, Effects, Inventory }
    private Tab tab = Tab.Skins;
    private Vector2 scroll;
    private Vector2 invScroll;
    private string status = "";
    private float statusTime;
    private bool statusGood;

    // What the live preview shows: defaults to the equipped loadout each frame, overridden by the
    // item row under the mouse so you can try before you buy.
    private string hoverSkin;
    private string hoverAccessory;
    private string hoverFx;

    void SetStatus(string message, bool good)
    {
        status = message;
        statusGood = good;
        statusTime = Time.time;
    }

    void OnGUI()
    {
        UITheme.BeginUI();
        if (MainMenu.Current != MenuScreen.Shop)
            return;

        UITheme.EnsureInit();

        float w = 800f;
        float h = 500f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;
        const float leftW = 500f; // left column (list) region width; preview fills the rest

        MainMenu.DrawScreenChrome(new Rect(x, y, w, h), null); // backdrop + halo; own header below
        GUI.Label(new Rect(x, y + 12, leftW, 34), "Shop", UITheme.TitleStyle);

        // Coin balance, top-right: bigger pill with a soft gold halo so the currency always pops.
        Rect coinPill = new Rect(x + w - 190, y + 14, 170, 38);
        UITheme.DrawGlow(new Rect(coinPill.x - 8, coinPill.y - 8, coinPill.width + 16, coinPill.height + 16),
            new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, 0.18f));
        GUI.Box(coinPill, "", UITheme.PillStyle);
        UITheme.RoundRect(new Rect(coinPill.x + 12, coinPill.y + 9, 20, 20), UITheme.Gold);
        UITheme.RoundRect(new Rect(coinPill.x + 16, coinPill.y + 13, 12, 12), new Color(1f, 0.92f, 0.55f));
        GUI.Label(new Rect(coinPill.x + 42, coinPill.y + 6, coinPill.width - 52, 26),
            EconomySystem.Coins.ToString(), UITheme.CoinStyle);

        // Tabs (left column) - four now that action effects are their own slot.
        float tabW = (leftW - 70) / 4f;
        DrawTab(new Rect(x + 20, y + 52, tabW, 32), "Skins", Tab.Skins);
        DrawTab(new Rect(x + 30 + tabW, y + 52, tabW, 32), "Accessoires", Tab.Accessories);
        DrawTab(new Rect(x + 40 + tabW * 2, y + 52, tabW, 32), "Effekte", Tab.Effects);
        DrawTab(new Rect(x + 50 + tabW * 3, y + 52, tabW, 32), "Inventar", Tab.Inventory);

        // Preview defaults to the equipped loadout; item rows override on hover.
        hoverSkin = EconomySystem.EquippedSkin;
        hoverAccessory = EconomySystem.EquippedAccessory;
        hoverFx = EconomySystem.EquippedActionFx;

        Rect listArea = new Rect(x + 20, y + 94, leftW - 40, h - 150);
        if (tab == Tab.Skins)
            DrawSkinList(listArea, false);
        else if (tab == Tab.Accessories)
            DrawAccessoryList(listArea, false);
        else if (tab == Tab.Effects)
            DrawFxList(listArea);
        else
            DrawInventory(listArea);

        DrawPreview(new Rect(x + leftW + 4, y + 52, w - leftW - 24, h - 108));

        if (status.Length > 0)
        {
            float a = Mathf.Clamp01(3f - (Time.time - statusTime)); // hold ~2s, then fade
            if (statusStyle == null)
                statusStyle = new GUIStyle(UITheme.LabelStyle) { fontStyle = FontStyle.Bold };
            var st = statusStyle;
            st.normal.textColor = statusGood ? UITheme.Positive : UITheme.Danger;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(x + 20, y + h - 52, leftW - 40, 22), status, st);
            GUI.color = prev;
        }

        if (GUI.Button(new Rect(x + w - 170, y + h - 52, 150, 32), "↩ Schließen (Tab)", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            MainMenu.CloseShop();
        }
    }

    // Right-column live preview: draws the rotating preview character (RenderTexture) with the
    // hovered skin/accessory applied, plus what's being shown vs. currently equipped.
    void DrawPreview(Rect box)
    {
        GUI.Box(box, "", UITheme.CardStyle);
        GUI.Label(new Rect(box.x + 12, box.y + 8, box.width - 24, 22), "Vorschau", UITheme.SubtitleStyle);

        var preview = CosmeticPreview.Instance;
        if (preview != null)
        {
            preview.SetPreview(hoverSkin, hoverAccessory);
            preview.SetPreviewFx(hoverFx); // real particles on the preview stage
        }

        float tw = box.width - 40f;
        float th = tw * (430f / 320f);
        float maxTh = box.height - 118f;
        if (th > maxTh)
        {
            th = maxTh;
            tw = th * (320f / 430f);
        }
        Rect rt = new Rect(box.x + (box.width - tw) / 2f, box.y + 36, tw, th);

        CosmeticsCatalog.TryGetActionFx(hoverFx, out ActionFxDef fxDef);

        if (preview != null && preview.Texture != null)
            GUI.DrawTexture(rt, preview.Texture, ScaleMode.ScaleToFit, false);
        else
            GUI.Label(rt, "(keine Vorschau)", UITheme.LabelStyle);

        // Purchase celebration: a brief golden flash over the preview.
        float flashAge = Time.time - purchaseFlashTime;
        if (flashAge < 0.7f)
            UITheme.DrawGlow(new Rect(rt.x - 20, rt.y - 20, rt.width + 40, rt.height + 40),
                new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, 0.7f * (1f - flashAge / 0.7f)));

        CosmeticsCatalog.TryGetSkin(hoverSkin, out SkinDef sk);
        CosmeticsCatalog.TryGetAccessory(hoverAccessory, out AccessoryDef ac);
        float ly = rt.yMax + 8f;
        GUI.Label(new Rect(box.x + 14, ly, box.width - 24, 20), "Skin: " + sk.Name, UITheme.SubtitleStyle);
        GUI.Label(new Rect(box.x + 14, ly + 22, box.width - 24, 20), "Accessoire: " + ac.Name, UITheme.SubtitleStyle);
        GUI.Label(new Rect(box.x + 14, ly + 44, box.width - 24, 20), "Effekt: " + fxDef.Name, UITheme.SubtitleStyle);

        CosmeticsCatalog.TryGetSkin(EconomySystem.EquippedSkin, out SkinDef esk);
        CosmeticsCatalog.TryGetAccessory(EconomySystem.EquippedAccessory, out AccessoryDef eac);
        var small = new GUIStyle(UITheme.LabelStyle) { fontSize = 12 };
        small.normal.textColor = new Color(0.72f, 0.74f, 0.8f);
        GUI.Label(new Rect(box.x + 14, ly + 66, box.width - 24, 18), $"Aktuell: {esk.Name} + {eac.Name}", small);
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
            DrawItemRow(new Rect(0, row, view.width, 56), s.Name, s.Price, new Color(0.55f, 0.6f, 0.72f),
                s.Id, true, EconomySystem.EquippedSkin);
            row += 62f;
        }
        GUI.EndScrollView();
    }

    void DrawAccessoryList(Rect area, bool ownedOnly)
    {
        var accessories = CosmeticsCatalog.Accessories;
        int shown = 0;
        for (int i = 0; i < accessories.Length; i++)
            if (!ownedOnly || EconomySystem.IsOwned(accessories[i].Id, false)) shown++;

        Rect view = new Rect(0, 0, area.width - 20, shown * 62f);
        scroll = GUI.BeginScrollView(area, scroll, view);
        float row = 0;
        foreach (var a in accessories)
        {
            if (ownedOnly && !EconomySystem.IsOwned(a.Id, false))
                continue;
            DrawItemRow(new Rect(0, row, view.width, 56), a.Name, a.Price, AccessorySwatch(a),
                a.Id, false, EconomySystem.EquippedAccessory);
            row += 62f;
        }
        GUI.EndScrollView();
    }

    // Action effects (slot 3): tints on jump ring / dash streak / landing dust - bound to actions,
    // so they can never block the view like the old permanent auras did.
    void DrawFxList(Rect area)
    {
        var fx = CosmeticsCatalog.ActionFx;
        Rect view = new Rect(0, 0, area.width - 20, fx.Length * 62f + 26f);
        scroll = GUI.BeginScrollView(area, scroll, view);
        GUI.Label(new Rect(0, 0, view.width, 20),
            "Färbt Sprungring, Dash-Schweif & Landestaub - sichtbar bei jeder Aktion.", UITheme.LabelStyle);
        float row = 26f;
        foreach (var f in fx)
        {
            DrawFxRow(new Rect(0, row, view.width, 56), f);
            row += 62f;
        }
        GUI.EndScrollView();
    }

    void DrawFxRow(Rect r, ActionFxDef f)
    {
        // Hovering a row drives the live particle preview and lights the row in its rarity colour.
        bool hovered = r.Contains(Event.current.mousePosition);
        if (hovered)
            hoverFx = f.Id;

        var rarity = Rarity.For(f.Price);
        if (hovered)
            UITheme.DrawGlow(new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16),
                new Color(rarity.Color.r, rarity.Color.g, rarity.Color.b, 0.25f));
        GUI.Box(r, "", UITheme.CardStyle);

        UITheme.RoundRect(new Rect(r.x + 4, r.y + 8, 4, r.height - 16), rarity.Color);
        UITheme.RoundRect(new Rect(r.x + 13, r.y + 11, 34, 34), rarity.Color);
        UITheme.RoundRect(new Rect(r.x + 15, r.y + 13, 30, 30), f.Id == "none" ? new Color(0.4f, 0.42f, 0.5f) : f.Color);
        GUI.Label(new Rect(r.x + 58, r.y + 6, 220, 24), f.Name, UITheme.SubtitleStyle);

        bool owned = EconomySystem.IsOwnedFx(f.Id);
        bool equipped = f.Id == EconomySystem.EquippedActionFx;

        if (rarityStyle == null)
            rarityStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 12, fontStyle = FontStyle.Bold };
        rarityStyle.normal.textColor = rarity.Color;
        string second = owned
            ? rarity.Name + (equipped ? "  ·  Aktiv" : "  ·  Gekauft")
            : rarity.Name + "  ·  ● " + f.Price;
        GUI.Label(new Rect(r.x + 58, r.y + 30, 240, 20), second, rarityStyle);

        Rect btn = new Rect(r.xMax - 130, r.y + 12, 118, 32);
        if (!owned)
        {
            if (GUI.Button(btn, "Kaufen", UITheme.BuyStyle))
            {
                if (EconomySystem.TryPurchaseFx(f.Id, f.Price))
                {
                    AudioManager.Instance?.PlayCoin();
                    EconomySystem.EquipFx(f.Id);
                    SetStatus(f.Name + " gekauft und ausgerüstet!", true);
                    purchaseFlashTime = Time.time;
                }
                else
                {
                    AudioManager.Instance?.PlayClick();
                    SetStatus("Nicht genug Münzen für " + f.Name + ".", false);
                }
            }
        }
        else if (!equipped)
        {
            if (GUI.Button(btn, "Ausrüsten", UITheme.EquipStyle))
            {
                AudioManager.Instance?.PlayClick();
                EconomySystem.EquipFx(f.Id);
            }
        }
        else
        {
            GUI.Label(new Rect(btn.x, btn.y + 4, btn.width, 24), "✓ Ausgerüstet", UITheme.TagStyle);
        }
    }

    // Single scroll view over all owned skins + accessories, so the whole inventory scrolls as one
    // list (the old two-nested-scrollviews layout couldn't be scrolled properly).
    void DrawInventory(Rect area)
    {
        var ownedSkins = new System.Collections.Generic.List<SkinDef>();
        foreach (var s in CosmeticsCatalog.Skins)
            if (EconomySystem.IsOwned(s.Id, true)) ownedSkins.Add(s);
        var ownedAccessories = new System.Collections.Generic.List<AccessoryDef>();
        foreach (var a in CosmeticsCatalog.Accessories)
            if (EconomySystem.IsOwned(a.Id, false)) ownedAccessories.Add(a);

        const float rowH = 62f, headerH = 30f;
        float contentH = headerH + ownedSkins.Count * rowH + headerH + ownedAccessories.Count * rowH + 10f;
        Rect view = new Rect(0, 0, area.width - 20, Mathf.Max(contentH, area.height));

        invScroll = GUI.BeginScrollView(area, invScroll, view);
        float y = 0f;

        GUI.Label(new Rect(0, y, 200, 24), "Skins", UITheme.SubtitleStyle);
        y += headerH;
        foreach (var s in ownedSkins)
        {
            DrawItemRow(new Rect(0, y, view.width, 56), s.Name, s.Price, new Color(0.55f, 0.6f, 0.72f),
                s.Id, true, EconomySystem.EquippedSkin);
            y += rowH;
        }

        GUI.Label(new Rect(0, y, 200, 24), "Accessoires", UITheme.SubtitleStyle);
        y += headerH;
        foreach (var a in ownedAccessories)
        {
            DrawItemRow(new Rect(0, y, view.width, 56), a.Name, a.Price, AccessorySwatch(a),
                a.Id, false, EconomySystem.EquippedAccessory);
            y += rowH;
        }

        GUI.EndScrollView();
    }

    static Color AccessorySwatch(AccessoryDef a)
    {
        return a.Kind == AccessoryKind.None ? new Color(0.4f, 0.42f, 0.5f) : a.Color;
    }

    void DrawItemRow(Rect r, string name, int price, Color swatch, string id, bool isSkin, string equippedId)
    {
        // Hovering a row drives the live preview (coords are content-local inside the scroll view)
        // and lights the row up in its rarity colour.
        bool hovered = r.Contains(Event.current.mousePosition);
        if (hovered)
        {
            if (isSkin) hoverSkin = id;
            else hoverAccessory = id;
        }

        var rarity = Rarity.For(price);
        if (hovered)
            UITheme.DrawGlow(new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16),
                new Color(rarity.Color.r, rarity.Color.g, rarity.Color.b, 0.25f));
        GUI.Box(r, "", UITheme.CardStyle);

        // Rarity stripe + colour swatch with rarity ring.
        UITheme.RoundRect(new Rect(r.x + 4, r.y + 8, 4, r.height - 16), rarity.Color);
        UITheme.RoundRect(new Rect(r.x + 13, r.y + 11, 34, 34), rarity.Color);
        UITheme.RoundRect(new Rect(r.x + 15, r.y + 13, 30, 30), swatch);

        GUI.Label(new Rect(r.x + 58, r.y + 6, 220, 24), name, UITheme.SubtitleStyle);

        bool owned = EconomySystem.IsOwned(id, isSkin);
        bool equipped = id == equippedId;

        if (rarityStyle == null)
            rarityStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 12, fontStyle = FontStyle.Bold };
        rarityStyle.normal.textColor = rarity.Color;
        string second = owned
            ? rarity.Name + (equipped ? "  ·  Aktiv" : "  ·  Gekauft")
            : rarity.Name + "  ·  ● " + price;
        GUI.Label(new Rect(r.x + 58, r.y + 30, 240, 20), second, rarityStyle);

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

    static GUIStyle rarityStyle;
    static GUIStyle statusStyle;
    float purchaseFlashTime = -9f;

    void Buy(string id, int price, bool isSkin, string name)
    {
        if (EconomySystem.TryPurchase(id, price, isSkin))
        {
            AudioManager.Instance?.PlayCoin();
            SetStatus(name + " gekauft und ausgerüstet!", true);
            purchaseFlashTime = Time.time; // gold celebration flash over the preview
            Equip(id, isSkin);
        }
        else
        {
            AudioManager.Instance?.PlayClick();
            SetStatus("Nicht genug Münzen für " + name + ".", false);
        }
    }

    void Equip(string id, bool isSkin)
    {
        AudioManager.Instance?.PlayClick();
        EconomySystem.Equip(id, isSkin);
        var pc = LocalCosmetics();
        if (pc != null)
            pc.SetEquipped(EconomySystem.EquippedSkin, EconomySystem.EquippedAccessory);
    }

    PlayerCosmetics LocalCosmetics()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            return GameManager.Instance.player.GetComponent<PlayerCosmetics>();
        return null;
    }
}
