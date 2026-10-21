using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Tests
{
    public class LevelUpToolkitTests
    {
        [Test]
        public void ProjectedAttributesAreClampedWithoutLegacySliderObjects()
        {
            Type type = Type.GetType("ZZ.PlayerUILevelUpManager, Assembly-CSharp", true);
            Type attribute = Type.GetType("ZZ.CharacterAttribute, Assembly-CSharp", true);
            GameObject owner = new GameObject("Level Plan Test");
            try
            {
                Component manager = owner.AddComponent(type);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                int[] current = (int[])type.GetField("m_currentAttributes", flags).GetValue(manager);
                int[] projected = (int[])type.GetField("m_projectedAttributes", flags).GetValue(manager);
                current[0] = 10;
                MethodInfo set = type.GetMethod("SetProjectedAttribute");
                set.Invoke(manager, new[] { Enum.ToObject(attribute, 0), (object)(-100) });
                Assert.That(projected[0], Is.EqualTo(10));
                set.Invoke(manager, new[] { Enum.ToObject(attribute, 0), (object)1000 });
                Assert.That(projected[0], Is.EqualTo(99));
                type.GetField("m_isConfirmationPending", flags).SetValue(manager, true);
                set.Invoke(manager, new[] { Enum.ToObject(attribute, 0), (object)20 });
                Assert.That(projected[0], Is.EqualTo(99), "An open confirmation freezes the reviewed plan.");
                type.GetField("m_isConfirmationPending", flags).SetValue(manager, false);
                Assert.DoesNotThrow(() => set.Invoke(manager, new[] { Enum.ToObject(attribute, 99), (object)20 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void NativeLevelLayoutKeepsConfirmationOutsideDisabledPlan()
        {
            VisualElement root = Resources.Load<VisualTreeAsset>("UI/LevelUp").CloneTree();
            Assert.That(root.Q<ScrollView>("attributes"), Is.Not.Null);
            Assert.That(root.Q("level-content").Contains(root.Q("level-confirmation")), Is.False);
            Assert.That(root.Q<Button>("confirm"), Is.Not.Null);
            Assert.That(root.Q<Button>("cancel"), Is.Not.Null);
            VisualElement row = Resources.Load<VisualTreeAsset>("UI/LevelUpAttributeRow").CloneTree();
            Assert.That(row.Q<SliderInt>("attribute-slider"), Is.Not.Null);
            Assert.That(row.Q<Button>("increase"), Is.Not.Null);
        }
    }
}
