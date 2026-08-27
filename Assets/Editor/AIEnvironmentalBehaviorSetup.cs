using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP108-110 patrol, sound, and sleep assets.</summary>
    public static class AIEnvironmentalBehaviorSetup
    {
        private const int k_PatrolPathID = 10801;
        private const string k_AIAnimatorControllerPath =
            "Assets/Data/Animations/AI/Undead AI Animator.controller";
        private const string k_AICharacterPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World AI Manager.prefab";
        private const string k_SleepClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Locomotion/" +
            "zombie_sit_idle_01.anim";
        private const string k_WakeClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Emotes/" +
            "zombie_sit_to_alert_01.anim";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_SleepStateName = "Sleep_01";
        private const string k_WakeStateName = "Wake_01";
        private const string k_PatrolPathName = "AI Patrol Path 10801";
        private const string k_WakeTriggerName = "Ambush Wake Trigger";

        private static readonly Vector3[] s_patrolOffsets =
        {
            Vector3.zero,
            new Vector3(4f, 0f, 0f),
            new Vector3(4f, 0f, 4f),
            new Vector3(0f, 0f, 4f)
        };

        [MenuItem("Tools/Elden/Configure AI Environmental Behaviors")]
        public static void ConfigureAIEnvironmentalBehaviors()
        {
            ConfigureAnimatorController();
            ConfigureAICharacterPrefab();
            ConfigureWorldAIManagerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateAIEnvironmentalBehaviors();
            Debug.Log(
                "[AIEnvironmentalBehaviorSetup] Configured Patrol, sound " +
                "investigation, sleep, and event-trigger wake behavior.");
        }

        [MenuItem("Tools/Elden/Validate AI Environmental Behaviors")]
        public static void ValidateAIEnvironmentalBehaviors()
        {
            ValidateAnimatorController();
            ValidateAICharacterPrefab();
            ValidateWorldAIManagerPrefab();
            Debug.Log(
                "[AIEnvironmentalBehaviorValidation] Patrol, sound, and sleep " +
                "assets are valid.");
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIAnimatorControllerPath);
            AnimatorStateMachine stateMachine = GetActionStateMachine(controller);
            AnimatorState emptyState = FindState(stateMachine, k_EmptyStateName) ??
                throw new InvalidOperationException(
                    "The AI action layer requires its existing Empty state.");
            AnimatorState sleepState = GetOrCreateState(
                stateMachine,
                k_SleepStateName,
                new Vector3(790f, 260f, 0f));
            sleepState.motion = LoadRequiredAsset<AnimationClip>(k_SleepClipPath);
            RemoveTransitions(sleepState);

            AnimatorState wakeState = GetOrCreateState(
                stateMachine,
                k_WakeStateName,
                new Vector3(790f, 370f, 0f));
            wakeState.motion = LoadRequiredAsset<AnimationClip>(k_WakeClipPath);
            ConfigureExitTransition(wakeState, emptyState, 0.9f, 0.1f);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureAICharacterPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_AICharacterPrefabPath);
            try
            {
                NavMeshAgent agent = root.GetComponent<NavMeshAgent>() ??
                    throw new InvalidOperationException(
                        "The Undead AI prefab requires a NavMeshAgent.");
                Animator animator = root.GetComponentInChildren<Animator>(true) ??
                    throw new InvalidOperationException(
                        "The Undead AI prefab requires an Animator.");
                agent.enabled = false;
                animator.keepAnimatorStateOnDisable = true;
                EditorUtility.SetDirty(agent);
                EditorUtility.SetDirty(animator);
                PrefabUtility.SaveAsPrefabAsset(root, k_AICharacterPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureWorldAIManagerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldAIManagerPrefabPath);
            try
            {
                AICharacterSpawner[] spawners = GetOrderedNormalSpawners(root);
                if (spawners.Length < 3)
                {
                    throw new InvalidOperationException(
                        "The World AI Manager requires three normal AI spawners.");
                }

                ConfigurePatrolPath(root.transform, spawners[0].transform);
                ConfigureSpawner(spawners[0], k_PatrolPathID, true, false, true);
                ConfigureSpawner(spawners[1], 0, false, false, true);
                ConfigureSpawner(spawners[2], 0, false, true, true);
                ConfigureWakeTrigger(root.transform, spawners[2].transform);
                PrefabUtility.SaveAsPrefabAsset(root, k_WorldAIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigurePatrolPath(
            Transform managerRoot,
            Transform firstSpawner)
        {
            Transform patrolRoot = GetOrCreateChild(managerRoot, k_PatrolPathName);
            patrolRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            AIPatrolPath patrolPath = patrolRoot.GetComponent<AIPatrolPath>() ??
                patrolRoot.gameObject.AddComponent<AIPatrolPath>();
            SetInteger(patrolPath, "m_patrolPathID", k_PatrolPathID);

            for (int pointIndex = 0;
                pointIndex < s_patrolOffsets.Length;
                pointIndex++)
            {
                Transform patrolPoint = GetOrCreateChild(
                    patrolRoot,
                    $"Patrol Point {pointIndex + 1:00}");
                patrolPoint.localPosition =
                    firstSpawner.localPosition + s_patrolOffsets[pointIndex];
                patrolPoint.localRotation = Quaternion.identity;
            }

            patrolPath.RefreshPatrolPoints();
            EditorUtility.SetDirty(patrolPath);
        }

        private static void ConfigureSpawner(
            AICharacterSpawner spawner,
            int patrolPathID,
            bool repeatPatrol,
            bool isSleeping,
            bool willInvestigateSound)
        {
            SerializedObject serializedSpawner = new SerializedObject(spawner);
            GetRequiredProperty(
                serializedSpawner,
                "m_patrolPathID").intValue = patrolPathID;
            GetRequiredProperty(
                serializedSpawner,
                "m_repeatPatrol").boolValue = repeatPatrol;
            GetRequiredProperty(
                serializedSpawner,
                "m_isSleeping").boolValue = isSleeping;
            GetRequiredProperty(
                serializedSpawner,
                "m_willInvestigateSound").boolValue = willInvestigateSound;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
        }

        private static void ConfigureWakeTrigger(
            Transform managerRoot,
            Transform sleepingSpawner)
        {
            Transform triggerTransform = GetOrCreateChild(
                managerRoot,
                k_WakeTriggerName);
            triggerTransform.localPosition =
                sleepingSpawner.localPosition + new Vector3(0f, 0.5f, -3f);
            triggerTransform.localRotation = Quaternion.identity;
            EventTriggerWakeNearbyCharacters wakeTrigger =
                triggerTransform.GetComponent<EventTriggerWakeNearbyCharacters>() ??
                triggerTransform.gameObject.AddComponent<
                    EventTriggerWakeNearbyCharacters>();
            SphereCollider triggerCollider =
                triggerTransform.GetComponent<SphereCollider>() ??
                triggerTransform.gameObject.AddComponent<SphereCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.radius = 1f;
            SetFloat(wakeTrigger, "m_triggerRadius", 1f);
            SetFloat(wakeTrigger, "m_awakenRadius", 20f);
            EditorUtility.SetDirty(triggerCollider);
            EditorUtility.SetDirty(wakeTrigger);
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIAnimatorControllerPath);
            AnimatorStateMachine stateMachine = GetActionStateMachine(controller);
            AnimatorState sleepState = FindState(stateMachine, k_SleepStateName);
            AnimatorState wakeState = FindState(stateMachine, k_WakeStateName);
            if (sleepState?.motion !=
                    LoadRequiredAsset<AnimationClip>(k_SleepClipPath) ||
                wakeState?.motion != LoadRequiredAsset<AnimationClip>(k_WakeClipPath) ||
                sleepState.transitions.Length != 0)
            {
                throw new InvalidOperationException(
                    "The AI Animator has invalid Sleep_01 or Wake_01 states.");
            }
        }

        private static void ValidateAICharacterPrefab()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_AICharacterPrefabPath);
            NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            if (agent == null ||
                agent.enabled ||
                animator == null ||
                !animator.keepAnimatorStateOnDisable)
            {
                throw new InvalidOperationException(
                    "The AI prefab must keep its NavMeshAgent disabled and Animator state persistent.");
            }
        }

        private static void ValidateWorldAIManagerPrefab()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_WorldAIManagerPrefabPath);
            AIPatrolPath patrolPath = prefab.GetComponentsInChildren<AIPatrolPath>(true)
                .FirstOrDefault(path => path.PatrolPathID == k_PatrolPathID);
            AICharacterSpawner[] spawners = GetOrderedNormalSpawners(prefab);
            EventTriggerWakeNearbyCharacters wakeTrigger = prefab
                .GetComponentInChildren<EventTriggerWakeNearbyCharacters>(true);
            if (patrolPath == null ||
                patrolPath.PatrolPoints.Count != s_patrolOffsets.Length ||
                spawners.Length < 3 ||
                spawners[0].PatrolPathID != k_PatrolPathID ||
                !spawners[2].IsSleeping ||
                !spawners[2].WillInvestigateSound ||
                wakeTrigger == null ||
                !Mathf.Approximately(wakeTrigger.TriggerRadius, 1f) ||
                !Mathf.Approximately(wakeTrigger.AwakenRadius, 20f))
            {
                throw new InvalidOperationException(
                    "The World AI Manager has invalid Patrol, Sleep, or wake-trigger data.");
            }
        }

        private static AICharacterSpawner[] GetOrderedNormalSpawners(
            GameObject root)
        {
            return root.GetComponentsInChildren<AICharacterSpawner>(true)
                .Where(spawner => !spawner.IsBoss)
                .OrderBy(spawner => spawner.transform.GetSiblingIndex())
                .ToArray();
        }

        private static AnimatorStateMachine GetActionStateMachine(
            AnimatorController controller)
        {
            AnimatorControllerLayer layer = controller.layers.FirstOrDefault(
                candidate => candidate.name == k_ActionLayerName);
            return layer?.stateMachine ??
                throw new InvalidOperationException(
                    "The AI Animator is missing its Action Override layer.");
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Vector3 position)
        {
            return FindState(stateMachine, stateName) ??
                stateMachine.AddState(stateName, position);
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName);
        }

        private static void ConfigureExitTransition(
            AnimatorState sourceState,
            AnimatorState destinationState,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = sourceState.transitions
                .FirstOrDefault(candidate =>
                    candidate.destinationState == destinationState) ??
                sourceState.AddTransition(destinationState);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            EditorUtility.SetDirty(transition);
        }

        private static void RemoveTransitions(AnimatorState state)
        {
            foreach (AnimatorStateTransition transition in state.transitions.ToArray())
            {
                state.RemoveTransition(transition);
            }
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void SetInteger(
            UnityEngine.Object target,
            string propertyName,
            int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(
            UnityEngine.Object target,
            string propertyName,
            float value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }
    }
}
