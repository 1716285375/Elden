using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Tests
{
    public class VillageResourceReplacementTests
    {
        [TestCase("Equipment/Weapons/Melee Weapons/Medium Shield.prefab")]
        [TestCase("Equipment/Weapons/Longbow.prefab")]
        [TestCase("Abilities/Incantation Catalyst.prefab")]
        [TestCase("Projectiles/Draw Arrow.prefab")]
        [TestCase("Projectiles/Released Arrow.prefab")]
        public void EquipmentUsesExtractedTexturedMeshesWithoutPrimitiveVisuals(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/" + path);
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(1));
            MeshFilter filter = renderers[0].GetComponent<MeshFilter>();
            Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(150));
            Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Does.StartWith("Assets/_Game/Art/Equipment/EldenRing/"));
            Material material = renderers[0].sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Component>(true).Any(component => component == null), Is.False);
        }

        [Test]
        public void VillageWorldRewardVisualsUseOnlyVillageModels()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/_Game/Scenes/Levels/LV02_VikingVillage/SCN_LV02_VikingVillage.unity", OpenSceneMode.Single);
            Transform gameplay = scene.GetRootGameObjects().Single(root => root.name == "_VillageGameplay").transform;
            MeshRenderer[] visuals = gameplay.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(visuals.Length, Is.GreaterThanOrEqualTo(8));
            foreach (MeshRenderer renderer in visuals)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Assert.That(filter, Is.Not.Null, renderer.name);
                Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Does.StartWith("Assets/Viking Village/"), renderer.name);
            }
        }

        [TestCase("Empty Flask")]
        [TestCase("Health Flask")]
        [TestCase("Focus Flask")]
        public void ConsumableVesselsUseVillageModels(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Items/" + name + ".prefab");
            MeshFilter[] meshes = prefab.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(meshes.Length, Is.GreaterThan(0));
            foreach (MeshFilter mesh in meshes)
            {
                Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Does.StartWith("Assets/Viking Village/"));
            }
        }
    }
}
