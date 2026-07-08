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
    public Transform accessoryAnchor;       // sits at the head; worn accessories attach here
    public ParticleSystem skinAura;
    public PlayerController playerController;

    private readonly NetworkVariable<FixedString32Bytes> equippedSkin = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<FixedString32Bytes> equippedAccessory = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Material charMat;
    private GameObject currentAccessory;

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
        equippedAccessory.OnValueChanged += (_, v) => ApplyAccessory(v.ToString());

        if (IsOwner)
        {
            equippedSkin.Value = EconomySystem.EquippedSkin;
            equippedAccessory.Value = EconomySystem.EquippedAccessory;
        }

        ApplySkin(equippedSkin.Value.ToString());
        ApplyAccessory(equippedAccessory.Value.ToString());
    }

    // Called by the shop (owner only) so the change takes effect live and syncs to others.
    public void SetEquipped(string skinId, string accessoryId)
    {
        if (!IsOwner)
            return;
        equippedSkin.Value = skinId;
        equippedAccessory.Value = accessoryId;
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

    void ApplyAccessory(string accessoryId)
    {
        if (currentAccessory != null)
            Destroy(currentAccessory);

        CosmeticsCatalog.TryGetAccessory(accessoryId, out AccessoryDef def);
        currentAccessory = CosmeticApplier.BuildAccessory(def);
        if (currentAccessory != null && accessoryAnchor != null)
        {
            currentAccessory.transform.SetParent(accessoryAnchor, false);
            currentAccessory.transform.localPosition = Vector3.zero;
            currentAccessory.transform.localRotation = Quaternion.identity;
            SetLayerRecursive(currentAccessory, accessoryAnchor.gameObject.layer);
        }
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
