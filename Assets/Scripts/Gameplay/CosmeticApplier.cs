using UnityEngine;

// Shared skin/effect application used by BOTH the live player (PlayerCosmetics) and the shop's
// preview (CosmeticPreview), so what you see in the preview is exactly what you get in game. Purely
// visual: skin recolors a set of per-instance materials, effect drives a trail + particle system.
public static class CosmeticApplier
{
    // Applies a character texture (+ optional tint) to a set of material instances - a skin is a
    // whole different character look, not a recolor.
    public static void ApplySkinTexture(Material[] mats, Texture2D texture, Color tint)
    {
        if (mats == null)
            return;
        Color t = tint.a <= 0f ? Color.white : tint;
        foreach (var mat in mats)
        {
            if (mat == null)
                continue;
            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", t);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", t);
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

    // Configures a character's themed skin aura (fire/ice/lightning/nature/shadow). Empty theme
    // disables it. Uses the soft glow sprite so it reads as real fire/ice/etc., not blocky pixels.
    public static void ApplyAura(ParticleSystem aura, string theme)
    {
        if (aura == null)
            return;

        var emission = aura.emission;
        if (string.IsNullOrEmpty(theme))
        {
            emission.rateOverTime = 0f;
            aura.Clear();
            aura.Stop();
            return;
        }

        var main = aura.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var shape = aura.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        var rot = aura.rotationOverLifetime;
        var sol = aura.sizeOverLifetime;
        var psr = aura.GetComponent<ParticleSystemRenderer>();

        // Per-theme look. Direction is driven by gravity (negative rises) + speed, so no orientation
        // juggling is needed.
        Color a, b;
        float size, speed, life, gravity, rate, spinDeg, endSize, radius;
        bool additive;
        switch (theme)
        {
            case "fire": // rising glowing embers
                a = new Color(1f, 0.72f, 0.16f); b = new Color(0.9f, 0.12f, 0f);
                size = 0.22f; speed = 1.6f; life = 0.7f; gravity = -0.55f; rate = 40f; spinDeg = 60f; endSize = 0.1f; radius = 0.3f; additive = true;
                break;
            case "ice": // slow falling frost sparkles
                a = new Color(0.85f, 0.97f, 1f); b = new Color(0.35f, 0.65f, 0.95f);
                size = 0.14f; speed = 0.4f; life = 1.5f; gravity = 0.5f; rate = 26f; spinDeg = 0f; endSize = 0.7f; radius = 0.42f; additive = true;
                break;
            case "lightning": // fast erratic bright sparks
                a = new Color(1f, 1f, 0.6f); b = new Color(0.7f, 0.85f, 1f);
                size = 0.09f; speed = 3.4f; life = 0.3f; gravity = 0f; rate = 55f; spinDeg = 720f; endSize = 0.05f; radius = 0.35f; additive = true;
                break;
            case "nature": // gently tumbling leaves
                a = new Color(0.4f, 0.8f, 0.3f); b = new Color(0.7f, 0.6f, 0.25f);
                size = 0.16f; speed = 0.5f; life = 1.7f; gravity = 0.12f; rate = 16f; spinDeg = 120f; endSize = 1f; radius = 0.45f; additive = false;
                break;
            default: // "shadow" - slow rising dark smoke
                a = new Color(0.35f, 0.12f, 0.55f); b = new Color(0.06f, 0.04f, 0.1f);
                size = 0.34f; speed = 0.5f; life = 1.4f; gravity = -0.2f; rate = 22f; spinDeg = 40f; endSize = 1.7f; radius = 0.4f; additive = false;
                break;
        }

        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.startSize = size;
        main.startSpeed = speed;
        main.startLifetime = life;
        main.gravityModifier = gravity;
        emission.rateOverTime = rate;
        shape.radius = radius;
        rot.enabled = Mathf.Abs(spinDeg) > 0.01f;
        rot.z = new ParticleSystem.MinMaxCurve(spinDeg * Mathf.Deg2Rad);
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, Mathf.Max(0.01f, endSize)));
        if (psr != null)
            psr.material = additive ? AdditiveMat : AlphaMat;

        if (!aura.isPlaying)
            aura.Play();
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
