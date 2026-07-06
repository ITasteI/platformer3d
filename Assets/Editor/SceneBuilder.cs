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
    const int PlatformCount = 160;
    const float TopHeight = 500f;
    const string KitPath = "Assets/KenneyKit/";
    const string CityKitPath = "Assets/CityKit/";

    static Material kenneyMaterial;
    static Material cityMaterial;

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
        AssetDatabase.CreateAsset(cache, assetPath);
        return cache;
    }

    static Material GetKenneyMaterial()
    {
        return LoadOrCreateMaterial(ref kenneyMaterial, "Assets/KenneyMaterial.mat", KitPath + "Textures/colormap.png", 0.15f);
    }

    static Material GetCityMaterial()
    {
        return LoadOrCreateMaterial(ref cityMaterial, "Assets/CityMaterial.mat", CityKitPath + "Textures/colormap.png", 0.1f);
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

    static GameObject InstantiateCityProp(string modelName, Vector3 pos)
    {
        return InstantiateModel(CityKitPath, GetCityMaterial(), modelName, pos);
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

        GraphicsSettings.defaultRenderPipeline = urpAsset;
        QualitySettings.renderPipeline = urpAsset;
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
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        SetupRenderPipeline();

        Light sunLight = CreateLight();
        Camera lobbyCam = CreateLobbyCamera();
        SetupPostProcessing(lobbyCam);
        GameObject playerPrefab = CreatePlayerPrefab();
        CreateNetworkManagerAndLobby(playerPrefab, lobbyCam);
        CreateGround();
        CreateJunkyardDecor();
        CreateCityBackground();
        CreateClouds();
        Vector3 topPos = CreateTower();
        CreateGoalFlag(topPos);
        ParticleSystem stars = CreateStarField();
        CreateGameManager();
        CreateSettingsMenu();
        CreateAtmosphereManager(sunLight, stars);

        string scenesDir = Path.Combine(Application.dataPath, "Scenes");
        Directory.CreateDirectory(scenesDir);
        string scenePath = "Assets/Scenes/MainScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder: Only-Up-style tower built at " + scenePath);
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

        Material sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetColor("_SkyTint", new Color(0.6f, 0.5f, 0.45f));
        sky.SetColor("_GroundColor", new Color(0.35f, 0.3f, 0.28f));
        sky.SetFloat("_AtmosphereThickness", 1.0f);
        RenderSettings.skybox = sky;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.6f, 0.65f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.48f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.6f, 0.55f, 0.5f);
        RenderSettings.fogStartDistance = 20f;
        RenderSettings.fogEndDistance = 80f;

        return light;
    }

    static GameObject CreatePlayerPrefab()
    {
        GameObject root = new GameObject("Player");
        root.tag = "Player";
        root.transform.position = new Vector3(0f, 1f, 0f);

        root.AddComponent<NetworkObject>();
        var netTransform = root.AddComponent<NetworkTransform>();
        netTransform.SyncPositionX = netTransform.SyncPositionY = netTransform.SyncPositionZ = true;
        netTransform.SyncRotAngleX = netTransform.SyncRotAngleY = netTransform.SyncRotAngleZ = false;

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

        const string prefabPath = "Assets/PlayerPrefab.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        return prefab;
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

    static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(2f, 1f, 2f);
        SetColor(ground, new Color(0.4f, 0.38f, 0.35f));
    }

    static readonly string[] JunkyardProps = { "crate", "crate-strong", "barrel", "tree-pine", "tree", "rocks" };

    static void CreateJunkyardDecor()
    {
        GameObject decor = new GameObject("JunkyardDecor");
        for (int i = 0; i < 14; i++)
        {
            float angle = i * 0.7f;
            float radius = 6f + (i % 3) * 1.8f;
            Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);

            string propName = JunkyardProps[i % JunkyardProps.Length];
            GameObject prop = InstantiateKenney(propName, pos);
            prop.name = "JunkProp_" + i;
            prop.transform.SetParent(decor.transform);
            prop.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
        }
    }

    static readonly string[] SkyscraperNames =
    {
        "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c",
        "building-skyscraper-d", "building-skyscraper-e",
    };

    static readonly string[] BuildingNames =
    {
        "building-a", "building-b", "building-c", "building-d", "building-e", "building-f", "building-g",
        "building-h", "building-i", "building-j", "building-k", "building-l", "building-m", "building-n",
    };

    static readonly string[] LowDetailNames =
    {
        "low-detail-building-a", "low-detail-building-b", "low-detail-building-c", "low-detail-building-d",
        "low-detail-building-e", "low-detail-building-f", "low-detail-building-g", "low-detail-building-h",
        "low-detail-building-i", "low-detail-building-j", "low-detail-building-k",
    };

    static void CreateCityBackground()
    {
        GameObject cityRoot = new GameObject("CitySkyline");

        CreateBuildingRing(cityRoot.transform, SkyscraperNames, 16, 24f, 34f, 1.2f, 2.0f);
        CreateBuildingRing(cityRoot.transform, BuildingNames, 24, 36f, 48f, 0.8f, 1.3f);
        CreateBuildingRing(cityRoot.transform, LowDetailNames, 28, 50f, 70f, 0.9f, 1.6f);
    }

    static void CreateBuildingRing(Transform parent, string[] names, int count, float radiusMin, float radiusMax, float scaleMin, float scaleMax)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * 360f * Mathf.Deg2Rad + i * 0.37f;
            float radius = Mathf.Lerp(radiusMin, radiusMax, (i % 3) / 2f);
            Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);

            string modelName = names[i % names.Length];
            GameObject building = InstantiateCityProp(modelName, pos);
            building.transform.SetParent(parent);
            building.transform.rotation = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);

            float scale = Mathf.Lerp(scaleMin, scaleMax, Mathf.Abs(Mathf.Sin(i * 12.9898f)));
            building.transform.localScale = Vector3.one * scale;
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
            float height = Mathf.Lerp(TopHeight * 0.3f, TopHeight * 0.65f, (i % 7) / 6f);
            Vector3 center = new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);

            GameObject cloud = new GameObject("Cloud_" + i);
            cloud.transform.SetParent(cloudRoot.transform);
            cloud.transform.position = center;

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

    static Vector3 CreateTower()
    {
        float cumulativeHeight = 0.5f;
        Vector3 lastPos = Vector3.zero;

        for (int i = 0; i < PlatformCount; i++)
        {
            float t = i / (float)(PlatformCount - 1);

            float stepHeight = i < 4 ? Mathf.Lerp(1.0f, 1.8f, i / 3f) : Mathf.Lerp(1.8f, 4.2f, t);
            cumulativeHeight += stepHeight;

            float angle = i * (0.5f + t * 0.15f);
            float radius = i < 4 ? Mathf.Lerp(1.8f, 3.2f, i / 3f) : Mathf.Lerp(3.2f, 4.6f, t) + (i % 5) * 0.5f;
            float jitter = i < 4 ? 0f : Mathf.Sin(i * 12.9898f) * 0.3f;
            Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, cumulativeHeight + jitter, Mathf.Cos(angle) * radius);

            int shapeType = i % 3;
            float sizeMultiplier = Mathf.Lerp(1f, 0.55f, t);

            GameObject platform = BuildPlatformShape(shapeType, i, sizeMultiplier);
            platform.transform.position = pos;

            int block = i / 8;
            int flavor = block % 5;
            bool earlySection = i < 3;

            switch (flavor)
            {
                case 0:
                    if (!earlySection && i % 6 == 2)
                        CreateHazard(pos + new Vector3(0f, 0.9f, 0f), i);
                    break;
                case 1:
                    if (!earlySection)
                        platform.AddComponent<CrumblingPlatform>();
                    break;
                case 2:
                    if (!earlySection)
                    {
                        var mover = platform.AddComponent<MovingPlatform>();
                        mover.moveOffset = (i % 2 == 0) ? new Vector3(3f + t * 2f, 0f, 0f) : new Vector3(0f, 0f, 3f + t * 2f);
                        mover.speed = 0.8f + t * 0.6f;
                    }
                    break;
                case 3:
                    if (!earlySection)
                    {
                        var swing = platform.AddComponent<SwingingPlatform>();
                        swing.armLength = 2.5f + (i % 3);
                        swing.swingAngleDeg = 35f + t * 20f;
                        swing.speed = 0.9f + t * 0.4f;
                    }
                    break;
                default:
                    if (!earlySection && i % 4 == 0)
                        CreateBouncePad(pos + new Vector3(0f, 0.35f, 0f), i);
                    if (!earlySection && i % 7 == 4)
                        CreateHazard(pos + new Vector3(0f, 0.9f, 0f), i);
                    break;
            }

            if (i % 2 == 0)
                CreateGem(pos + new Vector3(0f, 1.1f, 0f), i);

            lastPos = pos;
        }

        return lastPos;
    }

    static void CreateGoalFlag(Vector3 topPlatformPos)
    {
        GameObject flag = InstantiateKenney("flag", topPlatformPos + new Vector3(0f, 0.9f, 0f));
        flag.name = "GoalFlag";
        flag.transform.localScale = Vector3.one * 1.5f;
    }

    static GameObject BuildPlatformShape(int shapeType, int index, float sizeMultiplier)
    {
        GameObject platform;
        switch (shapeType)
        {
            case 0:
                platform = InstantiateKenney("crate-strong", Vector3.zero);
                platform.transform.localScale = Vector3.one * sizeMultiplier * 1.6f;
                break;
            case 1:
                platform = InstantiateKenney("pipe", Vector3.zero);
                platform.transform.localScale = Vector3.one * sizeMultiplier * 1.4f;
                platform.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                break;
            default:
                platform = InstantiateKenney("platform-fortified", Vector3.zero);
                platform.transform.localScale = Vector3.one * sizeMultiplier * 1.4f;
                break;
        }
        platform.name = "Platform_" + index;
        AddSolidCollider(platform);
        return platform;
    }

    static void CreateHazard(Vector3 pos, int index)
    {
        GameObject hazard = InstantiateKenney("saw", pos);
        hazard.name = "Hazard_" + index;
        hazard.transform.localScale = Vector3.one * 1.1f;
        var col = AddSolidCollider(hazard);
        col.isTrigger = true;
        hazard.AddComponent<Hazard>();
    }

    static void CreateGem(Vector3 pos, int index)
    {
        GameObject gem = InstantiateKenney("coin-gold", pos);
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
        starsGO.transform.position = new Vector3(0f, TopHeight - 10f, 0f);
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

    static GameObject CreateGameManager()
    {
        var gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        return gm;
    }

    static void CreateSettingsMenu()
    {
        var go = new GameObject("SettingsMenu");
        go.AddComponent<SettingsMenu>();
    }

    static void CreateAtmosphereManager(Light sunLight, ParticleSystem stars)
    {
        var go = new GameObject("AtmosphereManager");
        var atmosphere = go.AddComponent<ZoneAtmosphere>();
        atmosphere.sunLight = sunLight;
        atmosphere.stars = stars;

        atmosphere.zones = new[]
        {
            new ZoneAtmosphere.Zone
            {
                height = 0f,
                fogColor = new Color(0.6f, 0.55f, 0.5f),
                skyTint = new Color(0.6f, 0.5f, 0.45f),
                lightColor = new Color(1f, 0.93f, 0.8f),
                lightIntensity = 1.2f,
            },
            new ZoneAtmosphere.Zone
            {
                height = TopHeight * 0.45f,
                fogColor = new Color(0.85f, 0.9f, 0.97f),
                skyTint = new Color(0.55f, 0.75f, 1f),
                lightColor = new Color(1f, 1f, 0.98f),
                lightIntensity = 1.3f,
            },
            new ZoneAtmosphere.Zone
            {
                height = TopHeight,
                fogColor = new Color(0.02f, 0.02f, 0.08f),
                skyTint = new Color(0.02f, 0.02f, 0.1f),
                lightColor = new Color(0.7f, 0.75f, 1f),
                lightIntensity = 0.6f,
            },
        };
    }
}
