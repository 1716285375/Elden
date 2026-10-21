using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class ExtractedPlayerActionTests
    {
        [TestCase("Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Straight Sword Animator.overrideController")]
        [TestCase("Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController")]
        public void SwordIdleUsesLoopingExtractedMotion(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            AnimationClip clip = pairs.Single(pair => pair.Key.name == "unarmed_main_idle_01").Value;
            AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/_Game/Art/Characters/EldenRing/PlayerActions/Humanoid/Clips/a000_000000.anim");
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.humanMotion && clip.isLooping, Is.True);
            Assert.That(clip.length, Is.EqualTo(source.length).Within(0.001f));
        }

        [TestCase("Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Straight Sword Animator.overrideController", "01", "a030_030000")]
        [TestCase("Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Straight Sword Animator.overrideController", "02", "a030_030010")]
        [TestCase("Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController", "01", "a030_030000")]
        [TestCase("Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController", "02", "a030_030010")]
        public void EquippedSwordUsesExtractedHumanoidMotionWithCombatEvents(string path, string slot, string name)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            AnimationClip clip = pairs.Select(pair => pair.Value != null ? pair.Value : pair.Key)
                .Single(candidate => candidate.name.EndsWith("straight_sword_main_light_attack_" + slot));
            AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/_Game/Art/Characters/EldenRing/PlayerActions/Humanoid/Clips/" + name + ".anim");
            Assert.That(clip.humanMotion && source.humanMotion, Is.True);
            Assert.That(clip.length, Is.EqualTo(source.length).Within(0.001f));
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
            Assert.That(bindings.Length, Is.GreaterThan(20));
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve expected = AnimationUtility.GetEditorCurve(source, binding);
                AnimationCurve actual = AnimationUtility.GetEditorCurve(clip, binding);
                Assert.That(actual, Is.Not.Null, binding.propertyName);
                Assert.That(actual.Evaluate(source.length * 0.4f),
                    Is.EqualTo(expected.Evaluate(source.length * 0.4f)).Within(0.0001f), binding.propertyName);
            }
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            Assert.That(events.Any(item => item.functionName == "OpenDamageCollider"), Is.True);
            Assert.That(events.Any(item => item.functionName == "CloseDamageCollider"), Is.True);
            Assert.That(events.All(item => item.time >= 0f && item.time <= clip.length), Is.True);
        }
    }
}
