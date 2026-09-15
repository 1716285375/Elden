using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Exchanges subdivided player meshes with Blender while retaining the authored Unity rig.</summary>
    public static class PlayerModelOptimization
    {
        private const string k_Player = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_Output = "Assets/_Game/Art/Characters/Player/Optimized";

        [MenuItem("Tools/ZZ/Restore Original Player Model")]
        public static void RestoreOriginal()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before restoring the player model.");
            }
            var source = JsonUtility.FromJson<ExportDocument>(
                File.ReadAllText("SourceArt/Player/player-blender-source.json"));
            var originals = source.Meshes.GroupBy(record => Path.GetFileName(record.Path))
                .ToDictionary(group => group.Key, group => AssetDatabase.LoadAssetAtPath<Mesh>(group.First().Path));
            Material originalMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Art/Shared/Materials/General/Player_Material_01.mat");
            if (originalMaterial == null || originals.Values.Any(mesh => mesh == null))
            {
                throw new InvalidOperationException("An original player asset is missing; restoration was not applied.");
            }
            GameObject root = PrefabUtility.LoadPrefabContents(k_Player);
            try
            {
                string rigBefore = RigFingerprint(root);
                int meshCount = 0;
                int materialCount = 0;
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    string path = AssetDatabase.GetAssetPath(renderer.sharedMesh);
                    if (path.StartsWith(k_Output + "/", StringComparison.Ordinal))
                    {
                        renderer.sharedMesh = originals[Path.GetFileName(path)];
                        meshCount++;
                    }
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++)
                    {
                        if (AssetDatabase.GetAssetPath(materials[index]) == k_Output + "/Player Stylized.mat")
                        {
                            materials[index] = originalMaterial;
                            materialCount++;
                        }
                    }
                    renderer.sharedMaterials = materials;
                }
                if (RigFingerprint(root) != rigBefore)
                {
                    throw new InvalidOperationException("Restoration changed rig references; prefab was not saved.");
                }
                PrefabUtility.SaveAsPrefabAsset(root, k_Player);
                Directory.CreateDirectory(".utmp");
                File.WriteAllText(".utmp/player-model-restored.txt",
                    $"Restored {meshCount} mesh references and {materialCount} material slots. Rig fingerprint unchanged.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/ZZ/Export Player To Blender")]
        public static void Export()
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(k_Player);
            var document = new ExportDocument();
            var visibleNames = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(IsVisible)
                .Select(renderer => renderer.name).ToHashSet();
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if ((!visibleNames.Contains(renderer.name) &&
                    !visibleNames.Contains(renderer.name.Replace("_Female_", "_Male_"))) || renderer.sharedMesh == null)
                {
                    continue;
                }
                Mesh mesh = renderer.sharedMesh;
                if (AssetDatabase.GetAssetPath(mesh).StartsWith(k_Output, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Player is already subdivided. Reuse the preserved SourceArt input.");
                }
                var record = new MeshRecord
                {
                    Path = AssetDatabase.GetAssetPath(mesh), Name = renderer.name,
                    Positions = mesh.vertices, Normals = mesh.normals, UV = mesh.uv,
                    Weights = mesh.boneWeights, Keys = VertexKeys(mesh),
                    Matrix = renderer.transform.localToWorldMatrix,
                    Bones = renderer.bones.Select(bone => new BoneRecord
                    {
                        Name = bone.name, Parent = bone.parent != null ? bone.parent.name : "",
                        Position = bone.position, Matrix = bone.localToWorldMatrix
                    }).ToArray(),
                    Parts = Enumerable.Range(0, mesh.subMeshCount)
                        .Select(index => new IndexRecord { Indices = mesh.GetTriangles(index) }).ToArray()
                };
                document.Meshes.Add(record);
            }
            Directory.CreateDirectory("SourceArt/Player");
            File.WriteAllText("SourceArt/Player/player-blender-source.json", JsonUtility.ToJson(document));
        }

        [MenuItem("Tools/ZZ/Apply Blender Player Optimization")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before applying player assets.");
            }
            EnsureFolder(k_Output);
            var result = JsonUtility.FromJson<ExportDocument>(File.ReadAllText("SourceArt/Player/player-blender-result.json"));
            var replacements = new Dictionary<Mesh, Mesh>();
            var report = new StringBuilder();
            foreach (MeshRecord record in result.Meshes)
            {
                Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(record.Path);
                string[] keys = VertexKeys(source);
                if (!keys.SequenceEqual(record.Keys) || source.blendShapeCount != 0)
                {
                    throw new InvalidOperationException("Source changed or has unsupported blend shapes: " + record.Path);
                }
                Mesh optimized = ImportSubdivision(source, record);
                string path = k_Output + "/" + source.name + ".asset";
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(optimized, path);
                    saved = optimized;
                }
                else
                {
                    EditorUtility.CopySerialized(optimized, saved);
                    UnityEngine.Object.DestroyImmediate(optimized);
                }
                replacements[source] = saved;
                report.AppendLine($"{source.name}: vertices {source.vertexCount}->{saved.vertexCount}, " +
                    $"triangles {source.triangles.Length / 3}->{saved.triangles.Length / 3}");
            }
            GameObject root = PrefabUtility.LoadPrefabContents(k_Player);
            try
            {
                string rigBefore = RigFingerprint(root);
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer.sharedMesh != null && replacements.TryGetValue(renderer.sharedMesh, out Mesh mesh))
                    {
                        renderer.sharedMesh = mesh;
                    }
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(StylizedMaterial).ToArray();
                }
                if (RigFingerprint(root) != rigBefore)
                {
                    throw new InvalidOperationException("Model import changed a bone, transform or Animator reference.");
                }
                report.AppendLine("Rig hierarchy, local transforms, renderer bone references, Avatar and controller unchanged.");
                PrefabUtility.SaveAsPrefabAsset(root, k_Player);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(".utmp/player-optimization-result.txt", report.ToString());
        }

        private static Material StylizedMaterial(Material source)
        {
            if (source == null || source.name != "Player_Material_01")
            {
                return source;
            }
            string path = k_Output + "/Player Stylized.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = "Player Stylized" };
                Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(k_Output + "/Player Palette.png");
                if (palette == null)
                {
                    throw new InvalidOperationException("Import the Blender palette before applying the model.");
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(palette));
                importer.maxTextureSize = 256;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                material.SetTexture("_BaseMap", palette);
                material.SetTexture("_MainTex", palette);
                material.SetFloat("_Smoothness", 0.08f);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static Mesh ImportSubdivision(Mesh source, MeshRecord record)
        {
            if (record.Positions.Length <= source.vertexCount || record.Weights.Length != record.Positions.Length ||
                record.Normals.Length != record.Positions.Length || record.UV.Length != record.Positions.Length ||
                record.Parts.Length != source.subMeshCount)
            {
                throw new InvalidOperationException("Incomplete subdivided vertex data: " + source.name);
            }
            Bounds envelope = source.bounds;
            envelope.Expand(0.04f);
            foreach (Vector3 position in record.Positions)
            {
                if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z) ||
                    !envelope.Contains(position))
                {
                    throw new InvalidOperationException("Subdivision exceeded the source envelope: " + source.name);
                }
            }
            foreach (BoneWeight weight in record.Weights)
            {
                float sum = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
                if (!float.IsFinite(sum) || Mathf.Abs(sum - 1f) > 0.00001f ||
                    weight.boneIndex0 < 0 || weight.boneIndex0 >= source.bindposes.Length ||
                    weight.boneIndex1 < 0 || weight.boneIndex1 >= source.bindposes.Length ||
                    weight.boneIndex2 < 0 || weight.boneIndex2 >= source.bindposes.Length ||
                    weight.boneIndex3 < 0 || weight.boneIndex3 >= source.bindposes.Length)
                {
                    throw new InvalidOperationException("Subdivision contains invalid skin weights: " + source.name);
                }
            }
            var mesh = new Mesh { name = source.name + " Stylized", indexFormat =
                record.Positions.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : source.indexFormat };
            mesh.vertices = record.Positions;
            mesh.normals = record.Normals;
            mesh.uv = record.UV;
            mesh.boneWeights = record.Weights;
            mesh.bindposes = source.bindposes;
            mesh.subMeshCount = record.Parts.Length;
            for (int part = 0; part < record.Parts.Length; part++)
            {
                mesh.SetTriangles(record.Parts[part].Indices, part);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static string[] VertexKeys(Mesh mesh)
        {
            string[] result = new string[mesh.vertexCount];
            using var data = Mesh.AcquireReadOnlyMeshData(mesh);
            using var hash = SHA256.Create();
            int totalStride = Enumerable.Range(0, mesh.vertexBufferCount).Sum(mesh.GetVertexBufferStride);
            byte[] bytes = new byte[totalStride];
            for (int vertex = 0; vertex < mesh.vertexCount; vertex++)
            {
                int target = 0;
                for (int stream = 0; stream < mesh.vertexBufferCount; stream++)
                {
                    var values = data[0].GetVertexData<byte>(stream);
                    int stride = mesh.GetVertexBufferStride(stream);
                    for (int offset = 0; offset < stride; offset++)
                    {
                        bytes[target++] = values[vertex * stride + offset];
                    }
                }
                result[vertex] = Convert.ToBase64String(hash.ComputeHash(bytes));
            }
            return result;
        }

        private static string RigFingerprint(GameObject root)
        {
            var record = new StringBuilder();
            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            {
                record.Append(AnimationUtility.CalculateTransformPath(bone, root.transform));
                record.Append(JsonUtility.ToJson(bone.localPosition));
                record.Append(JsonUtility.ToJson(bone.localRotation));
                record.Append(JsonUtility.ToJson(bone.localScale));
            }
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                record.Append(AssetDatabase.GetAssetPath(animator.avatar));
                record.Append(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            }
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (Transform bone in renderer.bones)
                {
                    record.Append(bone != null ? AnimationUtility.CalculateTransformPath(bone, root.transform) : "null");
                }
            }
            return record.ToString();
        }

        private static bool IsVisible(Renderer renderer)
        {
            if (!renderer.enabled)
            {
                return false;
            }
            for (Transform ancestor = renderer.transform; ancestor != null; ancestor = ancestor.parent)
            {
                if (!ancestor.gameObject.activeSelf)
                {
                    return false;
                }
            }
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        [Serializable]
        public sealed class ExportDocument
        {
            public List<MeshRecord> Meshes = new();
        }

        [Serializable]
        public sealed class MeshRecord
        {
            public string Path;
            public string Name;
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Vector2[] UV;
            public BoneWeight[] Weights;
            public string[] Keys;
            public Matrix4x4 Matrix;
            public BoneRecord[] Bones;
            public IndexRecord[] Parts;
            public int[] SourceVertices;
        }

        [Serializable]
        public sealed class BoneRecord
        {
            public string Name;
            public string Parent;
            public Vector3 Position;
            public Matrix4x4 Matrix;
        }

        [Serializable]
        public sealed class IndexRecord
        {
            public int[] Indices;
        }

        [MenuItem("Tools/ZZ/Audit Player Model")]
        public static void Audit()
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(k_Player);
            var report = new StringBuilder();
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                report.AppendLine($"ANIMATOR {animator.name} avatar={AssetDatabase.GetAssetPath(animator.avatar)}");
            }
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            report.AppendLine($"RENDERERS {renderers.Length}");
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                bool visible = renderer.enabled;
                for (Transform ancestor = renderer.transform; ancestor != null; ancestor = ancestor.parent)
                {
                    visible &= ancestor.gameObject.activeSelf;
                }
                report.AppendLine($"MESH {renderer.name} visible={visible} enabled={renderer.enabled} active={renderer.gameObject.activeSelf} " +
                    $"vertices={mesh.vertexCount} triangles={mesh.triangles.Length / 3} bones={renderer.bones.Length} " +
                    $"shapes={mesh.blendShapeCount} path={AssetDatabase.GetAssetPath(mesh)} " +
                    $"materials={string.Join(";", renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath))}");
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/player-model-audit.txt", report.ToString());
        }
    }
}
