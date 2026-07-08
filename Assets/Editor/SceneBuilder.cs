using System.IO;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class SceneBuilder
{
    const int PlatformCount = 660;
    // Endless mode continues the SAME climb four more times over (5x total). It's baked but parented
    // under a toggle root, so only Endless switches it on - every other mode plays the unchanged base.
    const int EndlessExtraPlatforms = PlatformCount * 4;
    const float TopHeight = 500f;
    // Real height the generated tower reaches (measured after CreateTower). Zone atmosphere,
    // music, stars and the HUD all key off this so all five worlds are actually reached,
    // instead of the old fixed 500 that the ~240m tower never climbed to.
    static float actualTopHeight = TopHeight;
    // Endless-mode extension, filled in by CreateTower: the toggle root holding the extra 4x of tower
    // (+ its summit flag), and the base/endless summit positions. EndlessWorld reads these at runtime.
    static GameObject towerEndlessRoot;
    static Vector3 baseTopPos;
    static Vector3 endlessTopPos;
    const string KitPath = "Assets/KenneyKit/";
    const string NatureKitPath = "Assets/NatureKit/";
    const string SpaceKitPath = "Assets/SpaceKit/";
    const string FurnitureKitPath = "Assets/FurnitureKit/";
    const string FreeNaturePath = "Assets/Assets Free Neu/";

    static Material spaceMaterial;

    static Material kenneyMaterial;

    static Material furnitureMaterial;

    static Material freeNatureMaterial;
    static int freeDebugCount;

    static void SetColor(GameObject go, Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void SetEmissive(GameObject go, Color baseColor, Color emissionColor)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", baseColor);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", emissionColor);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void FixTextureImportSettings(string texPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null && (importer.mipmapEnabled || importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp))
        {
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    static Material LoadOrCreateMaterial(ref Material cache, string assetPath, string texturePath, float smoothness)
    {
        if (cache != null)
            return cache;

        cache = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (cache != null)
            return cache;

        FixTextureImportSettings(texturePath);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        cache = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        cache.SetTexture("_BaseMap", tex);
        cache.SetFloat("_Smoothness", smoothness);
        cache.enableInstancing = true;
        AssetDatabase.CreateAsset(cache, assetPath);
        return cache;
    }

    static Material GetKenneyMaterial()
    {
        return LoadOrCreateMaterial(ref kenneyMaterial, "Assets/KenneyMaterial.mat", KitPath + "Textures/colormap.png", 0.15f);
    }

    static Material GetSpaceMaterial()
    {
        return LoadOrCreateMaterial(ref spaceMaterial, "Assets/SpaceMaterial.mat", SpaceKitPath + "Textures/colormap.png", 0.4f);
    }

    static Material GetFurnitureMaterial()
    {
        // Kenney's furniture kit ships no texture (empty Textures/ folders); it UV-maps to the same
        // shared Kenney palette, so the KenneyKit colormap (copied in) colours it correctly.
        return LoadOrCreateMaterial(ref furnitureMaterial, "Assets/FurnitureMaterial.mat", FurnitureKitPath + "colormap.png", 0.15f);
    }

    static GameObject InstantiateFurniture(string modelName, Vector3 pos)
    {
        return InstantiateModel(FurnitureKitPath, GetFurnitureMaterial(), modelName, pos);
    }

    // The new "Assets Free Neu" nature pack ships no textures - its colours live in VERTEX COLORS,
    // which stock URP/Lit ignores (models would be flat grey). This material uses the custom
    // TasteJump/VertexColorLit shader so the trees/bushes/flowers render in their real colours.
    static Material GetFreeNatureMaterial()
    {
        if (freeNatureMaterial != null)
            return freeNatureMaterial;
        freeNatureMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FreeNatureMaterial.mat");
        if (freeNatureMaterial != null)
            return freeNatureMaterial;

        // DIAGNOSTIC 2: dead-simple URP Unlit RED - always renders. If vegetation is still invisible,
        // the problem is placement/mesh, not the material.
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        freeNatureMaterial = new Material(sh);
        if (freeNatureMaterial.HasProperty("_BaseColor"))
            freeNatureMaterial.SetColor("_BaseColor", new Color(1f, 0f, 0f));
        if (freeNatureMaterial.HasProperty("_Cull"))
            freeNatureMaterial.SetInt("_Cull", 0); // Cull Off - test for inverted normals
        freeNatureMaterial.enableInstancing = true;
        AssetDatabase.CreateAsset(freeNatureMaterial, "Assets/FreeNatureMaterial.mat");
        return freeNatureMaterial;
    }

    // Instantiates a "Assets Free Neu" model, applies the vertex-color material and normalizes it to a
    // target HEIGHT (the pack imports at raw Blender scale), with its base grounded at pos.
    static GameObject InstantiateFreeNature(string modelName, Vector3 pos, float targetHeight)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FreeNaturePath + modelName + ".fbx");
        if (prefab == null)
            return null;
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.name = modelName;
        inst.transform.position = Vector3.zero;

        Material mat = GetFreeNatureMaterial();
        foreach (var r in inst.GetComponentsInChildren<Renderer>())
        {
            r.enabled = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; // FBX may import as ShadowsOnly
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.sharedMaterials = mats;
        }

        bool got1 = TryGetHierarchyBounds(inst, out Bounds b);
        if (got1 && b.size.y > 0.01f)
            inst.transform.localScale = Vector3.one * (targetHeight / b.size.y);

        // Ground the base at pos.y and centre the footprint at pos.x/z (measured after scaling).
        bool got2 = TryGetHierarchyBounds(inst, out Bounds b2);
        if (got2)
            inst.transform.position = new Vector3(pos.x - b2.center.x, pos.y - b2.min.y, pos.z - b2.center.z);
        else
            inst.transform.position = pos;

        if (freeDebugCount < 8)
        {
            freeDebugCount++;
            int rends = inst.GetComponentsInChildren<Renderer>().Length;
            Debug.Log($"[Free] {modelName} rends={rends} got1={got1} rawSize={(got1 ? b.size.ToString("0.00") : "?")} scale={inst.transform.localScale.x:0.0000} finalPos={inst.transform.position.ToString("0.0")}");
        }
        return inst;
    }

    // Dispatches to the right kit loader by a short kit tag ("k"=Kenney, "n"=Nature, "f"=Furniture,
    // "s"=Space) so a single eclectic prop list can pull from every pack.
    static GameObject InstantiateProp(string kit, string modelName, Vector3 pos)
    {
        switch (kit)
        {
            case "n": return InstantiateNature(modelName, pos);
            case "f": return InstantiateFurniture(modelName, pos);
            case "s": return InstantiateSpaceKit(modelName, pos);
            default: return InstantiateKenney(modelName, pos);
        }
    }

    // World-space AABB over all solid renderers (reliable for static meshes at build time, unlike
    // skinned meshes). Used to normalize wildly different props to a fair, consistent platform size.
    static bool TryGetHierarchyBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool has = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer)
                continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
    }

    static GameObject InstantiateModel(string basePath, Material mat, string modelName, Vector3 pos)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath + modelName + ".fbx");
        if (prefab == null)
            return null;
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = modelName;
        instance.transform.position = pos;

        foreach (var rend in instance.GetComponentsInChildren<Renderer>())
        {
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            rend.sharedMaterials = mats;
        }

        return instance;
    }

    static GameObject InstantiateKenney(string modelName, Vector3 pos)
    {
        return InstantiateModel(KitPath, GetKenneyMaterial(), modelName, pos);
    }

    static GameObject InstantiateSpaceKit(string modelName, Vector3 pos)
    {
        return InstantiateModel(SpaceKitPath, GetSpaceMaterial(), modelName, pos);
    }

    static GameObject InstantiateNature(string modelName, Vector3 pos)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NatureKitPath + modelName + ".fbx");
        if (prefab == null)
            return null;
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = modelName;
        instance.transform.position = pos;
        return instance;
    }

    static BoxCollider AddSolidCollider(GameObject go)
    {
        BoxCollider box = go.AddComponent<BoxCollider>();
        ColliderUtil.FitToRenderBounds(go.transform, box);
        return box;
    }

    static void SetupRenderPipeline()
    {
        const string rendererPath = "Assets/URPRendererData.asset";
        const string pipelinePath = "Assets/URPAsset.asset";

        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, rendererPath);
        }

        UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (urpAsset == null)
        {
            urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urpAsset, pipelinePath);
        }

        bool hasSsao = false;
        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature is ScreenSpaceAmbientOcclusion)
            {
                hasSsao = true;
                break;
            }
        }
        if (!hasSsao)
        {
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            rendererData.rendererFeatures.Add(ssao);
            AssetDatabase.AddObjectToAsset(ssao, rendererData);
            rendererData.SetDirty();
        }

        // Shadow quality tuned for the tall night tower: 4 cascades over a longer distance keep
        // close-up shadows crisp while props much higher/further still cast; soft edges + 4k map.
        var urpSo = new SerializedObject(urpAsset);
        SetProp(urpSo, "m_ShadowDistance", p => p.floatValue = 160f);
        SetProp(urpSo, "m_ShadowCascadeCount", p => p.intValue = 4);
        SetProp(urpSo, "m_SoftShadowsSupported", p => p.boolValue = true);
        SetProp(urpSo, "m_MainLightShadowmapResolution", p => p.intValue = 4096);
        urpSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urpAsset);

        GraphicsSettings.defaultRenderPipeline = urpAsset;
        QualitySettings.renderPipeline = urpAsset;
    }

    static void SetProp(SerializedObject so, string name, System.Action<SerializedProperty> set)
    {
        var p = so.FindProperty(name);
        if (p != null)
            set(p);
        else
            Debug.LogWarning("[URP] serialized property not found: " + name);
    }

    // Lays ONE stylized look (TasteJump/StylizedLit: ramped light, night shadow tint, moon rim)
    // over every opaque prop/platform material, so the mixed asset packs read as one art style.
    // Skips water, characters, sky and everything emissive/transparent/unlit.
    static void UnifyPropMaterials()
    {
        Shader stylized = Shader.Find("TasteJump/StylizedLit");
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        Shader vertexColorLit = Shader.Find("TasteJump/VertexColorLit");
        if (stylized == null)
        {
            Debug.LogWarning("[Unify] TasteJump/StylizedLit not found - skipping unify pass");
            return;
        }

        var done = new HashSet<Material>();
        int count = 0;
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (var mat in mr.sharedMaterials)
            {
                if (mat == null || done.Contains(mat))
                    continue;
                bool wasVertexColor = vertexColorLit != null && mat.shader == vertexColorLit;
                if (mat.shader != urpLit && !wasVertexColor)
                    continue; // unlit glow, particles, skybox, custom shaders: keep as-is
                done.Add(mat);

                string mname = mat.name;
                if (mname.Contains("Water") || mname.Contains("Protagonist") || mname.Contains("Sky"))
                    continue;
                if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f)
                    continue; // transparent (ghosts, glass) keeps URP/Lit blending
                if (mat.IsKeywordEnabled("_EMISSION"))
                    continue; // emissive props keep their glow

                Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                Color baseCol = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                              : (mat.HasProperty("_Tint") ? mat.GetColor("_Tint") : Color.white);

                mat.shader = stylized;
                if (baseMap != null)
                    mat.SetTexture("_BaseMap", baseMap);
                mat.SetColor("_BaseColor", baseCol);
                // Vertex colors are opt-in: packs without the attribute would read garbage.
                mat.SetFloat("_VertexColor", wasVertexColor ? 1f : 0f);
                count++;
            }
        }
        Debug.Log($"[Unify] stylized {count} materials");
    }

    // Some imported asset packs (SimpleNaturePack, Skyden's Low Poly Environment) ship Built-in
    // Render Pipeline "Standard" shader materials by default, which render solid pink under URP.
    // Re-point them at URP/Lit, carrying over the main texture/color/metallic/smoothness.
    static void FixBuiltinStandardMaterials()
    {
        string[] folders = { "Assets/SimpleNaturePack", "Assets/Skyden_Games" };
        string[] guids = AssetDatabase.FindAssets("t:Material", folders);
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
            return;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null || mat.shader.name != "Standard")
                continue;

            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float glossiness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;

            mat.shader = urpLit;
            if (mainTex != null)
                mat.SetTexture("_BaseMap", mainTex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", glossiness);
        }

        AssetDatabase.SaveAssets();
    }

    static void SetupPostProcessing(Camera cam)
    {
        const string profilePath = "Assets/PostProcessProfile.asset";

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        else
        {
            profile.components.Clear();
        }

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(1.05f); // slightly stronger dreamy moonlit glow
        bloom.scatter.Override(0.82f);
        bloom.tint.Override(new Color(0.82f, 0.88f, 1f)); // cool blue glow around the moon/stars

        var colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.postExposure.Override(-0.1f);
        colorAdjustments.contrast.Override(15f);
        colorAdjustments.colorFilter.Override(new Color(0.72f, 0.79f, 1.06f)); // cool blue-violet night wash
        colorAdjustments.saturation.Override(-10f);                            // moonlight mutes colour, but keep props readable

        // White balance pushed cool + a hint of magenta for the blue-violet night mood (kept moderate
        // so it stacks with the colour filter without turning everything into blue soup).
        var whiteBalance = profile.Add<WhiteBalance>(true);
        whiteBalance.temperature.Override(-12f);
        whiteBalance.tint.Override(6f);

        // Shadows toward deep blue, highlights toward soft violet-white - classic moonlit grade.
        var smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.82f, 0.9f, 1.15f, 0f));
        smh.midtones.Override(new Vector4(0.95f, 0.97f, 1.05f, 0f));
        smh.highlights.Override(new Vector4(1.02f, 0.98f, 1.08f, 0f));

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.68f);
        vignette.color.Override(new Color(0.04f, 0.05f, 0.12f)); // deep blue night edges

        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        // Fine film grain: breaks up the flat night gradients ever so slightly - "filmic" texture.
        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.16f);
        grain.response.Override(0.7f);

        GameObject volumeGO = new GameObject("PostProcessVolume");
        Volume volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.profile = profile;

        var camData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null)
            camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Build Test Scene")]
    public static void BuildScene()
    {
        PlayerSettings.productName = "TasteJump";
        SetGameIcon();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        SetupRenderPipeline();
        FixBuiltinStandardMaterials();

        Light sunLight = CreateLight();
        CreateMoonDisc(sunLight);
        Camera lobbyCam = CreateLobbyCamera();
        // Layer 9 is the isolated cosmetic-preview stage; keep it out of the lobby camera's view.
        lobbyCam.cullingMask &= ~(1 << 9);
        SetupPostProcessing(lobbyCam);
        GameObject playerPrefab = CreatePlayerPrefab();
        CreateNetworkManagerAndLobby(playerPrefab, lobbyCam);
        CreateGround();
        CreateRiverWater();
        CreateLake();
        CreateNatureScatter();
        // ForestBelt + BonusNatureScatter disabled: the dense grid jungle (CreateNatureScatter) now
        // covers the ground, and those used the older Kenney/PurePoly style that clashed with the new
        // vertex-coloured pack. RockFormations (landmark peaks) stay.
        CreateRockFormations();
        CreateSpawnDecor();
        CreateJungleRuins();
        CreateGlowDecor();
        CreateGroundLanterns();
        CreatePathFlora();
        CreateRiverBridges();
        CreateStoneArch(0, 38f);
        CreateStoneArch(1, 52f);
        CreateMeadowBeauty();
        CreateButterflyPatch(new Vector3(12f, 0f, -9f));
        CreateButterflyPatch(new Vector3(-20f, 0f, 22f));
        CreateButterflyPatch(new Vector3(30f, 0f, 26f));
        CreateBackgroundIslands();
        CreateDistantLandmark();
        CreateMountainRing();
        // FOUR parkours - one per game mode - each baked as its own toggle root. ModeWorld
        // activates exactly one, so every mode is a genuinely different route (own direction,
        // pacing, danger and object mix) instead of the same tower with different rules.
        Vector3 topPos = GenerateTower(TowerConfig.Klassisch(), out GameObject towerK);
        Vector3 topZeit = GenerateTower(TowerConfig.Zeitrennen(), out GameObject towerZ);
        Vector3 topHard = GenerateTower(TowerConfig.Hardcore(), out GameObject towerH);
        Vector3 topEnd = GenerateTower(TowerConfig.Endlos(), out GameObject towerE);
        baseTopPos = topPos;
        actualTopHeight = topPos.y;       // environment (clouds/stars/aurora) anchors to Klassisch
        endlessTopPos = topEnd;
        towerEndlessRoot = towerE;        // the Endless atmosphere column parents here

        // Meadow intro pair: one melee wraith and one patroller (no shooter right at spawn).
        CreateEnemy(new Vector3(9f, SampleTerrainHeight(new Vector3(9f, 0f, 11f)) + 1.3f, 11f), 9001, EnemyKind.Wraith);
        CreateEnemy(new Vector3(-10f, SampleTerrainHeight(new Vector3(-10f, 0f, 7f)) + 1.3f, 7f), 9002, EnemyKind.Patrol);

        // TUTORIAL ZIPLINE right at the spawn meadow: a mast beside the footpath with a floating
        // treasure island hovering visibly over the grass - teaches the mechanic in minute one.
        // Kept SHORT (13 m) and near the centre so the whole run stays inside the boundary wall.
        Vector3 meadowAnchor = new Vector3(0f, 0f, 10f);
        meadowAnchor.y = SampleTerrainHeight(meadowAnchor) + 0.1f;
        CreateZipSecret(meadowAnchor, new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f), 9300, 0.05f, 6f, 13f);
        CreateBoundaryWall(topPos.y);
        CreateClouds();
        CreateWorldAmbience();
        CreateModeWorld(towerK, towerZ, towerE, towerH, topPos.y, topZeit.y, topEnd.y, topHard.y);
        ParticleSystem stars = CreateStarField();
        ParticleSystem weather = CreateWeatherParticles();
        CreateFireflies();
        CreateAurora();
        CreateClimbWisps();
        CreateEndlessAtmosphere();
        CreateBats();
        CreateJellyfish();
        CreateShootingStars();
        CreateFillLight();
        CreateGameManager();
        CreateAudioManager();
        CreateMusicManager();
        CreateEffectsManager();
        CreateSettingsMenu();
        CreateWinScreen();
        // (The best-run "ghost" feature was removed on request - script, HUD pace chip and all.)
        CreateMilestoneTracker();
        new GameObject("ModeTuner").AddComponent<ModeTuner>(); // per-mode parkour character
        new GameObject("PhotoMode").AddComponent<PhotoMode>();
        CreateCosmeticPreview();
        CreateTutorialOverlay();
        CreateAtmosphereManager(sunLight, stars, weather);

        // LAST: lay the unified stylized look over every prop/platform material (all objects exist now).
        UnifyPropMaterials();

        string scenesDir = Path.Combine(Application.dataPath, "Scenes");
        Directory.CreateDirectory(scenesDir);
        string scenePath = "Assets/Scenes/MainScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder: Only-Up-style tower built at " + scenePath);
    }

    const string SkyboxCubemapPath = "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Textures/Polyverse Skies - Blue Sky.png";

    // Boxophobic's Skybox Cubemap Extended shader, using its demo sky cubemap as the base
    // texture. Falls back to the built-in procedural skybox if the package isn't present.
    // ZoneAtmosphere drives _TintColor/_Exposure per zone at runtime for the 5-world mood shift.
    const string SkyboxNightCubemapPath = "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Textures/Polyverse Skies - Night Sky.exr";

    // Blends between the day (Blue Sky) and night (dark, pink-tinted nebula) cubemaps -
    // ZoneAtmosphere drives _CubemapTransition per zone so Sternenkrone shows the night sky.
    const string SkyboxMaterialPath = "Assets/SkyboxMaterial.mat";

    static Material CreateSkyboxMaterial()
    {
        Shader blendShader = Shader.Find("Skybox/Cubemap Blend");
        if (blendShader == null)
        {
            Material fallback = new Material(Shader.Find("Skybox/Procedural"));
            fallback.SetColor("_SkyTint", new Color(0.55f, 0.72f, 0.55f));
            fallback.SetColor("_GroundColor", new Color(0.4f, 0.42f, 0.3f));
            fallback.SetFloat("_AtmosphereThickness", 1.15f);
            fallback.SetFloat("_SunSize", 0.06f);
            fallback.SetFloat("_SunSizeConvergence", 4f);
            return fallback;
        }

        // Must be a persistent asset (not just an in-memory Material) or the reference can be
        // lost between the editor session that built the scene and the actual player build.
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
        if (sky == null || sky.shader != blendShader)
        {
            sky = new Material(blendShader);
            AssetDatabase.CreateAsset(sky, SkyboxMaterialPath);
        }

        Cubemap daySky = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyboxCubemapPath);
        Cubemap nightSky = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyboxNightCubemapPath);
        sky.SetTexture("_Tex", daySky);
        sky.SetTexture("_Tex_Blend", nightSky);

        // PBR reflections sample the NIGHT sky instead of a grey default probe, so smooth surfaces
        // (water, weapon metal) mirror the moonlit blue instead of daylight grey.
        if (nightSky != null)
        {
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = nightSky;
            RenderSettings.reflectionIntensity = 0.7f;
        }
        sky.SetColor("_Tex_HDR", new Color(1f, 1f, 0f, 0f));
        sky.SetColor("_Tex_Blend_HDR", new Color(1f, 1f, 0f, 0f));
        sky.SetFloat("_CubemapTransition", 1f); // full NIGHT sky (starry) - ZoneAtmosphere keeps it night
        sky.SetColor("_TintColor", new Color(0.6f, 0.62f, 0.75f, 1f));
        sky.SetFloat("_Exposure", 1.35f);       // brighten the night sky so stars read well
        sky.SetFloat("_CubemapPosition", 0f);

        sky.SetFloat("_EnableRotation", 0f);
        sky.DisableKeyword("_ENABLEROTATION_ON");

        // Horizon fog blend reads unity_FogColor automatically, which ZoneAtmosphere already
        // updates every frame - gives a free per-zone horizon haze that matches the ground fog.
        sky.SetFloat("_EnableFog", 1f);
        sky.EnableKeyword("_ENABLEFOG_ON");
        sky.SetFloat("_FogIntensity", 1f);
        sky.SetFloat("_FogHeight", 0.7f);
        sky.SetFloat("_FogSmoothness", 0.01f);
        sky.SetFloat("_FogFill", 0f);
        sky.SetFloat("_FogPosition", 0f);

        EditorUtility.SetDirty(sky);
        return sky;
    }

    static Light CreateLight()
    {
        var lightGO = new GameObject("Sun"); // the "moon" now
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        // Moonlit night: a bright, cool blue-white key light so platforms/obstacles stay clearly
        // readable (the "night" mood comes from the sky + colour grading, NOT from darkness).
        // ZoneAtmosphere drives per-zone colour/intensity on top.
        light.intensity = 0.85f;
        light.color = new Color(0.68f, 0.78f, 1f);
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
        // The cosmetic preview (layer 9) has its own dedicated light; keep the world moon off it.
        light.cullingMask = ~(1 << 9);

        Material sky = CreateSkyboxMaterial();
        RenderSettings.skybox = sky;

        // Night ambient: a soft blue fill - kept MODERATE (not near-black) so the scene still reads
        // clearly under moonlight, just cool and dim instead of daylight-bright.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.2f, 0.25f, 0.4f);
        RenderSettings.ambientEquatorColor = new Color(0.17f, 0.21f, 0.33f);
        RenderSettings.ambientGroundColor = new Color(0.1f, 0.11f, 0.18f);

        RenderSettings.fog = true;
        // Exponential-squared night haze: the near field (parkour) stays clear while distance fades
        // softly into the blue dark for real depth/mystery. Density tuned to the ~600m world scale.
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.12f, 0.15f, 0.28f);
        RenderSettings.fogDensity = 0.0026f;

        return light;
    }

    static GameObject CreatePlayerPrefab()
    {
        GameObject root = new GameObject("Player");
        root.tag = "Player";
        root.transform.position = new Vector3(0f, 1f, 0f);

        root.AddComponent<NetworkObject>();
        var netTransform = root.AddComponent<NetworkTransform>();
        netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        netTransform.SyncPositionX = netTransform.SyncPositionY = netTransform.SyncPositionZ = true;
        netTransform.SyncRotAngleX = netTransform.SyncRotAngleZ = false;
        netTransform.SyncRotAngleY = true;

        var controller = root.AddComponent<CharacterController>();
        controller.height = 2f;
        // Slim capsule (0.35) for a humanoid: a 0.5 radius = 1m-wide hitbox caught on platform edges
        // and walls, which read as "hitbox too big" and made WASD feel like it snagged on geometry.
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 1f, 0f);

        var playerController = root.AddComponent<PlayerController>();

        // Character visual: one humanoid Kenney "Animated Characters Protagonists" mesh. Each skin is
        // a whole different CHARACTER via its texture; PlayerCosmetics swaps the texture per skin.
        GameObject visualRoot = new GameObject("CharacterVisual");
        visualRoot.transform.SetParent(root.transform);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;

        var squash = visualRoot.AddComponent<CharacterSquash>();
        squash.player = playerController;

        SkinnedMeshRenderer characterRenderer = BuildProtagonistCharacter(visualRoot.transform, out Animator animator, out Texture2D[] skinTextures);
        playerController.animator = animator;

        var admin = root.AddComponent<AdminController>();
        admin.player = playerController;

        // Cosmetic trail (equippable effect). Sits at the player's base, thin/short so it never
        // blocks the view. PlayerCosmetics enables/colors it based on the equipped effect.
        GameObject trailGO = new GameObject("CosmeticTrail");
        trailGO.transform.SetParent(root.transform);
        trailGO.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        TrailRenderer trail = trailGO.AddComponent<TrailRenderer>();
        trail.time = 0.45f;
        trail.startWidth = 0.35f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.08f;
        trail.autodestruct = false;
        trail.emitting = false;
        trail.enabled = false;
        Shader trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (trailShader == null)
            trailShader = Shader.Find("Sprites/Default");
        trail.material = new Material(trailShader);

        // Dash motion streak: a bright cyan trail that only emits during a dash (PlayerController
        // toggles emitting). Additive + HDR so it glows/blooms like speed lines.
        GameObject dashTrailGO = new GameObject("DashTrail");
        dashTrailGO.transform.SetParent(root.transform);
        dashTrailGO.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        TrailRenderer dashTrail = dashTrailGO.AddComponent<TrailRenderer>();
        dashTrail.time = 0.22f;
        dashTrail.startWidth = 0.5f;
        dashTrail.endWidth = 0f;
        dashTrail.minVertexDistance = 0.05f;
        dashTrail.numCapVertices = 3;
        dashTrail.autodestruct = false;
        dashTrail.emitting = false;
        var dashMat = new Material(trailShader);
        if (dashMat.HasProperty("_BaseColor")) dashMat.SetColor("_BaseColor", new Color(0.62f, 0.86f, 1f) * 2f);
        if (dashMat.HasProperty("_SrcBlend")) dashMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (dashMat.HasProperty("_DstBlend")) dashMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (dashMat.HasProperty("_ZWrite")) dashMat.SetFloat("_ZWrite", 0f);
        dashMat.renderQueue = 3100;
        dashTrail.material = dashMat;
        var dashGrad = new Gradient();
        dashGrad.SetKeys(
            new[] { new GradientColorKey(new Color(0.72f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.42f, 0.6f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        dashTrail.colorGradient = dashGrad;
        playerController.dashTrail = dashTrail;
        // Arrow template lives UNDER the player so the reference survives SaveAsPrefabAsset (a prefab
        // can't reference a loose scene object - that's why the bow wasn't firing).
        GameObject arrowTemplate = BuildArrowTemplate();
        arrowTemplate.transform.SetParent(root.transform);
        arrowTemplate.transform.localPosition = Vector3.zero;
        playerController.arrowPrefab = arrowTemplate;

        GameObject bow = BuildBowVisual();
        bow.transform.SetParent(root.transform);
        bow.transform.localPosition = new Vector3(0.32f, 1.05f, 0.25f); // at the right hand, held forward
        bow.transform.localRotation = Quaternion.identity;              // upright, grip toward the target
        playerController.bowVisual = bow.transform;

        // Cosmetic particle emitter (equippable effect). PlayerCosmetics reconfigures its color/
        // size/speed/gravity/rate per effect so each one looks clearly different.
        GameObject fxGO = new GameObject("CosmeticParticles");
        fxGO.transform.SetParent(root.transform);
        fxGO.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var fx = fxGO.AddComponent<ParticleSystem>();
        var fxMain = fx.main;
        fxMain.loop = true;
        fxMain.playOnAwake = false;
        fxMain.startLifetime = 1f;
        fxMain.startSpeed = 1f;
        fxMain.startSize = 0.2f;
        fxMain.simulationSpace = ParticleSystemSimulationSpace.World;
        fxMain.maxParticles = 200;
        var fxEmission = fx.emission;
        fxEmission.rateOverTime = 0f;
        var fxShape = fx.shape;
        fxShape.shapeType = ParticleSystemShapeType.Sphere;
        fxShape.radius = 0.35f;
        var fxRenderer = fxGO.GetComponent<ParticleSystemRenderer>();
        Shader fxShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (fxShader == null)
            fxShader = Shader.Find("Sprites/Default");
        var fxMat = new Material(fxShader);
        if (fxMat.HasProperty("_BaseColor"))
            fxMat.SetColor("_BaseColor", Color.white);
        fxRenderer.material = fxMat;
        fx.Stop();

        // Second effect layer: a slower, larger, soft glow/smoke haze behind the sharp main particles.
        // Two layers is what turns a thin stream into a real "flames + embers + smoke" reward look.
        GameObject fx2GO = new GameObject("CosmeticParticlesGlow");
        fx2GO.transform.SetParent(root.transform);
        fx2GO.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var fx2 = fx2GO.AddComponent<ParticleSystem>();
        var fx2Main = fx2.main;
        fx2Main.loop = true;
        fx2Main.playOnAwake = false;
        fx2Main.startLifetime = 1f;
        fx2Main.startSpeed = 0.6f;
        fx2Main.startSize = 0.4f;
        fx2Main.simulationSpace = ParticleSystemSimulationSpace.World;
        fx2Main.maxParticles = 160;
        var fx2Emission = fx2.emission;
        fx2Emission.rateOverTime = 0f;
        var fx2Shape = fx2.shape;
        fx2Shape.shapeType = ParticleSystemShapeType.Sphere;
        fx2Shape.radius = 0.3f;
        var fx2Renderer = fx2GO.GetComponent<ParticleSystemRenderer>();
        var fx2Mat = new Material(fxShader);
        if (fx2Mat.HasProperty("_BaseColor"))
            fx2Mat.SetColor("_BaseColor", Color.white);
        fx2Renderer.material = fx2Mat;
        fx2.Stop();

        // Skin aura emitter - themed particles (fire/ice/lightning/nature/shadow) that surround the
        // character while a themed skin is worn. PlayerCosmetics configures it per equipped skin.
        GameObject auraGO = new GameObject("SkinAura");
        auraGO.transform.SetParent(root.transform);
        auraGO.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var aura = auraGO.AddComponent<ParticleSystem>();
        var auraMain = aura.main;
        auraMain.loop = true;
        auraMain.playOnAwake = false;
        auraMain.startLifetime = 1f;
        auraMain.startSpeed = 1f;
        auraMain.startSize = 0.2f;
        auraMain.simulationSpace = ParticleSystemSimulationSpace.World;
        auraMain.maxParticles = 300;
        var auraEmission = aura.emission;
        auraEmission.rateOverTime = 0f;
        var auraShape = aura.shape;
        auraShape.shapeType = ParticleSystemShapeType.Sphere;
        auraShape.radius = 0.4f;
        var auraRenderer = auraGO.GetComponent<ParticleSystemRenderer>();
        Shader auraShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (auraShader == null)
            auraShader = Shader.Find("Sprites/Default");
        var auraMat = new Material(auraShader);
        if (auraMat.HasProperty("_BaseColor"))
            auraMat.SetColor("_BaseColor", Color.white);
        auraRenderer.material = auraMat;
        aura.Stop();

        // Head anchor for worn accessories (hats/crowns). A HeadAccessoryMount drives it onto the real
        // humanoid Head bone each frame (crown height, upright, facing the body), so hats sit perfectly
        // on the head despite the runtime rescale/recenter/animation instead of low and behind it.
        GameObject headAnchor = new GameObject("HeadAnchor");
        headAnchor.transform.SetParent(root.transform);
        headAnchor.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        headAnchor.transform.localRotation = Quaternion.identity;
        var headMount = headAnchor.AddComponent<HeadAccessoryMount>();
        headMount.headBone = FindHeadBone(animator, visualRoot.transform);
        headMount.facing = root.transform;
        headMount.bodyRenderer = characterRenderer;

        var cosmetics = root.AddComponent<PlayerCosmetics>();
        cosmetics.characterRenderer = characterRenderer;
        cosmetics.skinTextures = skinTextures;
        cosmetics.skinTextureIds = SkinTextureNames;
        cosmetics.playerController = playerController;
        cosmetics.accessoryAnchor = headAnchor.transform;
        cosmetics.skinAura = aura;

        GameObject nameTagGO = new GameObject("NameTag");
        nameTagGO.transform.SetParent(root.transform);
        // Sits just above the head; smaller now so it labels the player without dominating the view.
        nameTagGO.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        TextMesh nameTagText = nameTagGO.AddComponent<TextMesh>();
        nameTagText.characterSize = 0.03f;
        nameTagText.fontSize = 64;
        nameTagText.anchor = TextAnchor.LowerCenter;
        nameTagText.alignment = TextAlignment.Center;
        nameTagText.color = Color.white;
        var nameTagDisplay = nameTagGO.AddComponent<PlayerNameTagDisplay>();
        nameTagDisplay.player = playerController;

        var camGO = new GameObject("PlayerCamera");
        camGO.transform.SetParent(root.transform);
        camGO.tag = "MainCamera";
        Camera cam = camGO.AddComponent<Camera>();
        cam.cullingMask &= ~(1 << 9); // never render the isolated cosmetic-preview stage
        // Enable post-processing on the actual gameplay camera - it was only on the lobby camera, so
        // in-game had NO bloom/tonemapping/color-grading/vignette. This makes the whole game benefit
        // from the already-configured pipeline (and the emissive skins/auras finally bloom).
        var playerCamData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (playerCamData == null)
            playerCamData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        playerCamData.renderPostProcessing = true;
        playerCamData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        AudioListener listener = camGO.AddComponent<AudioListener>();
        var follow = camGO.AddComponent<CameraFollow>();
        follow.target = root.transform;
        follow.distance = 7f;
        follow.height = 2.5f;
        camGO.transform.position = root.transform.position + new Vector3(0f, 2.5f, -7f);
        camGO.SetActive(false);

        playerController.playerCamera = cam;
        playerController.audioListener = listener;

        // Layer 8 = "Player": the camera's collision SphereCast masks this out so it never
        // snaps onto the player's own body when looking level.
        SetLayerRecursive(root, 8);

        const string prefabPath = "Assets/PlayerPrefab.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        return prefab;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    static Camera CreateLobbyCamera()
    {
        var camGO = new GameObject("LobbyCamera");
        camGO.tag = "MainCamera";
        Camera cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 3f, -8f);
        camGO.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
        return cam;
    }

    static void CreateNetworkManagerAndLobby(GameObject playerPrefab, Camera lobbyCam)
    {
        var nmGO = new GameObject("NetworkManager");
        var nm = nmGO.AddComponent<NetworkManager>();
        var transport = nmGO.AddComponent<UnityTransport>();
        nm.NetworkConfig.NetworkTransport = transport;
        nm.NetworkConfig.PlayerPrefab = playerPrefab;
        nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });

        var lobby = nmGO.AddComponent<LobbyBootstrap>();
        lobby.lobbyCamera = lobbyCam;
        lobby.transport = transport;
    }

    const string ProtagonistPath = "Assets/KenneyProtagonists/";
    static Avatar protagonistAvatar;

    // Imports the model + its clips as HUMANOID (the bones use standard names: Hips, Spine,
    // LeftUpLeg, ...). Humanoid normalizes pose, upright orientation and retargeting automatically -
    // that's what fixes the "lying down / no animation" a Generic rig gave. Returns the model avatar.
    static Avatar ConfigureProtagonistHumanoid()
    {
        if (protagonistAvatar != null)
            return protagonistAvatar;

        string modelPath = ProtagonistPath + "Model/characterMedium.fbx";
        var mi = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (mi != null)
        {
            mi.bakeAxisConversion = false;
            mi.useFileScale = true;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.SaveAndReimport();
        }
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            if (a is Avatar av) { protagonistAvatar = av; break; }

        // Each animation FBX ships its own skeleton whose hierarchy differs slightly from the model,
        // so CopyFromOther fails ("rig mis-match"). Instead give each clip file its OWN humanoid avatar
        // (CreateFromThisModel): humanoid clips are stored in rig-independent muscle space and retarget
        // onto the model's avatar at runtime regardless of the authoring skeleton.
        (string file, bool loop)[] anims = { ("idle.fbx", true), ("run.fbx", true), ("jump.fbx", false) };
        foreach (var (file, loop) in anims)
        {
            string p = ProtagonistPath + "Animations/" + file;
            var ai = AssetImporter.GetAtPath(p) as ModelImporter;
            if (ai == null) continue;

            // Pass 1: switch to Humanoid + own avatar and reimport. The FBX's takes (and thus
            // defaultClipAnimations) are only populated AFTER an import in the new rig mode - reading
            // them in the same pass returns an empty array, which is why loopTime never persisted
            // before (clipAnimations stayed []) and run/idle played once then froze.
            ai.animationType = ModelImporterAnimationType.Human;
            ai.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            ai.SaveAndReimport();

            // Pass 2: now the clips exist - stamp loopTime/loopPose and reimport so it sticks.
            ai = AssetImporter.GetAtPath(p) as ModelImporter;
            var clips = ai.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                }
                ai.clipAnimations = clips;
                ai.SaveAndReimport();
            }
            Debug.Log($"[Protagonist] {file}: {(clips == null ? 0 : clips.Length)} clip(s), loop={loop}");
        }
        return protagonistAvatar;
    }

    static AnimationClip LoadHumanoidClip(string file, string keyword)
    {
        AnimationClip fallback = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(ProtagonistPath + "Animations/" + file))
        {
            if (a is AnimationClip c && !c.name.StartsWith("__") && !c.name.Contains("Targeting"))
            {
                if (c.name.ToLower().Contains(keyword))
                    return c;
                fallback ??= c;
            }
        }
        return fallback;
    }

    // Animator for the humanoid, built from its (now Humanoid-retargeted) idle/run/jump clips.
    static RuntimeAnimatorController BuildProtagonistAnimator()
    {
        ConfigureProtagonistHumanoid();

        const string controllerPath = "Assets/ProtagonistAnimator.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        AnimationClip idle = LoadHumanoidClip("idle.fbx", "idle");
        AnimationClip run = LoadHumanoidClip("run.fbx", "run");
        AnimationClip jump = LoadHumanoidClip("jump.fbx", "jump");

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);

        var sm = controller.layers[0].stateMachine;
        var idleState = sm.AddState("Idle"); idleState.motion = idle;
        var runState = sm.AddState("Run"); runState.motion = run;
        var jumpState = sm.AddState("Jump"); jumpState.motion = jump;
        sm.defaultState = idleState;

        AnimatorStateTransition T(AnimatorState to)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = 0.12f;
            t.canTransitionToSelf = false;
            return t;
        }
        var toJump = T(jumpState); toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        var toRun = T(runState); toRun.AddCondition(AnimatorConditionMode.If, 0f, "Grounded"); toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        var toIdle = T(idleState); toIdle.AddCondition(AnimatorConditionMode.If, 0f, "Grounded"); toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        return controller;
    }

    // Skin textures = the different character looks (parallel to CosmeticsCatalog skin.Texture ids).
    static readonly string[] SkinTextureNames = { "skaterMaleA", "skaterFemaleA", "criminalMaleA", "cyborgFemaleA" };
    static RuntimeAnimatorController protagonistAnimatorCache;

    // Instantiates the humanoid protagonist under 'parent' with the shared animator and a material
    // set to the default skin texture; returns its renderer + all skin textures for the cosmetics.
    static SkinnedMeshRenderer BuildProtagonistCharacter(Transform parent, out Animator animator, out Texture2D[] textures)
    {
        if (protagonistAnimatorCache == null)
            protagonistAnimatorCache = BuildProtagonistAnimator();
        ConfigureProtagonistHumanoid(); // ensures the avatar exists

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistPath + "Model/characterMedium.fbx");
        GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        character.name = "Character";
        character.transform.SetParent(parent);
        character.transform.localPosition = Vector3.zero;
        // Humanoid rig normalizes orientation/pose; CharacterNormalizer fixes the size at runtime
        // (skinned bounds are unreliable at build time), so no manual rotation/scale guessing here.
        character.transform.localRotation = Quaternion.identity;
        character.transform.localScale = Vector3.one;
        character.AddComponent<CharacterNormalizer>();

        animator = character.GetComponent<Animator>();
        if (animator == null)
            animator = character.AddComponent<Animator>();
        animator.runtimeAnimatorController = protagonistAnimatorCache;
        animator.avatar = protagonistAvatar;
        // CRITICAL: the CharacterController drives movement, not the animation. With root motion on,
        // the run clip's baked forward motion would drive the mesh away from the player root (which
        // carries the hitbox, name tag and camera target), so the model "walks out of frame" while
        // the name stays centered. Keep the visual rigidly locked to the root.
        animator.applyRootMotion = false;

        textures = new Texture2D[SkinTextureNames.Length];
        for (int i = 0; i < SkinTextureNames.Length; i++)
            textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(ProtagonistPath + "Skins/" + SkinTextureNames[i] + ".png");

        var renderer = character.GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer != null)
        {
            // Saved material ASSET (not a runtime material). A runtime `new Material(...)` was being
            // lost when the player was saved via SaveAsPrefabAsset, so the build showed the pink
            // "missing shader" material. A real .mat asset persists into the build.
            renderer.sharedMaterial = GetProtagonistMaterial(textures.Length > 0 ? textures[0] : null);
            // Keep skinned bounds live even when culled, so CharacterNormalizer's height measure and the
            // HeadAccessoryMount's crown measure are correct (the shop preview starts off-screen).
            renderer.updateWhenOffscreen = true;
        }
        return renderer;
    }

    // Head bone for mounting worn accessories. Prefers the humanoid rig mapping; falls back to a
    // name search so it still works if the avatar mapping isn't queryable at build time.
    static Transform FindHeadBone(Animator animator, Transform characterRoot)
    {
        if (animator != null && animator.avatar != null && animator.isHuman)
        {
            Transform hb = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hb != null)
                return hb;
        }
        if (characterRoot != null)
        {
            foreach (Transform t in characterRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLower().Contains("head"))
                    return t;
        }
        Debug.LogWarning("[HeadBone] NOT FOUND - accessory mount will use fixed fallback height");
        return null;
    }

    static Material protagonistMaterialCache;
    static Material GetProtagonistMaterial(Texture2D defaultTex)
    {
        if (protagonistMaterialCache != null)
            return protagonistMaterialCache;

        const string path = "Assets/ProtagonistMaterial.mat";
        protagonistMaterialCache = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (protagonistMaterialCache == null)
        {
            protagonistMaterialCache = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(protagonistMaterialCache, path);
        }
        protagonistMaterialCache.shader = Shader.Find("Universal Render Pipeline/Lit");
        protagonistMaterialCache.SetFloat("_Smoothness", 0.1f);
        if (defaultTex != null)
            protagonistMaterialCache.SetTexture("_BaseMap", defaultTex);
        EditorUtility.SetDirty(protagonistMaterialCache);
        return protagonistMaterialCache;
    }

    static RuntimeAnimatorController BuildCharacterAnimator(string fbxPath)
    {
        const string controllerPath = "Assets/CharacterAnimator.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        AnimationClip FindClip(string clipName)
        {
            foreach (var a in assets)
                if (a is AnimationClip clip && clip.name == clipName)
                    return clip;
            return null;
        }

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);

        var rootSM = controller.layers[0].stateMachine;

        var idleState = rootSM.AddState("Idle");
        idleState.motion = FindClip("idle");

        var walkState = rootSM.AddState("Walk");
        walkState.motion = FindClip("walk");

        var jumpState = rootSM.AddState("Jump");
        jumpState.motion = FindClip("jump");

        var fallState = rootSM.AddState("Fall");
        fallState.motion = FindClip("fall");

        var crouchState = rootSM.AddState("Crouch");
        crouchState.motion = FindClip("crouch");

        rootSM.defaultState = idleState;

        AnimatorStateTransition T(AnimatorState target)
        {
            var t = rootSM.AddAnyStateTransition(target);
            t.hasExitTime = false;
            t.duration = 0.12f;
            t.canTransitionToSelf = false;
            return t;
        }

        var toCrouch = T(crouchState);
        toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "Crouching");
        toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

        var toJump = T(jumpState);
        toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        toJump.AddCondition(AnimatorConditionMode.Greater, 0.5f, "VerticalVelocity");

        var toFall = T(fallState);
        toFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        toFall.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalVelocity");

        var toWalk = T(walkState);
        toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        toWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var toIdle = T(idleState);
        toIdle.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        return controller;
    }

    static Terrain groundTerrain;

    static Texture2D CreateSolidTexture(string name, Color colorA, Color colorB)
    {
        string dir = Path.Combine(Application.dataPath, "Generated");
        string assetPath = "Assets/Generated/" + name + ".png";

        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (existing != null)
            return existing;

        Directory.CreateDirectory(dir);
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                tex.SetPixel(x, y, Color.Lerp(colorA, colorB, n));
            }
        }
        tex.Apply();

        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    const float RiverMaxHeight = 10f;

    // THREE big rivers that stretch right across the map (two roughly W-E, one N-S), each a long line
    // with a gentle sine wiggle. Kept clear of the flat spawn/tower base at the centre (closest
    // approach ~38m). Parameterized by s in [0,1].
    const int RiverCount = 3;
    static Vector3 RiverPointN(int r, float s)
    {
        switch (r)
        {
            case 0: return new Vector3(Mathf.Lerp(-235f, 235f, s), 0f, 66f + Mathf.Sin(s * Mathf.PI * 2.5f) * 26f);
            case 1: return new Vector3(62f + Mathf.Sin(s * Mathf.PI * 2.3f) * 26f, 0f, Mathf.Lerp(-235f, 235f, s));
            default: return new Vector3(Mathf.Lerp(235f, -235f, s), 0f, -74f + Mathf.Sin(s * Mathf.PI * 2.7f) * 24f);
        }
    }

    // Legacy single-river accessor (waterfall/lake references) points at river 0.
    static Vector3 RiverPoint(float s) => RiverPointN(0, s);

    static float DistanceToRiver(float worldX, float worldZ, out float riverS)
    {
        float best = float.MaxValue;
        float bestS = 0f;
        Vector2 q = new Vector2(worldX, worldZ);
        const int samples = 40;
        for (int r = 0; r < RiverCount; r++)
        {
            for (int i = 0; i <= samples; i++)
            {
                float s = i / (float)samples;
                Vector3 p = RiverPointN(r, s);
                float d = (q - new Vector2(p.x, p.z)).magnitude;
                if (d < best) { best = d; bestS = s; }
            }
        }
        riverS = bestS;
        return best;
    }

    // Shallow low channel so all three rivers carve to roughly the same low elevation.
    static float RiverBedNormalizedHeight(float s)
    {
        return 0.04f;
    }

    const float WaterfallS = 0.37f;

    // A still lake tucked on the side of the terrain the river's arc never sweeps through
    // (river spans angle -110..110 deg; the lake sits around 180 deg, well clear of it).
    static readonly Vector3 LakeCenter = new Vector3(18f, 0f, -138f);
    const float LakeRadius = 30f;
    const float LakeShoreWidth = 14f;
    const float LakeBedNormalizedHeight = 0.015f;

    const float GroundSize = 480f;

    static void CreateGround()
    {
        const int resolution = 257;
        const float size = GroundSize;
        const float maxHeight = RiverMaxHeight;

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = resolution;
        terrainData.size = new Vector3(size, maxHeight, size);

        Random.InitState(42);
        float offsetX = Random.Range(0f, 1000f);
        float offsetZ = Random.Range(0f, 1000f);

        float[,] heights = new float[resolution, resolution];
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float worldX = (x / (float)(resolution - 1) - 0.5f) * size;
                float worldZ = (z / (float)(resolution - 1) - 0.5f) * size;
                float distFromCenter = new Vector2(worldX, worldZ).magnitude;
                float flatten = Mathf.Clamp01((distFromCenter - 8f) / 20f);

                // Multi-octave, world-space noise: large rolling hills/valleys, medium bumps,
                // fine surface texture - instead of a single flat noise band.
                float macro = Mathf.PerlinNoise(worldX * 0.006f + offsetX * 3f, worldZ * 0.006f + offsetZ * 3f);
                float detail = Mathf.PerlinNoise(worldX * 0.05f + offsetX, worldZ * 0.05f + offsetZ);
                float fine = Mathf.PerlinNoise(worldX * 0.14f + offsetX, worldZ * 0.14f + offsetZ);
                // Rolling hills & dips (not a flat plane), but gentle enough that the carved rivers
                // stay visible instead of hiding in deep trenches. Spawn stays flat via `flatten`.
                float h = (macro * 0.5f + detail * 0.35f + fine * 0.15f) * flatten * 0.7f;

                float distToRiver = DistanceToRiver(worldX, worldZ, out float riverS);
                // Wider river + banks so the water winds through the jungle as a clear feature.
                const float riverHalfWidth = 7f;
                const float riverBankWidth = 11f;
                float riverCarve = 1f - Mathf.Clamp01((distToRiver - riverHalfWidth) / riverBankWidth);
                h = Mathf.Lerp(h, RiverBedNormalizedHeight(riverS), riverCarve);

                float distToLake = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(LakeCenter.x, LakeCenter.z));
                float lakeCarve = 1f - Mathf.Clamp01((distToLake - LakeRadius) / LakeShoreWidth);
                h = Mathf.Lerp(h, LakeBedNormalizedHeight, lakeCarve);

                heights[z, x] = h;
            }
        }
        terrainData.SetHeights(0, 0, heights);

        // Real tileable ground from the Pure Poly nature pack (grass + dirt, with a shared normal
        // map for surface relief) instead of the old flat solid-colour grass, so the base of the
        // world reads as proper terrain. Dirt/pebbles blend in on slopes and along the water.
        const string PPTex = "Assets/Pure Poly/Free Low Poly Nature Pack/Terrain/Terrain_Textures/";
        // Tighter tiling (6m/5m instead of 9m/7m) so the grass/dirt texture actually reads as surface
        // detail up close instead of one big flat green sheet.
        // Prefer the downloaded ambientCG PBR textures (CC0) - lush photographic grass and a real
        // dirt-path look, matching the reference art. Falls back to the old PurePoly set.
        const string DlGrassC = "Assets/DownloadedTextures/Grass001/Grass001_1K-JPG_Color.jpg";
        const string DlGrassN = "Assets/DownloadedTextures/Grass001/Grass001_1K-JPG_NormalGL.jpg";
        const string DlDirtC = "Assets/DownloadedTextures/Ground037/Ground037_1K-JPG_Color.jpg";
        const string DlDirtN = "Assets/DownloadedTextures/Ground037/Ground037_1K-JPG_NormalGL.jpg";
        bool haveDl = File.Exists(DlGrassC) && File.Exists(DlDirtC);
        if (haveDl)
        {
            EnsureNormalMapImport(DlGrassN);
            EnsureNormalMapImport(DlDirtN);
        }

        TerrainLayer grassLayer = BuildTerrainLayer("Assets/GrassTerrainLayer.asset",
            haveDl ? DlGrassC : PPTex + "PP_Ground_Green.png",
            haveDl ? DlGrassN : PPTex + "PP_Ground_Normal.png", new Vector2(7f, 7f));
        TerrainLayer dirtLayer = BuildTerrainLayer("Assets/DirtTerrainLayer.asset",
            haveDl ? DlDirtC : PPTex + "PP_Ground_Mid_Brown.png",
            haveDl ? DlDirtN : PPTex + "PP_Ground_Normal.png", new Vector2(5f, 5f));

        // Fallback so the build still works if the pack is ever removed.
        if (grassLayer.diffuseTexture == null)
            grassLayer.diffuseTexture = CreateSolidTexture("GrassTerrainDusk",
                new Color(0.2f, 0.32f, 0.16f), new Color(0.26f, 0.4f, 0.2f));

        bool hasDirt = dirtLayer.diffuseTexture != null;
        terrainData.terrainLayers = hasDirt ? new[] { grassLayer, dirtLayer } : new[] { grassLayer };

        GameObject terrainGO = Terrain.CreateTerrainGameObject(terrainData);
        terrainGO.name = "Ground";
        terrainGO.transform.position = new Vector3(-size / 2f, 0f, -size / 2f);

        groundTerrain = terrainGO.GetComponent<Terrain>();
        groundTerrain.allowAutoConnect = false;
        // Render the grass carpet far and dense; gentle wind sway.
        groundTerrain.detailObjectDistance = 250f;
        groundTerrain.detailObjectDensity = 1f;
        terrainData.wavingGrassStrength = 0.35f;
        terrainData.wavingGrassSpeed = 0.4f;
        terrainData.wavingGrassAmount = 0.35f;
        terrainData.wavingGrassTint = new Color(0.85f, 0.92f, 0.7f);

        if (hasDirt)
            PaintGroundSplat(terrainData, heights, size, maxHeight, offsetX, offsetZ);

        PaintTerrainGrass(terrainData, size);
    }

    // Dense grass carpet via Terrain detail billboards (GPU-instanced = cheap for huge counts). This is
    // what turns the bare textured ground into a lush jungle floor. Grass everywhere except the flat
    // spawn; density falls off with distance so the far map stays cheap. Water hides any underwater bits.
    static void PaintTerrainGrass(TerrainData data, float size)
    {
        var proto = new DetailPrototype
        {
            prototypeTexture = BuildGrassBladeTexture(),
            renderMode = DetailRenderMode.GrassBillboard,
            usePrototypeMesh = false,
            healthyColor = new Color(0.5f, 0.8f, 0.34f),
            dryColor = new Color(0.62f, 0.74f, 0.32f),
            minWidth = 1.3f,
            maxWidth = 2.6f,
            minHeight = 1.1f,
            maxHeight = 2.4f,
            noiseSpread = 0.35f,
            useInstancing = true,
        };
        data.detailPrototypes = new[] { proto };

        const int dRes = 384;
        data.SetDetailResolution(dRes, 32);
        int[,] map = new int[dRes, dRes];
        for (int y = 0; y < dRes; y++)
        {
            for (int x = 0; x < dRes; x++)
            {
                float wx = (x / (float)dRes - 0.5f) * size;
                float wz = (y / (float)dRes - 0.5f) * size;
                float distC = Mathf.Sqrt(wx * wx + wz * wz);
                if (distC < 12f) { map[y, x] = 0; continue; }         // keep spawn clear
                float patch = Mathf.PerlinNoise(wx * 0.05f + 11f, wz * 0.05f + 7f); // patchy, natural
                int baseD = distC < 210f ? 26 : 16;                   // very dense grass across the whole map
                map[y, x] = Mathf.RoundToInt(baseD * (0.6f + patch));
            }
        }
        data.SetDetailLayer(0, 0, 0, map);
    }

    // A small grass-tuft billboard: several tapered green blades on transparent background. Saved as a
    // real PNG asset (a runtime texture doesn't survive into the player build, so terrain grass details
    // referencing it rendered nothing in-game).
    static Texture2D BuildGrassBladeTexture()
    {
        const string path = "Assets/GrassBladeTex.png";
        const int W = 64, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                tex.SetPixel(x, y, clear);

        var rng = new System.Random(9182);
        Color darkG = new Color(0.18f, 0.42f, 0.14f);
        Color liteG = new Color(0.55f, 0.85f, 0.35f);
        int blades = 8;
        for (int b = 0; b < blades; b++)
        {
            float bx = (b + 0.5f) / blades * W + (float)(rng.NextDouble() - 0.5) * 6f;
            float lean = (float)(rng.NextDouble() - 0.5) * 0.5f;
            int bh = (int)(H * (0.55f + rng.NextDouble() * 0.4f));
            for (int t = 0; t < bh; t++)
            {
                float xx = bx + lean * t;
                float halfW = Mathf.Lerp(2.4f, 0.4f, t / (float)bh);
                Color col = Color.Lerp(darkG, liteG, t / (float)bh);
                for (int dx = -(int)halfW; dx <= (int)halfW; dx++)
                {
                    int px = Mathf.RoundToInt(xx) + dx;
                    if (px < 0 || px >= W || t >= H) continue;
                    col.a = 1f;
                    tex.SetPixel(px, t, col);
                }
            }
        }
        tex.Apply();

        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Downloaded normal maps arrive as plain textures - flip the importer to NormalMap once.
    static void EnsureNormalMapImport(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
        {
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }
    }

    // Builds/refreshes a TerrainLayer asset from a diffuse + normal texture in the project.
    static TerrainLayer BuildTerrainLayer(string assetPath, string diffusePath, string normalPath, Vector2 tile)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(assetPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, assetPath);
        }
        layer.tileSize = tile;
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);

        Texture2D normal = LoadNormalMap(normalPath);
        if (normal != null)
        {
            layer.normalMapTexture = normal;
            layer.normalScale = 0.85f; // a bit more surface relief so the ground reads less flat
        }
        EditorUtility.SetDirty(layer);
        return layer;
    }

    // Ensures a texture is imported as a Normal Map before loading it, so terrain lighting is correct.
    static Texture2D LoadNormalMap(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Paints the grass/dirt splatmap: dirt shows on steeper slopes, along the river/lake banks, and
    // in scattered noise patches; grass everywhere else.
    static void PaintGroundSplat(TerrainData data, float[,] heights, float size, float maxHeight, float offX, float offZ)
    {
        int res = heights.GetLength(0);
        const int aRes = 256;
        data.alphamapResolution = aRes;
        float[,,] splat = new float[aRes, aRes, 2];
        float spacing = size / (res - 1);

        for (int az = 0; az < aRes; az++)
        {
            for (int ax = 0; ax < aRes; ax++)
            {
                float u = ax / (float)(aRes - 1);
                float v = az / (float)(aRes - 1);
                int hx = Mathf.Clamp(Mathf.RoundToInt(u * (res - 1)), 1, res - 2);
                int hz = Mathf.Clamp(Mathf.RoundToInt(v * (res - 1)), 1, res - 2);

                float dhx = (heights[hz, hx + 1] - heights[hz, hx - 1]) * maxHeight / (2f * spacing);
                float dhz = (heights[hz + 1, hx] - heights[hz - 1, hx]) * maxHeight / (2f * spacing);
                float slope = Mathf.Sqrt(dhx * dhx + dhz * dhz);
                float slopeW = Mathf.Clamp01((slope - 0.35f) / 0.5f);

                float worldX = (u - 0.5f) * size;
                float worldZ = (v - 0.5f) * size;
                float riverW = Mathf.Clamp01(1f - DistanceToRiver(worldX, worldZ, out _) / 9f);
                float distToLake = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(LakeCenter.x, LakeCenter.z));
                float lakeW = Mathf.Clamp01(1f - (distToLake - LakeRadius) / 8f);
                float bankW = Mathf.Max(riverW, lakeW);

                float patch = Mathf.PerlinNoise(worldX * 0.03f + offX, worldZ * 0.03f + offZ);
                float patchW = Mathf.Clamp01((patch - 0.62f) / 0.2f) * 0.55f;

                float dirt = Mathf.Clamp01(slopeW + bankW * 0.7f + patchW + FootpathWeight(worldX, worldZ));
                splat[az, ax, 0] = 1f - dirt;
                splat[az, ax, 1] = dirt;
            }
        }
        data.SetAlphamaps(0, 0, splat);
    }

    // Two winding FOOTPATHS radiating out from the spawn meadow, painted as dirt into the splat -
    // little walkways through the jungle. The ground lanterns stand along the same curves.
    static float FootpathWeight(float x, float z)
    {
        float w = 0f;
        // Path A heads +Z, swaying in X.
        if (z > 2f && z < 150f)
        {
            float ax = Mathf.Sin(z * 0.09f) * 6f;
            w = Mathf.Max(w, 1f - Mathf.Abs(x - ax) / 1.8f);
        }
        // Path B heads -X, swaying in Z.
        if (x < -2f && x > -150f)
        {
            float bz = Mathf.Cos(x * 0.08f) * 6f;
            w = Mathf.Max(w, 1f - Mathf.Abs(z - bz) / 1.8f);
        }
        return Mathf.Clamp01(w);
    }

    // Warm little lanterns standing along the two footpaths - the cozy trail through the night
    // jungle. Every fourth casts a real point light; all of them get circling moths.
    static void CreateGroundLanterns()
    {
        var root = new GameObject("PathLanterns");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var wood = new Material(lit);
        wood.SetColor("_BaseColor", new Color(0.26f, 0.18f, 0.11f));
        Color warm = new Color(1f, 0.78f, 0.42f);
        Material lamp = MakeGlowMaterial(warm * 2.8f);

        int placed = 0, lightsUsed = 0;
        for (int p = 0; p < 2; p++)
        {
            for (float s = 10f; s < 145f; s += 13f)
            {
                // Positions follow the same curves FootpathWeight paints, offset to the side.
                Vector3 pos = p == 0
                    ? new Vector3(Mathf.Sin(s * 0.09f) * 6f + 2.3f, 0f, s)
                    : new Vector3(-s, 0f, Mathf.Cos(-s * 0.08f) * 6f + 2.3f);
                if (TooCloseToWater(pos, 3f))
                    continue;
                pos.y = SampleTerrainHeight(pos);

                var lantern = new GameObject("Lantern_" + placed);
                lantern.transform.SetParent(root.transform);
                lantern.transform.position = pos;

                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.transform.SetParent(lantern.transform, false);
                post.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                post.transform.localScale = new Vector3(0.09f, 0.7f, 0.09f);
                post.GetComponent<MeshRenderer>().sharedMaterial = wood;

                var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(head.GetComponent<Collider>());
                head.transform.SetParent(lantern.transform, false);
                head.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                head.transform.localScale = Vector3.one * 0.24f;
                var hm = head.GetComponent<MeshRenderer>();
                hm.sharedMaterial = lamp;
                hm.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                if (placed % 4 == 0 && lightsUsed < 8)
                {
                    var lgo = new GameObject("LanternLight");
                    lgo.transform.SetParent(lantern.transform, false);
                    lgo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                    var lt = lgo.AddComponent<Light>();
                    lt.type = LightType.Point;
                    lt.range = 9f;
                    lt.intensity = 2.0f;
                    lt.color = warm;
                    lt.shadows = LightShadows.None;
                    lt.cullingMask = ~(1 << 9);
                    lightsUsed++;
                }

                CreateMoths(lantern.transform, new Vector3(0f, 1.5f, 0f), warm);
                lantern.AddComponent<DistanceCuller>().maxDistance = 170f;
                placed++;
            }
        }
        Debug.Log($"[Lanterns] {placed} placed, {lightsUsed} lit");
    }

    // A point on footpath curve p (matches FootpathWeight's curves exactly).
    static Vector3 PathPoint(int p, float s) => p == 0
        ? new Vector3(Mathf.Sin(s * 0.09f) * 6f, 0f, s)
        : new Vector3(-s, 0f, Mathf.Cos(-s * 0.08f) * 6f);

    // Wooden arched bridges where the footpaths cross the rivers - walkable (colliders on).
    static void CreateRiverBridges()
    {
        int built = 0;
        for (int p = 0; p < 2; p++)
        {
            Vector3 prev = PathPoint(p, 4f);
            for (float s = 6f; s < 148f; s += 2f)
            {
                Vector3 cur = PathPoint(p, s);
                // Threshold matches DistanceToRiver's coarse ~12 m sampling: at a true crossing the
                // nearest sampled centreline point can still be ~6 m away.
                if (DistanceToRiver(cur.x, cur.z, out _) < 6f)
                {
                    Vector3 along = (cur - prev).normalized;
                    CreateBridge(cur, along);
                    built++;
                    s += 20f; // skip past this river before searching on
                }
                prev = cur;
            }
        }
        Debug.Log($"[Bridges] {built} river bridges");
    }

    // Builds a bridge that spans BANK TO BANK, however wide the river is: first the channel
    // centre is found (lowest terrain along the crossing axis), then each end walks outward
    // until the ground rises back up. Segment count adapts to the measured length.
    static void CreateBridge(Vector3 nearCross, Vector3 along)
    {
        // 1) Channel centre: the lowest terrain point along the axis around the coarse hit.
        Vector3 center = nearCross;
        float bedY = float.MaxValue;
        for (float d = -12f; d <= 12f; d += 0.5f)
        {
            Vector3 q = nearCross + along * d;
            float h = SampleTerrainHeight(q);
            if (h < bedY)
            {
                bedY = h;
                center = q;
            }
        }

        // 2) Walk outward to each bank (ground back above the carve) + a little onto solid land.
        Vector3 endA = center - along * 12f;
        Vector3 endB = center + along * 12f;
        for (float d = 1f; d <= 20f; d += 0.5f)
        {
            if (SampleTerrainHeight(center - along * d) > bedY + 1.6f)
            {
                endA = center - along * (d + 1.5f);
                break;
            }
        }
        for (float d = 1f; d <= 20f; d += 0.5f)
        {
            if (SampleTerrainHeight(center + along * d) > bedY + 1.6f)
            {
                endB = center + along * (d + 1.5f);
                break;
            }
        }

        float deckY = Mathf.Max(SampleTerrainHeight(endA), SampleTerrainHeight(endB)) + 0.1f;
        endA.y = deckY;
        endB.y = deckY;
        float len = Vector3.Distance(endA, endB);
        int segs = Mathf.Clamp(Mathf.CeilToInt(len / 3.2f), 3, 12);

        var root = new GameObject("Bridge");
        root.transform.position = center;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var wood = new Material(lit);
        wood.SetColor("_BaseColor", new Color(0.48f, 0.33f, 0.2f));

        // 3) Arched deck: segment endpoints follow a sine profile; each segment is oriented along
        // its own chord, so the arch is continuous. Deck colliders stay ON - it's walkable.
        Vector3 ArchPoint(float u) => Vector3.Lerp(endA, endB, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.8f);
        for (int s = 0; s < segs; s++)
        {
            Vector3 p0 = ArchPoint(s / (float)segs);
            Vector3 p1 = ArchPoint((s + 1) / (float)segs);
            Vector3 mid = (p0 + p1) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(p1 - p0);
            float segLen = Vector3.Distance(p0, p1) + 0.12f;

            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.transform.SetParent(root.transform, true);
            deck.transform.position = mid;
            deck.transform.rotation = rot;
            deck.transform.localScale = new Vector3(2.3f, 0.16f, segLen);
            deck.GetComponent<MeshRenderer>().sharedMaterial = wood;

            for (int r = -1; r <= 1; r += 2)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(rail.GetComponent<Collider>());
                rail.transform.SetParent(root.transform, true);
                rail.transform.position = mid + rot * new Vector3(r * 1.08f, 0.5f, 0f);
                rail.transform.rotation = rot;
                rail.transform.localScale = new Vector3(0.09f, 0.55f, segLen);
                rail.GetComponent<MeshRenderer>().sharedMaterial = wood;
            }
        }
    }

    // A white stone arch straddling a footpath - the little ruin landmark from the reference art.
    static void CreateStoneArch(int p, float s)
    {
        Vector3 c = PathPoint(p, s);
        if (DistanceToRiver(c.x, c.z, out _) < 7f)
        {
            s += 10f;
            c = PathPoint(p, s);
        }
        Vector3 along = (PathPoint(p, s + 2f) - PathPoint(p, s - 2f)).normalized;
        c.y = SampleTerrainHeight(c);

        var root = new GameObject("StoneArch");
        root.transform.position = c;
        root.transform.rotation = Quaternion.LookRotation(along);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var stone = new Material(lit);
        stone.SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.84f));

        for (int side = -1; side <= 1; side += 2)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube); // collider on: a real gate
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = new Vector3(side * 1.75f, 1.6f, 0f);
            pillar.transform.localScale = new Vector3(0.72f, 3.2f, 0.72f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = stone;
        }
        // Arch crown: three angled segments.
        float[] ax = { -1.25f, 0f, 1.25f };
        float[] ay = { 3.35f, 3.8f, 3.35f };
        float[] az = { 26f, 0f, -26f };
        for (int a = 0; a < 3; a++)
        {
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(seg.GetComponent<Collider>());
            seg.transform.SetParent(root.transform, false);
            seg.transform.localPosition = new Vector3(ax[a], ay[a], 0f);
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, az[a]);
            seg.transform.localScale = new Vector3(1.5f, 0.5f, 0.66f);
            seg.GetComponent<MeshRenderer>().sharedMaterial = stone;
        }
    }

    // The colourful "reference-art" meadow ring: autumn-tinted trees (via MPB - the unify shader's
    // _BaseColor multiplies vertex colours), bright white rocks and flower clumps around the spawn.
    static void CreateMeadowBeauty()
    {
        var root = new GameObject("MeadowBeauty");
        var mpb = new MaterialPropertyBlock();
        Color[] autumn =
        {
            new Color(0.95f, 0.4f, 0.28f),  // red
            new Color(1f, 0.6f, 0.22f),     // orange
            new Color(1f, 0.82f, 0.3f),     // gold
            new Color(0.95f, 0.55f, 0.65f), // pink blossom
            new Color(0.65f, 0.95f, 0.45f), // fresh green
        };
        string[] trees = { "BirchTree_1", "BirchTree_2", "BirchTree_3", "PineTree_1", "PineTree_2" };

        for (int i = 0; i < 26; i++)
        {
            float ang = i * 0.618f * 6.2831f;
            float rad = 16f + (i * 37 % 40);
            Vector3 pos = new Vector3(Mathf.Sin(ang) * rad, 0f, Mathf.Cos(ang) * rad);
            if (TooCloseToWater(pos, 3f) || FootpathWeight(pos.x, pos.z) > 0.2f)
                continue;
            pos.y = SampleTerrainHeight(pos);
            GameObject tr = InstantiateFreeNature(trees[i % trees.Length], pos, 3.4f + (i % 3) * 0.9f);
            if (tr == null)
                continue;
            tr.transform.SetParent(root.transform, true);
            mpb.SetColor("_BaseColor", autumn[i % autumn.Length]);
            foreach (var r in tr.GetComponentsInChildren<MeshRenderer>())
                r.SetPropertyBlock(mpb);
        }

        // Bright white rocks like in the reference image.
        for (int i = 0; i < 12; i++)
        {
            float ang = (i * 0.618f + 0.31f) * 6.2831f;
            float rad = 12f + (i * 29 % 36);
            Vector3 pos = new Vector3(Mathf.Sin(ang) * rad, 0f, Mathf.Cos(ang) * rad);
            if (TooCloseToWater(pos, 2.5f))
                continue;
            pos.y = SampleTerrainHeight(pos);
            GameObject rock = InstantiateFreeNature("Rock_1", pos, 0.8f + (i % 3) * 0.55f);
            if (rock == null)
                continue;
            rock.transform.SetParent(root.transform, true);
            rock.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
            mpb.SetColor("_BaseColor", new Color(1.35f, 1.33f, 1.26f));
            foreach (var r in rock.GetComponentsInChildren<MeshRenderer>())
                r.SetPropertyBlock(mpb);
        }

        // Flower clumps scattered through the meadow ring.
        string[] flowers = { "Flower_1_Clump", "Flower_2_Clump", "Flower_3_Clump", "Flower_4_Clump", "Flower_5_Clump" };
        for (int i = 0; i < 34; i++)
        {
            float ang = (i * 0.618f + 0.62f) * 6.2831f;
            float rad = 8f + (i * 23 % 44);
            Vector3 pos = new Vector3(Mathf.Sin(ang) * rad, 0f, Mathf.Cos(ang) * rad);
            if (TooCloseToWater(pos, 2f))
                continue;
            pos.y = SampleTerrainHeight(pos);
            GameObject fl = InstantiateFreeNature(flowers[i % flowers.Length], pos, 0.4f);
            if (fl != null)
                fl.transform.SetParent(root.transform, true);
        }
    }

    // Flower bushes lining both footpaths - the trails read as tended, cozy walkways.
    static void CreatePathFlora()
    {
        string[] models = { "Bush_Flowers", "Bush_Small_Flowers", "Bush_Small", "Bush_Large_Flowers" };
        var root = new GameObject("PathFlora");
        int placed = 0;
        for (int p = 0; p < 2; p++)
        {
            for (float s = 7f; s < 142f; s += 5.5f)
            {
                float side = (placed % 2 == 0) ? 1.9f : -1.9f;
                Vector3 pos = p == 0
                    ? new Vector3(Mathf.Sin(s * 0.09f) * 6f + side, 0f, s + 1.8f)
                    : new Vector3(-s, 0f, Mathf.Cos(-s * 0.08f) * 6f + side);
                if (TooCloseToWater(pos, 2.5f))
                    continue;
                pos.y = SampleTerrainHeight(pos);

                GameObject bush = InstantiateFreeNature(models[placed % models.Length], pos, 0.55f + (placed % 3) * 0.15f);
                if (bush != null)
                    bush.transform.SetParent(root.transform, true);
                placed++;
            }
        }
        Debug.Log($"[PathFlora] {placed} bushes along the footpaths");
    }

    // Patches of warm glow-butterflies fluttering low over the meadow - ground-level life.
    static void CreateButterflyPatch(Vector3 center)
    {
        center.y = SampleTerrainHeight(center) + 1.1f;
        var go = new GameObject("Butterflies");
        go.transform.position = center;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 8f;
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new Color(1f, 0.75f, 0.5f, 0.8f);
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var em = ps.emission;
        em.rateOverTime = 2.5f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(11f, 1.6f, 11f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.85f;
        noise.frequency = 0.4f;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        Shader psSh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (psSh == null) psSh = Shader.Find("Sprites/Default");
        var bmat = new Material(psSh);
        if (bmat.HasProperty("_SrcBlend")) bmat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (bmat.HasProperty("_DstBlend")) bmat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (bmat.HasProperty("_ZWrite")) bmat.SetFloat("_ZWrite", 0f);
        bmat.mainTexture = GetSoftParticleTexture();
        bmat.renderQueue = 3100;
        rend.material = bmat;
    }

    static float SampleTerrainHeight(Vector3 worldPos)
    {
        if (groundTerrain == null)
            return 0f;
        return groundTerrain.SampleHeight(worldPos);
    }

    static Material waterMaterial;

    static Material GetWaterMaterial()
    {
        if (waterMaterial != null)
            return waterMaterial;

        // OPAQUE bright blue so rivers are a clear, visible feature (the old transparent water barely
        // showed and didn't render reliably in the batch capture).
        waterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // NAME IS LOAD-BEARING: UnifyPropMaterials skips materials containing "Water" - the unnamed
        // material got swapped to the single-sided stylized shader, and since the ribbon's normals
        // point DOWN the rivers turned invisible from above. Naming it keeps URP/Lit + Cull Off.
        waterMaterial.name = "WaterMaterial";
        waterMaterial.SetColor("_BaseColor", new Color(0.16f, 0.46f, 0.82f, 1f));
        waterMaterial.SetFloat("_Smoothness", 0.92f);
        waterMaterial.SetFloat("_Metallic", 0.1f);
        waterMaterial.SetFloat("_Cull", 0f); // double-sided: the ribbon mesh normals point down, so a
        waterMaterial.SetInt("_Cull", 0);    // single-sided material was invisible from above
        return waterMaterial;
    }

    static Mesh BuildRiverRibbonMesh(int river, int steps, float width)
    {
        Vector3[] vertices = new Vector3[(steps + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[steps * 6];

        for (int i = 0; i <= steps; i++)
        {
            float s = i / (float)steps;
            Vector3 p = RiverPointN(river, s);
            Vector3 pNext = RiverPointN(river, Mathf.Min(1f, s + 0.01f));
            Vector3 tangent = (pNext - p).normalized;
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x) * (width * 0.5f);
            float y = RiverBedNormalizedHeight(s) * RiverMaxHeight + 0.5f; // water sits well above the bed

            vertices[i * 2] = new Vector3(p.x - side.x, y, p.z - side.z);
            vertices[i * 2 + 1] = new Vector3(p.x + side.x, y, p.z + side.z);
            uvs[i * 2] = new Vector2(0f, s * steps * 0.3f);
            uvs[i * 2 + 1] = new Vector2(1f, s * steps * 0.3f);

            if (i < steps)
            {
                int b = i * 2;
                triangles[i * 6 + 0] = b;
                triangles[i * 6 + 1] = b + 2;
                triangles[i * 6 + 2] = b + 1;
                triangles[i * 6 + 3] = b + 1;
                triangles[i * 6 + 4] = b + 2;
                triangles[i * 6 + 5] = b + 3;
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void CreateRiverWater()
    {
        // A wide water ribbon for each of the three rivers that cross the map.
        for (int r = 0; r < RiverCount; r++)
        {
            GameObject river = new GameObject("River_" + r);
            var filter = river.AddComponent<MeshFilter>();
            var mesh = BuildRiverRibbonMesh(r, 90, 15f);
            filter.sharedMesh = mesh;
            var renderer = river.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = GetWaterMaterial();
            Debug.Log($"[River] {river.name} verts={mesh.vertexCount} bounds={mesh.bounds.center.ToString("0")}/{mesh.bounds.size.ToString("0")} mat={renderer.sharedMaterial.shader.name}");
        }
    }

    static Mesh BuildDiscMesh(float radius, int segments)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        int[] triangles = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static readonly string[] LakeShoreProps = { "lily_large", "lily_small", "rock_smallB", "rock_smallD", "ground_riverRocks" };

    static void CreateLake()
    {
        GameObject lake = new GameObject("Lake");
        var filter = lake.AddComponent<MeshFilter>();
        filter.sharedMesh = BuildDiscMesh(LakeRadius, 28);
        var renderer = lake.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.sharedMaterial = GetWaterMaterial();
        lake.transform.position = LakeCenter + Vector3.up * (LakeBedNormalizedHeight * RiverMaxHeight + 0.1f);
        lake.isStatic = true;

        Random.InitState(3131);
        for (int i = 0; i < 14; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = LakeRadius * Random.Range(0.55f, 1.05f);
            Vector3 pos = LakeCenter + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
            pos.y = SampleTerrainHeight(pos) + 0.05f;

            string modelName = LakeShoreProps[Random.Range(0, LakeShoreProps.Length)];
            GameObject prop = InstantiateNature(modelName, pos);
            if (prop == null)
                continue;

            prop.name = "LakeShore_" + i;
            prop.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            prop.transform.localScale = Vector3.one * Random.Range(0.8f, 1.4f);
            prop.isStatic = true;
        }
    }

    static readonly string[] MeadowTrees = { "tree_default", "tree_oak", "tree_pineRoundA", "tree_pineTallA", "tree_small", "tree_fat" };
    static readonly string[] MeadowRocks = { "rock_smallA", "rock_smallC", "rock_largeB", "stone_smallA" };
    static readonly string[] MeadowGround = { "grass", "grass_large", "flower_purpleA", "flower_redA", "flower_yellowA", "mushroom_red", "mushroom_tan", "plant_bushSmall", "plant_flatShort" };

    // Dense jungle from the RELIABLE Kenney NatureKit. (The Quaternius "Assets Free Neu" FBX cast
    // shadows but never render their visible mesh in this URP pipeline - a broken import - so they're
    // unusable here.) Palms + varied trees give the jungle feel; these all render with their textures.
    static readonly string[] KJungleTrees = { "tree_palm", "tree_palmTall", "tree_default", "tree_oak", "tree_pineRoundA", "tree_pineTallA", "tree_thin", "tree_cone", "tree_fat" };
    static readonly string[] KBushes = { "plant_bush", "plant_bushLarge", "plant_bushDetailed", "plant_bushLargeTriangle", "plant_bushTriangle", "mushroom_redTall", "mushroom_redGroup", "mushroom_tanGroup" };
    static readonly string[] KGrass = { "grass_large", "grass_leafsLarge", "grass_leafs", "plant_flatTall", "plant_flatShort", "grass" };
    static readonly string[] KFlowers = { "flower_redA", "flower_purpleA", "flower_yellowA", "flower_redC", "flower_purpleB", "flower_redB", "mushroom_red" };
    static readonly string[] KRocks = { "rock_largeA", "rock_largeB", "rock_largeC", "rock_tallA", "rock_tallB" };

    // A couple of ancient-ruin clusters (toppled stone columns + blocks) as focal landmarks in the
    // jungle, like the arch in the reference. Placed at fixed, visible spots and given colliders so
    // they're part of the scenery you can stand on.
    static void CreateJungleRuins()
    {
        var root = new GameObject("JungleRuins");
        // Several ruin clusters spread across the whole map as landmarks (kept off the spawn cage).
        (Vector3 center, float baseRot)[] sites =
        {
            (new Vector3(40f, 0f, 46f), 20f),
            (new Vector3(-52f, 0f, -24f), -35f),
            (new Vector3(-90f, 0f, 80f), 60f),
            (new Vector3(120f, 0f, -60f), -15f),
            (new Vector3(30f, 0f, -150f), 100f),
            (new Vector3(-160f, 0f, -110f), 200f),
            (new Vector3(150f, 0f, 140f), -70f),
        };
        (string model, Vector3 offset, float yaw, float pitch, float scale)[] parts =
        {
            ("statue_column", new Vector3(-4.5f, 0f, 0f), 0f, 0f, 2.6f),
            ("statue_column", new Vector3(4.5f, 0f, 1f), 0f, 0f, 2.6f),
            ("statue_ring", new Vector3(0f, 0f, -0.5f), 0f, 0f, 3.2f),
            ("statue_columnDamaged", new Vector3(2f, 0f, -5f), 40f, 78f, 2.3f),  // toppled
            ("statue_block", new Vector3(-3.5f, 0f, -4f), 15f, 0f, 1.8f),
            ("statue_columnDamaged", new Vector3(-6f, 0f, 4f), 0f, 0f, 2.0f),
        };

        foreach (var (center, baseRot) in sites)
        {
            foreach (var p in parts)
            {
                Vector3 pos = center + Quaternion.Euler(0f, baseRot, 0f) * p.offset;
                pos.y = SampleTerrainHeight(pos) - 0.15f;
                GameObject obj = InstantiateNature(p.model, pos);
                if (obj == null)
                    continue;
                obj.transform.SetParent(root.transform);
                obj.transform.localScale = Vector3.one * p.scale;
                obj.transform.rotation = Quaternion.Euler(p.pitch, baseRot + p.yaw, 0f);
                obj.isStatic = true;
                AddSolidCollider(obj);
                obj.AddComponent<DistanceCuller>().maxDistance = 200f;
            }
        }
    }

    // Gives each tree a slightly different canopy tint (mostly greens, some gold/autumn) via a
    // MaterialPropertyBlock, so the forest is colourful and varied like the reference instead of one
    // flat teal. Multiplies the shared texture (keeps GPU instancing - no per-tree material clones).
    static void TintFoliage(GameObject obj)
    {
        float r = Random.value;
        Color tint;
        if (r < 0.7f)
            tint = new Color(Random.Range(0.4f, 0.7f), Random.Range(0.85f, 1.05f), Random.Range(0.4f, 0.6f));  // rich fresh greens
        else if (r < 0.86f)
            tint = new Color(Random.Range(0.85f, 1.15f), Random.Range(0.92f, 1.05f), Random.Range(0.35f, 0.5f)); // gold / lime
        else
            tint = new Color(Random.Range(1.35f, 1.75f), Random.Range(0.68f, 0.88f), Random.Range(0.35f, 0.5f));  // autumn orange/red

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", tint);
        foreach (var rend in obj.GetComponentsInChildren<Renderer>())
            rend.SetPropertyBlock(mpb);
    }

    // A properly DENSE jungle floor: an even grid (with jitter) of trees + undergrowth covering the
    // whole playable ground. Objects sink ~0.1m so nothing floats on slopes; rivers + spawn stay clear.
    static void CreateNatureScatter()
    {
        Random.InitState(777);
        GameObject root = new GameObject("NatureScatter");
        int idx = 0;

        const float area = 232f;  // fill the WHOLE map (terrain radius ~240), not just near spawn
        const float step = 5.2f;  // grid spacing - small = very dense
        for (float gx = -area; gx <= area; gx += step)
        {
            for (float gz = -area; gz <= area; gz += step)
            {
                float jx = gx + Random.Range(-step * 0.48f, step * 0.48f);
                float jz = gz + Random.Range(-step * 0.48f, step * 0.48f);
                float distC = Mathf.Sqrt(jx * jx + jz * jz);
                if (distC < 13f || distC > area)
                    continue;                        // clear the spawn, keep it circular
                Vector3 pos = new Vector3(jx, 0f, jz);
                if (TooCloseToWater(pos, 2f))
                    continue;                        // keep the river/lake open
                pos.y = SampleTerrainHeight(pos) - 0.1f; // slight embed so nothing floats on slopes

                // Mostly ground cover (bushes + grass tufts + flowers) with trees on top, so the ground
                // between trees is thick with foliage - not bare green.
                float roll = Random.value;
                string model;
                float scale;
                bool clutter;
                bool isTree = false;
                if (roll < 0.20f) { model = KJungleTrees[Random.Range(0, KJungleTrees.Length)]; scale = Random.Range(2.2f, 3.8f); clutter = false; isTree = true; }
                else if (roll < 0.26f) { model = KRocks[Random.Range(0, KRocks.Length)]; scale = Random.Range(1.0f, 2.4f); clutter = false; }
                else if (roll < 0.52f) { model = KBushes[Random.Range(0, KBushes.Length)]; scale = Random.Range(1.2f, 2.5f); clutter = true; }
                else if (roll < 0.80f) { model = KGrass[Random.Range(0, KGrass.Length)]; scale = Random.Range(1.3f, 2.8f); clutter = true; }
                else { model = KFlowers[Random.Range(0, KFlowers.Length)]; scale = Random.Range(1.0f, 1.9f); clutter = true; }

                GameObject obj = InstantiateNature(model, pos);
                if (obj == null)
                    continue;
                obj.name = "Nature_" + idx++;
                obj.transform.SetParent(root.transform, true);
                obj.transform.localScale = Vector3.one * scale;
                obj.transform.Rotate(0f, Random.Range(0f, 360f), 0f, Space.World);
                obj.isStatic = true;
                if (isTree)
                    TintFoliage(obj);
                if (clutter)
                    foreach (var r in obj.GetComponentsInChildren<Renderer>())
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Long render distances so the WHOLE filled map is visible from up the tower (trees
                // furthest since they read best at range). Distance culling keeps it performant.
                obj.AddComponent<DistanceCuller>().maxDistance = isTree ? 320f : (clutter ? 150f : 240f);
            }
        }
        Debug.Log($"[NatureScatter] placed {root.transform.childCount} objects");
    }

    // Renders the built scene to a PNG from a fixed vantage so world edits can be reviewed without a
    // manual in-game screenshot. Run via -executeMethod SceneBuilder.BuildAndCapture.
    public static void BuildAndCapture()
    {
        BuildScene();

        var camGO = new GameObject("CaptureCam");
        var cam = camGO.AddComponent<Camera>();
        var camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        // Make the capture actually apply the post-process volume (bloom + blue-violet night grade),
        // so these headless previews match the in-game look instead of showing raw base lighting.
        camData.volumeLayerMask = ~0;              // sample volumes on every layer
        camData.volumeTrigger = camGO.transform;
        camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        cam.allowHDR = true;
        cam.cullingMask = ~(1 << 9); // skip the cosmetic preview stage
        cam.fieldOfView = 55f;
        cam.farClipPlane = 900f;

        string dir = "C:/Users/Terne/AppData/Local/Temp/claude/C--Users-Terne-Desktop-Unn-tig-Claude-Code/6ab2e0fe-26a0-4e13-9989-0f1cb7d4113e/scratchpad";
        System.IO.Directory.CreateDirectory(dir);

        (Vector3 pos, Vector3 look, string name)[] shots =
        {
            (new Vector3(0f, 45f, -95f), new Vector3(0f, 6f, 20f), "world_overview"),
            (new Vector3(70f, 18f, -70f), new Vector3(20f, 3f, 20f), "world_ground"),
            (new Vector3(0f, 120f, -10f), new Vector3(0f, 0f, 40f), "world_top"),
            (new Vector3(0f, 10f, 20f), new Vector3(0f, 1f, 60f), "world_river"),
        };

        foreach (var (pos, look, name) in shots)
        {
            camGO.transform.position = pos;
            camGO.transform.LookAt(look);
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 2 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render(); // second pass: first frame can miss PP resource setup in edit mode
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes($"{dir}/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Debug.Log($"[Capture] wrote {name}.png");
        }
    }

    static bool TooCloseToWater(Vector3 pos, float clearance)
    {
        float distRiver = DistanceToRiver(pos.x, pos.z, out _);
        float distLake = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(LakeCenter.x, LakeCenter.z)) - LakeRadius;
        return distRiver < clearance || distLake < clearance;
    }

    static GameObject InstantiatePrefabByPath(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return null;
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    const string PurePolyPrefabPath = "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/";
    const string SimpleNaturePrefabPath = "Assets/SimpleNaturePack/Prefabs/";
    const string SkydenPrefabPath = "Assets/Skyden_Games/Low Poly Environment/Prefabs/";

    static readonly string[] PurePolyScatterPrefabs =
    {
        "PP_Birch_Tree_05", "PP_Birch_Tree_06", "PP_Tree_02", "PP_Tree_10",
        "PP_Rock_Moss_Grown_09", "PP_Rock_Moss_Grown_11", "PP_Rock_Pile_Forest_Moss_05", "PP_Rock_Pile_Forest_Moss_10",
        "PP_Mushroom_Fantasy_Orange_09", "PP_Mushroom_Fantasy_Orange_10", "PP_Mushroom_Fantasy_Purple_05", "PP_Mushroom_Fantasy_Purple_08",
        "PP_Daffodil_03", "PP_Hyacinth_04", "PP_Sunflower_04", "PP_Grass_11", "PP_Grass_15",
    };

    static readonly string[] SimpleNatureScatterPrefabs =
    {
        "Tree_01", "Tree_02", "Tree_03", "Tree_04", "Tree_05",
        "Bush_01", "Bush_02", "Bush_03",
        "Rock_01", "Rock_02", "Rock_03", "Rock_04", "Rock_05",
        "Mushroom_01", "Mushroom_02", "Flowers_01", "Flowers_02", "Stump_01", "Branch_01",
    };

    // Adds variety from the newly-added Pure Poly / SimpleNaturePack asset packs into the same
    // mid-ground ring as ForestBelt/RockFormations, plus a river bridge and a couple of Skyden
    // cliffs/waterfall as extra landmark set-dressing.
    static void CreateBonusNatureScatter()
    {
        Random.InitState(6006);
        GameObject root = new GameObject("BonusNatureScatter");
        int count = 70;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(70f, 200f);
            Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            if (TooCloseToWater(pos, 8f))
                continue;
            pos.y = SampleTerrainHeight(pos);

            bool usePurePoly = Random.value < 0.5f;
            string[] pool = usePurePoly ? PurePolyScatterPrefabs : SimpleNatureScatterPrefabs;
            string prefabPath = (usePurePoly ? PurePolyPrefabPath : SimpleNaturePrefabPath) + pool[Random.Range(0, pool.Length)] + ".prefab";

            GameObject obj = InstantiatePrefabByPath(prefabPath);
            if (obj == null)
                continue;

            obj.name = "BonusNature_" + i;
            obj.transform.SetParent(root.transform);
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            obj.transform.localScale = Vector3.one * Random.Range(0.8f, 1.3f);
            obj.isStatic = true;
            obj.AddComponent<DistanceCuller>().maxDistance = 190f;
        }

        // A bridge crossing the river downstream of the waterfall, oriented across the flow.
        const float bridgeS = 0.62f;
        Vector3 bridgeCenter = RiverPoint(bridgeS);
        GameObject bridge = InstantiatePrefabByPath(PurePolyPrefabPath + "PP_Bridge_15_Middle.prefab");
        if (bridge != null)
        {
            bridge.name = "RiverBridge";
            bridge.transform.SetParent(root.transform);
            bridge.transform.position = bridgeCenter + Vector3.up * (RiverBedNormalizedHeight(bridgeS) * RiverMaxHeight + 0.1f);
            Vector3 tangent = (RiverPoint(bridgeS + 0.02f) - RiverPoint(bridgeS - 0.02f)).normalized;
            bridge.transform.rotation = Quaternion.LookRotation(new Vector3(-tangent.z, 0f, tangent.x));
        }

        // A couple of Skyden cliffs near the rock formation belt, and its waterfall prefab
        // near the lake, for extra landmark variety beyond our procedural rock/water shapes.
        (string name, float angleDeg, float radius)[] skydenSpots =
        {
            ("Cliff 3", 40f, 165f),
            ("Cliff 6", 210f, 175f),
        };
        foreach (var spot in skydenSpots)
        {
            float rad = spot.angleDeg * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Sin(rad) * spot.radius, 0f, Mathf.Cos(rad) * spot.radius);
            if (TooCloseToWater(pos, 12f))
                continue;
            pos.y = SampleTerrainHeight(pos) - 0.5f;

            GameObject cliff = InstantiatePrefabByPath(SkydenPrefabPath + spot.name + ".prefab");
            if (cliff == null)
                continue;
            cliff.name = "SkydenCliff_" + spot.name;
            cliff.transform.SetParent(root.transform);
            cliff.transform.position = pos;
            cliff.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            cliff.isStatic = true;
            cliff.AddComponent<DistanceCuller>().maxDistance = 220f;
        }

        GameObject waterfall = InstantiatePrefabByPath(SkydenPrefabPath + "Water Fall.prefab");
        if (waterfall != null)
        {
            waterfall.name = "SkydenWaterfall";
            waterfall.transform.SetParent(root.transform);
            Vector3 wfPos = LakeCenter + new Vector3(LakeRadius + 6f, 0f, 4f);
            wfPos.y = SampleTerrainHeight(wfPos);
            waterfall.transform.position = wfPos;
            waterfall.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
        }
    }

    static readonly string[] ForestTrees = { "tree_default", "tree_oak", "tree_pineTallA", "tree_pineTallB", "tree_pineTallC", "tree_fat", "tree_tall", "tree_detailed" };

    // Fills the empty ring between the close-in meadow (out to ~65) and the mountain range
    // (starting at 260) with clustered stands of trees instead of one uniform scatter -
    // reads as an actual forest rather than props dropped on a plane.
    static void CreateForestBelt()
    {
        Random.InitState(4242);
        GameObject root = new GameObject("ForestBelt");
        int clusterCount = 22;

        for (int c = 0; c < clusterCount; c++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(75f, 195f);
            Vector3 clusterCenter = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            if (TooCloseToWater(clusterCenter, 14f))
                continue;

            int treeCount = Random.Range(4, 9);
            for (int t = 0; t < treeCount; t++)
            {
                Vector3 pos = clusterCenter + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                if (TooCloseToWater(pos, 6f))
                    continue;
                pos.y = SampleTerrainHeight(pos);

                string modelName = ForestTrees[Random.Range(0, ForestTrees.Length)];
                GameObject tree = InstantiateNature(modelName, pos);
                if (tree == null)
                    continue;

                tree.name = "ForestTree_" + c + "_" + t;
                tree.transform.SetParent(root.transform);
                tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                tree.transform.localScale = Vector3.one * Random.Range(1.1f, 1.8f);
                tree.isStatic = true;
                tree.AddComponent<DistanceCuller>().maxDistance = 210f;
            }
        }
    }

    static readonly string[] RockFormationModels =
    {
        "cliff_large_rock", "cliff_block_rock", "cliff_diagonal_rock",
        "rock_tallA", "rock_tallC", "rock_tallF", "stone_tallB", "stone_tallE",
    };

    // Clustered rock/cliff formations scattered through the same mid-ground ring as the forest
    // belt, giving the horizon actual "Felsformationen" instead of only trees.
    static void CreateRockFormations()
    {
        Random.InitState(9090);
        GameObject root = new GameObject("RockFormations");
        int clusterCount = 16;

        for (int c = 0; c < clusterCount; c++)
        {
            float angle = (c / (float)clusterCount) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            float radius = Random.Range(75f, 210f);
            Vector3 clusterCenter = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            if (TooCloseToWater(clusterCenter, 16f))
                continue;

            int rockCount = Random.Range(2, 5);
            for (int r = 0; r < rockCount; r++)
            {
                Vector3 pos = clusterCenter + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                if (TooCloseToWater(pos, 10f))
                    continue;
                pos.y = SampleTerrainHeight(pos) - 0.3f;

                string modelName = RockFormationModels[Random.Range(0, RockFormationModels.Length)];
                GameObject rock = InstantiateNature(modelName, pos);
                if (rock == null)
                    continue;

                rock.name = "RockFormation_" + c + "_" + r;
                rock.transform.SetParent(root.transform);
                rock.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                rock.transform.localScale = Vector3.one * Random.Range(1.8f, 3.2f);
                rock.isStatic = true;
                rock.AddComponent<DistanceCuller>().maxDistance = 230f;
            }
        }
    }

    // A jagged peak instead of a perfect cone: the apex ring wobbles per-vertex so the
    // silhouette reads as a rocky ridge rather than a smooth pyramid.
    static Mesh BuildJaggedPeakMesh(float radius, float height, int segments, float seed)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            float r = radius * (0.85f + HashFloat(seed + i * 1.7f) * 0.3f);
            vertices[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
        float apexJitter = (HashFloat(seed + 40f) - 0.5f) * radius * 0.3f;
        vertices[segments + 1] = new Vector3(apexJitter, height, apexJitter * 0.6f);

        int[] triangles = new int[segments * 3 * 2];
        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[t++] = i + 1;
            triangles[t++] = next + 1;
            triangles[t++] = segments + 1;
        }
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[t++] = 0;
            triangles[t++] = next + 1;
            triangles[t++] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material rockMaterial;
    static Material snowCapMaterial;

    static Material GetRockMaterial()
    {
        if (rockMaterial != null)
            return rockMaterial;
        rockMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        rockMaterial.SetColor("_BaseColor", new Color(0.34f, 0.36f, 0.42f));
        rockMaterial.SetFloat("_Smoothness", 0.05f);
        rockMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        return rockMaterial;
    }

    static Material GetSnowCapMaterial()
    {
        if (snowCapMaterial != null)
            return snowCapMaterial;
        snowCapMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        snowCapMaterial.SetColor("_BaseColor", new Color(0.93f, 0.95f, 0.98f));
        snowCapMaterial.SetFloat("_Smoothness", 0.1f);
        snowCapMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        return snowCapMaterial;
    }

    static readonly string[] MountainPrefabPaths =
    {
        SkydenPrefabPath + "Mountain 1.prefab",
        SkydenPrefabPath + "Mountain 2 .prefab",
        PurePolyPrefabPath + "PP_Forest_Mountain_Moss_01.prefab",
        PurePolyPrefabPath + "PP_Forest_Mountain_Moss_02.prefab",
    };

    // Combined world-space Y-height of all renderers under an object (at its current transform).
    static float RendererHeight(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return 0f;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers)
            b.Encapsulate(r.bounds);
        return b.size.y;
    }

    // Real low-poly mountain models instead of the old procedural grey cones. Each is normalized
    // to a target world height via its renderer bounds, so the unknown native model scale doesn't
    // matter. Densely ringed with overlap so no gaps show the void between peaks.
    static void CreateMountainRing()
    {
        Random.InitState(2024);
        GameObject root = new GameObject("MountainRange");
        int count = 46;

        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.06f, 0.06f);
            float radius = Random.Range(250f, 430f);

            string path = MountainPrefabPaths[i % MountainPrefabPaths.Length];
            GameObject m = InstantiatePrefabByPath(path);
            if (m == null)
                continue;

            m.transform.position = Vector3.zero;
            m.transform.rotation = Quaternion.identity;
            m.transform.localScale = Vector3.one;
            float nativeHeight = RendererHeight(m);
            float targetHeight = Random.Range(85f, 190f);
            float scale = nativeHeight > 0.01f ? targetHeight / nativeHeight : 20f;

            m.name = "Mountain_" + i;
            m.transform.SetParent(root.transform);
            m.transform.localScale = Vector3.one * scale;
            m.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            // Sink the base a bit so it reads as rising out of the ground/haze.
            m.transform.position = new Vector3(Mathf.Sin(angle) * radius, -targetHeight * 0.12f, Mathf.Cos(angle) * radius);
            m.isStatic = true;
        }
    }

    static readonly string[] VegetationModels = { "tree", "tree-pine", "mushrooms", "plant", "flowers", "flag" };
    static bool IsVegetation(string modelName) => System.Array.IndexOf(VegetationModels, modelName) >= 0;

    static void ApplyDecorFlags(GameObject obj, string modelName)
    {
        if (IsVegetation(modelName))
            obj.AddComponent<WindSway>();
        else
            obj.isStatic = true;
    }

    static readonly string[] SpawnDecorModels = { "log", "log_large", "stump_round", "stump_old", "rock_smallA", "rock_smallE" };

    // Close-in dressing around the spawn point. Was industrial crates/barrels left over from
    // an earlier theme - replaced with Nature Kit logs/stumps/rocks that actually fit Wiesenland.
    static void CreateSpawnDecor()
    {
        GameObject decor = new GameObject("SpawnDecor");
        for (int i = 0; i < 14; i++)
        {
            float angle = i * 0.7f;
            float radius = 6f + (i % 3) * 1.8f;
            Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            pos.y = SampleTerrainHeight(pos);

            string propName = SpawnDecorModels[i % SpawnDecorModels.Length];
            GameObject prop = InstantiateNature(propName, pos);
            if (prop == null)
                continue;

            prop.name = "SpawnDecor_" + i;
            prop.transform.SetParent(decor.transform);
            prop.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
            prop.isStatic = true;
        }
    }

    static readonly string[] LandmarkModels = { "statue_obelisk", "statue_column", "statue_columnDamaged", "statue_ring", "statue_block" };

    // A distant ruin/monolith cluster on the horizon in one direction - a fitting "entfernte
    // Landmarke" for a nature world, replacing a grey sci-fi skyline that never matched the theme.
    static void CreateDistantLandmark()
    {
        Random.InitState(8181);
        GameObject root = new GameObject("DistantLandmark");
        Vector3 center = new Vector3(Mathf.Sin(0.6f) * 300f, -10f, Mathf.Cos(0.6f) * 300f);

        for (int i = 0; i < 9; i++)
        {
            Vector3 pos = center + new Vector3(Random.Range(-18f, 18f), 0f, Random.Range(-18f, 18f));

            string modelName = LandmarkModels[Random.Range(0, LandmarkModels.Length)];
            GameObject obj = InstantiateNature(modelName, pos);
            if (obj == null)
                continue;

            obj.name = "Landmark_" + i;
            obj.transform.SetParent(root.transform);
            obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            obj.transform.localScale = Vector3.one * Random.Range(3f, 6f);
            obj.isStatic = true;
            obj.AddComponent<DistanceCuller>().maxDistance = 420f;
        }
    }

    // Floating islands with a waterfall each, visible from ground level looking outward -
    // distinct from the tower-climb ambience islands, which sit much closer to the spiral.
    static void CreateBackgroundIslands()
    {
        Random.InitState(7070);
        GameObject root = new GameObject("BackgroundIslands");
        int count = 6;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(160f, 240f);
            float height = Random.Range(25f, 65f);
            Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);

            GameObject island = new GameObject("BackgroundIsland_" + i);
            island.transform.SetParent(root.transform);
            island.transform.position = pos;
            island.AddComponent<DistanceCuller>().maxDistance = 280f;

            float w = Random.Range(10f, 18f);
            MakeAmbienceBlock(island.transform, pos, new Vector3(w, 3f, w), MakeUnlitColor(new Color(0.42f, 0.36f, 0.28f)));
            MakeAmbienceBlock(island.transform, pos + Vector3.up * 1.6f, new Vector3(w * 0.9f, 1.2f, w * 0.9f), MakeUnlitColor(new Color(0.32f, 0.52f, 0.24f)));

            int treeCount = Random.Range(1, 3);
            for (int t = 0; t < treeCount; t++)
            {
                Vector3 treePos = pos + new Vector3(Random.Range(-w * 0.3f, w * 0.3f), 2.4f, Random.Range(-w * 0.3f, w * 0.3f));
                GameObject tree = InstantiateNature("tree_pineTallA", treePos);
                if (tree == null)
                    continue;
                tree.transform.SetParent(island.transform);
                tree.transform.localScale = Vector3.one * Random.Range(1.3f, 2f);
            }

            GameObject fall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fall.name = "IslandWaterfall";
            Object.DestroyImmediate(fall.GetComponent<Collider>());
            fall.transform.SetParent(island.transform);
            fall.transform.position = pos + new Vector3(w * 0.5f, -1.2f - 8f, 0f);
            fall.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            fall.transform.localScale = new Vector3(3f, 16f, 1f);
            fall.GetComponent<Renderer>().sharedMaterial = GetWaterMaterial();
        }
    }

    static void CreateClouds()
    {
        Random.InitState(1234);
        GameObject cloudRoot = new GameObject("Clouds");
        int cloudCount = 18;

        for (int i = 0; i < cloudCount; i++)
        {
            float angle = i * 2.4f;
            float radius = 15f + (i % 5) * 5f;
            // Cluster the clouds around the Wolkenreich band (0.4-0.6 of the climb).
            float height = Mathf.Lerp(actualTopHeight * 0.4f, actualTopHeight * 0.6f, (i % 7) / 6f);
            Vector3 center = new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);

            GameObject cloud = new GameObject("Cloud_" + i);
            cloud.transform.SetParent(cloudRoot.transform);
            cloud.transform.position = center;
            cloud.AddComponent<DistanceCuller>().maxDistance = 130f;

            int puffCount = 4 + (i % 3);
            for (int p = 0; p < puffCount; p++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "Puff";
                puff.transform.SetParent(cloud.transform);
                puff.transform.localPosition = new Vector3(
                    Mathf.Sin(p * 2.1f + i) * 1.8f,
                    Mathf.Sin(p * 1.3f) * 0.4f,
                    Mathf.Cos(p * 2.1f + i) * 1.8f);
                float puffScale = Random.Range(1.6f, 2.6f);
                puff.transform.localScale = Vector3.one * puffScale;
                Object.DestroyImmediate(puff.GetComponent<Collider>());
                SetColor(puff, new Color(0.97f, 0.97f, 1f));
            }
        }
    }

    static Material MakeUnlitColor(Color c)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", c);
        mat.SetFloat("_Smoothness", 0.1f);
        return mat;
    }

    static Material MakeEmissiveColor(Color baseColor, Color emission)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", baseColor);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", emission);
        return mat;
    }

    static GameObject MakeAmbienceBlock(Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(block.GetComponent<Collider>());
        block.transform.SetParent(parent);
        block.transform.position = pos;
        block.transform.localScale = scale;
        block.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
        block.GetComponent<Renderer>().sharedMaterial = mat;
        block.isStatic = true;
        return block;
    }

    static readonly string[] SpaceStationPieces = { "room-small", "room-large", "corridor", "corridor-corner", "gate", "gate-door", "stairs" };

    // Decorative floating islands themed to each of the five worlds, ringed around the tower at
    // that world's height band. Purely cosmetic (no colliders) - gives each section a distinct
    // identity as you climb, matching the reference art's floating-island look.
    static void CreateWorldAmbience()
    {
        Random.InitState(5150);
        GameObject root = new GameObject("WorldAmbience");

        Material grassTop = MakeUnlitColor(new Color(0.32f, 0.55f, 0.24f));
        Material dirt = MakeUnlitColor(new Color(0.45f, 0.34f, 0.24f));
        Material volcanoRock = MakeUnlitColor(new Color(0.18f, 0.13f, 0.12f));
        Material lava = MakeEmissiveColor(new Color(0.3f, 0.08f, 0f), new Color(1.4f, 0.5f, 0.05f));
        Material iceMat = MakeEmissiveColor(new Color(0.6f, 0.8f, 0.95f), new Color(0.3f, 0.55f, 0.75f));
        Material asteroid = MakeUnlitColor(new Color(0.28f, 0.28f, 0.34f));
        Material crystalPurple = MakeEmissiveColor(new Color(0.35f, 0.2f, 0.5f), new Color(0.5f, 0.2f, 0.9f));

        const int perZone = 11;
        for (int zone = 0; zone < 5; zone++)
        {
            float hMin = actualTopHeight * (zone * 0.2f + 0.02f);
            float hMax = actualTopHeight * (zone * 0.2f + 0.18f);

            for (int i = 0; i < perZone; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float rad = Random.Range(18f, 45f);
                float h = Random.Range(hMin, hMax);
                Vector3 islandPos = new Vector3(Mathf.Sin(ang) * rad, h, Mathf.Cos(ang) * rad);

                GameObject island = new GameObject("Island_" + zone + "_" + i);
                island.transform.SetParent(root.transform);
                island.transform.position = islandPos;
                island.AddComponent<DistanceCuller>().maxDistance = 160f;

                float baseW = Random.Range(3f, 6f);

                switch (zone)
                {
                    case 0: // Nature: grass-topped dirt island with a tree
                        MakeAmbienceBlock(island.transform, islandPos, new Vector3(baseW, 1.2f, baseW), grassTop);
                        MakeAmbienceBlock(island.transform, islandPos + Vector3.down * 1.2f, new Vector3(baseW * 0.8f, 1.6f, baseW * 0.8f), dirt);
                        GameObject tree = InstantiateNature(Random.value < 0.5f ? "tree_default" : "tree_pineTallA", islandPos + Vector3.up * 0.7f);
                        if (tree != null)
                        {
                            tree.transform.SetParent(island.transform);
                            tree.transform.localScale = Vector3.one * Random.Range(1.2f, 2f);
                        }
                        break;
                    case 1: // Volcano: dark rock with glowing lava cracks
                        MakeAmbienceBlock(island.transform, islandPos, new Vector3(baseW, Random.Range(2f, 4f), baseW), volcanoRock);
                        MakeAmbienceBlock(island.transform, islandPos + Vector3.up * 0.4f, new Vector3(baseW * 0.5f, 0.4f, baseW * 0.5f), lava);
                        break;
                    case 2: // Cloud: soft white cloud puffs
                        for (int p = 0; p < 4; p++)
                        {
                            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            Object.DestroyImmediate(puff.GetComponent<Collider>());
                            puff.transform.SetParent(island.transform);
                            puff.transform.position = islandPos + new Vector3(Random.Range(-2f, 2f), Random.Range(-0.5f, 0.5f), Random.Range(-2f, 2f));
                            puff.transform.localScale = Vector3.one * Random.Range(2f, 3.2f);
                            puff.GetComponent<Renderer>().sharedMaterial = MakeUnlitColor(new Color(0.97f, 0.97f, 1f));
                            puff.isStatic = true;
                        }
                        break;
                    case 3: // Ice: pale glowing crystal shards
                        MakeAmbienceBlock(island.transform, islandPos, new Vector3(baseW, 1f, baseW), iceMat);
                        for (int c = 0; c < 3; c++)
                        {
                            GameObject shard = MakeAmbienceBlock(island.transform, islandPos + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 2f), Random.Range(-1.5f, 1.5f)), new Vector3(0.6f, Random.Range(2f, 4f), 0.6f), iceMat);
                            shard.transform.rotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
                        }
                        break;
                    default: // Space: dark asteroids + purple crystals + tumbling station wreckage
                        MakeAmbienceBlock(island.transform, islandPos, new Vector3(baseW * 0.8f, baseW * 0.7f, baseW * 0.8f), asteroid);
                        if (Random.value < 0.6f)
                            MakeAmbienceBlock(island.transform, islandPos + Vector3.up * baseW * 0.4f, new Vector3(0.7f, Random.Range(1.5f, 3f), 0.7f), crystalPurple);
                        if (Random.value < 0.45f)
                        {
                            string pieceName = SpaceStationPieces[Random.Range(0, SpaceStationPieces.Length)];
                            Vector3 piecePos = islandPos + new Vector3(Random.Range(-2f, 2f), Random.Range(0.5f, 2f), Random.Range(-2f, 2f));
                            GameObject piece = InstantiateSpaceKit(pieceName, piecePos);
                            if (piece != null)
                            {
                                piece.transform.SetParent(island.transform);
                                piece.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-15f, 50f));
                                piece.transform.localScale = Vector3.one * Random.Range(1.2f, 2f);
                                foreach (var col in piece.GetComponentsInChildren<Collider>())
                                    Object.DestroyImmediate(col);
                            }
                        }
                        break;
                }
            }
        }
    }

    const float MinVerticalGap = 1.0f;
    const float MaxVerticalGap = 3.0f;
    // Wider horizontal spacing so platforms sit clearly apart (props are bigger than the old crates).
    // The jump-safety clamp below keeps every gap reachable (a running double-jump covers ~5.5m), so
    // "further apart" never means "impossible". Combined with the smaller normalized platform size.
    const float MinHorizontalGap = 3.5f;
    const float MaxHorizontalGap = 5.7f;
    const int CheckpointInterval = 12;

    // Mirrors PlayerController's jump physics (moveSpeed/jumpHeight/gravity) so generated
    // gaps can be checked against what a single jump can actually reach.
    const float PlayerMoveSpeed = 7f;
    const float PlayerJumpHeight = 1.8f;
    const float PlayerGravity = -25f;
    const float JumpSafetyMargin = 0.7f;

    static float HashFloat(float seed)
    {
        return Mathf.Abs(Mathf.Sin(seed * 12.9898f + 78.233f)) % 1f;
    }

    // Height a single jump can reach by the time it has covered horizontalGap sideways.
    static float MaxSingleJumpHeightAt(float horizontalGap)
    {
        float v0 = Mathf.Sqrt(PlayerJumpHeight * -2f * PlayerGravity);
        float t = horizontalGap / PlayerMoveSpeed;
        return v0 * t + 0.5f * PlayerGravity * t * t;
    }

    static readonly float[] GateThresholds = { 0.2f, 0.4f, 0.6f, 0.8f };

    // Biases the platform flavor to the current height ZONE so each world has its own parkour feel
    // instead of the same random mix everywhere. Flavors: 0 static, 1 crumbling, 2 moving,
    // 3 swinging, 4 floating, 5 timed, 6 bouncepad.
    static int ZoneFlavor(int block, float t)
    {
        int zone = Mathf.Clamp(Mathf.FloorToInt(t * 5f), 0, 4);
        float r = HashFloat(block * 1.7f + zone * 4.3f);
        switch (zone)
        {
            case 0: return r < 0.85f ? 0 : 6;                                     // Wiese: sanft, meist statisch, selten Sprungfeld
            case 1: return r < 0.42f ? 5 : (r < 0.58f ? 6 : 0);                   // Vulkanfeld: zeitgesteuerte Lava-Platten
            case 2: return r < 0.34f ? 2 : (r < 0.60f ? 4 : (r < 0.82f ? 3 : 0)); // Wolkenreich: beweglich/schwebend/schwingend
            case 3: return r < 0.42f ? 1 : (r < 0.64f ? 5 : 0);                   // Eiskristall: bröckelnd + zeitgesteuert
            default: return r < 0.52f ? 0 : 4;                                    // Sternenkrone: Präzision + schwebend
        }
    }

    // A persisted .mat ASSET. Unlike a runtime `new Material`, this survives SaveAsPrefabAsset -
    // runtime materials assigned to PREFAB children get dropped and render pink. Used for the bow/arrow
    // (which live inside the player prefab) so they never pink out.
    static Material WeaponMaterial(string name, string shaderName, Color color, bool unlit)
    {
        string path = "Assets/Generated/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader sh = Shader.Find(shaderName);
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (m == null)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Generated"));
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (!unlit && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material WeaponBlack() => WeaponMaterial("WeaponBlack", "Universal Render Pipeline/Lit", new Color(0.06f, 0.06f, 0.07f), false);
    static Material WeaponRedGlow() => WeaponMaterial("WeaponRed", "Universal Render Pipeline/Unlit", new Color(1f, 0.16f, 0.2f) * 3f, true);

    // An unlit HDR-bright material that blooms into a soft glow (for crystals, orbs, secret markers).
    static Material MakeGlowMaterial(Color hdrColor)
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Unlit/Color");
        var m = new Material(unlit);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdrColor); else m.color = hdrColor;
        return m;
    }

    // A secret platform across a WIDE gap - too far to jump, reachable only by GLIDING (hold Space and
    // steer). A glowing crystal on it is visible enough to spot and lure you over, and collecting it
    // pays coins + the "secret found" achievement. This gives BOTH the glide a purpose and a real,
    // findable, rewarding secret.
    static void CreateGlideSecret(Vector3 anchorTop, Vector3 dir, Vector3 perpSide, int index)
    {
        var off = UnityEngine.Rendering.ShadowCastingMode.Off;
        // ~9 m to the side (beyond a normal jump of ~5-6 m) and ~2 m down, which matches a glide's
        // gentle descent - so you clear it by holding Space and steering, but not with a plain jump.
        Vector3 pos = anchorTop + perpSide * 9f + dir * 1.5f - new Vector3(0f, 2f, 0f);

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "GlideSecret_" + index;
        pad.transform.position = pos;
        pad.transform.localScale = new Vector3(3.4f, 0.5f, 3.4f); // generous, so a glide landing is fair
        pad.isStatic = true;
        pad.GetComponent<MeshRenderer>().sharedMaterial = GetKenneyMaterial();
        pad.AddComponent<DistanceCuller>().maxDistance = 170f;

        var shardGO = new GameObject("Shard_" + index);
        shardGO.transform.position = pos + new Vector3(0f, 1.0f, 0f);
        var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(crystal.GetComponent<Collider>());
        crystal.transform.SetParent(shardGO.transform, false);
        crystal.transform.localScale = new Vector3(0.3f, 0.55f, 0.3f);
        crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        var cmr = crystal.GetComponent<MeshRenderer>();
        cmr.sharedMaterial = MakeGlowMaterial(new Color(1f, 0.82f, 0.35f) * 2.6f);
        cmr.shadowCastingMode = off;

        var col = shardGO.AddComponent<SphereCollider>();
        col.radius = 1.1f;
        col.isTrigger = true;
        shardGO.AddComponent<SecretShard>();
        shardGO.AddComponent<DistanceCuller>().maxDistance = 170f;
    }

    // Tints a platform toward its height ZONE's colour so each world reads distinctly (green meadow ->
    // warm volcano -> cool clouds -> icy crystal -> cosmic summit). MPB keeps GPU instancing; on the
    // Kenney colour-atlas materials it multiplies, so props keep their shading, just hue-shifted.
    static readonly Color[] ZoneTints =
    {
        new Color(0.62f, 1.00f, 0.45f), // Wiesenland - vivid green
        new Color(1.00f, 0.40f, 0.26f), // Vulkanfeld - hot red/orange
        new Color(0.68f, 0.84f, 1.00f), // Wolkenreich - cool sky blue
        new Color(0.40f, 0.85f, 1.00f), // Eiskristall - bright icy cyan
        new Color(0.72f, 0.44f, 1.00f), // Sternenkrone - cosmic violet
    };

    static void ApplyZoneTint(GameObject platform, float t)
    {
        Color tint = ZoneTints[Mathf.Clamp(Mathf.FloorToInt(t * 5f), 0, 4)];
        var mpb = new MaterialPropertyBlock();
        foreach (var r in platform.GetComponentsInChildren<MeshRenderer>())
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", tint);
            mpb.SetColor("_Color", tint);
            r.SetPropertyBlock(mpb);
        }
    }

    // Scatters emissive glow crystals across the world for a living, magical night. Most are pure
    // bloom (no real light = cheap); a handful near the ground get a soft point light for guidance.
    static void CreateGlowDecor()
    {
        Random.InitState(9911);
        GameObject root = new GameObject("GlowDecor");
        Color[] palette =
        {
            new Color(0.42f, 0.9f, 1f),   // cyan
            new Color(0.72f, 0.55f, 1f),  // violet
            new Color(1f, 0.82f, 0.45f),  // gold
            new Color(0.5f, 1f, 0.78f),   // firefly green
        };

        int clusters = 52, lit = 0;
        for (int c = 0; c < clusters; c++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float rad = Random.Range(13f, 215f);
            Vector3 baseP = new Vector3(Mathf.Sin(ang) * rad, 0f, Mathf.Cos(ang) * rad);
            if (TooCloseToWater(baseP, 3f)) continue;
            baseP.y = SampleTerrainHeight(baseP);

            var cluster = new GameObject("GlowCluster_" + c) { isStatic = true };
            cluster.transform.SetParent(root.transform);
            cluster.transform.position = baseP;

            Color col = palette[Random.Range(0, palette.Length)];
            Material glow = MakeGlowMaterial(col * Random.Range(2.3f, 3.6f));

            int shards = Random.Range(2, 6);
            float tallest = 0f;
            for (int s = 0; s < shards; s++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(shard.GetComponent<Collider>());
                shard.transform.SetParent(cluster.transform);
                float hgt = Random.Range(0.5f, 2.0f);
                tallest = Mathf.Max(tallest, hgt);
                shard.transform.localPosition = new Vector3(Random.Range(-0.55f, 0.55f), hgt * 0.5f, Random.Range(-0.55f, 0.55f));
                shard.transform.localScale = new Vector3(Random.Range(0.12f, 0.26f), hgt, Random.Range(0.12f, 0.26f));
                shard.transform.localRotation = Quaternion.Euler(Random.Range(-16f, 16f), Random.value * 360f, Random.Range(-16f, 16f));
                var smr = shard.GetComponent<MeshRenderer>();
                smr.sharedMaterial = glow;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (rad < 110f && lit < 12 && Random.value < 0.55f)
            {
                var lgo = new GameObject("GlowLight");
                lgo.transform.SetParent(cluster.transform);
                lgo.transform.localPosition = new Vector3(0f, tallest + 0.3f, 0f);
                var lt = lgo.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.range = Random.Range(7f, 11f);
                lt.intensity = Random.Range(1.8f, 2.8f);
                lt.color = col;
                lt.shadows = LightShadows.None;
                lt.cullingMask = ~(1 << 9);
                lit++;
            }

            cluster.AddComponent<DistanceCuller>().maxDistance = 190f;
        }
        Debug.Log($"[GlowDecor] placed {root.transform.childCount} clusters, {lit} lit");
    }

    // A small glowing crystal beacon beside an Endless platform: a warm light island so the long
    // extension keeps visual anchors. Deterministic (no Random) so the rest of the bake is stable.
    static void CreateEndlessBeacon(Vector3 topPos, Vector3 perpSide, int index)
    {
        var cluster = new GameObject("EndlessBeacon_" + index);
        cluster.transform.position = topPos + perpSide * 2.3f;

        Color col = ((index / 90) % 2 == 0) ? new Color(1f, 0.82f, 0.45f) : new Color(0.72f, 0.55f, 1f);
        Material glow = MakeGlowMaterial(col * 3f);

        float tallest = 0f;
        for (int s = 0; s < 3; s++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(shard.GetComponent<Collider>());
            shard.transform.SetParent(cluster.transform);
            float hgt = 0.8f + 0.5f * ((index + s * 37) % 3);
            tallest = Mathf.Max(tallest, hgt);
            shard.transform.localPosition = new Vector3((s - 1) * 0.4f, hgt * 0.5f, ((s * 53 + index) % 3 - 1) * 0.3f);
            shard.transform.localScale = new Vector3(0.18f, hgt, 0.18f);
            shard.transform.localRotation = Quaternion.Euler((s - 1) * 10f, s * 120f + index, (s - 1) * 8f);
            var smr = shard.GetComponent<MeshRenderer>();
            smr.sharedMaterial = glow;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var lgo = new GameObject("BeaconLight");
        lgo.transform.SetParent(cluster.transform);
        lgo.transform.localPosition = new Vector3(0f, tallest + 0.4f, 0f);
        var lt = lgo.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.range = 16f;
        lt.intensity = 2.6f;
        lt.color = col;
        lt.shadows = LightShadows.None;
        lt.cullingMask = ~(1 << 9);

        // Moths around the beacon light - life even high up in the Endless climb.
        CreateMoths(cluster.transform, new Vector3(0f, tallest + 0.4f, 0f), col);

        cluster.AddComponent<DistanceCuller>().maxDistance = 220f;
    }

    // The GUARDIAN boss: an oversized shooter wraith protecting a summit flag. Tanky (headshots
    // still count double), fires fast bolts, pays a big bounty and its own achievement (ms_boss).
    static GameObject CreateBoss(Vector3 pos, int index, float health = 220f)
    {
        GameObject go = CreateEnemy(pos, index, EnemyKind.Shooter);
        go.name = "Boss_Gipfelwaechter_" + index;
        go.transform.localScale = Vector3.one * 2.3f;
        var e = go.GetComponent<Enemy>();
        e.maxHealth = health;
        e.coinReward = 150;
        e.shootInterval = 1.6f;
        e.boltSpeed = 9.5f;
        e.shootRange = 32f;
        e.barWidth = 110f;
        e.barLift = 4.2f;
        return go;
    }

    // A small cyan glow orb marking a glide gap's flight line ("hold Space through here").
    static void CreateGlideHintOrb(Vector3 pos)
    {
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(orb.GetComponent<Collider>());
        orb.name = "GlideHint";
        orb.transform.position = pos;
        orb.transform.localScale = Vector3.one * 0.22f;
        var mr = orb.GetComponent<MeshRenderer>();
        mr.sharedMaterial = MakeGlowMaterial(new Color(0.45f, 0.95f, 1f) * 2.6f);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ZIPLINE SECRET: a treasure island floating FAR off the path - way beyond any jump or glide
    // range, reachable ONLY by riding the zipline out (and a second line back). On the island: a
    // golden treasure chest (SecretShard reward + achievement). Out and back, no dead ends.
    static void CreateZipSecret(Vector3 anchorTop, Vector3 dir, Vector3 perpSide, int index, float t,
        float heightOffset = -1.5f, float islandDist = 26f)
    {
        Vector3 island = anchorTop + perpSide * islandDist + dir * 2f + Vector3.up * heightOffset;

        GameObject platform = BuildPlatformShape(index % 4, index, 1.25f, t);
        platform.transform.position = island;
        ApplyZoneTint(platform, t);

        float topY = island.y + 1.1f * 1.25f;
        if (platform.GetComponent<Collider>() is BoxCollider pb)
            topY = island.y + pb.center.y + pb.size.y * 0.5f;
        Vector3 padTop = new Vector3(island.x, topY, island.z);

        // The MAST stands at the platform's EDGE toward the island (you deliberately walk to it -
        // no surprise auto-grab while crossing the platform). Chest at the island's front; the
        // RETURN handle hangs over its back edge so arriving never instantly re-grabs.
        Vector3 mastBase = anchorTop + perpSide * 0.85f;
        CreateTreasureChest(padTop + dir * 0.9f, index);
        // Both lines run MAST TO MAST (start post + end post), so the rope never ends mid-air.
        CreateZiplineRun(mastBase + Vector3.up * 2.4f, padTop + Vector3.up * 2.2f, 2.4f, 2.2f);
        CreateZiplineRun(padTop - dir * 0.9f + Vector3.up * 2.0f, anchorTop + Vector3.up * 2.6f, 2.0f, 2.6f);
    }

    // A golden treasure chest: wooden body, slightly open lid, gold trim - and a spinning glow orb
    // inside that carries the actual SecretShard reward. Collecting hides the orb (chest looks
    // looted) while the chest itself stays as a landmark.
    static void CreateTreasureChest(Vector3 padTop, int index)
    {
        var chest = new GameObject("TreasureChest_" + index);
        chest.transform.position = padTop + new Vector3(0f, 0.26f, 0f);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var wood = new Material(lit);
        wood.SetColor("_BaseColor", new Color(0.32f, 0.2f, 0.1f));
        Material goldGlow = MakeGlowMaterial(new Color(1f, 0.82f, 0.3f) * 3f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(chest.transform, false);
        body.transform.localScale = new Vector3(0.8f, 0.45f, 0.55f);
        body.GetComponent<MeshRenderer>().sharedMaterial = wood;

        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(lid.GetComponent<Collider>());
        lid.transform.SetParent(chest.transform, false);
        lid.transform.localPosition = new Vector3(0f, 0.3f, -0.12f);
        lid.transform.localRotation = Quaternion.Euler(-32f, 0f, 0f); // slightly open
        lid.transform.localScale = new Vector3(0.8f, 0.12f, 0.55f);
        lid.GetComponent<MeshRenderer>().sharedMaterial = wood;

        var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(band.GetComponent<Collider>());
        band.transform.SetParent(chest.transform, false);
        band.transform.localScale = new Vector3(0.84f, 0.09f, 0.59f);
        var bandMr = band.GetComponent<MeshRenderer>();
        bandMr.sharedMaterial = goldGlow;
        bandMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // The treasure glow: spinning orb with the reward trigger (SecretShard hides ITS renderers
        // on pickup, so only the glow vanishes - the chest stays, visibly looted).
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "TreasureGlow";
        Object.DestroyImmediate(orb.GetComponent<Collider>());
        orb.transform.position = chest.transform.position + new Vector3(0f, 0.42f, 0.05f);
        orb.transform.localScale = Vector3.one * 0.28f;
        var omr = orb.GetComponent<MeshRenderer>();
        omr.sharedMaterial = goldGlow;
        omr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var trig = orb.AddComponent<SphereCollider>();
        trig.isTrigger = true;
        trig.radius = 4.5f; // generous: stepping anywhere near the chest collects
        var shard = orb.AddComponent<SecretShard>();
        shard.coinReward = 100;
        orb.transform.SetParent(chest.transform, true);
    }

    // A zipline, built to be UNMISSABLE at night: a wooden mast with a glowing tip on the start
    // platform, a big green T-handle (the ride trigger), a bright ROPE (not black wire) and a
    // dotted line of small gold glow markers running the whole way to the far end.
    static void CreateZiplineRun(Vector3 from, Vector3 to, float postHeight = 0f, float endPostHeight = 0f)
    {
        var root = new GameObject("Zipline");
        root.transform.position = from;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material glow = MakeGlowMaterial(new Color(0.4f, 1f, 0.75f) * 3f);
        Material gold = MakeGlowMaterial(new Color(1f, 0.85f, 0.4f) * 2.2f);
        var rope = new Material(lit);
        rope.SetColor("_BaseColor", new Color(0.56f, 0.42f, 0.26f)); // bright hemp rope, reads at night

        // Mast from the platform up to the handle, so the start is a visible structure.
        if (postHeight > 0.1f)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, -postHeight * 0.5f, 0f);
            post.transform.localScale = new Vector3(0.14f, postHeight * 0.5f, 0.14f);
            var pmr = post.GetComponent<MeshRenderer>();
            var wood = new Material(lit);
            wood.SetColor("_BaseColor", new Color(0.3f, 0.2f, 0.12f));
            pmr.sharedMaterial = wood;
        }

        // Big glowing T-handle: crossbar + grip sphere.
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(bar.GetComponent<Collider>());
        bar.transform.SetParent(root.transform, false);
        bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        bar.transform.localScale = new Vector3(0.09f, 0.42f, 0.09f);
        var bmr = bar.GetComponent<MeshRenderer>();
        bmr.sharedMaterial = glow;
        bmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(handle.GetComponent<Collider>());
        handle.transform.SetParent(root.transform, false);
        handle.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        handle.transform.localScale = Vector3.one * 0.42f;
        var hmr = handle.GetComponent<MeshRenderer>();
        hmr.sharedMaterial = glow;
        hmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var trig = root.AddComponent<SphereCollider>();
        trig.isTrigger = true;
        trig.radius = 1.1f;
        var zip = root.AddComponent<Zipline>();
        zip.endPoint = to;
        zip.speed = 9f;

        // Bright rope cable...
        var cable = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(cable.GetComponent<Collider>());
        cable.transform.position = (from + to) * 0.5f;
        cable.transform.rotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
        cable.transform.localScale = new Vector3(0.09f, Vector3.Distance(from, to) * 0.5f, 0.09f);
        cable.transform.SetParent(root.transform, true);
        var cmr = cable.GetComponent<MeshRenderer>();
        cmr.sharedMaterial = rope;
        cmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ...plus a dotted line of gold glow markers all the way across - the eye-catcher.
        float dist = Vector3.Distance(from, to);
        int dots = Mathf.Clamp(Mathf.RoundToInt(dist / 3.5f), 3, 12);
        for (int d = 1; d < dots; d++)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(dot.GetComponent<Collider>());
            dot.transform.position = Vector3.Lerp(from, to, d / (float)dots);
            dot.transform.localScale = Vector3.one * 0.14f;
            dot.transform.SetParent(root.transform, true);
            var dmr = dot.GetComponent<MeshRenderer>();
            dmr.sharedMaterial = gold;
            dmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var end = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(end.GetComponent<Collider>());
        end.transform.position = to;
        end.transform.localScale = Vector3.one * 0.26f;
        end.transform.SetParent(root.transform, true);
        var endMr = end.GetComponent<MeshRenderer>();
        endMr.sharedMaterial = glow;
        endMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // End mast, so the rope runs visibly MAST TO MAST instead of ending mid-air.
        if (endPostHeight > 0.1f)
        {
            var endPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(endPost.GetComponent<Collider>());
            endPost.transform.position = to + Vector3.down * (endPostHeight * 0.5f);
            endPost.transform.localScale = new Vector3(0.14f, endPostHeight * 0.5f, 0.14f);
            endPost.transform.SetParent(root.transform, true);
            var epm = endPost.GetComponent<MeshRenderer>();
            var wood2 = new Material(lit);
            wood2.SetColor("_BaseColor", new Color(0.3f, 0.2f, 0.12f));
            epm.sharedMaterial = wood2;
        }
    }

    // Tiny warm moths orbiting a light source - fauna detail for lanterns and beacons.
    static void CreateMoths(Transform parent, Vector3 localPos, Color col)
    {
        var go = new GameObject("Moths");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 6f;
        main.startSpeed = 0.05f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new Color(col.r, col.g, col.b, 0.85f);
        main.maxParticles = 12;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var em = ps.emission;
        em.rateOverTime = 2f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.orbitalY = new ParticleSystem.MinMaxCurve(2.2f);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        Shader psSh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (psSh == null) psSh = Shader.Find("Sprites/Default");
        var mmat = new Material(psSh);
        if (mmat.HasProperty("_SrcBlend")) mmat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mmat.HasProperty("_DstBlend")) mmat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mmat.HasProperty("_ZWrite")) mmat.SetFloat("_ZWrite", 0f);
        mmat.mainTexture = GetSoftParticleTexture();
        mmat.renderQueue = 3100;
        rend.material = mmat;
    }

    // Glowing sky-jellyfish drifting through the Wolkenreich cloud layer (see JellyfishDrift).
    static void CreateJellyfish()
    {
        var root = new GameObject("SkyJellies");
        float baseY = actualTopHeight * 0.4f;
        for (int j = 0; j < 8; j++)
        {
            float ang = j * (Mathf.PI * 2f / 8f) + 0.4f;
            float rad = 26f + (j % 3) * 9f;
            var jelly = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(jelly.GetComponent<Collider>());
            jelly.name = "Jelly_" + j;
            jelly.transform.SetParent(root.transform);
            jelly.transform.position = new Vector3(Mathf.Sin(ang) * rad, baseY + (j % 4) * 18f, Mathf.Cos(ang) * rad);
            jelly.transform.localScale = new Vector3(1.3f, 0.9f, 1.3f);
            var mr = jelly.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MakeGlowMaterial(new Color(0.5f, 0.8f, 1f) * 1.4f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var drift = jelly.AddComponent<JellyfishDrift>();
            drift.riseSpeed = 0.4f + (j % 3) * 0.15f;
            jelly.AddComponent<DistanceCuller>().maxDistance = 220f;
        }
    }

    // A glowing air ring (8 orbs in a circle + trigger): pass through it for an upward launch.
    static void CreateBoostRing(Vector3 center)
    {
        var go = new GameObject("BoostRing");
        go.transform.position = center;
        Material glow = MakeGlowMaterial(new Color(0.4f, 1f, 0.75f) * 3f);
        for (int s = 0; s < 8; s++)
        {
            float a = s * Mathf.PI * 2f / 8f;
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            orb.transform.SetParent(go.transform, false);
            orb.transform.localPosition = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.95f;
            orb.transform.localScale = Vector3.one * 0.2f;
            var mr = orb.GetComponent<MeshRenderer>();
            mr.sharedMaterial = glow;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.05f;
        go.AddComponent<BoostRing>();
    }

    // A glowing fan disc + a tall updraft trigger + visible wind streaks (Wolkenreich set-piece).
    static void CreateWindColumn(Vector3 basePos)
    {
        var go = new GameObject("WindColumn");
        go.transform.position = basePos;

        var fan = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(fan.GetComponent<Collider>());
        fan.transform.SetParent(go.transform, false);
        fan.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        fan.transform.localScale = new Vector3(1.6f, 0.08f, 1.6f);
        var fmr = fan.GetComponent<MeshRenderer>();
        fmr.sharedMaterial = MakeGlowMaterial(new Color(0.55f, 0.85f, 1f) * 2.2f);
        fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var col = go.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius = 1.4f;
        col.height = 15f;
        col.center = new Vector3(0f, 7.5f, 0f);
        go.AddComponent<WindColumn>();

        // Upward streaks so the wind is visible (rotated so the cone points up).
        var psGO = new GameObject("WindParticles");
        psGO.transform.SetParent(go.transform, false);
        psGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = psGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.8f;
        main.startSpeed = 7f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new Color(0.7f, 0.9f, 1f, 0.5f);
        main.maxParticles = 60;
        var em = ps.emission;
        em.rateOverTime = 24f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 4f;
        shape.radius = 1.1f;
        var rend = psGO.GetComponent<ParticleSystemRenderer>();
        Shader psSh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (psSh == null) psSh = Shader.Find("Sprites/Default");
        var wmat = new Material(psSh);
        if (wmat.HasProperty("_SrcBlend")) wmat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (wmat.HasProperty("_DstBlend")) wmat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (wmat.HasProperty("_ZWrite")) wmat.SetFloat("_ZWrite", 0f);
        wmat.mainTexture = GetSoftParticleTexture();
        wmat.renderQueue = 3100;
        rend.material = wmat;
    }

    // Vista rest garden on a breather platform: warm lantern light, two crystals, a coin arc.
    static void CreateRestGarden(Vector3 topPos, int index)
    {
        var root = new GameObject("RestGarden_" + index);
        root.transform.position = topPos;

        Color col = new Color(1f, 0.8f, 0.45f);
        Material glow = MakeGlowMaterial(col * 3f);
        for (int s = 0; s < 2; s++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(shard.GetComponent<Collider>());
            shard.transform.SetParent(root.transform, false);
            shard.transform.localPosition = new Vector3(s == 0 ? -1.1f : 1.1f, 0.55f, -0.8f);
            shard.transform.localScale = new Vector3(0.16f, 1.0f + s * 0.3f, 0.16f);
            shard.transform.localRotation = Quaternion.Euler(6f, s * 140f, -5f);
            var mr = shard.GetComponent<MeshRenderer>();
            mr.sharedMaterial = glow;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var lgo = new GameObject("GardenLight");
        lgo.transform.SetParent(root.transform, false);
        lgo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        var lt = lgo.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.range = 12f;
        lt.intensity = 2.2f;
        lt.color = col;
        lt.shadows = LightShadows.None;
        lt.cullingMask = ~(1 << 9);

        // Moths circling the lantern - the light feels alive.
        CreateMoths(root.transform, new Vector3(0f, 1.4f, 0f), col);

        // A little coin arc as a thank-you for pausing at the vista.
        for (int c = 0; c < 3; c++)
            CreateGem(topPos + new Vector3((c - 1) * 0.8f, 0.6f + (c == 1 ? 0.25f : 0f), 0.7f), index * 10 + c, CoinType.Normal);
    }

    // Night fauna: dark bat silhouettes circling in the sky at a few heights (see BatFlock).
    static void CreateBats()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(unlit);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.04f, 0.03f, 0.08f));
        if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", 0); // visible from both sides

        // Flocks 0-2 are bats; the highest (3) is a trio of big, slow OWL silhouettes.
        int[] heights = { 45, 130, 260, 340 };
        for (int f = 0; f < heights.Length; f++)
        {
            bool owl = f == 3;
            var flock = new GameObject(owl ? "OwlFlock" : "BatFlock_" + f);
            flock.transform.position = new Vector3((f - 1) * 40f, heights[f], f % 2 == 0 ? 30f : -35f);
            int count = owl ? 3 : 5;
            for (int b = 0; b < count; b++)
            {
                var bat = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(bat.GetComponent<Collider>());
                bat.transform.SetParent(flock.transform, false);
                bat.transform.localScale = owl ? new Vector3(2.2f, 0.7f, 1f) : new Vector3(1.4f, 0.45f, 1f);
                var mr = bat.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var bf = flock.AddComponent<BatFlock>();
            bf.radius = 18f + f * 6f;
            bf.speed = owl ? 0.28f : 0.45f + f * 0.1f;
            flock.AddComponent<DistanceCuller>().maxDistance = 260f;
        }
    }

    // Occasional shooting-star streaks high over the tower - a tiny sky event every few seconds.
    static void CreateShootingStars()
    {
        var go = new GameObject("ShootingStars");
        go.transform.position = new Vector3(0f, actualTopHeight + 120f, 0f);
        go.transform.rotation = Quaternion.Euler(35f, 0f, 0f); // streaks fall diagonally
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.4f;
        main.startSpeed = 55f;
        main.startSize = 0.35f;
        main.startColor = new Color(1f, 1f, 0.95f, 0.9f);
        main.maxParticles = 6;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 0.12f; // one streak roughly every 8 seconds
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(420f, 1f, 260f);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.12f;
        Shader psSh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (psSh == null) psSh = Shader.Find("Sprites/Default");
        var smat = new Material(psSh);
        if (smat.HasProperty("_SrcBlend")) smat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (smat.HasProperty("_DstBlend")) smat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (smat.HasProperty("_ZWrite")) smat.SetFloat("_ZWrite", 0f);
        smat.mainTexture = GetSoftParticleTexture();
        smat.renderQueue = 3100;
        rend.material = smat;
    }

    // Soft violet counter-light opposite the moon: lifts pitch-black faces without flattening
    // the night (no shadows, low intensity) - the classic fill light.
    static void CreateFillLight()
    {
        var go = new GameObject("FillLight");
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 0.16f;
        l.color = new Color(0.5f, 0.45f, 0.7f);
        l.shadows = LightShadows.None;
        go.transform.rotation = Quaternion.Euler(38f, 160f, 0f);
    }

    // Additive, soft-edged transparent unlit material (for aurora curtains) - blooms into a glow.
    static Material MakeAdditiveGlowMaterial(Color hdrColor, Texture tex)
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Sprites/Default");
        var m = new Material(unlit);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // transparent
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdrColor);
        if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        return m;
    }

    // Aurora curtains: large soft glowing quads high in the sky, animated by AuroraShimmer - the
    // signature polar-light glow over the moonlit world.
    static void CreateAurora()
    {
        Random.InitState(7321);
        GameObject root = new GameObject("Aurora");
        var shimmer = root.AddComponent<AuroraShimmer>();
        var rends = new System.Collections.Generic.List<Renderer>();
        Color[] cols = { new Color(0.3f, 1f, 0.6f), new Color(0.35f, 0.8f, 1f), new Color(0.6f, 0.45f, 1f) };
        Texture2D soft = GetSoftParticleTexture();
        int n = 7;
        for (int i = 0; i < n; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "AuroraCurtain_" + i;
            q.transform.SetParent(root.transform);
            float ang = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
            float dist = Random.Range(180f, 340f);
            float y = actualTopHeight * Random.Range(0.5f, 1.05f) + 70f;
            q.transform.position = new Vector3(Mathf.Sin(ang) * dist, y, Mathf.Cos(ang) * dist);
            q.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(ang), 0f, -Mathf.Cos(ang)));
            q.transform.localScale = new Vector3(Random.Range(90f, 170f), Random.Range(110f, 220f), 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MakeAdditiveGlowMaterial(cols[i % cols.Length] * 2f, soft);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rends.Add(mr);
        }
        shimmer.curtains = rends.ToArray();
        Debug.Log($"[Aurora] {rends.Count} curtains");
    }

    // Sparse glowing will-o'-wisps drifting through the ENTIRE climb column, so soft motes of light
    // accompany the whole ascent (not just the meadow).
    static void CreateClimbWisps()
    {
        GameObject go = new GameObject("ClimbWisps");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 9f;
        main.startSpeed = 0.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = new Color(0.6f, 0.85f, 1f, 0.6f);
        main.maxParticles = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 14f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0f, actualTopHeight * 0.5f, 0f);
        shape.scale = new Vector3(120f, Mathf.Max(60f, actualTopHeight), 120f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.15f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.6f, 0.85f, 1f), 0f), new GradientColorKey(new Color(0.7f, 0.6f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader psSh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (psSh == null) psSh = Shader.Find("Sprites/Default");
        var mat = new Material(psSh);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.7f, 0.9f, 1f));
        mat.mainTexture = GetSoftParticleTexture();
        mat.renderQueue = 3100;
        renderer.material = mat;
    }

    // A hostile, glowing red spiked orb that patrols near a platform (PatrolHazard drives it) and
    // resets the player on contact - a telegraphed, avoidable moving obstacle for the higher zones.
    static void CreatePatrolHazard(Vector3 pos, Vector3 offset, int index)
    {
        Random.InitState(index * 91 + 7);
        var go = new GameObject("PatrolHazard_" + index);
        go.transform.position = pos;

        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(orb.GetComponent<Collider>());
        orb.transform.SetParent(go.transform, false);
        orb.transform.localScale = Vector3.one * 0.7f;
        Material glow = MakeGlowMaterial(new Color(1f, 0.22f, 0.26f) * 2.6f);
        var omr = orb.GetComponent<MeshRenderer>();
        omr.sharedMaterial = glow;
        omr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        for (int s = 0; s < 6; s++)
        {
            var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(spike.GetComponent<Collider>());
            spike.transform.SetParent(orb.transform, false);
            Vector3 dir = Random.onUnitSphere;
            spike.transform.localPosition = dir * 0.5f;
            spike.transform.up = dir;
            spike.transform.localScale = new Vector3(0.14f, 0.42f, 0.14f);
            var smr = spike.GetComponent<MeshRenderer>();
            smr.sharedMaterial = glow;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.55f;
        col.isTrigger = true;
        var ph = go.AddComponent<PatrolHazard>();
        ph.patrolOffset = offset;
        go.AddComponent<DistanceCuller>().maxDistance = 95f;
    }

    // A fightable shadow wraith (Enemy): dark cloaked body + glowing red eyes, floating. Threatens on
    // contact, killed in 3 left-click hits, drops coins.
    // The three wraith kinds share one silhouette but read differently at a glance: Wraith =
    // violet body + red eyes, Patrol = blue body + cyan eyes, Shooter = ember body + red eyes +
    // a glowing chest orb (the bolt source).
    static GameObject CreateEnemy(Vector3 pos, int index, EnemyKind kind = EnemyKind.Wraith)
    {
        var go = new GameObject("Enemy_" + kind + "_" + index);
        go.transform.position = pos;

        Color bodyCol = kind == EnemyKind.Patrol ? new Color(0.09f, 0.13f, 0.26f)
                      : kind == EnemyKind.Shooter ? new Color(0.24f, 0.08f, 0.10f)
                      : new Color(0.13f, 0.09f, 0.22f);
        Color eyeCol = kind == EnemyKind.Patrol ? new Color(0.3f, 0.9f, 1f) : new Color(1f, 0.16f, 0.2f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        body.transform.localScale = new Vector3(0.85f, 0.95f, 0.85f);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var bmat = new Material(lit);
        if (bmat.HasProperty("_BaseColor")) bmat.SetColor("_BaseColor", bodyCol);
        if (bmat.HasProperty("_Smoothness")) bmat.SetFloat("_Smoothness", 0.2f);
        body.GetComponent<MeshRenderer>().sharedMaterial = bmat;

        Material eyeMat = MakeGlowMaterial(eyeCol * 3f);
        for (int e = -1; e <= 1; e += 2)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(eye.GetComponent<Collider>());
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(e * 0.17f, 0.82f, 0.34f);
            eye.transform.localScale = Vector3.one * 0.13f;
            var emr = eye.GetComponent<MeshRenderer>();
            emr.sharedMaterial = eyeMat;
            emr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        if (kind == EnemyKind.Shooter)
        {
            // Glowing chest orb - the visible source of its bolts, telegraphing "this one shoots".
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            orb.transform.SetParent(go.transform, false);
            orb.transform.localPosition = new Vector3(0f, 0.5f, 0.36f);
            orb.transform.localScale = Vector3.one * 0.18f;
            var omr = orb.GetComponent<MeshRenderer>();
            omr.sharedMaterial = MakeGlowMaterial(new Color(1f, 0.35f, 0.15f) * 3.4f);
            omr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.7f;
        col.center = new Vector3(0f, 0.55f, 0f);
        col.isTrigger = true;
        var enemy = go.AddComponent<Enemy>();
        enemy.kind = kind;
        return go;
    }

    // An inactive arrow projectile template the player instantiates on left-click (see Arrow.cs).
    static GameObject BuildArrowTemplate()
    {
        var go = new GameObject("ArrowTemplate");
        var off = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material black = WeaponBlack();
        Material red = WeaponRedGlow();

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(shaft.GetComponent<Collider>());
        shaft.transform.SetParent(go.transform, false);
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // length points along +Z (forward)
        shaft.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);
        var sr = shaft.GetComponent<MeshRenderer>();
        sr.sharedMaterial = black;
        sr.shadowCastingMode = off;

        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere); // glowing red tip
        Object.DestroyImmediate(tip.GetComponent<Collider>());
        tip.transform.SetParent(go.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.42f);
        tip.transform.localScale = Vector3.one * 0.13f;
        var tr = tip.GetComponent<MeshRenderer>();
        tr.sharedMaterial = red;
        tr.shadowCastingMode = off;

        var fletch = GameObject.CreatePrimitive(PrimitiveType.Cube); // red fletching at the back
        Object.DestroyImmediate(fletch.GetComponent<Collider>());
        fletch.transform.SetParent(go.transform, false);
        fletch.transform.localPosition = new Vector3(0f, 0f, -0.36f);
        fletch.transform.localScale = new Vector3(0.16f, 0.02f, 0.13f);
        var fr = fletch.GetComponent<MeshRenderer>();
        fr.sharedMaterial = red;
        fr.shadowCastingMode = off;

        go.AddComponent<Arrow>();
        go.SetActive(false);
        return go;
    }

    // A simple held bow (two wooden limbs forming an arc + a glowing string). Shown only while drawn.
    static GameObject BuildBowVisual()
    {
        var bow = new GameObject("BowVisual");
        var off = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material black = WeaponBlack();
        Material red = WeaponRedGlow();

        // Two black limbs form a vertical arc; the grip is forward (+Z, toward the target) and the tips
        // curve back so the glowing red string sits on the archer's side - the way a bow is held.
        var tips = new Vector3[2];
        for (int s = -1; s <= 1; s += 2)
        {
            var limb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(limb.GetComponent<Collider>());
            limb.transform.SetParent(bow.transform, false);
            const float len = 0.36f;
            Vector3 dir = new Vector3(0f, s, -0.3f).normalized; // up/down, curving back toward the archer
            limb.transform.localPosition = dir * (len * 0.5f);
            limb.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            limb.transform.localScale = new Vector3(0.03f, len * 0.5f, 0.03f);
            var lr = limb.GetComponent<MeshRenderer>();
            lr.sharedMaterial = black;
            lr.shadowCastingMode = off;
            tips[(s + 1) / 2] = dir * len;
        }

        var str = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(str.GetComponent<Collider>());
        str.transform.SetParent(bow.transform, false);
        Vector3 a = tips[0], b = tips[1];
        Vector3 sdir = b - a;
        str.transform.localPosition = (a + b) * 0.5f;
        str.transform.localRotation = Quaternion.FromToRotation(Vector3.up, sdir.normalized);
        str.transform.localScale = new Vector3(0.012f, sdir.magnitude * 0.5f, 0.012f);
        var smr = str.GetComponent<MeshRenderer>();
        smr.sharedMaterial = red;
        smr.shadowCastingMode = off;

        bow.SetActive(false);
        return bow;
    }

    // =============================================================================================
    // PER-MODE PARKOUR. Four towers are baked into the scene - one per game mode - and ModeWorld
    // activates exactly one. Each tower has its own route direction, pacing, difficulty and object
    // mix, so the modes FEEL different instead of being the same track with different rules.
    // The surrounding environment (ground, rivers, lanterns, decor) is shared and untouched.
    // =============================================================================================

    // Per-zone parkour DNA: platform size, gap character, climb steepness and the movement-pattern
    // table - so each of the five worlds PLAYS differently, not just looks different.
    //  Wiese: wide pads, short gentle hops (learning). Vulkan: steep staccato climbs.
    //  Wolkenreich: long flat floaty jumps. Eiskristall: zigzag + momentum. Sternenkrone: precise+wild.
    static void ZoneDna(int zone, out float size, out float gapH, out float stepV, out int[] phases)
    {
        switch (zone)
        {
            case 0: size = 1.15f; gapH = 0.85f; stepV = 0.85f; phases = new[] { 0, 1, 0, 2 }; break;
            case 1: size = 0.95f; gapH = 0.95f; stepV = 1.18f; phases = new[] { 2, 4, 0, 2, 3 }; break;
            case 2: size = 0.90f; gapH = 1.18f; stepV = 0.72f; phases = new[] { 1, 0, 1, 5, 1 }; break;
            case 3: size = 0.85f; gapH = 1.00f; stepV = 1.00f; phases = new[] { 4, 1, 4, 2 }; break;
            default: size = 0.72f; gapH = 1.12f; stepV = 1.05f; phases = new[] { 3, 4, 2, 5, 0 }; break;
        }
    }

    // Everything that makes a mode's parkour ITS OWN: counts, pacing, danger, object cadence.
    class TowerConfig
    {
        public string Name;
        public int Count;
        public float SizeMul = 1f;          // global platform size bias
        public float GapMulH = 1f;          // horizontal gap bias (flow length)
        public float StepMulV = 1f;         // vertical step bias (flat = fast, steep = climby)
        public float SafetyMargin = 0.7f;   // fraction of the max fair jump (higher = steeper/harder)
        public bool AllowTimed = true;      // Zeitrennen bans WAITING platforms entirely
        public int CheckpointEvery = 12;    // farther apart = more punishing
        public int HazardMod = 6;           // smaller = more spinning hazards
        public int PrecisionMod = 9;        // smaller = more tiny precision pads
        public float PrecisionScale = 0.65f;
        public int HardJumpMod = 31;        // challenge-jump cadence
        public float HardJumpCap = 7.2f;
        public int GlideGapMod = 47;        // glide-only gap cadence
        public int RingMod = 90;            // boost rings
        public int WindMod = 45;            // wind columns (cloud zone)
        public int SecretMod = 70;          // treasure-zipline cadence
        public int GauntletMod = 137;       // moving-platform set-piece cadence
        public int BouncePadMod = 4;        // default-flavor pad cadence (smaller = more)
        public float EnemyGapNear = 34f, EnemyGapFar = 16f;
        public float BossHealth = 220f;
        public bool ExtraCrumble = false;   // Hardcore sprinkles crumbling into static stretches
        public bool ClimbDecor = false;     // decorative props along the route (Klassisch showcase)
        public bool EndlessStyle = false;   // zone cycling + centre steering + light beacons
        public int PhaseOffset = 0;         // rotates the pattern tables so routes differ
        public float AngleSeed = 0f;        // start direction - each tower fans out differently

        // Klassisch: balanced, classic platforming, a clean learning curve.
        public static TowerConfig Klassisch() => new TowerConfig
        {
            Name = "Klassisch", Count = 660, ClimbDecor = true, SecretMod = 70, AngleSeed = 0f,
        };

        // Zeitrennen: flow. Flatter steps, longer lines, NO waiting platforms, more pads/rings -
        // a track you can run without ever stopping.
        public static TowerConfig Zeitrennen() => new TowerConfig
        {
            Name = "Zeitrennen", Count = 620, SizeMul = 1.05f, GapMulH = 1.06f, StepMulV = 0.8f,
            AllowTimed = false, CheckpointEvery = 14, HazardMod = 8, HardJumpMod = 26,
            GlideGapMod = 40, RingMod = 60, WindMod = 40, SecretMod = 90, BouncePadMod = 3,
            EnemyGapNear = 44f, EnemyGapFar = 30f, BossHealth = 200f, PhaseOffset = 1, AngleSeed = 1.6f,
        };

        // Hardcore: precision. Smaller pads, steeper/wider jumps, more crumble and hazards,
        // checkpoints farther apart - demanding but always inside the fair-jump envelope.
        public static TowerConfig Hardcore() => new TowerConfig
        {
            Name = "Hardcore", Count = 640, SizeMul = 0.82f, GapMulH = 1.05f, StepMulV = 1.05f,
            SafetyMargin = 0.82f, CheckpointEvery = 16, HazardMod = 4, PrecisionMod = 6,
            PrecisionScale = 0.55f, HardJumpMod = 22, HardJumpCap = 7.5f, GlideGapMod = 42,
            RingMod = 120, WindMod = 50, SecretMod = 80, GauntletMod = 120,
            EnemyGapNear = 26f, EnemyGapFar = 12f, BossHealth = 260f, ExtraCrumble = true,
            PhaseOffset = 2, AngleSeed = 3.2f,
        };

        // Endlos: endurance. Very long, zones CYCLE (all five worlds repeat with rising difficulty)
        // so it never settles into repetition; beacons and steering keep it near the tower axis.
        public static TowerConfig Endlos() => new TowerConfig
        {
            // Count/steepness tuned so the summit clears 1000 m - the "Höhenrausch" achievement
            // (1000 m, Endlos-Modus) must stay reachable.
            Name = "Endlos", Count = 2750, StepMulV = 1.12f, CheckpointEvery = 12, SecretMod = 80,
            EnemyGapNear = 32f, EnemyGapFar = 24f, BossHealth = 320f, EndlessStyle = true,
            PhaseOffset = 3, AngleSeed = 4.7f,
        };
    }

    // Generates one complete mode tower into its own chunked toggle root. Returns the summit.
    static Vector3 GenerateTower(TowerConfig cfg, out GameObject root)
    {
        Vector3 prevPos = new Vector3(0f, 0.5f, 0f);
        Vector3 lastPos = prevPos;
        float angle = cfg.AngleSeed;
        bool[] gatesPlaced = new bool[GateThresholds.Length];
        int sinceEnemy = 0;
        bool zipSecretDue = false;

        Scene scene = SceneManager.GetActiveScene();
        var before = new HashSet<GameObject>(scene.GetRootGameObjects());

        const int cycleLen = 400; // Endless: one full 5-zone tour per 400 platforms

        for (int i = 0; i < cfg.Count; i++)
        {
            // tVisual = which WORLD this stretch belongs to (cycles in Endless);
            // tDiff = how HARD it is (always ramps from bottom to top).
            float tDiff = Mathf.Clamp01(i / (float)(cfg.Count - 1));
            if (cfg.EndlessStyle)
                tDiff = Mathf.Lerp(0.55f, 1f, tDiff); // endless starts mid-difficulty, keeps rising
            float tVisual = cfg.EndlessStyle ? (i % cycleLen) / (float)cycleLen : tDiff;
            int zone = Mathf.Min(4, (int)(tVisual * 5f));
            ZoneDna(zone, out float zSize, out float zGapH, out float zStepV, out int[] zPhases);

            bool earlySection = i < 4;
            int block = i / 3;
            bool isCheckpoint = i > 0 && i % cfg.CheckpointEvery == 0;
            bool isRestStop = !earlySection && !isCheckpoint && i % cfg.CheckpointEvery == cfg.CheckpointEvery - 1;

            int flavor = earlySection ? 0 : ZoneFlavor(block, tVisual);
            if (!cfg.AllowTimed && flavor == 5)
                flavor = 6; // Zeitrennen: waiting platforms become bounce pads - flow never stops
            if (cfg.ExtraCrumble && flavor == 0 && i % 5 == 1 && !earlySection)
                flavor = 1; // Hardcore: static stretches keep crumbling underfoot
            if (isCheckpoint || isRestStop)
                flavor = -1;

            bool inGauntlet = i % cfg.GauntletMod >= 70 && i % cfg.GauntletMod <= 74;
            if (!earlySection && !isCheckpoint && !isRestStop && inGauntlet)
                flavor = 2;

            bool glideGap = !earlySection && !isCheckpoint && !isRestStop && !inGauntlet
                && tDiff > 0.25f && i % cfg.GlideGapMod == 20;
            if (glideGap)
                flavor = 0;

            bool isMovingType = flavor == 2;
            bool isSwingingType = flavor == 3;
            bool isFloatingType = flavor == 4;
            float dynamicPenalty = (isMovingType || isSwingingType) ? 0.7f : (isFloatingType ? 0.85f : 1f);

            float stepHeight = earlySection
                ? Mathf.Lerp(1.0f, MinVerticalGap + 0.3f, i / 3f)
                : Mathf.Lerp(MinVerticalGap + 0.3f, MaxVerticalGap, tDiff) * dynamicPenalty * zStepV * cfg.StepMulV;

            float horizontalGap = earlySection
                ? Mathf.Lerp(1.8f, MinHorizontalGap + 0.6f, i / 3f)
                : Mathf.Min(7.4f,
                    Mathf.Lerp(MinHorizontalGap + 0.6f, MaxHorizontalGap, tDiff) * dynamicPenalty * zGapH * cfg.GapMulH);

            // Never generate a vertical gap a single jump can't reach at that horizontal distance.
            float safeMaxHeight = Mathf.Max(0.5f, MaxSingleJumpHeightAt(horizontalGap) * cfg.SafetyMargin);
            stepHeight = Mathf.Min(stepHeight, safeMaxHeight);

            if (earlySection)
            {
                angle += 0.4f;
            }
            else if (prevPos.y < BoundaryWallHeight + 8f)
            {
                // Tight upward spiral until the route clears the boundary wall.
                angle += 0.62f;
                stepHeight = Mathf.Min(stepHeight, safeMaxHeight);
            }
            else
            {
                // The zone's OWN pattern table (rotated per mode), instead of one global cycle.
                const int phaseLength = 18;
                int phase = zPhases[((i / phaseLength) + cfg.PhaseOffset) % zPhases.Length];
                float angleIncrement;
                float stepMul;
                switch (phase)
                {
                    case 0: angleIncrement = 0.4f + tDiff * 0.15f; stepMul = 1f; break;   // gentle spiral
                    case 1: angleIncrement = 0f; stepMul = 0.35f; break;                  // flat straight run
                    case 2: angleIncrement = 0.1f; stepMul = 1f; break;                   // steep climb
                    case 3: angleIncrement = 0.9f + tDiff * 0.2f; stepMul = 0.9f; break;  // loop around
                    case 4: angleIncrement = (i % 2 == 0 ? 1.15f : -1.15f); stepMul = 0.75f; break; // zigzag
                    default: angleIncrement = 0.72f; stepMul = 1f; break;                 // tight coil
                }
                angle += angleIncrement;
                stepHeight = Mathf.Min(stepHeight * stepMul, safeMaxHeight);
            }

            // Keep long towers near the axis instead of wandering out over the void.
            if (cfg.EndlessStyle)
            {
                float r = new Vector2(prevPos.x, prevPos.z).magnitude;
                const float comfortRadius = 70f;
                if (r > comfortRadius)
                {
                    float toCenterDeg = Mathf.Atan2(-prevPos.x, -prevPos.z) * Mathf.Rad2Deg;
                    float maxTurn = Mathf.Min(26f, (r - comfortRadius) * 0.9f);
                    angle = Mathf.MoveTowardsAngle(angle * Mathf.Rad2Deg, toCenterDeg, maxTurn) * Mathf.Deg2Rad;
                }
            }

            if (glideGap)
            {
                // Wide + slightly down: only the glide clears it (marked by cyan hint orbs below).
                horizontalGap = 10.5f;
                stepHeight = -2.5f;
            }
            else if (!earlySection && !isCheckpoint && !isRestStop && !inGauntlet && i % cfg.HardJumpMod == 11)
            {
                // Challenge jump: wider than the fair max, made fair again by a slight drop.
                horizontalGap = Mathf.Min(horizontalGap * 1.3f, cfg.HardJumpCap);
                stepHeight = Mathf.Min(stepHeight, -0.6f);
            }

            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 pos = prevPos + dir * horizontalGap;
            pos.y = prevPos.y + stepHeight;
            Vector3 perpSide = new Vector3(dir.z, 0f, -dir.x) * (i % 2 == 0 ? 1f : -1f);

            int shapeType = i % 4;
            float sizeMultiplier = Mathf.Clamp(Mathf.Lerp(1f, 0.62f, tDiff) * zSize * cfg.SizeMul, 0.5f, 1.7f);

            bool isPrecision = flavor == 0 && !earlySection && !isRestStop && i % cfg.PrecisionMod == 5 && !glideGap;
            if (isRestStop)
                sizeMultiplier *= 1.6f;
            else if (glideGap)
                sizeMultiplier *= 1.4f; // generous landing pad after the glide
            else if (isPrecision)
                sizeMultiplier *= cfg.PrecisionScale;
            if (isMovingType || isSwingingType || isFloatingType)
                sizeMultiplier *= 0.7f;

            GameObject platform = BuildPlatformShape(shapeType, i, sizeMultiplier, tVisual);
            platform.transform.position = pos;
            ApplyZoneTint(platform, tVisual); // Endless cycles ALL five world colours as you climb

            // Eiskristall signature: slippery static platforms (zone-based, cycles in Endless).
            if (!earlySection && !isCheckpoint && !isRestStop && flavor == 0 && zone == 3 && i % 3 == 0)
                platform.AddComponent<IceSurface>();

            float platformTopY = pos.y + 1.1f * sizeMultiplier;
            if (platform.GetComponent<Collider>() is BoxCollider pBox)
                platformTopY = pos.y + pBox.center.y + pBox.size.y * 0.5f;
            Vector3 topPos = new Vector3(pos.x, platformTopY, pos.z);

            if (isCheckpoint)
            {
                CreateCheckpointMarker(topPos, i);
            }
            else if (isRestStop)
            {
                if ((i / cfg.CheckpointEvery) % 3 == 2)
                    CreateRestGarden(topPos, i);
            }
            else
            {
                switch (flavor)
                {
                    case 0:
                        if (!earlySection && i % cfg.HazardMod == 2 && !glideGap)
                            CreateHazard(topPos + perpSide * (0.55f * sizeMultiplier) + new Vector3(0f, 0.5f, 0f), i);
                        else if (!earlySection && i % 15 == 7 && !glideGap)
                            platform.AddComponent<RotatingPlatform>();
                        break;
                    case 1:
                        if (!earlySection)
                            platform.AddComponent<CrumblingPlatform>();
                        break;
                    case 2:
                        if (!earlySection)
                        {
                            var mover = platform.AddComponent<MovingPlatform>();
                            mover.moveOffset = (i % 2 == 0) ? new Vector3(2.2f, 0f, 0f) : new Vector3(0f, 0f, 2.2f);
                            mover.speed = 0.8f + tDiff * 0.5f;
                        }
                        break;
                    case 3:
                        if (!earlySection)
                        {
                            var swing = platform.AddComponent<SwingingPlatform>();
                            swing.armLength = 2f + (i % 3) * 0.5f;
                            swing.swingAngleDeg = 30f + tDiff * 15f;
                            swing.speed = 0.9f + tDiff * 0.3f;
                        }
                        break;
                    case 4:
                        if (!earlySection)
                        {
                            var floater = platform.AddComponent<FloatingPlatform>();
                            floater.amplitude = 0.35f + (i % 3) * 0.1f;
                            floater.speed = 1f + tDiff * 0.4f;
                        }
                        break;
                    case 5:
                        if (!earlySection)
                        {
                            var timed = platform.AddComponent<TimedPlatform>();
                            timed.solidDuration = 3.2f;
                            timed.goneDuration = 1.2f;
                            timed.phaseOffset = i * 0.6f;
                        }
                        break;
                    default:
                        if (!earlySection && i % cfg.BouncePadMod == 0)
                            CreateBouncePad(topPos + new Vector3(0f, 0.05f, 0f), i);
                        if (!earlySection && i % 7 == 4)
                            CreateHazard(topPos + perpSide * (0.55f * sizeMultiplier) + new Vector3(0f, 0.5f, 0f), i);
                        break;
                }
            }

            if (!isCheckpoint)
            {
                CoinType coinType = CoinType.Normal;
                bool isRiskyFlavor = flavor == 0 || flavor == 2 || flavor == 4 || flavor == 5 || flavor == 6;
                if (!earlySection && isRiskyFlavor && i % 3 == 0)
                    coinType = CoinType.Rare;

                Vector3 coinPos = topPos + new Vector3(0f, 0.55f, 0f);
                if (!earlySection && i % 15 == 7)
                    coinType = CoinType.Epic;
                if (!earlySection && !isRestStop && i % 40 == 20)
                {
                    coinType = CoinType.Legendary;
                    coinPos += perpSide * 1.2f;
                }

                if (earlySection || i % 6 == 0)
                    CreateGem(coinPos, i, coinType);

                if (!earlySection && !isRestStop && !cfg.EndlessStyle && i % 8 == 4)
                    CreateCrate(topPos + perpSide * 0.9f + new Vector3(0f, 0.35f, 0f), i, 5);

                if (!earlySection && !isRestStop && !cfg.EndlessStyle && i % 17 == 8)
                    CreatePopupSpike(topPos + perpSide * 0.6f + new Vector3(0f, 0.02f, 0f), i);
                if (!earlySection && !isRestStop && !cfg.EndlessStyle && i % 23 == 11)
                    CreateTntCrate(topPos - perpSide * 0.8f + new Vector3(0f, 0.35f, 0f), i);

                // Treasure zipline: due on a cadence, built on the next STABLE platform.
                if (!earlySection && tDiff > 0.05f && i % cfg.SecretMod == 20)
                    zipSecretDue = true;
                if (zipSecretDue && !isRestStop && flavor == 0 && !glideGap)
                {
                    zipSecretDue = false;
                    CreateZipSecret(topPos, dir, perpSide, i, tVisual);
                }

                if (cfg.EndlessStyle && i % 90 == 45)
                    CreateEndlessBeacon(topPos, perpSide, i);

                if (!earlySection && !isRestStop && tDiff > 0.22f && i % cfg.RingMod == 60 % cfg.RingMod)
                    CreateBoostRing(topPos + Vector3.up * 3.2f + perpSide * 1.4f);

                // Wind columns live in the CLOUD zone (which cycles back around in Endless).
                if (!earlySection && !isRestStop && zone == 2 && i % cfg.WindMod == 22 % cfg.WindMod)
                    CreateWindColumn(topPos + perpSide * 2.6f);

                sinceEnemy++;
                int enemyGap = Mathf.RoundToInt(Mathf.Lerp(cfg.EnemyGapNear, cfg.EnemyGapFar, tDiff));
                if (!earlySection && !isRestStop && sinceEnemy >= enemyGap)
                {
                    EnemyKind ekind = (i % 3 == 0) ? EnemyKind.Wraith : (i % 3 == 1 ? EnemyKind.Patrol : EnemyKind.Shooter);
                    CreateEnemy(topPos + perpSide * 1.9f + Vector3.up * 1.1f, i, ekind);
                    sinceEnemy = 0;
                }
            }

            if (glideGap)
            {
                CreateGlideHintOrb(Vector3.Lerp(prevPos, pos, 0.35f) + Vector3.up * 2.4f);
                CreateGlideHintOrb(Vector3.Lerp(prevPos, pos, 0.68f) + Vector3.up * 1.6f);
            }

            if (cfg.ClimbDecor && i % 3 == 1 && !isCheckpoint)
                CreateClimbDecor(pos, i, tVisual);

            for (int g = 0; g < GateThresholds.Length; g++)
            {
                if (!gatesPlaced[g] && tVisual >= GateThresholds[g])
                {
                    CreateWorldGate(pos, GameManager.GetWorldName(tVisual));
                    gatesPlaced[g] = true;
                    break;
                }
            }

            float rad = new Vector2(pos.x, pos.z).magnitude;
            towerMaxRadius = Mathf.Max(towerMaxRadius, rad);
            if (pos.y < BoundaryWallHeight)
                towerLowRadius = Mathf.Max(towerLowRadius, rad);

            prevPos = pos;
            lastPos = pos;
        }

        // Summit: goal flag + guardian boss belong to THIS tower and toggle with it.
        GameObject flag = CreateGoalFlag(lastPos);
        flag.name = "GoalFlag_" + cfg.Name;
        CreateBoss(lastPos + new Vector3(-2.8f, 1.6f, 0f), 9100 + cfg.PhaseOffset, cfg.BossHealth);

        // Reparent everything this tower spawned into activation chunks under one toggle root.
        root = new GameObject("Tower" + cfg.Name);
        before.Add(root);
        const int chunkSize = 250;
        GameObject curChunk = null;
        int inChunk = 0, chunkIndex = 0;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (before.Contains(go))
                continue;
            if (curChunk == null || inChunk >= chunkSize)
            {
                curChunk = new GameObject(cfg.Name + "Chunk_" + (chunkIndex++));
                curChunk.transform.SetParent(root.transform, false);
                inChunk = 0;
            }
            go.transform.SetParent(curChunk.transform, true);
            inChunk++;
        }

        // Root stays active; children start OFF and are switched by ModeWorld.
        foreach (Transform child in root.transform)
            child.gameObject.SetActive(false);

        Debug.Log($"SceneBuilder: Tower {cfg.Name}: {cfg.Count} platforms, summit {lastPos.y:0} m");
        return lastPos;
    }

    static float towerMaxRadius;
    static float towerLowRadius;
    const float BoundaryWallHeight = 30f;

    // An invisible fence around the SPAWN/lower climb so the player can't wander off across the jungle
    // floor. It's only BoundaryWallHeight tall and sits just outside the tower's reach BELOW that
    // height - so the climb rises up and over it and is never blocked. Colliders only (no renderer).
    static void CreateBoundaryWall(float topHeight)
    {
        float radius = Mathf.Max(24f, towerLowRadius + 9f); // outside the low tower + jump room
        var root = new GameObject("BoundaryWall");
        const int segments = 48;
        float segLen = (2f * Mathf.PI * radius / segments) * 1.25f; // overlap so there are no gaps
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var seg = new GameObject("Wall_" + i);
            seg.transform.SetParent(root.transform);
            seg.transform.position = new Vector3(Mathf.Sin(a) * radius, BoundaryWallHeight * 0.5f - 4f, Mathf.Cos(a) * radius);
            seg.transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            var box = seg.AddComponent<BoxCollider>();
            box.size = new Vector3(segLen, BoundaryWallHeight, 1f); // wide tangential, tall, thin radial
        }
    }

    static void CreateWorldGate(Vector3 pos, string worldName)
    {
        GameObject gate = InstantiateKenney("door-rotate-large", pos + new Vector3(0f, 1.5f, 0f));
        gate.name = "WorldGate_" + worldName;
        gate.transform.localScale = Vector3.one * 2.5f;
        foreach (var col in gate.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(col);
    }

    static readonly string[] GrassZoneDecor = { "rocks", "mushrooms", "plant", "fence-broken" };
    static readonly string[] VolcanoZoneDecor = { "rocks", "spike-block", "poles" };
    static readonly string[] CloudZoneDecor = { "flag", "poles" };
    static readonly string[] IceZoneDecor = { "rocks", "poles", "sign" };
    static readonly string[] FinalZoneDecor = { "flag", "poles", "sign" };

    static string[] GetZoneDecorPool(float t)
    {
        if (t < 0.2f) return GrassZoneDecor;
        if (t < 0.4f) return VolcanoZoneDecor;
        if (t < 0.6f) return CloudZoneDecor;
        if (t < 0.8f) return IceZoneDecor;
        return FinalZoneDecor;
    }

    static void CreateClimbDecor(Vector3 platformPos, int index, float t)
    {
        string[] pool = GetZoneDecorPool(t);
        string modelName = pool[index % pool.Length];

        float side = (index % 2 == 0) ? 1f : -1f;
        Vector3 offset = new Vector3(side * 1.4f, 0.4f, side * 0.6f);

        GameObject decor = InstantiateKenney(modelName, platformPos + offset);
        decor.name = "ClimbDecor_" + index;
        decor.transform.rotation = Quaternion.Euler(0f, index * 41f, 0f);
        decor.transform.localScale = Vector3.one * 0.8f;
        ApplyDecorFlags(decor, modelName);

        foreach (var col in decor.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(col);
    }

    static void CreateCheckpointMarker(Vector3 surfacePos, int index)
    {
        GameObject marker = InstantiateKenney("sign", surfacePos);
        marker.name = "Checkpoint_" + index;
        marker.transform.localScale = Vector3.one * 1.3f;

        // Rest the sign's base exactly on the platform surface. The Kenney "sign" pivot isn't reliably
        // at its base and checkpoint platforms are oversized, so the old fixed +0.7 offset from the
        // platform CENTRE let the sign sink into the (taller) platform. Grounding by render bounds is
        // pivot-independent, so the post always stands cleanly on top.
        GroundOnSurface(marker.transform, surfacePos.y);

        BoxCollider trigger = marker.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2.4f, 3f, 2.4f);
        // Cover the volume just ABOVE the surface where the player stands (trigger centre is local, so
        // convert the desired world height ~1.2m above the surface into the grounded transform's space).
        trigger.center = new Vector3(0f, surfacePos.y + 1.2f - marker.transform.position.y, 0f);
        marker.AddComponent<Checkpoint>();
    }

    // Shifts a placed object vertically so the BOTTOM of its combined renderer bounds rests on
    // surfaceY (with a hair of clearance to avoid z-fighting). Static mesh bounds are reliable in
    // edit/batch mode, so this is a robust, pivot-independent way to stand props on a surface.
    static void GroundOnSurface(Transform t, float surfaceY)
    {
        Bounds b = default;
        bool has = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has)
            return;
        t.position += new Vector3(0f, surfaceY - b.min.y + 0.02f, 0f);
    }

    static GameObject CreateGoalFlag(Vector3 topPlatformPos)
    {
        GameObject flag = InstantiateKenney("flag", topPlatformPos + new Vector3(0f, 0.9f, 0f));
        flag.name = "GoalFlag";
        flag.transform.localScale = Vector3.one * 1.5f;
        flag.AddComponent<WindSway>();

        BoxCollider trigger = flag.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2.5f, 3f, 2.5f);
        flag.AddComponent<GoalTrigger>();
        return flag;
    }

    static void CreateWinScreen()
    {
        var go = new GameObject("WinScreen");
        go.AddComponent<WinScreen>();
    }

    static void CreateModeWorld(GameObject k, GameObject z, GameObject e, GameObject h,
        float topK, float topZ, float topE, float topH)
    {
        var go = new GameObject("ModeWorld");
        var mw = go.AddComponent<ModeWorld>();
        mw.klassischRoot = k;
        mw.zeitrennenRoot = z;
        mw.endlosRoot = e;
        mw.hardcoreRoot = h;
        mw.klassischTop = topK;
        mw.zeitrennenTop = topZ;
        mw.endlosTop = topE;
        mw.hardcoreTop = topH;
    }

    static void CreateMilestoneTracker()
    {
        var go = new GameObject("MilestoneTracker");
        go.AddComponent<MilestoneTracker>();
    }

    // Isolated rotating-character stage (on layer 9) that a dedicated camera renders into a
    // RenderTexture for the shop's live cosmetic preview. Sits far from the play area; the world
    // cameras and sun exclude layer 9, and it has its own light for consistent framing.
    static void CreateCosmeticPreview()
    {
        const int PreviewLayer = 9;
        Vector3 origin = new Vector3(2000f, 2000f, 2000f);

        GameObject rootGO = new GameObject("CosmeticPreview");
        rootGO.transform.position = origin;
        var preview = rootGO.AddComponent<CosmeticPreview>();

        SkinnedMeshRenderer previewRenderer = BuildProtagonistCharacter(rootGO.transform, out Animator previewAnimator, out Texture2D[] previewTextures);

        // Trail offset from the spin axis so pure rotation traces a visible ring.
        GameObject trailGO = new GameObject("PreviewTrail");
        trailGO.transform.SetParent(rootGO.transform);
        trailGO.transform.localPosition = new Vector3(0.3f, 0.6f, 0f);
        TrailRenderer trail = trailGO.AddComponent<TrailRenderer>();
        trail.time = 0.5f;
        trail.startWidth = 0.35f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.04f;
        trail.autodestruct = false;
        trail.emitting = false;
        trail.enabled = false;
        Shader trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (trailShader == null)
            trailShader = Shader.Find("Sprites/Default");
        trail.material = new Material(trailShader);

        GameObject fxGO = new GameObject("PreviewParticles");
        fxGO.transform.SetParent(rootGO.transform);
        fxGO.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var fx = fxGO.AddComponent<ParticleSystem>();
        var fxMain = fx.main;
        fxMain.loop = true;
        fxMain.playOnAwake = false;
        fxMain.startLifetime = 1f;
        fxMain.startSpeed = 1f;
        fxMain.startSize = 0.2f;
        fxMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        fxMain.maxParticles = 200;
        var fxEmission = fx.emission;
        fxEmission.rateOverTime = 0f;
        var fxShape = fx.shape;
        fxShape.shapeType = ParticleSystemShapeType.Sphere;
        fxShape.radius = 0.35f;
        var fxRenderer = fxGO.GetComponent<ParticleSystemRenderer>();
        Shader fxShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (fxShader == null)
            fxShader = Shader.Find("Sprites/Default");
        var fxMat = new Material(fxShader);
        if (fxMat.HasProperty("_BaseColor"))
            fxMat.SetColor("_BaseColor", Color.white);
        fxRenderer.material = fxMat;
        fx.Stop();

        // Preview glow layer (matches the in-game second effect layer).
        GameObject fx2GO = new GameObject("PreviewParticlesGlow");
        fx2GO.transform.SetParent(rootGO.transform);
        fx2GO.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var fx2 = fx2GO.AddComponent<ParticleSystem>();
        var fx2Main = fx2.main;
        fx2Main.loop = true;
        fx2Main.playOnAwake = false;
        fx2Main.startLifetime = 1f;
        fx2Main.startSpeed = 0.6f;
        fx2Main.startSize = 0.4f;
        fx2Main.simulationSpace = ParticleSystemSimulationSpace.Local;
        fx2Main.maxParticles = 160;
        var fx2Emission = fx2.emission;
        fx2Emission.rateOverTime = 0f;
        var fx2Shape = fx2.shape;
        fx2Shape.shapeType = ParticleSystemShapeType.Sphere;
        fx2Shape.radius = 0.3f;
        var fx2Renderer = fx2GO.GetComponent<ParticleSystemRenderer>();
        var fx2Mat = new Material(fxShader);
        if (fx2Mat.HasProperty("_BaseColor"))
            fx2Mat.SetColor("_BaseColor", Color.white);
        fx2Renderer.material = fx2Mat;
        fx2.Stop();

        // Preview aura (so themed skins show their aura in the shop too).
        GameObject pAuraGO = new GameObject("PreviewAura");
        pAuraGO.transform.SetParent(rootGO.transform);
        pAuraGO.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var pAura = pAuraGO.AddComponent<ParticleSystem>();
        var pAuraMain = pAura.main;
        pAuraMain.loop = true;
        pAuraMain.playOnAwake = false;
        pAuraMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        pAuraMain.maxParticles = 300;
        var pAuraEmission = pAura.emission;
        pAuraEmission.rateOverTime = 0f;
        var pAuraRenderer = pAuraGO.GetComponent<ParticleSystemRenderer>();
        pAuraRenderer.material = new Material(fxShader);
        pAura.Stop();

        SetLayerRecursive(rootGO, PreviewLayer);

        // Dedicated camera renders ONLY the preview layer into the RenderTexture (set at runtime).
        GameObject camGO = new GameObject("CosmeticPreviewCamera");
        camGO.transform.position = origin + new Vector3(0f, 1.0f, 3.4f);
        camGO.transform.LookAt(origin + new Vector3(0f, 0.95f, 0f));
        Camera cam = camGO.AddComponent<Camera>();
        cam.cullingMask = 1 << PreviewLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.11f, 0.15f, 1f);
        cam.fieldOfView = 34f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;
        cam.enabled = false;

        // Dedicated light (only affects the preview layer) for stable, zone-independent lighting.
        GameObject lightGO = new GameObject("CosmeticPreviewLight");
        lightGO.transform.position = origin;
        lightGO.transform.rotation = Quaternion.Euler(28f, 200f, 0f);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.cullingMask = 1 << PreviewLayer;

        // Preview head anchor for accessories (must be on the preview layer so its accessory renders
        // into the shop's preview camera).
        GameObject previewHeadAnchor = new GameObject("PreviewHeadAnchor") { layer = PreviewLayer };
        previewHeadAnchor.transform.SetParent(rootGO.transform);
        previewHeadAnchor.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        previewHeadAnchor.transform.localRotation = Quaternion.identity;
        var pHeadMount = previewHeadAnchor.AddComponent<HeadAccessoryMount>();
        pHeadMount.headBone = FindHeadBone(previewAnimator, rootGO.transform);
        pHeadMount.facing = rootGO.transform;
        pHeadMount.bodyRenderer = previewRenderer;

        preview.accessoryAnchor = previewHeadAnchor.transform;
        preview.skinAura = pAura;
        preview.previewCamera = cam;
        preview.skinTextures = previewTextures;
        preview.skinTextureIds = SkinTextureNames;
    }

    static void CreateEffectsManager()
    {
        var go = new GameObject("EffectsManager");
        var effects = go.AddComponent<EffectsManager>();

        effects.dustTemplate = BuildBurstParticle("DustTemplate", new Color(0.6f, 0.5f, 0.4f), 10, 0.3f, 0.6f);
        effects.dustTemplate.transform.SetParent(go.transform);
        effects.dustTemplate.gameObject.SetActive(false);

        effects.sparkleTemplate = BuildBurstParticle("SparkleTemplate", new Color(1f, 0.85f, 0.3f), 14, 0.2f, 0.5f);
        effects.sparkleTemplate.transform.SetParent(go.transform);
        effects.sparkleTemplate.gameObject.SetActive(false);

        effects.jumpRingTemplate = BuildRingParticle("JumpRingTemplate", new Color(0.62f, 0.86f, 1f));
        effects.jumpRingTemplate.transform.SetParent(go.transform);
        effects.jumpRingTemplate.gameObject.SetActive(false);
    }

    // An expanding horizontal ring of glowing motes - the tell for a mid-air (double) jump.
    static ParticleSystem BuildRingParticle(string name, Color color)
    {
        GameObject go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = 0.34f;
        main.startSpeed = 4.6f;
        main.startSize = 0.26f;
        main.startColor = color * 2f; // HDR-bright so it blooms
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)26) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;
        shape.radiusThickness = 0f;              // emit from the ring edge, moving outward
        shape.arc = 360f;
        shape.rotation = new Vector3(90f, 0f, 0f); // lay the ring flat (horizontal, on the ground)

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        Texture2D soft = GetSoftParticleTexture();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", soft);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", soft);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3100;
        renderer.material = mat;

        ps.Stop();
        return ps;
    }

    static Texture2D softParticleTex;

    // A soft round glow sprite (radial alpha) so burst effects read as smooth puffs, not pixel blocks.
    static Texture2D GetSoftParticleTexture()
    {
        if (softParticleTex != null)
            return softParticleTex;

        const string assetPath = "Assets/Generated/SoftParticle.png";
        softParticleTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (softParticleTex != null)
            return softParticleTex;

        string dir = Path.Combine(Application.dataPath, "Generated");
        Directory.CreateDirectory(dir);
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2((S - 1) / 2f, (S - 1) / 2f);
        float maxR = (S - 1) / 2f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        File.WriteAllBytes(Path.Combine(dir, "SoftParticle.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.Refresh();

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        softParticleTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        return softParticleTex;
    }

    static ParticleSystem BuildBurstParticle(string name, Color color, int burstCount, float size, float lifetime)
    {
        GameObject go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = 3f;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        // Smooth shrink-out so particles fade instead of popping.
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        // Soft round sprite + alpha-transparent blend so effects look like smooth puffs.
        Texture2D soft = GetSoftParticleTexture();
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", soft);
        if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", soft);
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);
        renderer.material = mat;

        ps.Stop();
        return ps;
    }

    static void CreateMusicManager()
    {
        // ~30 min loop track. Import as Vorbis "Compressed In Memory" so it's small in RAM and can
        // be played by two AudioSources at once (MusicManager crossfades them for a seamless loop).
        string loopPath = AudioPath + "Music/music_loop.mp3";
        var audioImporter = AssetImporter.GetAtPath(loopPath) as AudioImporter;
        if (audioImporter != null)
        {
            var settings = audioImporter.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.CompressedInMemory || settings.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.5f;
                settings.preloadAudioData = true;
                audioImporter.defaultSampleSettings = settings;
                audioImporter.SaveAndReimport();
            }
        }

        var go = new GameObject("MusicManager");
        var music = go.AddComponent<MusicManager>();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(loopPath);
        music.loopClip = clip;
        Debug.Log(clip != null
            ? $"MusicManager: loop clip loaded ({clip.length:0}s)"
            : "MusicManager: loop clip is NULL at " + loopPath);
    }

    // Grass-topped Kenney platformer blocks in varied footprints (all full height so landing spots
    // stay consistent) - big, cheerful, themed platforms instead of the old grey crates. Snow blocks
    // do the same for the ice zone. Volcano/cloud/final use the fortified/overhang/ramp platform set.
    static readonly string[] GrassZonePlatforms = { "block-grass-large", "block-grass-long", "block-grass", "block-grass-hexagon", "block-grass-narrow" };
    static readonly string[] VolcanoZonePlatforms = { "platform-fortified", "brick", "platform", "platform-ramp" };
    static readonly string[] CloudZonePlatforms = { "platform-overhang", "platform-ramp", "platform", "platform-fortified" };
    static readonly string[] IceZonePlatforms = { "block-snow-large", "block-snow-long", "block-snow", "block-snow-hexagon", "block-snow-narrow" };
    static readonly string[] FinalZonePlatforms = { "platform-fortified", "platform-overhang", "platform-ramp", "brick" };

    static string[] GetZonePlatformPool(float t)
    {
        if (t < 0.2f) return GrassZonePlatforms;
        if (t < 0.4f) return VolcanoZonePlatforms;
        if (t < 0.6f) return CloudZonePlatforms;
        if (t < 0.8f) return IceZonePlatforms;
        return FinalZonePlatforms;
    }

    // Deliberately ECLECTIC prop pool (Only Up-style): grass/snow blocks, crates, boulders, cliffs,
    // giant mushrooms, stumps, logs, statues AND everyday furniture - so the climb is a varied,
    // colourful tower of different objects, never a run of identical blocks. kit: k=Kenney, n=Nature,
    // f=Furniture. Curated to moderate-aspect shapes so size-normalization gives fair landing pads.
    static readonly (string kit, string model)[] VariedProps =
    {
        // Only kits that render with a real texture (the furniture kit had no texture and rendered
        // solid black, so it's out). Still a big eclectic mix of object shapes: grass/snow blocks,
        // crates, platforms, boulders, cliffs, giant mushrooms, tree stumps, log stacks, statues.
        ("k","block-grass-large"), ("n","rock_largeA"), ("k","crate-strong"), ("n","mushroom_redTall"),
        ("k","block-grass-hexagon"), ("n","cliff_block_rock"), ("k","platform-fortified"), ("n","stump_squareDetailedWide"),
        ("k","block-snow-large"), ("n","rock_largeC"), ("k","crate-item-strong"), ("n","log_stackLarge"),
        ("k","block-grass-long"), ("n","mushroom_tanTall"), ("k","brick"), ("n","rock_largeD"),
        ("n","cliff_block_stone"), ("k","block-snow-hexagon"), ("n","statue_block"), ("k","platform"),
        ("n","rock_largeB"), ("n","stump_round"), ("n","rock_largeE"), ("n","cliff_blockHalf_rock"),
        ("n","log_stack"), ("n","stump_old"),
    };

    // Builds one climb platform from the eclectic pool. Wildly different props are normalized to a
    // fair, consistent landing size via their (build-time-reliable) static mesh bounds, wrapped so the
    // wrapper origin is the landing base (any model pivot works), and given a flat box-collider top.
    static GameObject BuildPlatformShape(int shapeType, int index, float sizeMultiplier, float t)
    {
        var entry = VariedProps[index % VariedProps.Length];
        GameObject prop = InstantiateProp(entry.kit, entry.model, Vector3.zero);
        if (prop == null)
            prop = InstantiateKenney("block-grass-large", Vector3.zero);
        if (prop == null)
            prop = GameObject.CreatePrimitive(PrimitiveType.Cube);

        GameObject wrapper = new GameObject("Platform_" + index);
        prop.transform.SetParent(wrapper.transform, false);
        prop.transform.localRotation = Quaternion.Euler(0f, 90f * (index % 4), 0f);

        // Normalize by HEIGHT to a consistent top (so jump gaps stay fair regardless of the prop),
        // widening only props that would otherwise be too narrow to land on.
        if (TryGetHierarchyBounds(prop, out Bounds b0) && b0.size.y > 0.01f)
        {
            float targetTop = 1.1f * sizeMultiplier;
            float scale = targetTop / b0.size.y;
            float footprint = Mathf.Min(b0.size.x, b0.size.z) * scale;
            float minFoot = 1.5f * sizeMultiplier;
            if (footprint > 0.01f && footprint < minFoot)
                scale *= minFoot / footprint;
            prop.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.03f, 30f);

            // Ground the base at the wrapper origin and centre the footprint on the spin axis.
            TryGetHierarchyBounds(prop, out Bounds b);
            prop.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);

            TryGetHierarchyBounds(prop, out Bounds bf);
            var col = wrapper.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, bf.size.y * 0.5f, 0f);
            col.size = new Vector3(Mathf.Max(0.1f, bf.size.x), Mathf.Max(0.1f, bf.size.y), Mathf.Max(0.1f, bf.size.z));
        }
        else
        {
            prop.transform.localScale = Vector3.one * sizeMultiplier * 1.5f;
            AddSolidCollider(wrapper);
        }

        return wrapper;
    }

    static readonly string[] HazardModels = { "saw", "spike-block", "trap-spikes" };
    static readonly string[] GemModels = { "coin-gold", "coin-silver", "jewel" };

    static void CreateHazard(Vector3 pos, int index)
    {
        string modelName = HazardModels[index % HazardModels.Length];
        GameObject hazard = InstantiateKenney(modelName, pos);
        hazard.name = "Hazard_" + index;
        hazard.transform.localScale = Vector3.one * 1.1f;
        var col = AddSolidCollider(hazard);
        col.isTrigger = true;
        var hazardComp = hazard.AddComponent<Hazard>();
        hazardComp.spins = modelName == "saw";
    }

    // Normal coins keep the shared Kenney atlas look (cheap, batched). Rare/Legendary get a
    // per-instance material clone so they can be tinted/glowing without affecting every other
    // coin in the scene that still shares the one cached Kenney material.
    static void CreateGem(Vector3 pos, int index, CoinType type = CoinType.Normal)
    {
        string modelName = GemModels[index % GemModels.Length];
        GameObject gem = InstantiateKenney(modelName, pos);
        gem.name = "Gem_" + index + "_" + type;

        float scale = type switch
        {
            CoinType.Legendary => 1.8f,
            CoinType.Epic => 1.6f,
            CoinType.Rare => 1.45f,
            _ => 1.2f,
        };
        gem.transform.localScale = Vector3.one * scale;

        if (type != CoinType.Normal)
        {
            // Rare = blue, Epic = purple, Legendary = gold.
            Color tint = type switch
            {
                CoinType.Legendary => new Color(1.3f, 1.05f, 0.25f),
                CoinType.Epic => new Color(0.85f, 0.45f, 1.3f),
                _ => new Color(0.55f, 0.65f, 1.3f),
            };
            Color emission = type switch
            {
                CoinType.Legendary => new Color(0.9f, 0.6f, 0.05f),
                CoinType.Epic => new Color(0.5f, 0.15f, 0.8f),
                _ => new Color(0.15f, 0.2f, 0.55f),
            };

            foreach (var rend in gem.GetComponentsInChildren<Renderer>())
            {
                Material unique = new Material(rend.sharedMaterial);
                unique.SetColor("_BaseColor", tint);
                unique.EnableKeyword("_EMISSION");
                unique.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                unique.SetColor("_EmissionColor", emission);
                rend.sharedMaterial = unique;
            }
        }

        var col = AddSolidCollider(gem);
        col.isTrigger = true;
        var coin = gem.AddComponent<Coin>();
        coin.type = type;
        coin.legendaryBaseScale = scale;
    }

    static void CreateCrate(Vector3 pos, int index, int coins)
    {
        GameObject crate = InstantiateKenney("crate-item", pos);
        crate.name = "Crate_" + index;
        crate.transform.localScale = Vector3.one * 1.1f;
        var col = AddSolidCollider(crate);
        col.isTrigger = true;
        var c = crate.AddComponent<Crate>();
        c.coins = coins;
    }

    // Telegraphed pop-up spikes (rise/retract on a timer) - avoidable side hazard, not a forced death.
    static void CreatePopupSpike(Vector3 pos, int index)
    {
        GameObject spike = InstantiateKenney("trap-spikes", pos);
        spike.name = "PopupSpike_" + index;
        spike.transform.localScale = Vector3.one * 1.1f;
        var col = AddSolidCollider(spike);
        col.isTrigger = true;
        var ps = spike.AddComponent<PopupSpike>();
        ps.phaseOffset = index * 0.5f;
    }

    // TNT crate (Crash-style): a clearly-telegraphed bomb; touching it is lethal. Sits to the side.
    static void CreateTntCrate(Vector3 pos, int index)
    {
        GameObject tnt = InstantiateKenney("bomb", pos);
        tnt.name = "TntCrate_" + index;
        tnt.transform.localScale = Vector3.one * 1.1f;
        var col = AddSolidCollider(tnt);
        col.isTrigger = true;
        var h = tnt.AddComponent<Hazard>();
        h.spins = false;
    }

    static void CreateBouncePad(Vector3 pos, int index)
    {
        GameObject pad = InstantiateKenney("spring", pos);
        pad.name = "BouncePad_" + index;
        pad.transform.localScale = Vector3.one * 1.2f;

        var trigger = AddSolidCollider(pad);
        trigger.isTrigger = true;

        pad.AddComponent<BouncePad>();
    }

    // Endless-only sky: the base star field, wisps and aurora end around the ~362 m base summit,
    // so the 5x extension gets its own column of stars, wisps and a second aurora ring. Parented
    // under TowerEndless, so it only exists (and only costs anything) in Endless mode.
    static void CreateEndlessAtmosphere()
    {
        if (towerEndlessRoot == null)
            return;

        var root = new GameObject("EndlessAtmosphere");
        root.transform.SetParent(towerEndlessRoot.transform, false);

        float bottom = actualTopHeight;        // where the base sky ends
        float top = endlessTopPos.y + 50f;     // a bit above the endless summit
        float mid = (bottom + top) * 0.5f;
        float span = Mathf.Max(80f, top - bottom);

        Shader psShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (psShader == null) psShader = Shader.Find("Sprites/Default");

        // Star column covering the whole extension.
        var starsGO = new GameObject("EndlessStars");
        starsGO.transform.SetParent(root.transform, false);
        var sps = starsGO.AddComponent<ParticleSystem>();
        var smain = sps.main;
        smain.loop = true;
        smain.startLifetime = 30f;
        smain.startSpeed = 0.04f;
        smain.startSize = 0.16f;
        smain.startColor = Color.white;
        smain.maxParticles = 900;
        smain.simulationSpace = ParticleSystemSimulationSpace.World;
        var sem = sps.emission;
        sem.rateOverTime = 30f;
        var sshape = sps.shape;
        sshape.shapeType = ParticleSystemShapeType.Box;
        sshape.position = new Vector3(0f, mid, 0f);
        sshape.scale = new Vector3(220f, span, 220f);
        var srend = starsGO.GetComponent<ParticleSystemRenderer>();
        var smat = new Material(psShader);
        if (smat.HasProperty("_BaseColor")) smat.SetColor("_BaseColor", Color.white);
        srend.material = smat;

        // Wisp column (same recipe as the base ClimbWisps, shifted up the extension).
        var wispGO = new GameObject("EndlessWisps");
        wispGO.transform.SetParent(root.transform, false);
        var wps = wispGO.AddComponent<ParticleSystem>();
        var wmain = wps.main;
        wmain.loop = true;
        wmain.startLifetime = 9f;
        wmain.startSpeed = 0.25f;
        wmain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        wmain.startColor = new Color(0.6f, 0.85f, 1f, 0.6f);
        wmain.maxParticles = 200;
        wmain.simulationSpace = ParticleSystemSimulationSpace.World;
        var wem = wps.emission;
        wem.rateOverTime = 10f;
        var wshape = wps.shape;
        wshape.shapeType = ParticleSystemShapeType.Box;
        wshape.position = new Vector3(0f, mid, 0f);
        wshape.scale = new Vector3(140f, span, 140f);
        var wnoise = wps.noise;
        wnoise.enabled = true;
        wnoise.strength = 0.6f;
        wnoise.frequency = 0.2f;
        wnoise.scrollSpeed = 0.15f;
        var wrend = wispGO.GetComponent<ParticleSystemRenderer>();
        var wmat = new Material(psShader);
        if (wmat.HasProperty("_SrcBlend")) wmat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (wmat.HasProperty("_DstBlend")) wmat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (wmat.HasProperty("_ZWrite")) wmat.SetFloat("_ZWrite", 0f);
        if (wmat.HasProperty("_BaseColor")) wmat.SetColor("_BaseColor", new Color(0.7f, 0.9f, 1f));
        wmat.mainTexture = GetSoftParticleTexture();
        wmat.renderQueue = 3100;
        wrend.material = wmat;

        // Second aurora ring stacked up the extension heights.
        var shimGO = new GameObject("EndlessAurora");
        shimGO.transform.SetParent(root.transform, false);
        var shimmer = shimGO.AddComponent<AuroraShimmer>();
        var rends = new List<Renderer>();
        Color[] cols = { new Color(0.3f, 1f, 0.6f), new Color(0.35f, 0.8f, 1f), new Color(0.6f, 0.45f, 1f) };
        Texture2D soft = GetSoftParticleTexture();
        const int n = 6;
        for (int i = 0; i < n; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "EndlessAurora_" + i;
            q.transform.SetParent(shimGO.transform);
            float ang = (i / (float)n) * Mathf.PI * 2f;
            float dist = 240f + (i % 3) * 60f;
            float y = Mathf.Lerp(bottom + 150f, top, (i + 0.5f) / n);
            q.transform.position = new Vector3(Mathf.Sin(ang) * dist, y, Mathf.Cos(ang) * dist);
            q.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(ang), 0f, -Mathf.Cos(ang)));
            q.transform.localScale = new Vector3(120f + (i % 2) * 40f, 160f + (i % 3) * 40f, 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MakeAdditiveGlowMaterial(cols[i % cols.Length] * 2f, soft);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rends.Add(mr);
        }
        shimmer.curtains = rends.ToArray();

        root.SetActive(false); // toggled by ModeWorld together with the Endlos tower chunks
    }

    static ParticleSystem CreateStarField()
    {
        GameObject starsGO = new GameObject("StarField");
        starsGO.transform.position = new Vector3(0f, actualTopHeight - 10f, 0f);
        var ps = starsGO.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 25f;
        main.startSpeed = 0.05f;
        main.startSize = 0.15f;
        main.startColor = Color.white;
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 60f;

        var renderer = starsGO.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Sprites/Default");
        var starMat = new Material(particleShader);
        if (starMat.HasProperty("_BaseColor"))
            starMat.SetColor("_BaseColor", Color.white);
        renderer.material = starMat;

        return ps;
    }

    static ParticleSystem CreateWeatherParticles()
    {
        GameObject go = new GameObject("WeatherParticles");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 6f;
        main.startSpeed = 0.5f;
        main.startSize = 0.12f;
        main.startColor = Color.white;
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.15f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 4f, 30f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(particleShader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", Color.white);
        renderer.material = mat;

        return ps;
    }

    // A gentle drift of glowing fireflies near the ground, giving the night jungle a living, magical
    // feel. Warm yellow-green soft orbs that slowly blink (alpha pulse over lifetime) and meander.
    static void CreateFireflies()
    {
        GameObject go = new GameObject("Fireflies");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 7f;
        main.startSpeed = 0.35f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        main.startColor = new Color(1f, 0.95f, 0.55f, 1f);
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 55f;

        // Spread across the playable meadow, hugging the ground where the player starts and roams.
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0f, 9f, 0f);
        shape.scale = new Vector3(230f, 16f, 230f);

        // Slow wandering drift so they meander like real fireflies instead of moving in straight lines.
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.2f;

        // Blink: fade in -> glow -> dim -> glow -> fade out over each particle's life.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.55f), 0f),
                new GradientColorKey(new Color(0.8f, 1f, 0.55f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0.15f, 0.5f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Sprites/Default");
        var mat = new Material(particleShader);
        // Additive blending so the orbs glow against the dark night (and bloom catches them).
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.6f));
        mat.mainTexture = GetSoftParticleTexture();
        mat.renderQueue = 3100;
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    // A big, softly glowing moon disc in the night sky, placed opposite the moonlight direction so it
    // reads as the light source. Unlit + HDR-bright so the Bloom post-processing wraps it in a halo.
    static void CreateMoonDisc(Light moon)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Moon";
        Object.DestroyImmediate(go.GetComponent<Collider>());

        Vector3 dir = moon.transform.forward;   // direction the light travels
        go.transform.position = -dir * 820f;     // sit up-sky, opposite the rays
        go.transform.localScale = Vector3.one * 105f;

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null)
            unlit = Shader.Find("Unlit/Color");
        var mat = new Material(unlit);
        // HDR-bright pale blue-white so it blooms into a glowing moon (bloom threshold is 0.8).
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1.5f, 1.55f, 1.7f));
        else mat.color = new Color(0.95f, 0.97f, 1f);
        mr.sharedMaterial = mat;
    }

    static GameObject CreateGameManager()
    {
        var gm = new GameObject("GameManager");
        var manager = gm.AddComponent<GameManager>();
        manager.topHeight = actualTopHeight;
        return gm;
    }

    const string AudioPath = "Assets/Audio/";

    static void CreateAudioManager()
    {
        var go = new GameObject("AudioManager");
        var audio = go.AddComponent<AudioManager>();

        audio.footstepClips = new[]
        {
            AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "footstep_grass_000.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "footstep_grass_001.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "footstep_grass_002.ogg"),
        };
        audio.jumpClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "jump.ogg");
        audio.landClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "land.ogg");
        audio.coinClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "coin.ogg");
        audio.deathClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "death.ogg");
        audio.checkpointClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "checkpoint.ogg");
        audio.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "click.ogg");
        audio.bounceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "bounce.ogg");
        audio.whooshClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "whoosh.ogg");
        audio.victoryClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "victory.ogg");
    }

    static void SetGameIcon()
    {
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon/icon.jpg");
        if (icon == null)
            return;

        int standaloneCount = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone).Length;
        var standaloneIcons = new Texture2D[Mathf.Max(1, standaloneCount)];
        for (int i = 0; i < standaloneIcons.Length; i++)
            standaloneIcons[i] = icon;
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, standaloneIcons);

        int defaultCount = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Unknown).Length;
        var defaultIcons = new Texture2D[Mathf.Max(1, defaultCount)];
        for (int i = 0; i < defaultIcons.Length; i++)
            defaultIcons[i] = icon;
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, defaultIcons);
    }

    static void CreateSettingsMenu()
    {
        var go = new GameObject("MainMenu");
        go.AddComponent<MainMenu>();
        go.AddComponent<SettingsMenu>();
        go.AddComponent<ShopMenu>();
    }

    static void CreateTutorialOverlay()
    {
        var go = new GameObject("TutorialOverlay");
        go.AddComponent<TutorialOverlay>();
    }

    static void CreateAtmosphereManager(Light sunLight, ParticleSystem stars, ParticleSystem weather)
    {
        var go = new GameObject("AtmosphereManager");
        var atmosphere = go.AddComponent<ZoneAtmosphere>();
        atmosphere.sunLight = sunLight;
        atmosphere.stars = stars;
        atmosphere.weather = weather;

        atmosphere.zones = new[]
        {
            // PERMANENT NIGHT: every zone is moonlit (starry sky throughout, skyBlend = 1). Light stays
            // bright enough (0.95-1.15) that platforms/obstacles read clearly - the "night" comes from
            // the sky + blue-violet grading, not from darkness. Subtle per-zone tints keep variety.

            // Wiesenland - moonlit night jungle: cool blue moonlight, deep-blue haze, drifting motes.
            new ZoneAtmosphere.Zone
            {
                height = 0f,
                fogColor = new Color(0.12f, 0.15f, 0.28f),
                skyTint = new Color(0.6f, 0.62f, 0.78f),
                skyExposure = 1.35f,
                skyBlend = 1f,
                lightColor = new Color(0.68f, 0.78f, 1f),
                lightIntensity = 0.85f,
                particleColor = new Color(0.8f, 0.9f, 1f, 0.6f),
                particleRate = 10f,
            },
            // Vulkanfeld - night with a faint warm ember glow in the haze.
            new ZoneAtmosphere.Zone
            {
                height = actualTopHeight * 0.25f,
                fogColor = new Color(0.22f, 0.13f, 0.18f),
                skyTint = new Color(0.62f, 0.55f, 0.68f),
                skyExposure = 1.3f,
                skyBlend = 1f,
                lightColor = new Color(0.85f, 0.72f, 0.85f),
                lightIntensity = 0.9f,
                particleColor = new Color(1f, 0.55f, 0.3f, 0.8f),
                particleRate = 16f,
            },
            // Wolkenreich - misty moonlit clouds, cool blue-white.
            new ZoneAtmosphere.Zone
            {
                height = actualTopHeight * 0.5f,
                fogColor = new Color(0.24f, 0.28f, 0.44f),
                skyTint = new Color(0.6f, 0.64f, 0.8f),
                skyExposure = 1.4f,
                skyBlend = 1f,
                lightColor = new Color(0.75f, 0.83f, 1f),
                lightIntensity = 0.92f,
                particleColor = new Color(0.85f, 0.92f, 1f, 0.6f),
                particleRate = 12f,
            },
            // Eiskristall - icy moonlit night, falling snow glinting.
            new ZoneAtmosphere.Zone
            {
                height = actualTopHeight * 0.75f,
                fogColor = new Color(0.18f, 0.26f, 0.42f),
                skyTint = new Color(0.58f, 0.66f, 0.82f),
                skyExposure = 1.35f,
                skyBlend = 1f,
                lightColor = new Color(0.8f, 0.9f, 1f),
                lightIntensity = 0.9f,
                particleColor = new Color(0.9f, 0.97f, 1f, 0.85f),
                particleRate = 24f,
            },
            // Sternenkrone - deep starry space, the climax of the night climb.
            new ZoneAtmosphere.Zone
            {
                height = actualTopHeight,
                fogColor = new Color(0.03f, 0.03f, 0.12f),
                skyTint = new Color(0.6f, 0.6f, 0.78f),
                skyExposure = 1.3f,
                skyBlend = 1f,
                lightColor = new Color(0.72f, 0.76f, 1f),
                lightIntensity = 0.75f,
                particleColor = new Color(0.75f, 0.7f, 1f, 0.8f),
                particleRate = 10f,
            },
        };
    }
}
