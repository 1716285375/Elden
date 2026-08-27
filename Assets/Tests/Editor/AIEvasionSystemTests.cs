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
    public class AIEvasionSystemTests
    {
        private const string k_AIControllerPath =
            "Assets/Data/Animations/AI/Undead AI Animator.controller";
        private const string k_RollClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/" +
            "core_main_roll_med_to_idle_F_01.anim";
        private const string k_UndeadPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";

        [Test]
        public void LegacyUndeadDefaultsOffWhileBossCanEvade()
        {
            AssertEvasionConfiguration(k_UndeadPrefabPath, false);
            AssertEvasionConfiguration(k_BossPrefabPath, true);
        }

        [Test]
        public void CombatRotationRollsOnceAndEvadesOnceAgainstAnAttack()
        {
            string source = ReadRuntimeSource(
                "Character/AI/States/CombatStanceAIState.cs");

            Assert.That(source, Does.Contain("m_hasRolledForEvasionChance"));
            Assert.That(source, Does.Contain("m_willEvadeDuringThisCombatRotation"));
            Assert.That(source, Does.Contain("!character.IsCurrentTargetAttacking"));
            Assert.That(
                source,
                Does.Contain("m_hasEvaded = character.TryPerformEvasion();"));
            Assert.That(source, Does.Contain("m_hasEvaded = false;"));
        }

        [Test]
        public void EvasionExecutionIsVirtualRangeLimitedAndInvulnerable()
        {
            Type combatType = GetRuntimeType("ZZ.AICharacterCombatManager");
            MethodInfo method = combatType.GetMethod("PerformEvasion");
            string source = ReadRuntimeSource(
                "Character/AI/AICharacterCombatManager.cs");

            Assert.That(method, Is.Not.Null);
            Assert.That(method.IsVirtual, Is.True);
            Assert.That(source, Does.Contain("target == null"));
            Assert.That(source, Does.Contain("> m_maximumEvasionDistance"));
            Assert.That(source, Does.Contain("SetInvulnerable(true)"));
            Assert.That(
                source,
                Does.Contain("CharacterActionAnimation.RollForward"));
            Assert.That(
                source,
                Does.Contain("NotifyServerOfActionAnimationServerRpc"));
        }

        [Test]
        public void BackwardEvasionDirectionIsHorizontalAndNormalized()
        {
            Type combatType = GetRuntimeType("ZZ.AICharacterCombatManager");
            MethodInfo method = combatType.GetMethod(
                "GetBackwardEvasionDirection",
                BindingFlags.NonPublic | BindingFlags.Static);
            Vector3 direction = (Vector3)method.Invoke(
                null,
                new object[] { new Vector3(2f, 4f, 0f) });

            Assert.That(direction.y, Is.EqualTo(0f));
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(direction.x, Is.LessThan(0f));
        }

        [Test]
        public void AnimatorContainsNetworkCompatibleRollState()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    k_AIControllerPath);
            AnimatorStateMachine actionLayer = controller.layers
                .Single(layer => layer.name == "Action Override")
                .stateMachine;
            AnimatorState emptyState = actionLayer.states
                .Select(childState => childState.state)
                .Single(state => state.name == "Empty");
            AnimatorState rollState = actionLayer.states
                .Select(childState => childState.state)
                .Single(state => state.name == "Roll_Forward_01");

            Assert.That(
                rollState.motion,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    k_RollClipPath)));
            Assert.That(
                rollState.transitions.Any(transition =>
                    transition.destinationState == emptyState &&
                    transition.hasExitTime),
                Is.True);
        }

        [Test]
        public void RootMotionAndActionResetBoundTheInvulnerabilityWindow()
        {
            string animatorSource = ReadRuntimeSource(
                "Character/AI/AICharacterAnimatorManager.cs");
            string managerSource = ReadRuntimeSource(
                "Character/AI/AICharacterManager.cs");
            string combatSource = ReadRuntimeSource(
                "Character/CharacterCombatManager.cs");

            Assert.That(animatorSource, Does.Contain("OnAnimatorMove()"));
            Assert.That(animatorSource, Does.Contain("CharacterAnimator.deltaPosition"));
            Assert.That(managerSource, Does.Contain("m_navMeshAgent.Move(deltaPosition)"));
            Assert.That(combatSource, Does.Contain("SetInvulnerable(false)"));
        }

        [Test]
        public void DefeatedBossCannotBeReactivatedByItsBeacon()
        {
            Type managerType = GetRuntimeType("ZZ.AICharacterManager");
            string managerSource = ReadRuntimeSource(
                "Character/AI/AICharacterManager.cs");
            string bossSource = ReadRuntimeSource(
                "Character/AI/Boss/BossCharacterManager.cs");

            Assert.That(
                managerType.GetMethod("ActivateCharacter").IsVirtual,
                Is.True);
            Assert.That(
                managerType.GetMethod("DeactivateCharacter").IsVirtual,
                Is.True);
            Assert.That(
                managerSource,
                Does.Contain("m_bossCharacter?.HasBeenDefeated == true"));
            Assert.That(bossSource, Does.Contain("HasBossBeenDefeated == true"));
        }

        private static void AssertEvasionConfiguration(
            string prefabPath,
            bool expectedValue)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            Component manager = prefab.GetComponent(
                GetRuntimeType("ZZ.AICharacterManager"));
            Component combatManager = prefab.GetComponent(
                GetRuntimeType("ZZ.AICharacterCombatManager"));
            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedObject serializedCombat =
                new SerializedObject(combatManager);

            Assert.That(
                serializedManager.FindProperty("m_canEvade").boolValue,
                Is.EqualTo(expectedValue));
            Assert.That(
                serializedCombat.FindProperty("m_maximumEvasionDistance")
                    .floatValue,
                Is.EqualTo(5f));
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            return File.ReadAllText($"Assets/Script/{relativePath}");
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
