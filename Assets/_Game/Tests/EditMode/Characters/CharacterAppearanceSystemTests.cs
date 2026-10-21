using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class CharacterAppearanceSystemTests
    {
        [Test]
        public void FacialFeaturesSurviveCloneJsonAndNetworkSnapshot()
        {
            var source = new CharacterAppearanceData
            {
                Eyebrow = 4, Beard = 3, IrisSize = 1.15f, EyeColor = new Color32(41, 81, 110, 255)
            };
            var copy = JsonUtility.FromJson<CharacterAppearanceData>(JsonUtility.ToJson(source.Clone()));
            Type state = Type.GetType("ZZ.CharacterAppearanceState, Assembly-CSharp", true);
            object packed = state.GetMethod("FromData").Invoke(null, new object[] { copy });
            var restored = (CharacterAppearanceData)state.GetMethod("ToData").Invoke(packed, null);
            Assert.That(restored.Eyebrow, Is.EqualTo(4));
            Assert.That(restored.Beard, Is.EqualTo(3));
            Assert.That(restored.EyeColor, Is.EqualTo(source.EyeColor));
            Assert.That(restored.IrisSize, Is.EqualTo(source.IrisSize).Within(0.003f));
            source.IrisSize = float.NaN;
            source.Eyebrow = 99;
            source.Beard = -1;
            Assert.That(source.IrisSize, Is.EqualTo(1f));
            Assert.That(source.Eyebrow, Is.EqualTo(4));
            Assert.That(source.Beard, Is.Zero);
        }

        [Test]
        public void PreviewContainsOriginalEyesWithAllFaceControlsAndEyeShader()
        {
            GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Resources/UI/CharacterPreview.prefab");
            var eyes = preview.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(item => item.name.StartsWith("FG_A_1500_")).ToArray();
            Assert.That(eyes.Length, Is.EqualTo(2));
            foreach (SkinnedMeshRenderer eye in eyes)
            {
                Assert.That(eye.sharedMesh.blendShapeCount, Is.EqualTo(87));
                Assert.That(eye.sharedMaterial.shader.name, Is.EqualTo("ZZ/Characters/Extracted Eye"));
                Assert.That(eye.sharedMaterial.GetTexture("_IrisMask"), Is.Not.Null);
                Assert.That(eye.bones.All(bone => bone != null), Is.True);
            }
        }

        [Test]
        public void AppearanceSnapshotRoundTripsAllFaceControlsWithoutSharedArrays()
        {
            var data = new CharacterAppearanceData { Hairstyle = 3, HairColor = new Color32(12, 34, 56, 255) };
            for (int index = 0; index < 87; index++) { data.SetShape(index, (index - 43f) / 43f); }
            CharacterAppearanceData restored = JsonUtility.FromJson<CharacterAppearanceData>(JsonUtility.ToJson(data));
            Assert.That(restored.ShapeCount, Is.EqualTo(87));
            Assert.That(restored.Hairstyle, Is.EqualTo(3));
            Assert.That(restored.HairColor, Is.EqualTo(data.HairColor));
            CharacterAppearanceData copy = restored.Clone();
            copy.SetShape(0, 0.75f);
            Assert.That(restored.GetShape(0), Is.EqualTo(-1f));
            Assert.That(copy.GetShape(0), Is.EqualTo(0.75f));
        }

        [Test]
        public void MorphInputsAreFiniteBoundedAndSchemaLimited()
        {
            var data = new CharacterAppearanceData();
            data.SetShape(0, float.NaN);
            data.SetShape(1, 999f);
            data.SetShape(2, -999f);
            Assert.That(data.GetShape(0), Is.Zero);
            Assert.That(data.GetShape(1), Is.EqualTo(1f));
            Assert.That(data.GetShape(2), Is.EqualTo(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.SetShape(128, 0f));
        }

        [Test]
        public void QuantizedNetworkStatePreservesAppearanceWithinOneStep()
        {
            Type state = Type.GetType("ZZ.CharacterAppearanceState, Assembly-CSharp", true);
            var data = new CharacterAppearanceData { Hairstyle = 8, SkinColor = new Color32(170, 120, 90, 255) };
            for (int index = 0; index < 87; index++) { data.SetShape(index, Mathf.Sin(index)); }
            object packed = state.GetMethod("FromData").Invoke(null, new object[] { data });
            var restored = (CharacterAppearanceData)state.GetMethod("ToData").Invoke(packed, null);
            for (int index = 0; index < 87; index++)
            {
                Assert.That(restored.GetShape(index), Is.EqualTo(data.GetShape(index)).Within(1f / 127f));
            }
            Assert.That(restored.Hairstyle, Is.EqualTo(data.Hairstyle));
            Assert.That(restored.SkinColor, Is.EqualTo(data.SkinColor));
        }

        [Test]
        public void NativeHeadMeshesHaveAllOriginalFaceControls()
        {
            GameObject player = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            try
            {
                var faces = player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(item => item.name.StartsWith("FG_A_0100_")).ToArray();
                Assert.That(faces.Length, Is.EqualTo(3));
                foreach (var face in faces)
                {
                    Assert.That(face.sharedMesh.blendShapeCount, Is.EqualTo(87), face.name);
                    var delta = new Vector3[face.sharedMesh.vertexCount];
                    face.sharedMesh.GetBlendShapeFrameVertices(25, 0, delta, null, null);
                    Assert.That(delta.Any(value => value.sqrMagnitude > 0.00000001f), Is.True, face.name);
                    Assert.That(delta.All(value => float.IsFinite(value.x) && float.IsFinite(value.y) &&
                        float.IsFinite(value.z) && value.magnitude < 0.1f), Is.True, face.name);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        [Test]
        public void ShieldUsesOriginalGripInsteadOfLegacyPivotCompensation()
        {
            GameObject player = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            GameObject shield = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Medium Shield.prefab");
            try
            {
                Component slot = player.GetComponentsInChildren<Transform>(true)
                    .SelectMany(item => item.GetComponents<Component>())
                    .Single(item => item != null && item.GetType().Name == "WeaponModelInstantiationSlot" &&
                        item.GetType().GetProperty("WeaponModelSlot").GetValue(item).ToString() == "LeftHandShieldSlot");
                Assert.That(slot.transform.parent.name, Is.EqualTo("ER_L_Weapon"));
                Assert.That(slot.transform.parent.parent,
                    Is.EqualTo(player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand)));
                Assert.That(slot.transform.localPosition.magnitude, Is.LessThan(0.00001f));
                Transform pivot = shield.transform.Find("Weapon Pivot");
                Assert.That(pivot.localPosition.magnitude, Is.LessThan(0.00001f));
                Assert.That(Quaternion.Angle(pivot.localRotation, Quaternion.identity), Is.LessThan(0.01f));
                Assert.That(pivot.Find("Extracted Equipment Visual").localPosition.magnitude, Is.LessThan(0.00001f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
                PrefabUtility.UnloadPrefabContents(shield);
            }
        }

        [Test]
        public void PreviewContainsOnlyVisualSystemsAndEightOriginalHairstyles()
        {
            GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Resources/UI/CharacterPreview.prefab");
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.GetComponentsInChildren<Component>(true).Any(item => item != null &&
                (item.GetType().Name == "PlayerManager" || item.GetType().Name == "NetworkObject")), Is.False);
            Assert.That(preview.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name.StartsWith("Appearance Hair ")), Is.EqualTo(8));
            Assert.That(preview.GetComponent<Animator>().avatar.isHuman, Is.True);
        }
    }
}
