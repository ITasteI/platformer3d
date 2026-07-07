using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Networked cosmetic state for a player. The base character is one humanoid Kenney mesh; each
// "skin" is a whole different character LOOK via its own texture (skaterMale, cyborg, ...), plus an
// optional themed aura. Effects (trail + particles) are a separate slot. Owner writes from the shop;
// everyone reads and applies, so remote players see each other's cosmetics. Purely visual.
public class PlayerCosmetics : NetworkBehaviour
{
    // Wired by SceneBuilder.
    public Renderer characterRenderer;      // the humanoid's SkinnedMeshRenderer
    public Texture2D[] skinTextures;
    public string[] skinTextureIds;         // parallel to skinTextures (PNG name = SkinDef.Texture)
    public TrailRenderer trail;
    public ParticleSystem effectParticles;
    public ParticleSystem skinAura;
    public PlayerController playerController;

    private readonly NetworkVariable<FixedString32Bytes> equippedSkin = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<FixedString32Bytes> equippedEffect = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Material charMat;

    void Awake()
    {
        CacheMaterial();
    }

    void CacheMaterial()
    {
        if (charMat != null || characterRenderer == null)
            return;
        // Per-instance clone so re-texturing one player never changes the others.
        charMat = new Material(characterRenderer.sharedMaterial);
        characterRenderer.material = charMat;
    }

    public override void OnNetworkSpawn()
    {
        CacheMaterial();

        equippedSkin.OnValueChanged += (_, v) => ApplySkin(v.ToString());
        equippedEffect.OnValueChanged += (_, v) => ApplyEffect(v.ToString());

        if (IsOwner)
        {
            equippedSkin.Value = EconomySystem.EquippedSkin;
            equippedEffect.Value = EconomySystem.EquippedEffect;
        }

        ApplySkin(equippedSkin.Value.ToString());
        ApplyEffect(equippedEffect.Value.ToString());
    }

    // Called by the shop (owner only) so the change takes effect live and syncs to others.
    public void SetEquipped(string skinId, string effectId)
    {
        if (!IsOwner)
            return;
        equippedSkin.Value = skinId;
        equippedEffect.Value = effectId;
    }

    void ApplySkin(string skinId)
    {
        if (charMat == null)
            return;

        CosmeticsCatalog.TryGetSkin(skinId, out SkinDef skin);

        Texture2D tex = FindTexture(skin.Texture);
        if (tex != null)
        {
            if (charMat.HasProperty("_BaseMap")) charMat.SetTexture("_BaseMap", tex);
            if (charMat.HasProperty("_MainTex")) charMat.SetTexture("_MainTex", tex);
        }
        Color tint = skin.Tint.a <= 0f ? Color.white : skin.Tint;
        if (charMat.HasProperty("_BaseColor")) charMat.SetColor("_BaseColor", tint);

        CosmeticApplier.ApplyAura(skinAura, skin.AuraTheme);
    }

    Texture2D FindTexture(string texName)
    {
        if (!string.IsNullOrEmpty(texName) && skinTextureIds != null)
            for (int i = 0; i < skinTextureIds.Length; i++)
                if (skinTextureIds[i] == texName && i < skinTextures.Length)
                    return skinTextures[i];
        return skinTextures != null && skinTextures.Length > 0 ? skinTextures[0] : null;
    }

    void ApplyEffect(string effectId) => CosmeticApplier.ApplyEffect(trail, effectParticles, effectId);
}
