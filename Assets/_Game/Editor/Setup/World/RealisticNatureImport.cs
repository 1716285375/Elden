using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Imports verified Poly Haven nature models with URP materials and distance LODs.</summary>
    public static class RealisticNatureImport
    {
        public const string Root = "Assets/_Game/Art/Environment/PolyHaven";

        /// <summary>Builds reusable nature prefabs from the locally verified source conversion.</summary>
        [MenuItem("Tools/ZZ/Environment/Build Realistic Nature Prefabs")]
        public static void Build()
        {
            if (Application.isPlaying || !File.Exists("Docs/ArtRefresh/polyhaven-conversion.json"))
            {
                throw new InvalidOperationException("Stop Play Mode and finish the verified FBX conversion first.");
            }
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Textures/Packed");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(Root + "/Textures", "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta") && !path.Contains("/Packed/")))
            {
                ConfigureTexture(path.Replace('\\', '/'));
            }
            var materials = new Dictionary<string, Material>();
            var report = new List<string>();
            foreach (string path in Directory.GetFiles(Root + "/Models", "*.fbx"))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
                importer.importAnimation = false;
                importer.isReadable = false;
                importer.SaveAndReimport();
                GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
                instance.name = Path.GetFileNameWithoutExtension(path);
                try
                {
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in renderers)
                    {
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                            ResolveMaterial(source.name, materials)).ToArray();
                    }
                    LODGroup group = instance.GetComponent<LODGroup>();
                    if (group == null)
                    {
                        group = instance.AddComponent<LODGroup>();
                    }
                    var lods = new List<LOD>();
                    float[] thresholds = { 0.2f, 0.06f, 0.008f };
                    for (int index = 0; index < thresholds.Length; index++)
                    {
                        Renderer[] lodRenderers = renderers.Where(renderer => renderer.name == "LOD" + index).ToArray();
                        if (lodRenderers.Length == 0)
                        {
                            throw new InvalidDataException("Missing LOD" + index + " in " + path);
                        }
                        lods.Add(new LOD(thresholds[index], lodRenderers));
                    }
                    group.SetLODs(lods.ToArray());
                    group.RecalculateBounds();
                    if (renderers.Any(renderer => renderer.sharedMaterials.Any(material => material == null)))
                    {
                        throw new InvalidDataException("Unassigned nature material: " + path);
                    }
                    string prefab = Root + "/Prefabs/" + instance.name + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(instance, prefab);
                    report.Add(prefab);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            if (report.Count < 15)
            {
                throw new InvalidDataException("The expected nature variant library is incomplete.");
            }
            AssetDatabase.SaveAssets();
            File.WriteAllLines("Docs/ArtRefresh/realistic-prefabs.txt", report);
            Debug.Log("REALISTIC_NATURE_READY: " + report.Count + " prefabs, " + materials.Count + " URP materials.");
        }

        private static void ConfigureTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            bool normal = path.Contains("_nor_gl_");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = path.Contains("_diff_");
            importer.isReadable = !normal;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static Material ResolveMaterial(string source, Dictionary<string, Material> materials)
        {
            string prefix = source.Replace(" (Instance)", "");
            prefix = prefix.Replace("tree_small_02_branches", "tree_small_02_branch")
                .Replace("tree_small_02_trunk", "tree_small_02")
                .Replace("fir_tree_01_dead_branches", "fir_tree_01_bark");
            if (materials.TryGetValue(prefix, out Material existing))
            {
                return existing;
            }
            string textureFolder = Directory.GetDirectories(Root + "/Textures")
                .Where(folder => !folder.EndsWith("Packed"))
                .Single(folder => prefix.StartsWith(Path.GetFileName(folder)));
            Texture2D diffuse = FindTexture(textureFolder, prefix, "diff", true);
            Texture2D alpha = FindTexture(textureFolder, prefix, "alpha", false);
            Texture2D roughness = FindTexture(textureFolder, prefix, "rough", false);
            Texture2D normal = FindTexture(textureFolder, prefix, "nor_gl", false);
            string materialPath = Root + "/Materials/" + prefix + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", PackTexture(prefix + "_Base", diffuse, alpha, false));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 1f);
            if (roughness != null)
            {
                material.SetTexture("_MetallicGlossMap", PackTexture(prefix + "_Surface", roughness, null, true));
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_AlphaClip", alpha != null ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.42f);
            material.SetFloat("_Cull", alpha != null ? (float)CullMode.Off : (float)CullMode.Back);
            material.SetOverrideTag("RenderType", alpha != null ? "TransparentCutout" : "Opaque");
            material.renderQueue = alpha != null ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            if (alpha != null)
            {
                material.EnableKeyword("_ALPHATEST_ON");
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            materials.Add(prefix, material);
            return material;
        }

        private static Texture2D FindTexture(string folder, string prefix, string channel, bool required)
        {
            string path = Directory.GetFiles(folder, prefix + "_" + channel + "_2k.*")
                .FirstOrDefault(candidate => !candidate.EndsWith(".meta"));
            if (path == null && required)
            {
                throw new FileNotFoundException("Missing verified texture: " + prefix + " " + channel);
            }
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
        }

        private static Texture2D PackTexture(string name, Texture2D source, Texture2D alpha, bool surface)
        {
            Color32[] pixels = source.GetPixels32();
            Color32[] alphaPixels = alpha != null ? alpha.GetPixels32() : null;
            if (alpha != null && (alpha.width != source.width || alpha.height != source.height))
            {
                throw new InvalidDataException("Texture channel sizes differ: " + name);
            }
            for (int index = 0; index < pixels.Length; index++)
            {
                if (surface)
                {
                    pixels[index] = new Color32(0, 255, 0, (byte)(255 - pixels[index].r));
                }
                else
                {
                    pixels[index].a = alphaPixels != null ? alphaPixels[index].r : (byte)255;
                }
            }
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            string path = Root + "/Textures/Packed/" + name + ".png";
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = !surface;
            importer.alphaIsTransparency = alpha != null;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
