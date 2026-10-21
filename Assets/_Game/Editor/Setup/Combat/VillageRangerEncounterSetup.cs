using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Adds a ranged opponent to connected Village navigation.</summary>
    public static class VillageRangerEncounterSetup
    {
        [MenuItem("Tools/ZZ/Combat/Add Village Ranger Encounter")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            Scene scene = EditorSceneManager.OpenScene(WorldScenePathLayout.GameplayScenePath, OpenSceneMode.Single);
            Transform gameplay = scene.GetRootGameObjects().Single(root => root.name == "_VillageGameplay").transform;
            const string name = "Village Banished Archer";
            if (gameplay.Find(name) != null)
            {
                return;
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Ranger/Ranger AI.prefab");
            if (prefab == null)
            {
                throw new InvalidOperationException("The configured Ranger prefab is missing.");
            }
            if (!NavMesh.SamplePosition(new Vector3(-13f, 6f, -12f), out NavMeshHit entry, 6f, NavMesh.AllAreas))
            {
                throw new InvalidOperationException("Village entry is not navigable.");
            }
            Vector3 position = FindPosition(entry.position);
            var point = new GameObject(name);
            point.transform.SetParent(gameplay);
            point.transform.position = position;
            point.transform.rotation = Quaternion.LookRotation(entry.position - position);
            var data = new SerializedObject(point.AddComponent<AICharacterSpawner>());
            data.FindProperty("m_characterGameObject").objectReferenceValue = prefab;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Docs/CombatRefresh/village-ranger.txt", $"Archer={position}\nConnectedToEntry=True\n");
        }

        private static Vector3 FindPosition(Vector3 entry)
        {
            foreach (Vector3 offset in new[] { new Vector3(12f, 0f, 8f), new Vector3(-12f, 0f, 8f),
                new Vector3(8f, 0f, -12f), new Vector3(-8f, 0f, -12f) })
            {
                if (!NavMesh.SamplePosition(entry + offset, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    continue;
                }
                var path = new NavMeshPath();
                if (Vector3.Distance(hit.position, entry) >= 10f &&
                    NavMesh.CalculatePath(entry, hit.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete)
                {
                    return hit.position;
                }
            }
            throw new InvalidOperationException("No connected archer placement was found.");
        }
    }
}
