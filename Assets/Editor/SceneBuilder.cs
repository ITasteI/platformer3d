using System.IO;
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
    const int PlatformCount = 550;
    const float TopHeight = 500f;
    // Real height the generated tower reaches (measured after CreateTower). Zone atmosphere,
    // music, stars and the HUD all key off this so all five worlds are actually reached,
    // instead of the old fixed 500 that the ~240m tower never climbed to.
    static float actualTopHeight = TopHeight;
    const string KitPath = "Assets/KenneyKit/";
    const string NatureKitPath = "Assets/NatureKit/";
    const string SpaceKitPath = "Assets/SpaceKit/";

    static Material spaceMaterial;

    static Material kenneyMaterial;

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

    static GameObject InstantiateModel(string basePath, Material mat, string modelName, Vector3 pos)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath + modelName + ".fbx");
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

        GraphicsSettings.defaultRenderPipeline = urpAsset;
        QualitySettings.renderPipeline = urpAsset;
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
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.5f);
        bloom.scatter.Override(0.7f);

        var colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.postExposure.Override(0.15f);
        colorAdjustments.contrast.Override(12f);
        colorAdjustments.saturation.Override(18f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.6f);

        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

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
        Camera lobbyCam = CreateLobbyCamera();
        SetupPostProcessing(lobbyCam);
        GameObject playerPrefab = CreatePlayerPrefab();
        CreateNetworkManagerAndLobby(playerPrefab, lobbyCam);
        CreateGround();
        CreateRiverWater();
        CreateLake();
        CreateNatureScatter();
        CreateForestBelt();
        CreateRockFormations();
        CreateBonusNatureScatter();
        CreateSpawnDecor();
        CreateBackgroundIslands();
        CreateDistantLandmark();
        CreateMountainRing();
        Vector3 topPos = CreateTower();
        CreateClouds();
        CreateWorldAmbience();
        CreateGoalFlag(topPos);
        ParticleSystem stars = CreateStarField();
        ParticleSystem weather = CreateWeatherParticles();
        CreateGameManager();
        CreateAudioManager();
        CreateMusicManager();
        CreateEffectsManager();
        CreateSettingsMenu();
        CreateWinScreen();
        CreateTutorialOverlay();
        CreateAtmosphereManager(sunLight, stars, weather);

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
        sky.SetColor("_Tex_HDR", new Color(1f, 1f, 0f, 0f));
        sky.SetColor("_Tex_Blend_HDR", new Color(1f, 1f, 0f, 0f));
        sky.SetFloat("_CubemapTransition", 0f);
        sky.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 1f));
        sky.SetFloat("_Exposure", 1f);
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
        var lightGO = new GameObject("Sun");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.93f, 0.8f);
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        Material sky = CreateSkyboxMaterial();
        RenderSettings.skybox = sky;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.6f, 0.65f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.48f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.6f, 0.55f, 0.5f);
        // Wide enough that distant mountains (radius 260-420) read as hazy silhouettes with
        // real color/shading instead of flattening into a solid fog-colored blob.
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 480f;

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
        controller.radius = 0.5f;
        controller.center = new Vector3(0f, 1f, 0f);

        var playerController = root.AddComponent<PlayerController>();

        GameObject visual = InstantiateKenney("character-oobi", Vector3.zero);
        visual.name = "CharacterVisual";
        visual.transform.SetParent(root.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        Animator animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = BuildCharacterAnimator(KitPath + "character-oobi.fbx");
        playerController.animator = animator;

        var squash = visual.AddComponent<CharacterSquash>();
        squash.player = playerController;

        var admin = root.AddComponent<AdminController>();
        admin.player = playerController;

        GameObject nameTagGO = new GameObject("NameTag");
        nameTagGO.transform.SetParent(root.transform);
        nameTagGO.transform.localPosition = new Vector3(0f, 2.3f, 0f);
        TextMesh nameTagText = nameTagGO.AddComponent<TextMesh>();
        nameTagText.characterSize = 0.12f;
        nameTagText.fontSize = 48;
        nameTagText.anchor = TextAnchor.LowerCenter;
        nameTagText.alignment = TextAlignment.Center;
        nameTagText.color = Color.white;
        var nameTagDisplay = nameTagGO.AddComponent<PlayerNameTagDisplay>();
        nameTagDisplay.player = playerController;

        var camGO = new GameObject("PlayerCamera");
        camGO.transform.SetParent(root.transform);
        camGO.tag = "MainCamera";
        Camera cam = camGO.AddComponent<Camera>();
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

    // A broad arc kept at radius >= 65 so it never crosses the flattened spawn area or
    // overlaps the tower base. Parameterized by s in [0,1].
    static Vector3 RiverPoint(float s)
    {
        float angle = Mathf.Lerp(-110f, 110f, s) * Mathf.Deg2Rad;
        float radius = 100f + Mathf.Sin(s * Mathf.PI) * 35f;
        return new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
    }

    static float DistanceToRiver(float worldX, float worldZ, out float riverS)
    {
        float best = float.MaxValue;
        float bestS = 0f;
        const int samples = 48;
        for (int i = 0; i <= samples; i++)
        {
            float s = i / (float)samples;
            Vector3 p = RiverPoint(s);
            float d = (new Vector2(worldX, worldZ) - new Vector2(p.x, p.z)).magnitude;
            if (d < best)
            {
                best = d;
                bestS = s;
            }
        }
        riverS = bestS;
        return best;
    }

    // Normalized (0..1) heightmap value for the riverbed at progress s along its length -
    // high upstream, then a sharp drop around s=0.3-0.45 for a waterfall, low downstream.
    static float RiverBedNormalizedHeight(float s)
    {
        return Mathf.Lerp(0.35f, 0.03f, Mathf.Clamp01((s - 0.3f) / 0.15f));
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
                float h = (macro * 0.5f + detail * 0.35f + fine * 0.15f) * flatten * 0.6f;

                float distToRiver = DistanceToRiver(worldX, worldZ, out float riverS);
                const float riverHalfWidth = 5f;
                const float riverBankWidth = 9f;
                float riverCarve = 1f - Mathf.Clamp01((distToRiver - riverHalfWidth) / riverBankWidth);
                h = Mathf.Lerp(h, RiverBedNormalizedHeight(riverS), riverCarve);

                float distToLake = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(LakeCenter.x, LakeCenter.z));
                float lakeCarve = 1f - Mathf.Clamp01((distToLake - LakeRadius) / LakeShoreWidth);
                h = Mathf.Lerp(h, LakeBedNormalizedHeight, lakeCarve);

                heights[z, x] = h;
            }
        }
        terrainData.SetHeights(0, 0, heights);

        Texture2D grassTex = CreateSolidTexture("GrassTerrainTex", new Color(0.3f, 0.4f, 0.2f), new Color(0.38f, 0.48f, 0.26f));
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/GrassTerrainLayer.asset");
        if (layer == null)
        {
            layer = new TerrainLayer();
            layer.diffuseTexture = grassTex;
            layer.tileSize = new Vector2(8f, 8f);
            AssetDatabase.CreateAsset(layer, "Assets/GrassTerrainLayer.asset");
        }
        terrainData.terrainLayers = new[] { layer };

        GameObject terrainGO = Terrain.CreateTerrainGameObject(terrainData);
        terrainGO.name = "Ground";
        terrainGO.transform.position = new Vector3(-size / 2f, 0f, -size / 2f);

        groundTerrain = terrainGO.GetComponent<Terrain>();
        groundTerrain.allowAutoConnect = false;
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

        waterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        waterMaterial.SetColor("_BaseColor", new Color(0.22f, 0.5f, 0.62f, 0.6f));
        waterMaterial.SetFloat("_Smoothness", 0.85f);
        waterMaterial.SetFloat("_Surface", 1f);
        waterMaterial.SetFloat("_Blend", 0f);
        waterMaterial.SetOverrideTag("RenderType", "Transparent");
        waterMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        waterMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        waterMaterial.SetInt("_ZWrite", 0);
        waterMaterial.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        waterMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        waterMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return waterMaterial;
    }

    static Mesh BuildRiverRibbonMesh(int steps, float width)
    {
        Vector3[] vertices = new Vector3[(steps + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[steps * 6];

        for (int i = 0; i <= steps; i++)
        {
            float s = i / (float)steps;
            Vector3 p = RiverPoint(s);
            Vector3 pNext = RiverPoint(Mathf.Min(1f, s + 0.01f));
            Vector3 tangent = (pNext - p).normalized;
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x) * (width * 0.5f);
            float y = RiverBedNormalizedHeight(s) * RiverMaxHeight + 0.12f;

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
        GameObject river = new GameObject("River");
        var filter = river.AddComponent<MeshFilter>();
        filter.sharedMesh = BuildRiverRibbonMesh(60, 9f);
        var renderer = river.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.sharedMaterial = GetWaterMaterial();
        river.isStatic = true;

        // Waterfall: a vertical water sheet spanning the height drop, plus a light mist puff.
        Vector3 topPoint = RiverPoint(WaterfallS - 0.03f);
        Vector3 bottomPoint = RiverPoint(WaterfallS + 0.05f);
        float topY = RiverBedNormalizedHeight(WaterfallS - 0.03f) * RiverMaxHeight + 0.3f;
        float bottomY = RiverBedNormalizedHeight(WaterfallS + 0.05f) * RiverMaxHeight + 0.1f;
        float dropHeight = Mathf.Max(0.5f, topY - bottomY);

        GameObject fall = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fall.name = "Waterfall";
        Object.DestroyImmediate(fall.GetComponent<Collider>());
        Vector3 mid = (topPoint + bottomPoint) * 0.5f;
        fall.transform.position = new Vector3(mid.x, (topY + bottomY) * 0.5f, mid.z);
        Vector3 flowDir = (bottomPoint - topPoint);
        flowDir.y = bottomY - topY;
        fall.transform.rotation = Quaternion.LookRotation(new Vector3(flowDir.z, 0f, -flowDir.x).normalized) * Quaternion.Euler(90f, 0f, 0f);
        fall.transform.localScale = new Vector3(7f, dropHeight, 1f);
        fall.GetComponent<Renderer>().sharedMaterial = GetWaterMaterial();

        GameObject mist = new GameObject("WaterfallMist");
        mist.transform.position = new Vector3(bottomPoint.x, bottomY + 0.3f, bottomPoint.z);
        var ps = mist.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.6f;
        main.startSize = 0.5f;
        main.startColor = new Color(1f, 1f, 1f, 0.5f);
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 20f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(3f, 0.3f, 1f);
        var pRenderer = mist.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Universal Render Pipeline/Unlit");
        var mistMat = new Material(particleShader);
        if (mistMat.HasProperty("_BaseColor"))
            mistMat.SetColor("_BaseColor", Color.white);
        pRenderer.material = mistMat;
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

    static void CreateNatureScatter()
    {
        Random.InitState(777);
        GameObject root = new GameObject("NatureScatter");
        int count = 90;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(11f, 65f);
            Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            pos.y = SampleTerrainHeight(pos);

            float roll = Random.value;
            string modelName;
            float scale;
            if (roll < 0.35f) { modelName = MeadowTrees[Random.Range(0, MeadowTrees.Length)]; scale = Random.Range(0.9f, 1.4f); }
            else if (roll < 0.6f) { modelName = MeadowRocks[Random.Range(0, MeadowRocks.Length)]; scale = Random.Range(0.8f, 1.6f); }
            else { modelName = MeadowGround[Random.Range(0, MeadowGround.Length)]; scale = Random.Range(0.7f, 1.2f); }

            GameObject obj = InstantiateNature(modelName, pos);
            if (obj == null)
                continue;

            obj.name = "Nature_" + i;
            obj.transform.SetParent(root.transform);
            obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            obj.transform.localScale = Vector3.one * scale;
            obj.isStatic = true;
            obj.AddComponent<DistanceCuller>().maxDistance = 90f;
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

    static void CreateMountainRing()
    {
        Random.InitState(2024);
        GameObject root = new GameObject("MountainRange");
        // Count and minimum base radius are sized so neighboring mountains always overlap at
        // any radius in range - no gaps to see the void through between peaks.
        int count = 44;

        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.08f, 0.08f);
            float radius = Random.Range(260f, 440f);
            float height = Random.Range(90f, 220f);
            float baseRadius = Random.Range(55f, 95f);
            Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, -10f, Mathf.Cos(angle) * radius);

            GameObject mountain = new GameObject("Mountain_" + i);
            mountain.transform.SetParent(root.transform);
            mountain.transform.position = pos;
            mountain.isStatic = true;

            // Two overlapping jagged cones per peak read as a small ridge instead of a lone
            // symmetric pyramid, and give each mountain a secondary shoulder peak.
            GameObject main = new GameObject("Rock");
            main.transform.SetParent(mountain.transform);
            main.transform.localPosition = Vector3.zero;
            var mainFilter = main.AddComponent<MeshFilter>();
            mainFilter.sharedMesh = BuildJaggedPeakMesh(baseRadius, height, 10, i * 13.1f);
            var mainRenderer = main.AddComponent<MeshRenderer>();
            mainRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mainRenderer.sharedMaterial = GetRockMaterial();

            GameObject shoulder = new GameObject("Shoulder");
            shoulder.transform.SetParent(mountain.transform);
            float shoulderAngle = Random.Range(0f, Mathf.PI * 2f);
            shoulder.transform.localPosition = new Vector3(Mathf.Sin(shoulderAngle) * baseRadius * 0.7f, 0f, Mathf.Cos(shoulderAngle) * baseRadius * 0.7f);
            var shoulderFilter = shoulder.AddComponent<MeshFilter>();
            shoulderFilter.sharedMesh = BuildJaggedPeakMesh(baseRadius * 0.6f, height * Random.Range(0.55f, 0.8f), 8, i * 13.1f + 90f);
            var shoulderRenderer = shoulder.AddComponent<MeshRenderer>();
            shoulderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shoulderRenderer.sharedMaterial = GetRockMaterial();

            // Snow cap: a small white cone nested at the very top of the main peak.
            float snowHeight = height * Random.Range(0.22f, 0.32f);
            GameObject snow = new GameObject("SnowCap");
            snow.transform.SetParent(mountain.transform);
            snow.transform.localPosition = new Vector3(0f, height - snowHeight * 1.6f, 0f);
            var snowFilter = snow.AddComponent<MeshFilter>();
            snowFilter.sharedMesh = BuildJaggedPeakMesh(baseRadius * 0.42f, snowHeight * 2f, 8, i * 13.1f + 200f);
            var snowRenderer = snow.AddComponent<MeshRenderer>();
            snowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            snowRenderer.sharedMaterial = GetSnowCapMaterial();
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
    const float MinHorizontalGap = 1.8f;
    const float MaxHorizontalGap = 4.5f;
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

    static Vector3 CreateTower()
    {
        Vector3 prevPos = new Vector3(0f, 0.5f, 0f);
        Vector3 lastPos = prevPos;
        float angle = 0f;
        bool[] gatesPlaced = new bool[GateThresholds.Length];

        for (int i = 0; i < PlatformCount; i++)
        {
            float t = i / (float)(PlatformCount - 1);
            bool earlySection = i < 4;
            int block = i / 8;
            bool isCheckpoint = i > 0 && i % CheckpointInterval == 0;

            bool isRestStop = !earlySection && !isCheckpoint && i % CheckpointInterval == CheckpointInterval - 1;

            // Flavors: 0 static(+hazard), 1 crumbling, 2 moving, 3 swinging, 4 floating, 5 timed, 6 bouncepad(+hazard).
            int flavor = Mathf.FloorToInt(HashFloat(block) * 7f) % 7;
            if (isCheckpoint || isRestStop)
                flavor = -1;

            bool isMovingType = flavor == 2;
            bool isSwingingType = flavor == 3;
            bool isFloatingType = flavor == 4;
            float dynamicPenalty = (isMovingType || isSwingingType) ? 0.7f : (isFloatingType ? 0.85f : 1f);

            float stepHeight = earlySection
                ? Mathf.Lerp(1.0f, MinVerticalGap + 0.3f, i / 3f)
                : Mathf.Lerp(MinVerticalGap + 0.3f, MaxVerticalGap, t) * dynamicPenalty;

            float horizontalGap = earlySection
                ? Mathf.Lerp(1.8f, MinHorizontalGap + 0.6f, i / 3f)
                : Mathf.Lerp(MinHorizontalGap + 0.6f, MaxHorizontalGap, t) * dynamicPenalty;

            // Never generate a vertical gap a single jump can't reach at that horizontal distance.
            float safeMaxHeight = Mathf.Max(0.5f, MaxSingleJumpHeightAt(horizontalGap) * JumpSafetyMargin);
            stepHeight = Mathf.Min(stepHeight, safeMaxHeight);

            // Path shape varies in phases instead of one continuous spiral: long straight-up
            // stretches, regular spiral stretches, and faster loop-around stretches. Only the
            // turning rate changes here - stepHeight/horizontalGap (and the jump-safety clamp
            // above) are untouched, so this never affects fairness, only the route's shape.
            if (earlySection)
            {
                angle += 0.4f;
            }
            else
            {
                const int phaseLength = 24;
                int phase = (i / phaseLength) % 3;
                float angleIncrement;
                switch (phase)
                {
                    case 0: angleIncrement = 0.45f + t * 0.2f; break; // spiral
                    case 1: angleIncrement = 0.06f; break; // mostly straight up
                    default: angleIncrement = 0.95f + t * 0.25f; break; // loop around
                }
                angle += angleIncrement;
            }
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 pos = prevPos + dir * horizontalGap;
            pos.y = prevPos.y + stepHeight;
            Vector3 perpSide = new Vector3(dir.z, 0f, -dir.x) * (i % 2 == 0 ? 1f : -1f);

            int shapeType = i % 4;
            float sizeMultiplier = Mathf.Lerp(1f, 0.6f, t);

            bool isPrecision = flavor == 0 && !earlySection && !isRestStop && i % 9 == 5;
            if (isRestStop)
                sizeMultiplier *= 1.6f;
            else if (isPrecision)
                sizeMultiplier *= 0.65f;

            GameObject platform = BuildPlatformShape(shapeType, i, sizeMultiplier, t);
            platform.transform.position = pos;

            if (isCheckpoint)
            {
                CreateCheckpointMarker(pos, i);
            }
            else if (isRestStop)
            {
                // Safe, oversized breather platform before the next checkpoint - no hazards, no dynamics.
            }
            else
            {
                switch (flavor)
                {
                    case 0:
                        if (!earlySection && i % 6 == 2)
                            CreateHazard(pos + perpSide * (0.55f * sizeMultiplier) + new Vector3(0f, 0.9f, 0f), i);
                        else if (!earlySection && i % 15 == 7)
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
                            mover.speed = 0.8f + t * 0.5f;
                        }
                        break;
                    case 3:
                        if (!earlySection)
                        {
                            var swing = platform.AddComponent<SwingingPlatform>();
                            swing.armLength = 2f + (i % 3) * 0.5f;
                            swing.swingAngleDeg = 30f + t * 15f;
                            swing.speed = 0.9f + t * 0.3f;
                        }
                        break;
                    case 4:
                        if (!earlySection)
                        {
                            var floater = platform.AddComponent<FloatingPlatform>();
                            floater.amplitude = 0.35f + (i % 3) * 0.1f;
                            floater.speed = 1f + t * 0.4f;
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
                        if (!earlySection && i % 4 == 0)
                            CreateBouncePad(pos + new Vector3(0f, 0.35f, 0f), i);
                        if (!earlySection && i % 7 == 4)
                            CreateHazard(pos + perpSide * (0.55f * sizeMultiplier) + new Vector3(0f, 0.9f, 0f), i);
                        break;
                }
            }

            if (i % 2 == 0 && !isCheckpoint)
                CreateGem(pos + new Vector3(0f, 1.1f, 0f), i);

            if (i % 3 == 1 && !isCheckpoint)
                CreateClimbDecor(pos, i, t);

            for (int g = 0; g < GateThresholds.Length; g++)
            {
                if (!gatesPlaced[g] && t >= GateThresholds[g])
                {
                    CreateWorldGate(pos, GameManager.GetWorldName(t));
                    gatesPlaced[g] = true;
                    break;
                }
            }

            prevPos = pos;
            lastPos = pos;
        }

        actualTopHeight = lastPos.y;
        Debug.Log("SceneBuilder: tower reaches height " + actualTopHeight.ToString("0"));
        return lastPos;
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

    static void CreateCheckpointMarker(Vector3 pos, int index)
    {
        GameObject marker = InstantiateKenney("sign", pos + new Vector3(0f, 0.7f, 0f));
        marker.name = "Checkpoint_" + index;
        marker.transform.localScale = Vector3.one * 1.3f;

        BoxCollider trigger = marker.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2f, 3f, 2f);
        marker.AddComponent<Checkpoint>();
    }

    static void CreateGoalFlag(Vector3 topPlatformPos)
    {
        GameObject flag = InstantiateKenney("flag", topPlatformPos + new Vector3(0f, 0.9f, 0f));
        flag.name = "GoalFlag";
        flag.transform.localScale = Vector3.one * 1.5f;
        flag.AddComponent<WindSway>();

        BoxCollider trigger = flag.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2.5f, 3f, 2.5f);
        flag.AddComponent<GoalTrigger>();
    }

    static void CreateWinScreen()
    {
        var go = new GameObject("WinScreen");
        go.AddComponent<WinScreen>();
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

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        renderer.material = mat;

        ps.Stop();
        return ps;
    }

    static void CreateMusicManager()
    {
        var go = new GameObject("MusicManager");
        var music = go.AddComponent<MusicManager>();
        music.topHeight = actualTopHeight;
        music.lowZoneClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "Music/music_low.mp3");
        music.midZoneClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "Music/music_mid.mp3");
        music.highZoneClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "Music/music_high.mp3");
    }

    static readonly string[] GrassZonePlatforms = { "crate-strong", "crate-item-strong", "platform" };
    static readonly string[] VolcanoZonePlatforms = { "platform-fortified", "brick", "platform" };
    static readonly string[] CloudZonePlatforms = { "platform-overhang", "platform-ramp" };
    static readonly string[] IceZonePlatforms = { "platform-fortified", "platform-overhang", "brick" };
    static readonly string[] FinalZonePlatforms = { "platform-fortified", "platform-overhang", "platform-ramp" };

    static string[] GetZonePlatformPool(float t)
    {
        if (t < 0.2f) return GrassZonePlatforms;
        if (t < 0.4f) return VolcanoZonePlatforms;
        if (t < 0.6f) return CloudZonePlatforms;
        if (t < 0.8f) return IceZonePlatforms;
        return FinalZonePlatforms;
    }

    // Colorful Kenney platform props (shared candy-colored texture atlas) instead of plain
    // concrete - keeps the climb feeling cheerful rather than grey and monolithic.
    static GameObject BuildPlatformShape(int shapeType, int index, float sizeMultiplier, float t)
    {
        string[] pool = GetZonePlatformPool(t);
        string modelName = pool[shapeType % pool.Length];

        GameObject platform = InstantiateKenney(modelName, Vector3.zero);
        platform.transform.localScale = Vector3.one * sizeMultiplier * 1.5f;

        platform.name = "Platform_" + index;
        AddSolidCollider(platform);
        return platform;
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

    static void CreateGem(Vector3 pos, int index)
    {
        string modelName = GemModels[index % GemModels.Length];
        GameObject gem = InstantiateKenney(modelName, pos);
        gem.name = "Gem_" + index;
        gem.transform.localScale = Vector3.one * 1.2f;
        var col = AddSolidCollider(gem);
        col.isTrigger = true;
        gem.AddComponent<Coin>();
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
            // Wiesenland - warm grassy daylight, pure day sky. skyTint multiplies the cubemap
            // (0.5,0.5,0.5 = neutral/unchanged), skyExposure brightens/dims it overall.
            new ZoneAtmosphere.Zone
            {
                height = 0f,
                fogColor = new Color(0.65f, 0.72f, 0.58f),
                skyTint = new Color(0.52f, 0.5f, 0.46f),
                skyExposure = 1f,
                skyBlend = 0f,
                lightColor = new Color(1f, 0.95f, 0.82f),
                lightIntensity = 1.2f,
                particleColor = new Color(1f, 1f, 1f, 0f),
                particleRate = 0f,
            },
            // Vulkanfeld - hazy orange, drifting embers, still mostly day sky
            new ZoneAtmosphere.Zone
            {
                height = actualTopHeight * 0.25f,
                fogColor = new Color(0.5f, 0.22f, 0.12f),
                skyTint = new Color(0.62f, 0.32f, 0.2f),
                skyExposure = 0.85f,
                skyBlend = 0.05f,
                lightColor = new Color(1f, 0.55f, 0.3f),
                lightIntensity = 1.35f,
                particleColor = new Color(1f, 0.5f, 0.15f, 0.9f),
                particleRate = 16f,
            },
            // Wolkenreich - bright misty blue-white, day sky still dominant
            new ZoneAtmosphere.Zone
            {
                height = actualTopHeight * 0.5f,
                fogColor = new Color(0.88f, 0.92f, 0.98f),
                skyTint = new Color(0.52f, 0.56f, 0.64f),
                skyExposure = 1.2f,
                skyBlend = 0.15f,
                lightColor = new Color(1f, 1f, 0.98f),
                lightIntensity = 1.35f,
                particleColor = new Color(1f, 1f, 1f, 0.6f),
                particleRate = 10f,
            },
            // Eiskristall - pale cyan, falling snow, sky well into the day->night transition
            new ZoneAtmosphere.Zone
            {
                height = actualTopHeight * 0.75f,
                fogColor = new Color(0.78f, 0.9f, 0.95f),
                skyTint = new Color(0.48f, 0.56f, 0.6f),
                skyExposure = 1f,
                skyBlend = 0.55f,
                lightColor = new Color(0.85f, 0.93f, 1f),
                lightIntensity = 1.1f,
                particleColor = new Color(0.9f, 0.97f, 1f, 0.85f),
                particleRate = 24f,
            },
            // Sternenkrone - deep space, stardust. Full night cubemap (dark, pink-tinted nebula)
            // instead of just darkening the day sky - the star particle field layers on top.
            new ZoneAtmosphere.Zone
            {
                height = actualTopHeight,
                fogColor = new Color(0.02f, 0.02f, 0.08f),
                skyTint = new Color(0.5f, 0.5f, 0.5f),
                skyExposure = 0.9f,
                skyBlend = 1f,
                lightColor = new Color(0.7f, 0.75f, 1f),
                lightIntensity = 0.6f,
                particleColor = new Color(0.7f, 0.6f, 1f, 0.7f),
                particleRate = 6f,
            },
        };
    }
}
