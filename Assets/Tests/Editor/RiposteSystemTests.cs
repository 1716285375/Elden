using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public class RiposteSystemTests
    {
        private const string k_AIControllerPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Undead AI Animator.controller";
        private const string k_RipostedClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
            "core_main_riposte_victim_01.anim";

        [Test]
        public void CriticalAngleAcceptsFrontAndRejectsSide()
        {
            Type combatType = GetRuntimeType("ZZ.CharacterCombatManager");
            MethodInfo angleMethod = combatType.GetMethod(
                "IsWithinCriticalAttackAngle",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(angleMethod, Is.Not.Null);
            Assert.That(angleMethod.Invoke(
                null,
                new object[] { Vector3.forward, Vector3.forward, 60f }),
                Is.True);
            Assert.That(angleMethod.Invoke(
                null,
                new object[] { Vector3.forward, Vector3.right, 60f }),
                Is.False);
        }

        [Test]
        public void RiposteRpcPreservesOrderedDamagePayload()
        {
            Type networkType = GetRuntimeType("ZZ.CharacterNetworkManager");
            MethodInfo rpc = networkType.GetMethod(
                "NotifyServerOfRiposteServerRpc",
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
        public void CriticalDamageUsesDelayedPendingContract()
        {
            Type combatType = GetRuntimeType("ZZ.CharacterCombatManager");
            Type criticalEffectType = GetRuntimeType("ZZ.TakeCriticalDamageEffect");

            Assert.That(combatType.GetProperty("PendingCriticalDamage"),
                Is.Not.Null);
            Assert.That(combatType.GetMethod("SetPendingCriticalDamage"),
                Is.Not.Null);
            Assert.That(combatType.GetMethod("ApplyCriticalDamage"),
                Is.Not.Null);
            Assert.That(criticalEffectType.BaseType?.Name,
                Is.EqualTo("TakeDamageEffect"));
        }

        [Test]
        public void RipostedStateOnlyGetsUpWhileAlive()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    k_AIControllerPath);
            AnimatorControllerLayer actionLayer = controller.layers.Single(layer =>
                layer.name == "Action Override");
            AnimatorState ripostedState = actionLayer.stateMachine.states
                .Select(childState => childState.state)
                .Single(state => state.name == "Riposted_01");
            AnimatorStateTransition getUpTransition =
                ripostedState.transitions.Single(transition =>
                    transition.destinationState.name ==
                    "Riposted_Get_Up_01");

            Assert.That(getUpTransition.destinationState.name,
                Is.EqualTo("Riposted_Get_Up_01"));
            Assert.That(getUpTransition.conditions, Has.Length.EqualTo(1));
            Assert.That(getUpTransition.conditions[0].parameter,
                Is.EqualTo("isDead"));
            Assert.That(getUpTransition.conditions[0].mode,
                Is.EqualTo(AnimatorConditionMode.IfNot));
        }

        [Test]
        public void VictimAnimationSettlesCriticalDamageExactlyOnce()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                k_RipostedClipPath);
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);

            Assert.That(events, Has.Length.EqualTo(1));
            Assert.That(events[0].functionName,
                Is.EqualTo("ApplyCriticalDamage"));
            Assert.That(events[0].time, Is.GreaterThan(0f));
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve {fullName}.");
            return type;
        }
    }
}
