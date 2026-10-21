using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class EldenRingPresentationTests
    {
        [Test]
        public void MapChunkHasTexturedGeometryAndMatchingStaticCollision()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Art/Environment/EldenRing/Stormveil/Stormveil Rampart.prefab");
            Assert.That(prefab, Is.Not.Null);
            MeshFilter[] meshes = prefab.GetComponentsInChildren<MeshFilter>();
            Assert.That(meshes.Length, Is.GreaterThan(10));
            foreach (MeshFilter filter in meshes)
            {
                Mesh mesh = filter.sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(mesh.vertices.All(vertex =>
                    float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z)), Is.True);
                Assert.That(filter.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(mesh));
                Material material = filter.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(AssetDatabase.GetAssetPath(material.mainTexture),
                    Does.StartWith("Assets/_Game/Art/Environment/EldenRing/Stormveil/Textures/"));
            }
        }

        [TestCase("Guard Spark", "s10030_a")]
        [TestCase("Parry Flash", "s10061_a")]
        public void CombatEmittersUseOriginalFlareTexturesAndKeepTheirLifetime(string name, string textureName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Resources/Effects/Anime/" + name + ".prefab");
            Assert.That(prefab.GetComponents<MonoBehaviour>()
                .Any(component => component.GetType().Name == "Utility_DestroyAfterTime"), Is.True);
            foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>())
            {
                Assert.That(particles.main.loop, Is.False);
                Assert.That(particles.GetComponent<ParticleSystemRenderer>().sharedMaterial.GetTexture("_BaseMap").name,
                    Is.EqualTo(textureName));
            }
        }

        [Test]
        public void EnemyDamageAudioUsesNonSilentDecodedOriginalMedia()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab");
            SerializedObject serialized = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Select(component => new SerializedObject(component))
                .Single(component => component.FindProperty("m_damageGrunts") != null);
            SerializedProperty clips = serialized.FindProperty("m_damageGrunts");
            Assert.That(clips.arraySize, Is.EqualTo(3));
            for (int index = 0; index < clips.arraySize; index++)
            {
                var clip = (AudioClip)clips.GetArrayElementAtIndex(index).objectReferenceValue;
                Assert.That(AssetDatabase.GetAssetPath(clip), Does.StartWith("Assets/_Game/Art/Audio/EldenRing/c3000/"));
                Assert.That(clip.length, Is.InRange(0.1f, 2f));
                var samples = new float[clip.samples * clip.channels];
                Assert.That(clip.GetData(samples, 0), Is.True);
                Assert.That(samples.Any(sample => Mathf.Abs(sample) > 0.001f), Is.True);
            }
        }

        [Test]
        public void EnemyBodyKeepsTheAuthoredAttackVolumes()
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab");
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                Transform visual = animator.transform.Find("EldenRing c3000");
                Assert.That(visual, Is.Not.Null);
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
                Assert.That(root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Count(renderer => renderer.enabled), Is.EqualTo(21));
                foreach (SkinnedMeshRenderer renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Assert.That(renderer.bones.All(bone => bone != null && bone.IsChildOf(visual)), Is.True);
                    Assert.That(renderer.sharedMesh.bindposes.Length, Is.EqualTo(renderer.bones.Length));
                }
                Collider[] attacks = root.GetComponentsInChildren<Collider>(true)
                    .Where(collider => collider.name.Contains("Hand Damage")).ToArray();
                Assert.That(attacks.Length, Is.EqualTo(2));
                foreach (Collider attack in attacks)
                {
                    Assert.That(attack.enabled, Is.False);
                    Assert.That(attack.transform.parent.name, Is.EqualTo("AI Damage Colliders"));
                    Assert.That(attack.transform.localPosition.y, Is.EqualTo(1f));
                    Assert.That(attack.transform.localPosition.z, Is.EqualTo(0.9f));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase("Quick Slot Items/Flask of Crimson Tears", "00015")]
        [TestCase("Quick Slot Items/Flask of Cerulean Tears", "00019")]
        [TestCase("Weapons/Melee Weapons/Broadsword", "10042")]
        public void CatalogUsesTheMatchingOriginalIcon(string relativePath, string iconId)
        {
            Object item = AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Data/Items/" + relativePath + ".asset");
            Assert.That(item, Is.Not.Null);
            var serialized = new SerializedObject(item);
            var sprite = serialized.FindProperty("m_itemIcon").objectReferenceValue as Sprite;
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.name, Is.EqualTo("MENU_ItemIcon_" + iconId));
            Assert.That(AssetDatabase.GetAssetPath(sprite), Does.StartWith("Assets/_Game/Art/UI/EldenRing/Icons/"));
        }

        [TestCase("Broadsword", 1326)]
        [TestCase("Straight Sword", 1671)]
        public void SwordArtFitsTheExistingDamageVolume(string weaponName, int vertexCount)
        {
            string path = "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/" + weaponName + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform pivot = root.transform.Find("Weapon Pivot");
                Transform visual = pivot.Find("EldenRing " + weaponName);
                Assert.That(visual, Is.Not.Null);
                Mesh mesh = visual.GetComponent<MeshFilter>().sharedMesh;
                BoxCollider collider = root.GetComponentInChildren<BoxCollider>(true);
                Assert.That(collider.enabled, Is.False, "The attack window must own collider activation.");
                Assert.That(mesh.vertexCount, Is.EqualTo(vertexCount));
                Assert.That(mesh.bounds.size.y * visual.localScale.y,
                    Is.EqualTo(collider.size.y).Within(0.00001f));
                Assert.That(Vector3.Distance(visual.localPosition +
                    Vector3.Scale(mesh.bounds.center, visual.localScale), collider.center), Is.LessThan(0.00001f));
                Assert.That(pivot.GetComponentsInChildren<MeshRenderer>(true)
                    .Count(renderer => renderer.enabled), Is.EqualTo(1));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
