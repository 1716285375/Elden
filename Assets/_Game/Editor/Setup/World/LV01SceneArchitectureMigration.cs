using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// One-shot tooling for the LV01 scene architecture migration (Phase 1, R01).
    /// Splits the shared R01_A01 slice content into per-Area slice scenes, rebuilds
    /// the Master scene roots, and wires one streaming unit per Area. Invoke the
    /// public static methods via reflection; each method is re-entrant.
    ///
    /// Intended order:
    /// 1. DumpLegacyAudit            (read-only report)
    /// 2. CreateAreaSliceScenes(1..3)
    /// 3. SplitAreaContent(1..3)
    /// 4. ConfigureAreaSceneSets
    /// 5. RestructureMasterScene
    /// 6. PlaceAreaTriggers
    /// 7. VerifySplitPositions(1..3) + VerifyBuildIndexes
    /// </summary>
    public static class LV01SceneArchitectureMigration
    {
        private const string k_MasterScenePath = WorldScenePathLayout.MasterScenePath;
        private const string k_BakingSetPath =
            "Assets/_Game/Settings/Rendering/Lighting/World Streaming Probe Volume Baking Set.asset";
        private const string k_WorldLocationsFolder =
            "Assets/_Game/Resources/World Locations";
        private const string k_TriggerPrefabPath =
            "Assets/_Game/Prefabs/World/Streaming/Area Load Trigger.prefab";
        private const string k_LocationTriggerFolder =
            "Assets/_Game/Prefabs/World/Streaming/World Location Triggers";
        private const string k_LayoutAssetPath =
            "Assets/_Game/Data/LevelDesign/LV01_GreyboxLayout.asset";

        /// <summary>Destination index 4 addresses R02's region Scene Set.</summary>
        private const int k_DestinationR02 = 4;

        private const int k_RegionOutskirts = 0;

        private static readonly string[] s_sliceNames =
        {
            "Base", "Props", "Effects", "Spawners"
        };

        private static readonly string[] s_areaNames =
        {
            LV01GreyboxSpec.CliffPath,
            LV01GreyboxSpec.Graveyard,
            LV01GreyboxSpec.MainGate,
            LV01GreyboxSpec.GateTower
        };

        private static readonly string[] s_areaLocationIDs =
        {
            "Area_01_Sub_Area_00",
            "Area_01_Sub_Area_05",
            "Area_01_Sub_Area_06",
            "Area_01_Sub_Area_07"
        };

        private static readonly WorldSceneLocation[] s_areaLegacyLocations =
        {
            WorldSceneLocation.Area01SubArea00,
            WorldSceneLocation.Area01SubArea05,
            WorldSceneLocation.Area01SubArea06,
            WorldSceneLocation.Area01SubArea07
        };

        /// <summary>
        /// Streaming adjacency per Area index (A01..A04): which other Areas stay
        /// loaded while one Area is active, mirroring the authored route graph.
        /// </summary>
        private static readonly int[][] s_areaRequired =
        {
            new[] { 1 },
            new[] { 0, 2, 3 },
            new[] { 1, 3 },
            new[] { 1, 2 }
        };

        /// <summary>
        /// Area entry triggers. From = source Area index, To = destination Area
        /// index or k_DestinationR02 for R02's region set. Positions sit on the
        /// walkable ground at each junction (trigger box is 4 x 5 x 1, so the
        /// centre rides groundY + 2.5 to overlap the player capsule).
        /// </summary>
        private static readonly (int From, int To, Vector3 Position)[] s_areaJunctions =
        {
            (0, 1, new Vector3(0f, 7.5f, 54.5f)),
            (1, 2, new Vector3(5f, 9.5f, 103f)),
            (1, 3, new Vector3(23f, 7.5f, 100f)),
            (2, 3, new Vector3(14f, 9.5f, 112f)),
            (2, 1, new Vector3(0f, 9.5f, 107f)),
            (2, k_DestinationR02, new Vector3(0f, 9.5f, 110.5f))
        };

        // ---------------------------------------------------------------------
        // 1. Audit
        // ---------------------------------------------------------------------

        /// <summary>
        /// Logs every Master-scene object (including the full legacy World
        /// subtree) with its world position and the Area footprint that contains
        /// it, or OUTSIDE. Read-only; run before restructuring.
        /// </summary>
        public static void DumpLegacyAudit()
        {
            LV01GreyboxLayout layout = LoadLayout();
            if (layout == null)
            {
                return;
            }

            Scene scene = OpenMaster();
            var report = new StringBuilder();
            report.AppendLine("[LV01Migration] R01 area footprints:");
            for (int area = 0; area < s_areaNames.Length; area++)
            {
                if (layout.TryGetAreaBounds(k_RegionOutskirts, s_areaNames[area], out Bounds bounds))
                {
                    report.AppendLine(
                        $"  {s_areaNames[area]}: center {bounds.center} size {bounds.size}");
                }
            }

            report.AppendLine("[LV01Migration] Legacy objects:");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                DumpObject(root, layout, report);
            }

            Debug.Log(report.ToString());
            CloseIfOpened(scene);
        }

        private static void DumpObject(
            GameObject gameObject,
            LV01GreyboxLayout layout,
            StringBuilder report)
        {
            string area = Classify(gameObject.transform.position, layout);
            report.AppendLine(
                $"  {gameObject.name}  pos={gameObject.transform.position}  " +
                $"area={area}  childCount={gameObject.transform.childCount}");
            foreach (Transform child in gameObject.transform)
            {
                DumpObject(child.gameObject, layout, report);
            }
        }

        private static string Classify(Vector3 position, LV01GreyboxLayout layout)
        {
            for (int area = 0; area < s_areaNames.Length; area++)
            {
                if (layout.TryGetAreaBounds(
                        k_RegionOutskirts,
                        s_areaNames[area],
                        out Bounds bounds) &&
                    bounds.Contains(position))
                {
                    return s_areaNames[area];
                }
            }

            return "OUTSIDE";
        }

        // ---------------------------------------------------------------------
        // 2. Master scene restructure
        // ---------------------------------------------------------------------

        /// <summary>
        /// Renames World to _LegacyWorld, builds the six Master roots, reparents
        /// runtime services, migrates in-footprint gameplay objects to their Area
        /// Spawners slices, deletes superseded geometry and gameplay, and aligns
        /// the Player Spawn Point with the new design. Requires the R01 per-Area
        /// slice scenes to exist (run CreateAreaSliceScenes + SplitAreaContent
        /// first) so migrated objects have a destination.
        /// </summary>
        public static void RestructureMasterScene()
        {
            LV01GreyboxLayout layout = LoadLayout();
            if (layout == null)
            {
                return;
            }

            Scene scene = OpenMaster();

            GameObject legacyWorld = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == "World");
            if (legacyWorld != null)
            {
                legacyWorld.name = "_LegacyWorld";
            }

            GameObject level = EnsureRoot(scene, "_Level");
            GameObject streaming = EnsureRoot(scene, "_Streaming");
            GameObject gameplay = EnsureRoot(scene, "_Gameplay");
            GameObject environment = EnsureRoot(scene, "_Environment");
            GameObject areaMarkers = EnsureRoot(scene, "_AreaMarkers");
            EnsureRoot(scene, "_Debug");

            ReparentRoot(scene, level, "Player Spawn Point");
            ReparentRoot(scene, level, "Navigation");
            ReparentRoot(scene, streaming, "World Streaming Manager");
            ReparentRoot(scene, streaming, "World Streaming Probe Volume Data");
            ReparentRoot(scene, streaming, "World Streaming Adaptive Probe Volume");
            ReparentRoot(scene, gameplay, "World AI Manager");
            ReparentRoot(scene, gameplay, "World Object Manager");

            DeleteOldGameplay(scene, layout);

            // The whole legacy dungeon is superseded by the new LV01 greybox
            // (new design wins; no duplicate geometry; Master holds none).
            if (legacyWorld != null)
            {
                int childCount = legacyWorld.transform.childCount;
                UnityEngine.Object.DestroyImmediate(legacyWorld);
                Debug.Log(
                    $"[LV01Migration] Deleted _LegacyWorld with {childCount} root children.");
            }

            AlignPlayerSpawn(scene);
            RecentreProbeVolume(scene, layout);
            CreateAreaMarkers(scene, areaMarkers.transform, layout);

            if (environment.transform.childCount == 0)
            {
                Debug.Log(
                    "[LV01Migration] _Environment left empty: dungeon-local lighting " +
                    "was deleted with _LegacyWorld.");
            }

            EditorSceneManager.SaveScene(scene);
            CloseIfOpened(scene);
            Debug.Log("[LV01Migration] Master scene restructured.");
        }

        /// <summary>
        /// Removes the legacy gameplay layer. Every object sits on (z 0..20) or
        /// next to the new CliffPath's teaching route or the old dungeon, and the
        /// new spawners pass has already authored the replacements, so the new
        /// design wins (rule: no duplicate gameplay, no stale gates on the path).
        /// </summary>
        private static void DeleteOldGameplay(Scene scene, LV01GreyboxLayout layout)
        {
            string[] oldRootNames =
            {
                "First Step Site of Grace",
                "World Item Pickup 000",
                "World Item Pickup 001",
                "World Item Pickup 002",
                "Fallen Watcher Fog Wall Interactable"
            };

            foreach (string rootName in oldRootNames)
            {
                GameObject root = scene.GetRootGameObjects()
                    .FirstOrDefault(candidate => candidate.name == rootName);
                if (root != null)
                {
                    DeleteGameplay(root, layout);
                }
            }

            Transform aiManagerTransform = FindTransform(scene, "World AI Manager");
            if (aiManagerTransform == null)
            {
                return;
            }

            for (int index = aiManagerTransform.childCount - 1; index >= 0; index--)
            {
                DeleteGameplay(aiManagerTransform.GetChild(index).gameObject, layout);
            }
        }

        private static void DeleteGameplay(GameObject gameObject, LV01GreyboxLayout layout)
        {
            string area = Classify(gameObject.transform.position, layout);
            Debug.Log(
                $"[LV01Migration] Deleted legacy gameplay '{gameObject.name}' " +
                $"(pos {gameObject.transform.position}, in {area}).");
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private static void AlignPlayerSpawn(Scene scene)
        {
            Transform spawnPoint = FindTransform(scene, "Player Spawn Point");
            if (spawnPoint != null)
            {
                spawnPoint.position = LV01GreyboxSpec.PlayerSpawn;
            }

            Transform triggerTransform = FindTransform(scene, "Spawn Area Load Trigger");
            if (triggerTransform != null)
            {
                triggerTransform.position = spawnPoint != null
                    ? spawnPoint.position
                    : Vector3.zero;
            }
        }

        /// <summary>
        /// Re-centres the master's adaptive probe volume over the authored world
        /// bounds so APV streaming covers the new level instead of the deleted
        /// legacy dungeon.
        /// </summary>
        private static void RecentreProbeVolume(Scene scene, LV01GreyboxLayout layout)
        {
            Transform probeTransform = FindTransform(scene, "World Streaming Adaptive Probe Volume");
            if (probeTransform == null)
            {
                return;
            }

            GameObject probeObject = probeTransform.gameObject;

            bool hasBounds = false;
            Bounds worldBounds = default;
            for (int region = 0; region < 2; region++)
            {
                for (int area = 0; area < WorldScenePathLayout.GetAreaCount(region); area++)
                {
                    string areaName = region == 0
                        ? s_areaNames[area]
                        : area == 0
                            ? LV01GreyboxSpec.EntranceHall
                            : LV01GreyboxSpec.Cloister;
                    if (!layout.TryGetAreaBounds(region, areaName, out Bounds bounds))
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        worldBounds = bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        worldBounds.Encapsulate(bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                return;
            }

            probeObject.transform.position = worldBounds.center;
            ProbeVolume probeVolume = probeObject.GetComponent<ProbeVolume>();
            if (probeVolume != null)
            {
                probeVolume.size = worldBounds.size + Vector3.one * 4f;
            }

            Debug.Log(
                $"[LV01Migration] Re-centred adaptive probe volume to {worldBounds.center} " +
                $"size {worldBounds.size}.");
        }

        private static void CreateAreaMarkers(
            Scene scene,
            Transform parent,
            LV01GreyboxLayout layout)
        {
            for (int area = 0; area < s_areaNames.Length; area++)
            {
                string markerName = $"Marker {s_areaNames[area]}";
                if (parent.Find(markerName) != null)
                {
                    continue;
                }

                if (!layout.TryGetAreaBounds(
                        k_RegionOutskirts,
                        s_areaNames[area],
                        out Bounds bounds))
                {
                    continue;
                }

                GameObject marker = new(markerName);
                marker.transform.SetParent(parent, false);
                marker.transform.position = bounds.center;
            }
        }

        // ---------------------------------------------------------------------
        // 3. Per-Area slice scenes
        // ---------------------------------------------------------------------

        /// <summary>
        /// Creates the four slice scenes for one R01 Area (A02-A04), each with the
        /// standard scaffold (World Area marker, probe data, renderer manager),
        /// registers them in Build Settings and the APV Baking Set.
        /// </summary>
        public static void CreateAreaSliceScenes(int areaIndex)
        {
            if (areaIndex < 1 || areaIndex >= s_areaNames.Length)
            {
                Debug.LogError(
                    $"[LV01Migration] CreateAreaSliceScenes expects 1..3, got {areaIndex}.");
                return;
            }

            string[] paths = new string[s_sliceNames.Length];
            for (int slice = 0; slice < s_sliceNames.Length; slice++)
            {
                paths[slice] = WorldScenePathLayout.GetScenePath(
                    k_RegionOutskirts,
                    areaIndex,
                    slice);
            }

            Scene originalActive = SceneManager.GetActiveScene();
            int createdCount = 0;
            try
            {
                for (int slice = 0; slice < s_sliceNames.Length; slice++)
                {
                    string path = paths[slice];
                    if (File.Exists(path))
                    {
                        continue;
                    }

                    Scene scene = EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Additive);

                    GameObject marker = new($"World Area - {s_areaLocationIDs[areaIndex]}");
                    SceneManager.MoveGameObjectToScene(marker, scene);
                    marker.SetActive(false);

                    GameObject manager = new("World Location Renderer Manager");
                    SceneManager.MoveGameObjectToScene(manager, scene);
                    WorldLocationRendererManager rendererManager =
                        manager.AddComponent<WorldLocationRendererManager>();
                    SerializedObject serializedManager = new(rendererManager);
                    serializedManager.FindProperty("m_rendererSceneID").intValue =
                        GetExpectedBuildIndex(areaIndex, slice);
                    serializedManager.FindProperty("m_manageRootObjects").boolValue =
                        slice != 3;
                    serializedManager.ApplyModifiedPropertiesWithoutUndo();

                    // The scene must exist on disk before its GUID is resolvable.
                    EditorSceneManager.SaveScene(scene, path);

                    GameObject probeData = new("World Streaming Probe Volume Data");
                    SceneManager.MoveGameObjectToScene(probeData, scene);
                    probeData.hideFlags = HideFlags.HideInHierarchy;
                    ProbeVolumePerSceneData perSceneData =
                        probeData.AddComponent<ProbeVolumePerSceneData>();
                    SerializedObject serializedData = new(perSceneData);
                    serializedData.FindProperty("serializedBakingSet").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(k_BakingSetPath);
                    serializedData.FindProperty("sceneGUID").stringValue =
                        AssetDatabase.AssetPathToGUID(path);
                    serializedData.ApplyModifiedPropertiesWithoutUndo();

                    EditorSceneManager.SaveScene(scene);
                    EditorSceneManager.CloseScene(scene, true);
                    createdCount++;
                }
            }
            finally
            {
                if (originalActive.IsValid() && originalActive.isLoaded)
                {
                    SceneManager.SetActiveScene(originalActive);
                }
            }

            if (createdCount > 0)
            {
                AddToBuildSettings(paths);
                AddToBakingSet(paths);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"[LV01Migration] Created {createdCount} slice scenes for " +
                    $"{s_areaNames[areaIndex]} at build indexes " +
                    $"{GetExpectedBuildIndex(areaIndex, 0)}-" +
                    $"{GetExpectedBuildIndex(areaIndex, s_sliceNames.Length - 1)}.");
            }
            else
            {
                Debug.Log(
                    $"[LV01Migration] Slice scenes for {s_areaNames[areaIndex]} already exist.");
            }
        }

        private static int GetExpectedBuildIndex(int areaIndex, int sliceIndex)
        {
            return 22 + (areaIndex - 1) * s_sliceNames.Length + sliceIndex;
        }

        // ---------------------------------------------------------------------
        // 4. Split shared content into per-Area scenes
        // ---------------------------------------------------------------------

        /// <summary>
        /// Moves one Area's root (e.g. A02_Graveyard) out of the shared R01_A01
        /// slices into its own per-Area slice scenes. World transforms are
        /// preserved; pre-move positions are recorded for verification.
        /// </summary>
        public static void SplitAreaContent(int areaIndex)
        {
            if (areaIndex < 0 || areaIndex >= s_areaNames.Length)
            {
                Debug.LogError(
                    $"[LV01Migration] SplitAreaContent expects 0..3, got {areaIndex}.");
                return;
            }

            if (areaIndex == 0)
            {
                Debug.Log(
                    "[LV01Migration] A01 already owns the shared slice scenes; nothing to split.");
                return;
            }

            string areaName = s_areaNames[areaIndex];
            List<Scene> opened = new();
            try
            {
                for (int slice = 0; slice < s_sliceNames.Length; slice++)
                {
                    Scene source = OpenAdditiveIfNeeded(
                        WorldScenePathLayout.GetScenePath(k_RegionOutskirts, 0, slice));
                    Scene target = OpenAdditiveIfNeeded(
                        WorldScenePathLayout.GetScenePath(k_RegionOutskirts, areaIndex, slice));
                    if (!source.IsValid() || !target.IsValid())
                    {
                        continue;
                    }

                    opened.Add(source);
                    opened.Add(target);

                    GameObject areaRoot = source.GetRootGameObjects()
                        .FirstOrDefault(root => root.name == areaName);
                    if (areaRoot == null)
                    {
                        Debug.LogWarning(
                            $"[LV01Migration] No '{areaName}' root in {source.name}; skipping slice.");
                        continue;
                    }

                    SceneManager.MoveGameObjectToScene(areaRoot, target);
                    Debug.Log(
                        $"[LV01Migration] Moved '{areaName}' from {source.name} to {target.name} " +
                        $"({areaRoot.transform.childCount} children).");
                }

                foreach (Scene scene in opened)
                {
                    RefreshRendererManagers(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                foreach (Scene scene in opened.Where(scene => scene.IsValid() && scene.isLoaded))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            Debug.Log(
                $"[LV01Migration] Split {areaName} into its own slice scenes. " +
                $"Run VerifySplitPositions({areaIndex}) to confirm zero drift.");
        }

        // ---------------------------------------------------------------------
        // 5. Streaming data: one WorldLocationSceneSet per Area
        // ---------------------------------------------------------------------

        /// <summary>
        /// Rebuilds R01's streaming data: the existing region asset becomes A01,
        /// three new Area assets are created, the adjacency graph is rewired, and
        /// the R02 asset now points at R01 A03.
        /// </summary>
        public static void ConfigureAreaSceneSets()
        {
            EnsureAssetFolder(k_WorldLocationsFolder);
            WorldLocationSceneSet[] sets = new WorldLocationSceneSet[s_areaNames.Length];

            string existingPath = $"{k_WorldLocationsFolder}/" +
                $"{WorldScenePathLayout.GetRegionFolderName(k_RegionOutskirts)}.asset";
            string a01Path = $"{k_WorldLocationsFolder}/R01_MonasteryOutskirts_A01_CliffPath.asset";
            if (File.Exists(existingPath) && !File.Exists(a01Path))
            {
                AssetDatabase.MoveAsset(existingPath, a01Path);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"[LV01Migration] Renamed region asset to {a01Path} (GUID preserved).");
            }

            sets[0] = AssetDatabase.LoadAssetAtPath<WorldLocationSceneSet>(a01Path);
            if (sets[0] == null)
            {
                Debug.LogError("[LV01Migration] R01 A01 asset missing after rename.");
                return;
            }

            for (int area = 1; area < s_areaNames.Length; area++)
            {
                string path = $"{k_WorldLocationsFolder}/R01_MonasteryOutskirts_{s_areaNames[area]}.asset";
                sets[area] = AssetDatabase.LoadAssetAtPath<WorldLocationSceneSet>(path);
                if (sets[area] == null)
                {
                    sets[area] = ScriptableObject.CreateInstance<WorldLocationSceneSet>();
                    AssetDatabase.CreateAsset(sets[area], path);
                }
            }

            for (int area = 0; area < s_areaNames.Length; area++)
            {
                SerializedObject serializedSet = new(sets[area]);
                serializedSet.FindProperty("m_locationID").stringValue =
                    s_areaLocationIDs[area];
                serializedSet.FindProperty("m_legacyLocation").intValue =
                    (int)s_areaLegacyLocations[area];

                SerializedProperty scenes = serializedSet.FindProperty(
                    "m_scenesRequiredForThisLocation");
                scenes.arraySize = s_sliceNames.Length;
                for (int slice = 0; slice < s_sliceNames.Length; slice++)
                {
                    scenes.GetArrayElementAtIndex(slice).stringValue =
                        WorldScenePathLayout.GetSceneID(k_RegionOutskirts, area, slice);
                }

                SerializedProperty required = serializedSet.FindProperty(
                    "m_requiredLocations");
                int[] requiredAreas = s_areaRequired[area];
                required.arraySize = requiredAreas.Length;
                for (int index = 0; index < requiredAreas.Length; index++)
                {
                    required.GetArrayElementAtIndex(index).objectReferenceValue =
                        sets[requiredAreas[index]];
                }

                serializedSet.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sets[area]);
            }

            WireR02ToMainGate(sets[2]);
            AssetDatabase.SaveAssets();
            Debug.Log("[LV01Migration] R01 streaming data now has one Scene Set per Area.");
        }

        private static void WireR02ToMainGate(WorldLocationSceneSet a03Set)
        {
            string r02Path = $"{k_WorldLocationsFolder}/" +
                $"{WorldScenePathLayout.GetRegionFolderName(1)}.asset";
            WorldLocationSceneSet r02 =
                AssetDatabase.LoadAssetAtPath<WorldLocationSceneSet>(r02Path);
            if (r02 == null)
            {
                Debug.LogWarning("[LV01Migration] R02 asset missing; adjacency not rewired.");
                return;
            }

            SerializedObject serializedR02 = new(r02);
            SerializedProperty required = serializedR02.FindProperty("m_requiredLocations");
            required.arraySize = 2;
            required.GetArrayElementAtIndex(0).objectReferenceValue = a03Set;
            required.GetArrayElementAtIndex(1).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WorldLocationSceneSet>(
                    $"{k_WorldLocationsFolder}/" +
                    $"{WorldScenePathLayout.GetRegionFolderName(2)}.asset");
            serializedR02.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(r02);
        }

        // ---------------------------------------------------------------------
        // 6. Area entry triggers
        // ---------------------------------------------------------------------

        /// <summary>
        /// Instantiates the generic Area Load Trigger prefab at every R01 area
        /// junction inside the source Area's Spawners slice, wired to the
        /// destination Area's Scene Set. Re-runs replace existing triggers.
        /// </summary>
        public static void PlaceAreaTriggers()
        {
            GameObject triggerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_TriggerPrefabPath);
            if (triggerPrefab == null)
            {
                Debug.LogError($"[LV01Migration] Missing trigger prefab {k_TriggerPrefabPath}.");
                return;
            }

            WorldLocationSceneSet[] sets = LoadAreaSets();
            WorldLocationSceneSet r02Set = AssetDatabase.LoadAssetAtPath<WorldLocationSceneSet>(
                $"{k_WorldLocationsFolder}/" +
                $"{WorldScenePathLayout.GetRegionFolderName(1)}.asset");
            if (sets.Any(set => set == null) || r02Set == null)
            {
                Debug.LogError(
                    "[LV01Migration] Run ConfigureAreaSceneSets before placing triggers.");
                return;
            }

            List<Scene> opened = new();
            try
            {
                foreach ((int from, int to, Vector3 position) in s_areaJunctions)
                {
                    Scene scene = OpenAdditiveIfNeeded(
                        WorldScenePathLayout.GetScenePath(k_RegionOutskirts, from, 3));
                    if (!scene.IsValid())
                    {
                        continue;
                    }

                    opened.Add(scene);
                    GameObject areaRoot = scene.GetRootGameObjects()
                        .FirstOrDefault(root => root.name == s_areaNames[from]);
                    Transform parent = areaRoot != null ? areaRoot.transform : null;

                    string destinationName = to == k_DestinationR02
                        ? WorldScenePathLayout.GetRegionFolderName(1)
                        : s_areaNames[to];
                    string triggerName = $"Area Trigger {s_areaNames[from]} -> {destinationName}";

                    // Idempotent: replace only the trigger for this exact junction,
                    // never other junctions sharing the same source Area.
                    if (parent != null)
                    {
                        for (int index = parent.childCount - 1; index >= 0; index--)
                        {
                            if (parent.GetChild(index).name == triggerName)
                            {
                                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
                            }
                        }
                    }

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                        triggerPrefab,
                        scene);
                    instance.name = triggerName;
                    instance.transform.SetParent(parent, false);
                    instance.transform.position = position;

                    EventTriggerLoadScene trigger =
                        instance.GetComponent<EventTriggerLoadScene>();
                    SerializedObject serialized = new(trigger);
                    serialized.FindProperty("m_worldLocation").objectReferenceValue =
                        to == k_DestinationR02 ? r02Set : sets[to];
                    serialized.FindProperty("m_area").intValue =
                        to == k_DestinationR02
                            ? (int)WorldSceneLocation.Area01SubArea01
                            : (int)s_areaLegacyLocations[to];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log(
                        $"[LV01Migration] Placed area trigger at {position} in {scene.name} " +
                        $"({s_areaNames[from]} -> {destinationName}).");
                }

                foreach (Scene scene in opened.Distinct())
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                foreach (Scene scene in opened.Where(scene => scene.IsValid() && scene.isLoaded))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// Creates the convex ProBuilder trigger prefab for every R01 Area Scene
        /// Set, mirroring the existing per-location trigger pattern.
        /// </summary>
        public static void CreateAreaTriggerPrefabs()
        {
            WorldLocationSceneSet[] sets = LoadAreaSets();
            if (sets.Any(set => set == null))
            {
                Debug.LogError(
                    "[LV01Migration] Run ConfigureAreaSceneSets before creating trigger prefabs.");
                return;
            }

            int eventTriggerLayer = LayerMask.NameToLayer("Event Trigger");
            EnsureAssetFolder(k_LocationTriggerFolder);
            for (int area = 0; area < sets.Length; area++)
            {
                string prefabPath = $"{k_LocationTriggerFolder}/" +
                    $"{s_areaLocationIDs[area]} Trigger.prefab";
                if (File.Exists(prefabPath))
                {
                    continue;
                }

                GameObject triggerObject = new(
                    $"{s_areaLocationIDs[area]} Trigger",
                    typeof(ProBuilderMesh),
                    typeof(PolyShape),
                    typeof(MeshCollider),
                    typeof(EventTriggerLoadScene));
                try
                {
                    triggerObject.layer = eventTriggerLayer;
                    PolyShape polyShape = triggerObject.GetComponent<PolyShape>();
                    polyShape.SetControlPoints(new[]
                    {
                        new Vector3(-4f, 0f, -3f),
                        new Vector3(2.5f, 0f, -3f),
                        new Vector3(4f, 0f, -1f),
                        new Vector3(3f, 0f, 3f),
                        new Vector3(-3f, 0f, 3f),
                        new Vector3(-4f, 0f, 1f)
                    });
                    polyShape.extrude = 5f;
                    polyShape.flipNormals = false;
                    ActionResult result = polyShape.CreateShapeFromPolygon();
                    if (result.status == ActionResult.Status.Failure)
                    {
                        throw new InvalidOperationException(result.notification);
                    }

                    MeshCollider collider = triggerObject.GetComponent<MeshCollider>();
                    collider.sharedMesh = triggerObject.GetComponent<MeshFilter>()
                        .sharedMesh;
                    collider.convex = true;
                    collider.isTrigger = true;
                    triggerObject.GetComponent<MeshRenderer>().enabled = false;

                    EventTriggerLoadScene trigger =
                        triggerObject.GetComponent<EventTriggerLoadScene>();
                    SerializedObject serializedTrigger = new(trigger);
                    serializedTrigger.FindProperty("m_worldLocation")
                        .objectReferenceValue = sets[area];
                    serializedTrigger.FindProperty("m_area").intValue =
                        (int)s_areaLegacyLocations[area];
                    serializedTrigger.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(triggerObject, prefabPath);
                    Debug.Log($"[LV01Migration] Created {prefabPath}.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(triggerObject);
                }
            }

            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------------
        // 7. Verification
        // ---------------------------------------------------------------------

        /// <summary>
        /// Re-opens the split scenes and compares every moved PB_ object's world
        /// position and rotation against its authored box in the layout asset.
        /// This verifies both the move (transform preservation) and the content
        /// itself (matches the LV01 design). Any delta above epsilon fails.
        /// </summary>
        public static void VerifySplitPositions(int areaIndex)
        {
            if (areaIndex < 0 || areaIndex >= s_areaNames.Length)
            {
                Debug.LogError(
                    $"[LV01Migration] VerifySplitPositions expects 0..3, got {areaIndex}.");
                return;
            }

            LV01GreyboxLayout layout = LoadLayout();
            if (layout == null)
            {
                return;
            }

            Dictionary<string, GreyboxBox> boxesByName = layout.Boxes
                .Where(box => box.RegionIndex == k_RegionOutskirts &&
                    box.Area == s_areaNames[areaIndex])
                .GroupBy(box => box.ObjectName)
                .ToDictionary(group => group.Key, group => group.First());

            int verified = 0;
            int unverified = 0;
            int failures = 0;
            List<Scene> opened = new();
            try
            {
                for (int slice = 0; slice < s_sliceNames.Length; slice++)
                {
                    Scene scene = OpenAdditiveIfNeeded(
                        WorldScenePathLayout.GetScenePath(k_RegionOutskirts, areaIndex, slice));
                    if (!scene.IsValid())
                    {
                        continue;
                    }

                    opened.Add(scene);
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        if (root.name != s_areaNames[areaIndex])
                        {
                            continue;
                        }

                        foreach (Transform transform in
                            root.GetComponentsInChildren<Transform>(true))
                        {
                            if (transform == root.transform || !transform.name.StartsWith("PB_"))
                            {
                                continue;
                            }

                            if (!boxesByName.TryGetValue(transform.name, out GreyboxBox box))
                            {
                                unverified++;
                                continue;
                            }

                            float positionDelta = Vector3.Distance(box.Position, transform.position);
                            float rotationDelta = Quaternion.Angle(
                                Quaternion.Euler(box.Rotation),
                                transform.rotation);
                            verified++;
                            if (positionDelta > 0.001f || rotationDelta > 0.01f)
                            {
                                failures++;
                                Debug.LogError(
                                    $"[LV01Migration] Drift on '{transform.name}': " +
                                    $"pos {positionDelta:0.0000} m, rot {rotationDelta:0.000} deg " +
                                    $"(authored {box.Position}, now {transform.position}).");
                            }
                        }
                    }
                }
            }
            finally
            {
                foreach (Scene scene in opened.Where(scene => scene.IsValid() && scene.isLoaded))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            Debug.Log(
                failures == 0
                    ? $"[LV01Migration] Verified {verified} PB_ transforms for " +
                        $"{s_areaNames[areaIndex]} against the layout: zero drift " +
                        $"({unverified} non-layout objects skipped)."
                    : $"[LV01Migration] FAILED: {failures} of {verified} transforms drifted.");
        }

        /// <summary>
        /// Checks every slice scene's renderer manager build index against its
        /// actual build index.
        /// </summary>
        public static void VerifyBuildIndexes()
        {
            int failures = 0;
            Scene originalActive = SceneManager.GetActiveScene();
            try
            {
                for (int region = 0; region < WorldScenePathLayout.RegionCount; region++)
                {
                    for (int area = 0; area < WorldScenePathLayout.GetAreaCount(region); area++)
                    {
                        for (int slice = 0; slice < s_sliceNames.Length; slice++)
                        {
                            string path = WorldScenePathLayout.GetScenePath(region, area, slice);
                            if (!File.Exists(path))
                            {
                                continue;
                            }

                            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                            try
                            {
                                int actual = scene.buildIndex;
                                foreach (GameObject root in scene.GetRootGameObjects())
                                {
                                    WorldLocationRendererManager manager =
                                        root.GetComponentInChildren<WorldLocationRendererManager>(true);
                                    if (manager == null)
                                    {
                                        continue;
                                    }

                                    SerializedObject serialized = new(manager);
                                    int configured = serialized
                                        .FindProperty("m_rendererSceneID").intValue;
                                    if (configured != actual)
                                    {
                                        failures++;
                                        Debug.LogError(
                                            $"[LV01Migration] {scene.name}: m_rendererSceneID " +
                                            $"{configured} != buildIndex {actual}.");
                                    }
                                }
                            }
                            finally
                            {
                                EditorSceneManager.CloseScene(scene, true);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (originalActive.IsValid() && originalActive.isLoaded)
                {
                    SceneManager.SetActiveScene(originalActive);
                }
            }

            Debug.Log(
                failures == 0
                    ? "[LV01Migration] All renderer manager build indexes match."
                    : $"[LV01Migration] FAILED: {failures} renderer manager index mismatches.");
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static WorldLocationSceneSet[] LoadAreaSets()
        {
            WorldLocationSceneSet[] sets = new WorldLocationSceneSet[s_areaNames.Length];
            for (int area = 0; area < s_areaNames.Length; area++)
            {
                string path = area == 0
                    ? $"{k_WorldLocationsFolder}/R01_MonasteryOutskirts_A01_CliffPath.asset"
                    : $"{k_WorldLocationsFolder}/R01_MonasteryOutskirts_{s_areaNames[area]}.asset";
                sets[area] = AssetDatabase.LoadAssetAtPath<WorldLocationSceneSet>(path);
            }

            return sets;
        }

        private static LV01GreyboxLayout LoadLayout()
        {
            LV01GreyboxLayout layout =
                AssetDatabase.LoadAssetAtPath<LV01GreyboxLayout>(k_LayoutAssetPath);
            if (layout == null)
            {
                Debug.LogError(
                    $"[LV01Migration] Missing layout asset {k_LayoutAssetPath}. " +
                    "请先在 ZZ 工具面板中执行“从规范重建布局资源”。");
            }

            return layout;
        }

        private static Scene OpenMaster()
        {
            for (int index = 0; index < EditorSceneManager.sceneCount; index++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    Debug.LogError(
                        $"[LV01Migration] Dirty scene '{scene.name}' is open; save or " +
                        "discard it before restructuring the Master scene.");
                    return default;
                }
            }

            Scene master = SceneManager.GetSceneByPath(k_MasterScenePath);
            if (!master.IsValid() || !master.isLoaded)
            {
                master = EditorSceneManager.OpenScene(k_MasterScenePath, OpenSceneMode.Single);
            }

            return master;
        }

        private static Scene OpenAdditiveIfNeeded(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[LV01Migration] Missing scene '{path}'.");
                return default;
            }

            for (int index = 0; index < EditorSceneManager.sceneCount; index++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(index);
                if (scene.path == path)
                {
                    return scene;
                }
            }

            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        private static void CloseIfOpened(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static GameObject EnsureRoot(Scene scene, string rootName)
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

        private static void ReparentRoot(Scene scene, GameObject newParent, string rootName)
        {
            GameObject root = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == rootName);
            if (root == null)
            {
                Debug.LogWarning($"[LV01Migration] Expected root '{rootName}' missing in Master.");
                return;
            }

            root.transform.SetParent(newParent.transform, true);
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void RefreshRendererManagers(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                WorldLocationRendererManager manager =
                    root.GetComponentInChildren<WorldLocationRendererManager>(true);
                manager?.RefreshSceneObjects();
            }
        }

        private static void AddToBuildSettings(string[] scenePaths)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            bool changed = false;
            foreach (string path in scenePaths)
            {
                if (scenes.Any(scene => scene.path == path))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
                changed = true;
            }

            if (changed)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void AddToBakingSet(string[] scenePaths)
        {
            ProbeVolumeBakingSet bakingSet =
                AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(k_BakingSetPath);
            if (bakingSet == null)
            {
                Debug.LogError($"[LV01Migration] Missing baking set {k_BakingSetPath}.");
                return;
            }

            foreach (string path in scenePaths)
            {
                string sceneGUID = AssetDatabase.AssetPathToGUID(path);
                if (!bakingSet.sceneGUIDs.Contains(sceneGUID))
                {
                    bakingSet.TryAddScene(sceneGUID);
                }
            }

            EditorUtility.SetDirty(bakingSet);
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            EnsureAssetFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, Path.GetFileName(folderPath));
        }
    }
}
