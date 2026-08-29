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
    public class AICombatDecisionSystemTests
    {
        private const string k_AIControllerPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Undead AI Animator.controller";
        private const string k_UndeadPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_UndeadAttack01Path =
            "Assets/_Game/Data/AI/Combat/Undead Swipe 01.asset";
        private const string k_UndeadAttack02Path =
            "Assets/_Game/Data/AI/Combat/Undead Swipe 02.asset";
        private const string k_WatcherClawPath =
            "Assets/_Game/Data/AI/Boss/Fallen Watcher/Watcher Claw.asset";
        private const string k_WatcherFrenzyPath =
            "Assets/_Game/Data/AI/Boss/Fallen Watcher/Watcher Frenzy.asset";

        private static readonly string[] s_comboClipPaths =
        {
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Combat/General/" +
                "zombie_light_attack_01.anim",
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Combat/General/" +
                "zombie_swipe_attack_01.anim",
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Combat/General/" +
                "zombie_swipe_attack_02.anim"
        };

        [Test]
        public void CombatRollUsesSingleDeterministicPercentageAndDirectionBoundary()
        {
            Type stateType = GetRuntimeType("ZZ.CombatStanceAIState");
            MethodInfo rollMethod = stateType.GetMethod(
                "RollForOutcomeChance",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo directionMethod = stateType.GetMethod(
                "SelectStrafeAmount",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(rollMethod.Invoke(null, new object[] { 0f, 0 }), Is.False);
            Assert.That(rollMethod.Invoke(null, new object[] { 100f, 99 }), Is.True);
            Assert.That(rollMethod.Invoke(null, new object[] { 75f, 74 }), Is.True);
            Assert.That(rollMethod.Invoke(null, new object[] { 75f, 75 }), Is.False);
            Assert.That(
                directionMethod.Invoke(null, new object[] { 50, 0.5f }),
                Is.EqualTo(-0.5f));
            Assert.That(
                directionMethod.Invoke(null, new object[] { 49, 0.5f }),
                Is.EqualTo(0.5f));
        }

        [Test]
        public void AttackAssetsContainAuthoredComboLinksAndRanges()
        {
            Type attackActionType = GetRuntimeType(
                "ZZ.AICharacterAttackAction");
            Type bossAttackType = GetRuntimeType("ZZ.BossAttackData");
            UnityEngine.Object initial = AssetDatabase.LoadAssetAtPath(
                k_UndeadAttack01Path,
                attackActionType);
            UnityEngine.Object followUp = AssetDatabase.LoadAssetAtPath(
                k_UndeadAttack02Path,
                attackActionType);
            UnityEngine.Object claw = AssetDatabase.LoadAssetAtPath(
                k_WatcherClawPath,
                bossAttackType);
            UnityEngine.Object frenzy = AssetDatabase.LoadAssetAtPath(
                k_WatcherFrenzyPath,
                bossAttackType);
            PropertyInfo comboProperty = attackActionType.GetProperty(
                "ComboAction");
            MethodInfo rangeMethod = attackActionType.GetMethod("IsInRange");

            Assert.That(comboProperty.GetValue(initial), Is.EqualTo(followUp));
            Assert.That(comboProperty.GetValue(claw), Is.EqualTo(frenzy));
            Assert.That(rangeMethod.Invoke(initial, new object[] { 2.1f }), Is.True);
            Assert.That(rangeMethod.Invoke(initial, new object[] { 2.11f }), Is.False);
            Assert.That(
                Convert.ToInt32(
                    attackActionType.GetProperty("AttackType").GetValue(followUp)),
                Is.EqualTo(3));
        }

        [Test]
        public void LegacyUndeadDefaultsOffWhileBossEnablesCombatDecisions()
        {
            AssertCombatDecisionConfiguration(k_UndeadPrefabPath, false);
            AssertCombatDecisionConfiguration(k_BossPrefabPath, true);
        }

        [Test]
        public void AnimatorContainsDirectionalLocomotionAndBlockingBlendTrees()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    k_AIControllerPath);
            AnimatorState[] states = controller.layers[0].stateMachine.states
                .Select(childState => childState.state)
                .ToArray();
            AnimatorState locomotion = states.Single(state =>
                state.name == "Locomotion");
            AnimatorState blocking = states.Single(state =>
                state.name == "Blocking");
            BlendTree locomotionTree = locomotion.motion as BlendTree;
            BlendTree blockingTree = blocking.motion as BlendTree;

            Assert.That(
                locomotionTree?.blendType,
                Is.EqualTo(BlendTreeType.FreeformCartesian2D));
            Assert.That(locomotionTree?.children.Length, Is.GreaterThanOrEqualTo(4));
            Assert.That(
                blockingTree?.blendType,
                Is.EqualTo(BlendTreeType.FreeformCartesian2D));
            Assert.That(blockingTree?.children.Length, Is.GreaterThanOrEqualTo(4));
            Assert.That(
                HasBlockingTransition(
                    locomotion,
                    blocking,
                    AnimatorConditionMode.If),
                Is.True);
            Assert.That(
                HasBlockingTransition(
                    blocking,
                    locomotion,
                    AnimatorConditionMode.IfNot),
                Is.True);
        }

        [Test]
        public void ComboClipsContainOrderedOpenAndCloseEvents()
        {
            foreach (string clipPath in s_comboClipPaths)
            {
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath));
                AnimationEvent enableEvent = events
                    .Where(animationEvent =>
                        animationEvent.functionName == "EnableCanDoCombo")
                    .OrderBy(animationEvent => animationEvent.time)
                    .FirstOrDefault();
                AnimationEvent disableEvent = events.SingleOrDefault(
                    animationEvent =>
                        animationEvent.functionName == "DisableCanDoCombo");

                Assert.That(enableEvent, Is.Not.Null, clipPath);
                Assert.That(disableEvent, Is.Not.Null, clipPath);
                Assert.That(disableEvent.time, Is.GreaterThan(enableEvent.time));
            }
        }

        [Test]
        public void AIPrefabsContainBlockingResourcesAndFixedImpactSounds()
        {
            foreach (string prefabPath in new[]
                {
                    k_UndeadPrefabPath,
                    k_BossPrefabPath
                })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);
                Component stats = prefab.GetComponent(
                    GetRuntimeType("ZZ.CharacterStatsManager"));
                SerializedObject serializedStats = new SerializedObject(stats);
                Component soundManager = prefab.GetComponentInChildren(
                    GetRuntimeType("ZZ.AICharacterSoundFXManager"),
                    true);
                SerializedObject serializedSound =
                    new SerializedObject(soundManager);

                Assert.That(
                    serializedStats
                        .FindProperty("m_blockingPhysicalAbsorption")
                        .floatValue,
                    Is.GreaterThan(0f));
                Assert.That(
                    serializedStats.FindProperty("m_blockingStability").floatValue,
                    Is.GreaterThan(0f));
                Assert.That(
                    serializedSound.FindProperty("m_blockingSoundEffects").arraySize,
                    Is.GreaterThanOrEqualTo(3));
            }

            GameObject statsObject = new GameObject("AI Stats Test");
            try
            {
                Component stats = statsObject.AddComponent(
                    GetRuntimeType("ZZ.CharacterStatsManager"));
                Assert.That(
                    stats.GetType()
                        .GetMethod("CalculateStaminaBasedOnEnduranceLevel")
                        .Invoke(stats, new object[] { 10 }),
                    Is.EqualTo(100f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(statsObject);
            }
        }

        [Test]
        public void ComboExecutionChecksWindowBeforeActionCompletionReturn()
        {
            string attackStateSource = ReadRuntimeSource(
                "Character/AI/States/AttackAIState.cs");
            string combatSource = ReadRuntimeSource(
                "Character/AI/AICharacterCombatManager.cs");
            string colliderSource = ReadRuntimeSource(
                "Character/AI/AIDamageCollider.cs");

            Assert.That(
                attackStateSource.IndexOf(
                    "PerformCombo(character);",
                    StringComparison.Ordinal),
                Is.LessThan(attackStateSource.IndexOf(
                    "!character.IsPerformingAction",
                    StringComparison.Ordinal)));
            Assert.That(combatSource, Does.Contain("m_canPerformCombo = true"));
            Assert.That(combatSource, Does.Contain(
                "m_hasHitTargetDuringCombo = false"));
            Assert.That(colliderSource, Does.Contain("RecordSuccessfulHit(target)"));
        }

        [Test]
        public void SpawnerExposesOptionalPerInstanceHealthAndStamina()
        {
            Type spawnerType = GetRuntimeType("ZZ.AICharacterSpawner");
            const BindingFlags k_PrivateInstance =
                BindingFlags.NonPublic | BindingFlags.Instance;

            Assert.That(
                spawnerType.GetField("m_manuallySetStats", k_PrivateInstance),
                Is.Not.Null);
            Assert.That(
                spawnerType.GetField("m_maximumHealth", k_PrivateInstance),
                Is.Not.Null);
            Assert.That(
                spawnerType.GetField("m_maximumStamina", k_PrivateInstance),
                Is.Not.Null);
        }

        private static void AssertCombatDecisionConfiguration(
            string prefabPath,
            bool expectedValue)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Component manager = prefab.GetComponent(
                GetRuntimeType("ZZ.AICharacterManager"));
            SerializedObject serializedManager = new SerializedObject(manager);

            Assert.That(
                serializedManager.FindProperty("m_willCircleTarget").boolValue,
                Is.EqualTo(expectedValue));
            Assert.That(
                serializedManager.FindProperty("m_canBlock").boolValue,
                Is.EqualTo(expectedValue));
            Assert.That(
                serializedManager.FindProperty("m_canPerformCombo").boolValue,
                Is.EqualTo(expectedValue));
        }

        private static bool HasBlockingTransition(
            AnimatorState source,
            AnimatorState destination,
            AnimatorConditionMode mode)
        {
            return source.transitions.Any(transition =>
                transition.destinationState == destination &&
                transition.conditions.Any(condition =>
                    condition.mode == mode &&
                    condition.parameter == "isBlocking"));
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            relativePath = RemapRuntimeSourcePath(relativePath);
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
        }
        /// <summary>Maps a pre-refactor Script-relative path to the new layout.</summary>
        private static string RemapRuntimeSourcePath(string relativePath)
        {
            if (relativePath.StartsWith("Character/Player/Player UI/"))
                return "Characters/Player/Player UI/" + relativePath.Substring("Character/Player/Player UI/".Length);
            if (relativePath.StartsWith("Character/Player/"))
                return "Characters/Player/" + relativePath.Substring("Character/Player/".Length);
            if (relativePath.StartsWith("Character/AI/"))
                return "Characters/AI/" + relativePath.Substring("Character/AI/".Length);
            if (relativePath.StartsWith("Character/Effects/"))
                return "Characters/Common/Effects/" + relativePath.Substring("Character/Effects/".Length);
            if (relativePath.StartsWith("Character/Equipment/"))
                return "Characters/Common/Equipment/" + relativePath.Substring("Character/Equipment/".Length);
            if (relativePath.StartsWith("Character/Inventory/"))
                return "Characters/Common/Inventory/" + relativePath.Substring("Character/Inventory/".Length);
            if (relativePath.StartsWith("Character/Character UI/"))
                return "Characters/Common/Character UI/" + relativePath.Substring("Character/Character UI/".Length);
            if (relativePath.StartsWith("Character/Animation State Behaviors/"))
                return "Characters/Common/Animation State Behaviors/" + relativePath.Substring("Character/Animation State Behaviors/".Length);
            if (relativePath.StartsWith("Character/"))
                return "Characters/Common/" + relativePath.Substring("Character/".Length);
            if (relativePath.StartsWith("World Managers/AI/"))
                return "World/Managers/AI/" + relativePath.Substring("World Managers/AI/".Length);
            if (relativePath.StartsWith("World Managers/"))
                return "World/Managers/" + relativePath.Substring("World Managers/".Length);
            if (relativePath.StartsWith("World Objects/"))
                return "World/Objects/" + relativePath.Substring("World Objects/".Length);
            if (relativePath.StartsWith("Save System/"))
                return "Save/" + relativePath.Substring("Save System/".Length);
            if (relativePath.StartsWith("Menu Scene/"))
                return "UI/Frontend/" + relativePath.Substring("Menu Scene/".Length);
            if (relativePath.StartsWith("Effects/"))
                return "Combat/Effects/" + relativePath.Substring("Effects/".Length);
            if (relativePath.StartsWith("Damage/"))
                return "Combat/Damage/" + relativePath.Substring("Damage/".Length);
            if (relativePath.StartsWith("Actions/"))
                return "Combat/Actions/" + relativePath.Substring("Actions/".Length);
            if (relativePath.StartsWith("Projectiles/"))
                return "Combat/Projectiles/" + relativePath.Substring("Projectiles/".Length);
            if (relativePath.StartsWith("Spells/"))
                return "Abilities/Spells/" + relativePath.Substring("Spells/".Length);
            if (relativePath.StartsWith("Utility/"))
                return "Utilities/" + relativePath.Substring("Utility/".Length);
            return relativePath;
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
