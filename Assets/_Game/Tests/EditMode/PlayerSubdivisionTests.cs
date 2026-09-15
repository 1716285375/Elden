using System.IO;
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
        public void SubdivisionIncreasesDetailAndRetainsOriginalBindPoses()
        {
            string[] paths = AssetDatabase.FindAssets("t:Mesh", new[] { k_Folder })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths.Length, Is.EqualTo(28));
            foreach (string path in paths)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Mesh original = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Shared/Models/Rigged/Shared/Meshes/" + Path.GetFileName(path));
                Assert.That(mesh.bindposes, Is.EqualTo(original.bindposes), path);
                Assert.That(mesh.triangles.Length, Is.GreaterThan(original.triangles.Length), path);
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
        public void PlayerKeepsItsAvatarAndUsesTheRestoredOriginalBodyAndPalette()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            Animator animator = player.GetComponentInChildren<Animator>(true);
            Assert.That(AssetDatabase.GetAssetPath(animator.avatar),
                Is.EqualTo("Assets/_Game/Art/Shared/Models/Rigged/Shared/Avatars/ZephyrPlayerAvatar.asset"));
            SkinnedMeshRenderer[] renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(1302), "Modular equipment nodes must not be removed.");
            Assert.That(renderers.Count(renderer => renderer.sharedMesh != null &&
                AssetDatabase.GetAssetPath(renderer.sharedMesh).StartsWith(k_Folder)), Is.Zero);
            Assert.That(renderers.SelectMany(renderer => renderer.sharedMaterials).Any(material =>
                AssetDatabase.GetAssetPath(material).StartsWith(k_Folder)), Is.False);
            SkinnedMeshRenderer head = renderers.Single(renderer => renderer.name == "Chr_Head_Male_00");
            Assert.That(AssetDatabase.GetAssetPath(head.sharedMesh), Is.EqualTo(
                "Assets/_Game/Art/Shared/Models/Rigged/Shared/Meshes/Chr_Head_Male_00.asset"));
            Assert.That(AssetDatabase.GetAssetPath(head.sharedMaterial), Is.EqualTo(
                "Assets/_Game/Art/Shared/Materials/General/Player_Material_01.mat"));
        }
    }
}
