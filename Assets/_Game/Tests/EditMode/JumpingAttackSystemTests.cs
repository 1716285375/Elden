using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public class JumpingAttackSystemTests
    {
        private const string k_ControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_OverrideControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Overrides/Straight Sword.overrideController";

        private static readonly string[] s_weaponPaths =
        {
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Unarmed.asset",
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset",
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset"
        };

        [Test]
        public void JumpAttackTypesAppendStableSerializedIdentifiers()
        {
            Type attackType = Type.GetType("ZZ.AttackType, Assembly-CSharp");

            Assert.That(attackType, Is.Not.Null);
            Assert.That(
                Convert.ToInt32(Enum.Parse(attackType, "LightJumpingAttack01")),
                Is.EqualTo(9));
            Assert.That(
                Convert.ToInt32(Enum.Parse(attackType, "HeavyJumpingAttack01")),
                Is.EqualTo(10));
        }

        [TestCase(false, false, "Airborne")]
        [TestCase(false, true, "Airborne")]
        [TestCase(true, true, "Takeoff")]
        [TestCase(true, false, "Grounded")]
        public void AttackContextPrioritizesAirborneAndBlocksTakeoff(
            bool isGrounded,
            bool isJumping,
            string expectedContext)
        {
            Type actionType = Type.GetType(
                "ZZ.WeaponItemBasedAction, Assembly-CSharp");
            MethodInfo resolveMethod = actionType?.GetMethod(
                "ResolveJumpAttackContext",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(resolveMethod, Is.Not.Null);
            object context = resolveMethod.Invoke(
                null,
                new object[] { isGrounded, isJumping });
            Assert.That(context.ToString(), Is.EqualTo(expectedContext));
        }

        [Test]
        public void WeaponsUseIndependentJumpDamageAndSharedStaminaCosts()
        {
            Type attackType = Type.GetType("ZZ.AttackType, Assembly-CSharp");
            object lightJump = Enum.Parse(attackType, "LightJumpingAttack01");
            object heavyJump = Enum.Parse(attackType, "HeavyJumpingAttack01");
            object lightGround = Enum.Parse(attackType, "LightAttack01");
            object heavyGround = Enum.Parse(attackType, "HeavyAttack01");

            foreach (string weaponPath in s_weaponPaths)
            {
                UnityEngine.Object weapon = AssetDatabase.LoadMainAssetAtPath(weaponPath);
                MethodInfo damageMethod = weapon.GetType().GetMethod(
                    "GetAttackDamageModifier");
                MethodInfo staminaMethod = weapon.GetType().GetMethod(
                    "GetStaminaCostMultiplier");

                Assert.That(damageMethod.Invoke(weapon, new[] { lightJump }),
                    Is.EqualTo(1f));
                Assert.That(damageMethod.Invoke(weapon, new[] { heavyJump }),
                    Is.EqualTo(1.8f));
                Assert.That(staminaMethod.Invoke(weapon, new[] { lightJump }),
                    Is.EqualTo(staminaMethod.Invoke(weapon, new[] { lightGround })));
                Assert.That(staminaMethod.Invoke(weapon, new[] { heavyJump }),
                    Is.EqualTo(staminaMethod.Invoke(weapon, new[] { heavyGround })));
            }
        }

        [Test]
        public void AnimatorContainsMainAndTwoHandJumpAttackGraphs()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                k_ControllerPath);
            AnimatorOverrideController overrideController =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                    k_OverrideControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .Single(layer => layer.name == "Action Override")
                .stateMachine;
            AnimatorState emptyState = GetState(stateMachine, "Empty");

            AssertLightJumpState(
                stateMachine,
                emptyState,
                overrideController,
                "MainJumpLightAttack",
                "straight_sword_main_jump_light_attack_01");
            AssertLightJumpState(
                stateMachine,
                emptyState,
                overrideController,
                "TwoHandJumpLightAttack",
                "straight_sword_th_jump_light_attack_01");
            AssertHeavyJumpGraph(stateMachine, emptyState, "MainJumpHeavy");
            AssertHeavyJumpGraph(stateMachine, emptyState, "TwoHandJumpHeavy");
        }

        [Test]
        public void JumpClipsOwnStaminaAndDamageColliderWindows()
        {
            string locomotion =
                "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/";
            AssertEvents(
                locomotion + "straight_sword_main_jump_light_attack_01.anim",
                "DrainStaminaBasedOnAttack",
                "OpenDamageCollider",
                "CloseDamageCollider");
            AssertEvents(
                locomotion + "straight_sword_th_jump_light_attack_01.anim",
                "DrainStaminaBasedOnAttack",
                "OpenDamageCollider",
                "CloseDamageCollider");
            AssertEvents(
                locomotion + "straight_sword_main_jump_attack_01_charge.anim",
                "DrainStaminaBasedOnAttack",
                "OpenDamageCollider");
            AssertEvents(
                locomotion + "straight_sword_main_jump_attack_01_end.anim",
                "CloseDamageCollider");
            AssertEvents(
                locomotion + "straight_sword_th_jump_attack_01_charge.anim",
                "DrainStaminaBasedOnAttack",
                "OpenDamageCollider");
            AssertEvents(
                locomotion + "straight_sword_th_jump_attack_01_end.anim",
                "CloseDamageCollider");
        }

        [Test]
        public void FireballMaintainsAuthoredSpeedInsteadOfAccumulatingVelocity()
        {
            Type spellManagerType = Type.GetType("ZZ.SpellManager, Assembly-CSharp");
            GameObject projectile = new GameObject("Jump Attack Fireball Test");
            try
            {
                projectile.AddComponent<SphereCollider>();
                Component spellManager = projectile.AddComponent(spellManagerType);
                Rigidbody rigidbody = projectile.GetComponent<Rigidbody>();
                MethodInfo awake = spellManagerType.GetMethod(
                    "Awake",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo fixedUpdate = spellManagerType.GetMethod(
                    "FixedUpdate",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                Assert.That(spellManager, Is.Not.Null);
                Assert.That(awake, Is.Not.Null);
                Assert.That(fixedUpdate, Is.Not.Null);
                awake.Invoke(spellManager, null);
                fixedUpdate.Invoke(spellManager, null);
                float firstSpeed = rigidbody.linearVelocity.magnitude;
                fixedUpdate.Invoke(spellManager, null);

                Assert.That(firstSpeed, Is.EqualTo(18f).Within(0.001f));
                Assert.That(
                    rigidbody.linearVelocity.magnitude,
                    Is.EqualTo(firstSpeed).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectile);
            }
        }

        private static void AssertLightJumpState(
            AnimatorStateMachine stateMachine,
            AnimatorState emptyState,
            AnimatorOverrideController overrideController,
            string stateName,
            string expectedOverrideName)
        {
            AnimatorState state = GetState(stateMachine, stateName);
            AnimationClip resolvedClip = overrideController[state.motion.name];

            Assert.That(resolvedClip?.name, Is.EqualTo(expectedOverrideName));
            Assert.That(
                state.transitions.Any(transition =>
                    transition.destinationState == emptyState &&
                    transition.hasExitTime),
                Is.True);
        }

        private static void AssertHeavyJumpGraph(
            AnimatorStateMachine stateMachine,
            AnimatorState emptyState,
            string prefix)
        {
            AnimatorState start = GetState(stateMachine, prefix + "Start");
            AnimatorState idle = GetState(stateMachine, prefix + "Idle");
            AnimatorState end = GetState(stateMachine, prefix + "End");

            AssertTransition(start, idle, AnimatorConditionMode.IfNot);
            AssertTransition(start, end, AnimatorConditionMode.If);
            AssertTransition(idle, end, AnimatorConditionMode.If);
            Assert.That(
                end.transitions.Any(transition =>
                    transition.destinationState == emptyState &&
                    transition.hasExitTime),
                Is.True);
            Assert.That(
                end.behaviours.Any(behaviour =>
                    behaviour.GetType().Name == "ResetJumpingState"),
                Is.True);
        }

        private static void AssertTransition(
            AnimatorState source,
            AnimatorState destination,
            AnimatorConditionMode expectedMode)
        {
            AnimatorStateTransition transition = source.transitions.Single(candidate =>
                candidate.destinationState == destination);
            Assert.That(transition.conditions.Length, Is.EqualTo(1));
            Assert.That(transition.conditions[0].parameter, Is.EqualTo("isGrounded"));
            Assert.That(transition.conditions[0].mode, Is.EqualTo(expectedMode));
        }

        private static AnimatorState GetState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .Single(state => state.name == stateName);
        }

        private static void AssertEvents(string clipPath, params string[] eventNames)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            string[] actualEvents = AnimationUtility.GetAnimationEvents(clip)
                .Select(animationEvent => animationEvent.functionName)
                .ToArray();
            Assert.That(eventNames.All(actualEvents.Contains), Is.True, clipPath);
        }
    }
}
