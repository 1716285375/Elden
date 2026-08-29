using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public class PowerStanceSystemTests
    {
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_CombatSwordFolder =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Sword/";
        private const string k_LocomotionFolder =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/";

        private static readonly string[] s_weaponPaths =
        {
            "Assets/Data/Items/Weapons/Melee Weapons/Unarmed.asset",
            "Assets/Data/Items/Weapons/Melee Weapons/Straight Sword.asset",
            "Assets/Data/Items/Weapons/Melee Weapons/Broadsword.asset"
        };

        private static readonly string[] s_dualClipPaths =
        {
            k_CombatSwordFolder +
                "straight_sword_dw_light_attack_01.anim",
            k_CombatSwordFolder +
                "straight_sword_dw_light_attack_02.anim",
            k_LocomotionFolder +
                "straight_sword_dw_run_attack_01.anim",
            k_LocomotionFolder +
                "straight_sword_dw_roll_attack_01_release.anim",
            k_CombatSwordFolder +
                "straight_sword_dw_back_step_attack_04_release.anim",
            k_LocomotionFolder +
                "straight_sword_dw_jump_attack_01_end.anim"
        };

        [Test]
        public static void DualAttackTypesAppendStableSerializedIdentifiers()
        {
            Type attackType = GetRuntimeType("ZZ.AttackType");

            AssertEnumValue(attackType, "DualAttack01", 11);
            AssertEnumValue(attackType, "DualAttack02", 12);
            AssertEnumValue(attackType, "DualJumpAttack", 13);
            AssertEnumValue(attackType, "DualRunAttack", 14);
            AssertEnumValue(attackType, "DualRollAttack", 15);
            AssertEnumValue(attackType, "DualBackstepAttack", 16);
        }

        [Test]
        public static void EligibilityUsesWeaponClassAndRejectsTwoHanding()
        {
            UnityEngine.Object straightSword = LoadWeapon(s_weaponPaths[1]);
            UnityEngine.Object broadsword = LoadWeapon(s_weaponPaths[2]);
            UnityEngine.Object unarmed = LoadWeapon(s_weaponPaths[0]);
            MethodInfo eligibilityMethod = GetRuntimeType(
                "ZZ.PlayerCombatManager").GetMethod(
                    "CanUsePowerStance",
                    BindingFlags.Public | BindingFlags.Static);

            Assert.That(
                eligibilityMethod.Invoke(
                    null,
                    new object[] { straightSword, broadsword, false }),
                Is.True);
            Assert.That(
                eligibilityMethod.Invoke(
                    null,
                    new object[] { straightSword, unarmed, false }),
                Is.False);
            Assert.That(
                eligibilityMethod.Invoke(
                    null,
                    new object[] { straightSword, broadsword, true }),
                Is.False);
        }

        [Test]
        public static void AttackResolutionUsesEP125PriorityAndComboCycle()
        {
            AssertResolvedAttack(
                "DualJumpAttack",
                false,
                false,
                false,
                false,
                true,
                "DualAttack01");
            AssertResolvedAttack(
                "DualRollAttack",
                true,
                true,
                true,
                true,
                true,
                "DualAttack01");
            AssertResolvedAttack(
                "DualBackstepAttack",
                true,
                true,
                false,
                true,
                true,
                "DualAttack01");
            AssertResolvedAttack(
                "DualRunAttack",
                true,
                false,
                false,
                false,
                true,
                "DualAttack01");
            AssertResolvedAttack(
                "DualAttack02",
                true,
                false,
                false,
                false,
                false,
                "DualAttack01");
            AssertResolvedAttack(
                "DualAttack01",
                true,
                false,
                false,
                false,
                false,
                "DualAttack02");
        }

        [Test]
        public static void OffHandInputOwnsPowerStanceAndComboWindow()
        {
            string inputSource = ReadRuntimeSource(
                "World Managers/PlayerInputManager.cs");
            string combatSource = ReadRuntimeSource(
                "Character/Player/PlayerCombatManager.cs");

            Assert.That(inputSource, Does.Contain("CanUsePowerStance()"));
            Assert.That(
                inputSource,
                Does.Contain("PerformPowerStanceLeftHandAction"));
            Assert.That(combatSource, Does.Contain("m_canPerformOffHandCombo"));
            Assert.That(combatSource, Does.Contain("IsUsingLeftHand.Value"));
            Assert.That(
                combatSource,
                Does.Contain("SetCharacterActionHand(false)"));
            Assert.That(
                GetRuntimeType("ZZ.PlayerCombatManager").GetProperty(
                    "CanPerformOffHandCombo"),
                Is.Not.Null);
        }

        [Test]
        public static void DualDamageAndStaminaModifiersUseExpectedBalance()
        {
            Type attackType = GetRuntimeType("ZZ.AttackType");
            object[] dualAttacks =
            {
                Enum.Parse(attackType, "DualAttack01"),
                Enum.Parse(attackType, "DualAttack02"),
                Enum.Parse(attackType, "DualJumpAttack"),
                Enum.Parse(attackType, "DualRunAttack"),
                Enum.Parse(attackType, "DualRollAttack"),
                Enum.Parse(attackType, "DualBackstepAttack")
            };
            foreach (string weaponPath in s_weaponPaths)
            {
                UnityEngine.Object weapon = LoadWeapon(weaponPath);
                MethodInfo damageMethod = weapon.GetType().GetMethod(
                    "GetAttackDamageModifier");
                MethodInfo staminaMethod = weapon.GetType().GetMethod(
                    "GetStaminaCostMultiplier");
                Assert.That(
                    dualAttacks.All(dualAttack =>
                        Mathf.Approximately(
                            (float)damageMethod.Invoke(
                                weapon,
                                new[] { dualAttack }),
                            0.77f)),
                    Is.True,
                    weapon.name);
                Assert.That(
                    GetStaminaMultiplier(
                        staminaMethod,
                        weapon,
                        attackType,
                        "DualRunAttack"),
                    Is.EqualTo(GetStaminaMultiplier(
                        staminaMethod,
                        weapon,
                        attackType,
                        "RunningAttack01")));
                Assert.That(
                    GetStaminaMultiplier(
                        staminaMethod,
                        weapon,
                        attackType,
                        "DualRollAttack"),
                    Is.EqualTo(GetStaminaMultiplier(
                        staminaMethod,
                        weapon,
                        attackType,
                        "RollAttack01")));
                Assert.That(
                    GetStaminaMultiplier(
                        staminaMethod,
                        weapon,
                        attackType,
                        "DualBackstepAttack"),
                    Is.EqualTo(GetStaminaMultiplier(
                        staminaMethod,
                        weapon,
                        attackType,
                        "BackStepAttack01")));
            }
        }

        [Test]
        public static void EquipmentExposesIndependentColliderWindows()
        {
            string[] methodNames =
            {
                "OpenMainHandDamageCollider",
                "CloseMainHandDamageCollider",
                "OpenOffHandDamageCollider",
                "CloseOffHandDamageCollider"
            };
            foreach (string methodName in methodNames)
            {
                Assert.That(
                    GetRuntimeType("ZZ.PlayerEquipmentManager")
                        .GetMethod(methodName),
                    Is.Not.Null,
                    methodName);
                Assert.That(
                    GetRuntimeType("ZZ.PlayerAnimatorManager")
                        .GetMethod(methodName),
                    Is.Not.Null,
                    methodName);
            }

            string equipmentSource = ReadRuntimeSource(
                "Character/Equipment/PlayerEquipmentManager.cs");
            Assert.That(equipmentSource, Does.Contain("weaponManager.SetAttackType("));
            Assert.That(equipmentSource, Does.Contain("weaponManager.OpenDamageCollider()"));
        }

        [Test]
        public static void AnimatorContainsEveryPowerStanceState()
        {
            AnimatorController controller = AssetDatabase
                .LoadAssetAtPath<AnimatorController>(k_ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .Single(layer => layer.name == "Action Override")
                .stateMachine;
            string[] stateNames =
            {
                "Dual_Attack_01",
                "Dual_Attack_02",
                "Dual_Jump_Attack_Start",
                "Dual_Jump_Attack_Idle",
                "Dual_Jump_Attack_End",
                "Dual_Run_Attack",
                "Dual_Roll_Attack",
                "Dual_BackStep_Attack"
            };

            Assert.That(controller, Is.Not.Null);
            Assert.That(
                stateNames.All(stateName =>
                    stateMachine.states.Any(childState =>
                        childState.state.name == stateName)),
                Is.True);
            AnimatorState jumpEnd = stateMachine.states
                .Select(childState => childState.state)
                .Single(state => state.name == "Dual_Jump_Attack_End");
            Assert.That(
                jumpEnd.behaviours.Any(behaviour =>
                    behaviour.GetType().Name == "ResetJumpingState"),
                Is.True);
        }

        [Test]
        public static void DualClipsOwnTwoCostsAndTwoIndependentHitWindows()
        {
            string[] requiredEvents =
            {
                "OpenMainHandDamageCollider",
                "CloseMainHandDamageCollider",
                "OpenOffHandDamageCollider",
                "CloseOffHandDamageCollider"
            };
            foreach (string clipPath in s_dualClipPaths)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    clipPath);
                string[] eventNames = AnimationUtility
                    .GetAnimationEvents(clip)
                    .Select(animationEvent => animationEvent.functionName)
                    .ToArray();

                Assert.That(
                    requiredEvents.All(eventNames.Contains),
                    Is.True,
                    clipPath);
                Assert.That(
                    eventNames.Count(eventName =>
                        eventName == "DrainStaminaBasedOnAttack"),
                    Is.EqualTo(2),
                    clipPath);
            }
        }

        [Test]
        public static void DualAttackReplicationUsesExistingAttackRpc()
        {
            string combatSource = ReadRuntimeSource(
                "Character/Player/PlayerCombatManager.cs");
            string animatorSource = ReadRuntimeSource(
                "Character/CharacterAnimatorManager.cs");

            Assert.That(
                combatSource,
                Does.Contain("NotifyServerOfAttackActionServerRpc(attackType)"));
            Assert.That(
                animatorSource,
                Does.Contain("case AttackType.DualJumpAttack:"));
            Assert.That(
                animatorSource,
                Does.Contain("case AttackType.DualBackstepAttack:"));
        }

        private static void AssertResolvedAttack(
            string expected,
            bool isGrounded,
            bool isPerformingAction,
            bool canPerformRollAttack,
            bool canPerformBackstepAttack,
            bool isSprinting,
            string previousAttack)
        {
            Type attackType = GetRuntimeType("ZZ.AttackType");
            MethodInfo resolveMethod = GetRuntimeType(
                "ZZ.PlayerCombatManager").GetMethod(
                    "ResolvePowerStanceAttackType",
                    BindingFlags.Public | BindingFlags.Static);
            object resolvedAttack = resolveMethod.Invoke(
                null,
                new object[]
                {
                    isGrounded,
                    isPerformingAction,
                    canPerformRollAttack,
                    canPerformBackstepAttack,
                    isSprinting,
                    Enum.Parse(attackType, previousAttack)
                });
            Assert.That(
                resolvedAttack.ToString(),
                Is.EqualTo(expected));
        }

        private static UnityEngine.Object LoadWeapon(string assetPath)
        {
            return AssetDatabase.LoadMainAssetAtPath(assetPath);
        }

        private static float GetStaminaMultiplier(
            MethodInfo staminaMethod,
            UnityEngine.Object weapon,
            Type attackType,
            string attackName)
        {
            return (float)staminaMethod.Invoke(
                weapon,
                new[] { Enum.Parse(attackType, attackName) });
        }

        private static void AssertEnumValue(
            Type enumType,
            string name,
            int expectedValue)
        {
            Assert.That(
                Convert.ToInt32(Enum.Parse(enumType, name)),
                Is.EqualTo(expectedValue));
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
        }
    }
}
