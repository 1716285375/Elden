using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP153-155 sneaking animation and concealment assets.</summary>
    public static class SneakingSystemSetup
    {
        private const string k_TargetControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_SourceControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Runtime/Yosif.controller";
        private const string k_StealthObjectPrefabPath =
            "Assets/_Game/Prefabs/World/Objects/Stealth Object.prefab";
        private const string k_BaseLayerName = "Base Layer";
        private const string k_ActionLayerName = "Action Override";
        private const string k_IsSneakingParameter = "isSneaking";
        private const string k_IsMovingParameter = "isMoving";
        private const string k_IsTwoHandingParameter = "isTwoHandingWeapon";

        private const string k_OneHandLocomotion = "Locomotion One Handed";
        private const string k_TwoHandLocomotion = "Locomotion Two Handed";
        private const string k_OneHandSneakIdle = "Sneak Idle One Handed";
        private const string k_OneHandSneakLocomotion =
            "Sneak Locomotion One Handed";
        private const string k_TwoHandSneakIdle = "Sneak Idle Two Handed";
        private const string k_TwoHandSneakLocomotion =
            "Sneak Locomotion Two Handed";
        private const string k_SneakBowDraw = "Sneak Bow Draw";

        [MenuItem("Tools/Elden/Configure Sneaking System")]
        public static void ConfigureSneakingSystem()
        {
            AnimatorController targetController = LoadRequiredAsset<AnimatorController>(
                k_TargetControllerPath);
            AnimatorController sourceController = LoadRequiredAsset<AnimatorController>(
                k_SourceControllerPath);
            EnsureBoolParameter(targetController, k_IsSneakingParameter);

            AnimatorStateMachine targetStateMachine = GetLayerStateMachine(
                targetController,
                k_BaseLayerName);
            AnimatorState oneHandLocomotion = GetRequiredState(
                targetStateMachine,
                k_OneHandLocomotion);
            AnimatorState twoHandLocomotion = GetRequiredState(
                targetStateMachine,
                k_TwoHandLocomotion);
            AnimatorState oneHandSneakIdle = EnsureStateFromSource(
                targetController,
                targetStateMachine,
                sourceController,
                "Idle Sneak (1h Weapon)",
                k_OneHandSneakIdle,
                new Vector3(820f, -120f));
            AnimatorState oneHandSneakLocomotion = EnsureStateFromSource(
                targetController,
                targetStateMachine,
                sourceController,
                "Locomotion (Sneak 1h Weapon)",
                k_OneHandSneakLocomotion,
                new Vector3(1060f, -120f));
            AnimatorState twoHandSneakIdle = EnsureStateFromSource(
                targetController,
                targetStateMachine,
                sourceController,
                "Idle Sneak (2h Weapon)",
                k_TwoHandSneakIdle,
                new Vector3(820f, 60f));
            AnimatorState twoHandSneakLocomotion = EnsureStateFromSource(
                targetController,
                targetStateMachine,
                sourceController,
                "Locomotion (Sneak 2h Weapon)",
                k_TwoHandSneakLocomotion,
                new Vector3(1060f, 60f));

            ConfigureEntryTransitions(
                oneHandLocomotion,
                twoHandLocomotion,
                oneHandSneakIdle,
                oneHandSneakLocomotion,
                twoHandSneakIdle,
                twoHandSneakLocomotion);
            ConfigureSneakTransitions(
                oneHandLocomotion,
                twoHandLocomotion,
                oneHandSneakIdle,
                oneHandSneakLocomotion,
                twoHandSneakIdle,
                twoHandSneakLocomotion);
            ConfigureSneakBowDraw(
                targetController,
                sourceController);
            ConfigureStealthObjectPrefab();

            EditorUtility.SetDirty(targetController);
            AssetDatabase.SaveAssets();
            ValidateSneakingSystem();
            Debug.Log(
                "[SneakingSystemSetup] Configured one-hand/two-hand sneak locomotion " +
                "and the reusable concealment volume.");
        }

        [MenuItem("Tools/Elden/Validate Sneaking System")]
        public static void ValidateSneakingSystem()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_TargetControllerPath);
            AnimatorControllerParameter sneakingParameter = controller.parameters
                .FirstOrDefault(parameter => parameter.name == k_IsSneakingParameter);
            if (sneakingParameter == null ||
                sneakingParameter.type != AnimatorControllerParameterType.Bool)
            {
                throw new InvalidOperationException(
                    "The shared Humanoid controller requires a Boolean isSneaking parameter.");
            }

            AnimatorStateMachine stateMachine = GetLayerStateMachine(
                controller,
                k_BaseLayerName);
            ValidateState(stateMachine, k_OneHandSneakIdle);
            ValidateState(stateMachine, k_OneHandSneakLocomotion);
            ValidateState(stateMachine, k_TwoHandSneakIdle);
            ValidateState(stateMachine, k_TwoHandSneakLocomotion);

            AnimatorStateMachine actionStateMachine = GetLayerStateMachine(
                controller,
                k_ActionLayerName);
            AnimatorState sneakBowDraw = GetRequiredState(
                actionStateMachine,
                k_SneakBowDraw);
            if (sneakBowDraw.motion == null ||
                sneakBowDraw.transitions.Length != 3)
            {
                throw new InvalidOperationException(
                    "Sneak Bow Draw requires its crouched motion and three bow transitions.");
            }

            GameObject stealthPrefab = LoadRequiredAsset<GameObject>(
                k_StealthObjectPrefabPath);
            Collider stealthCollider = stealthPrefab.GetComponent<Collider>();
            if (stealthPrefab.GetComponent<StealthObject>() == null ||
                stealthCollider == null ||
                !stealthCollider.isTrigger)
            {
                throw new InvalidOperationException(
                    "The Stealth Object prefab requires StealthObject and a trigger Collider.");
            }

            Debug.Log(
                "[SneakingSystemValidation] Animator states, transitions, motions, " +
                "and concealment prefab are valid.");
        }

        private static void ConfigureEntryTransitions(
            AnimatorState oneHandLocomotion,
            AnimatorState twoHandLocomotion,
            AnimatorState oneHandSneakIdle,
            AnimatorState oneHandSneakLocomotion,
            AnimatorState twoHandSneakIdle,
            AnimatorState twoHandSneakLocomotion)
        {
            EnsureTransition(
                oneHandLocomotion,
                oneHandSneakIdle,
                Condition(k_IsSneakingParameter, AnimatorConditionMode.If),
                Condition(k_IsMovingParameter, AnimatorConditionMode.IfNot),
                Condition(k_IsTwoHandingParameter, AnimatorConditionMode.IfNot));
            EnsureTransition(
                oneHandLocomotion,
                oneHandSneakLocomotion,
                Condition(k_IsSneakingParameter, AnimatorConditionMode.If),
                Condition(k_IsMovingParameter, AnimatorConditionMode.If),
                Condition(k_IsTwoHandingParameter, AnimatorConditionMode.IfNot));
            EnsureTransition(
                twoHandLocomotion,
                twoHandSneakIdle,
                Condition(k_IsSneakingParameter, AnimatorConditionMode.If),
                Condition(k_IsMovingParameter, AnimatorConditionMode.IfNot),
                Condition(k_IsTwoHandingParameter, AnimatorConditionMode.If));
            EnsureTransition(
                twoHandLocomotion,
                twoHandSneakLocomotion,
                Condition(k_IsSneakingParameter, AnimatorConditionMode.If),
                Condition(k_IsMovingParameter, AnimatorConditionMode.If),
                Condition(k_IsTwoHandingParameter, AnimatorConditionMode.If));
        }

        private static void ConfigureSneakTransitions(
            AnimatorState oneHandLocomotion,
            AnimatorState twoHandLocomotion,
            AnimatorState oneHandSneakIdle,
            AnimatorState oneHandSneakLocomotion,
            AnimatorState twoHandSneakIdle,
            AnimatorState twoHandSneakLocomotion)
        {
            EnsureTransition(
                oneHandSneakIdle,
                oneHandLocomotion,
                Condition(k_IsSneakingParameter, AnimatorConditionMode.IfNot));
            EnsureTransition(
                oneHandSneakIdle,
                oneHandSneakLocomotion,
                Condition(k_IsMovingParameter, AnimatorConditionMode.If));
            EnsureTransition(
                oneHandSneakIdle,
                twoHandSneakIdle,
                Condition(k_IsTwoHandingParameter, AnimatorConditionMode.If));

            EnsureTransition(
                oneHandSneakLocomotion,
                oneHandLocomotion,
                Condition(k_IsSneakingParameter, AnimatorConditionMode.IfNot));
            EnsureTransition(
                oneHandSneakLocomotion,
                oneHandSneakIdle,
                Condition(k_IsMovingParameter, AnimatorConditionMode.IfNot));
            EnsureTransition(
                oneHandSneakLocomotion,
                twoHandSneakLocomotion,
                Condition(k_IsTwoHandingParameter, AnimatorConditionMode.If));

            EnsureTransition(
                twoHandSneakIdle,
                twoHandLocomotion,
                Condition(k_IsSneakingParameter, AnimatorConditionMode.IfNot));
            EnsureTransition(
                twoHandSneakIdle,
                twoHandSneakLocomotion,
                Condition(k_IsMovingParameter, AnimatorConditionMode.If));
            EnsureTransition(
                twoHandSneakIdle,
                oneHandSneakIdle,
                Condition(k_IsTwoHandingParameter, AnimatorConditionMode.IfNot));

            EnsureTransition(
                twoHandSneakLocomotion,
                twoHandLocomotion,
                Condition(k_IsSneakingParameter, AnimatorConditionMode.IfNot));
            EnsureTransition(
                twoHandSneakLocomotion,
                twoHandSneakIdle,
                Condition(k_IsMovingParameter, AnimatorConditionMode.IfNot));
            EnsureTransition(
                twoHandSneakLocomotion,
                oneHandSneakLocomotion,
                Condition(k_IsTwoHandingParameter, AnimatorConditionMode.IfNot));
        }

        private static void ConfigureSneakBowDraw(
            AnimatorController targetController,
            AnimatorController sourceController)
        {
            AnimatorStateMachine actionStateMachine = GetLayerStateMachine(
                targetController,
                k_ActionLayerName);
            AnimatorState standardBowDraw = GetRequiredState(
                actionStateMachine,
                "Bow_Draw");
            AnimatorState sneakBowDraw = EnsureStateFromSource(
                targetController,
                actionStateMachine,
                sourceController,
                "Crouch_Load_Projectile_01",
                k_SneakBowDraw,
                new Vector3(1320f, 520f));
            CopyTransitions(standardBowDraw, sneakBowDraw);
        }

        private static void CopyTransitions(
            AnimatorState sourceState,
            AnimatorState targetState)
        {
            foreach (AnimatorStateTransition sourceTransition in
                sourceState.transitions)
            {
                if (sourceTransition.destinationState == null ||
                    targetState.transitions.Any(targetTransition =>
                        targetTransition.destinationState ==
                            sourceTransition.destinationState &&
                        ConditionsMatch(
                            targetTransition.conditions,
                            sourceTransition.conditions)))
                {
                    continue;
                }

                AnimatorStateTransition targetTransition =
                    targetState.AddTransition(sourceTransition.destinationState);
                targetTransition.hasExitTime = sourceTransition.hasExitTime;
                targetTransition.exitTime = sourceTransition.exitTime;
                targetTransition.hasFixedDuration =
                    sourceTransition.hasFixedDuration;
                targetTransition.duration = sourceTransition.duration;
                targetTransition.offset = sourceTransition.offset;
                targetTransition.canTransitionToSelf =
                    sourceTransition.canTransitionToSelf;
                targetTransition.interruptionSource =
                    sourceTransition.interruptionSource;
                targetTransition.orderedInterruption =
                    sourceTransition.orderedInterruption;
                foreach (AnimatorCondition condition in
                    sourceTransition.conditions)
                {
                    targetTransition.AddCondition(
                        condition.mode,
                        condition.threshold,
                        condition.parameter);
                }

                EditorUtility.SetDirty(targetTransition);
            }
        }

        private static AnimatorState EnsureStateFromSource(
            AnimatorController targetController,
            AnimatorStateMachine targetStateMachine,
            AnimatorController sourceController,
            string sourceStateName,
            string targetStateName,
            Vector3 position)
        {
            AnimatorState targetState = FindState(targetStateMachine, targetStateName);
            AnimatorState sourceState = sourceController.layers
                .Select(layer => FindState(layer.stateMachine, sourceStateName))
                .FirstOrDefault(state => state != null && state.motion != null);
            if (sourceState == null)
            {
                throw new InvalidOperationException(
                    $"Source controller is missing motion state {sourceStateName}.");
            }

            targetState ??= targetStateMachine.AddState(targetStateName, position);
            if (targetState.motion == null)
            {
                targetState.motion = CloneMotion(
                    sourceState.motion,
                    targetController,
                    targetStateName);
            }

            targetState.iKOnFeet = sourceState.iKOnFeet;
            targetState.writeDefaultValues = sourceState.writeDefaultValues;
            targetState.speed = sourceState.speed;
            EditorUtility.SetDirty(targetState);
            return targetState;
        }

        private static Motion CloneMotion(
            Motion sourceMotion,
            AnimatorController targetController,
            string motionName)
        {
            if (sourceMotion is not BlendTree sourceBlendTree)
            {
                return sourceMotion;
            }

            BlendTree targetBlendTree = new BlendTree
            {
                name = motionName + " Blend Tree",
                blendType = sourceBlendTree.blendType,
                blendParameter = sourceBlendTree.blendParameter,
                blendParameterY = sourceBlendTree.blendParameterY,
                minThreshold = sourceBlendTree.minThreshold,
                maxThreshold = sourceBlendTree.maxThreshold,
                useAutomaticThresholds = sourceBlendTree.useAutomaticThresholds,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(targetBlendTree, targetController);

            ChildMotion[] children = sourceBlendTree.children;
            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                ChildMotion child = children[childIndex];
                child.motion = CloneMotion(
                    child.motion,
                    targetController,
                    motionName + " Child " + childIndex);
                children[childIndex] = child;
            }

            targetBlendTree.children = children;
            EditorUtility.SetDirty(targetBlendTree);
            return targetBlendTree;
        }

        private static void EnsureTransition(
            AnimatorState sourceState,
            AnimatorState destinationState,
            params AnimatorCondition[] conditions)
        {
            bool alreadyExists = sourceState.transitions.Any(transition =>
                transition.destinationState == destinationState &&
                ConditionsMatch(transition.conditions, conditions));
            if (alreadyExists)
            {
                return;
            }

            AnimatorStateTransition newTransition = sourceState.AddTransition(
                destinationState);
            newTransition.hasExitTime = false;
            newTransition.hasFixedDuration = true;
            newTransition.duration = 0.15f;
            newTransition.canTransitionToSelf = false;
            foreach (AnimatorCondition condition in conditions)
            {
                newTransition.AddCondition(
                    condition.mode,
                    condition.threshold,
                    condition.parameter);
            }

            EditorUtility.SetDirty(newTransition);
        }

        private static bool ConditionsMatch(
            AnimatorCondition[] configuredConditions,
            AnimatorCondition[] expectedConditions)
        {
            return configuredConditions.Length == expectedConditions.Length &&
                expectedConditions.All(expected => configuredConditions.Any(configured =>
                    configured.mode == expected.mode &&
                    configured.parameter == expected.parameter &&
                    Mathf.Approximately(
                        configured.threshold,
                        expected.threshold)));
        }

        private static AnimatorCondition Condition(
            string parameter,
            AnimatorConditionMode conditionMode)
        {
            return new AnimatorCondition
            {
                parameter = parameter,
                mode = conditionMode,
                threshold = 0f
            };
        }

        private static void ConfigureStealthObjectPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_StealthObjectPrefabPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(k_StealthObjectPrefabPath)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.name = "Stealth Object";
                BoxCollider stealthCollider = root.GetComponent<BoxCollider>();
                if (stealthCollider == null)
                {
                    throw new InvalidOperationException(
                        "Unity failed to create the Stealth Object BoxCollider.");
                }

                stealthCollider.isTrigger = true;
                stealthCollider.size = new Vector3(4f, 2f, 4f);
                UnityEngine.Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
                UnityEngine.Object.DestroyImmediate(root.GetComponent<MeshFilter>());
                if (root.GetComponent<StealthObject>() == null)
                {
                    root.AddComponent<StealthObject>();
                }
                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        k_StealthObjectPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to save {k_StealthObjectPrefabPath}.");
                }
            }
            finally
            {
                if (prefab != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void EnsureBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .FirstOrDefault(candidate => candidate.name == parameterName);
            if (parameter == null)
            {
                controller.AddParameter(
                    parameterName,
                    AnimatorControllerParameterType.Bool);
                return;
            }

            if (parameter.type != AnimatorControllerParameterType.Bool)
            {
                throw new InvalidOperationException(
                    $"Animator parameter {parameterName} must be Boolean.");
            }
        }

        private static AnimatorStateMachine GetLayerStateMachine(
            AnimatorController controller,
            string layerName)
        {
            AnimatorControllerLayer layer = controller.layers
                .FirstOrDefault(candidate => candidate.name == layerName);
            return layer?.stateMachine ??
                throw new InvalidOperationException(
                    $"Animator controller is missing layer {layerName}.");
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return FindState(stateMachine, stateName) ??
                throw new InvalidOperationException(
                    $"Animator layer is missing state {stateName}.");
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            AnimatorState directState = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName);
            if (directState != null)
            {
                return directState;
            }

            foreach (ChildAnimatorStateMachine childStateMachine in
                stateMachine.stateMachines)
            {
                AnimatorState nestedState = FindState(
                    childStateMachine.stateMachine,
                    stateName);
                if (nestedState != null)
                {
                    return nestedState;
                }
            }

            return null;
        }

        private static void ValidateState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            AnimatorState state = GetRequiredState(stateMachine, stateName);
            if (state.motion == null || state.transitions.Length < 2)
            {
                throw new InvalidOperationException(
                    $"Sneaking state {stateName} requires motion and transitions.");
            }
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
