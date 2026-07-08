using System.Collections.Generic;
using UnityEngine;

// Live shop preview: a small rotating humanoid character on its own layer, rendered by a dedicated
// camera into a RenderTexture that the shop draws. Swaps the character texture (skin) + aura + effect
// exactly like the real player, so the preview matches in-game. Only renders while the shop is open.
public class CosmeticPreview : MonoBehaviour
{
    public static CosmeticPreview Instance { get; private set; }

    // Wired by SceneBuilder.
    public Transform accessoryAnchor;
    public ParticleSystem skinAura;
    public Camera previewCamera;
    public Texture2D[] skinTextures;
    public string[] skinTextureIds;

    public RenderTexture Texture { get; private set; }

    private Material[] skinMaterials;
    private string curSkin = " ";
    private string curAccessory = " ";
    private GameObject currentAccessory;

    void Awake()
    {
        Instance = this;

        Texture = new RenderTexture(320, 430, 16) { name = "CosmeticPreviewRT" };
        if (previewCamera != null)
        {
            previewCamera.targetTexture = Texture;
            previewCamera.enabled = false;
        }

        // Clone the character's materials so re-texturing the preview never touches the shared asset.
        var mats = new List<Material>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is TrailRenderer || r is ParticleSystemRenderer)
                continue;
            var m = new Material(r.sharedMaterial);
            r.material = m;
            mats.Add(m);
        }
        skinMaterials = mats.ToArray();
    }

    // Called by the shop each frame with the skin/accessory to show (only re-applies on change).
    public void SetPreview(string skinId, string accessoryId)
    {
        if (skinId != curSkin)
        {
            curSkin = skinId;
            CosmeticsCatalog.TryGetSkin(skinId, out SkinDef skin);
            CosmeticApplier.ApplySkinTexture(skinMaterials, FindTexture(skin.Texture), skin.Tint);
            CosmeticApplier.ApplyAura(skinAura, skin.AuraTheme);
        }
        if (accessoryId != curAccessory)
        {
            curAccessory = accessoryId;
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
    }

    // ---- Action-effect preview: REAL particles on the preview stage --------------------------
    // The preview camera renders every frame while the shop is open, so a small looping fountain
    // in the effect's colour shows the tint honestly (a GUI glow can't - the RT covers it).
    private ParticleSystem fxPreview;
    private string curFx = " ";

    public void SetPreviewFx(string fxId)
    {
        if (fxId == curFx)
            return;
        curFx = fxId;
        EnsureFxSystem();
        if (fxPreview == null)
            return;

        CosmeticsCatalog.TryGetActionFx(fxId, out ActionFxDef fx);
        if (fx.Id == "none")
        {
            fxPreview.Stop();
            fxPreview.Clear();
            return;
        }

        var main = fxPreview.main;
        Color c = fx.Color;
        c.a = 0.85f;
        main.startColor = c;
        fxPreview.Clear();
        fxPreview.Play();
    }

    void EnsureFxSystem()
    {
        if (fxPreview != null)
            return;

        var go = new GameObject("FxPreview");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // cone points up
        go.layer = gameObject.layer; // preview layer, so only the preview camera sees it

        fxPreview = go.AddComponent<ParticleSystem>();
        var main = fxPreview.main;
        main.loop = true;
        main.startLifetime = 0.9f;
        main.startSpeed = 1.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var em = fxPreview.emission;
        em.rateOverTime = 26f;
        var shape = fxPreview.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.28f;

        // Reuse the game's own sparkle material (soft round additive texture) so the preview
        // particles look exactly like the in-game ones - just tinted.
        var rend = go.GetComponent<ParticleSystemRenderer>();
        var sparkle = EffectsManager.Instance != null ? EffectsManager.Instance.sparkleTemplate : null;
        var srcRend = sparkle != null ? sparkle.GetComponent<ParticleSystemRenderer>() : null;
        if (srcRend != null && srcRend.sharedMaterial != null)
        {
            rend.sharedMaterial = srcRend.sharedMaterial;
        }
        else
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            rend.material = new Material(sh);
        }

        fxPreview.Stop();
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    Texture2D FindTexture(string texName)
    {
        if (!string.IsNullOrEmpty(texName) && skinTextureIds != null)
            for (int i = 0; i < skinTextureIds.Length; i++)
                if (skinTextureIds[i] == texName && i < skinTextures.Length)
                    return skinTextures[i];
        return skinTextures != null && skinTextures.Length > 0 ? skinTextures[0] : null;
    }

    void Update()
    {
        bool shopOpen = MainMenu.Current == MenuScreen.Shop;
        if (previewCamera != null)
            previewCamera.enabled = shopOpen;
        if (shopOpen)
            transform.Rotate(0f, 38f * Time.deltaTime, 0f);
    }
}
