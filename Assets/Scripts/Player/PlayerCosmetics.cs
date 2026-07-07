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

    void ApplySkin(string skinId)
    {
        if (skinMaterials == null)
            return;
        if (!CosmeticsCatalog.TryGetSkin(skinId, out SkinDef skin))
            return;

        foreach (var mat in skinMaterials)
        {
            if (mat == null)
                continue;

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", skin.BaseColor);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", skin.BaseColor);

            if (skin.HasEmission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", skin.EmissionColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    void ApplyEffect(string effectId)
    {
        if (trail == null)
            return;

        if (!CosmeticsCatalog.TryGetEffect(effectId, out EffectDef effect) || effect.Id == "none")
        {
            trail.emitting = false;
            trail.enabled = false;
            return;
        }

        trail.enabled = true;
        trail.emitting = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(effect.ColorA, 0f),
                new GradientColorKey(effect.ColorB, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        trail.colorGradient = gradient;
    }
}
