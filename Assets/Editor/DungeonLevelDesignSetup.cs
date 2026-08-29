#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ZZ.EditorTools
{
    /// <summary>Builds the EP99–100 Ashen Crypt graybox, lighting, and navigation.</summary>
    public static class DungeonLevelDesignSetup
    {
        private const string k_ScenePath = WorldScenePathLayout.MasterScenePath;
        private const string k_RootName = "World";
        private const string k_LocationName = "Location 01 - Ashen Crypt";
        private const string k_MaterialFolder = "Assets/Data/Materials/Level Design";
        private const string k_SettingsFolder = "Assets/Data/Settings/Level Design";
        private const string k_VolumeProfilePath =
            k_SettingsFolder + "/EP99-100 Ashen Crypt Volume.asset";
        private const string k_LightingSettingsPath =
            k_SettingsFolder + "/EP99-100 World Lighting Settings.lighting";

        private static readonly StaticEditorFlags s_environmentStaticFlags =
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.NavigationStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.ReflectionProbeStatic;

        /// <summary>Creates or replaces only the owned EP99–100 dungeon hierarchy.</summary>
        [MenuItem("Tools/ZZ/EP99-100/Build Ashen Crypt Dungeon")]
        public static void BuildDungeonLevel()
        {
            EnsureEditMode();
            EnsureFolder(k_MaterialFolder);
            EnsureFolder(k_SettingsFolder);
            Scene scene = OpenWorldScene();
            Material floorMaterial = GetOrCreateMaterial(
                "M_Graybox_Floor",
                new Color(0.19f, 0.21f, 0.23f));
            Material wallMaterial = GetOrCreateMaterial(
                "M_Graybox_Wall",
                new Color(0.27f, 0.28f, 0.30f));
            Material damagedWallMaterial = GetOrCreateMaterial(
                "M_Graybox_DamagedWall",
                new Color(0.18f, 0.17f, 0.16f));
            Material routeMaterial = GetOrCreateMaterial(
                "M_Graybox_RouteMarker",
                new Color(0.14f, 0.32f, 0.43f));
            Material gateMaterial = GetOrCreateMaterial(
                "M_Graybox_Gate",
                new Color(0.18f, 0.11f, 0.055f),
                0.75f);
            Material bossMaterial = GetOrCreateMaterial(
                "M_Graybox_Boss",
                new Color(0.32f, 0.055f, 0.045f));
            Material rewardMaterial = GetOrCreateMaterial(
                "M_Graybox_Reward",
                new Color(0.9f, 0.58f, 0.12f),
                1.5f);

            GameObject world = GetOrCreateRoot(scene, k_RootName);
            Transform existingLocation = world.transform.Find(k_LocationName);
            if (existingLocation != null)
            {
                UnityEngine.Object.DestroyImmediate(existingLocation.gameObject);
            }

            GameObject location = new(k_LocationName);
            SceneManager.MoveGameObjectToScene(location, scene);
            location.transform.SetParent(world.transform, false);

            BuildEntry(
                location.transform,
                floorMaterial,
                wallMaterial,
                routeMaterial);
            BuildUpperPath(
                location.transform,
                floorMaterial,
                wallMaterial,
                damagedWallMaterial);
            BuildLowerPath(
                location.transform,
                floorMaterial,
                wallMaterial,
                damagedWallMaterial,
                gateMaterial);
            BuildConvergenceAndShortcut(
                location.transform,
                floorMaterial,
                wallMaterial,
                damagedWallMaterial,
                gateMaterial);
            BuildRewardWing(
                location.transform,
                floorMaterial,
                wallMaterial,
                gateMaterial,
                rewardMaterial);
            BuildBossRoom(
                location.transform,
                floorMaterial,
                wallMaterial,
                damagedWallMaterial,
                bossMaterial);
            BuildLightingAndAtmosphere(location.transform);
            ConfigureSpawnAndGrace(scene);
            ConfigureNavigation(scene);
            ConfigureLightingSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("EP99-100 Ashen Crypt dungeon setup completed.");
        }

        /// <summary>Bakes navigation after the generated dungeon scene has been saved.</summary>
        [MenuItem("Tools/ZZ/EP99-100/Bake Dungeon Navigation")]
        public static void BakeDungeonNavigation()
        {
            EnsureEditMode();
            Scene scene = OpenWorldScene();
            NavMeshSurface surface = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true))
                .FirstOrDefault();
            if (surface == null)
            {
                throw new InvalidOperationException(
                    $"{WorldScenePathLayout.MasterSceneName} has no NavMeshSurface.");
            }

            surface.BuildNavMesh();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("EP99-100 dungeon NavMesh bake completed.");
        }

        /// <summary>Bakes the configured Shadowmask lighting for the generated dungeon.</summary>
        [MenuItem("Tools/ZZ/EP99-100/Bake Dungeon Lighting")]
        public static void BakeDungeonLighting()
        {
            EnsureEditMode();
            Scene scene = OpenWorldScene();
            ConfigureLightingSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (Lightmapping.isRunning)
            {
                throw new InvalidOperationException(
                    "A lighting bake is already running. Wait for it to finish first.");
            }

            Lightmapping.bakeCompleted -= OnDungeonLightingBakeCompleted;
            Lightmapping.bakeCompleted += OnDungeonLightingBakeCompleted;
            if (!Lightmapping.BakeAsync())
            {
                Lightmapping.bakeCompleted -= OnDungeonLightingBakeCompleted;
                throw new InvalidOperationException("EP99-100 dungeon lighting bake failed.");
            }

            Debug.Log("EP99-100 dungeon Shadowmask lighting bake started.");
        }

        private static void BuildEntry(
            Transform location,
            Material floorMaterial,
            Material wallMaterial,
            Material routeMaterial)
        {
            Zone zone = CreateZone(location, "Sub Location 00 - Grace and Entry");
            CreateBlock(
                zone.Floors,
                "Dungeon_Floor_Entry_5x5_A",
                new Vector3(24f, -0.25f, 0f),
                new Vector3(10f, 0.5f, 10f),
                floorMaterial);
            CreateBlock(
                zone.Floors,
                "Dungeon_Floor_PathA_Marker",
                new Vector3(27f, -0.18f, 7.5f),
                new Vector3(5f, 0.15f, 7f),
                routeMaterial,
                false);
            CreateBlock(
                zone.Floors,
                "Dungeon_Floor_PathB_Marker",
                new Vector3(27f, -0.18f, -7.5f),
                new Vector3(5f, 0.15f, 7f),
                routeMaterial,
                false);
            CreateWall(zone.Walls, "Entry_Wall_North", new Vector3(21f, 2.5f, 5f),
                new Vector3(6f, 5f, 0.5f), wallMaterial);
            CreateWall(zone.Walls, "Entry_Wall_South", new Vector3(21f, 2.5f, -5f),
                new Vector3(6f, 5f, 0.5f), wallMaterial);
            CreateWall(zone.Walls, "Entry_Wall_West", new Vector3(19f, 2.5f, 0f),
                new Vector3(0.5f, 5f, 10f), wallMaterial);
            CreatePillar(zone.Props, new Vector3(19.8f, 2f, 4.3f), wallMaterial, 0);
            CreatePillar(zone.Props, new Vector3(19.8f, 2f, -4.3f), wallMaterial, 1);
            CreateTorch(zone.Props, new Vector3(20f, 2.2f, 0f), Vector3.right);
        }

        private static void BuildUpperPath(
            Transform location,
            Material floorMaterial,
            Material wallMaterial,
            Material damagedWallMaterial)
        {
            Zone zone = CreateZone(location, "Sub Location 01 - Upper Path A");
            CreateBlock(zone.Floors, "PathA_Approach_Floor", new Vector3(27f, -0.25f, 10f),
                new Vector3(5f, 0.5f, 10f), floorMaterial);
            CreateStairs(zone.Floors, "PathA_Stair", new Vector3(29f, 0f, 14f),
                Vector3.right, true, floorMaterial);
            CreateBlock(zone.Floors, "PathA_UpperRoom_Floor", new Vector3(42f, 3.75f, 14f),
                new Vector3(16f, 0.5f, 14f), floorMaterial);
            CreateBlock(zone.Floors, "PathA_UpperTunnel_Floor", new Vector3(55f, 3.75f, 14f),
                new Vector3(12f, 0.5f, 5f), floorMaterial);
            CreateStairs(zone.Floors, "PathA_Descent", new Vector3(61f, 4f, 14f),
                new Vector3(0.7f, 0f, -0.7f), false, floorMaterial);
            CreateWall(zone.Walls, "PathA_Room_North_01", new Vector3(38f, 6.5f, 21f),
                new Vector3(8f, 5f, 0.5f), wallMaterial);
            CreateWall(zone.Walls, "PathA_Room_North_02", new Vector3(47f, 6.5f, 21f),
                new Vector3(6f, 5f, 0.5f), damagedWallMaterial);
            CreateWall(zone.Walls, "PathA_Room_South", new Vector3(42f, 6.5f, 7f),
                new Vector3(16f, 5f, 0.5f), wallMaterial);
            CreateWall(zone.Walls, "PathA_Tunnel_North", new Vector3(55f, 6.5f, 16.5f),
                new Vector3(12f, 5f, 0.5f), damagedWallMaterial);
            CreateWall(zone.Walls, "PathA_Tunnel_South", new Vector3(55f, 6.5f, 11.5f),
                new Vector3(12f, 5f, 0.5f), wallMaterial);
            CreateCeiling(zone.Walls, "PathA_UpperRoom_Ceiling", new Vector3(42f, 9f, 14f),
                new Vector3(16f, 0.4f, 14f), wallMaterial);
            CreateRubble(zone.Props, new Vector3(46f, 4.2f, 18f), damagedWallMaterial, 13);
            CreateTorch(zone.Props, new Vector3(38f, 5.8f, 20.5f), Vector3.back);
        }

        private static void BuildLowerPath(
            Transform location,
            Material floorMaterial,
            Material wallMaterial,
            Material damagedWallMaterial,
            Material gateMaterial)
        {
            Zone zone = CreateZone(location, "Sub Location 02 - Lower Path B");
            CreateStairs(zone.Floors, "PathB_Descent", new Vector3(28f, 0f, -9f),
                new Vector3(0.7f, 0f, -0.7f), false, floorMaterial);
            CreateBlock(zone.Floors, "PathB_LowerRoom_Floor", new Vector3(42f, -3.25f, -14f),
                new Vector3(16f, 0.5f, 14f), floorMaterial);
            CreateBlock(zone.Floors, "PathB_LowerTunnel_Floor", new Vector3(55f, -3.25f, -14f),
                new Vector3(12f, 0.5f, 5f), floorMaterial);
            CreateStairs(zone.Floors, "PathB_Ascent", new Vector3(61f, -3f, -14f),
                new Vector3(0.7f, 0f, 0.7f), true, floorMaterial);
            CreateWall(zone.Walls, "PathB_Room_North", new Vector3(42f, -0.5f, -7f),
                new Vector3(16f, 5f, 0.5f), damagedWallMaterial);
            CreateWall(zone.Walls, "PathB_Room_South_01", new Vector3(38f, -0.5f, -21f),
                new Vector3(8f, 5f, 0.5f), wallMaterial);
            CreateWall(zone.Walls, "PathB_Room_South_02", new Vector3(47f, -0.5f, -21f),
                new Vector3(6f, 5f, 0.5f), damagedWallMaterial);
            CreateWall(zone.Walls, "PathB_Tunnel_North", new Vector3(55f, -0.5f, -11.5f),
                new Vector3(12f, 5f, 0.5f), wallMaterial);
            CreateWall(zone.Walls, "PathB_Tunnel_South", new Vector3(55f, -0.5f, -16.5f),
                new Vector3(12f, 5f, 0.5f), damagedWallMaterial);
            CreateCeiling(zone.Walls, "PathB_LowerRoom_Ceiling", new Vector3(42f, 2f, -14f),
                new Vector3(16f, 0.4f, 14f), wallMaterial);
            CreateOneWayGate(
                zone.Props,
                "Locked From Other Side Gate",
                new Vector3(66f, 0f, -8f),
                Quaternion.identity,
                new Vector3(5f, 4.5f, 0.5f),
                gateMaterial,
                true);
            CreateRubble(zone.Props, new Vector3(39f, -2.8f, -18f), damagedWallMaterial, 27);
            CreateTorch(zone.Props, new Vector3(47f, -1.2f, -20.5f), Vector3.forward);
        }

        private static void BuildConvergenceAndShortcut(
            Transform location,
            Material floorMaterial,
            Material wallMaterial,
            Material damagedWallMaterial,
            Material gateMaterial)
        {
            Zone zone = CreateZone(location, "Sub Location 03 - Convergence and Shortcut");
            CreateBlock(zone.Floors, "MainArea_Floor", new Vector3(70f, -0.25f, 0f),
                new Vector3(18f, 0.5f, 18f), floorMaterial);
            CreateBlock(zone.Floors, "Shortcut_Floor", new Vector3(47f, -0.25f, 0f),
                new Vector3(38f, 0.5f, 5f), floorMaterial);
            CreateWall(zone.Walls, "Shortcut_Wall_North", new Vector3(47f, 2.5f, 2.5f),
                new Vector3(38f, 5f, 0.5f), damagedWallMaterial);
            CreateWall(zone.Walls, "Shortcut_Wall_South", new Vector3(47f, 2.5f, -2.5f),
                new Vector3(38f, 5f, 0.5f), wallMaterial);
            CreateCeiling(zone.Walls, "Shortcut_Ceiling", new Vector3(47f, 5f, 0f),
                new Vector3(38f, 0.4f, 5f), wallMaterial);
            CreateOneWayGate(
                zone.Props,
                "Grace Boss Shortcut Gate",
                new Vector3(31f, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector3(0.5f, 4.5f, 5f),
                gateMaterial,
                true);
            CreatePillar(zone.Props, new Vector3(63f, 2.5f, 7f), wallMaterial, 2);
            CreatePillar(zone.Props, new Vector3(77f, 2.5f, 7f), damagedWallMaterial, 3);
            CreatePillar(zone.Props, new Vector3(63f, 2.5f, -7f), damagedWallMaterial, 4);
            CreatePillar(zone.Props, new Vector3(77f, 2.5f, -7f), wallMaterial, 5);
            CreateTorch(zone.Props, new Vector3(32f, 2.2f, 2f), Vector3.back);
            CreateTorch(zone.Props, new Vector3(64f, 2.2f, 0f), Vector3.right);
        }

        private static void BuildRewardWing(
            Transform location,
            Material floorMaterial,
            Material wallMaterial,
            Material gateMaterial,
            Material rewardMaterial)
        {
            Zone zone = CreateZone(location, "Sub Location 04 - Visible Reward Wing");
            CreateBlock(zone.Floors, "RewardWing_Floor", new Vector3(70f, -0.25f, 17f),
                new Vector3(8f, 0.5f, 16f), floorMaterial);
            CreateWall(zone.Walls, "RewardWing_Wall_East", new Vector3(74f, 2.5f, 17f),
                new Vector3(0.5f, 5f, 16f), wallMaterial);
            CreateWall(zone.Walls, "RewardWing_Wall_West", new Vector3(66f, 2.5f, 17f),
                new Vector3(0.5f, 5f, 16f), wallMaterial);
            CreateIronBars(
                zone.Props,
                "Visible Reward Iron Gate",
                new Vector3(70f, 2f, 11f),
                new Vector3(8f, 4f, 0.4f),
                gateMaterial,
                true);
            CreateRewardPickup(zone.Props, new Vector3(70f, 0.45f, 22f), rewardMaterial);
            CreateBlock(zone.Floors, "Reward_Alternate_Route", new Vector3(59f, 3.75f, 21f),
                new Vector3(16f, 0.5f, 4f), floorMaterial);
            CreateStairs(zone.Floors, "Reward_Route_Descent", new Vector3(65f, 4f, 21f),
                Vector3.right, false, floorMaterial);
            CreateTorch(zone.Props, new Vector3(70f, 2f, 20f), Vector3.back);
        }

        private static void BuildBossRoom(
            Transform location,
            Material floorMaterial,
            Material wallMaterial,
            Material damagedWallMaterial,
            Material bossMaterial)
        {
            Zone zone = CreateZone(location, "Boss Room");
            CreateBlock(zone.Floors, "BossApproach_Floor", new Vector3(87f, -0.25f, 0f),
                new Vector3(16f, 0.5f, 6f), floorMaterial);
            CreateBlock(zone.Floors, "BossRoom_Floor", new Vector3(108f, -0.25f, 0f),
                new Vector3(26f, 0.5f, 26f), bossMaterial);
            CreateWall(zone.Walls, "BossRoom_North", new Vector3(108f, 3.5f, 13f),
                new Vector3(26f, 7f, 0.7f), damagedWallMaterial);
            CreateWall(zone.Walls, "BossRoom_South", new Vector3(108f, 3.5f, -13f),
                new Vector3(26f, 7f, 0.7f), wallMaterial);
            CreateWall(zone.Walls, "BossRoom_East", new Vector3(121f, 3.5f, 0f),
                new Vector3(0.7f, 7f, 26f), damagedWallMaterial);
            CreateWall(zone.Walls, "BossRoom_West_North", new Vector3(95f, 3.5f, 8f),
                new Vector3(0.7f, 7f, 10f), wallMaterial);
            CreateWall(zone.Walls, "BossRoom_West_South", new Vector3(95f, 3.5f, -8f),
                new Vector3(0.7f, 7f, 10f), wallMaterial);
            CreateCeiling(zone.Walls, "BossRoom_Ceiling", new Vector3(108f, 7f, 0f),
                new Vector3(26f, 0.5f, 26f), wallMaterial);
            CreatePillar(zone.Props, new Vector3(99f, 3f, 9f), wallMaterial, 6);
            CreatePillar(zone.Props, new Vector3(117f, 3f, 9f), damagedWallMaterial, 7);
            CreatePillar(zone.Props, new Vector3(99f, 3f, -9f), damagedWallMaterial, 8);
            CreatePillar(zone.Props, new Vector3(117f, 3f, -9f), wallMaterial, 9);
            CreateTorch(zone.Props, new Vector3(98f, 3f, 0f), Vector3.right);
            CreateTorch(zone.Props, new Vector3(118f, 3f, 0f), Vector3.left);
        }

        private static void BuildLightingAndAtmosphere(Transform location)
        {
            GameObject lighting = new("Lighting and Atmosphere");
            lighting.transform.SetParent(location, false);

            VolumeProfile profile = GetOrCreateVolumeProfile();
            GameObject volumeObject = new("Ashen Crypt Global Volume");
            volumeObject.transform.SetParent(lighting.transform, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            volume.sharedProfile = profile;
            volumeObject.AddComponent<DungeonPostProcessingController>();

            GameObject probesObject = new("Ashen Crypt Light Probes");
            probesObject.transform.SetParent(lighting.transform, false);
            LightProbeGroup probeGroup = probesObject.AddComponent<LightProbeGroup>();
            probeGroup.probePositions = CreateProbePositions().ToArray();

            GameObject dustObject = new("Ashen Crypt Dust");
            dustObject.transform.SetParent(lighting.transform, false);
            dustObject.transform.position = new Vector3(70f, 2f, 0f);
            ParticleSystem dust = dustObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = dust.main;
            main.loop = true;
            main.duration = 12f;
            main.startLifetime = 10f;
            main.startSpeed = 0.08f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.62f, 0.58f, 0.5f, 0.28f);
            main.maxParticles = 350;
            ParticleSystem.EmissionModule emission = dust.emission;
            emission.rateOverTime = 22f;
            ParticleSystem.ShapeModule shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(95f, 7f, 42f);
        }

        private static void ConfigureSpawnAndGrace(Scene scene)
        {
            GameObject spawnPoint = FindGameObject(scene, "Player Spawn Point");
            if (spawnPoint != null)
            {
                spawnPoint.transform.SetPositionAndRotation(
                    new Vector3(23f, 0.1f, 0f),
                    Quaternion.Euler(0f, 90f, 0f));
            }

            GameObject grace = FindGameObject(scene, "First Step Site of Grace");
            if (grace != null)
            {
                grace.transform.position = new Vector3(22f, 0f, 2.5f);
            }
        }

        private static void ConfigureNavigation(Scene scene)
        {
            GameObject navigation = FindGameObject(scene, "Navigation");
            if (navigation == null)
            {
                navigation = new GameObject("Navigation");
                SceneManager.MoveGameObjectToScene(navigation, scene);
            }

            NavMeshSurface surface = navigation.GetComponent<NavMeshSurface>();
            surface ??= navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.BuildNavMesh();
        }

        private static void ConfigureLightingSettings()
        {
            LightingSettings lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(
                k_LightingSettingsPath);
            if (lightingSettings == null)
            {
                lightingSettings = new LightingSettings();
                AssetDatabase.CreateAsset(lightingSettings, k_LightingSettingsPath);
            }

            lightingSettings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            lightingSettings.mixedBakeMode = MixedLightingMode.Shadowmask;
            lightingSettings.lightmapResolution = 12f;
            lightingSettings.lightmapPadding = 2;
            lightingSettings.lightmapMaxSize = 1024;
            Lightmapping.lightingSettings = lightingSettings;
            EditorUtility.SetDirty(lightingSettings);
        }

        private static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "EP99-100 setup tools can only run after Play Mode has fully stopped.");
            }
        }

        private static VolumeProfile GetOrCreateVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                k_VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, k_VolumeProfilePath);
            }

            Bloom bloom = GetOrAddOverride<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.32f);
            Tonemapping tonemapping = GetOrAddOverride<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);
            Vignette vignette = GetOrAddOverride<Vignette>(profile);
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.42f);
            ColorAdjustments colorAdjustments = GetOrAddOverride<ColorAdjustments>(profile);
            colorAdjustments.postExposure.Override(0.25f);
            colorAdjustments.contrast.Override(8f);
            colorAdjustments.saturation.Override(-10f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAddOverride<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            if (profile.TryGet(out T existingComponent))
            {
                return existingComponent;
            }

            T component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            component.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            EditorUtility.SetDirty(component);
            return component;
        }

        private static void OnDungeonLightingBakeCompleted()
        {
            Lightmapping.bakeCompleted -= OnDungeonLightingBakeCompleted;
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("EP99-100 dungeon Shadowmask lighting bake completed.");
        }

        private static IEnumerable<Vector3> CreateProbePositions()
        {
            Vector3[] routePoints =
            {
                new(23f, 1f, 0f),
                new(28f, 1f, 9f),
                new(38f, 5f, 14f),
                new(50f, 5f, 14f),
                new(62f, 3f, 12f),
                new(28f, 0f, -9f),
                new(38f, -2f, -14f),
                new(50f, -2f, -14f),
                new(62f, 0f, -12f),
                new(48f, 1f, 0f),
                new(68f, 1f, 0f),
                new(72f, 1f, 16f),
                new(88f, 1f, 0f),
                new(105f, 1f, 0f),
                new(116f, 1f, 0f)
            };
            foreach (Vector3 routePoint in routePoints)
            {
                yield return routePoint;
                yield return routePoint + Vector3.up * 2f;
            }
        }

        private static Zone CreateZone(Transform location, string name)
        {
            GameObject zoneObject = new(name);
            zoneObject.transform.SetParent(location, false);
            Transform floors = CreateContainer(zoneObject.transform, "Floors");
            Transform walls = CreateContainer(zoneObject.transform, "Walls");
            Transform props = CreateContainer(zoneObject.transform, "Props");
            return new Zone(floors, walls, props);
        }

        private static Transform CreateContainer(Transform parent, string name)
        {
            GameObject container = new(name);
            container.transform.SetParent(parent, false);
            return container.transform;
        }

        private static GameObject CreateBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool hasCollider = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            if (!hasCollider)
            {
                UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            }

            GameObjectUtility.SetStaticEditorFlags(block, s_environmentStaticFlags);
            return block;
        }

        private static void CreateWall(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            CreateBlock(parent, name, position, scale, material);
        }

        private static void CreateCeiling(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            CreateBlock(parent, name, position, scale, material);
        }

        private static void CreateStairs(
            Transform parent,
            string prefix,
            Vector3 start,
            Vector3 direction,
            bool risesForward,
            Material material)
        {
            Vector3 normalizedDirection = direction.normalized;
            for (int stepIndex = 0; stepIndex < 8; stepIndex++)
            {
                int heightIndex = risesForward ? stepIndex : 7 - stepIndex;
                Vector3 position = start + normalizedDirection * stepIndex * 0.75f;
                position.y += heightIndex * 0.5f - 0.25f;
                CreateBlock(
                    parent,
                    $"{prefix}_{stepIndex:00}",
                    position,
                    new Vector3(2.5f, 0.5f, 1.2f),
                    material);
            }
        }

        private static void CreatePillar(
            Transform parent,
            Vector3 position,
            Material material,
            int variant)
        {
            GameObject pillar = CreateBlock(
                parent,
                $"Dungeon_Pillar_Variant_{variant % 3:00}",
                position,
                new Vector3(1.2f, 5f + variant % 2, 1.2f),
                material);
            pillar.transform.rotation = Quaternion.Euler(0f, variant * 17f, variant % 2 * 3f);
        }

        private static void CreateRubble(
            Transform parent,
            Vector3 center,
            Material material,
            int seed)
        {
            System.Random random = new(seed);
            for (int rubbleIndex = 0; rubbleIndex < 7; rubbleIndex++)
            {
                GameObject rubble = GameObject.CreatePrimitive(
                    rubbleIndex % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                rubble.name = $"Rubble_{seed:00}_{rubbleIndex:00}";
                rubble.transform.SetParent(parent, false);
                rubble.transform.position = center + new Vector3(
                    (float)random.NextDouble() * 3f - 1.5f,
                    (float)random.NextDouble() * 0.25f,
                    (float)random.NextDouble() * 3f - 1.5f);
                rubble.transform.localScale = new Vector3(
                    0.25f + (float)random.NextDouble() * 0.65f,
                    0.2f + (float)random.NextDouble() * 0.45f,
                    0.25f + (float)random.NextDouble() * 0.65f);
                rubble.transform.rotation = UnityEngine.Random.rotation;
                rubble.GetComponent<Renderer>().sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(rubble, s_environmentStaticFlags);
            }
        }

        private static void CreateTorch(
            Transform parent,
            Vector3 position,
            Vector3 direction)
        {
            GameObject torch = new("Dungeon_Torch");
            torch.transform.SetParent(parent, false);
            torch.transform.position = position;
            torch.transform.forward = direction;
            Light light = torch.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.46f, 0.16f);
            light.intensity = 4.2f;
            light.range = 10f;
            light.shadows = LightShadows.Soft;
            light.lightmapBakeType = LightmapBakeType.Mixed;
        }

        private static void CreateOneWayGate(
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            Vector3 blockingSize,
            Material material,
            bool allowedFromPositiveForwardSide)
        {
            GameObject gateRoot = new(name);
            gateRoot.transform.SetParent(parent, false);
            gateRoot.transform.SetPositionAndRotation(position, rotation);
            BoxCollider interactionCollider = gateRoot.AddComponent<BoxCollider>();
            interactionCollider.center = new Vector3(0f, 2f, 0f);
            interactionCollider.size = blockingSize + new Vector3(3f, 1f, 3f);
            interactionCollider.isTrigger = true;

            GameObject visual = new("Gate Visual");
            visual.transform.SetParent(gateRoot.transform, false);
            CreateIronBars(
                visual.transform,
                "Gate Bars",
                new Vector3(0f, 2f, 0f),
                blockingSize,
                material,
                false);

            GameObject blocker = new("Gate Blocking Collider");
            blocker.transform.SetParent(gateRoot.transform, false);
            blocker.transform.localPosition = new Vector3(0f, 2f, 0f);
            BoxCollider blockingCollider = blocker.AddComponent<BoxCollider>();
            blockingCollider.size = blockingSize;

            DungeonOneWayGate gate = gateRoot.AddComponent<DungeonOneWayGate>();
            SerializedObject serializedGate = new(gate);
            serializedGate.FindProperty("m_interactableCollider").objectReferenceValue =
                interactionCollider;
            serializedGate.FindProperty("m_shouldDisableColliderAfterInteraction").boolValue =
                false;
            serializedGate.FindProperty("m_gateVisual").objectReferenceValue = visual.transform;
            serializedGate.FindProperty("m_blockingCollider").objectReferenceValue =
                blockingCollider;
            serializedGate.FindProperty("m_allowedFromPositiveForwardSide").boolValue =
                allowedFromPositiveForwardSide;
            serializedGate.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateIronBars(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            Material material,
            bool includeBlockingCollider)
        {
            GameObject bars = new(name);
            bars.transform.SetParent(parent, false);
            bars.transform.localPosition = localPosition;
            float width = Mathf.Max(size.x, size.z);
            int barCount = Mathf.Max(3, Mathf.RoundToInt(width));
            for (int barIndex = 0; barIndex < barCount; barIndex++)
            {
                float offset = Mathf.Lerp(-width * 0.45f, width * 0.45f,
                    barCount <= 1 ? 0.5f : barIndex / (barCount - 1f));
                Vector3 barPosition = size.x >= size.z
                    ? new Vector3(offset, 0f, 0f)
                    : new Vector3(0f, 0f, offset);
                GameObject bar = CreateBlock(
                    bars.transform,
                    $"Iron_Bar_{barIndex:00}",
                    bars.transform.TransformPoint(barPosition),
                    new Vector3(0.18f, size.y, 0.18f),
                    material,
                    false);
                bar.transform.rotation = bars.transform.rotation;
            }

            if (includeBlockingCollider)
            {
                BoxCollider collider = bars.AddComponent<BoxCollider>();
                collider.size = size;
            }
        }

        private static void CreateRewardPickup(
            Transform parent,
            Vector3 position,
            Material rewardMaterial)
        {
            GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Data/Prefabs/Interactables/Item Pickup.prefab");
            if (pickupPrefab != null &&
                PrefabUtility.InstantiatePrefab(pickupPrefab, parent) is GameObject pickup)
            {
                pickup.name = "Visible Reward - Ashen Crypt";
                pickup.transform.position = position;
                PickupItemInteractable interactable =
                    pickup.GetComponent<PickupItemInteractable>();
                if (interactable != null)
                {
                    SerializedObject serializedPickup = new(interactable);
                    serializedPickup.FindProperty("m_pickupType").enumValueIndex = 0;
                    serializedPickup.FindProperty("m_itemID").intValue = 9900;
                    serializedPickup.FindProperty("m_trackDroppingCreaturePosition").boolValue =
                        false;
                    serializedPickup.ApplyModifiedPropertiesWithoutUndo();
                }

                return;
            }

            GameObject reward = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reward.name = "Visible Reward Placeholder";
            reward.transform.SetParent(parent, false);
            reward.transform.position = position;
            reward.transform.localScale = Vector3.one * 0.5f;
            reward.GetComponent<Renderer>().sharedMaterial = rewardMaterial;
        }

        private static Scene OpenWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_ScenePath);
            return scene.IsValid() && scene.isLoaded
                ? scene
                : EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
        }

        private static GameObject GetOrCreateRoot(Scene scene, string rootName)
        {
            GameObject root = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == rootName);
            if (root != null)
            {
                return root;
            }

            root = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static GameObject FindGameObject(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(candidate => candidate.name == objectName)
                ?.gameObject;
        }

        private static Material GetOrCreateMaterial(
            string materialName,
            Color color,
            float emission = 0f)
        {
            string materialPath = $"{k_MaterialFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader)
                {
                    name = materialName
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.12f);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] folders = folderPath.Split('/');
            string currentFolder = folders[0];
            for (int folderIndex = 1; folderIndex < folders.Length; folderIndex++)
            {
                string nextFolder = $"{currentFolder}/{folders[folderIndex]}";
                if (!AssetDatabase.IsValidFolder(nextFolder))
                {
                    AssetDatabase.CreateFolder(currentFolder, folders[folderIndex]);
                }

                currentFolder = nextFolder;
            }
        }

        private readonly struct Zone
        {
            public Zone(Transform floors, Transform walls, Transform props)
            {
                Floors = floors;
                Walls = walls;
                Props = props;
            }

            public Transform Floors { get; }
            public Transform Walls { get; }
            public Transform Props { get; }
        }
    }
}
#endif
