using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Restores supported source Mb2 layers after verifying the original vertex UV channels.</summary>
    public static class EldenRingMapLayerImport
    {
        private const string k_Map = "F:/MyProject/RE/EldenRing/Converted/MapWorld/Maps/m10_00_00_00";
        private const string k_Art = "Assets/_Game/Art/Environment/EldenRing/World";
        private const string k_Legacy = "Assets/_Game/Art/Environment/EldenRing/StormveilWorld";

        /// <summary>Applies source Mb2 color/normal masks without changing names, GUIDs, positions or topology.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Restore Stormveil Mb2 Layers")]
        public static void Restore()
        {
            Document source = JsonUtility.FromJson<Document>(File.ReadAllText(k_Map + "/mb2.json"));
            if (source.Map != "m10_00_00_00" || source.Errors.Length != 0)
            {
                throw new InvalidDataException("Mb2 preparation is incomplete.");
            }
            Shader shader = Shader.Find("ZZ/EldenRing/MapMb2");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                throw new InvalidDataException("Source Mb2 shader did not compile.");
            }
            int meshes = 0;
            var materials = source.Materials.ToDictionary(item => item.Key);
            foreach (ModelRecord model in source.Models)
            {
                using (SHA256 hash = SHA256.Create())
                {
                    string actual = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(model.Channels))).Replace("-", "").ToLowerInvariant();
                    if (actual != model.SHA256)
                    {
                        throw new InvalidDataException("Source vertex channel hash differs: " + model.Model);
                    }
                }
                using var stream = File.OpenRead(model.Channels);
                using var compressed = new GZipStream(stream, CompressionMode.Decompress);
                using var reader = new StreamReader(compressed);
                ChannelDocument channels = JsonUtility.FromJson<ChannelDocument>(reader.ReadToEnd());
                if (channels.Model != model.Model)
                {
                    throw new InvalidDataException("Source channel model ID differs.");
                }
                foreach (Slot slot in model.Slots)
                {
                    Layer material = materials[slot.Key];
                    ChannelMesh original = channels.Meshes[slot.Index];
                    string meshPath = new[] { k_Art, k_Legacy }.Select(folder => folder + "/Models/" +
                        model.Model + "/Mesh_" + slot.Index + ".asset")
                        .FirstOrDefault(path => AssetDatabase.LoadAssetAtPath<Mesh>(path) != null);
                    Mesh mesh = meshPath == null ? null : AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null || mesh.vertexCount != original.VertexCount || material.BlendUVChannel != 1 ||
                        original.UVs.Length < 2 || original.UVs.Length > 7)
                    {
                        throw new InvalidDataException("Source Mb2 vertex layout differs: " + model.Model);
                    }
                    Vector3[] vertices = mesh.vertices;
                    Vector3[] normals = mesh.normals;
                    Vector2[] uv0 = mesh.uv;
                    Vector4[] tangents = mesh.tangents;
                    int[] indices = mesh.triangles;
                    float[] firstUV = original.UVs[0].Values;
                    for (int index = 0; index < mesh.vertexCount; index++)
                    {
                        if (Mathf.Abs(uv0[index].x - firstUV[index * 3]) > 0.00001f ||
                            Mathf.Abs(uv0[index].y - (1 - firstUV[index * 3 + 1])) > 0.00001f)
                        {
                            throw new InvalidDataException("Original UV0 differs: " + model.Model);
                        }
                    }
                    for (int channel = 1; channel < original.UVs.Length; channel++)
                    {
                        float[] values = original.UVs[channel].Values;
                        if (values.Length != mesh.vertexCount * 3)
                        {
                            throw new InvalidDataException("Source UV channel length differs.");
                        }
                        mesh.SetUVs(channel, Enumerable.Range(0, mesh.vertexCount).Select(index =>
                            new Vector3(values[index * 3], values[index * 3 + 1], values[index * 3 + 2])).ToList());
                    }
                    mesh.SetUVs(7, Enumerable.Repeat(Vector2.one, mesh.vertexCount).ToList());
                    if (!mesh.vertices.SequenceEqual(vertices) || !mesh.normals.SequenceEqual(normals) ||
                        !mesh.uv.SequenceEqual(uv0) || !mesh.tangents.SequenceEqual(tangents) || !mesh.triangles.SequenceEqual(indices))
                    {
                        throw new InvalidDataException("Geometry changed while adding source UVBlend.");
                    }
                    EditorUtility.SetDirty(mesh);
                    meshes++;
                }
            }
            foreach (Layer layer in source.Materials)
            {
                string path = k_Art + "/Materials/Source/" + layer.Key + "/" + layer.Name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.name != layer.Name)
                {
                    throw new InvalidDataException("Source material identity missing: " + layer.Name);
                }
                Texture2D primary = Texture(layer.Primary, false);
                Texture2D secondary = Texture(layer.Secondary, false);
                Texture2D mask = Texture(layer.Mask, true);
                Texture2D normal = Normal(layer.PrimaryNormal);
                Texture2D secondaryNormal = Normal(layer.SecondaryNormal);
                material.shader = shader;
                material.SetTexture("_BaseMap", primary);
                material.SetTexture("_BumpMap", normal);
                material.SetTexture("_SecondaryMap", secondary);
                material.SetTexture("_SecondaryBumpMap", secondaryNormal);
                material.SetTexture("_BlendMask", mask);
                material.SetVector("_PrimaryScale", new Vector4(layer.PrimaryScale[0], layer.PrimaryScale[1], 0, 0));
                material.SetVector("_SecondaryScale", new Vector4(layer.SecondaryScale[0], layer.SecondaryScale[1], 0, 0));
                material.SetVector("_MaskScale", new Vector4(layer.MaskScale[0], layer.MaskScale[1], 0, 0));
                material.SetOverrideTag("EldenRingLayerScope", "Source Mb2 mask and UVBlend; overlay/shininess incomplete");
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            foreach (Layer layer in source.Materials)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(k_Art + "/Materials/Source/" + layer.Key + "/" + layer.Name + ".mat");
                if (material.shader != shader || material.GetTexture("_SecondaryMap")?.name != layer.Secondary ||
                    material.GetTexture("_BlendMask")?.name != layer.Mask ||
                    AssetDatabase.GetAssetPath(material.GetTexture("_SecondaryBumpMap")) != layer.SecondaryNormal)
                {
                    throw new InvalidDataException("Saved layer references differ: " + layer.Name);
                }
            }
            File.WriteAllText(k_Map + "/unity-mb2-result.json", JsonUtility.ToJson(new Result {
                Materials = source.Materials.Length, Models = source.Models.Length, Meshes = meshes,
                Status = "verified_source_mb2_uv_and_texture_references",
                Scope = "Source Mb2 albedo/normal mask blending; no claim of original overlay, shininess, lighting or full shader parity." }, true));
        }

        private static Texture2D Texture(string name, bool linear)
        {
            string path = new[] { k_Art, k_Legacy }.Select(folder => folder + "/Textures/" + name + ".png")
                .FirstOrDefault(candidate => File.Exists(candidate));
            if (path == null)
            {
                throw new FileNotFoundException("Missing original layer texture: " + name);
            }
            if (AssetImporter.GetAtPath(path) == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (linear && importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D Normal(string path)
        {
            if (AssetImporter.GetAtPath(path) == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("Missing layer normal: " + path);
            }
            if (importer.textureType != TextureImporterType.NormalMap || importer.sRGBTexture || importer.flipGreenChannel ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize != 16384)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.flipGreenChannel = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 16384;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        [Serializable] private sealed class Document { public string Map; public Layer[] Materials; public ModelRecord[] Models; public Error[] Errors; }
        [Serializable] private sealed class Error { public string Model; public string ErrorText; }
        [Serializable] private sealed class ModelRecord { public string Model; public string Channels; public string SHA256; public Slot[] Slots; }
        [Serializable] private sealed class Slot { public int Index; public string Key; }
        [Serializable] private sealed class ChannelDocument { public string Model; public ChannelMesh[] Meshes; }
        [Serializable] private sealed class ChannelMesh { public int VertexCount; public Channel[] UVs; }
        [Serializable] private sealed class Channel { public float[] Values; }
        [Serializable] private sealed class Layer
        {
            public string Key;
            public string Name;
            public string Primary;
            public string Secondary;
            public string PrimaryNormal;
            public string SecondaryNormal;
            public string Mask;
            public int BlendUVChannel;
            public float[] PrimaryScale;
            public float[] SecondaryScale;
            public float[] MaskScale;
        }
        [Serializable] private sealed class Result { public string Status; public string Scope; public int Materials; public int Models; public int Meshes; }
    }
}
