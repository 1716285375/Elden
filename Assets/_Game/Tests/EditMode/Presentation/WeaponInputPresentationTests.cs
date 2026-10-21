using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ.Tests
{
    public sealed class WeaponInputPresentationTests
    {
        [TestCase("RightHandSlot", "ER_R_Weapon")]
        [TestCase("LeftHandSlot", "ER_L_Weapon")]
        [TestCase("LeftHandShieldSlot", "ER_L_Shield")]
        public void ImportedWeaponsUseOriginalWeaponBone(string slotName, string boneName)
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            Component slot = player.GetComponentsInChildren<Component>(true).Single(component =>
                component != null && component.GetType().Name == "WeaponModelInstantiationSlot" &&
                component.GetType().GetProperty("WeaponModelSlot").GetValue(component).ToString() == slotName);
            Assert.That(slot.transform.parent.name, Is.EqualTo(boneName));
            Assert.That(Quaternion.Angle(slot.transform.localRotation, Quaternion.identity), Is.LessThan(0.01f));
        }

        [TestCase("Equipment/Weapons/Melee Weapons/Straight Sword", 23)]
        [TestCase("Equipment/Weapons/Melee Weapons/Broadsword", 23)]
        [TestCase("Equipment/Weapons/Melee Weapons/Medium Shield", 49)]
        [TestCase("Equipment/Weapons/Longbow", 44)]
        [TestCase("Abilities/Incantation Catalyst", 41)]
        public void EquippedPlacementsMatchExtractedRegulationAndDummyFrames(string prefabName, int parameterId)
        {
            SourceDocument source = JsonUtility.FromJson<SourceDocument>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/_Game/Art/Equipment/EldenRing/OriginalAttachments.json").text);
            SourceFields fields = source.Parameters.Single(row => row.Id == parameterId).Fields;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/" + prefabName + ".prefab");
            Component attachment = prefab.GetComponents<Component>().Single(component =>
                component != null && component.GetType().Name == "OriginalWeaponAttachment");
            var serialized = new SerializedObject(attachment);
            foreach (var pair in new[] { ("m_rightHand", fields.right_0), ("m_leftHand", fields.left_0),
                ("m_twoHandRight", fields.both_0), ("m_twoHandLeft", fields.leftBoth_0),
                ("m_storedRight", fields.rightHang_0 % 1000), ("m_storedLeft", fields.leftHang_0 % 1000) })
            {
                SourcePlacement expected = source.Attachments.Single(row => row.Id == pair.Item2);
                SerializedProperty actual = serialized.FindProperty(pair.Item1);
                Assert.That(actual.FindPropertyRelative("Bone").stringValue, Is.EqualTo(expected.Bone));
                Assert.That(Vector3.Distance(actual.FindPropertyRelative("Position").vector3Value,
                    new Vector3(expected.Position[0], expected.Position[1], expected.Position[2])), Is.LessThan(0.00001f));
                Quaternion rotation = new Quaternion(expected.Rotation[0], expected.Rotation[1],
                    expected.Rotation[2], expected.Rotation[3]).normalized;
                Assert.That(Quaternion.Angle(actual.FindPropertyRelative("Rotation").quaternionValue, rotation),
                    Is.LessThan(0.05f));
            }
        }

        [Test]
        public void TwoHandTriggerReleaseDoesNotQueueHeavyAttackRelease()
        {
            Type type = Type.GetType("ZZ.PlayerInputManager, Assembly-CSharp");
            var root = new GameObject("Input chord fixture");
            root.SetActive(false);
            Component input = root.AddComponent(type);
            try
            {
                type.GetField("m_isTwoHandInputHeld", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(input, true);
                type.GetProperty("IsMovementInputEnabled").GetSetMethod(true).Invoke(input, new object[] { true });
                foreach (string callback in new[] { "OnRTStarted", "OnRTCanceled" })
                {
                    type.GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(input, new object[] { default(InputAction.CallbackContext) });
                }
                Assert.That(type.GetField("m_hasRTReleasedInput", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(input), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Serializable] private sealed class SourceDocument { public SourcePlacement[] Attachments; public SourceParameter[] Parameters; }
        [Serializable] private sealed class SourcePlacement { public int Id; public string Bone; public float[] Position; public float[] Rotation; }
        [Serializable] private sealed class SourceParameter { public int Id; public SourceFields Fields; }
        [Serializable] private sealed class SourceFields
        {
            public int right_0;
            public int left_0;
            public int both_0;
            public int leftBoth_0;
            public int rightHang_0;
            public int leftHang_0;
        }
    }
}
