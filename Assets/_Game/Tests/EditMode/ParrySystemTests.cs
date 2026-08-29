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
    public class ParrySystemTests
    {
        private const string k_ParryAssetPath =
            "Assets/_Game/Data/Items/Ashes Of War/Parry Slow.asset";
        private const string k_MediumShieldPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Medium Shield.asset";
        private const string k_ItemDatabasePath =
            "Assets/_Game/Prefabs/World/Managers/World Item Database.prefab";
        private const string k_AIControllerPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Undead AI Animator.controller";
        private const string k_ParriedClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/General/" +
            "core_main_parry_victim_01.anim";

        private static readonly string[] s_parryClipPaths =
        {
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Shield/" +
                "shield_off_parry_01_fast_start.anim",
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Shield/" +
                "shield_off_parry_01_start.anim",
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Shield/" +
                "shield_off_parry_01_slow_start.anim"
        };

        [Test]
        public void LTBindsGamepadAndKeyboard()
        {
            string json = File.ReadAllText("Assets/_Game/Settings/Input/PlayerControls.inputactions");

            StringAssert.Contains("\"name\": \"LT\"", json);
            StringAssert.Contains("<Gamepad>/leftTrigger", json);
            StringAssert.Contains("<Keyboard>/c", json);
        }

        [Test]
        public void ParryAshIsRegisteredAndEquippedOnShield()
        {
            ScriptableObject parry = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                k_ParryAssetPath);
            ScriptableObject shield = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                k_MediumShieldPath);
            GameObject databasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_ItemDatabasePath);

            Assert.That(parry, Is.Not.Null);
            Assert.That(shield, Is.Not.Null);
            Assert.That(databasePrefab, Is.Not.Null);
            Assert.That(
                new SerializedObject(shield)
                    .FindProperty("m_ashOfWarAction")
                    .objectReferenceValue,
                Is.EqualTo(parry));
            Component database = databasePrefab.GetComponents<Component>()
                .Single(component => component.GetType().Name ==
                    "WorldItemDatabase");
            SerializedProperty ashes = new SerializedObject(database)
                .FindProperty("m_ashesOfWar");
            Assert.That(ashes.arraySize, Is.GreaterThanOrEqualTo(1));
            Assert.That(Enumerable.Range(0, ashes.arraySize).Any(index =>
                ashes.GetArrayElementAtIndex(index).objectReferenceValue == parry),
                Is.True);
        }

        [Test]
        public void ParryRpcAllowsServerValidatedNonOwnerRequest()
        {
            Type networkType = GetRuntimeType("ZZ.CharacterNetworkManager");
            MethodInfo rpc = networkType.GetMethod(
                "NotifyServerOfParryServerRpc",
                BindingFlags.Public | BindingFlags.Instance);
            object attribute = rpc?.GetCustomAttributes(false)
                .Single(candidate =>
                    candidate.GetType().Name == "ServerRpcAttribute");
            FieldInfo requireOwnership = attribute?.GetType()
                .GetField("RequireOwnership");

            Assert.That(rpc, Is.Not.Null);
            Assert.That(requireOwnership?.GetValue(attribute), Is.False);
            Assert.That(networkType.GetField("IsParrying"), Is.Not.Null);
            Assert.That(networkType.GetField("IsParryable"), Is.Not.Null);
        }

        [Test]
        public void EveryConcreteMeleeColliderOverridesParryBoundary()
        {
            Type damageCollider = GetRuntimeType("ZZ.DamageCollider");
            Type meleeCollider = GetRuntimeType("ZZ.MeleeWeaponDamageCollider");
            Type aiCollider = GetRuntimeType("ZZ.AIDamageCollider");

            Assert.That(damageCollider.GetMethod(
                "CheckForParry",
                BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
            Assert.That(meleeCollider.GetMethod(
                "CheckForParry",
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
                Is.Not.Null);
            Assert.That(aiCollider.GetMethod(
                "CheckForParry",
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
                Is.Not.Null);
        }

        [Test]
        public void ParryClipsHaveFiniteOrderedWindows()
        {
            foreach (string clipPath in s_parryClipPaths)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    clipPath);
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);

                Assert.That(events, Has.Length.EqualTo(2), clipPath);
                Assert.That(events[0].functionName,
                    Is.EqualTo("EnableIsParrying"));
                Assert.That(events[1].functionName,
                    Is.EqualTo("DisableIsParrying"));
                Assert.That(events[0].time, Is.LessThan(events[1].time));
                Assert.That(events[1].time, Is.LessThanOrEqualTo(clip.length));
            }
        }

        [Test]
        public void ParriedAnimationOpensRiposteExactlyOnce()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                k_ParriedClipPath);
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);

            Assert.That(events, Has.Length.EqualTo(1));
            Assert.That(events[0].functionName,
                Is.EqualTo("EnableIsRipostable"));
        }

        [Test]
        public void AIAnimatorContainsParriedRecoveryState()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    k_AIControllerPath);
            AnimatorControllerLayer actionLayer = controller.layers.Single(layer =>
                layer.name == "Action Override");
            AnimatorState parriedState = actionLayer.stateMachine.states
                .Select(childState => childState.state)
                .Single(state => state.name == "Parried_01");

            Assert.That(parriedState.motion.name,
                Is.EqualTo("core_main_parry_victim_01"));
            Assert.That(parriedState.transitions.Any(transition =>
                transition.destinationState.name == "Empty"), Is.True);
        }

        [Test]
        public void BossSweepRejectsParryWhileClawAcceptsIt()
        {
            Assert.That(ReadParryable("Watcher Claw.asset"), Is.True);
            Assert.That(ReadParryable("Watcher Sweep.asset"), Is.False);
        }

        [Test]
        public void StandardUndeadAttackAcceptsParry()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab");
            Component combatManager = prefab.GetComponents<Component>()
                .Single(component => component.GetType().Name ==
                    "AICharacterCombatManager");
            SerializedProperty property = new SerializedObject(combatManager)
                .FindProperty("m_defaultAttackIsParryable");

            Assert.That(property.boolValue, Is.True);
        }

        private static bool ReadParryable(string fileName)
        {
            ScriptableObject attack = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                $"Assets/_Game/Data/AI/Boss/Fallen Watcher/{fileName}");
            Assert.That(attack, Is.Not.Null);
            return new SerializedObject(attack)
                .FindProperty("m_isParryable")
                .boolValue;
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve {fullName}.");
            return type;
        }
    }
}
