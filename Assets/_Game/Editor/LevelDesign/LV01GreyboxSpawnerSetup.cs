using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Populates the Spawners slices of R01 and R02 with the authored encounters,
    /// the Main Gate lever loop, and the Cloister checkpoint.
    /// </summary>
    /// <remarks>
    /// Only objects named <c>PB_</c> are ever replaced, so hand-authored spawner
    /// content in these Scenes survives a rebuild.
    /// </remarks>
    public static class LV01GreyboxSpawnerSetup
    {
        private const string k_GeneratedPrefix = "PB_";
        private const string k_EnemyPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_LeverGatePrefabPath =
            "Assets/_Game/Prefabs/World/Objects/Doors/Lever Gate.prefab";

        [ZZTool("关卡设计", "构建生成器内容", 50)]
        public static void BuildSpawnersContent()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                Debug.LogError("[LV01Greybox] Exit Play Mode before building spawner content.");
                return;
            }

            GameObject enemyPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(k_EnemyPrefabPath);
            if (enemyPrefab == null)
            {
                Debug.LogError($"[LV01Greybox] Missing enemy prefab '{k_EnemyPrefabPath}'.");
                return;
            }

            int created = 0;
            for (int region = 0; region < 2; region++)
            {
                for (int area = 0; area < WorldScenePathLayout.GetAreaCount(region); area++)
                {
                    created += BuildAreaSpawners(region, area, enemyPrefab);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[LV01Greybox] Spawners content built: {created} gameplay objects.");
        }

        private static int BuildAreaSpawners(
            int regionIndex,
            int areaIndex,
            GameObject enemyPrefab)
        {
            int created = 0;
            Scene scene = OpenSpawnersScene(regionIndex, areaIndex);
            if (!scene.IsValid())
            {
                return 0;
            }

            if (regionIndex == LV01GreyboxSpec.RegionOutskirts)
            {
                if (areaIndex == AreaIndex(LV01GreyboxSpec.CliffPath))
                {
                    created += AddEnemies(scene, LV01GreyboxSpec.CliffPath, enemyPrefab,
                        LV01GreyboxSpec.CliffEnemyOne, LV01GreyboxSpec.CliffEnemyTwo);
                }
                else if (areaIndex == AreaIndex(LV01GreyboxSpec.Graveyard))
                {
                    created += AddEnemies(scene, LV01GreyboxSpec.Graveyard, enemyPrefab,
                        LV01GreyboxSpec.GraveyardPatrolOne,
                        LV01GreyboxSpec.GraveyardPatrolTwo,
                        LV01GreyboxSpec.GraveyardArcher);
                }
                else if (areaIndex == AreaIndex(LV01GreyboxSpec.MainGate))
                {
                    BuildLeverGate(scene);
                }
                else if (areaIndex == AreaIndex(LV01GreyboxSpec.GateTower))
                {
                    created += AddEnemies(scene, LV01GreyboxSpec.GateTower, enemyPrefab,
                        LV01GreyboxSpec.GateTowerGuard);
                }
            }
            else if (areaIndex == AreaIndex(LV01GreyboxSpec.EntranceHall))
            {
                created += AddEnemies(scene, LV01GreyboxSpec.EntranceHall, enemyPrefab,
                    LV01GreyboxSpec.HallEnemy);
            }
            else if (areaIndex == AreaIndex(LV01GreyboxSpec.Cloister))
            {
                created += AddSiteOfGrace(scene);
            }

            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            return created;
        }

        private static int AreaIndex(string area)
        {
            return int.TryParse(area.Substring(1, 2), out int areaNumber)
                ? areaNumber - 1
                : -1;
        }

        private static int AddEnemies(
            Scene scene,
            string area,
            GameObject enemyPrefab,
            params Vector3[] positions)
        {
            GameObject areaRoot = FindOrCreateAreaRoot(scene, area);
            RemoveGeneratedChildren(areaRoot);

            int created = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject spawnerObject = new($"PB_{area}_Enemy_{i:00}");
                SceneManager.MoveGameObjectToScene(spawnerObject, scene);
                spawnerObject.transform.SetParent(areaRoot.transform, false);
                spawnerObject.transform.position = positions[i];

                AICharacterSpawner spawner =
                    spawnerObject.AddComponent<AICharacterSpawner>();
                SerializedObject serialized = new(spawner);
                serialized.FindProperty("m_characterGameObject").objectReferenceValue =
                    enemyPrefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                created++;
            }

            return created;
        }

        private static int AddSiteOfGrace(Scene scene)
        {
            GameObject areaRoot = FindOrCreateAreaRoot(scene, LV01GreyboxSpec.Cloister);
            RemoveGeneratedChildren(areaRoot);

            GameObject graceObject =
                new($"PB_{LV01GreyboxSpec.Cloister}_SiteOfGrace_00");
            SceneManager.MoveGameObjectToScene(graceObject, scene);
            graceObject.transform.SetParent(areaRoot.transform, false);
            graceObject.transform.position = LV01GreyboxSpec.CloisterCheckpoint;

            BoxCollider collider = graceObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(2.5f, 2.5f, 2.5f);

            Rigidbody body = graceObject.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
            }

            graceObject.AddComponent<Unity.Netcode.NetworkObject>();
            SiteOfGraceInteractable grace =
                graceObject.AddComponent<SiteOfGraceInteractable>();

            SerializedObject serialized = new(grace);
            serialized.FindProperty("m_siteOfGraceID").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject lightObject = new("Grace Light");
            lightObject.transform.SetParent(graceObject.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.6f);
            light.range = 8f;
            light.intensity = 2f;
            light.enabled = false;

            serialized.Update();
            serialized.FindProperty("m_graceLight").objectReferenceValue = light;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return 1;
        }

        /// <summary>
        /// Places the existing Lever Gate prefab so its gate fills the Main Gate
        /// opening and its lever sits on the Gate Tower's upper level. The prefab's
        /// own wiring survives because only transforms move.
        /// </summary>
        private static void BuildLeverGate(Scene scene)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(k_LeverGatePrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[LV01Greybox] Missing prefab '{k_LeverGatePrefabPath}'.");
                return;
            }

            GameObject areaRoot = FindOrCreateAreaRoot(scene, LV01GreyboxSpec.MainGate);
            RemoveGeneratedChildren(areaRoot);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = $"PB_{LV01GreyboxSpec.MainGate}_LeverGate";
            instance.transform.SetParent(areaRoot.transform, false);

            AlignGateVisual(instance);
            MoveLeverToTower(instance);
        }

        private static void AlignGateVisual(GameObject instance)
        {
            Transform gateVisual = FindDescendant(instance.transform, "Gate Visual");
            if (gateVisual == null)
            {
                Debug.LogWarning(
                    "[LV01Greybox] Lever Gate prefab has no 'Gate Visual'; " +
                    "the gate was placed without rescaling.");
                return;
            }

            Renderer renderer = gateVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Vector3 size = renderer.bounds.size;
                if (size.x > 0.01f && size.y > 0.01f)
                {
                    Vector3 localScale = gateVisual.localScale;
                    gateVisual.localScale = new Vector3(
                        localScale.x * (LV01GreyboxSpec.GateWidth / size.x),
                        localScale.y * (LV01GreyboxSpec.GateHeight / size.y),
                        localScale.z);
                }
            }

            // Slide the whole instance so the gate visual centres on the opening.
            renderer = gateVisual.GetComponent<Renderer>();
            Vector3 centre = renderer != null
                ? renderer.bounds.center
                : gateVisual.position;
            instance.transform.position += LV01GreyboxSpec.GateOpeningCentre - centre;
        }

        private static void MoveLeverToTower(GameObject instance)
        {
            List<Transform> leverRoots = new();
            CollectTopmostMatching(instance.transform, "Lever", leverRoots);
            if (leverRoots.Count == 0)
            {
                Debug.LogWarning(
                    "[LV01Greybox] Lever Gate prefab has no 'Lever' objects; " +
                    "the lever stayed beside the gate.");
                return;
            }

            Vector3 pivot = leverRoots[0].position;
            Vector3 delta = LV01GreyboxSpec.GateTowerLever - pivot;
            foreach (Transform leverRoot in leverRoots)
            {
                leverRoot.position += delta;
            }

            Debug.Log(
                $"[LV01Greybox] Moved {leverRoots.Count} lever object(s) to " +
                $"{LV01GreyboxSpec.GateTowerLever}.");
        }

        private static void CollectTopmostMatching(
            Transform parent,
            string nameContains,
            List<Transform> results)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(nameContains))
                {
                    results.Add(child);
                    continue;
                }

                CollectTopmostMatching(child, nameContains, results);
            }
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                Transform found = FindDescendant(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ---- Scene helpers ---------------------------------------------------

        private static Scene OpenSpawnersScene(int regionIndex, int areaIndex)
        {
            string path = WorldScenePathLayout.GetScenePath(regionIndex, areaIndex, 3);
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"[LV01Greybox] Missing Spawners Scene '{path}'.");
                return default;
            }

            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        private static GameObject FindOrCreateAreaRoot(Scene scene, string areaName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == areaName)
                {
                    return root;
                }
            }

            GameObject areaRoot = new(areaName);
            SceneManager.MoveGameObjectToScene(areaRoot, scene);
            return areaRoot;
        }

        private static void RemoveGeneratedChildren(GameObject parent)
        {
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.transform.GetChild(i);
                if (child.name.StartsWith(k_GeneratedPrefix))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
