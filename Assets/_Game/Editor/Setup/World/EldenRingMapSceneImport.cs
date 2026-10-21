using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Reconstructs original static placements in a separate scene, with explicit coverage evidence.</summary>
    public static class EldenRingMapSceneImport
    {
        private static string s_Source = "F:/MyProject/RE/EldenRing/Converted/Map_Stormveil";
        private static string s_Output = "Assets/_Game/Art/Environment/EldenRing/StormveilWorld";
        private static string s_Scene = "Assets/_Game/Scenes/Recovered/SCN_Stormveil.unity";
        private static string s_Report = "F:/MyProject/RE/EldenRing/Logs/stormveil-scene.txt";
        private static string s_Models;
        private static string s_RootName = "Stormveil Original Static Layout";
        private static string s_CapturePath = "F:/MyProject/RE/EldenRing/Logs/stormveil-original-scene.png";
        private static bool s_ProbeSurfaces;

        /// <summary>Identifies original meshes behind representative preview pixels without saving the scene.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Inspect Stormveil Preview Surfaces")]
        public static void InspectPreviewSurfaces()
        {
            s_ProbeSurfaces = true;
            try
            {
                CaptureFirstImportedMap();
            }
            finally
            {
                s_ProbeSurfaces = false;
            }
        }

        /// <summary>Renders the first imported map for direct geometry inspection.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Capture First Imported Map")]
        public static void CaptureFirstImportedMap()
        {
            string previousScene = s_Scene;
            string previousCapture = s_CapturePath;
            try
            {
                s_Scene = "Assets/_Game/Scenes/Maps/m10/SCN_m10_00_00_00.unity";
                s_CapturePath = "F:/MyProject/RE/EldenRing/Converted/MapWorld/Maps/m10_00_00_00/capture.png";
                Capture();
            }
            finally
            {
                s_Scene = previousScene;
                s_CapturePath = previousCapture;
            }
        }

        /// <summary>Imports one map in its own local coordinate system and verifies saved geometry and placements.</summary>
        public static void ImportMap(string mapId)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(mapId, @"^m\d{2}_\d{2}_\d{2}_\d{2}$"))
            {
                throw new ArgumentException("Invalid original map ID.", nameof(mapId));
            }
            string previousSource = s_Source;
            string previousOutput = s_Output;
            string previousScene = s_Scene;
            string previousReport = s_Report;
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene workspace = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(workspace);
            try
            {
                s_Source = "F:/MyProject/RE/EldenRing/Converted/MapWorld/Maps/" + mapId;
                s_Models = "F:/MyProject/RE/EldenRing/Converted/MapWorld/Models";
                s_Output = "Assets/_Game/Art/Environment/EldenRing/World";
                s_Scene = "Assets/_Game/Scenes/Maps/" + mapId.Substring(0, 3) + "/SCN_" + mapId + ".unity";
                s_Report = s_Source + "/unity-scene.txt";
                s_RootName = mapId;
                Directory.CreateDirectory(Path.GetDirectoryName(s_Scene));
                Build();
                Verify();
            }
            finally
            {
                SceneManager.SetActiveScene(previousActiveScene);
                if (workspace.IsValid() && workspace.isLoaded)
                {
                    EditorSceneManager.CloseScene(workspace, true);
                }
                s_Source = previousSource;
                s_Output = previousOutput;
                s_Scene = previousScene;
                s_Report = previousReport;
                s_Models = null;
                s_RootName = "Stormveil Original Static Layout";
            }
        }

        private static string ModelPath(string model)
        {
            if (s_Models == null)
            {
                return s_Source + "/Models/" + model + ".json";
            }
            string legacy = "F:/MyProject/RE/EldenRing/Converted/Map_Stormveil/Models/" + model + ".json";
            return File.Exists(legacy) ? legacy : s_Models + "/" + model + ".json.gz";
        }

        private static Model ReadModel(string path)
        {
            if (!path.EndsWith(".gz", StringComparison.Ordinal))
            {
                return JsonUtility.FromJson<Document>(File.ReadAllText(path)).Models.Single();
            }
            using var stream = File.OpenRead(path);
            using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return JsonUtility.FromJson<Document>(reader.ReadToEnd()).Models.Single();
        }

        /// <summary>Converts original XZY rotation to the mirrored Unity coordinate basis.</summary>
        public static Quaternion ConvertRotation(Vector3 degrees) =>
            Quaternion.AngleAxis(-degrees.y, Vector3.up) * Quaternion.AngleAxis(-degrees.z, Vector3.forward) *
            Quaternion.AngleAxis(degrees.x, Vector3.right);

        /// <summary>Builds the recovered scene without replacing existing gameplay scenes or save identities.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Build Original Stormveil Scene")]
        public static void Build()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before importing the original map.");
            }
            if (SceneManager.GetSceneByPath(s_Scene).isLoaded)
            {
                throw new InvalidOperationException("Close the recovered scene before rebuilding it.");
            }
            World source = JsonUtility.FromJson<World>(File.ReadAllText(s_Source + "/world.json"));
            Directory.CreateDirectory(s_Output + "/Models");
            Directory.CreateDirectory(s_Output + "/Materials");
            Directory.CreateDirectory("Assets/_Game/Scenes/Recovered");
            AssetDatabase.Refresh();
            var prefabs = new Dictionary<string, GameObject>();
            var report = new StringBuilder();
            int completed = 0;
            foreach (Binding binding in source.Bindings)
            {
                string path = ModelPath(binding.Model);
                if (!File.Exists(path) || binding.Status == "Failed")
                {
                    report.AppendLine("UNSUPPORTED_MODEL " + binding.Name);
                    continue;
                }
                Model model = ReadModel(path);
                if (!string.IsNullOrEmpty(binding.GeometrySHA256))
                {
                    using var stream = File.OpenRead(path);
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                    if (!hash.Equals(binding.GeometrySHA256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Source geometry cache hash differs: " + binding.Name);
                    }
                }
                prefabs.Add(binding.Name, BuildModel(binding.Model, model));
                completed++;
                if (completed % 25 == 0)
                {
                    File.WriteAllText(s_Report, $"BUILDING_MODELS {completed}/{source.Bindings.Length}\n");
                }
            }
            Scene scene = s_Models == null ?
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive) :
                SceneManager.GetActiveScene();
            try
            {
                var root = new GameObject(s_RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                int placed = 0;
                int unresolved = 0;
                foreach (Part part in source.Parts)
                {
                    if (!prefabs.TryGetValue(part.ModelName, out GameObject prefab))
                    {
                        report.AppendLine("UNRESOLVED_PLACEMENT " + part.Name + " -> " + part.ModelName);
                        unresolved++;
                        continue;
                    }
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.name = part.Name;
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.localPosition = Mirror(Vector(part.Position));
                    instance.transform.localRotation = ConvertRotation(Vector(part.Rotation));
                    instance.transform.localScale = Vector(part.Scale);
                    placed++;
                }
                var sunlight = new GameObject("Recovery Preview Sun");
                SceneManager.MoveGameObjectToScene(sunlight, scene);
                Light light = sunlight.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
                sunlight.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                EditorSceneManager.SaveScene(scene, s_Scene);
                report.Insert(0, $"Map={source.Map}\nSourcePlacements={source.Parts.Length}\n" +
                    $"Placed={placed}\nUnresolvedPlacements={unresolved}\nModels={prefabs.Count}\n");
                report.AppendLine("LIMIT: original Havok collision, events, lighting and material layering are not restored.");
                File.WriteAllText(s_Report, report.ToString());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ORIGINAL_STORMVEIL_SCENE_BUILT " + s_Report);
        }

        /// <summary>Updates shared mesh winding without rebuilding placements or changing asset identities.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Update Original Map Winding")]
        public static void UpdateWinding()
        {
            if (Application.isPlaying || SceneManager.GetSceneByPath(s_Scene).isLoaded)
            {
                throw new InvalidOperationException("Stop Play Mode and close the recovered scene first.");
            }
            World source = JsonUtility.FromJson<World>(File.ReadAllText(s_Source + "/world.json"));
            int updated = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (Binding binding in source.Bindings)
                {
                    string path = ModelPath(binding.Model);
                    if (!File.Exists(path))
                    {
                        continue;
                    }
                    Model model = ReadModel(path);
                    for (int index = 0; index < model.Meshes.Length; index++)
                    {
                        MeshRecord record = model.Meshes[index];
                        if (record.Positions.Length == 0 || record.Indices.Length == 0)
                        {
                            continue;
                        }
                        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                            s_Output + "/Models/" + binding.Model + "/Mesh_" + index + ".asset");
                        if (mesh == null || mesh.vertexCount != record.Positions.Length / 3)
                        {
                            throw new InvalidDataException("Missing or incompatible mesh: " + binding.Model);
                        }
                        mesh.triangles = record.Indices;
                        mesh.RecalculateTangents();
                        EditorUtility.SetDirty(mesh);
                        updated++;
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            File.WriteAllText(s_Report.Replace("scene.txt", "winding.txt"), $"UpdatedMeshes={updated}\n");
        }

        /// <summary>Checks saved scene placements against the converted source, preserving opened scenes.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Verify Original Stormveil Scene")]
        public static void Verify()
        {
            if (Application.isPlaying || SceneManager.GetSceneByPath(s_Scene).isLoaded)
            {
                throw new InvalidOperationException("Stop Play Mode and close the recovered scene before verification.");
            }
            World source = JsonUtility.FromJson<World>(File.ReadAllText(s_Source + "/world.json"));
            var available = source.Bindings.Where(binding =>
                File.Exists(ModelPath(binding.Model)) && binding.Status != "Failed")
                .Select(binding => binding.Name).ToHashSet();
            Part[] expected = source.Parts.Where(part => available.Contains(part.ModelName)).ToArray();
            Scene scene = EditorSceneManager.OpenScene(s_Scene, OpenSceneMode.Additive);
            try
            {
                GameObject root = scene.GetRootGameObjects()
                    .Single(item => item.name == s_RootName);
                var placed = root.transform.Cast<Transform>().ToDictionary(item => item.name);
                if (placed.Count != expected.Length)
                {
                    throw new InvalidDataException($"Saved placements {placed.Count}, expected {expected.Length}.");
                }
                foreach (Part part in expected)
                {
                    Quaternion expectedRotation = ConvertRotation(Vector(part.Rotation));
                    if (!placed.TryGetValue(part.Name, out Transform instance) ||
                        Vector3.Distance(instance.localPosition, Mirror(Vector(part.Position))) > 0.0001f ||
                        Vector3.Distance(instance.localScale, Vector(part.Scale)) > 0.00001f)
                    {
                        throw new InvalidDataException("Saved transform mismatch: " + part.Name);
                    }
                    Quaternion actualRotation = instance.localRotation;
                    float sign = Quaternion.Dot(actualRotation, expectedRotation) >= 0f ? 1f : -1f;
                    if (Vector4.Distance(new Vector4(actualRotation.x, actualRotation.y,
                            actualRotation.z, actualRotation.w), sign * new Vector4(expectedRotation.x,
                            expectedRotation.y, expectedRotation.z, expectedRotation.w)) > 0.00001f)
                    {
                        throw new InvalidDataException("Saved rotation mismatch: " + part.Name);
                    }
                }
                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
                if (filters.Any(filter => filter.sharedMesh == null ||
                    filter.sharedMesh.vertexCount == 0 || !AssetDatabase.Contains(filter.sharedMesh)))
                {
                    throw new InvalidDataException("Missing or unsaved scene mesh.");
                }
                string report = $"Map={source.Map}\nVerifiedPlacements={placed.Count}\n" +
                    $"UnresolvedPlacements={source.Parts.Length - placed.Count}\nMeshInstances={filters.Length}\n" +
                    "TransformsAndSavedMeshReferences=PASS\nGameplayAndSourceCollision=NOT_VERIFIED\n";
                File.WriteAllText(s_Report.Replace("scene.txt", "layout-verification.txt"), report);
                Debug.Log("ORIGINAL_STORMVEIL_LAYOUT_VERIFIED " + placed.Count);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Captures the imported geometry directly, without relying on background Game View repaint.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Capture Original Stormveil Scene")]
        public static void Capture()
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene loaded = SceneManager.GetSceneAt(index);
                if (loaded.isDirty || string.IsNullOrEmpty(loaded.path))
                {
                    throw new InvalidOperationException("Save open scenes before capturing an isolated map preview.");
                }
            }
            try
            {
                CaptureScene();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void CaptureScene()
        {
            if (Application.isPlaying || SceneManager.GetSceneByPath(s_Scene).isLoaded)
            {
                throw new InvalidOperationException("Stop Play Mode and close the recovered scene before capture.");
            }
            // A preview must not render or be occluded by geometry in the user's other loaded scenes.
            Scene scene = EditorSceneManager.OpenScene(s_Scene, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            RenderSettings.sun = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<Light>()).First(light => light != null);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            var cameraObject = new GameObject("Recovery Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.19f, 0.22f, 0.25f);
            camera.farClipPlane = 4000f;
            Renderer[] renderers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Renderer>()).ToArray();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            float distance = Mathf.Max(bounds.size.x, bounds.size.z);
            camera.farClipPlane = Mathf.Max(4000f, distance * 4f);
            camera.transform.position = bounds.center + new Vector3(distance * 0.55f, distance * 0.45f, distance);
            camera.transform.LookAt(bounds.center);
            var target = new RenderTexture(1600, 900, 24);
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(s_CapturePath, image.EncodeToPNG());
                if (s_ProbeSurfaces)
                {
                    ProbeSurfaces(camera, renderers);
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.ChangeExtension(s_CapturePath, ".detail.png"), image.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void ProbeSurfaces(Camera camera, Renderer[] renderers)
        {
            var report = new PreviewSurfaceReport();
            Vector3 focus = Vector3.zero;
            Vector3 focusDirection = Vector3.forward;
            foreach (Vector2 pixel in new[] { new Vector2(1065, 270), new Vector2(855, 325),
                new Vector2(920, 322), new Vector2(994, 320), new Vector2(815, 500) })
            {
                Ray ray = camera.ScreenPointToRay(new Vector3(pixel.x, 900 - pixel.y, 0));
                var hits = new List<PreviewSurfaceHit>();
                foreach (Renderer renderer in renderers)
                {
                    if (!renderer.enabled || !renderer.bounds.IntersectRay(ray))
                    {
                        continue;
                    }
                    Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null)
                    {
                        continue;
                    }
                    Matrix4x4 inverse = renderer.transform.worldToLocalMatrix;
                    Vector3 origin = inverse.MultiplyPoint3x4(ray.origin);
                    Vector3 direction = inverse.MultiplyVector(ray.direction);
                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;
                    float closest = float.PositiveInfinity;
                    float facing = 0;
                    for (int index = 0; index < triangles.Length; index += 3)
                    {
                        Vector3 a = vertices[triangles[index]];
                        Vector3 ab = vertices[triangles[index + 1]] - a;
                        Vector3 ac = vertices[triangles[index + 2]] - a;
                        Vector3 cross = Vector3.Cross(direction, ac);
                        float determinant = Vector3.Dot(ab, cross);
                        if (Mathf.Abs(determinant) < 0.0000001f)
                        {
                            continue;
                        }
                        Vector3 offset = origin - a;
                        float u = Vector3.Dot(offset, cross) / determinant;
                        Vector3 q = Vector3.Cross(offset, ab);
                        float v = Vector3.Dot(direction, q) / determinant;
                        float distance = Vector3.Dot(ac, q) / determinant;
                        if (u >= 0 && v >= 0 && u + v <= 1 && distance > 0 && distance < closest)
                        {
                            closest = distance;
                            facing = determinant;
                        }
                    }
                    if (float.IsPositiveInfinity(closest))
                    {
                        continue;
                    }
                    Material material = renderer.sharedMaterial;
                    hits.Add(new PreviewSurfaceHit { Object = renderer.transform.parent.name + "/" + renderer.name,
                        Mesh = AssetDatabase.GetAssetPath(mesh), Material = material?.name,
                        SourceShader = material?.GetTag("EldenRingSourceShader", false, ""),
                        Texture = material?.mainTexture?.name, Distance = closest, Facing = facing });
                }
                report.Pixels.Add(new PreviewSurfacePixel { X = pixel.x, Y = pixel.y,
                    Hits = hits.OrderBy(hit => hit.Distance).Take(6).ToArray() });
                if (report.Pixels.Count == 1 && hits.Count > 0)
                {
                    focus = ray.GetPoint(hits.Min(hit => hit.Distance));
                    focusDirection = ray.direction;
                }
            }
            File.WriteAllText(Path.ChangeExtension(s_CapturePath, ".surfaces.json"), JsonUtility.ToJson(report, true));
            camera.transform.position = focus - focusDirection * 30f;
            camera.transform.LookAt(focus);
        }

        [Serializable] private sealed class PreviewSurfaceReport
        {
            public List<PreviewSurfacePixel> Pixels = new List<PreviewSurfacePixel>();
        }
        [Serializable] private sealed class PreviewSurfacePixel
        {
            public float X;
            public float Y;
            public PreviewSurfaceHit[] Hits;
        }
        [Serializable] private sealed class PreviewSurfaceHit
        {
            public string Object;
            public string Mesh;
            public string Material;
            public string SourceShader;
            public string Texture;
            public float Distance;
            public float Facing;
        }

        private static GameObject BuildModel(string name, Model source)
        {
            if (s_Models != null)
            {
                foreach (string output in new[] { s_Output,
                    "Assets/_Game/Art/Environment/EldenRing/StormveilWorld" })
                {
                    var cached = AssetDatabase.LoadAssetAtPath<GameObject>(
                        output + "/Models/" + name + "/" + name + ".prefab");
                    if (cached != null)
                    {
                        VerifyModel(cached, source);
                        return cached;
                    }
                }
            }
            if (new DriveInfo(Application.dataPath.Substring(0, 3)).AvailableFreeSpace < 12L * 1024 * 1024 * 1024)
            {
                throw new IOException("Less than 12 GiB free on Unity drive; model import stopped at checkpoint.");
            }
            string folder = s_Output + "/Models/" + name;
            Directory.CreateDirectory(folder);
            var root = new GameObject(name);
            try
            {
                for (int index = 0; index < source.Meshes.Length; index++)
                {
                    MeshRecord record = source.Meshes[index];
                    if (record.Positions.Length == 0 || record.Indices.Length == 0)
                    {
                        continue;
                    }
                    string path = folder + "/Mesh_" + index + ".asset";
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                    {
                        mesh = new Mesh();
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    mesh.Clear();
                    mesh.indexFormat = IndexFormat.UInt32;
                    mesh.vertices = Enumerable.Range(0, record.Positions.Length / 3)
                        .Select(vertex => Mirror(Vector(record.Positions, vertex))).ToArray();
                    mesh.normals = Enumerable.Range(0, record.Normals.Length / 3)
                        .Select(vertex => Mirror(Vector(record.Normals, vertex))).ToArray();
                    mesh.uv = Enumerable.Range(0, record.UV.Length / 2)
                        .Select(vertex => new Vector2(record.UV[vertex * 2], record.UV[vertex * 2 + 1])).ToArray();
                    // FLVER face normals oppose its geometric cross product; mirroring X gives Unity front faces.
                    mesh.triangles = record.Indices;
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    EditorUtility.SetDirty(mesh);
                    var child = new GameObject("Mesh " + index);
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    child.AddComponent<MeshRenderer>().sharedMaterial = BuildMaterial(source.Materials[record.Material]);
                }
                string prefabPath = folder + "/" + name + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                VerifyModel(prefab, source);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void VerifyModel(GameObject prefab, Model source)
        {
            int expectedMeshes = source.Meshes.Count(mesh => mesh.Positions.Length > 0 && mesh.Indices.Length > 0);
            if (prefab.GetComponentsInChildren<MeshFilter>(true).Length != expectedMeshes)
            {
                throw new InvalidDataException("Source mesh count differs: " + prefab.name);
            }
            for (int index = 0; index < source.Meshes.Length; index++)
            {
                MeshRecord record = source.Meshes[index];
                if (record.Positions.Length == 0 || record.Indices.Length == 0)
                {
                    continue;
                }
                Transform child = prefab.transform.Find("Mesh " + index);
                Mesh mesh = child == null ? null : child.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || mesh.vertexCount != record.Positions.Length / 3 ||
                    !mesh.triangles.SequenceEqual(record.Indices))
                {
                    throw new InvalidDataException("Source topology differs: " + prefab.name + "/" + index);
                }
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uv = mesh.uv;
                Material material = child.GetComponent<MeshRenderer>()?.sharedMaterial;
                TextureRecord albedo = source.Materials[record.Material].Textures.FirstOrDefault(texture =>
                    texture.Type.Contains("AlbedoMap") && !string.IsNullOrEmpty(texture.Name));
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader" ||
                    (albedo != null && albedo.Name != "SYSTEX_Dummy" &&
                        (material.GetTexture("_BaseMap") == null || material.GetTexture("_BaseMap").name != albedo.Name)))
                {
                    throw new InvalidDataException("Missing or mismatched source material: " + prefab.name);
                }
                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    if (Vector3.Distance(vertices[vertex], Mirror(Vector(record.Positions, vertex))) > 0.00001f ||
                        normals.Length != vertices.Length || uv.Length != vertices.Length ||
                        Vector3.Distance(normals[vertex], Mirror(Vector(record.Normals, vertex))) > 0.00001f ||
                        Vector2.Distance(uv[vertex], new Vector2(record.UV[vertex * 2], record.UV[vertex * 2 + 1])) >
                            0.00001f)
                    {
                        throw new InvalidDataException("Source vertex data differs: " + prefab.name + "/" + index);
                    }
                }
            }
        }

        private static Material BuildMaterial(MaterialRecord record)
        {
            TextureRecord albedo = record.Textures.FirstOrDefault(texture =>
                texture.Type.Contains("AlbedoMap") && !string.IsNullOrEmpty(texture.Name));
            string name = albedo?.Name ?? "UntexturedSource";
            string path = s_Output + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.enableInstancing = true;
                Texture2D texture = albedo == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(
                    s_Output + "/Textures/" + name + ".png");
                if (albedo != null && texture == null)
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Assets/_Game/Art/Environment/EldenRing/StormveilWorld/Textures/" + name + ".png");
                }
                if (albedo != null && texture == null && name != "SYSTEX_Dummy")
                {
                    throw new InvalidDataException("Missing source albedo: " + name);
                }
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_Smoothness", 0.15f);
                // ponytail: albedo-only recovery; source shader layering remains a separately tracked requirement.
                AssetDatabase.CreateAsset(material, path);
            }
            if (name == "SYSTEX_Dummy")
            {
                // Retain original helper geometry for inspection, without opaque blockers in the preview.
                material.SetColor("_BaseColor", Color.clear);
                material.SetFloat("_AlphaClip", 1f);
                material.EnableKeyword("_ALPHATEST_ON");
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Vector3 Vector(float[] values, int index = 0) =>
            new(values[index * 3], values[index * 3 + 1], values[index * 3 + 2]);

        private static Vector3 Mirror(Vector3 value) => new(-value.x, value.y, value.z);

        [Serializable] private sealed class World { public string Map; public Binding[] Bindings; public Part[] Parts; }
        [Serializable] private sealed class Binding
        {
            public string Name;
            public string Model;
            public string Status;
            public string GeometrySHA256;
        }
        [Serializable] private sealed class Part
        {
            public string Name;
            public string ModelName;
            public float[] Position;
            public float[] Rotation;
            public float[] Scale;
        }
        [Serializable] private sealed class Document { public Model[] Models; }
        [Serializable] private sealed class Model { public MeshRecord[] Meshes; public MaterialRecord[] Materials; }
        [Serializable] private sealed class MaterialRecord { public TextureRecord[] Textures; }
        [Serializable] private sealed class TextureRecord { public string Type; public string Name; }
        [Serializable] private sealed class MeshRecord
        {
            public int Material;
            public float[] Positions;
            public float[] Normals;
            public float[] UV;
            public int[] Indices;
        }
    }
}
