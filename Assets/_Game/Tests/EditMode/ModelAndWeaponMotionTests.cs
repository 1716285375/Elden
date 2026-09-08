using System;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class ModelAndWeaponMotionTests
    {
        private const string k_Variants = "Assets/_Game/Prefabs/Characters/AI/Variants";
        private const string k_Controller = "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Runtime/Humanoid Runtime.controller";

        [Test]
        public void ModelVariantsHaveTheirOwnValidRigAndNetworkRegistration()
        {
            var prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset");
            string[] assets = AssetDatabase.FindAssets("t:Prefab", new[] { k_Variants });
            Assert.That(assets.Length, Is.EqualTo(7));
            foreach (string guid in assets)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Assert.That(animators.Length, Is.EqualTo(1), prefab.name);
                Animator animator = animators[0];
                Assert.That(animator.avatar.isHuman && animator.avatar.isValid, Is.True, prefab.name);
                Assert.That(AssetDatabase.GetAssetPath(animator.avatar), Does.StartWith(k_Variants),
                    prefab.name + " must use a rebuilt Avatar for its actual hierarchy.");
                Assert.That(prefabs.Contains(prefab), Is.True, prefab.name);
                Assert.That(animator.GetComponent("AICharacterAnimatorManager"), Is.Not.Null, prefab.name);
                Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer =>
                    renderer.sharedMesh != null && renderer.enabled), Is.True, prefab.name);
                Component ai = prefab.GetComponent("AICharacterManager");
                Assert.That(new SerializedObject(ai).FindProperty("m_animator").objectReferenceValue, Is.EqualTo(animator));
            }
        }

        [TestCase("OffHand_Attack_01")]
        [TestCase("OffHand_Attack_02")]
        [TestCase("OffHand_JumpLight")]
        public void LeftHandStatesContainMotionAndBalancedHitWindows(string stateName)
        {
            AnimatorStateMachine machine = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Controller).layers
                .Single(layer => layer.name == "Action Override").stateMachine;
            AnimatorState state = machine.states.Single(child => child.state.name == stateName).state;
            var clip = state.motion as AnimationClip;
            Assert.That(clip != null && clip.humanMotion, Is.True);
            string[] events = AnimationUtility.GetAnimationEvents(clip).Select(item => item.functionName).ToArray();
            Assert.That(events.Count(name => name == "OpenDamageCollider"), Is.EqualTo(1));
            Assert.That(events.Count(name => name == "CloseDamageCollider"), Is.EqualTo(1));
            Assert.That(events, Does.Not.Contain("ActivateMainHandWeaponTrail"));
            Assert.That(state.transitions.Length, Is.GreaterThan(0), "Attacks must return to locomotion.");
        }

        [TestCase("Straight Sword Animator.overrideController", "straight_sword")]
        [TestCase("Broadsword.overrideController", "ultra")]
        public void EquippedWeaponsUseTheirOwnAerialMotions(string file, string prefix)
        {
            string path = file.StartsWith("Broadsword")
                ? "Assets/_Game/Data/Animations/Broadsword/" + file
                : "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/" + file;
            var weapon = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            AnimatorStateMachine action = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Controller).layers
                .Single(layer => layer.name == "Action Override").stateMachine;
            string[] states = { "MainJumpLightAttack", "MainJumpHeavyStart", "MainJumpHeavyIdle", "MainJumpHeavyEnd",
                "TwoHandJumpLightAttack", "TwoHandJumpHeavyStart", "TwoHandJumpHeavyIdle", "TwoHandJumpHeavyEnd" };
            foreach (string name in states)
            {
                var original = (AnimationClip)action.states.Single(child => child.state.name == name).state.motion;
                AnimationClip motion = weapon[original];
                Assert.That(motion, Is.Not.Null, name);
                Assert.That(motion.humanMotion, Is.True, name);
                Assert.That(motion.name, Does.StartWith(prefix), name);
            }
        }

        [Test]
        public void LeftTwoHandGripMirrorsBothAttacksAndLocomotion()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Controller);
            Assert.That(controller.parameters.Any(parameter => parameter.name == "isTwoHandingLeftWeapon"), Is.True);
            AnimatorState[] states = controller.layers.SelectMany(layer => layer.stateMachine.states)
                .Select(child => child.state).Where(state => state.name.StartsWith("TwoHand") ||
                    state.name.Contains("Two Handed")).ToArray();
            Assert.That(states.Length, Is.GreaterThan(10));
            Assert.That(states.All(state => state.mirrorParameterActive &&
                state.mirrorParameter == "isTwoHandingLeftWeapon"), Is.True);
        }
    }
}
