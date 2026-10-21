using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Restores source material identities and ER normal maps on recovered prefabs.</summary>
    [InitializeOnLoad]
    public static class EldenRingMapMaterialImport
    {
        private const string k_Root = "F:/MyProject/RE/EldenRing/Converted/MapWorld/Maps";
        private const string k_Output = "Assets/_Game/Art/Environment/EldenRing/World";
        private const string k_Legacy = "Assets/_Game/Art/Environment/EldenRing/StormveilWorld";
        private const string k_Enabled = "ZZ.EldenRing.MapMaterials";
        private const int k_Version = 4;
        private static double s_NextCheck;

        static EldenRingMapMaterialImport()
        {
            EditorApplication.update += ImportNext;
            s_NextCheck = EditorApplication.timeSinceStartup + 60;
        }

        /// <summary>Starts or resumes applying prepared material recipes.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Restore Prepared Map Materials")]
        public static void Start()
        {
            SessionState.SetBool(k_Enabled, true);
            s_NextCheck = 0;
        }

        /// <summary>Stops before the next map without discarding saved materials.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Stop Map Material Restore")]
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
            foreach (string path in Directory.GetFiles(k_Root, "materials.json", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                string folder = Path.GetDirectoryName(path);
                string resultPath = folder + "/unity-material-result.json";
                if ((File.Exists(resultPath) && JsonUtility.FromJson<Result>(File.ReadAllText(resultPath)).Version == k_Version) ||
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
                    File.WriteAllText(folder + "/unity-material-error.txt", error.ToString());
                    SessionState.SetBool(k_Enabled, false);
                    Debug.LogException(error);
                }
                GC.Collect();
                EditorUtility.UnloadUnusedAssetsImmediate();
                s_NextCheck = EditorApplication.timeSinceStartup + 15;
                return;
            }
        }

        /// <summary>Restores the source material recipes for one selected map.</summary>
        public static void ImportPreparedMap(string mapId)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(mapId, @"^m\d{2}_\d{2}_\d{2}_\d{2}$"))
            {
                throw new ArgumentException("Invalid original map ID.", nameof(mapId));
            }
            Import(k_Root + "/" + mapId + "/materials.json");
        }

        private static void Import(string path)
        {
            if (new DriveInfo(Application.dataPath.Substring(0, 3)).AvailableFreeSpace < 12L * 1024 * 1024 * 1024)
            {
                throw new IOException("Less than 12 GiB free; material import stopped.");
            }
            Document data = JsonUtility.FromJson<Document>(File.ReadAllText(path));
            if (data.Map != new DirectoryInfo(Path.GetDirectoryName(path)).Name)
            {
                throw new InvalidDataException("Material recipe map ID differs from its directory.");
            }
            Directory.CreateDirectory(k_Output + "/Materials/Source");
            int slots = 0;
            int normals = 0;
            int layered = 0;
            var materialCache = new Dictionary<string, Material>();
            foreach (ModelRecord model in data.Models)
            {
                string prefabPath = new[] { k_Output, k_Legacy }.Select(folder =>
                    folder + "/Models/" + model.Model + "/" + model.Model + ".prefab")
                    .FirstOrDefault(candidate => AssetDatabase.LoadAssetAtPath<GameObject>(candidate) != null);
                if (prefabPath == null)
                {
                    throw new FileNotFoundException("Missing imported prefab: " + model.Model);
                }
                Material[] materials = model.Materials.Select(source =>
                {
                    if (!materialCache.TryGetValue(source.Key, out Material material))
                    {
                        material = BuildMaterial(source);
                        materialCache.Add(source.Key, material);
                    }
                    return material;
                }).ToArray();
                slots += model.Slots.Count(slot => slot >= 0);
                normals += model.Slots.Count(slot => slot >= 0 && !string.IsNullOrEmpty(model.Materials[slot].Normal));
                layered += model.Slots.Count(slot => slot >= 0 && model.Materials[slot].Layered);
                if (Matches(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), model, materials))
                {
                    continue;
                }
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    for (int index = 0; index < model.Slots.Length; index++)
                    {
                        int slot = model.Slots[index];
                        if (slot < 0)
                        {
                            continue;
                        }
                        MeshRenderer renderer = root.transform.Find("Mesh " + index)?.GetComponent<MeshRenderer>();
                        if (renderer == null || slot >= materials.Length)
                        {
                            throw new InvalidDataException("Source material slot differs: " + model.Model + "/" + index);
                        }
                        renderer.sharedMaterial = materials[slot];
                        if (!string.IsNullOrEmpty(model.Materials[slot].Normal))
                        {
                            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                            if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent))
                            {
                                Vector3[] vertices = mesh.vertices;
                                int[] triangles = mesh.triangles;
                                mesh.RecalculateTangents();
                                if (!mesh.vertices.SequenceEqual(vertices) || !mesh.triangles.SequenceEqual(triangles) ||
                                    !mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent))
                                {
                                    throw new InvalidDataException("Could not restore normal-map tangents: " + model.Model);
                                }
                                EditorUtility.SetDirty(mesh);
                            }
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                if (!Matches(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), model, materials))
                {
                    throw new InvalidDataException("Saved source material differs: " + model.Model);
                }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetDirectoryName(path) + "/unity-material-result.json", JsonUtility.ToJson(
                new Result { Version = k_Version, Map = data.Map, Models = data.Models.Length, Slots = slots, NormalSlots = normals,
                    LayeredSlots = layered, MissingTextures = data.Errors.Length,
                    Status = data.Errors.Length == 0 ? "verified_material_identity_and_normal_references" : "partial",
                    Scope = data.Scope }, true));
        }

        private static bool Matches(GameObject prefab, ModelRecord model, Material[] materials)
        {
            for (int index = 0; index < model.Slots.Length; index++)
            {
                int slot = model.Slots[index];
                if (slot < 0)
                {
                    continue;
                }
                Transform child = prefab.transform.Find("Mesh " + index);
                Material material = child == null ? null : child.GetComponent<MeshRenderer>()?.sharedMaterial;
                Recipe source = model.Materials[slot];
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader" ||
                    material != materials[slot] || material.name != source.Name ||
                    Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(material)) != source.Name ||
                    material.GetTag("EldenRingSourceIdentity", false) != source.Key ||
                    material.GetTag("EldenRingSourceShader", false) != source.Shader)
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(source.Albedo) && source.Albedo != "SYSTEX_Dummy" &&
                    (material.GetTexture("_BaseMap") == null || material.GetTexture("_BaseMap").name != source.Albedo))
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(source.Normal) &&
                    (AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")) != source.Normal ||
                        !material.IsKeywordEnabled("_NORMALMAP") ||
                        !child.GetComponent<MeshFilter>().sharedMesh.HasVertexAttribute(
                            UnityEngine.Rendering.VertexAttribute.Tangent)))
                {
                    return false;
                }
            }
            return true;
        }

        private static Material BuildMaterial(Recipe source)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(source.Key, "^[0-9a-f]{64}$"))
            {
                throw new InvalidDataException("Invalid source material identity.");
            }
            Texture2D albedo = null;
            if (!string.IsNullOrEmpty(source.Albedo) && source.Albedo != "SYSTEX_Dummy")
            {
                albedo = new[] { k_Output, k_Legacy }.Select(folder => AssetDatabase.LoadAssetAtPath<Texture2D>(
                    folder + "/Textures/" + source.Albedo + ".png")).FirstOrDefault(texture => texture != null);
                if (albedo == null)
                {
                    throw new InvalidDataException("Missing source albedo: " + source.Albedo);
                }
            }
            Texture2D normal = null;
            if (!string.IsNullOrEmpty(source.Normal))
            {
                var importer = AssetImporter.GetAtPath(source.Normal) as TextureImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(source.Normal, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(source.Normal) as TextureImporter;
                }
                if (importer == null)
                {
                    throw new InvalidDataException("Missing converted source normal: " + source.Normal);
                }
                if (importer.textureType != TextureImporterType.NormalMap || importer.sRGBTexture ||
                    importer.flipGreenChannel || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    importer.maxTextureSize != 16384)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                    importer.flipGreenChannel = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 16384;
                    importer.SaveAndReimport();
                }
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(source.Normal);
            }
            if (string.IsNullOrWhiteSpace(source.Name) || source.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                source.Name.EndsWith(".", StringComparison.Ordinal) || source.Name.EndsWith(" ", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Source material name cannot be preserved as a Windows filename: " + source.Name);
            }
            string folder = k_Output + "/Materials/Source/" + source.Key;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(k_Output + "/Materials/Source", source.Key);
            }
            string path = folder + "/" + source.Name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                string previousPath = k_Output + "/Materials/Source/" + source.Key + ".mat";
                material = AssetDatabase.LoadAssetAtPath<Material>(previousPath);
                if (material != null)
                {
                    string error = AssetDatabase.MoveAsset(previousPath, path);
                    if (!string.IsNullOrEmpty(error))
                    {
                        throw new IOException(error);
                    }
                }
                else
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
            }
            material.name = source.Name;
            material.enableInstancing = true;
            material.SetOverrideTag("EldenRingSourceIdentity", source.Key);
            material.SetOverrideTag("EldenRingSourceShader", source.Shader);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", 0.15f);
            // shortcut: first-layer URP preview; original blending and shininess need source shader translation.
            if (source.Albedo == "SYSTEX_Dummy")
            {
                material.SetColor("_BaseColor", Color.clear);
                material.SetFloat("_AlphaClip", 1f);
                material.EnableKeyword("_ALPHATEST_ON");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        [Serializable] private sealed class Document
        {
            public string Map;
            public ModelRecord[] Models;
            public string Scope;
            public ErrorRecord[] Errors;
        }
        [Serializable] private sealed class ErrorRecord { public string Error; }
        [Serializable] private sealed class ModelRecord
        {
            public string Model;
            public Recipe[] Materials;
            public int[] Slots;
        }
        [Serializable] private sealed class Recipe
        {
            public string Key;
            public string Name;
            public string Shader;
            public string Albedo;
            public string Normal;
            public bool Layered;
        }
        [Serializable] private sealed class GeometryResult { public int Version; public string Status; }
        [Serializable] private sealed class Result
        {
            public int Version;
            public string Map;
            public string Status;
            public string Scope;
            public int Models;
            public int Slots;
            public int NormalSlots;
            public int LayeredSlots;
            public int MissingTextures;
        }
    }
}
