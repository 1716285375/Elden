using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Replaces every LV01 greybox environment with the Idyllic Fantasy Nature
    /// demo while preserving the existing Region, Area, and slice architecture.
    /// Every mutating step is idempotent and can be run independently.
    /// </summary>
    public static class LV01NatureMapIntegration
    {
        private const string k_DemoScenePath =
            "Assets/_Game/Scenes/Tests/Environment/IdyllicFantasyNature/Demo.unity";
        private const string k_BackupRootPath = "F:/tmp-files/Game/Elden-backdata";
        private const string k_EnvironmentRootName = "_Environment";
        private const string k_ImportedEnvironmentRootName = "Idyllic Fantasy Nature";
        private const string k_RendererManagerName = "World Location Renderer Manager";
        private const string k_NavigationRootName = "_Navigation";
        private const string k_NatureLinkRootName = "Nature NavMesh Links";
        private const string k_TriggerPrefabPath =
            "Assets/_Game/Prefabs/World/Streaming/Area Load Trigger.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World AI Manager.prefab";
        private const string k_DialogueNpcSpawnerName =
            "Nameless Knight Dialogue NPC Spawner";
        private const string k_BossPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_EnemyPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_MerchantPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Blacksmith NPC.prefab";
        private const string k_ItemPickupPrefabPath =
            "Assets/_Game/Prefabs/Interactables/Item Pickup.prefab";
        private const string k_SmithingStonePrefabPath =
            "Assets/_Game/Prefabs/Interactables/Small Smithing Stone Pickup.prefab";
        private const string k_DungeonKeyPrefabPath =
            "Assets/_Game/Prefabs/Interactables/Old Dungeon Key Pickup.prefab";
        private const string k_ChestPrefabPath =
            "Assets/_Game/Art/Shared/Models/Rigged/Props/" +
            "Interactable_Chest_01/Interactable_Chest_01.prefab";
        private const string k_WorldLocationFolder =
            "Assets/_Game/Resources/World Locations";
        private const string k_NatureGameplayRootName = "Nature Gameplay Placeholders";
        private const string k_NaturePopulationRootName = "Nature AI Population";
        private const string k_NatureTriggerRootName = "Nature Streaming Triggers";
        private const int k_BossID = 1001;
        private const int k_EnemyPlacementSeed = 71021;
        private const int k_PoissonCandidatesPerPoint = 30;
        private const int k_SliceCount = 4;
        private const int k_BaseSliceIndex = 0;
        private const int k_PropsSliceIndex = 1;
        private const int k_EffectsSliceIndex = 2;
        private const int k_SpawnersSliceIndex = 3;
        private const float k_NavHorizontalMargin = 5f;
        private const float k_NavVerticalMargin = 10f;
        private const float k_MaxPlacementSlope = 32f;
        private const float k_PlacementSearchRadius = 14f;
        private const float k_PlacementSearchStep = 2f;
        private const float k_PlacementClearanceRadius = 0.75f;
        private const float k_PlacementClearanceHeight = 2f;
        private const float k_EnemyMinimumSpacing = 10.5f;
        private const float k_SpawnEnemyExclusionRadius = 22f;

        private static readonly string[] s_natureAssetPaths =
        {
            "Assets/_Game/Art/Environment/Nature/Animations/IdyllicFantasyNature",
            "Assets/_Game/Art/Environment/Nature/Materials/IdyllicFantasyNature",
            "Assets/_Game/Art/Environment/Nature/Models/IdyllicFantasyNature",
            "Assets/_Game/Art/Environment/Nature/Textures/IdyllicFantasyNature",
            "Assets/_Game/Art/Environment/Nature/Shaders/IdyllicFantasyNature",
            "Assets/_Game/Art/Environment/Nature/TerrainLayers/IdyllicFantasyNature",
            "Assets/_Game/Art/Environment/Nature/Documentation/" +
                "Idyllic Fantasy Nature Documentation.pdf",
            "Assets/_Game/Prefabs/World/Nature/IdyllicFantasyNature",
            "Assets/_Game/Scripts/World/Environment/IdyllicFantasyNature",
            "Assets/_Game/Scenes/Tests/Environment/IdyllicFantasyNature"
        };

        private static readonly Vector3 s_playerSpawnPosition =
            new(26f, 6.19999981f, 42.7999992f);
        private static readonly Quaternion s_playerSpawnRotation =
            new(0f, 0.70710683f, 0f, 0.70710683f);
        private static readonly Vector2 s_gracePosition = new(31.5f, 42.8f);
        private static readonly Vector2 s_merchantPosition = new(29f, 49f);

        private static readonly AreaDefinition[] s_areas =
        {
            new(
                0,
                0,
                "A01_CliffPath",
                "Area_01_Sub_Area_00",
                WorldSceneLocation.Area01SubArea00,
                "R01_MonasteryOutskirts_A01_CliffPath.asset",
                new Rect(15f, 10f, 155f, 70f)),
            new(
                0,
                1,
                "A02_Graveyard",
                "Area_01_Sub_Area_05",
                WorldSceneLocation.Area01SubArea05,
                "R01_MonasteryOutskirts_A02_Graveyard.asset",
                new Rect(40f, 80f, 160f, 90f)),
            new(
                0,
                2,
                "A03_MainGate",
                "Area_01_Sub_Area_06",
                WorldSceneLocation.Area01SubArea06,
                "R01_MonasteryOutskirts_A03_MainGate.asset",
                new Rect(110f, 170f, 80f, 35f)),
            new(
                0,
                3,
                "A04_GateTower",
                "Area_01_Sub_Area_07",
                WorldSceneLocation.Area01SubArea07,
                "R01_MonasteryOutskirts_A04_GateTower.asset",
                new Rect(190f, 120f, 60f, 85f)),
            new(
                1,
                0,
                "A01_MonasteryInterior",
                "Area_01_Sub_Area_01",
                WorldSceneLocation.Area01SubArea01,
                "R02_MonasteryInterior.asset",
                new Rect(90f, 205f, 100f, 45f)),
            new(
                2,
                0,
                "A01_Catacombs",
                "Area_01_Sub_Area_02",
                WorldSceneLocation.Area01SubArea02,
                "R03_Catacombs.asset",
                new Rect(30f, 200f, 60f, 65f)),
            new(
                3,
                0,
                "A01_BellTower",
                "Area_01_Sub_Area_03",
                WorldSceneLocation.Area01SubArea03,
                "R04_BellTower.asset",
                new Rect(190f, 205f, 75f, 70f)),
            new(
                4,
                0,
                "A01_BossSanctum",
                "Area_01_Sub_Area_04",
                WorldSceneLocation.Area01SubArea04,
                "R05_BossSanctum.asset",
                new Rect(100f, 250f, 100f, 50f))
        };

        private static readonly NatureCategory[] s_natureCategories =
        {
            new("Cliffs", k_BaseSliceIndex, 390),
            new("Rocks", k_BaseSliceIndex, 364),
            new("Trees", k_PropsSliceIndex, 305),
            new("Bushes", k_PropsSliceIndex, 480),
            new("Waterplants", k_PropsSliceIndex, 44),
            new("Particle Effects", k_EffectsSliceIndex, 26),
            new("ButterflySpawnAreas", k_EffectsSliceIndex, 34)
        };

        private static readonly int[] s_enemyCountsByArea =
        {
            14,
            18,
            8,
            10,
            10,
            10,
            10,
            8
        };

        private static readonly BossPlacement[] s_bossPlacements =
        {
            new(1, 1101, "Graveyard Warden Boss Spawner", new Vector2(72f, 151f)),
            new(3, 1102, "Gate Tower Warden Boss Spawner", new Vector2(229f, 190f)),
            new(5, 1103, "Catacombs Warden Boss Spawner", new Vector2(64f, 247f)),
            new(6, 1104, "Bell Tower Warden Boss Spawner", new Vector2(235f, 249f)),
            new(7, k_BossID, "Fallen Watcher Boss Spawner", new Vector2(150f, 278f))
        };

        private static readonly GameplayPlacement[] s_gameplayPlacements =
        {
            new(
                0,
                "PB_A01_CliffPath_Enemy_00",
                "Gameplay_A01_CliffPath_Enemy_00",
                new Vector2(120f, 35f)),
            new(
                0,
                "PB_A01_CliffPath_Enemy_01",
                "Gameplay_A01_CliffPath_Enemy_01",
                new Vector2(150f, 58f)),
            new(
                1,
                "PB_A02_Graveyard_Enemy_00",
                "Gameplay_A02_Graveyard_Enemy_00",
                new Vector2(80f, 105f)),
            new(
                1,
                "PB_A02_Graveyard_Enemy_01",
                "Gameplay_A02_Graveyard_Enemy_01",
                new Vector2(120f, 130f)),
            new(
                1,
                "PB_A02_Graveyard_Enemy_02",
                "Gameplay_A02_Graveyard_Enemy_02",
                new Vector2(165f, 150f)),
            new(
                3,
                "PB_A04_GateTower_Enemy_00",
                "Gameplay_A04_GateTower_Enemy_00",
                new Vector2(220f, 165f)),
            new(
                4,
                "PB_A01_EntranceHall_Enemy_00",
                "Gameplay_A01_MonasteryInterior_Enemy_00",
                new Vector2(135f, 225f)),
            new(
                0,
                "Gameplay_A01_MonasteryInterior_SiteOfGrace_00",
                "Gameplay_A01_CliffPath_SiteOfGrace_00",
                s_gracePosition)
        };

        private static readonly ItemPlacement[] s_itemPlacements =
        {
            new(0, k_SmithingStonePrefabPath, "Nature Pickup - Smithing Stone", new Vector2(145f, 50f)),
            new(1, k_ItemPickupPrefabPath, "Nature Pickup - Graveyard Reward", new Vector2(70f, 140f)),
            new(2, k_DungeonKeyPrefabPath, "Nature Pickup - Dungeon Key", new Vector2(155f, 190f)),
            new(3, k_ChestPrefabPath, "Nature Placeholder - Gate Tower Chest", new Vector2(225f, 175f)),
            new(4, k_SmithingStonePrefabPath, "Nature Pickup - Monastery Stone", new Vector2(150f, 230f)),
            new(5, k_DungeonKeyPrefabPath, "Nature Pickup - Catacombs Key", new Vector2(60f, 245f)),
            new(6, k_ChestPrefabPath, "Nature Placeholder - Bell Tower Chest", new Vector2(235f, 250f)),
            new(7, k_ItemPickupPrefabPath, "Nature Pickup - Sanctum Reward", new Vector2(165f, 270f))
        };

        private static readonly JunctionDefinition[] s_junctions =
        {
            new(0, 1, new Vector2(130f, 80f)),
            new(1, 0, new Vector2(130f, 80f)),
            new(1, 2, new Vector2(150f, 170f)),
            new(2, 1, new Vector2(150f, 170f)),
            new(1, 3, new Vector2(195f, 150f)),
            new(3, 1, new Vector2(195f, 150f)),
            new(2, 3, new Vector2(190f, 188f)),
            new(3, 2, new Vector2(190f, 188f)),
            new(2, 4, new Vector2(150f, 205f)),
            new(4, 2, new Vector2(150f, 205f)),
            new(4, 5, new Vector2(90f, 230f)),
            new(5, 4, new Vector2(90f, 230f)),
            new(5, 6, new Vector2(190f, 235f)),
            new(6, 5, new Vector2(190f, 235f)),
            new(6, 7, new Vector2(195f, 260f)),
            new(7, 6, new Vector2(195f, 260f))
        };

        /// <summary>Validates every dependency and creates an external rollback copy.</summary>
        [ZZTool("世界与导航", "01 预检并备份", 110)]
        public static void PreflightAndBackup()
        {
            RunPreflight();
            string backupPath = CreateBackup();
            Debug.Log($"[LV01Nature] Preflight passed. Backup: {backupPath}");
        }

        /// <summary>Deletes all PB geometry while preserving gameplay components.</summary>
        [ZZTool("世界与导航", "02 删除灰盒几何", 120, "将删除 LV01 灰盒环境几何。请确认备份已完成。")]
        public static void DeleteGreyboxGeometry()
        {
            EnsureNoDirtyScenes();
            int deletedCount = 0;
            int renamedGameplayCount = 0;

            foreach (string scenePath in GetAllSliceScenePaths())
            {
                Scene scene = OpenSceneIfNeeded(scenePath, out bool openedByIntegration);
                try
                {
                    List<Transform> greyboxObjects = GetSceneTransforms(scene)
                        .Where(transform => transform.name.StartsWith("PB_", StringComparison.Ordinal))
                        .OrderByDescending(GetHierarchyDepth)
                        .ToList();
                    foreach (Transform greyboxObject in greyboxObjects)
                    {
                        if (greyboxObject == null)
                        {
                            continue;
                        }

                        if (ContainsGameplayComponent(greyboxObject))
                        {
                            greyboxObject.name = "Gameplay_" + greyboxObject.name.Substring(3);
                            renamedGameplayCount++;
                            continue;
                        }

                        UnityEngine.Object.DestroyImmediate(greyboxObject.gameObject);
                        deletedCount++;
                    }

                    RefreshRendererManagers(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    CloseSceneIfNeeded(scene, openedByIntegration);
                }
            }

            Debug.Log(
                $"[LV01Nature] Deleted {deletedCount} greybox objects and preserved " +
                $"{renamedGameplayCount} gameplay objects.");
        }

        /// <summary>Moves the demo terrain, water, lighting, and controls into the master scene.</summary>
        [ZZTool("世界与导航", "03 移动环境到主场景", 130)]
        public static void MoveEnvironmentToMaster()
        {
            EnsureNoDirtyScenes();
            Scene originalActiveScene = SceneManager.GetActiveScene();
            Scene demoScene = OpenSceneIfNeeded(k_DemoScenePath, out bool demoOpenedByIntegration);
            bool wasDemoOpen = !demoOpenedByIntegration;
            bool wasDemoActive = originalActiveScene == demoScene;
            Scene masterScene = default;
            bool masterOpenedByIntegration = false;

            try
            {
                SceneManager.SetActiveScene(demoScene);
                EnvironmentSettings environmentSettings = CaptureEnvironmentSettings();

                masterScene = OpenSceneIfNeeded(
                    WorldScenePathLayout.MasterScenePath,
                    out masterOpenedByIntegration);
                GameObject environmentRoot = EnsureSceneRoot(masterScene, k_EnvironmentRootName);
                DeleteAllChildren(environmentRoot.transform);
                GameObject importedRoot = new(k_ImportedEnvironmentRootName);
                SceneManager.MoveGameObjectToScene(importedRoot, masterScene);
                importedRoot.transform.SetParent(environmentRoot.transform, false);

                MoveDemoObject(demoScene, masterScene, "Terrains/LandTerrain", importedRoot.transform);
                MoveDemoObject(demoScene, masterScene, "Terrains/WaterTerrain", importedRoot.transform);
                MoveDemoObject(demoScene, masterScene, "Water", importedRoot.transform);
                GameObject sunObject = MoveDemoObject(
                    demoScene,
                    masterScene,
                    "Directional Light",
                    importedRoot.transform);
                MoveDemoObject(demoScene, masterScene, "PostProcessing", importedRoot.transform);
                MoveDemoObject(
                    demoScene,
                    masterScene,
                    "Controls/VegetationBendControl",
                    importedRoot.transform);
                MoveDemoObject(
                    demoScene,
                    masterScene,
                    "Controls/WindControl",
                    importedRoot.transform);

                SceneManager.SetActiveScene(masterScene);
                StaticOcclusionCulling.Clear();
                environmentSettings.Apply(sunObject.GetComponent<Light>());
                EditorSceneManager.MarkSceneDirty(masterScene);
                EditorSceneManager.SaveScene(masterScene);
                Debug.Log("[LV01Nature] Demo terrain and environment moved into the master scene.");
            }
            finally
            {
                ResetDemoScene(demoScene, wasDemoOpen, wasDemoActive);
                CloseSceneIfNeeded(masterScene, masterOpenedByIntegration);
                RestoreActiveScene(originalActiveScene);
            }
        }

        /// <summary>Distributes every authored demo nature instance into its owning slice.</summary>
        [ZZTool("世界与导航", "04 分配自然环境内容", 140)]
        public static void DistributeNatureContent()
        {
            EnsureNoDirtyScenes();
            Scene originalActiveScene = SceneManager.GetActiveScene();
            Scene demoScene = OpenSceneIfNeeded(k_DemoScenePath, out bool demoOpenedByIntegration);
            bool wasDemoOpen = !demoOpenedByIntegration;
            bool wasDemoActive = originalActiveScene == demoScene;
            int movedCount = 0;
            int nearestAreaCount = 0;
            var areaCounts = new int[s_areas.Length];

            try
            {
                foreach (NatureCategory category in s_natureCategories)
                {
                    Transform sourceRoot = FindTransform(demoScene, category.SourceRootName) ??
                        throw new InvalidOperationException(
                            $"Demo root '{category.SourceRootName}' is missing.");
                    List<Transform>[] buckets = CreateAreaBuckets();
                    foreach (Transform sourceChild in sourceRoot.Cast<Transform>().ToList())
                    {
                        int areaIndex = FindOwningArea(sourceChild.position, out bool isInsideArea);
                        buckets[areaIndex].Add(sourceChild);
                        areaCounts[areaIndex]++;
                        if (!isInsideArea)
                        {
                            nearestAreaCount++;
                        }
                    }

                    for (int areaIndex = 0; areaIndex < s_areas.Length; areaIndex++)
                    {
                        AreaDefinition area = s_areas[areaIndex];
                        string scenePath = GetSliceScenePath(area, category.SliceIndex);
                        Scene targetScene = OpenSceneIfNeeded(
                            scenePath,
                            out bool openedByIntegration);
                        try
                        {
                            Transform areaRoot = EnsureAreaRoot(targetScene, area).transform;
                            DeleteNamedTransforms(targetScene, category.GeneratedRootName);
                            GameObject categoryRoot = new(category.GeneratedRootName);
                            SceneManager.MoveGameObjectToScene(categoryRoot, targetScene);
                            categoryRoot.transform.SetParent(areaRoot, false);

                            foreach (Transform sourceChild in buckets[areaIndex])
                            {
                                sourceChild.SetParent(null, true);
                                SceneManager.MoveGameObjectToScene(sourceChild.gameObject, targetScene);
                                sourceChild.SetParent(categoryRoot.transform, true);
                                movedCount++;
                            }

                            RefreshRendererManagers(targetScene);
                            EditorSceneManager.SaveScene(targetScene);
                        }
                        finally
                        {
                            CloseSceneIfNeeded(targetScene, openedByIntegration);
                        }
                    }
                }
            }
            finally
            {
                ResetDemoScene(demoScene, wasDemoOpen, wasDemoActive);
                RestoreActiveScene(originalActiveScene);
            }

            string counts = string.Join(
                ", ",
                areaCounts.Select((count, index) => $"{s_areas[index].RootName}={count}"));
            Debug.Log(
                $"[LV01Nature] Distributed {movedCount} nature roots. " +
                $"Nearest-area fallback used for {nearestAreaCount}. {counts}");
        }

        /// <summary>Moves all preserved gameplay objects onto sampled terrain positions.</summary>
        [ZZTool("世界与导航", "05 重定位玩法对象", 150)]
        public static void RelocateGameplayObjects()
        {
            EnsureNoDirtyScenes();
            Scene masterScene = OpenSceneIfNeeded(
                WorldScenePathLayout.MasterScenePath,
                out bool masterOpenedByIntegration);
            try
            {
                Terrain landTerrain = FindLandTerrain(masterScene);
                EnsureLandTerrainCollision(landTerrain);
                EnsureWorldAIManager(masterScene);
                foreach (IGrouping<int, GameplayPlacement> placementGroup in
                         s_gameplayPlacements.GroupBy(placement => placement.AreaIndex))
                {
                    AreaDefinition area = s_areas[placementGroup.Key];
                    string scenePath = GetSliceScenePath(area, k_SpawnersSliceIndex);
                    Scene scene = OpenSceneIfNeeded(scenePath, out bool openedByIntegration);
                    Scene baseScene = OpenSceneIfNeeded(
                        GetSliceScenePath(area, k_BaseSliceIndex),
                        out bool baseOpenedByIntegration);
                    Scene propsScene = OpenSceneIfNeeded(
                        GetSliceScenePath(area, k_PropsSliceIndex),
                        out bool propsOpenedByIntegration);
                    try
                    {
                        Physics.SyncTransforms();
                        foreach (GameplayPlacement placement in placementGroup)
                        {
                            string legacyGameplayName = placement.OriginalName.Replace(
                                "PB_",
                                "Gameplay_");
                            Transform gameplayObject =
                                FindTransform(scene, placement.OriginalName) ??
                                FindTransform(scene, placement.NewName) ??
                                FindTransform(scene, legacyGameplayName) ??
                                MoveGameplayObjectFromAnotherSpawnerScene(
                                    placement,
                                    legacyGameplayName,
                                    scene) ??
                                throw new InvalidOperationException(
                                    $"Gameplay object '{placement.OriginalName}' is missing in all spawner scenes.");
                            gameplayObject.name = placement.NewName;
                            gameplayObject.SetParent(EnsureAreaRoot(scene, area).transform, true);
                            SetPositionOnSafeTerrain(
                                gameplayObject,
                                placement.Position,
                                area.Bounds,
                                landTerrain,
                                0.1f);
                            EnsureSiteOfGraceTeleport(gameplayObject, landTerrain);
                        }

                        EditorSceneManager.SaveScene(scene);
                    }
                    finally
                    {
                        CloseSceneIfNeeded(propsScene, propsOpenedByIntegration);
                        CloseSceneIfNeeded(baseScene, baseOpenedByIntegration);
                        CloseSceneIfNeeded(scene, openedByIntegration);
                    }
                }

                Transform playerSpawn = FindTransform(masterScene, "Player Spawn Point") ??
                    throw new InvalidOperationException("Player Spawn Point is missing from the master scene.");
                playerSpawn.SetPositionAndRotation(
                    s_playerSpawnPosition,
                    s_playerSpawnRotation);
                playerSpawn.localScale = Vector3.one;

                Transform initialTrigger = FindTransform(masterScene, "Spawn Area Load Trigger") ??
                    throw new InvalidOperationException(
                        "Spawn Area Load Trigger is missing from the master scene.");
                initialTrigger.position = s_playerSpawnPosition + Vector3.down;
                Collider triggerCollider = initialTrigger.GetComponent<Collider>() ??
                    throw new InvalidOperationException(
                        "Spawn Area Load Trigger has no Collider component.");
                triggerCollider.enabled = true;
                triggerCollider.isTrigger = true;

                EditorSceneManager.SaveScene(masterScene);
                Debug.Log(
                    $"[LV01Nature] Gameplay objects relocated to safe terrain. " +
                    $"Player spawn fixed at {s_playerSpawnPosition}.");
            }
            finally
            {
                CloseSceneIfNeeded(masterScene, masterOpenedByIntegration);
            }
        }

        /// <summary>
        /// Adds items, the nearby merchant, and a deterministic blue-noise AI population.
        /// </summary>
        [ZZTool("世界与导航", "06 添加 Boss 与物品占位", 160)]
        public static void AddBossAndItemPlaceholders()
        {
            EnsureNoDirtyScenes();
            Scene masterScene = OpenSceneIfNeeded(
                WorldScenePathLayout.MasterScenePath,
                out bool masterOpenedByIntegration);
            try
            {
                Terrain landTerrain = FindLandTerrain(masterScene);
                GameObject bossPrefab = LoadRequiredAsset<GameObject>(k_BossPrefabPath);
                GameObject enemyPrefab = LoadRequiredAsset<GameObject>(k_EnemyPrefabPath);
                GameObject merchantPrefab = LoadRequiredAsset<GameObject>(k_MerchantPrefabPath);
                int generatedEnemyCount = 0;
                int generatedBossCount = 0;
                for (int areaIndex = 0; areaIndex < s_areas.Length; areaIndex++)
                {
                    AreaDefinition area = s_areas[areaIndex];
                    Scene scene = OpenSceneIfNeeded(
                        GetSliceScenePath(area, k_SpawnersSliceIndex),
                        out bool openedByIntegration);
                    Scene baseScene = OpenSceneIfNeeded(
                        GetSliceScenePath(area, k_BaseSliceIndex),
                        out bool baseOpenedByIntegration);
                    Scene propsScene = OpenSceneIfNeeded(
                        GetSliceScenePath(area, k_PropsSliceIndex),
                        out bool propsOpenedByIntegration);
                    try
                    {
                        Physics.SyncTransforms();
                        Transform areaRoot = EnsureAreaRoot(scene, area).transform;
                        DeleteNamedTransforms(scene, k_NatureGameplayRootName);
                        DeleteNamedTransforms(scene, k_NaturePopulationRootName);
                        GameObject gameplayRoot = new(k_NatureGameplayRootName);
                        SceneManager.MoveGameObjectToScene(gameplayRoot, scene);
                        gameplayRoot.transform.SetParent(areaRoot, false);
                        GameObject populationRoot = new(k_NaturePopulationRootName);
                        SceneManager.MoveGameObjectToScene(populationRoot, scene);
                        populationRoot.transform.SetParent(areaRoot, false);

                        foreach (ItemPlacement item in s_itemPlacements
                                     .Where(item => item.AreaIndex == areaIndex))
                        {
                            GameObject prefab = LoadRequiredAsset<GameObject>(item.PrefabPath);
                            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                            instance.name = item.Name;
                            instance.transform.SetParent(gameplayRoot.transform, true);
                            SetPositionOnSafeTerrain(
                                instance.transform,
                                item.Position,
                                area.Bounds,
                                landTerrain,
                                0.2f);
                        }

                        List<AICharacterSpawner> preservedEnemySpawners =
                            GetSceneComponents<AICharacterSpawner>(scene)
                                .Where(spawner =>
                                    !spawner.IsBoss &&
                                    GetSpawnerCharacterPrefab(spawner) != merchantPrefab)
                                .ToList();
                        List<Vector2> occupiedPoints = preservedEnemySpawners
                            .Select(spawner => new Vector2(
                                spawner.transform.position.x,
                                spawner.transform.position.z))
                            .ToList();
                        List<Vector2> exclusions = s_bossPlacements
                            .Where(placement => placement.AreaIndex == areaIndex)
                            .Select(placement => placement.Position)
                            .ToList();
                        if (areaIndex == 0)
                        {
                            exclusions.Add(new Vector2(
                                s_playerSpawnPosition.x,
                                s_playerSpawnPosition.z));
                            exclusions.Add(s_gracePosition);
                            exclusions.Add(s_merchantPosition);
                        }

                        int requestedEnemyCount = Mathf.Max(
                            0,
                            s_enemyCountsByArea[areaIndex] -
                            preservedEnemySpawners.Count);
                        List<Vector2> enemyPoints = GeneratePoissonPoints(
                            area,
                            landTerrain,
                            requestedEnemyCount,
                            k_EnemyPlacementSeed + areaIndex * 997,
                            occupiedPoints,
                            exclusions);
                        var random = new System.Random(
                            k_EnemyPlacementSeed + areaIndex * 1597);
                        for (int enemyIndex = 0;
                             enemyIndex < enemyPoints.Count;
                             enemyIndex++)
                        {
                            Vector2 point = enemyPoints[enemyIndex];
                            AICharacterSpawner spawner = CreateCharacterSpawner(
                                scene,
                                populationRoot.transform,
                                $"Nature Enemy Spawner {areaIndex + 1:D2}-{enemyIndex + 1:D2}",
                                enemyPrefab,
                                0,
                                true);
                            SetPositionOnTerrain(
                                spawner.transform,
                                point,
                                landTerrain,
                                0.1f);
                            spawner.transform.rotation = Quaternion.Euler(
                                0f,
                                (float)random.NextDouble() * 360f,
                                0f);
                            generatedEnemyCount++;
                        }

                        if (areaIndex == 0)
                        {
                            AICharacterSpawner merchantSpawner = CreateCharacterSpawner(
                                scene,
                                populationRoot.transform,
                                "Spawn Camp Blacksmith Merchant Spawner",
                                merchantPrefab,
                                0,
                                false);
                            SetPositionOnSafeTerrain(
                                merchantSpawner.transform,
                                s_merchantPosition,
                                area.Bounds,
                                landTerrain,
                                0.1f);
                            FaceTransformTowards(
                                merchantSpawner.transform,
                                s_playerSpawnPosition);
                        }

                        foreach (BossPlacement placement in s_bossPlacements
                                     .Where(placement =>
                                         placement.AreaIndex == areaIndex))
                        {
                            AICharacterSpawner bossSpawner = CreateCharacterSpawner(
                                scene,
                                populationRoot.transform,
                                placement.Name,
                                bossPrefab,
                                placement.BossID,
                                true);
                            SetPositionOnSafeTerrain(
                                bossSpawner.transform,
                                placement.Position,
                                area.Bounds,
                                landTerrain,
                                0.1f);
                            FaceTransformTowards(
                                bossSpawner.transform,
                                new Vector3(
                                    area.Bounds.center.x,
                                    bossSpawner.transform.position.y,
                                    area.Bounds.center.y));
                            generatedBossCount++;
                        }

                        EditorSceneManager.SaveScene(scene);
                    }
                    finally
                    {
                        CloseSceneIfNeeded(propsScene, propsOpenedByIntegration);
                        CloseSceneIfNeeded(baseScene, baseOpenedByIntegration);
                        CloseSceneIfNeeded(scene, openedByIntegration);
                    }
                }

                Debug.Log(
                    $"[LV01Nature] Added {s_itemPlacements.Length} item placeholders, " +
                    $"{generatedEnemyCount} generated enemies, one spawn merchant, and " +
                    $"{generatedBossCount} bosses.");
            }
            finally
            {
                CloseSceneIfNeeded(masterScene, masterOpenedByIntegration);
            }
        }

        /// <summary>Configures per-Area NavMesh volumes, renderer caches, and junction triggers.</summary>
        [ZZTool("世界与导航", "07 配置导航与触发器", 170)]
        public static void ConfigureNavigationAndTriggers()
        {
            EnsureNoDirtyScenes();
            Scene masterScene = OpenSceneIfNeeded(
                WorldScenePathLayout.MasterScenePath,
                out bool masterOpenedByIntegration);
            try
            {
                Terrain landTerrain = FindLandTerrain(masterScene);
                Bounds terrainBounds = GetTerrainWorldBounds(landTerrain);
                foreach (AreaDefinition area in s_areas)
                {
                    for (int sliceIndex = 0; sliceIndex < k_SliceCount; sliceIndex++)
                    {
                        Scene scene = OpenSceneIfNeeded(
                            GetSliceScenePath(area, sliceIndex),
                            out bool openedByIntegration);
                        try
                        {
                            ConfigureRendererManager(scene, sliceIndex);
                            if (sliceIndex == k_BaseSliceIndex)
                            {
                                ConfigureNavigationSurface(scene, area, terrainBounds);
                            }

                            EditorSceneManager.SaveScene(scene);
                        }
                        finally
                        {
                            CloseSceneIfNeeded(scene, openedByIntegration);
                        }
                    }
                }

                PlaceNavMeshLinks(landTerrain);
                PlaceStreamingTriggers(landTerrain);
                Debug.Log(
                    $"[LV01Nature] Configured {s_areas.Length} navigation volumes and " +
                    $"{GetUniqueJunctionCount()} bidirectional links, plus " +
                    $"{s_junctions.Length} directed streaming triggers.");
            }
            finally
            {
                CloseSceneIfNeeded(masterScene, masterOpenedByIntegration);
            }
        }

        /// <summary>
        /// Deletes stale Area NavMesh assets, bakes the current nature geometry,
        /// and snaps every AI spawner to its Area's rebuilt navigation surface.
        /// </summary>
        [ZZTool("世界与导航", "08 重建自然地图导航", 180)]
        public static void RebuildAllNatureNavigation()
        {
            EnsureNoDirtyScenes();
            ConfigureNavigationAndTriggers();
            Scene originalActiveScene = SceneManager.GetActiveScene();
            Scene masterScene = OpenSceneIfNeeded(
                WorldScenePathLayout.MasterScenePath,
                out bool masterOpenedByIntegration);
            int repairedSpawnerCount = 0;
            try
            {
                foreach (AreaDefinition area in s_areas)
                {
                    Scene baseScene = OpenSceneIfNeeded(
                        GetSliceScenePath(area, k_BaseSliceIndex),
                        out bool baseOpenedByIntegration);
                    Scene propsScene = OpenSceneIfNeeded(
                        GetSliceScenePath(area, k_PropsSliceIndex),
                        out bool propsOpenedByIntegration);
                    Scene spawnersScene = default;
                    bool spawnersOpenedByIntegration = false;
                    try
                    {
                        SceneManager.SetActiveScene(baseScene);
                        GameObject navigationRoot = baseScene.GetRootGameObjects()
                            .FirstOrDefault(root => root.name == k_NavigationRootName) ??
                            throw new InvalidOperationException(
                                $"{baseScene.name} is missing {k_NavigationRootName}.");
                        NavMeshSurface surface =
                            navigationRoot.GetComponent<NavMeshSurface>() ??
                            throw new InvalidOperationException(
                                $"{baseScene.name} has no NavMeshSurface.");
                        surface.RemoveData();
                        surface.navMeshData = null;

                        string navigationAssetPath =
                            GetNavigationAssetPath(area);
                        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                                navigationAssetPath) != null &&
                            !AssetDatabase.DeleteAsset(navigationAssetPath))
                        {
                            throw new InvalidOperationException(
                                $"Could not delete stale navigation asset " +
                                $"{navigationAssetPath}.");
                        }

                        string navigationDirectory = Path.GetDirectoryName(
                                navigationAssetPath)
                            ?.Replace('\\', '/');
                        if (!string.IsNullOrEmpty(navigationDirectory) &&
                            !AssetDatabase.IsValidFolder(navigationDirectory))
                        {
                            Directory.CreateDirectory(navigationDirectory);
                            AssetDatabase.Refresh();
                        }

                        surface.BuildNavMesh();
                        NavMeshData builtData = surface.navMeshData ??
                            throw new InvalidOperationException(
                                $"NavMesh bake produced no data for {area.RootName}.");
                        AssetDatabase.CreateAsset(
                            builtData,
                            navigationAssetPath);
                        surface.navMeshData = builtData;
                        EditorUtility.SetDirty(builtData);
                        EditorUtility.SetDirty(surface);
                        EditorSceneManager.MarkSceneDirty(baseScene);
                        EditorSceneManager.SaveScene(baseScene);

                        spawnersScene = OpenSceneIfNeeded(
                            GetSliceScenePath(area, k_SpawnersSliceIndex),
                            out spawnersOpenedByIntegration);
                        foreach (AICharacterSpawner spawner in
                                 GetSceneComponents<AICharacterSpawner>(spawnersScene))
                        {
                            if (!NavMesh.SamplePosition(
                                    spawner.transform.position,
                                    out NavMeshHit hit,
                                    8f,
                                    NavMesh.AllAreas))
                            {
                                throw new InvalidOperationException(
                                    $"No rebuilt NavMesh exists within 8m of " +
                                    $"{spawner.name} in {area.RootName}.");
                            }

                            if ((spawner.transform.position - hit.position)
                                .sqrMagnitude <= 0.01f)
                            {
                                continue;
                            }

                            spawner.transform.position = hit.position;
                            repairedSpawnerCount++;
                            EditorSceneManager.MarkSceneDirty(spawnersScene);
                        }

                        EditorSceneManager.SaveScene(spawnersScene);
                        AssetDatabase.SaveAssets();
                    }
                    finally
                    {
                        CloseSceneIfNeeded(
                            spawnersScene,
                            spawnersOpenedByIntegration);
                        CloseSceneIfNeeded(propsScene, propsOpenedByIntegration);
                        CloseSceneIfNeeded(baseScene, baseOpenedByIntegration);
                    }
                }
            }
            finally
            {
                CloseSceneIfNeeded(masterScene, masterOpenedByIntegration);
                RestoreActiveScene(originalActiveScene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[LV01Nature] Rebuilt {s_areas.Length} Area NavMeshes and " +
                $"snapped {repairedSpawnerCount} AI spawners to reachable points.");
        }

        /// <summary>Verifies environment, content counts, gameplay, navigation, and streaming.</summary>
        [ZZTool("世界与导航", "09 验证自然地图集成", 190)]
        public static void VerifyIntegration()
        {
            EnsureNoDirtyScenes();
            var failures = new List<string>();
            int natureCount = 0;
            int prefabLinkedCount = 0;
            int normalSpawnerCount = 0;
            int merchantSpawnerCount = 0;
            int bossSpawnerCount = 0;
            int graceCount = 0;
            int triggerCount = 0;
            int legacyTriggerCount = 0;
            int navMeshLinkCount = 0;
            var bossIDs = new HashSet<int>();
            GameObject merchantPrefab =
                LoadRequiredAsset<GameObject>(k_MerchantPrefabPath);

            Scene masterScene = OpenSceneIfNeeded(
                WorldScenePathLayout.MasterScenePath,
                out bool masterOpenedByIntegration);
            try
            {
                ValidateMasterEnvironment(masterScene, failures);
                foreach (AreaDefinition area in s_areas)
                {
                    for (int sliceIndex = 0; sliceIndex < k_SliceCount; sliceIndex++)
                    {
                        Scene scene = OpenSceneIfNeeded(
                            GetSliceScenePath(area, sliceIndex),
                            out bool openedByIntegration);
                        try
                        {
                            ValidateSliceInfrastructure(scene, area, sliceIndex, failures);
                            foreach (Transform transform in GetSceneTransforms(scene))
                            {
                                if (transform.name.StartsWith("PB_", StringComparison.Ordinal))
                                {
                                    failures.Add($"{scene.name} still contains {transform.name}.");
                                }

                                if (transform.name.StartsWith(
                                        "Area Trigger ",
                                        StringComparison.Ordinal))
                                {
                                    legacyTriggerCount++;
                                }
                            }

                            foreach (NatureCategory category in s_natureCategories
                                         .Where(category => category.SliceIndex == sliceIndex))
                            {
                                Transform root = FindTransform(scene, category.GeneratedRootName);
                                if (root == null)
                                {
                                    failures.Add(
                                        $"{scene.name} is missing {category.GeneratedRootName}.");
                                    continue;
                                }

                                natureCount += root.childCount;
                                prefabLinkedCount += root.Cast<Transform>()
                                    .Count(child => PrefabUtility.IsPartOfPrefabInstance(child.gameObject));
                            }

                            foreach (AICharacterSpawner spawner in GetSceneComponents<AICharacterSpawner>(scene))
                            {
                                if (spawner.BossID > 0)
                                {
                                    bossSpawnerCount++;
                                    bossIDs.Add(spawner.BossID);
                                    if (!s_bossPlacements.Any(placement =>
                                            placement.BossID == spawner.BossID))
                                    {
                                        failures.Add(
                                            $"{scene.name} has unexpected Boss ID {spawner.BossID}.");
                                    }
                                }
                                else if (GetSpawnerCharacterPrefab(spawner) == merchantPrefab)
                                {
                                    merchantSpawnerCount++;
                                }
                                else
                                {
                                    normalSpawnerCount++;
                                }
                            }

                            graceCount += GetSceneComponents<SiteOfGraceInteractable>(scene).Count;
                            foreach (NavMeshLink link in GetSceneComponents<NavMeshLink>(scene))
                            {
                                navMeshLinkCount++;
                                if (!link.name.StartsWith("Nature Link ", StringComparison.Ordinal) ||
                                    !link.bidirectional)
                                {
                                    failures.Add($"{scene.name} contains stale NavMeshLink {link.name}.");
                                }
                            }

                            Transform triggerRoot = FindTransform(scene, k_NatureTriggerRootName);
                            triggerCount += triggerRoot != null ? triggerRoot.childCount : 0;
                        }
                        finally
                        {
                            CloseSceneIfNeeded(scene, openedByIntegration);
                        }
                    }
                }
            }
            finally
            {
                CloseSceneIfNeeded(masterScene, masterOpenedByIntegration);
            }

            int expectedNatureCount = s_natureCategories.Sum(category => category.ExpectedCount);
            if (natureCount != expectedNatureCount)
            {
                failures.Add($"Nature root count {natureCount} != expected {expectedNatureCount}.");
            }

            if (prefabLinkedCount != natureCount)
            {
                failures.Add(
                    $"Only {prefabLinkedCount} of {natureCount} nature roots retain prefab links.");
            }

            int expectedNormalSpawnerCount = s_enemyCountsByArea.Sum();
            if (normalSpawnerCount != expectedNormalSpawnerCount)
            {
                failures.Add(
                    $"Normal AI spawner count {normalSpawnerCount} != expected " +
                    $"{expectedNormalSpawnerCount}.");
            }

            if (merchantSpawnerCount != 1)
            {
                failures.Add(
                    $"Merchant spawner count {merchantSpawnerCount} != expected 1.");
            }

            if (bossSpawnerCount != s_bossPlacements.Length ||
                bossIDs.Count != s_bossPlacements.Length)
            {
                failures.Add(
                    $"Boss spawner count {bossSpawnerCount} with {bossIDs.Count} unique IDs " +
                    $"!= expected {s_bossPlacements.Length}.");
            }

            if (graceCount != 1)
            {
                failures.Add($"Site of Grace count {graceCount} != expected 1.");
            }

            if (triggerCount != s_junctions.Length)
            {
                failures.Add(
                    $"Nature streaming trigger count {triggerCount} != expected {s_junctions.Length}.");
            }

            if (legacyTriggerCount > 0)
            {
                failures.Add($"Legacy streaming trigger count {legacyTriggerCount} != expected 0.");
            }

            int expectedNavMeshLinkCount = GetUniqueJunctionCount();
            if (navMeshLinkCount != expectedNavMeshLinkCount)
            {
                failures.Add(
                    $"NavMeshLink count {navMeshLinkCount} != expected {expectedNavMeshLinkCount}.");
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "[LV01Nature] Verification failed:\n- " + string.Join("\n- ", failures));
            }

            Debug.Log(
                $"[LV01Nature] Verification passed: {natureCount} nature roots, " +
                $"{normalSpawnerCount} normal spawners, 1 merchant, " +
                $"{bossSpawnerCount} bosses, 1 grace, {navMeshLinkCount} links, " +
                $"and {triggerCount} triggers.");
        }

        /// <summary>Repairs LV01 spawn, solid colliders, and safe gameplay placement.</summary>
        [ZZTool("世界与导航", "10 修复出生点、碰撞与放置", 200)]
        public static void RepairSpawnCollisionAndPlacement()
        {
            EnsureNoDirtyScenes();
            EnsureNatureSolidColliders();
            RelocateGameplayObjects();
            AddBossAndItemPlaceholders();
            RebuildAllNatureNavigation();
            VerifyIntegration();
            ValidateSpawnCollision();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[LV01Nature] Spawn, collision, safe placements, navigation, and streaming repaired.");
        }

        /// <summary>Runs the complete direct-replacement pipeline in dependency order.</summary>
        [ZZTool("世界与导航", "11 运行完整集成", 210, "将执行完整的 LV01 自然地图替换流程。请确认已完成预检与备份。")]
        public static void RunFullIntegration()
        {
            PreflightAndBackup();
            DeleteGreyboxGeometry();
            MoveEnvironmentToMaster();
            DistributeNatureContent();
            EnsureNatureSolidColliders();
            RelocateGameplayObjects();
            AddBossAndItemPlaceholders();
            RebuildAllNatureNavigation();
            VerifyIntegration();
            ValidateSpawnCollision();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LV01Nature] Full direct-replacement integration completed.");
        }

        /// <summary>Frames the integrated terrain for a quick Scene View inspection.</summary>
        [ZZTool("世界与导航", "12 聚焦自然地图总览", 220)]
        public static void FrameNatureOverview()
        {
            Scene masterScene = OpenSceneIfNeeded(
                WorldScenePathLayout.MasterScenePath,
                out bool masterOpenedByIntegration);
            try
            {
                Terrain landTerrain = FindLandTerrain(masterScene);
                SceneView sceneView = SceneView.lastActiveSceneView ??
                    throw new InvalidOperationException("No active Scene View is available.");
                Selection.activeGameObject = landTerrain.gameObject;
                sceneView.rotation = Quaternion.Euler(35f, -45f, 0f);
                sceneView.orthographic = false;
                sceneView.FrameSelected(false);
                SceneView.RepaintAll();
            }
            finally
            {
                CloseSceneIfNeeded(masterScene, masterOpenedByIntegration);
            }
        }

        private static void RunPreflight()
        {
            EnsureNoDirtyScenes();
            RequireFile(k_DemoScenePath);
            RequireFile(WorldScenePathLayout.MasterScenePath);
            RequireFile(k_TriggerPrefabPath);
            RequireFile(k_EnemyPrefabPath);
            RequireFile(k_MerchantPrefabPath);
            RequireFile(k_BossPrefabPath);
            RequireFile(k_BossPrefabPath);
            RequireFile(k_ItemPickupPrefabPath);
            RequireFile(k_SmithingStonePrefabPath);
            RequireFile(k_DungeonKeyPrefabPath);
            RequireFile(k_ChestPrefabPath);
            foreach (string scenePath in GetAllSliceScenePaths())
            {
                RequireFile(scenePath);
                if (SceneUtility.GetBuildIndexByScenePath(scenePath) < 0)
                {
                    throw new InvalidOperationException(
                        $"Slice scene is not enabled in Build Settings: {scenePath}");
                }
            }

            foreach (AreaDefinition area in s_areas)
            {
                RequireFile(GetWorldLocationPath(area));
            }

            Scene demoScene = OpenSceneIfNeeded(k_DemoScenePath, out bool openedByIntegration);
            try
            {
                foreach (NatureCategory category in s_natureCategories)
                {
                    Transform root = FindTransform(demoScene, category.SourceRootName) ??
                        throw new InvalidOperationException(
                            $"Demo root '{category.SourceRootName}' is missing.");
                    if (root.childCount != category.ExpectedCount)
                    {
                        throw new InvalidOperationException(
                            $"Demo root '{category.SourceRootName}' has {root.childCount} children; " +
                            $"expected {category.ExpectedCount}.");
                    }
                }

                RequireTransform(demoScene, "Terrains/LandTerrain");
                RequireTransform(demoScene, "Terrains/WaterTerrain");
                RequireTransform(demoScene, "Water");
                RequireTransform(demoScene, "Directional Light");
                RequireTransform(demoScene, "PostProcessing");
                RequireTransform(demoScene, "Controls/VegetationBendControl");
                RequireTransform(demoScene, "Controls/WindControl");
            }
            finally
            {
                CloseSceneIfNeeded(demoScene, openedByIntegration);
            }

            List<Shader> brokenShaders = AssetDatabase.GetDependencies(k_DemoScenePath, true)
                .Select(path => AssetDatabase.LoadAssetAtPath<Shader>(path))
                .Where(shader => shader != null && ShaderUtil.ShaderHasError(shader))
                .Distinct()
                .ToList();
            if (brokenShaders.Count > 0)
            {
                IEnumerable<string> shaderErrors = brokenShaders.Select(shader =>
                {
                    string assetPath = AssetDatabase.GetAssetPath(shader);
                    string[] messages = ShaderUtil.GetShaderMessages(shader)
                        .Where(message => message.severity ==
                            UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        .Select(message => $"  {message.platform}: {message.message}")
                        .ToArray();
                    return messages.Length > 0
                        ? $"{assetPath}\n{string.Join("\n", messages)}"
                        : assetPath;
                });
                throw new InvalidOperationException(
                    "Idyllic Fantasy Nature has shader compilation errors:\n- " +
                    string.Join("\n- ", shaderErrors));
            }
        }

        private static string CreateBackup()
        {
            if (!Directory.Exists("F:/"))
            {
                throw new DirectoryNotFoundException("Backup drive F: is unavailable.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException("Unable to resolve the Unity project root.");
            string backupPath = Path.Combine(
                k_BackupRootPath,
                $"LV01NatureMapIntegration_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(backupPath);

            string levelSource = ToAbsoluteProjectPath(projectRoot, WorldScenePathLayout.LevelFolder);
            string levelDestination = Path.Combine(
                backupPath,
                "Assets",
                "_Game",
                "Scenes",
                "Levels",
                WorldScenePathLayout.LevelFolderName);

            var copiedFiles = new List<string>();
            try
            {
                foreach (string natureAssetPath in s_natureAssetPaths)
                {
                    string sourcePath = ToAbsoluteProjectPath(projectRoot, natureAssetPath);
                    string destinationPath = Path.Combine(
                        backupPath,
                        natureAssetPath.Replace('/', Path.DirectorySeparatorChar));
                    if (Directory.Exists(sourcePath))
                    {
                        CopyDirectory(sourcePath, destinationPath, copiedFiles);
                    }
                    else
                    {
                        CopySingleFile(sourcePath, destinationPath, copiedFiles);
                    }

                    string metaSourcePath = sourcePath + ".meta";
                    if (File.Exists(metaSourcePath))
                    {
                        CopySingleFile(
                            metaSourcePath,
                            destinationPath + ".meta",
                            copiedFiles);
                    }
                }

                CopyDirectory(levelSource, levelDestination, copiedFiles);
                CopySingleFile(
                    Path.Combine(projectRoot, "ProjectSettings", "EditorBuildSettings.asset"),
                    Path.Combine(backupPath, "ProjectSettings", "EditorBuildSettings.asset"),
                    copiedFiles);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var manifest = new StringBuilder();
            manifest.AppendLine("LV01 Idyllic Fantasy Nature integration backup");
            manifest.AppendLine($"Created: {DateTime.Now:O}");
            manifest.AppendLine($"Project: {projectRoot}");
            manifest.AppendLine($"Files: {copiedFiles.Count}");
            foreach (string copiedFile in copiedFiles.OrderBy(path => path))
            {
                var info = new FileInfo(copiedFile);
                manifest.AppendLine($"{info.Length}\t{copiedFile}");
            }

            File.WriteAllText(Path.Combine(backupPath, "manifest.txt"), manifest.ToString());
            return backupPath.Replace('\\', '/');
        }

        private static void CopyDirectory(
            string sourceDirectory,
            string destinationDirectory,
            ICollection<string> copiedFiles)
        {
            string[] files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++)
            {
                string sourceFile = files[index];
                string relativePath = sourceFile
                    .Substring(sourceDirectory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar).Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destinationFile = Path.Combine(destinationDirectory, relativePath);
                CopySingleFile(sourceFile, destinationFile, copiedFiles);
                if (index % 25 == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "LV01 Nature Integration Backup",
                        relativePath,
                        files.Length > 0 ? (float)index / files.Length : 1f);
                }
            }
        }

        private static void CopySingleFile(
            string sourceFile,
            string destinationFile,
            ICollection<string> copiedFiles)
        {
            string destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourceFile, destinationFile, true);
            copiedFiles.Add(destinationFile);
        }

        private static void PlaceNavMeshLinks(Terrain landTerrain)
        {
            for (int sourceAreaIndex = 0; sourceAreaIndex < s_areas.Length; sourceAreaIndex++)
            {
                JunctionDefinition[] links = s_junctions
                    .Where(junction =>
                        junction.SourceAreaIndex == sourceAreaIndex &&
                        junction.SourceAreaIndex < junction.DestinationAreaIndex)
                    .ToArray();
                if (links.Length == 0)
                {
                    continue;
                }

                AreaDefinition sourceArea = s_areas[sourceAreaIndex];
                Scene scene = OpenSceneIfNeeded(
                    GetSliceScenePath(sourceArea, k_BaseSliceIndex),
                    out bool openedByIntegration);
                try
                {
                    GameObject navigationRoot = EnsureSceneRoot(scene, k_NavigationRootName);
                    GameObject linkRoot = new(k_NatureLinkRootName);
                    SceneManager.MoveGameObjectToScene(linkRoot, scene);
                    linkRoot.transform.SetParent(navigationRoot.transform, false);

                    foreach (JunctionDefinition junction in links)
                    {
                        AreaDefinition destinationArea = s_areas[junction.DestinationAreaIndex];
                        GameObject linkObject = new(
                            $"Nature Link {sourceArea.RootName} <-> {destinationArea.RootName}");
                        SceneManager.MoveGameObjectToScene(linkObject, scene);
                        linkObject.transform.SetParent(linkRoot.transform, true);
                        SetPositionOnTerrain(
                            linkObject.transform,
                            junction.Position,
                            landTerrain,
                            0.1f);

                        Vector2 direction =
                            (destinationArea.Bounds.center - sourceArea.Bounds.center).normalized;
                        Vector3 offset = new(direction.x * 2f, 0f, direction.y * 2f);
                        Vector3 startWorld = linkObject.transform.position - offset;
                        startWorld.y = landTerrain.SampleHeight(startWorld) + 0.1f;
                        Vector3 endWorld = linkObject.transform.position + offset;
                        endWorld.y = landTerrain.SampleHeight(endWorld) + 0.1f;
                        NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
                        link.agentTypeID = 0;
                        link.startPoint = linkObject.transform.InverseTransformPoint(startWorld);
                        link.endPoint = linkObject.transform.InverseTransformPoint(endWorld);
                        link.width = 4f;
                        link.bidirectional = true;
                        link.costModifier = -1f;
                    }

                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    CloseSceneIfNeeded(scene, openedByIntegration);
                }
            }
        }

        private static int GetUniqueJunctionCount()
        {
            return s_junctions.Count(junction =>
                junction.SourceAreaIndex < junction.DestinationAreaIndex);
        }

        private static void PlaceStreamingTriggers(Terrain landTerrain)
        {
            GameObject triggerPrefab = LoadRequiredAsset<GameObject>(k_TriggerPrefabPath);
            for (int sourceAreaIndex = 0; sourceAreaIndex < s_areas.Length; sourceAreaIndex++)
            {
                AreaDefinition sourceArea = s_areas[sourceAreaIndex];
                Scene scene = OpenSceneIfNeeded(
                    GetSliceScenePath(sourceArea, k_SpawnersSliceIndex),
                    out bool openedByIntegration);
                try
                {
                    Transform areaRoot = EnsureAreaRoot(scene, sourceArea).transform;
                    DeleteNamedTransforms(scene, k_NatureTriggerRootName);
                    DeleteLegacyStreamingTriggers(scene);
                    GameObject triggerRoot = new(k_NatureTriggerRootName);
                    SceneManager.MoveGameObjectToScene(triggerRoot, scene);
                    triggerRoot.transform.SetParent(areaRoot, false);

                    foreach (JunctionDefinition junction in s_junctions
                                 .Where(junction => junction.SourceAreaIndex == sourceAreaIndex))
                    {
                        AreaDefinition destinationArea = s_areas[junction.DestinationAreaIndex];
                        WorldLocationSceneSet destination =
                            LoadRequiredAsset<WorldLocationSceneSet>(
                                GetWorldLocationPath(destinationArea));
                        GameObject instance =
                            (GameObject)PrefabUtility.InstantiatePrefab(triggerPrefab, scene);
                        instance.name =
                            $"Nature Trigger {sourceArea.RootName} -> {destinationArea.RootName}";
                        instance.transform.SetParent(triggerRoot.transform, true);
                        SetPositionOnTerrain(instance.transform, junction.Position, landTerrain, 2.5f);

                        EventTriggerLoadScene trigger =
                            instance.GetComponent<EventTriggerLoadScene>() ??
                            throw new InvalidOperationException(
                                $"Trigger prefab lacks {nameof(EventTriggerLoadScene)}.");
                        SerializedObject serializedTrigger = new(trigger);
                        RequireProperty(serializedTrigger, "m_worldLocation")
                            .objectReferenceValue = destination;
                        RequireProperty(serializedTrigger, "m_area").intValue =
                            (int)destinationArea.LegacyLocation;
                        serializedTrigger.ApplyModifiedPropertiesWithoutUndo();
                    }

                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    CloseSceneIfNeeded(scene, openedByIntegration);
                }
            }
        }

        private static void ConfigureNavigationSurface(
            Scene scene,
            AreaDefinition area,
            Bounds terrainBounds)
        {
            DeleteAllNavMeshLinks(scene);
            GameObject navigationRoot = EnsureSceneRoot(scene, k_NavigationRootName);
            NavMeshSurface surface = navigationRoot.GetComponent<NavMeshSurface>() ??
                navigationRoot.AddComponent<NavMeshSurface>();
            Vector3 worldCenter = new(
                area.Bounds.center.x,
                terrainBounds.center.y,
                area.Bounds.center.y);
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Volume;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.layerMask = 1 << 0;
            surface.center = navigationRoot.transform.InverseTransformPoint(worldCenter);
            surface.size = new Vector3(
                area.Bounds.width + k_NavHorizontalMargin * 2f,
                terrainBounds.size.y + k_NavVerticalMargin * 2f,
                area.Bounds.height + k_NavHorizontalMargin * 2f);
            EditorUtility.SetDirty(surface);
        }

        private static void ConfigureRendererManager(Scene scene, int sliceIndex)
        {
            GameObject managerObject = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == k_RendererManagerName) ??
                new GameObject(k_RendererManagerName);
            if (managerObject.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(managerObject, scene);
            }

            WorldLocationRendererManager manager =
                managerObject.GetComponent<WorldLocationRendererManager>() ??
                managerObject.AddComponent<WorldLocationRendererManager>();
            SerializedObject serializedManager = new(manager);
            RequireProperty(serializedManager, "m_rendererSceneID").intValue = scene.buildIndex;
            RequireProperty(serializedManager, "m_manageRootObjects").boolValue =
                sliceIndex != k_SpawnersSliceIndex;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            manager.RefreshSceneObjects();
            EditorUtility.SetDirty(manager);
        }

        private static void ValidateMasterEnvironment(Scene masterScene, ICollection<string> failures)
        {
            int worldAIManagerCount = GetSceneComponents<WorldAIManager>(masterScene).Count;
            if (worldAIManagerCount != 1)
            {
                failures.Add(
                    $"Master scene World AI Manager count {worldAIManagerCount} != expected 1.");
            }

            Transform importedRoot = FindTransform(masterScene, k_ImportedEnvironmentRootName);
            if (importedRoot == null)
            {
                failures.Add("Master scene is missing the imported environment root.");
                return;
            }

            string[] requiredNames =
            {
                "LandTerrain",
                "WaterTerrain",
                "Water",
                "Directional Light",
                "PostProcessing",
                "VegetationBendControl",
                "WindControl"
            };
            foreach (string requiredName in requiredNames)
            {
                if (importedRoot.GetComponentsInChildren<Transform>(true)
                    .All(transform => transform.name != requiredName))
                {
                    failures.Add($"Imported environment is missing {requiredName}.");
                }
            }

            if (importedRoot.GetComponentInChildren<Terrain>(true) == null)
            {
                failures.Add("Imported environment contains no Terrain component.");
            }
        }

        private static void EnsureWorldAIManager(Scene masterScene)
        {
            List<WorldAIManager> existingManagers =
                GetSceneComponents<WorldAIManager>(masterScene);
            for (int index = 1; index < existingManagers.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(existingManagers[index].gameObject);
            }

            GameObject managerObject;
            if (existingManagers.Count > 0)
            {
                managerObject = existingManagers[0].gameObject;
                managerObject.transform.SetParent(null, true);
            }
            else
            {
                GameObject managerPrefab = LoadRequiredAsset<GameObject>(
                    k_WorldAIManagerPrefabPath);
                managerObject = (GameObject)PrefabUtility.InstantiatePrefab(
                    managerPrefab,
                    masterScene);
            }

            managerObject.name = "World AI Manager";
            for (int childIndex = managerObject.transform.childCount - 1;
                 childIndex >= 0;
                 childIndex--)
            {
                Transform child = managerObject.transform.GetChild(childIndex);
                if (child.name != k_DialogueNpcSpawnerName)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            if (managerObject.transform.Find(k_DialogueNpcSpawnerName) == null)
            {
                GameObject managerPrefab = LoadRequiredAsset<GameObject>(
                    k_WorldAIManagerPrefabPath);
                Transform dialogueSpawnerPrefab =
                    managerPrefab.transform.Find(k_DialogueNpcSpawnerName) ??
                    throw new InvalidOperationException(
                        $"World AI Manager prefab lacks '{k_DialogueNpcSpawnerName}'.");
                GameObject dialogueSpawner = UnityEngine.Object.Instantiate(
                    dialogueSpawnerPrefab.gameObject);
                dialogueSpawner.name = k_DialogueNpcSpawnerName;
                dialogueSpawner.transform.SetParent(managerObject.transform, false);
            }

            EditorUtility.SetDirty(managerObject);
        }

        private static void EnsureSiteOfGraceTeleport(
            Transform gameplayObject,
            Terrain landTerrain)
        {
            SiteOfGraceInteractable site =
                gameplayObject.GetComponentInChildren<SiteOfGraceInteractable>(true);
            if (site == null)
            {
                return;
            }

            Transform teleportPoint = site.transform.Find("Teleport Point");
            if (teleportPoint == null)
            {
                GameObject teleportObject = new("Teleport Point");
                teleportPoint = teleportObject.transform;
                teleportPoint.SetParent(site.transform, false);
            }

            Vector3 directionToSpawn = s_playerSpawnPosition - site.transform.position;
            directionToSpawn.y = 0f;
            directionToSpawn = directionToSpawn.sqrMagnitude > 0.001f
                ? directionToSpawn.normalized
                : Vector3.back;
            Vector3 teleportPosition =
                site.transform.position + directionToSpawn * 2f;
            teleportPosition.y =
                landTerrain.SampleHeight(teleportPosition) +
                landTerrain.transform.position.y +
                0.1f;
            teleportPoint.SetPositionAndRotation(
                teleportPosition,
                s_playerSpawnRotation);

            SerializedObject serializedSite = new(site);
            RequireProperty(serializedSite, "m_teleportTransform")
                .objectReferenceValue = teleportPoint;
            serializedSite.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(site);
        }

        private static void ValidateSliceInfrastructure(
            Scene scene,
            AreaDefinition area,
            int sliceIndex,
            ICollection<string> failures)
        {
            if (FindTransform(scene, area.RootName) == null)
            {
                failures.Add($"{scene.name} is missing Area root {area.RootName}.");
            }

            WorldLocationRendererManager manager =
                GetSceneComponents<WorldLocationRendererManager>(scene).FirstOrDefault();
            if (manager == null)
            {
                failures.Add($"{scene.name} is missing its renderer manager.");
            }
            else if (manager.RendererSceneID != scene.buildIndex)
            {
                failures.Add(
                    $"{scene.name} renderer ID {manager.RendererSceneID} != build index " +
                    $"{scene.buildIndex}.");
            }

            if (sliceIndex == k_BaseSliceIndex)
            {
                NavMeshSurface surface = GetSceneComponents<NavMeshSurface>(scene).FirstOrDefault();
                if (surface == null || surface.size.sqrMagnitude <= 0f)
                {
                    failures.Add($"{scene.name} has no configured NavMeshSurface volume.");
                }
            }
        }

        private static GameObject MoveDemoObject(
            Scene sourceScene,
            Scene destinationScene,
            string hierarchyPath,
            Transform destinationParent)
        {
            Transform source = FindTransformByPath(sourceScene, hierarchyPath) ??
                throw new InvalidOperationException(
                    $"Demo object '{hierarchyPath}' is missing.");
            source.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(source.gameObject, destinationScene);
            source.SetParent(destinationParent, true);
            return source.gameObject;
        }

        private static void ResetDemoScene(Scene demoScene, bool wasOpen, bool wasActive)
        {
            if (!demoScene.IsValid() || !demoScene.isLoaded)
            {
                return;
            }

            EditorSceneManager.CloseScene(demoScene, true);
            if (!wasOpen)
            {
                return;
            }

            Scene reloadedDemo = EditorSceneManager.OpenScene(k_DemoScenePath, OpenSceneMode.Additive);
            if (wasActive)
            {
                SceneManager.SetActiveScene(reloadedDemo);
            }
        }

        private static EnvironmentSettings CaptureEnvironmentSettings()
        {
            return new EnvironmentSettings(
                RenderSettings.skybox,
                RenderSettings.fog,
                RenderSettings.fogColor,
                RenderSettings.fogMode,
                RenderSettings.fogDensity,
                RenderSettings.fogStartDistance,
                RenderSettings.fogEndDistance,
                RenderSettings.ambientMode,
                RenderSettings.ambientSkyColor,
                RenderSettings.ambientEquatorColor,
                RenderSettings.ambientGroundColor,
                RenderSettings.ambientIntensity,
                RenderSettings.defaultReflectionMode,
                RenderSettings.customReflectionTexture,
                RenderSettings.reflectionIntensity,
                RenderSettings.reflectionBounces,
                RenderSettings.subtractiveShadowColor,
                RenderSettings.haloStrength,
                RenderSettings.flareStrength,
                RenderSettings.flareFadeSpeed);
        }

        private static Terrain FindLandTerrain(Scene masterScene)
        {
            Transform terrainTransform = FindTransform(masterScene, "LandTerrain") ??
                throw new InvalidOperationException(
                    "LandTerrain is missing. Run Move Environment To Master first.");
            return terrainTransform.GetComponent<Terrain>() ??
                throw new InvalidOperationException("LandTerrain has no Terrain component.");
        }

        private static void EnsureLandTerrainCollision(Terrain terrain)
        {
            bool changed = false;
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider == null)
            {
                terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
                changed = true;
            }

            if (terrainCollider.terrainData != terrain.terrainData)
            {
                terrainCollider.terrainData = terrain.terrainData;
                changed = true;
            }

            if (!terrainCollider.enabled)
            {
                terrainCollider.enabled = true;
                changed = true;
            }

            if (terrain.gameObject.layer != 0)
            {
                terrain.gameObject.layer = 0;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(terrainCollider);
                EditorUtility.SetDirty(terrain.gameObject);
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0 && Physics.GetIgnoreLayerCollision(0, playerLayer))
            {
                Physics.IgnoreLayerCollision(0, playerLayer, false);
                Debug.LogWarning(
                    "[LV01Nature] Re-enabled collision between Default terrain and Player layers.");
            }
        }

        private static Bounds GetTerrainWorldBounds(Terrain terrain)
        {
            Vector3 size = terrain.terrainData.size;
            return new Bounds(terrain.transform.position + size * 0.5f, size);
        }

        private static void SetPositionOnTerrain(
            Transform target,
            Vector2 position,
            Terrain terrain,
            float yOffset)
        {
            Vector3 samplePosition = new(position.x, terrain.transform.position.y, position.y);
            float groundY = terrain.SampleHeight(samplePosition) + terrain.transform.position.y;
            target.position = new Vector3(position.x, groundY + yOffset, position.y);
        }

        private static void SetPositionOnSafeTerrain(
            Transform target,
            Vector2 requestedPosition,
            Rect areaBounds,
            Terrain terrain,
            float yOffset)
        {
            Physics.SyncTransforms();
            foreach (Vector2 candidate in GetPlacementCandidates(requestedPosition))
            {
                if (!areaBounds.Contains(candidate))
                {
                    continue;
                }

                float groundY = GetTerrainGroundY(terrain, candidate);
                if (GetTerrainSlope(terrain, candidate) > k_MaxPlacementSlope ||
                    IsPlacementBlocked(target, candidate, groundY))
                {
                    continue;
                }

                target.position = new Vector3(candidate.x, groundY + yOffset, candidate.y);
                Physics.SyncTransforms();
                return;
            }

            Vector2 fallback = new(
                Mathf.Clamp(requestedPosition.x, areaBounds.xMin, areaBounds.xMax),
                Mathf.Clamp(requestedPosition.y, areaBounds.yMin, areaBounds.yMax));
            target.position = new Vector3(
                fallback.x,
                GetTerrainGroundY(terrain, fallback) + yOffset,
                fallback.y);
            Debug.LogWarning(
                $"[LV01Nature] No obstacle-free flat point found near {requestedPosition} " +
                $"for {target.name}; used terrain fallback {fallback}.");
            Physics.SyncTransforms();
        }

        private static List<Vector2> GeneratePoissonPoints(
            AreaDefinition area,
            Terrain terrain,
            int requestedCount,
            int seed,
            IReadOnlyCollection<Vector2> occupiedPoints,
            IReadOnlyCollection<Vector2> exclusionPoints)
        {
            var generatedPoints = new List<Vector2>(requestedCount);
            if (requestedCount <= 0)
            {
                return generatedPoints;
            }

            float cellSize = k_EnemyMinimumSpacing / Mathf.Sqrt(2f);
            var spatialGrid = new Dictionary<Vector2Int, List<Vector2>>();
            foreach (Vector2 occupiedPoint in occupiedPoints)
            {
                AddPointToSpatialGrid(occupiedPoint, cellSize, spatialGrid);
            }

            var random = new System.Random(seed);
            var activePoints = new List<Vector2>();
            int restartAttempts = 0;
            int maximumRestartAttempts = Mathf.Max(256, requestedCount * 64);
            while (generatedPoints.Count < requestedCount &&
                   restartAttempts < maximumRestartAttempts)
            {
                if (activePoints.Count == 0)
                {
                    if (!TryFindPoissonSeed(
                            area,
                            terrain,
                            random,
                            cellSize,
                            spatialGrid,
                            exclusionPoints,
                            out Vector2 seedPoint))
                    {
                        restartAttempts++;
                        continue;
                    }

                    RegisterGeneratedPoint(
                        seedPoint,
                        cellSize,
                        generatedPoints,
                        activePoints,
                        spatialGrid);
                    continue;
                }

                int activeIndex = random.Next(activePoints.Count);
                Vector2 origin = activePoints[activeIndex];
                bool placedCandidate = false;
                for (int attempt = 0;
                     attempt < k_PoissonCandidatesPerPoint;
                     attempt++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float distance = k_EnemyMinimumSpacing *
                        (1f + (float)random.NextDouble());
                    Vector2 candidate = origin + new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)) * distance;
                    if (!IsValidPoissonPoint(
                            candidate,
                            area,
                            terrain,
                            cellSize,
                            spatialGrid,
                            exclusionPoints))
                    {
                        continue;
                    }

                    RegisterGeneratedPoint(
                        candidate,
                        cellSize,
                        generatedPoints,
                        activePoints,
                        spatialGrid);
                    placedCandidate = true;
                    break;
                }

                if (!placedCandidate)
                {
                    activePoints.RemoveAt(activeIndex);
                    restartAttempts++;
                }
            }

            if (generatedPoints.Count < requestedCount)
            {
                Debug.LogWarning(
                    $"[LV01Nature] {area.RootName} accepted {generatedPoints.Count} of " +
                    $"{requestedCount} requested enemies after bounded Poisson sampling.");
            }

            return generatedPoints;
        }

        private static bool TryFindPoissonSeed(
            AreaDefinition area,
            Terrain terrain,
            System.Random random,
            float cellSize,
            IReadOnlyDictionary<Vector2Int, List<Vector2>> spatialGrid,
            IReadOnlyCollection<Vector2> exclusionPoints,
            out Vector2 seedPoint)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                Vector2 candidate = new(
                    Mathf.Lerp(
                        area.Bounds.xMin,
                        area.Bounds.xMax,
                        (float)random.NextDouble()),
                    Mathf.Lerp(
                        area.Bounds.yMin,
                        area.Bounds.yMax,
                        (float)random.NextDouble()));
                if (IsValidPoissonPoint(
                        candidate,
                        area,
                        terrain,
                        cellSize,
                        spatialGrid,
                        exclusionPoints))
                {
                    seedPoint = candidate;
                    return true;
                }
            }

            seedPoint = default;
            return false;
        }

        private static bool IsValidPoissonPoint(
            Vector2 candidate,
            AreaDefinition area,
            Terrain terrain,
            float cellSize,
            IReadOnlyDictionary<Vector2Int, List<Vector2>> spatialGrid,
            IReadOnlyCollection<Vector2> exclusionPoints)
        {
            if (!area.Bounds.Contains(candidate) ||
                exclusionPoints.Any(point =>
                    (point - candidate).sqrMagnitude <
                    k_SpawnEnemyExclusionRadius * k_SpawnEnemyExclusionRadius) ||
                GetTerrainSlope(terrain, candidate) > k_MaxPlacementSlope)
            {
                return false;
            }

            float groundY = GetTerrainGroundY(terrain, candidate);
            if (IsPlacementBlocked(null, candidate, groundY))
            {
                return false;
            }

            Vector2Int cell = GetSpatialGridCell(candidate, cellSize);
            for (int x = cell.x - 2; x <= cell.x + 2; x++)
            {
                for (int y = cell.y - 2; y <= cell.y + 2; y++)
                {
                    if (!spatialGrid.TryGetValue(
                            new Vector2Int(x, y),
                            out List<Vector2> neighbors))
                    {
                        continue;
                    }

                    if (neighbors.Any(point =>
                            (point - candidate).sqrMagnitude <
                            k_EnemyMinimumSpacing * k_EnemyMinimumSpacing))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void RegisterGeneratedPoint(
            Vector2 point,
            float cellSize,
            ICollection<Vector2> generatedPoints,
            ICollection<Vector2> activePoints,
            IDictionary<Vector2Int, List<Vector2>> spatialGrid)
        {
            generatedPoints.Add(point);
            activePoints.Add(point);
            AddPointToSpatialGrid(point, cellSize, spatialGrid);
        }

        private static void AddPointToSpatialGrid(
            Vector2 point,
            float cellSize,
            IDictionary<Vector2Int, List<Vector2>> spatialGrid)
        {
            Vector2Int cell = GetSpatialGridCell(point, cellSize);
            if (!spatialGrid.TryGetValue(cell, out List<Vector2> points))
            {
                points = new List<Vector2>(1);
                spatialGrid.Add(cell, points);
            }

            points.Add(point);
        }

        private static Vector2Int GetSpatialGridCell(Vector2 point, float cellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(point.x / cellSize),
                Mathf.FloorToInt(point.y / cellSize));
        }

        private static AICharacterSpawner CreateCharacterSpawner(
            Scene scene,
            Transform parent,
            string objectName,
            GameObject characterPrefab,
            int bossID,
            bool willInvestigateSound)
        {
            GameObject spawnerObject = new(objectName);
            SceneManager.MoveGameObjectToScene(spawnerObject, scene);
            spawnerObject.transform.SetParent(parent, false);
            AICharacterSpawner spawner =
                spawnerObject.AddComponent<AICharacterSpawner>();
            SerializedObject serializedSpawner = new(spawner);
            RequireProperty(serializedSpawner, "m_characterGameObject")
                .objectReferenceValue = characterPrefab;
            RequireProperty(serializedSpawner, "m_patrolPathID").intValue = 0;
            RequireProperty(serializedSpawner, "m_repeatPatrol").boolValue = false;
            RequireProperty(serializedSpawner, "m_isSleeping").boolValue = false;
            RequireProperty(serializedSpawner, "m_willInvestigateSound")
                .boolValue = willInvestigateSound;
            RequireProperty(serializedSpawner, "m_bossID").intValue = bossID;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        private static GameObject GetSpawnerCharacterPrefab(
            AICharacterSpawner spawner)
        {
            SerializedObject serializedSpawner = new(spawner);
            return RequireProperty(serializedSpawner, "m_characterGameObject")
                .objectReferenceValue as GameObject;
        }

        private static void FaceTransformTowards(Transform target, Vector3 position)
        {
            Vector3 direction = position - target.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                target.rotation = Quaternion.LookRotation(direction);
            }
        }

        private static IEnumerable<Vector2> GetPlacementCandidates(Vector2 origin)
        {
            yield return origin;
            for (float radius = k_PlacementSearchStep;
                 radius <= k_PlacementSearchRadius;
                 radius += k_PlacementSearchStep)
            {
                int sampleCount = Mathf.Max(8, Mathf.CeilToInt(radius * 2f));
                for (int index = 0; index < sampleCount; index++)
                {
                    float angle = Mathf.PI * 2f * index / sampleCount;
                    yield return origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
            }
        }

        private static float GetTerrainGroundY(Terrain terrain, Vector2 position)
        {
            Vector3 worldPosition = new(position.x, terrain.transform.position.y, position.y);
            return terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
        }

        private static float GetTerrainSlope(Terrain terrain, Vector2 position)
        {
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            float normalizedX = Mathf.InverseLerp(
                terrainPosition.x,
                terrainPosition.x + terrainSize.x,
                position.x);
            float normalizedZ = Mathf.InverseLerp(
                terrainPosition.z,
                terrainPosition.z + terrainSize.z,
                position.y);
            Vector3 normal = terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
            return Vector3.Angle(normal, Vector3.up);
        }

        private static bool IsPlacementBlocked(
            Transform target,
            Vector2 position,
            float groundY)
        {
            Vector3 bottom = new(position.x, groundY + k_PlacementClearanceRadius, position.y);
            Vector3 top = new(
                position.x,
                groundY + k_PlacementClearanceHeight - k_PlacementClearanceRadius,
                position.y);
            return Physics.OverlapCapsule(
                    bottom,
                    top,
                    k_PlacementClearanceRadius,
                    ~0,
                    QueryTriggerInteraction.Ignore)
                .Any(collider =>
                    collider is not TerrainCollider &&
                    (target == null ||
                     (collider.transform != target &&
                      !collider.transform.IsChildOf(target))));
        }

        private static void EnsureNatureSolidColliders()
        {
            int auditedCount = 0;
            int addedCount = 0;
            foreach (AreaDefinition area in s_areas)
            {
                for (int sliceIndex = k_BaseSliceIndex;
                     sliceIndex <= k_PropsSliceIndex;
                     sliceIndex++)
                {
                    Scene scene = OpenSceneIfNeeded(
                        GetSliceScenePath(area, sliceIndex),
                        out bool openedByIntegration);
                    try
                    {
                        foreach (LODGroup lodGroup in GetSceneComponents<LODGroup>(scene))
                        {
                            bool shouldBeSolid = sliceIndex == k_BaseSliceIndex ||
                                lodGroup.name.Contains("Tree", StringComparison.OrdinalIgnoreCase);
                            if (!shouldBeSolid)
                            {
                                continue;
                            }

                            auditedCount++;
                            if (lodGroup.GetComponentInChildren<Collider>(true) != null)
                            {
                                continue;
                            }

                            Renderer collisionRenderer = lodGroup.GetLODs()
                                .SelectMany(lod => lod.renderers)
                                .FirstOrDefault(renderer =>
                                    renderer != null && renderer.GetComponent<MeshFilter>()?.sharedMesh != null);
                            MeshFilter meshFilter = collisionRenderer?.GetComponent<MeshFilter>();
                            if (meshFilter == null)
                            {
                                Debug.LogWarning(
                                    $"[LV01Nature] No collision mesh found for {lodGroup.name} in {scene.name}.");
                                continue;
                            }

                            MeshCollider meshCollider =
                                collisionRenderer.gameObject.AddComponent<MeshCollider>();
                            meshCollider.sharedMesh = meshFilter.sharedMesh;
                            meshCollider.convex = false;
                            EditorUtility.SetDirty(meshCollider);
                            addedCount++;
                        }

                        if (addedCount > 0)
                        {
                            EditorSceneManager.MarkSceneDirty(scene);
                            EditorSceneManager.SaveScene(scene);
                        }
                    }
                    finally
                    {
                        CloseSceneIfNeeded(scene, openedByIntegration);
                    }
                }
            }

            Debug.Log(
                $"[LV01Nature] Audited {auditedCount} solid cliffs, rocks, and trees; " +
                $"added {addedCount} missing MeshColliders.");
        }

        private static void ValidateSpawnCollision()
        {
            EnsureNoDirtyScenes();
            Scene masterScene = OpenSceneIfNeeded(
                WorldScenePathLayout.MasterScenePath,
                out bool masterOpenedByIntegration);
            AreaDefinition spawnArea = s_areas[0];
            Scene baseScene = OpenSceneIfNeeded(
                GetSliceScenePath(spawnArea, k_BaseSliceIndex),
                out bool baseOpenedByIntegration);
            Scene propsScene = OpenSceneIfNeeded(
                GetSliceScenePath(spawnArea, k_PropsSliceIndex),
                out bool propsOpenedByIntegration);
            try
            {
                Terrain landTerrain = FindLandTerrain(masterScene);
                EnsureLandTerrainCollision(landTerrain);
                Transform playerSpawn = FindTransform(masterScene, "Player Spawn Point") ??
                    throw new InvalidOperationException("Player Spawn Point is missing.");
                Transform initialTrigger = FindTransform(masterScene, "Spawn Area Load Trigger") ??
                    throw new InvalidOperationException("Spawn Area Load Trigger is missing.");
                Collider triggerCollider = initialTrigger.GetComponent<Collider>() ??
                    throw new InvalidOperationException("Spawn Area Load Trigger has no Collider.");

                Physics.SyncTransforms();
                RaycastHit support = Physics.RaycastAll(
                        playerSpawn.position + Vector3.up * 1000f,
                        Vector3.down,
                        2000f,
                        ~0,
                        QueryTriggerInteraction.Ignore)
                    .Where(hit => hit.point.y <= playerSpawn.position.y + 0.25f)
                    .OrderByDescending(hit => hit.point.y)
                    .FirstOrDefault();
                if (support.collider == null)
                {
                    throw new InvalidOperationException(
                        $"No solid collider exists below player spawn {playerSpawn.position}.");
                }

                float supportGap = playerSpawn.position.y - support.point.y;
                if (supportGap < -0.25f || supportGap > 3f)
                {
                    throw new InvalidOperationException(
                        $"Player spawn is {supportGap:F2}m above support collider " +
                        $"'{support.collider.name}', outside the safe range.");
                }

                if (!triggerCollider.bounds.Contains(playerSpawn.position))
                {
                    throw new InvalidOperationException(
                        "Spawn Area Load Trigger does not contain Player Spawn Point.");
                }

                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0 && Physics.GetIgnoreLayerCollision(0, playerLayer))
                {
                    throw new InvalidOperationException(
                        "Default terrain layer does not collide with Player layer.");
                }

                Debug.Log(
                    $"[LV01Nature] Spawn collision passed: {playerSpawn.position}, " +
                    $"support={support.collider.name}, gap={supportGap:F2}m, " +
                    "initial A01 trigger overlaps the spawn.");
            }
            finally
            {
                CloseSceneIfNeeded(propsScene, propsOpenedByIntegration);
                CloseSceneIfNeeded(baseScene, baseOpenedByIntegration);
                CloseSceneIfNeeded(masterScene, masterOpenedByIntegration);
            }
        }

        private static int FindOwningArea(Vector3 position, out bool isInsideArea)
        {
            Vector2 point = new(position.x, position.z);
            int closestArea = -1;
            float closestScore = float.PositiveInfinity;
            isInsideArea = false;

            for (int index = 0; index < s_areas.Length; index++)
            {
                Rect bounds = s_areas[index].Bounds;
                if (!bounds.Contains(point))
                {
                    continue;
                }

                float normalizedX = (point.x - bounds.center.x) / Mathf.Max(bounds.width, 0.001f);
                float normalizedZ = (point.y - bounds.center.y) / Mathf.Max(bounds.height, 0.001f);
                float score = normalizedX * normalizedX + normalizedZ * normalizedZ;
                if (score < closestScore)
                {
                    closestArea = index;
                    closestScore = score;
                }
            }

            if (closestArea >= 0)
            {
                isInsideArea = true;
                return closestArea;
            }

            for (int index = 0; index < s_areas.Length; index++)
            {
                Rect bounds = s_areas[index].Bounds;
                Vector2 closestPoint = new(
                    Mathf.Clamp(point.x, bounds.xMin, bounds.xMax),
                    Mathf.Clamp(point.y, bounds.yMin, bounds.yMax));
                float score = (point - closestPoint).sqrMagnitude;
                if (score < closestScore)
                {
                    closestArea = index;
                    closestScore = score;
                }
            }

            return closestArea;
        }

        private static List<Transform>[] CreateAreaBuckets()
        {
            var buckets = new List<Transform>[s_areas.Length];
            for (int index = 0; index < buckets.Length; index++)
            {
                buckets[index] = new List<Transform>();
            }

            return buckets;
        }

        private static IEnumerable<string> GetAllSliceScenePaths()
        {
            return s_areas.SelectMany(
                area => Enumerable.Range(0, k_SliceCount)
                    .Select(sliceIndex => GetSliceScenePath(area, sliceIndex)));
        }

        private static string GetSliceScenePath(AreaDefinition area, int sliceIndex)
        {
            return WorldScenePathLayout.GetScenePath(
                area.RegionIndex,
                area.SceneAreaIndex,
                sliceIndex);
        }

        private static string GetNavigationAssetPath(AreaDefinition area)
        {
            string baseSceneDirectory = Path.GetDirectoryName(
                    GetSliceScenePath(area, k_BaseSliceIndex))
                ?.Replace('\\', '/') ??
                throw new InvalidOperationException(
                    $"Could not resolve the scene directory for {area.RootName}.");
            string navigationDirectory = area.RegionIndex == 0
                ? $"{baseSceneDirectory}/{area.RootName}/Navigation"
                : $"{baseSceneDirectory}/Navigation";
            return $"{navigationDirectory}/NAV_LV01_R{area.RegionIndex + 1:D2}_" +
                $"A{area.SceneAreaIndex + 1:D2}.asset";
        }

        private static string GetWorldLocationPath(AreaDefinition area)
        {
            return $"{k_WorldLocationFolder}/{area.WorldLocationAssetName}";
        }

        private static GameObject EnsureAreaRoot(Scene scene, AreaDefinition area)
        {
            Transform existing = FindTransform(scene, area.RootName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject root = new(area.RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static GameObject EnsureSceneRoot(Scene scene, string rootName)
        {
            GameObject root = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == rootName);
            if (root == null)
            {
                root = new GameObject(rootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            return root;
        }

        private static void DeleteAllChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
            }
        }

        private static void DeleteNamedTransforms(Scene scene, string objectName)
        {
            foreach (Transform transform in GetSceneTransforms(scene)
                         .Where(transform => transform.name == objectName)
                         .OrderByDescending(GetHierarchyDepth)
                         .ToList())
            {
                if (transform != null)
                {
                    UnityEngine.Object.DestroyImmediate(transform.gameObject);
                }
            }
        }

        private static void DeleteLegacyStreamingTriggers(Scene scene)
        {
            foreach (Transform transform in GetSceneTransforms(scene)
                         .Where(transform => transform.name.StartsWith(
                             "Area Trigger ",
                             StringComparison.Ordinal))
                         .OrderByDescending(GetHierarchyDepth)
                         .ToList())
            {
                if (transform != null)
                {
                    UnityEngine.Object.DestroyImmediate(transform.gameObject);
                }
            }
        }

        private static void DeleteAllNavMeshLinks(Scene scene)
        {
            foreach (NavMeshLink link in GetSceneComponents<NavMeshLink>(scene).ToList())
            {
                if (link != null)
                {
                    UnityEngine.Object.DestroyImmediate(link.gameObject);
                }
            }

            DeleteNamedTransforms(scene, k_NatureLinkRootName);
        }

        private static bool ContainsGameplayComponent(Transform transform)
        {
            return transform.GetComponentInChildren<AICharacterSpawner>(true) != null ||
                transform.GetComponentInChildren<SiteOfGraceInteractable>(true) != null ||
                transform.GetComponentInChildren<EventTriggerLoadScene>(true) != null;
        }

        private static int GetHierarchyDepth(Transform transform)
        {
            int depth = 0;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }

        private static List<Transform> GetSceneTransforms(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Distinct()
                .ToList();
        }

        private static List<T> GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .Distinct()
                .ToList();
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            return GetSceneTransforms(scene)
                .FirstOrDefault(transform => transform.name == objectName);
        }

        private static Transform MoveGameplayObjectFromAnotherSpawnerScene(
            GameplayPlacement placement,
            string legacyGameplayName,
            Scene targetScene)
        {
            foreach (AreaDefinition sourceArea in s_areas)
            {
                string sourceScenePath = GetSliceScenePath(
                    sourceArea,
                    k_SpawnersSliceIndex);
                if (sourceScenePath == targetScene.path)
                {
                    continue;
                }

                Scene sourceScene = OpenSceneIfNeeded(
                    sourceScenePath,
                    out bool openedByIntegration);
                Transform movedObject = null;
                try
                {
                    Transform candidate =
                        FindTransform(sourceScene, placement.OriginalName) ??
                        FindTransform(sourceScene, placement.NewName) ??
                        FindTransform(sourceScene, legacyGameplayName);
                    if (candidate == null)
                    {
                        continue;
                    }

                    candidate.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(
                        candidate.gameObject,
                        targetScene);
                    EditorSceneManager.MarkSceneDirty(sourceScene);
                    EditorSceneManager.SaveScene(sourceScene);
                    movedObject = candidate;
                }
                finally
                {
                    CloseSceneIfNeeded(sourceScene, openedByIntegration);
                }

                if (movedObject != null)
                {
                    return movedObject;
                }
            }

            return null;
        }

        private static Transform FindTransformByPath(Scene scene, string hierarchyPath)
        {
            string[] segments = hierarchyPath.Split('/');
            Transform current = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == segments[0])
                ?.transform;
            for (int index = 1; index < segments.Length && current != null; index++)
            {
                current = current.Cast<Transform>()
                    .FirstOrDefault(child => child.name == segments[index]);
            }

            return current;
        }

        private static Transform RequireTransform(Scene scene, string hierarchyPath)
        {
            return FindTransformByPath(scene, hierarchyPath) ??
                throw new InvalidOperationException(
                    $"Scene {scene.name} is missing '{hierarchyPath}'.");
        }

        private static Scene OpenSceneIfNeeded(string scenePath, out bool openedByIntegration)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                openedByIntegration = false;
                return scene;
            }

            openedByIntegration = true;
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        private static void CloseSceneIfNeeded(Scene scene, bool openedByIntegration)
        {
            if (openedByIntegration && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void RestoreActiveScene(Scene originalActiveScene)
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }
        }

        private static void RefreshRendererManagers(Scene scene)
        {
            foreach (WorldLocationRendererManager manager in
                     GetSceneComponents<WorldLocationRendererManager>(scene))
            {
                manager.RefreshSceneObjects();
                EditorUtility.SetDirty(manager);
            }
        }

        private static void EnsureNoDirtyScenes()
        {
            for (int index = 0; index < EditorSceneManager.sceneCount; index++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new InvalidOperationException(
                        $"Open scene '{scene.name}' has unsaved changes. Save or discard it first.");
                }
            }
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required {typeof(T).Name} asset is missing: {assetPath}");
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} lacks {propertyName}.");
        }

        private static void RequireFile(string projectRelativePath)
        {
            if (!File.Exists(projectRelativePath))
            {
                throw new FileNotFoundException(
                    $"Required file is missing: {projectRelativePath}",
                    projectRelativePath);
            }
        }

        private static string ToAbsoluteProjectPath(string projectRoot, string projectRelativePath)
        {
            return Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private readonly struct AreaDefinition
        {
            public AreaDefinition(
                int regionIndex,
                int sceneAreaIndex,
                string rootName,
                string locationID,
                WorldSceneLocation legacyLocation,
                string worldLocationAssetName,
                Rect bounds)
            {
                RegionIndex = regionIndex;
                SceneAreaIndex = sceneAreaIndex;
                RootName = rootName;
                LocationID = locationID;
                LegacyLocation = legacyLocation;
                WorldLocationAssetName = worldLocationAssetName;
                Bounds = bounds;
            }

            public int RegionIndex { get; }
            public int SceneAreaIndex { get; }
            public string RootName { get; }
            public string LocationID { get; }
            public WorldSceneLocation LegacyLocation { get; }
            public string WorldLocationAssetName { get; }
            public Rect Bounds { get; }
        }

        private readonly struct NatureCategory
        {
            public NatureCategory(string sourceRootName, int sliceIndex, int expectedCount)
            {
                SourceRootName = sourceRootName;
                SliceIndex = sliceIndex;
                ExpectedCount = expectedCount;
            }

            public string SourceRootName { get; }
            public int SliceIndex { get; }
            public int ExpectedCount { get; }
            public string GeneratedRootName => $"Nature - {SourceRootName}";
        }

        private readonly struct GameplayPlacement
        {
            public GameplayPlacement(
                int areaIndex,
                string originalName,
                string newName,
                Vector2 position)
            {
                AreaIndex = areaIndex;
                OriginalName = originalName;
                NewName = newName;
                Position = position;
            }

            public int AreaIndex { get; }
            public string OriginalName { get; }
            public string NewName { get; }
            public Vector2 Position { get; }
        }

        private readonly struct ItemPlacement
        {
            public ItemPlacement(int areaIndex, string prefabPath, string name, Vector2 position)
            {
                AreaIndex = areaIndex;
                PrefabPath = prefabPath;
                Name = name;
                Position = position;
            }

            public int AreaIndex { get; }
            public string PrefabPath { get; }
            public string Name { get; }
            public Vector2 Position { get; }
        }

        private readonly struct BossPlacement
        {
            public BossPlacement(
                int areaIndex,
                int bossID,
                string name,
                Vector2 position)
            {
                AreaIndex = areaIndex;
                BossID = bossID;
                Name = name;
                Position = position;
            }

            public int AreaIndex { get; }
            public int BossID { get; }
            public string Name { get; }
            public Vector2 Position { get; }
        }

        private readonly struct JunctionDefinition
        {
            public JunctionDefinition(
                int sourceAreaIndex,
                int destinationAreaIndex,
                Vector2 position)
            {
                SourceAreaIndex = sourceAreaIndex;
                DestinationAreaIndex = destinationAreaIndex;
                Position = position;
            }

            public int SourceAreaIndex { get; }
            public int DestinationAreaIndex { get; }
            public Vector2 Position { get; }
        }

        private readonly struct EnvironmentSettings
        {
            private readonly Material m_skybox;
            private readonly bool m_fog;
            private readonly Color m_fogColor;
            private readonly FogMode m_fogMode;
            private readonly float m_fogDensity;
            private readonly float m_fogStartDistance;
            private readonly float m_fogEndDistance;
            private readonly AmbientMode m_ambientMode;
            private readonly Color m_ambientSkyColor;
            private readonly Color m_ambientEquatorColor;
            private readonly Color m_ambientGroundColor;
            private readonly float m_ambientIntensity;
            private readonly DefaultReflectionMode m_defaultReflectionMode;
            private readonly Texture m_customReflectionTexture;
            private readonly float m_reflectionIntensity;
            private readonly int m_reflectionBounces;
            private readonly Color m_subtractiveShadowColor;
            private readonly float m_haloStrength;
            private readonly float m_flareStrength;
            private readonly float m_flareFadeSpeed;

            public EnvironmentSettings(
                Material skybox,
                bool fog,
                Color fogColor,
                FogMode fogMode,
                float fogDensity,
                float fogStartDistance,
                float fogEndDistance,
                AmbientMode ambientMode,
                Color ambientSkyColor,
                Color ambientEquatorColor,
                Color ambientGroundColor,
                float ambientIntensity,
                DefaultReflectionMode defaultReflectionMode,
                Texture customReflectionTexture,
                float reflectionIntensity,
                int reflectionBounces,
                Color subtractiveShadowColor,
                float haloStrength,
                float flareStrength,
                float flareFadeSpeed)
            {
                m_skybox = skybox;
                m_fog = fog;
                m_fogColor = fogColor;
                m_fogMode = fogMode;
                m_fogDensity = fogDensity;
                m_fogStartDistance = fogStartDistance;
                m_fogEndDistance = fogEndDistance;
                m_ambientMode = ambientMode;
                m_ambientSkyColor = ambientSkyColor;
                m_ambientEquatorColor = ambientEquatorColor;
                m_ambientGroundColor = ambientGroundColor;
                m_ambientIntensity = ambientIntensity;
                m_defaultReflectionMode = defaultReflectionMode;
                m_customReflectionTexture = customReflectionTexture;
                m_reflectionIntensity = reflectionIntensity;
                m_reflectionBounces = reflectionBounces;
                m_subtractiveShadowColor = subtractiveShadowColor;
                m_haloStrength = haloStrength;
                m_flareStrength = flareStrength;
                m_flareFadeSpeed = flareFadeSpeed;
            }

            public void Apply(Light sun)
            {
                RenderSettings.skybox = m_skybox;
                RenderSettings.sun = sun;
                RenderSettings.fog = m_fog;
                RenderSettings.fogColor = m_fogColor;
                RenderSettings.fogMode = m_fogMode;
                RenderSettings.fogDensity = m_fogDensity;
                RenderSettings.fogStartDistance = m_fogStartDistance;
                RenderSettings.fogEndDistance = m_fogEndDistance;
                RenderSettings.ambientMode = m_ambientMode;
                RenderSettings.ambientSkyColor = m_ambientSkyColor;
                RenderSettings.ambientEquatorColor = m_ambientEquatorColor;
                RenderSettings.ambientGroundColor = m_ambientGroundColor;
                RenderSettings.ambientIntensity = m_ambientIntensity;
                RenderSettings.defaultReflectionMode = m_defaultReflectionMode;
                RenderSettings.customReflectionTexture = m_customReflectionTexture;
                RenderSettings.reflectionIntensity = m_reflectionIntensity;
                RenderSettings.reflectionBounces = m_reflectionBounces;
                RenderSettings.subtractiveShadowColor = m_subtractiveShadowColor;
                RenderSettings.haloStrength = m_haloStrength;
                RenderSettings.flareStrength = m_flareStrength;
                RenderSettings.flareFadeSpeed = m_flareFadeSpeed;
            }
        }
    }
}
