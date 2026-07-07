using UnityEngine;

// Shared skin/effect application used by BOTH the live player (PlayerCosmetics) and the shop's
// preview (CosmeticPreview), so what you see in the preview is exactly what you get in game. Purely
// visual: skin recolors a set of per-instance materials, effect drives a trail + particle system.
public static class CosmeticApplier
{
    public static void ApplySkin(Material[] skinMaterials, string skinId)
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

    public static void ApplyEffect(TrailRenderer trail, ParticleSystem effectParticles, string effectId)
    {
        bool has = CosmeticsCatalog.TryGetEffect(effectId, out EffectDef effect) && effect.Id != "none";

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

                var shape = effectParticles.shape;
                shape.enabled = true;
                shape.shapeType = effect.Shape;

                var rot = effectParticles.rotationOverLifetime;
                rot.enabled = Mathf.Abs(effect.SpinDegPerSec) > 0.01f;
                rot.z = new ParticleSystem.MinMaxCurve(effect.SpinDegPerSec * Mathf.Deg2Rad);

                var sol = effectParticles.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, Mathf.Max(0.01f, effect.EndSizeMul)));

                var psr = effectParticles.GetComponent<ParticleSystemRenderer>();
                if (psr != null)
                    psr.material = effect.Additive ? AdditiveMat : AlphaMat;

                if (!effectParticles.isPlaying)
                    effectParticles.Play();
            }
        }
    }

    // Shared particle blend materials (color comes from the system's startColor, so a single white
    // alpha + additive material serves every effect).
    static Material alphaMat;
    static Material additiveMat;
    static Material AlphaMat => alphaMat != null ? alphaMat : (alphaMat = BuildParticleMaterial(false));
    static Material AdditiveMat => additiveMat != null ? additiveMat : (additiveMat = BuildParticleMaterial(true));

    // Soft round glow sprite so particles read as smooth puffs/sparks instead of hard pixel squares.
    static Texture2D softSprite;
    static Texture2D SoftSprite => softSprite != null ? softSprite : (softSprite = BuildSoftSprite());

    static Texture2D BuildSoftSprite()
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Vector2 c = new Vector2((S - 1) / 2f, (S - 1) / 2f);
        float maxR = (S - 1) / 2f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a *= a; // smooth quadratic falloff to a soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Material BuildParticleMaterial(bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        var m = new Material(shader);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_BaseMap"))
            m.SetTexture("_BaseMap", SoftSprite);
        if (m.HasProperty("_MainTex"))
            m.SetTexture("_MainTex", SoftSprite);
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
