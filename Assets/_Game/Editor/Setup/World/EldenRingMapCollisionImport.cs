using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Imports source HKX collision triangles into maps that passed geometry import.</summary>
    [InitializeOnLoad]
    public static class EldenRingMapCollisionImport
    {
        private const string k_Root = "F:/MyProject/RE/EldenRing/Converted/MapWorld/Maps";
        private const string k_Enabled = "ZZ.EldenRing.MapCollisions";
        private const string k_CollisionRoot = "SourceCollision";
        private static double s_NextCheck;

        static EldenRingMapCollisionImport()
        {
            EditorApplication.update += ImportNext;
            s_NextCheck = EditorApplication.timeSinceStartup + 60;
        }

        /// <summary>Resumes importing prepared collision geometry in map ID order.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Import Prepared Map Collisions")]
        public static void Start()
        {
            SessionState.SetBool(k_Enabled, true);
            s_NextCheck = 0;
        }

        /// <summary>Stops before the next map without discarding saved collisions.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Stop Map Collision Import")]
        public static void Stop()
        {
            SessionState.SetBool(k_Enabled, false);
        }

        private static void ImportNext()
        {
            if (!SessionState.GetBool(k_Enabled, false) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.timeSinceStartup < s_NextCheck)
            {
                return;
            }
            s_NextCheck = EditorApplication.timeSinceStartup + 15;
            foreach (string path in Directory.GetFiles(k_Root, "collision.json", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                string folder = Path.GetDirectoryName(path);
                string physicsPath = folder + "/physics.json";
                if (File.Exists(physicsPath) && File.Exists(folder + "/unity-collision-result.json") &&
                    !File.Exists(folder + "/unity-physics-result.json"))
                {
                    try
                    {
                        ImportPhysics(path, physicsPath);
                    }
                    catch (Exception error)
                    {
                        File.WriteAllText(folder + "/unity-physics-error.txt", error.ToString());
                        SessionState.SetBool(k_Enabled, false);
                        Debug.LogException(error);
                    }
                    s_NextCheck = EditorApplication.timeSinceStartup + 15;
                    return;
                }
                if (File.Exists(folder + "/unity-collision-result.json") ||
                    !File.Exists(folder + "/unity-result.json"))
                {
                    continue;
                }
                GeometryResult geometry = JsonUtility.FromJson<GeometryResult>(File.ReadAllText(folder + "/unity-result.json"));
                if (geometry.Version != 3 ||
                    (geometry.Status != "partial" && geometry.Status != "verified_static_geometry"))
                {
                    continue;
                }
                try
                {
                    Import(path);
                }
                catch (Exception error)
                {
                    File.WriteAllText(folder + "/unity-collision-error.txt", error.ToString());
                    SessionState.SetBool(k_Enabled, false);
                    Debug.LogException(error);
                }
                s_NextCheck = EditorApplication.timeSinceStartup + 15;
                return;
            }
        }

        /// <summary>Imports one selected map's prepared collision and supported physics parameters.</summary>
        public static void ImportPreparedMap(string mapId)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(mapId, @"^m\d{2}_\d{2}_\d{2}_\d{2}$"))
            {
                throw new ArgumentException("Invalid original map ID.", nameof(mapId));
            }
            string folder = k_Root + "/" + mapId;
            Import(folder + "/collision.json");
            ImportPhysics(folder + "/collision.json", folder + "/physics.json");
        }

        private static void Import(string path)
        {
            if (new DriveInfo(Application.dataPath.Substring(0, 3)).AvailableFreeSpace < 12L * 1024 * 1024 * 1024)
            {
                throw new IOException("Less than 12 GiB free; collision import stopped.");
            }
            MapDocument source = JsonUtility.FromJson<MapDocument>(File.ReadAllText(path));
            if (source.Map != new DirectoryInfo(Path.GetDirectoryName(path)).Name ||
                !System.Text.RegularExpressions.Regex.IsMatch(source.Map, @"^m\d{2}_\d{2}_\d{2}_\d{2}$"))
            {
                throw new InvalidDataException("Collision manifest map ID differs from its directory.");
            }
            string scenePath = "Assets/_Game/Scenes/Maps/" + source.Map.Substring(0, 3) + "/SCN_" + source.Map + ".unity";
            Scene previous = SceneManager.GetActiveScene();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == scenePath || loaded.isDirty || string.IsNullOrEmpty(loaded.path))
                {
                    throw new InvalidOperationException("Collision import requires saved scenes and the target map closed.");
                }
            }
            var meshes = new Dictionary<string, Mesh[]>();
            foreach (Binding binding in source.Bindings)
            {
                using (FileStream input = File.OpenRead(binding.Path))
                using (SHA256 hash = SHA256.Create())
                {
                    if (BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "") != binding.SHA256)
                    {
                        throw new InvalidDataException("Collision geometry hash differs: " + binding.Model);
                    }
                }
                Document data = JsonUtility.FromJson<Document>(File.ReadAllText(binding.Path));
                string folder = "Assets/_Game/Art/Environment/EldenRing/World/Collisions/" + source.Map + "/" + binding.Model;
                Directory.CreateDirectory(folder);
                var modelMeshes = new List<Mesh>();
                for (int index = 0; index < data.Meshes.Length; index++)
                {
                    MeshRecord record = data.Meshes[index];
                    int[] indices = OriginalAssetCollisionSetup.MirrorWinding(record.Indices);
                    Vector3[] vertices = Enumerable.Range(0, record.Positions.Length / 3)
                        .Select(i => new Vector3(-record.Positions[i * 3], record.Positions[i * 3 + 1], record.Positions[i * 3 + 2])).ToArray();
                    string meshPath = CollisionMeshPath(folder, record.Name, index);
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        mesh = new Mesh { name = record.Name, indexFormat = IndexFormat.UInt32 };
                        mesh.vertices = vertices;
                        mesh.triangles = indices;
                        mesh.RecalculateBounds();
                        AssetDatabase.CreateAsset(mesh, meshPath);
                    }
                    else if (mesh.vertices.SequenceEqual(vertices) && mesh.triangles.SequenceEqual(record.Indices))
                    {
                        mesh.triangles = indices;
                    }
                    mesh.name = record.Name;
                    EditorUtility.SetDirty(mesh);
                    if (!mesh.vertices.SequenceEqual(vertices) || !mesh.triangles.SequenceEqual(indices))
                    {
                        throw new InvalidDataException("Saved collision mesh differs: " + binding.Model);
                    }
                    modelMeshes.Add(mesh);
                }
                meshes.Add(binding.Model, modelMeshes.ToArray());
            }
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject oldRoot = scene.GetRootGameObjects().SingleOrDefault(item => item.name == k_CollisionRoot);
                if (oldRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldRoot);
                }
                var root = new GameObject(k_CollisionRoot);
                int count = 0;
                foreach (Part part in source.Parts)
                {
                    if (!meshes.TryGetValue(part.ModelName, out Mesh[] modelMeshes))
                    {
                        continue;
                    }
                    var instance = new GameObject(part.Name);
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.localPosition = new Vector3(-part.Position[0], part.Position[1], part.Position[2]);
                    instance.transform.localScale = new Vector3(part.Scale[0], part.Scale[1], part.Scale[2]);
                    instance.transform.localRotation = EldenRingMapSceneImport.ConvertRotation(
                        new Vector3(part.Rotation[0], part.Rotation[1], part.Rotation[2]));
                    foreach (Mesh mesh in modelMeshes)
                    {
                        var body = new GameObject(mesh.name);
                        body.transform.SetParent(instance.transform, false);
                        body.AddComponent<MeshCollider>().sharedMesh = mesh;
                    }
                    count++;
                }
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                {
                    throw new IOException("Could not save collision scene.");
                }
                EditorSceneManager.CloseScene(scene, true);
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                GameObject savedRoot = scene.GetRootGameObjects().Single(item => item.name == k_CollisionRoot);
                if (savedRoot.transform.childCount != count)
                {
                    throw new InvalidDataException("Saved collision instance count differs.");
                }
                foreach (Part part in source.Parts.Where(part => meshes.ContainsKey(part.ModelName)))
                {
                    Transform instance = savedRoot.transform.Find(part.Name);
                    Quaternion rotation = EldenRingMapSceneImport.ConvertRotation(
                        new Vector3(part.Rotation[0], part.Rotation[1], part.Rotation[2]));
                    if (instance == null || Vector3.Distance(instance.localPosition,
                            new Vector3(-part.Position[0], part.Position[1], part.Position[2])) > 0.0001f ||
                        Vector3.Distance(instance.localScale, new Vector3(part.Scale[0], part.Scale[1], part.Scale[2])) > 0.00001f ||
                        Quaternion.Angle(instance.localRotation, rotation) > 0.01f ||
                        !instance.GetComponentsInChildren<MeshCollider>().Select(collider => collider.sharedMesh)
                            .SequenceEqual(meshes[part.ModelName]))
                    {
                        throw new InvalidDataException("Saved collision placement differs: " + part.Name);
                    }
                }
                Physics.SyncTransforms();
                int rays = 0;
                foreach (MeshCollider collider in savedRoot.GetComponentsInChildren<MeshCollider>())
                {
                    Mesh mesh = collider.sharedMesh;
                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;
                    bool tested = false;
                    for (int i = 0; i < triangles.Length && !tested; i += 3)
                    {
                        Vector3 a = collider.transform.TransformPoint(vertices[triangles[i]]);
                        Vector3 b = collider.transform.TransformPoint(vertices[triangles[i + 1]]);
                        Vector3 c = collider.transform.TransformPoint(vertices[triangles[i + 2]]);
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        if (normal.sqrMagnitude < 0.000001f)
                        {
                            continue;
                        }
                        normal.Normalize();
                        Vector3 center = (a + b + c) / 3f;
                        if (!collider.Raycast(new Ray(center + normal * 0.01f, -normal), out RaycastHit hit, 0.02f) ||
                            Vector3.Distance(hit.point, center) > 0.02f)
                        {
                            throw new InvalidDataException("Collision ray test failed: " + collider.name);
                        }
                        tested = true;
                        rays++;
                    }
                }
                File.WriteAllText(Path.GetDirectoryName(path) + "/unity-collision-result.json",
                    JsonUtility.ToJson(new Result { Map = source.Map, Instances = count, RayTests = rays,
                        UnresolvedInstances = source.Parts.Length - count,
                        Scope = "Source high-resolution map collision triangles; Havok surface response and movable asset physics are not restored.",
                        Status = source.Status == "ready" ? "verified_collision_triangles" : "partial" }, true));
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static string CollisionMeshPath(string folder, string sourceName, int index)
        {
            if (string.IsNullOrEmpty(sourceName) || sourceName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                sourceName.EndsWith(".", StringComparison.Ordinal) || sourceName.EndsWith(" ", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Source collision body name is not a valid filename.");
            }
            string path = folder + "/" + sourceName + ".asset";
            string legacy = folder + "/" + index + ".asset";
            if (!File.Exists(path) && File.Exists(legacy))
            {
                string error = AssetDatabase.MoveAsset(legacy, path);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new IOException(error);
                }
            }
            return path;
        }

        private static PhysicsMaterialCombine ConvertCombine(string source)
        {
            switch (source)
            {
                case "COMBINE_MIN": return PhysicsMaterialCombine.Minimum;
                case "COMBINE_MAX": return PhysicsMaterialCombine.Maximum;
                case "COMBINE_AVERAGE": return PhysicsMaterialCombine.Average;
                case "COMBINE_MULTIPLY": return PhysicsMaterialCombine.Multiply;
                default: throw new InvalidDataException("Unsupported Havok combine policy: " + source);
            }
        }

        private static void VerifyPhysics(PhysicsMaterial material, PhysicsBody source)
        {
            if (material == null || material.dynamicFriction != source.DynamicFriction ||
                material.staticFriction != source.StaticFriction || material.bounciness != source.Restitution ||
                material.frictionCombine != ConvertCombine(source.FrictionCombine) ||
                material.bounceCombine != ConvertCombine(source.RestitutionCombine))
            {
                throw new InvalidDataException("Saved physics material differs: " + source.Name);
            }
        }

        private static void ImportPhysics(string collisionPath, string physicsPath)
        {
            MapDocument collision = JsonUtility.FromJson<MapDocument>(File.ReadAllText(collisionPath));
            PhysicsDocument source = JsonUtility.FromJson<PhysicsDocument>(File.ReadAllText(physicsPath));
            using (SHA256 hash = SHA256.Create())
            {
                string digest = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(collisionPath))).Replace("-", "");
                if (source.Version != 1 || source.Map != collision.Map || source.CollisionSHA256 != digest)
                {
                    throw new InvalidDataException("Physics manifest differs from source collision manifest.");
                }
            }
            string scenePath = "Assets/_Game/Scenes/Maps/" + source.Map.Substring(0, 3) + "/SCN_" + source.Map + ".unity";
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene loaded = SceneManager.GetSceneAt(index);
                if (loaded.isDirty || string.IsNullOrEmpty(loaded.path) || loaded.path == scenePath)
                {
                    throw new InvalidOperationException("Physics import requires saved scenes and the target closed.");
                }
            }
            var materials = new Dictionary<string, PhysicsMaterial[]>();
            foreach (PhysicsModel model in source.Models)
            {
                Binding binding = collision.Bindings.Single(item => item.Model == model.Model);
                if (model.SourceSHA256 != binding.SourceSHA256)
                {
                    throw new InvalidDataException("Physics source HKX hash differs: " + model.Model);
                }
                Document geometry = JsonUtility.FromJson<Document>(File.ReadAllText(binding.Path));
                if (!geometry.Meshes.Select(item => item.Name).SequenceEqual(model.Bodies.Select(item => item.Name)))
                {
                    throw new InvalidDataException("Physics body order differs from collision mesh order.");
                }
                string folder = "Assets/_Game/Art/Environment/EldenRing/World/Collisions/" + source.Map + "/" + model.Model;
                var saved = new List<PhysicsMaterial>();
                for (int index = 0; index < model.Bodies.Length; index++)
                {
                    PhysicsBody body = model.Bodies[index];
                    string meshPath = CollisionMeshPath(folder, body.Name, index);
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        throw new InvalidDataException("Source collision mesh is missing: " + body.Name);
                    }
                    mesh.name = body.Name;
                    EditorUtility.SetDirty(mesh);
                    if (body.TriggerType != "TRIGGER_TYPE_NONE" ||
                        new[] { body.DynamicFriction, body.StaticFriction, body.Restitution }.Any(value => float.IsNaN(value) || value < 0 || value > 1))
                    {
                        throw new InvalidDataException("Unsupported source physics material: " + body.Name);
                    }
                    string assetPath = folder + "/" + index + ".physicMaterial";
                    PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(assetPath);
                    if (material == null)
                    {
                        material = new PhysicsMaterial();
                        AssetDatabase.CreateAsset(material, assetPath);
                    }
                    material.name = body.MaterialName;
                    material.dynamicFriction = body.DynamicFriction;
                    material.staticFriction = body.StaticFriction;
                    material.bounciness = body.Restitution;
                    material.frictionCombine = ConvertCombine(body.FrictionCombine);
                    material.bounceCombine = ConvertCombine(body.RestitutionCombine);
                    EditorUtility.SetDirty(material);
                    VerifyPhysics(material, body);
                    saved.Add(material);
                }
                materials.Add(model.Model, saved.ToArray());
            }
            AssetDatabase.SaveAssets();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                GameObject root = scene.GetRootGameObjects().Single(item => item.name == k_CollisionRoot);
                int count = 0;
                foreach (Part part in collision.Parts.Where(part => materials.ContainsKey(part.ModelName)))
                {
                    Transform instance = root.transform.Find(part.Name);
                    MeshCollider[] colliders = instance.GetComponentsInChildren<MeshCollider>();
                    PhysicsModel model = source.Models.Single(item => item.Model == part.ModelName);
                    if (!colliders.Select(item => item.sharedMesh.name).SequenceEqual(model.Bodies.Select(item => item.Name)))
                    {
                        throw new InvalidDataException("Saved collider body order differs: " + part.Name);
                    }
                    for (int index = 0; index < colliders.Length; index++)
                    {
                        string expectedPath = "Assets/_Game/Art/Environment/EldenRing/World/Collisions/" +
                            source.Map + "/" + part.ModelName + "/" + model.Bodies[index].Name + ".asset";
                        if (AssetDatabase.GetAssetPath(colliders[index].sharedMesh) != expectedPath)
                        {
                            throw new InvalidDataException("Saved collider source mesh differs: " + part.Name);
                        }
                        colliders[index].name = model.Bodies[index].Name;
                        colliders[index].sharedMaterial = materials[part.ModelName][index];
                        count++;
                    }
                }
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                {
                    throw new IOException("Could not save physics materials in map scene.");
                }
                EditorSceneManager.CloseScene(scene, true);
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                root = scene.GetRootGameObjects().Single(item => item.name == k_CollisionRoot);
                int verified = 0;
                foreach (Part part in collision.Parts.Where(part => materials.ContainsKey(part.ModelName)))
                {
                    MeshCollider[] colliders = root.transform.Find(part.Name).GetComponentsInChildren<MeshCollider>();
                    PhysicsModel model = source.Models.Single(item => item.Model == part.ModelName);
                    for (int index = 0; index < model.Bodies.Length; index++)
                    {
                        if (colliders[index].name != model.Bodies[index].Name ||
                            colliders[index].sharedMesh.name != model.Bodies[index].Name)
                        {
                            throw new InvalidDataException("Saved collision body name differs: " + part.Name);
                        }
                        VerifyPhysics(colliders[index].sharedMaterial, model.Bodies[index]);
                        verified++;
                    }
                }
                if (verified != count)
                {
                    throw new InvalidDataException("Saved physics collider count differs.");
                }
                File.WriteAllText(Path.GetDirectoryName(physicsPath) + "/unity-physics-result.json",
                    JsonUtility.ToJson(new PhysicsResult { Map = source.Map, Bodies = verified,
                        Status = "verified_source_friction_restitution_and_combine_references",
                        Scope = "Original body material coefficients and combine policies mapped to PhysX; Havok solver, per-triangle materials, filters and dynamic behavior are not reproduced." }, true));
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Serializable] private sealed class PhysicsDocument
        {
            public int Version;
            public string Map;
            public string CollisionSHA256;
            public PhysicsModel[] Models;
        }
        [Serializable] private sealed class PhysicsModel
        {
            public string Model;
            public string SourceSHA256;
            public PhysicsBody[] Bodies;
        }
        [Serializable] private sealed class PhysicsBody
        {
            public string Name;
            public string MaterialName;
            public string TriggerType;
            public float DynamicFriction;
            public float StaticFriction;
            public float Restitution;
            public string FrictionCombine;
            public string RestitutionCombine;
        }
        [Serializable] private sealed class PhysicsResult
        {
            public string Map;
            public string Status;
            public int Bodies;
            public string Scope;
        }

        [Serializable] private sealed class MapDocument
        {
            public string Map;
            public string Status;
            public Binding[] Bindings;
            public Part[] Parts;
        }
        [Serializable] private sealed class Binding
        {
            public string Model;
            public string Path;
            public string SHA256;
            public string SourceSHA256;
        }
        [Serializable] private sealed class Part
        {
            public string Name;
            public string ModelName;
            public float[] Position;
            public float[] Rotation;
            public float[] Scale;
        }
        [Serializable] private sealed class Document { public MeshRecord[] Meshes; }
        [Serializable] private sealed class MeshRecord { public string Name; public float[] Positions; public int[] Indices; }
        [Serializable] private sealed class GeometryResult { public int Version; public string Status; }
        [Serializable] private sealed class Result
        {
            public string Map;
            public string Status;
            public int Instances;
            public int RayTests;
            public int UnresolvedInstances;
            public string Scope;
        }
    }
}
