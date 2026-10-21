using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Maps imported environment physics to the game's ground layer and restores boardwalk geometry.</summary>
    public static class VillageGroundCollisionSetup
    {
        [MenuItem("Tools/ZZ/Environment/Repair Village Ground Collision")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before repairing scene physics.");
            }
            Scene scene = EditorSceneManager.OpenScene(WorldScenePathLayout.GameplayScenePath, OpenSceneMode.Single);
            GameObject content = scene.GetRootGameObjects().Single(root => root.name == "Content");
            var report = new StringBuilder();
            foreach (Collider collider in content.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger || collider is CharacterController)
                {
                    continue;
                }
                if (collider.gameObject.layer != 0)
                {
                    report.AppendLine($"GROUND_LAYER {collider.name}: {collider.gameObject.layer} -> 0");
                    collider.gameObject.layer = 0;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider.gameObject);
                }
            }
            foreach (MeshFilter filter in content.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.gameObject.activeInHierarchy || filter.sharedMesh == null ||
                    !filter.name.StartsWith("prop_boardwalk", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (filter.GetComponent<Collider>() == null)
                {
                    MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    report.AppendLine("ADDED_BOARDWALK_COLLIDER " + filter.name);
                }
                filter.gameObject.layer = 0;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter.gameObject);
            }
            Physics.SyncTransforms();
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Docs/CollisionAssetRefresh");
            File.WriteAllText("Docs/CollisionAssetRefresh/ground-repair.txt", report.ToString());
        }
    }
}
