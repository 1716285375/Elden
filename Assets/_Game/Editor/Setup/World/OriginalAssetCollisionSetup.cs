using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Installs the AEG collision layer that contains the cave's actual floors and walls.</summary>
    public static class OriginalAssetCollisionSetup
    {
        [MenuItem("Tools/ZZ/Elden Ring/Audit Groveside Collision")]
        public static void Audit()
        {
            Scene scene = SceneManager.GetSceneByPath(OriginalSingleMapSetup.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened)
            {
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(OriginalSingleMapSetup.ScenePath,
                    UnityEditor.SceneManagement.OpenSceneMode.Additive);
            }
            bool backfaces = Physics.queriesHitBackfaces;
            var report = new System.Text.StringBuilder();
            try
            {
                Physics.SyncTransforms();
                Transform entrance = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>())
                    .Single(item => item.name == "Player Spawn Point");
                UnityEngine.AI.NavMesh.SamplePosition(entrance.position, out var origin, 2f, UnityEngine.AI.NavMesh.AllAreas);
                foreach (AICharacterSpawner spawn in UnityEngine.Object.FindObjectsByType<AICharacterSpawner>(FindObjectsSortMode.None))
                {
                    report.AppendLine(spawn.name + " " + spawn.transform.position);
                    foreach (bool includeBackfaces in new[] { false, true })
                    {
                        Physics.queriesHitBackfaces = includeBackfaces;
                        bool found = Physics.Raycast(spawn.transform.position + Vector3.up, Vector3.down,
                            out RaycastHit hit, 15f, ~0, QueryTriggerInteraction.Ignore);
                        report.AppendLine("Backfaces=" + includeBackfaces + " floor=" + found + " point=" + hit.point +
                            " normal=" + hit.normal + " collider=" + hit.collider?.name);
                    }
                    bool navigation = UnityEngine.AI.NavMesh.SamplePosition(spawn.transform.position, out var nearest,
                        20f, UnityEngine.AI.NavMesh.AllAreas);
                    report.AppendLine("Navigation=" + navigation + " point=" + nearest.position);
                    var path = new UnityEngine.AI.NavMeshPath();
                    UnityEngine.AI.NavMesh.CalculatePath(origin.position, nearest.position, UnityEngine.AI.NavMesh.AllAreas, path);
                    report.AppendLine("Route=" + path.status + " corners=" + string.Join(" -> ", path.corners.Select(point => point.ToString("F3"))));
                }
                File.WriteAllText("Docs/OriginalSingleMap/collision-audit.txt", report.ToString());
            }
            finally
            {
                Physics.queriesHitBackfaces = backfaces;
                if (opened)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        public static void Install(Scene scene)
        {
            const string sourceFolder = "F:/MyProject/RE/EldenRing/Converted/SingleMapGameplay/AssetCollision";
            const string output = "Assets/_Game/Art/Environment/EldenRing/World/Collisions/m31_03_00_00/Assets";
            JObject manifest = JObject.Parse(File.ReadAllText(sourceFolder + "/manifest.json"));
            JObject world = JObject.Parse(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/MapWorld/Maps/m31_03_00_00/world.json"));
            JToken[] bindings = manifest["Bindings"].ToArray();
            string[] expected = world["Parts"].Where(part => part.Value<string>("Type") == "Asset")
                .Select(part => part.Value<string>("ModelName")).Distinct().OrderBy(model => model).ToArray();
            if (!bindings.Select(binding => binding.Value<string>("Model")).OrderBy(model => model).SequenceEqual(expected) ||
                bindings.Any(binding => binding.Value<string>("Status") == "failed"))
            {
                throw new InvalidDataException("Original AEG collision export is incomplete.");
            }
            GameObject previous = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "SourceAssetCollision");
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous);
            }
            var root = new GameObject("SourceAssetCollision");
            SceneManager.MoveGameObjectToScene(root, scene);
            JObject mapCollision = JObject.Parse(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/MapWorld/Maps/m31_03_00_00/collision.json"));
            JArray collisionParts = JArray.Parse(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/SingleMapGameplay/collision-parts.json"));
            foreach (JToken binding in mapCollision["Bindings"])
            {
                JToken part = collisionParts.Single(value => value.Value<string>("Name") == binding.Value<string>("Model"));
                if (part.Value<int>("HitFilterID") == 11)
                {
                    // ER filter 11 is NPCOnly; retain the enemy navigation barrier while allowing the player through.
                    Transform instance = scene.GetRootGameObjects().Single(value => value.name == "SourceCollision")
                        .transform.Find(part.Value<string>("Name"));
                    foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
                    {
                        collider.excludeLayers = 1 << LayerMask.NameToLayer("Player");
                        collider.layerOverridePriority = 1;
                    }
                }
                foreach (JToken record in JObject.Parse(File.ReadAllText(binding.Value<string>("Path")))["Meshes"])
                {
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                        "Assets/_Game/Art/Environment/EldenRing/World/Collisions/m31_03_00_00/" +
                        binding.Value<string>("Model") + "/" + record.Value<string>("Name") + ".asset");
                    int[] original = record["Indices"].ToObject<int[]>();
                    int[] mirrored = MirrorWinding(original);
                    if (mesh == null || (!mesh.triangles.SequenceEqual(original) && !mesh.triangles.SequenceEqual(mirrored)))
                    {
                        throw new InvalidDataException("Original map collision topology differs.");
                    }
                    if (mesh.triangles.SequenceEqual(original))
                    {
                        mesh.triangles = mirrored;
                        EditorUtility.SetDirty(mesh);
                        AssetDatabase.SaveAssetIfDirty(mesh);
                    }
                }
            }
            int instances = 0;
            foreach (JToken binding in bindings.Where(binding => binding.Value<string>("Status") == "verified"))
            {
                string path = binding.Value<string>("Path");
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(path);
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (hash != binding.Value<string>("SHA256"))
                {
                    throw new InvalidDataException("Original AEG collision hash differs: " + path);
                }
                string model = binding.Value<string>("Model");
                JArray records = (JArray)JObject.Parse(File.ReadAllText(path))["Meshes"];
                Directory.CreateDirectory(output + "/" + model);
                Mesh[] meshes = records.Select((record, index) =>
                {
                    float[] positions = record["Positions"].ToObject<float[]>();
                    int[] indices = MirrorWinding(record["Indices"].ToObject<int[]>());
                    if (positions.Length % 3 != 0 || indices.Length % 3 != 0 ||
                        indices.Any(vertex => vertex < 0 || vertex >= positions.Length / 3))
                    {
                        throw new InvalidDataException("Invalid original AEG collision topology: " + model);
                    }
                    string assetPath = output + "/" + model + "/" + index + ".asset";
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    bool create = mesh == null;
                    if (create)
                    {
                        mesh = new Mesh { name = model + " Collision " + index, indexFormat = IndexFormat.UInt32 };
                    }
                    Vector3[] vertices = Enumerable.Range(0, positions.Length / 3).Select(vertex =>
                        new Vector3(-positions[vertex * 3], positions[vertex * 3 + 1], positions[vertex * 3 + 2])).ToArray();
                    if (!create && mesh.vertices.SequenceEqual(vertices) && mesh.triangles.SequenceEqual(indices))
                    {
                        return mesh;
                    }
                    mesh.Clear();
                    mesh.vertices = vertices;
                    mesh.triangles = indices;
                    mesh.RecalculateBounds();
                    if (create)
                    {
                        AssetDatabase.CreateAsset(mesh, assetPath);
                    }
                    EditorUtility.SetDirty(mesh);
                    AssetDatabase.SaveAssetIfDirty(mesh);
                    return mesh;
                }).ToArray();
                foreach (JToken part in world["Parts"].Where(part => part.Value<string>("ModelName") == model &&
                    part.Value<int>("GameEditionDisable") != 1))
                {
                    var instance = new GameObject(part.Value<string>("Name"));
                    instance.transform.SetParent(root.transform, false);
                    float[] position = part["Position"].ToObject<float[]>();
                    float[] rotation = part["Rotation"].ToObject<float[]>();
                    float[] scale = part["Scale"].ToObject<float[]>();
                    instance.transform.localPosition = new Vector3(-position[0], position[1], position[2]);
                    instance.transform.localRotation = EldenRingMapSceneImport.ConvertRotation(new Vector3(rotation[0], rotation[1], rotation[2]));
                    instance.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);
                    foreach (Mesh mesh in meshes)
                    {
                        var body = new GameObject(mesh.name);
                        body.transform.SetParent(instance.transform, false);
                        body.AddComponent<MeshCollider>().sharedMesh = mesh;
                    }
                    instances++;
                }
            }
            if (instances == 0)
            {
                throw new InvalidDataException("Original AEG collision layer is empty.");
            }
            Physics.SyncTransforms();
            Debug.Log("ORIGINAL_ASSET_COLLISION instances=" + instances);
        }

        public static int[] MirrorWinding(int[] indices)
        {
            if (indices == null || indices.Length % 3 != 0)
            {
                throw new InvalidDataException("Collision indices must contain complete triangles.");
            }
            int[] mirrored = (int[])indices.Clone();
            for (int index = 0; index < mirrored.Length; index += 3)
            {
                (mirrored[index + 1], mirrored[index + 2]) = (mirrored[index + 2], mirrored[index + 1]);
            }
            return mirrored;
        }
    }
}
