using UnityEngine;

// Shared skin/accessory application used by BOTH the live player (PlayerCosmetics) and the shop's
// preview (CosmeticPreview), so what you see in the preview is exactly what you get in game. Purely
// visual: skin re-textures the character, accessory attaches a procedural hat/crown to the head.
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

    // Builds a procedural head accessory as a GameObject whose origin sits at the head anchor (y=0 is
    // the crown of the head). Returns null for "none". All parts are simple lit primitives / small
    // procedural meshes in the item's colors, so it stays in the low-poly style with no external art.
    public static GameObject BuildAccessory(AccessoryDef def)
    {
        if (def.Kind == AccessoryKind.None)
            return null;

        var root = new GameObject("Accessory_" + def.Id);
        bool glow = def.Kind == AccessoryKind.Halo;
        Material main = AccMat(def.Color, glow);
        Material trim = AccMat(def.Color2, glow || def.Kind == AccessoryKind.Crown);

        switch (def.Kind)
        {
            case AccessoryKind.Cap:
                AddBall(root, main, new Vector3(0f, -0.04f, 0f), new Vector3(0.28f, 0.22f, 0.28f));   // dome
                AddBox(root, main, new Vector3(0f, -0.05f, 0.16f), new Vector3(0.26f, 0.03f, 0.18f)); // brim
                AddBall(root, trim, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.05f, 0.05f));    // button
                break;

            case AccessoryKind.TopHat:
                AddCyl(root, main, new Vector3(0f, -0.01f, 0f), 0.40f, 0.03f);   // brim
                AddCyl(root, main, new Vector3(0f, 0.10f, 0f), 0.25f, 0.20f);    // crown
                AddCyl(root, trim, new Vector3(0f, 0.02f, 0f), 0.265f, 0.04f);   // band
                break;

            case AccessoryKind.Crown:
                AddCyl(root, main, new Vector3(0f, 0.03f, 0f), 0.28f, 0.12f);    // band
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f;
                    Vector3 p = new Vector3(Mathf.Sin(a) * 0.13f, 0.09f, Mathf.Cos(a) * 0.13f);
                    AddCone(root, main, p, 0.04f, 0.11f, Vector3.zero);
                    AddBall(root, trim, p + Vector3.up * 0.11f, new Vector3(0.045f, 0.045f, 0.045f)); // gems on tips
                }
                break;

            case AccessoryKind.PartyHat:
                AddCone(root, main, new Vector3(0f, -0.02f, 0f), 0.24f, 0.46f, Vector3.zero);
                AddBall(root, trim, new Vector3(0f, 0.44f, 0f), new Vector3(0.09f, 0.09f, 0.09f));    // pom
                break;

            case AccessoryKind.WizardHat:
                AddCyl(root, main, new Vector3(0f, -0.01f, 0f), 0.42f, 0.03f);   // brim
                AddCone(root, main, new Vector3(0f, 0.0f, 0f), 0.22f, 0.54f, Vector3.zero);
                for (int i = 0; i < 4; i++)
                    AddBall(root, trim, new Vector3(Mathf.Sin(i * 1.7f) * 0.12f, 0.14f + i * 0.09f, Mathf.Cos(i * 1.7f) * 0.12f), new Vector3(0.045f, 0.045f, 0.045f));
                break;

            case AccessoryKind.Helmet:
                AddBall(root, main, new Vector3(0f, -0.03f, 0f), new Vector3(0.32f, 0.28f, 0.32f));   // dome
                AddCyl(root, trim, new Vector3(0f, 0.05f, 0f), 0.34f, 0.045f);                        // rim
                AddCone(root, trim, new Vector3(-0.17f, 0.05f, 0f), 0.055f, 0.2f, new Vector3(0f, 0f, 35f));  // left horn
                AddCone(root, trim, new Vector3(0.17f, 0.05f, 0f), 0.055f, 0.2f, new Vector3(0f, 0f, -35f));  // right horn
                break;

            case AccessoryKind.Halo:
                var ring = new GameObject("halo");
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.24f, 0f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.AddComponent<MeshFilter>().sharedMesh = TorusMesh(0.15f, 0.028f, 22, 10);
                ring.AddComponent<MeshRenderer>().sharedMaterial = main;
                break;
        }
        return root;
    }

    // --- procedural part helpers -------------------------------------------------------------

    static void AddPrimitive(GameObject parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        var col = go.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Object.Destroy(col); }
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // Cylinder helper by real diameter + height (Unity's cylinder primitive is 2 units tall).
    static void AddCyl(GameObject p, Material m, Vector3 center, float diameter, float height)
        => AddPrimitive(p, PrimitiveType.Cylinder, m, center, new Vector3(diameter, height * 0.5f, diameter));

    static void AddBall(GameObject p, Material m, Vector3 center, Vector3 diameters)
        => AddPrimitive(p, PrimitiveType.Sphere, m, center, diameters);

    static void AddBox(GameObject p, Material m, Vector3 center, Vector3 size)
        => AddPrimitive(p, PrimitiveType.Cube, m, center, size);

    // Cone with its base at basePos (tip pointing up +Y before the optional euler rotation).
    static void AddCone(GameObject parent, Material mat, Vector3 basePos, float radius, float height, Vector3 euler)
    {
        var go = new GameObject("cone");
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = basePos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<MeshFilter>().sharedMesh = ConeMesh(radius, height, 14);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Mesh ConeMesh(float radius, float height, int segments)
    {
        var verts = new Vector3[segments + 2];
        verts[0] = Vector3.zero;                 // base center
        verts[1] = new Vector3(0f, height, 0f);  // tip
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            verts[2 + i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < segments; i++)
        {
            int cur = 2 + i;
            int next = 2 + (i + 1) % segments;
            tris.Add(1); tris.Add(next); tris.Add(cur);   // side
            tris.Add(0); tris.Add(cur); tris.Add(next);   // base
        }
        var mesh = new Mesh();
        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh TorusMesh(float majorR, float minorR, int majorSeg, int minorSeg)
    {
        var verts = new Vector3[majorSeg * minorSeg];
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < majorSeg; i++)
        {
            float u = i / (float)majorSeg * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(u) * majorR, Mathf.Sin(u) * majorR, 0f);
            for (int j = 0; j < minorSeg; j++)
            {
                float v = j / (float)minorSeg * Mathf.PI * 2f;
                Vector3 normal = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(u) * Mathf.Cos(v), Mathf.Sin(v));
                verts[i * minorSeg + j] = center + normal * minorR;
            }
        }
        for (int i = 0; i < majorSeg; i++)
        {
            for (int j = 0; j < minorSeg; j++)
            {
                int a = i * minorSeg + j;
                int b = ((i + 1) % majorSeg) * minorSeg + j;
                int c = ((i + 1) % majorSeg) * minorSeg + (j + 1) % minorSeg;
                int d = i * minorSeg + (j + 1) % minorSeg;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }
        }
        var mesh = new Mesh();
        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material AccMat(Color c, bool emissive)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.6f);
        }
        return m;
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
    // alpha + additive material serves every aura).
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
