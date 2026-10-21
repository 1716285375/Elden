using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class PlayerSkinnedEquipmentTests
    {
        private GameObject m_player;
        private Component m_skin;

        [SetUp]
        public void SetUp()
        {
            m_player = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            m_skin = m_player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "PlayerSkinnedEquipment");
        }

        [TearDown]
        public void TearDown()
        {
            if (m_player != null)
            {
                PrefabUtility.UnloadPrefabContents(m_player);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UnequippedBodyHasSkinAndUnderwearWithoutArmor(bool isMale)
        {
            Refresh(isMale, -1, -1, -1, -1);
            SkinnedMeshRenderer[] visible = Visible();
            Assert.That(visible.Length, Is.GreaterThan(10));
            Assert.That(visible.Any(renderer => renderer.name.StartsWith("BD_M_1100")), Is.False);
            string gender = isMale ? "_M_" : "_F_";
            Assert.That(visible.All(renderer => renderer.name.Contains(gender) ||
                renderer.name.StartsWith("FG_A_0100")), Is.True);
            Assert.That(visible.Any(renderer => renderer.name.StartsWith("LG" + gender + "0000")), Is.True);
        }

        [Test]
        public void CreationPreviewPrefabStartsWithBodyAndNoArmor()
        {
            GameObject preview = PrefabUtility.LoadPrefabContents("Assets/_Game/Resources/UI/CharacterPreview.prefab");
            try
            {
                var visible = preview.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
                Assert.That(visible.Any(renderer => renderer.name.Contains("_1100_")), Is.False,
                    "Creation must not start with the Drake Knight outfit baked into its preview.");
                Assert.That(visible.Any(renderer => renderer.name.StartsWith("FC_M_0100_3")), Is.True,
                    "The exposed base torso must be present before a class is committed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(preview);
            }
        }

        [Test]
        public void OpenVagabondHelmetKeepsTheFaceUnderItsScalpMask()
        {
            Component equipment = m_player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "PlayerEquipmentManager");
            equipment.GetType().GetMethod("LoadHeadEquipment").Invoke(equipment, new object[]
            {
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/_Game/Art/Characters/EldenRing/StartingClasses/Items/ER_660000.asset")
            });
            Assert.That(Visible().Any(renderer => renderer.name == "FG_A_0100_13"), Is.True,
                "A scalp draw mask must not remove the entire FaceGen face under an open helmet.");
        }

        [TestCase(4, "HD_M_1100", "FC_M_0100_0")]
        [TestCase(5, "BD_M_1100", "FC_M_0100_3")]
        [TestCase(6, "AM_M_1100", "FC_M_0100_8")]
        [TestCase(7, "LG_M_1100", "FC_M_0100_9")]
        public void EquippingOnePieceHidesItsCoveredSkinAndUnequippingRestoresIt(
            int itemId, string prefix, string skinName)
        {
            Refresh(true, itemId == 4 ? itemId : -1, itemId == 5 ? itemId : -1,
                itemId == 6 ? itemId : -1, itemId == 7 ? itemId : -1);
            Assert.That(Visible().Any(renderer => renderer.name.StartsWith(prefix)), Is.True);
            Assert.That(Visible().Any(renderer => renderer.name == skinName), Is.False);
            Refresh(true, -1, -1, -1, -1);
            Assert.That(Visible().Any(renderer => renderer.name.StartsWith(prefix)), Is.False);
            Assert.That(Visible().Any(renderer => renderer.name == skinName), Is.True);
        }

        [Test]
        public void BodyAndArmorUseSameSkeletonWithFiniteBakedVertices()
        {
            Animator animator = m_player.GetComponentInChildren<Animator>(true);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Transform[] bones = null;
            foreach (SkinnedMeshRenderer renderer in Parts())
            {
                Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null, renderer.name);
                Assert.That(renderer.sharedMesh.bindposes.Length, Is.EqualTo(renderer.bones.Length));
                bones ??= renderer.bones;
                Assert.That(renderer.bones, Is.EqualTo(bones), renderer.name);
                var baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked);
                    Assert.That(baked.vertexCount, Is.GreaterThan(0));
                    Assert.That(baked.vertices.All(vertex =>
                        !float.IsNaN(vertex.x) && !float.IsInfinity(vertex.x) &&
                        !float.IsNaN(vertex.y) && !float.IsInfinity(vertex.y) &&
                        !float.IsNaN(vertex.z) && !float.IsInfinity(vertex.z)), Is.True, renderer.name);
                    Assert.That(baked.bounds.size.magnitude, Is.LessThan(5f), renderer.name);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            }
        }

        [Test]
        public void AppearancePreviewHidesHelmetAndRestoresHeadSkin()
        {
            Component equipment = m_player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "PlayerEquipmentManager");
            Type type = equipment.GetType();
            type.GetMethod("LoadHeadEquipment").Invoke(equipment, new object[]
            {
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/_Game/Data/Items/Armor/Starter Hood.asset")
            });
            type.GetMethod("SetHeadEquipmentPresentationHidden").Invoke(equipment, new object[] { true });
            Assert.That(Visible().Any(renderer => renderer.name.StartsWith("HD_M_1100")), Is.False);
            Assert.That(Visible().Any(renderer => renderer.name == "FC_M_0100_0"), Is.True);
            type.GetMethod("SetHeadEquipmentPresentationHidden").Invoke(equipment, new object[] { false });
            Assert.That(Visible().Any(renderer => renderer.name.StartsWith("HD_M_1100")), Is.True);
        }

        [TestCase(HumanBodyBones.LeftMiddleProximal)]
        [TestCase(HumanBodyBones.LeftRingProximal)]
        [TestCase(HumanBodyBones.RightMiddleProximal)]
        [TestCase(HumanBodyBones.RightRingProximal)]
        public void MiddleAndRingFingersHaveTheirOwnAvatarAndSkinBinding(HumanBodyBones finger)
        {
            Animator animator = m_player.GetComponentInChildren<Animator>(true);
            Transform bone = animator.GetBoneTransform(finger);
            Assert.That(bone, Is.Not.Null, finger.ToString());
            Assert.That(Parts().Where(renderer => renderer.name.StartsWith("AM_M_1100")).Any(renderer =>
                renderer.bones.Contains(bone) && renderer.sharedMesh.boneWeights.Any(weight =>
                    (weight.weight0 > 0f && renderer.bones[weight.boneIndex0] == bone) ||
                    (weight.weight1 > 0f && renderer.bones[weight.boneIndex1] == bone) ||
                    (weight.weight2 > 0f && renderer.bones[weight.boneIndex2] == bone) ||
                    (weight.weight3 > 0f && renderer.bones[weight.boneIndex3] == bone))), Is.True);
        }

        [Test]
        public void EquippedHelmetFollowsTheHeadWithoutDetachedVertices()
        {
            Refresh(true, 4, 5, 6, 7);
            Animator animator = m_player.GetComponentInChildren<Animator>(true);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            SkinnedMeshRenderer helmet = Parts().Single(renderer => renderer.name == "HD_M_1100_0");
            Assert.That(helmet.enabled, Is.True);
            var before = new Mesh();
            var after = new Mesh();
            try
            {
                helmet.BakeMesh(before);
                Vector3 offset = Vector3.right * 0.2f;
                head.position += offset;
                helmet.BakeMesh(after);
                Vector3 expected = helmet.transform.InverseTransformVector(offset);
                Assert.That(before.vertices.Zip(after.vertices, (a, b) =>
                    Vector3.Distance(b - a, expected)).Max(), Is.LessThan(0.002f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(before);
                UnityEngine.Object.DestroyImmediate(after);
            }
        }

        private void Refresh(bool isMale, int headId, int bodyId, int handId, int legId)
        {
            m_skin.GetType().GetMethod("Refresh").Invoke(m_skin,
                new object[] { isMale, headId, bodyId, handId, legId });
        }

        private SkinnedMeshRenderer[] Parts()
        {
            return m_player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.transform.parent.name == "Extracted Modular Skin").ToArray();
        }

        private SkinnedMeshRenderer[] Visible()
        {
            return Parts().Where(renderer => renderer.enabled).ToArray();
        }
    }
}
