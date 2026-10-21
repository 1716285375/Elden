using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class OriginalParryPresentationTests
    {
        [TestCase("core_off_guard_idle_01")]
        [TestCase("core_off_walk_guard_F_01")]
        [TestCase("core_off_walk_guard_B_01")]
        [TestCase("core_off_walk_guard_L_01")]
        [TestCase("core_off_walk_guard_R_01")]
        public void GuardKeepsOriginalUpperBodyAndEquipmentWhileMoving(string slot)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Straight Sword Animator.overrideController");
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            var pair = pairs.Single(item => item.Key.name == slot);
            AnimationClip clip = pair.Value != null ? pair.Value : pair.Key;
            StringAssert.Contains("a000_000100", clip.name);
            foreach (string bone in new[] { "L_Weapon", "R_Weapon", "L_Shield", "R_Shield" })
            {
                Assert.That(AnimationUtility.GetCurveBindings(clip).Count(binding =>
                    binding.path.EndsWith("/ER_" + bone)), Is.EqualTo(7), slot + ": " + bone);
            }
        }

        [Test]
        public void MediumShieldParryUsesOriginalMotionAndAllEquipmentTracks()
        {
            var shield = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/_Game/Data/Items/Weapons/Melee Weapons/Medium Shield.asset"));
            var controller = (AnimatorOverrideController)shield.FindProperty("m_weaponAnimator").objectReferenceValue;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            var pair = pairs.Single(item => item.Key.name == "shield_off_parry_01_slow_start");
            AnimationClip clip = pair.Value != null ? pair.Value : pair.Key;
            StringAssert.Contains("a692_040040", clip.name,
                "Medium shield parry must not fall back to the old shield motion.");
            Assert.That(clip.humanMotion, Is.True);
            foreach (string bone in new[] { "L_Weapon", "R_Weapon", "L_Shield", "R_Shield" })
            {
                Assert.That(AnimationUtility.GetCurveBindings(clip).Count(binding =>
                    binding.path.EndsWith("/ER_" + bone) &&
                    (binding.propertyName.StartsWith("m_LocalPosition.") ||
                     binding.propertyName.StartsWith("m_LocalRotation."))), Is.EqualTo(7), bone);
            }
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            Assert.That(events.Count(item => item.functionName == "EnableIsParrying"), Is.EqualTo(1));
            Assert.That(events.Count(item => item.functionName == "DisableIsParrying"), Is.EqualTo(1));
            Assert.That(events.Single(item => item.functionName == "EnableIsParrying").time,
                Is.EqualTo(4f / 30f).Within(0.0001f));
            Assert.That(events.Single(item => item.functionName == "DisableIsParrying").time,
                Is.EqualTo(10f / 30f).Within(0.0001f));
        }
    }
}
