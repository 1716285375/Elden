using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class PlayerSubdivisionTests
    {
        private const string k_Folder = "Assets/_Game/Art/Characters/Player/Optimized";

        [Test]
        public void ExtractedMeshesRetainNormalizedSkinWeightsAndBindPoses()
        {
            string[] paths = AssetDatabase.FindAssets("t:Mesh", new[]
                { "Assets/_Game/Art/Characters/EldenRing/PlayerBase/Meshes" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(40));
            foreach (string path in paths)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.That(mesh.bindposes.Length, Is.GreaterThan(0), path);
                Assert.That(mesh.triangles.Length, Is.GreaterThan(0), path);
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), path);
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount), path);
                Assert.That(mesh.boneWeights.Length, Is.EqualTo(mesh.vertexCount), path);
                foreach (BoneWeight weight in mesh.boneWeights)
                {
                    Assert.That(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3,
                        Is.EqualTo(1f).Within(0.00001f), path);
                    Assert.That(new[] { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 },
                        Has.All.InRange(0, mesh.bindposes.Length - 1), path);
                }
            }
        }

        [Test]
        public void PlayerUsesEldenRingOutfitAndRetainsModularEquipmentNodes()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            Animator animator = player.GetComponentInChildren<Animator>(true);
            Assert.That(AssetDatabase.GetAssetPath(animator.avatar),
                Is.EqualTo("Assets/_Game/Art/Characters/EldenRing/Set1100/Set1100Avatar.asset"));
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            SkinnedMeshRenderer[] renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(88), "Only extracted body and wearable armor retain skin renderers.");
            SkinnedMeshRenderer[] outfit = renderers.Where(renderer =>
                renderer.transform.parent.name == "Extracted Modular Skin").ToArray();
            Assert.That(outfit.Length, Is.EqualTo(renderers.Length));
            Assert.That(outfit.All(renderer => renderer.sharedMaterial != null &&
                renderer.bones.All(bone => bone != null)), Is.True);
            Assert.That(renderers.Count(renderer => renderer.sharedMesh != null &&
                AssetDatabase.GetAssetPath(renderer.sharedMesh).StartsWith(k_Folder)), Is.Zero);
            Assert.That(renderers.SelectMany(renderer => renderer.sharedMaterials).Any(material =>
                AssetDatabase.GetAssetPath(material).StartsWith(k_Folder)), Is.False);
            Assert.That(renderers.Any(renderer => renderer.name == "Chr_Head_Male_00"), Is.False);
        }

        [Test]
        public void ImportedHelmetRetainsSourceVerticesAfterReimport()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/_Game/Art/Characters/EldenRing/Set1100/Meshes/HD_M_1100_0.asset");
            Assert.That(mesh.vertexCount, Is.EqualTo(4361));
            // Original FLVER vertex; stale native buffers previously left this transformed by an old import.
            Assert.That(Vector3.Distance(mesh.vertices[0],
                new Vector3(0.000023628032f, 1.6911461f, -0.08959981f)), Is.LessThan(0.00001f));
            Assert.That(mesh.boneWeights.All(weight => Mathf.Abs(weight.weight0 + weight.weight1 +
                weight.weight2 + weight.weight3 - 1f) < 0.00001f), Is.True);
        }
    }
}
