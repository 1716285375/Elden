using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Records physical floor coverage and currently rendered equipment before replacement.</summary>
    public static class VillageCollisionAssetAudit
    {
        [MenuItem("Tools/ZZ/Environment/Audit Village Collision And Props")]
        public static void Audit()
        {
            Directory.CreateDirectory("Docs/CollisionAssetRefresh");
            Scene scene = EditorSceneManager.OpenScene(WorldScenePathLayout.GameplayScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var report = new StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) { continue; }
                    Renderer renderer = filter.GetComponent<Renderer>();
                    if (renderer == null) { continue; }
                    Bounds bounds = renderer.bounds;
                    string lower = filter.name.ToLowerInvariant();
                    if ((bounds.size.x * bounds.size.z > 15f && bounds.size.y < 4f) ||
                        lower.Contains("terrain") || lower.Contains("ground") || lower.Contains("dock") ||
                        lower.Contains("bridge") || lower.Contains("walk") || lower.Contains("chest"))
                    {
                        report.AppendLine($"SURFACE {filter.name}; position={filter.transform.position}; bounds={bounds}; " +
                            $"active={filter.gameObject.activeInHierarchy}; layer={filter.gameObject.layer}; mesh={AssetDatabase.GetAssetPath(filter.sharedMesh)}; " +
                            $"colliders={string.Join(",", filter.GetComponents<Collider>().Select(c => c.GetType().Name + ":" + c.enabled + ":trigger=" + c.isTrigger))}");
                    }
                }
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    report.AppendLine($"TERRAIN {terrain.name}; position={terrain.transform.position}; size={terrain.terrainData.size}; collider={terrain.GetComponent<TerrainCollider>()?.enabled}");
                }
            }
            NavMeshTriangulation nav = NavMesh.CalculateTriangulation();
            int missing = 0;
            for (int index = 0; index < nav.indices.Length; index += 3)
            {
                Vector3 point = (nav.vertices[nav.indices[index]] + nav.vertices[nav.indices[index + 1]] +
                    nav.vertices[nav.indices[index + 2]]) / 3f;
                if (!Physics.RaycastAll(point + Vector3.up * 0.7f, Vector3.down, 1.2f, ~0,
                    QueryTriggerInteraction.Ignore).Any(hit => hit.normal.y > 0.5f))
                {
                    missing++;
                    report.AppendLine("UNSUPPORTED_NAV " + point);
                }
            }
            report.AppendLine($"NAV_TRIANGLES={nav.indices.Length / 3}; UNSUPPORTED={missing}");
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Equipment", "Assets/_Game/Prefabs/World" })
                .Select(AssetDatabase.GUIDToAssetPath))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (MeshFilter mesh in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    report.AppendLine($"PROP {path}; object={mesh.name}; vertices={mesh.sharedMesh?.vertexCount}; " +
                        $"render={mesh.GetComponent<Renderer>()?.enabled}; mesh={AssetDatabase.GetAssetPath(mesh.sharedMesh)}");
                }
            }
            File.WriteAllText("Docs/CollisionAssetRefresh/baseline.txt", report.ToString());
        }

        [MenuItem("Tools/ZZ/Environment/Audit Legacy Gameplay Props")]
        public static void AuditGameplayProps()
        {
            Scene scene = EditorSceneManager.OpenScene(WorldScenePathLayout.GameplayScenePath, OpenSceneMode.Single);
            Transform gameplay = scene.GetRootGameObjects().Single(root => root.name == "_VillageGameplay").transform;
            var report = new StringBuilder();
            foreach (MeshFilter mesh in gameplay.GetComponentsInChildren<MeshFilter>(true))
            {
                Transform root = mesh.transform;
                while (root.parent != gameplay && root.parent != null) { root = root.parent; }
                report.AppendLine($"SCENE_PROP {root.name}/{mesh.name}; mesh={AssetDatabase.GetAssetPath(mesh.sharedMesh)}; " +
                    $"components={string.Join(",", root.GetComponentsInChildren<Component>(true).Where(c => c != null).Select(c => c.GetType().Name).Distinct())}");
            }
            File.WriteAllText("Docs/CollisionAssetRefresh/legacy-scene-props.txt", report.ToString());
        }
    }
}
