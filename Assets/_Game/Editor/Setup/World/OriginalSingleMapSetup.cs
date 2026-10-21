using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Builds and verifies the selected original map before replacing the playable world.</summary>
    public static class OriginalSingleMapSetup
    {
        public const string MapId = "m31_03_00_00";
        public const string ScenePath = "Assets/_Game/Scenes/Maps/m31/SCN_m31_03_00_00.unity";

        [MenuItem("Tools/ZZ/Elden Ring/Install Groveside Cave Gameplay")]
        public static void InstallGameplay()
        {
            if (Application.isPlaying || EditorSceneManager.GetSceneManagerSetup().Any(entry => entry.isLoaded &&
                SceneManager.GetSceneByPath(entry.path).isDirty))
            {
                throw new InvalidOperationException("Stop Play Mode and save open scenes before installing the map.");
            }
            if (!File.Exists("Docs/OriginalSingleMap/gameplay-prefabs.txt"))
            {
                throw new InvalidOperationException("Install the original creature gameplay prefabs first.");
            }
            JObject source = JObject.Parse(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/SingleMapGameplay/source.json"));
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject previous = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "_OriginalGameplay");
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous);
            }
            var gameplay = new GameObject("_OriginalGameplay");
            GameObject manager = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/World/Managers/World AI Manager.prefab"), scene);
            PrefabUtility.UnpackPrefabInstance(manager, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (Transform child in manager.transform.Cast<Transform>().ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            manager.transform.SetParent(gameplay.transform, false);
            gameplay.AddComponent<WorldObjectManager>();
            JToken spawn = source["Players"].Single(row => row.Value<int>("EntityID") == 31030980);
            Transform playerSpawn = new GameObject("Player Spawn Point").transform;
            playerSpawn.SetParent(gameplay.transform, false);
            playerSpawn.SetPositionAndRotation(Position(spawn["Position"]), Rotation(spawn["Rotation"]) *
                Quaternion.Euler(0f, 180f, 0f));
            var report = new StringBuilder();
            AICharacterSpawner bossSpawner = null;
            int count = 0;
            foreach (JToken enemy in source["Enemies"].Where(row => row.Value<string>("ModelName") != "c1000"))
            {
                string actor = enemy.Value<string>("ModelName");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalCreatureGameplaySetup.Output +
                    "/" + actor + "/" + actor + " Gameplay.prefab");
                if (prefab == null)
                {
                    throw new InvalidDataException("Missing original creature prefab: " + actor);
                }
                var point = new GameObject(enemy.Value<string>("Name"));
                point.transform.SetParent(gameplay.transform, false);
                point.transform.SetPositionAndRotation(Position(enemy["Position"]), Rotation(enemy["Rotation"]) *
                    Quaternion.Euler(0f, 180f, 0f));
                AICharacterSpawner spawner = point.AddComponent<AICharacterSpawner>();
                var data = new SerializedObject(spawner);
                data.FindProperty("m_characterGameObject").objectReferenceValue = prefab;
                data.FindProperty("m_isSleeping").boolValue = actor == "c3970" ||
                    enemy.Value<int>("EntityID") is 31030210 or 31030211 or 31030216;
                if (actor == "c3970")
                {
                    point.name = "法姆·亚兹拉的兽人 Spawner";
                    data.FindProperty("m_bossID").intValue = enemy.Value<int>("EntityID");
                    bossSpawner = spawner;
                }
                else
                {
                    JObject npc = (JObject)source["Parameters"]["NpcParam"].Single(row =>
                        row.Value<int>("ID") == enemy.Value<int>("NPCParamID"))["Fields"];
                    data.FindProperty("m_manuallySetStats").boolValue = true;
                    data.FindProperty("m_maximumHealth").floatValue =
                        OriginalCreatureGameplaySetup.CalculatePassiveHealth(npc, source);
                    data.FindProperty("m_maximumStamina").floatValue = Mathf.Max(1f, npc.Value<float>("stamina"));
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                report.AppendLine("SOURCE AI " + enemy.Value<int>("EntityID") + " " + actor + " " + point.transform.position);
                count++;
            }
            if (count != 8 || bossSpawner == null)
            {
                throw new InvalidDataException("The selected cave must contain one Boss and seven wolves.");
            }
            JToken region = source["Regions"].Single(row => row.Value<int>("EntityID") == 31032800);
            var battle = new GameObject("Original battle region 31032800");
            battle.transform.SetParent(gameplay.transform, false);
            battle.transform.SetPositionAndRotation(Position(region["Position"]), Rotation(region["Rotation"]));
            BoxCollider box = battle.AddComponent<BoxCollider>();
            box.size = new Vector3(region["Shape"].Value<float>("Width"), region["Shape"].Value<float>("Height"),
                region["Shape"].Value<float>("Depth"));
            box.center = Vector3.up * box.size.y * 0.5f;
            box.isTrigger = true;
            Rigidbody body = battle.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var trigger = new SerializedObject(battle.AddComponent<BossArenaController>());
            trigger.FindProperty("m_bossID").intValue = 31030800;
            trigger.ApplyModifiedPropertiesWithoutUndo();
            InstallGrace(gameplay.transform, source, playerSpawn);
            RegisterPrefabs();
            var warmup = new SerializedObject(gameplay.AddComponent<VillageSceneWarmup>());
            Renderer[] renderers = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<Renderer>(true)).Where(renderer => renderer.enabled &&
                renderer.gameObject.activeInHierarchy).OrderBy(renderer =>
                Vector3.SqrMagnitude(renderer.bounds.center - playerSpawn.position)).ToArray();
            SerializedProperty targets = warmup.FindProperty("m_renderers");
            targets.arraySize = renderers.Length;
            for (int index = 0; index < renderers.Length; index++)
            {
                targets.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
            }
            warmup.ApplyModifiedPropertiesWithoutUndo();
            OriginalAssetCollisionSetup.Install(scene);
            NavMeshSurface surface = gameplay.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData == null || NavMesh.CalculateTriangulation().vertices.Length == 0)
            {
                throw new InvalidOperationException("Original map navigation is empty.");
            }
            string navPath = "Assets/_Game/Scenes/Maps/m31/Groveside Navigation.asset";
            NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            }
            else
            {
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.RemoveData();
                surface.navMeshData = existing;
                surface.AddData();
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            Physics.SyncTransforms();
            foreach (AICharacterSpawner point in gameplay.GetComponentsInChildren<AICharacterSpawner>())
            {
                if (!NavMesh.SamplePosition(point.transform.position, out _, 2f, NavMesh.AllAreas))
                {
                    throw new InvalidOperationException("Original enemy spawn has no nearby navigation: " + point.name);
                }
            }
            if (!Physics.Raycast(playerSpawn.position + Vector3.up * 2f, Vector3.down, out RaycastHit ground,
                    8f, ~0, QueryTriggerInteraction.Ignore))
            {
                throw new InvalidOperationException("Original player spawn has no collision floor.");
            }
            report.AppendLine("PASS: 8 original spawns have navigation; player floor=" + ground.point);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/OriginalSingleMap/gameplay-scene.txt", report.ToString());
            foreach (string obsolete in new[] { "Assets/_Game/Scripts/World/AI/OriginalMapBossTrigger.cs",
                "Assets/_Game/Editor/Setup/World/VikingVillageIntegration.cs",
                "Assets/_Game/Editor/Setup/World/VillageWorldSetup.cs" })
            {
                if (File.Exists(obsolete))
                {
                    AssetDatabase.DeleteAsset(obsolete);
                }
            }
        }

        [MenuItem("Tools/ZZ/Elden Ring/Activate Groveside Cave Gameplay")]
        public static void ActivateGameplay()
        {
            if (!File.Exists("Docs/OriginalSingleMap/gameplay-scene.txt") ||
                WorldScenePathLayout.GameplayScenePath != ScenePath)
            {
                throw new InvalidOperationException("Verify the source map and update its central scene path first.");
            }
            EditorBuildSettingsScene[] scenes = new[]
            {
                new EditorBuildSettingsScene(WorldScenePathLayout.MainMenuScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.globalScenes = scenes;
            EditorBuildSettings.scenes = scenes;
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/OriginalSingleMap/build-scenes.txt", string.Join("\n",
                EditorBuildSettings.scenes.Select(entry => entry.path)) + "\nGLOBAL\n" + string.Join("\n",
                EditorBuildSettings.globalScenes.Select(entry => entry.path)));
        }

        [MenuItem("Tools/ZZ/Elden Ring/Remove Replaced Viking Village")]
        public static void RemoveReplacedVillage()
        {
            if (Application.isPlaying || WorldScenePathLayout.GameplayScenePath != ScenePath ||
                !File.Exists("Docs/OriginalSingleMap/attack-runtime.txt") ||
                !File.ReadAllText("Docs/OriginalSingleMap/attack-runtime.txt").Contains("PASS: actors=3 attacks=34;"))
            {
                throw new InvalidOperationException("Stop Play Mode after all 34 original creature attacks pass.");
            }
            string[] folders = { "Assets/_Game/Scenes/Levels/LV02_VikingVillage", "Assets/Viking Village" };
            string[] roots = EditorBuildSettings.scenes.Where(entry => entry.enabled).Select(entry => entry.path)
                .Concat(AssetDatabase.FindAssets(string.Empty, new[] { "Assets/Resources", "Assets/_Game/Resources" })
                    .Select(AssetDatabase.GUIDToAssetPath))
                .Concat(new[] { AssetDatabase.GetAssetPath(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline),
                    AssetDatabase.GetAssetPath(QualitySettings.renderPipeline) }).Where(path => !string.IsNullOrEmpty(path)).ToArray();
            if (!roots.Contains(ScenePath) || roots.Any(path => path.Contains("LV02_VikingVillage")))
            {
                throw new InvalidOperationException("The active build must use the original cave only.");
            }
            string[] references = AssetDatabase.GetDependencies(roots, true).Where(path => folders.Any(folder =>
                path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal))).ToArray();
            File.WriteAllLines("Docs/OriginalSingleMap/village-retained-references.txt", references);
            if (references.Length != 0)
            {
                throw new InvalidOperationException("Replace the Village references listed in village-retained-references.txt first.");
            }
            var report = new StringBuilder();
            foreach (string folder in folders.Where(AssetDatabase.IsValidFolder))
            {
                int count = AssetDatabase.FindAssets(string.Empty, new[] { folder }).Length;
                if (!AssetDatabase.DeleteAsset(folder))
                {
                    throw new IOException("Could not remove replaced Village folder: " + folder);
                }
                report.AppendLine("REMOVED " + folder + " assets=" + count);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/OriginalSingleMap/village-cleanup.txt", report +
                "PASS: active build scenes and Resources have no Village dependencies.\n");
        }

        private static void InstallGrace(Transform parent, JObject source, Transform spawn)
        {
            JToken asset = source["Assets"].Single(row => row.Value<int>("EntityID") == 31031950);
            var root = new GameObject("Original Grace 31031950");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(Position(asset["Position"]), Rotation(asset["Rotation"]));
            root.AddComponent<NetworkObject>();
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 2f;
            var data = new SerializedObject(root.AddComponent<SiteOfGraceInteractable>());
            data.FindProperty("m_siteOfGraceID").intValue = 310300;
            data.FindProperty("m_displayName").stringValue = "近林洞窟";
            data.FindProperty("m_interactableCollider").objectReferenceValue = collider;
            data.FindProperty("m_autoDiscoverInteractableCollider").boolValue = false;
            data.FindProperty("m_teleportTransform").objectReferenceValue = spawn;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterPrefabs()
        {
            string[] lists = AssetDatabase.FindAssets("t:NetworkPrefabsList", new[] { "Assets/_Game" });
            if (lists.Length == 0)
            {
                throw new InvalidOperationException("No first-party network prefab registry is available.");
            }
            foreach (string guid in lists)
            {
                NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (string actor in new[] { "c3970", "c4070", "c4071" })
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalCreatureGameplaySetup.Output +
                        "/" + actor + "/" + actor + " Gameplay.prefab");
                    if (!list.PrefabList.Any(entry => entry.Prefab == prefab))
                    {
                        list.Add(new NetworkPrefab { Prefab = prefab });
                    }
                }
                EditorUtility.SetDirty(list);
                AssetDatabase.SaveAssetIfDirty(list);
            }
        }

        private static Vector3 Position(JToken token)
        {
            float[] value = token.ToObject<float[]>();
            return new Vector3(-value[0], value[1], value[2]);
        }

        private static Quaternion Rotation(JToken token)
        {
            float[] value = token.ToObject<float[]>();
            return EldenRingMapSceneImport.ConvertRotation(new Vector3(value[0], value[1], value[2]));
        }

        [MenuItem("Tools/ZZ/Elden Ring/Prepare Original Groveside Cave")]
        public static void Prepare()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before preparing the original map.");
            }
            Directory.CreateDirectory("Docs/OriginalSingleMap");
            File.WriteAllText("Docs/OriginalSingleMap/import.txt", "RUNNING " + MapId + "\n");
            EldenRingMapSceneImport.ImportMap(MapId);
            EldenRingMapMaterialImport.ImportPreparedMap(MapId);
            EldenRingMapCollisionImport.ImportPreparedMap(MapId);
            File.AppendAllText("Docs/OriginalSingleMap/import.txt",
                "PASS: geometry, placements, supported material recipes, collision and physics imported.\n" +
                "Gameplay, enemies, events and original rendering still require separate verification.\n");
        }
    }
}
