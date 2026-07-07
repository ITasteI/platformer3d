using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Networked cosmetic state for a player: equipped skin (recolors the character) and equipped
// effect (a trail). Owner writes from EconomySystem; everyone reads and applies, so remote
// players see each other's cosmetics. Purely visual - never touches gameplay values.
public class PlayerCosmetics : NetworkBehaviour
{
    // Wired by SceneBuilder when the prefab is built.
    public Transform characterVisual;
    public TrailRenderer trail;
    public ParticleSystem effectParticles;

    private readonly NetworkVariable<FixedString32Bytes> equippedSkin = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<FixedString32Bytes> equippedEffect = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Renderer[] visualRenderers;
    private Material[] skinMaterials;

    void Awake()
    {
        CacheVisual();
    }

    void CacheVisual()
    {
        if (visualRenderers != null || characterVisual == null)
            return;

        visualRenderers = characterVisual.GetComponentsInChildren<Renderer>();
        // Per-instance material clones so recoloring one player never tints the others.
        skinMaterials = new Material[visualRenderers.Length];
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            skinMaterials[i] = new Material(visualRenderers[i].sharedMaterial);
            visualRenderers[i].material = skinMaterials[i];
        }
    }

    public override void OnNetworkSpawn()
    {
        CacheVisual();

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

    void ApplySkin(string skinId) => CosmeticApplier.ApplySkin(skinMaterials, skinId);
    void ApplyEffect(string effectId) => CosmeticApplier.ApplyEffect(trail, effectParticles, effectId);
}
