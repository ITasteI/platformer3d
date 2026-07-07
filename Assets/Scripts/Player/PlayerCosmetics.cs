using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Networked cosmetic state for a player: equipped skin (which character model + recolor) and effect
// (trail + particles). Owner writes from EconomySystem; everyone reads and applies, so remote players
// see each other's cosmetics. Purely visual - never touches gameplay values.
//
// Skins can be genuinely different character MESHES: the prefab holds every referenced Kenney model
// (they share one rig, so a single animator controller drives all of them). Only one is active at a
// time; the animator reference on the PlayerController is retargeted to the active model.
public class PlayerCosmetics : NetworkBehaviour
{
    // Wired by SceneBuilder. Parallel arrays: character model object + its Kenney mesh id.
    public GameObject[] modelObjects;
    public string[] modelIds;
    public TrailRenderer trail;
    public ParticleSystem effectParticles;
    public ParticleSystem skinAura;   // themed aura tied to the equipped skin (fire/ice/...)
    public PlayerController playerController;

    private readonly NetworkVariable<FixedString32Bytes> equippedSkin = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<FixedString32Bytes> equippedEffect = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Material[][] modelMaterials;   // per-model per-instance material clones
    private Animator[] modelAnimators;
    private int activeModel = -1;

    void Awake()
    {
        CacheModels();
    }

    void CacheModels()
    {
        if (modelMaterials != null || modelObjects == null)
            return;

        modelMaterials = new Material[modelObjects.Length][];
        modelAnimators = new Animator[modelObjects.Length];
        for (int i = 0; i < modelObjects.Length; i++)
        {
            if (modelObjects[i] == null)
            {
                modelMaterials[i] = new Material[0];
                continue;
            }

            modelAnimators[i] = modelObjects[i].GetComponent<Animator>();
            var renderers = modelObjects[i].GetComponentsInChildren<Renderer>(true);
            var mats = new Material[renderers.Length];
            for (int r = 0; r < renderers.Length; r++)
            {
                // Per-instance clone so recoloring one player never tints the others.
                mats[r] = new Material(renderers[r].sharedMaterial);
                renderers[r].material = mats[r];
            }
            modelMaterials[i] = mats;
        }
    }

    public override void OnNetworkSpawn()
    {
        CacheModels();

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
        if (modelObjects == null || modelObjects.Length == 0)
            return;

        CosmeticsCatalog.TryGetSkin(skinId, out SkinDef skin);
        int index = FindModel(skin.Model);

        // Swap the active character model + retarget the animator.
        if (index != activeModel)
        {
            for (int i = 0; i < modelObjects.Length; i++)
                if (modelObjects[i] != null)
                    modelObjects[i].SetActive(i == index);
            activeModel = index;

            if (playerController != null && modelAnimators[index] != null)
                playerController.animator = modelAnimators[index];
        }

        // Recolor the active model + drive its themed aura.
        CosmeticApplier.ApplySkin(modelMaterials[index], skinId);
        CosmeticApplier.ApplyAura(skinAura, skin.AuraTheme);
    }

    int FindModel(string modelId)
    {
        if (!string.IsNullOrEmpty(modelId) && modelIds != null)
            for (int i = 0; i < modelIds.Length; i++)
                if (modelIds[i] == modelId)
                    return i;
        return 0; // default to the first (base) model
    }

    void ApplyEffect(string effectId) => CosmeticApplier.ApplyEffect(trail, effectParticles, effectId);
}
