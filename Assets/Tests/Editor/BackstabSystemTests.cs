using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public class BackstabSystemTests
    {
        private const string k_AIControllerPath =
            "Assets/Data/Animations/AI/Undead AI Animator.controller";
        private const string k_BackstabbedClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
            "core_main_backstab_victim_01.anim";
        private const string k_UndeadPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";

        [Test]
        public void CriticalConesSeparateFrontFromRear()
        {
            Type combatType = GetRuntimeType("ZZ.CharacterCombatManager");
            MethodInfo frontMethod = combatType.GetMethod(
                "IsWithinCriticalAttackAngle",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo rearMethod = combatType.GetMethod(
                "IsWithinBackstabAngle",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(frontMethod, Is.Not.Null);
            Assert.That(rearMethod, Is.Not.Null);
            Assert.That(frontMethod.Invoke(
                null,
                new object[] { Vector3.forward, Vector3.forward, 60f }),
                Is.True);
            Assert.That(rearMethod.Invoke(
                null,
                new object[] { Vector3.forward, Vector3.forward, 145f }),
                Is.False);
            Assert.That(frontMethod.Invoke(
                null,
                new object[] { Vector3.forward, Vector3.back, 60f }),
                Is.False);
            Assert.That(rearMethod.Invoke(
                null,
                new object[] { Vector3.forward, Vector3.back, 145f }),
                Is.True);
        }

        [Test]
        public void BackstabRpcPreservesOrderedDamagePayload()
        {
            Type networkType = GetRuntimeType("ZZ.CharacterNetworkManager");
            MethodInfo rpc = networkType.GetMethod(
                "NotifyTheServerOfBackstabServerRpc",
                BindingFlags.Public | BindingFlags.Instance);
            string[] parameterNames = rpc?.GetParameters()
                .Take(10)
                .Select(parameter => parameter.Name)
                .ToArray();

            Assert.That(parameterNames, Is.EqualTo(new[]
            {
                "targetNetworkObjectId",
                "attackerNetworkObjectId",
                "weaponID",
                "criticalDamageAnimation",
                "physicalDamage",
                "magicDamage",
                "fireDamage",
                "lightningDamage",
                "holyDamage",
                "poiseDamage"
            }));
        }

        [Test]
        public void BackstabbedStateBranchesOnDeath()
        {
            AnimatorStateMachine stateMachine = GetActionStateMachine();
            AnimatorState backstabbedState = GetState(
                stateMachine,
                "Backstabbed_01");

            AssertConditionalDestination(
                backstabbedState,
                "Backstabbed_Get_Up_01",
                AnimatorConditionMode.IfNot);
            AssertConditionalDestination(
                backstabbedState,
                "Backstab_Critical_Death_01",
                AnimatorConditionMode.If);
        }

        [Test]
        public void RipostedStateHasCriticalDeathBranch()
        {
            AnimatorState ripostedState = GetState(
                GetActionStateMachine(),
                "Riposted_01");

            AssertConditionalDestination(
                ripostedState,
                "Riposte_Critical_Death_01",
                AnimatorConditionMode.If);
        }

        [Test]
        public void BackstabbedAnimationSettlesDamageExactlyOnce()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                k_BackstabbedClipPath);
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);

            Assert.That(events, Has.Length.EqualTo(1));
            Assert.That(events[0].functionName,
                Is.EqualTo("ApplyCriticalDamage"));
            Assert.That(events[0].time, Is.GreaterThan(0f));
        }

        [Test]
        public void StandardEnemyAllowsBackstabAndBossRejectsIt()
        {
            Assert.That(ReadBackstabPolicy(k_UndeadPrefabPath), Is.True);
            Assert.That(ReadBackstabPolicy(k_BossPrefabPath), Is.False);
        }

        private static AnimatorStateMachine GetActionStateMachine()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    k_AIControllerPath);
            Assert.That(controller, Is.Not.Null);
            return controller.layers.Single(layer =>
                layer.name == "Action Override").stateMachine;
        }

        private static AnimatorState GetState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .Single(state => state.name == stateName);
        }

        private static void AssertConditionalDestination(
            AnimatorState source,
            string destinationName,
            AnimatorConditionMode mode)
        {
            AnimatorStateTransition transition = source.transitions.Single(
                candidate => candidate.destinationState.name == destinationName);
            Assert.That(transition.conditions, Has.Length.EqualTo(1));
            Assert.That(transition.conditions[0].parameter,
                Is.EqualTo("isDead"));
            Assert.That(transition.conditions[0].mode, Is.EqualTo(mode));
        }

        private static bool ReadBackstabPolicy(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            Assert.That(prefab, Is.Not.Null);
            Component combatManager = prefab.GetComponentsInChildren<Component>(
                    true)
                .FirstOrDefault(component =>
                    component != null &&
                    component.GetType().Name == "AICharacterCombatManager");
            Assert.That(combatManager, Is.Not.Null);
            SerializedProperty property = new SerializedObject(combatManager)
                .FindProperty("m_canBeBackstabbed");
            Assert.That(property, Is.Not.Null);
            return property.boolValue;
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve {fullName}.");
            return type;
        }
    }
}
