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
        bool has = CosmeticsCatalog.TryGetEffect(effectId, out EffectDef effect) && effect.Id != "none";

        // Trail
        if (trail != null)
        {
            if (!has || effect.TrailWidth <= 0f)
            {
                trail.emitting = false;
                trail.enabled = false;
            }
            else
            {
                trail.enabled = true;
                trail.emitting = true;
                trail.time = effect.TrailTime;
                trail.startWidth = effect.TrailWidth;
                trail.endWidth = 0f;

                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(effect.ColorA, 0f), new GradientColorKey(effect.ColorB, 1f) },
                    new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = gradient;
            }
        }

        // Particles - each effect gets its own colour/size/speed/gravity/rate PLUS emission shape,
        // spin, size-over-life and additive glow, so they read as genuinely different rewards.
        if (effectParticles != null)
        {
            var emission = effectParticles.emission;
            if (!has || effect.ParticleRate <= 0f)
            {
                emission.rateOverTime = 0f;
                effectParticles.Clear();
                effectParticles.Stop();
            }
            else
            {
                var main = effectParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(effect.ColorA, effect.ColorB);
                main.startSize = effect.ParticleSize;
                main.startSpeed = effect.ParticleSpeed;
                main.startLifetime = effect.ParticleLife;
                main.gravityModifier = effect.ParticleGravity;
                emission.rateOverTime = effect.ParticleRate;

                // Emission shape (cone/sphere/circle/hemisphere) gives each effect a distinct spread.
                var shape = effectParticles.shape;
                shape.enabled = true;
                shape.shapeType = effect.Shape;

                // Spin over lifetime: crackling sparks vs. lazy swirl.
                var rot = effectParticles.rotationOverLifetime;
                rot.enabled = Mathf.Abs(effect.SpinDegPerSec) > 0.01f;
                rot.z = new ParticleSystem.MinMaxCurve(effect.SpinDegPerSec * Mathf.Deg2Rad);

                // Size over lifetime: embers shrink, puffs grow, twinkles fade out.
                var sol = effectParticles.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, Mathf.Max(0.01f, effect.EndSizeMul)));

                ApplyParticleBlend(effect.Additive);

                if (!effectParticles.isPlaying)
                    effectParticles.Play();
            }
        }
    }

    // Swaps the particle renderer between soft alpha and glowing additive blending. Materials are
    // built once and reused; degrades gracefully if a shader property isn't present.
    private Material alphaParticleMat;
    private Material additiveParticleMat;

    void ApplyParticleBlend(bool additive)
    {
        var psr = effectParticles.GetComponent<ParticleSystemRenderer>();
        if (psr == null)
            return;

        if (additive)
        {
            if (additiveParticleMat == null)
                additiveParticleMat = BuildParticleMaterial(true);
            psr.material = additiveParticleMat;
        }
        else
        {
            if (alphaParticleMat == null)
                alphaParticleMat = BuildParticleMaterial(false);
            psr.material = alphaParticleMat;
        }
    }

    static Material BuildParticleMaterial(bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        var m = new Material(shader);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", Color.white);
        // URP Particles/Unlit: _Surface 1 = Transparent, _Blend 0 = Alpha, 1 = Additive.
        if (m.HasProperty("_Surface"))
            m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend"))
            m.SetFloat("_Blend", additive ? 1f : 0f);
        if (additive)
        {
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return m;
    }
}
