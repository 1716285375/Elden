using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace ZZ.Tests
{
    public class AIEnvironmentalBehaviorTests
    {
        private const int k_PatrolPathID = 10801;
        private const string k_AIAnimatorControllerPath =
            "Assets/Data/Animations/AI/Undead AI Animator.controller";
        private const string k_AICharacterPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World AI Manager.prefab";

        [Test]
        public void PatrolPathCollectsHierarchyOrderAndFindsClosestPoint()
        {
            GameObject routeObject = new GameObject("Patrol Test Route");
            try
            {
                Type patrolPathType = GetRuntimeType("ZZ.AIPatrolPath");
                Component route = routeObject.AddComponent(patrolPathType);
                CreatePoint(route.transform, "Point 1", Vector3.zero);
                CreatePoint(route.transform, "Point 2", Vector3.right * 5f);
                CreatePoint(route.transform, "Point 3", Vector3.forward * 5f);
                patrolPathType.GetMethod("RefreshPatrolPoints")
                    .Invoke(route, null);
                object patrolPoints = patrolPathType.GetProperty("PatrolPoints")
                    .GetValue(route);
                PropertyInfo countProperty = patrolPoints.GetType()
                    .GetProperty("Count");
                PropertyInfo indexer = patrolPoints.GetType()
                    .GetProperty("Item");

                Assert.That(countProperty.GetValue(patrolPoints), Is.EqualTo(3));
                Assert.That(
                    indexer.GetValue(patrolPoints, new object[] { 0 }),
                    Is.EqualTo(Vector3.zero));
                Assert.That(
                    indexer.GetValue(patrolPoints, new object[] { 1 }),
                    Is.EqualTo(Vector3.right * 5f));
                Assert.That(
                    patrolPathType.GetMethod("GetClosestPatrolPointIndex")
                        .Invoke(
                            route,
                            new object[] { new Vector3(4.5f, 0f, 0f) }),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routeObject);
            }
        }

        [Test]
        public void IdleStateDispatchesPatrolAndSleepWithCombatPriority()
        {
            string source = ReadRuntimeSource(
                "Character/AI/States/IdleAIState.cs");

            Assert.That(source, Does.Contain("character.IdleMode switch"));
            Assert.That(source, Does.Contain("GetClosestPatrolPointIndex"));
            Assert.That(source, Does.Contain("k_PatrolArrivalTolerance = 2f"));
            Assert.That(source, Does.Contain("character.TryAcquireTarget()"));
            Assert.That(source, Does.Contain("character.TimeBetweenPatrols"));
            Assert.That(source, Does.Contain("character.RepeatPatrol"));
            Assert.That(source, Does.Contain("character.WakeFromSleep()"));
            Assert.That(
                ReadRuntimeSource("Character/AI/AICharacterManager.cs"),
                Does.Contain("m_startsSleeping && !IsAwake"));
        }

        [Test]
        public void InvestigationPivotsResolvesReachabilityAndReturnsToIdle()
        {
            string stateSource = ReadRuntimeSource(
                "Character/AI/States/InvestigateSoundAIState.cs");
            string managerSource = ReadRuntimeSource(
                "Character/AI/AICharacterManager.cs");
            string pursueSource = ReadRuntimeSource(
                "Character/AI/States/PursueTargetAIState.cs");

            Assert.That(stateSource, Does.Contain("PivotTowardsPosition"));
            Assert.That(stateSource, Does.Contain("IsDestinationReachable"));
            Assert.That(stateSource, Does.Contain("k_InvestigationTime = 3f"));
            Assert.That(stateSource, Does.Contain("character.TryAcquireTarget()"));
            Assert.That(managerSource, Does.Contain("CalculatePath"));
            Assert.That(managerSource, Does.Contain("k_SoundSampleDistance"));
            Assert.That(managerSource, Does.Contain("NavMesh.SamplePosition"));
            Assert.That(managerSource, Does.Contain("PathComplete"));
            Assert.That(
                stateSource,
                Does.Contain("character.IsPerformingAction && !character.CanMove"));
            Assert.That(
                pursueSource,
                Does.Contain("character.IsPerformingAction && !character.CanMove"));
        }

        [Test]
        public void SoundBroadcastIsServerOnlyAndDeduplicatesCharacterColliders()
        {
            string worldSoundSource = ReadRuntimeSource(
                "World Managers/WorldSoundFXManager.cs");
            string combatSource = ReadRuntimeSource(
                "Character/AI/AICharacterCombatManager.cs");

            Assert.That(worldSoundSource, Does.Contain("!networkManager.IsServer"));
            Assert.That(worldSoundSource, Does.Contain("OverlapSphereNonAlloc"));
            Assert.That(worldSoundSource, Does.Contain("HashSet<AICharacterManager>"));
            Assert.That(worldSoundSource, Does.Contain("m_alertedCharacters.Add"));
            Assert.That(combatSource, Does.Contain("AlertCharacterToSound"));
            Assert.That(combatSource, Does.Contain("WillInvestigateSound"));
            Assert.That(combatSource, Does.Contain("AICharacterStateId.Idle"));
        }

        [Test]
        public void FootstepsArrowsAndFireballsUseSharedSoundStimulus()
        {
            string playerSoundSource = ReadRuntimeSource(
                "Character/Player/PlayerSoundFXManager.cs");
            string arrowSource = ReadRuntimeSource(
                "Projectiles/RangedProjectileManager.cs");
            string spellSource = ReadRuntimeSource("Spells/SpellManager.cs");

            Assert.That(
                playerSoundSource,
                Does.Contain("override void PlayFootstepSoundEffect"));
            Assert.That(playerSoundSource, Does.Contain("k_FootstepSoundRange = 2f"));
            Assert.That(arrowSource, Does.Match(
                @"AlertNearbyCharactersToSound\(\s*contactPoint,\s*3f\)"));
            Assert.That(spellSource, Does.Match(
                @"AlertNearbyCharactersToSound\(\s*contactPoint,\s*5f\)"));
        }

        [Test]
        public void SleepStateUsesFixedStringsAndSupportsLateJoinPresentation()
        {
            string networkSource = ReadRuntimeSource(
                "Character/AI/AICharacterNetworkManager.cs");
            string animatorSource = ReadRuntimeSource(
                "Character/AI/AICharacterAnimatorManager.cs");

            Assert.That(networkSource, Does.Contain("NetworkVariable<bool> IsAwake"));
            Assert.That(
                networkSource,
                Does.Contain("NetworkVariable<FixedString64Bytes>"));
            Assert.That(networkSource, Does.Contain("if (!IsAwake.Value)"));
            Assert.That(networkSource, Does.Contain("PlaySleepingAnimation();"));
            Assert.That(
                networkSource,
                Does.Contain("IsAwake.OnValueChanged += OnAwakeStateChanged"));
            Assert.That(
                networkSource,
                Does.Contain("IsAwake.OnValueChanged -= OnAwakeStateChanged"));
            Assert.That(
                animatorSource,
                Does.Contain("keepAnimatorStateOnDisable = true"));
        }

        [Test]
        public void WakeTriggerUsesSeparateRadiiAndOnlyAssignsTarget()
        {
            string source = ReadRuntimeSource(
                "World Managers/AI/EventTriggerWakeNearbyCharacters.cs");

            Assert.That(source, Does.Contain("m_triggerRadius = 1f"));
            Assert.That(source, Does.Contain("m_awakenRadius = 20f"));
            Assert.That(source, Does.Contain("!networkManager.IsServer"));
            Assert.That(source, Does.Contain("m_creaturesToWake.Add"));
            Assert.That(source, Does.Contain("aiCharacter.SetTarget(player)"));
            Assert.That(source, Does.Not.Contain("SetAwakeState("));
        }

        [Test]
        public void AuthoredPrefabsExposePatrolSleepAndPersistentAnimation()
        {
            GameObject aiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_AICharacterPrefabPath);
            NavMeshAgent agent = aiPrefab?.GetComponent<NavMeshAgent>();
            Animator animator = aiPrefab?.GetComponentInChildren<Animator>(true);
            GameObject managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_WorldAIManagerPrefabPath);
            Type patrolPathType = GetRuntimeType("ZZ.AIPatrolPath");
            Type spawnerType = GetRuntimeType("ZZ.AICharacterSpawner");
            Type wakeTriggerType = GetRuntimeType(
                "ZZ.EventTriggerWakeNearbyCharacters");
            Component patrolPath = managerPrefab
                ?.GetComponentsInChildren(patrolPathType, true)
                .FirstOrDefault(path =>
                    GetPropertyValue<int>(path, "PatrolPathID") ==
                    k_PatrolPathID);
            Component[] spawners = managerPrefab
                ?.GetComponentsInChildren(spawnerType, true)
                .Where(spawner => !GetPropertyValue<bool>(spawner, "IsBoss"))
                .ToArray();
            Assert.That(patrolPath, Is.Not.Null);
            object patrolPoints = patrolPathType.GetProperty("PatrolPoints")
                .GetValue(patrolPath);

            Assert.That(agent?.enabled, Is.False);
            Assert.That(animator?.keepAnimatorStateOnDisable, Is.True);
            Assert.That(
                patrolPoints.GetType().GetProperty("Count").GetValue(patrolPoints),
                Is.EqualTo(4));
            Assert.That(spawners, Is.Not.Null);
            Assert.That(
                spawners.Any(spawner =>
                    GetPropertyValue<int>(spawner, "PatrolPathID") ==
                    k_PatrolPathID),
                Is.True);
            Assert.That(
                spawners.Any(spawner =>
                    GetPropertyValue<bool>(spawner, "IsSleeping")),
                Is.True);
            Assert.That(
                managerPrefab.GetComponentInChildren(wakeTriggerType, true),
                Is.Not.Null);
        }

        [Test]
        public void AnimatorContainsAuthoredSleepAndWakeStates()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    k_AIAnimatorControllerPath);
            AnimatorControllerLayer actionLayer = controller.layers
                .Single(layer => layer.name == "Action Override");
            AnimatorState[] states = actionLayer.stateMachine.states
                .Select(childState => childState.state)
                .ToArray();

            Assert.That(states.Any(state => state.name == "Sleep_01"), Is.True);
            Assert.That(states.Any(state => state.name == "Wake_01"), Is.True);
            Assert.That(
                states.Single(state => state.name == "Sleep_01").transitions,
                Is.Empty);
        }

        private static void CreatePoint(
            Transform parent,
            string pointName,
            Vector3 position)
        {
            GameObject point = new GameObject(pointName);
            point.transform.SetParent(parent, false);
            point.transform.position = position;
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            return File.ReadAllText($"Assets/Script/{relativePath}");
        }

        private static T GetPropertyValue<T>(
            object target,
            string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
