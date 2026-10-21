using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class AIRangerCombatSystemTests
    {
        private const string k_RangerPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Ranger/Ranger AI.prefab";
        private const string k_RangerActionPath =
            "Assets/_Game/Data/Actions/AI/Ranger/Ranger_Attack_01.asset";
        private const string k_BowDrawClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/" +
            "Bow_Draw.anim";
        private const string k_BowFireClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/" +
            "Bow_Fire.anim";
        private const string k_NetworkPrefabsPath =
            "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset";

        [Test]
        public void RangedStateAndAnimationEventsLiveOnCharacterBaseTypes()
        {
            Type networkType = GetRuntimeType("CharacterNetworkManager");
            Type combatType = GetRuntimeType("CharacterCombatManager");
            Type animatorType = GetRuntimeType("CharacterAnimatorManager");
            Type playerNetworkType = GetRuntimeType("PlayerNetworkManager");

            Assert.That(networkType.GetProperty("HasArrowNotched"), Is.Not.Null);
            Assert.That(networkType.GetProperty("IsHoldingArrow"), Is.Not.Null);
            Assert.That(combatType.GetMethod("DrawProjectile")?.IsVirtual, Is.True);
            Assert.That(combatType.GetMethod("ReleaseArrow")?.IsVirtual, Is.True);
            Assert.That(animatorType.GetMethod("DrawProjectile"), Is.Not.Null);
            Assert.That(animatorType.GetMethod("ReleaseArrow"), Is.Not.Null);
            Assert.That(
                playerNetworkType.GetProperty("HasArrowNotched")?.DeclaringType,
                Is.EqualTo(networkType));
        }

        [Test]
        public void RangerPrefabUsesSpecializedComponentsAndNetworkObject()
        {
            Type rangerType = GetRuntimeType("AIRangerManager");
            Type rangerCombatType = GetRuntimeType("AIRangerCombatManager");
            Type equipmentType = GetRuntimeType("AIRangerEquipmentManager");
            Type baseManagerType = GetRuntimeType("AICharacterManager");
            Type baseCombatType = GetRuntimeType("AICharacterCombatManager");
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_RangerPrefabPath);
            try
            {
                Component equipment = root.GetComponent(equipmentType);
                SerializedObject serializedEquipment = new(equipment);

                Assert.That(root.GetComponent(rangerType), Is.Not.Null);
                Assert.That(root.GetComponent(rangerCombatType), Is.Not.Null);
                Assert.That(equipment, Is.Not.Null);
                Assert.That(root.GetComponent<NetworkObject>(), Is.Not.Null);
                Assert.That(
                    root.GetComponents(baseManagerType)
                        .Count(component => component.GetType() == baseManagerType),
                    Is.Zero);
                Assert.That(
                    root.GetComponents(baseCombatType)
                        .Count(component => component.GetType() == baseCombatType),
                    Is.Zero);
                Assert.That(
                    serializedEquipment.FindProperty("m_bow")
                        .objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    serializedEquipment.FindProperty("m_projectile")
                        .objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    (serializedEquipment.FindProperty("m_drawHand")
                        .objectReferenceValue as Transform)?.name,
                    Is.EqualTo("Arrow Instantiation Slot"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void RangerAttackAndAnimatorEventsDriveDrawAimAndFire()
        {
            UnityEngine.Object attack = AssetDatabase.LoadMainAssetAtPath(
                k_RangerActionPath);
            SerializedObject serializedAttack = new(attack);
            AnimationEvent[] drawEvents = AnimationUtility.GetAnimationEvents(
                AssetDatabase.LoadAssetAtPath<AnimationClip>(k_BowDrawClipPath));
            AnimationEvent[] fireEvents = AnimationUtility.GetAnimationEvents(
                AssetDatabase.LoadAssetAtPath<AnimationClip>(k_BowFireClipPath));

            Assert.That(GetProperty<float>(attack, "MinimumRange"), Is.EqualTo(1f));
            Assert.That(GetProperty<float>(attack, "MaximumRange"), Is.EqualTo(20f));
            Assert.That(GetProperty<float>(attack, "RecoveryTime"), Is.EqualTo(2f));
            Assert.That(
                GetProperty<bool>(attack, "UseCharacterActionAnimation"),
                Is.True);
            Assert.That(
                GetProperty<object>(attack, "CharacterActionAnimation").ToString(),
                Is.EqualTo("BowDraw"));
            Assert.That(
                serializedAttack.FindProperty("m_isParryable").boolValue,
                Is.False);
            Assert.That(
                drawEvents.Count(animationEvent =>
                    animationEvent.functionName == "DrawProjectile"),
                Is.EqualTo(1));
            Assert.That(
                fireEvents.Count(animationEvent =>
                    animationEvent.functionName == "ReleaseArrow"),
                Is.EqualTo(1));
        }

        [Test]
        public void AimDurationFacingAndMinimumDistanceAreDeterministicAtEdges()
        {
            Type combatType = GetRuntimeType("AIRangerCombatManager");
            MethodInfo aimDuration = combatType.GetMethod("SelectAimDuration");
            MethodInfo canFire = combatType.GetMethod("CanFireAtTarget");

            Assert.That(
                aimDuration.Invoke(null, new object[] { 1f, 4f, 0f }),
                Is.EqualTo(1f));
            Assert.That(
                aimDuration.Invoke(null, new object[] { 1f, 4f, 1f }),
                Is.EqualTo(4f));
            Assert.That(
                CanFire(canFire, Vector3.forward, Vector3.forward, 10f),
                Is.True);
            Assert.That(
                CanFire(canFire, Vector3.forward, Vector3.back, 10f),
                Is.False);
            Assert.That(
                CanFire(canFire, Vector3.forward, Vector3.forward, 1f),
                Is.False);
        }

        [Test]
        public void EngagementRangeSkipsNullAndUsesSeventyFivePercentHysteresis()
        {
            Type managerType = GetRuntimeType("AICharacterManager");
            Type attackType = GetRuntimeType("AICharacterAttackAction");
            UnityEngine.Object attack = AssetDatabase.LoadMainAssetAtPath(
                k_RangerActionPath);
            Array attacks = Array.CreateInstance(attackType, 2);
            attacks.SetValue(null, 0);
            attacks.SetValue(attack, 1);
            MethodInfo maximumRange = managerType.GetMethod(
                "GetMaximumAttackRange",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo pursuitDistance = managerType.GetMethod(
                "GetMinimumPursuitDistance",
                BindingFlags.NonPublic | BindingFlags.Static);
            float maximum = (float)maximumRange.Invoke(
                null,
                new object[] { attacks, 3f });
            float minimum = (float)pursuitDistance.Invoke(
                null,
                new object[] { maximum, 0.75f });

            Assert.That(maximum, Is.EqualTo(20f));
            Assert.That(minimum, Is.EqualTo(15f));
            Assert.That(
                File.ReadAllText(
                    "Assets/_Game/Scripts/Characters/AI/States/" +
                    "PursueTargetAIState.cs"),
                Does.Contain("CanEndPursuit"));
            Assert.That(
                File.ReadAllText(
                    "Assets/_Game/Scripts/Characters/AI/States/" +
                    "CombatStanceAIState.cs"),
                Does.Contain("ShouldResumePursuit"));
        }

        [Test]
        public void PursuitModesMapToMovementAndNoneStopsNavigation()
        {
            Type managerType = GetRuntimeType("AICharacterManager");
            Type pursuitModeType = GetRuntimeType("PursuitMode");
            MethodInfo movement = managerType.GetMethod(
                "GetMovementAnimationValue",
                BindingFlags.NonPublic | BindingFlags.Static);

            AssertMovement(movement, pursuitModeType, "None", 0f);
            AssertMovement(movement, pursuitModeType, "Walk", 0.5f);
            AssertMovement(movement, pursuitModeType, "Run", 1f);
            AssertMovement(movement, pursuitModeType, "Sprint", 2f);
            Assert.That(
                File.ReadAllText(
                    "Assets/_Game/Scripts/Characters/AI/AICharacterManager.cs"),
                Does.Contain(
                    "m_navMeshAgent.SetDestination(transform.position)"));
        }

        [Test]
        public void RangerProjectileIsReplicatedAndDoesNotConsumeAmmo()
        {
            string rangerSource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/AI/Ranger/" +
                "AIRangerCombatManager.cs");
            Type networkType = GetRuntimeType("AICharacterNetworkManager");
            NetworkPrefabsList prefabs =
                AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                    k_NetworkPrefabsPath);
            GameObject rangerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_RangerPrefabPath);

            Assert.That(
                networkType.GetMethod("ReplicateRangedProjectile"),
                Is.Not.Null);
            Assert.That(prefabs.Contains(rangerPrefab), Is.True);
            Assert.That(rangerSource, Does.Contain("target == null"));
            Assert.That(rangerSource, Does.Not.Contain("TryConsumeAmmo"));
            Assert.That(
                rangerSource,
                Does.Not.Contain("NotifyProjectileAmountChanged"));
        }

        private static bool CanFire(
            MethodInfo method,
            Vector3 forward,
            Vector3 direction,
            float distance)
        {
            return (bool)method.Invoke(
                null,
                new object[] { forward, direction, distance, 1f, 35f });
        }

        private static void AssertMovement(
            MethodInfo method,
            Type enumType,
            string enumName,
            float expected)
        {
            object mode = Enum.Parse(enumType, enumName);
            Assert.That(method.Invoke(null, new[] { mode }), Is.EqualTo(expected));
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName)?.GetValue(target);
        }

        private static Type GetRuntimeType(string typeName)
        {
            return Type.GetType($"ZZ.{typeName}, Assembly-CSharp", true);
        }
    }
}
