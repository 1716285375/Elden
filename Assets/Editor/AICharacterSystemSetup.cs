using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    public static class AICharacterSystemSetup
    {
        private const string k_AIAnimatorControllerPath =
            "Assets/Data/Animations/AI/Undead AI Animator.controller";
        private const string k_AICharacterPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World AI Manager.prefab";
        private const string k_WorldScenePath = "Assets/Scenes/Scene_World_01.unity";
        private const string k_NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string k_SourceVisualPrefabPath =
            "Assets/Art/Models/Rigged/Characters/Creatures/Undead/" +
            "Skeleton_00_Unarmed/Skeleton_00_Unarmed.prefab";
        private const string k_BloodVFXPath =
            "Assets/Data/Prefabs/Effects/BloodSplatterVFX.prefab";
        private const string k_IdleClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Locomotion/" +
            "zombie_idle_01.anim";
        private const string k_WalkClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Locomotion/" +
            "zombie_walk_01.anim";
        private const string k_PivotLeftClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Locomotion/" +
            "zombie_turn_L90_01.anim";
        private const string k_PivotRightClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Locomotion/" +
            "zombie_turn_R90_01.anim";
        private const string k_AttackClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Combat/General/" +
            "zombie_light_attack_01.anim";
        private const string k_DeathClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Reactions/" +
            "zombie_death_UP_01.anim";
        private const string k_DamageableLayerName = "Damageable Character";
        private const string k_DamageColliderLayerName = "Damage Collider";
        private const string k_BaseLayerName = "Base Layer";
        private const string k_ActionLayerName = "Action Override";
        private const string k_LocomotionStateName = "Locomotion";
        private const string k_PivotLeftStateName = "Pivot_Left";
        private const string k_PivotRightStateName = "Pivot_Right";
        private const string k_EmptyStateName = "Empty";
        private const string k_AttackStateName = "Attack_01";
        private const string k_DeathStateName = "Dead_01";
        private const string k_VisualRootName = "Undead Visuals";
        private const string k_DamageColliderRootName = "AI Damage Colliders";
        private const string k_LeftDamageColliderName = "Left Hand Damage Collider";
        private const string k_RightDamageColliderName = "Right Hand Damage Collider";
        private const string k_NavigationRootName = "Navigation";

        private static readonly Vector3[] s_spawnPositions =
        {
            new Vector3(-6f, 0.1f, 7f),
            new Vector3(6f, 0.1f, 7f),
            new Vector3(0f, 0.1f, 11f)
        };

        private static readonly ReactionDefinition[] s_reactionDefinitions =
        {
            new ReactionDefinition(
                "m_hitForwardAnimations",
                new[]
                {
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_f_01.anim"),
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_f_02.anim"),
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_f_03.anim")
                }),
            new ReactionDefinition(
                "m_hitBackwardAnimations",
                new[]
                {
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_B_01.anim"),
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_B_02.anim"),
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_B_03.anim")
                }),
            new ReactionDefinition(
                "m_hitLeftAnimations",
                new[]
                {
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_L_01.anim"),
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_L_02.anim"),
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_L_03.anim")
                }),
            new ReactionDefinition(
                "m_hitRightAnimations",
                new[]
                {
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_R_01.anim"),
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_R_02.anim"),
                    GetHumanoidReactionPath("core_oh_hit_reaction_medium_R_03.anim")
                })
        };

        [MenuItem("Tools/Elden/Configure AI Character System")]
        public static void ConfigureAICharacterSystem()
        {
            EnsureFolder("Assets/Data/Animations/AI");
            EnsureFolder("Assets/Data/Prefabs/Characters/AI");

            AnimatorController controller = ConfigureAnimatorController();
            GameObject aiCharacterPrefab = ConfigureAICharacterPrefab(controller);
            ConfigureWorldAIManagerPrefab(aiCharacterPrefab);
            RegisterNetworkPrefab(aiCharacterPrefab);
            ConfigureWorldScene();
            AssetDatabase.SaveAssets();
            ValidateAICharacterSystem();
            Debug.Log(
                "[AICharacterSystemSetup] Configured server-authoritative idle, " +
                "pursuit, pivot, combat, attack, damage, death, spawning, and NavMesh.");
        }

        [MenuItem("Tools/Elden/Validate AI Character System")]
        public static void ValidateAICharacterSystem()
        {
            ValidateStateIdentifiers();
            ValidateStateArchitecture();
            ValidateAnimatorController();
            ValidateAICharacterPrefab();
            ValidateWorldAIManagerPrefab();
            ValidateNetworkPrefabRegistration();
            ValidateWorldScene();
            Debug.Log(
                "[AICharacterSystemValidation] AI states, prefab, animation events, " +
                "network registration, spawn points, and NavMesh are valid.");
        }

        private static AnimatorController ConfigureAnimatorController()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    k_AIAnimatorControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(
                    k_AIAnimatorControllerPath);
            }

            EnsureParameter(controller, "Horizontal", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Vertical", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "isGrounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "inAirTimer", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "isDead", AnimatorControllerParameterType.Bool);
            EnsureParameter(
                controller,
                "isChargingAttack",
                AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "PivotLeft", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "PivotRight", AnimatorControllerParameterType.Trigger);

            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].name = k_BaseLayerName;
            layers[0].defaultWeight = 1f;
            controller.layers = layers;
            AnimatorControllerLayer baseLayer = controller.layers[0];
            ConfigureBaseLayer(controller, baseLayer);
            AnimatorControllerLayer actionLayer = GetOrCreateLayer(
                controller,
                k_ActionLayerName);
            layers = controller.layers;
            int actionLayerIndex = Array.FindIndex(
                layers,
                candidate => candidate.name == k_ActionLayerName);
            layers[actionLayerIndex].defaultWeight = 1f;
            layers[actionLayerIndex].blendingMode =
                AnimatorLayerBlendingMode.Override;
            controller.layers = layers;
            actionLayer = controller.layers[actionLayerIndex];
            ConfigureActionLayer(controller, actionLayer);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureBaseLayer(
            AnimatorController controller,
            AnimatorControllerLayer layer)
        {
            AnimatorStateMachine stateMachine = layer.stateMachine;
            AnimatorState locomotionState = GetOrCreateState(
                stateMachine,
                k_LocomotionStateName,
                new Vector3(280f, 120f, 0f));
            BlendTree locomotionTree = locomotionState.motion as BlendTree;
            if (locomotionTree == null)
            {
                locomotionTree = new BlendTree
                {
                    name = "AI Locomotion Blend Tree",
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(locomotionTree, controller);
                locomotionState.motion = locomotionTree;
            }

            locomotionTree.blendType = BlendTreeType.Simple1D;
            locomotionTree.blendParameter = "Vertical";
            locomotionTree.useAutomaticThresholds = false;
            locomotionTree.children = new[]
            {
                new ChildMotion
                {
                    motion = LoadRequiredAsset<AnimationClip>(k_IdleClipPath),
                    threshold = 0f,
                    timeScale = 1f
                },
                new ChildMotion
                {
                    motion = LoadRequiredAsset<AnimationClip>(k_WalkClipPath),
                    threshold = 1f,
                    timeScale = 1f
                }
            };
            stateMachine.defaultState = locomotionState;

            AnimatorState pivotLeftState = GetOrCreateState(
                stateMachine,
                k_PivotLeftStateName,
                new Vector3(530f, 45f, 0f));
            AnimatorState pivotRightState = GetOrCreateState(
                stateMachine,
                k_PivotRightStateName,
                new Vector3(530f, 195f, 0f));
            pivotLeftState.motion = LoadRequiredAsset<AnimationClip>(
                k_PivotLeftClipPath);
            pivotRightState.motion = LoadRequiredAsset<AnimationClip>(
                k_PivotRightClipPath);
            ConfigureConditionalTransition(
                locomotionState,
                pivotLeftState,
                "PivotLeft");
            ConfigureConditionalTransition(
                locomotionState,
                pivotRightState,
                "PivotRight");
            ConfigureExitTransition(pivotLeftState, locomotionState, 0.9f, 0.1f);
            ConfigureExitTransition(pivotRightState, locomotionState, 0.9f, 0.1f);
        }

        private static void ConfigureActionLayer(
            AnimatorController controller,
            AnimatorControllerLayer layer)
        {
            AnimatorStateMachine stateMachine = layer.stateMachine;
            AnimatorState emptyState = GetOrCreateState(
                stateMachine,
                k_EmptyStateName,
                new Vector3(260f, 110f, 0f));
            emptyState.motion = null;
            if (!emptyState.behaviours.Any(behaviour => behaviour is ResetActionFlags))
            {
                emptyState.AddStateMachineBehaviour<ResetActionFlags>();
            }

            stateMachine.defaultState = emptyState;
            AnimatorState attackState = GetOrCreateState(
                stateMachine,
                k_AttackStateName,
                new Vector3(520f, 50f, 0f));
            attackState.motion = LoadRequiredAsset<AnimationClip>(k_AttackClipPath);
            ConfigureExitTransition(attackState, emptyState, 0.9f, 0.1f);

            AnimatorState deathState = GetOrCreateState(
                stateMachine,
                k_DeathStateName,
                new Vector3(520f, 190f, 0f));
            deathState.motion = LoadRequiredAsset<AnimationClip>(k_DeathClipPath);
            RemoveTransitions(deathState);

            int reactionIndex = 0;
            foreach (ReactionDefinition definition in s_reactionDefinitions)
            {
                foreach (string animationPath in definition.AnimationPaths)
                {
                    AnimationClip reactionClip =
                        LoadRequiredAsset<AnimationClip>(animationPath);
                    AnimatorState reactionState = GetOrCreateState(
                        stateMachine,
                        reactionClip.name,
                        new Vector3(
                            760f + reactionIndex % 3 * 210f,
                            30f + reactionIndex / 3 * 90f,
                            0f));
                    reactionState.motion = reactionClip;
                    ConfigureExitTransition(reactionState, emptyState, 0.9f, 0.1f);
                    reactionIndex++;
                }
            }
        }

        private static GameObject ConfigureAICharacterPrefab(
            RuntimeAnimatorController controller)
        {
            EditPrefab(
                k_AICharacterPrefabPath,
                "Undead AI",
                root => ConfigureAICharacterRoot(root, controller));
            return LoadRequiredAsset<GameObject>(k_AICharacterPrefabPath);
        }

        private static void ConfigureAICharacterRoot(
            GameObject root,
            RuntimeAnimatorController controller)
        {
            root.name = "Undead AI";
            int damageableLayer = LayerMask.NameToLayer(k_DamageableLayerName);
            int damageColliderLayer = LayerMask.NameToLayer(k_DamageColliderLayerName);
            if (damageableLayer < 0 || damageColliderLayer < 0)
            {
                throw new InvalidOperationException(
                    "AI setup requires Damageable Character and Damage Collider layers.");
            }

            root.layer = damageableLayer;
            GetOrAddComponent<NetworkObject>(root);
            CharacterController characterController =
                GetOrAddComponent<CharacterController>(root);
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.radius = 0.38f;
            characterController.height = 1.8f;
            characterController.enabled = false;

            CapsuleCollider bodyCollider = GetOrAddComponent<CapsuleCollider>(root);
            bodyCollider.center = new Vector3(0f, 0.9f, 0f);
            bodyCollider.radius = 0.38f;
            bodyCollider.height = 1.8f;
            bodyCollider.isTrigger = false;
            bodyCollider.enabled = true;

            NavMeshAgent navMeshAgent = GetOrAddComponent<NavMeshAgent>(root);
            navMeshAgent.speed = 2.4f;
            navMeshAgent.acceleration = 12f;
            navMeshAgent.angularSpeed = 0f;
            navMeshAgent.radius = 0.38f;
            navMeshAgent.height = 1.8f;
            navMeshAgent.stoppingDistance = 1.65f;
            navMeshAgent.autoBraking = true;
            navMeshAgent.updateRotation = false;

            AICharacterNetworkManager networkManager =
                GetOrAddComponent<AICharacterNetworkManager>(root);
            AICharacterManager characterManager =
                GetOrAddComponent<AICharacterManager>(root);
            CharacterStatsManager statsManager =
                GetOrAddComponent<CharacterStatsManager>(root);
            CharacterEffectsManager effectsManager =
                GetOrAddComponent<CharacterEffectsManager>(root);
            AICharacterCombatManager combatManager =
                GetOrAddComponent<AICharacterCombatManager>(root);

            GameObject visualRoot = GetOrCreateVisualRoot(root);
            foreach (Collider visualCollider in
                visualRoot.GetComponentsInChildren<Collider>(true))
            {
                visualCollider.enabled = false;
            }

            Animator animator = visualRoot.GetComponentInChildren<Animator>(true) ??
                throw new InvalidOperationException(
                    $"{k_SourceVisualPrefabPath} is missing an Animator.");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            CharacterAnimatorManager existingAnimatorManager =
                animator.GetComponent<CharacterAnimatorManager>();
            if (existingAnimatorManager != null &&
                existingAnimatorManager is not AICharacterAnimatorManager)
            {
                UnityEngine.Object.DestroyImmediate(existingAnimatorManager, true);
            }

            AICharacterAnimatorManager animatorManager =
                GetOrAddComponent<AICharacterAnimatorManager>(animator.gameObject);
            ConfigureReactionLists(animatorManager);

            Transform damageRoot = GetOrCreateChild(
                root.transform,
                k_DamageColliderRootName);
            AIDamageCollider leftDamageCollider = ConfigureDamageCollider(
                GetOrCreateChild(damageRoot, k_LeftDamageColliderName),
                new Vector3(-0.3f, 1f, 0.9f),
                damageColliderLayer);
            AIDamageCollider rightDamageCollider = ConfigureDamageCollider(
                GetOrCreateChild(damageRoot, k_RightDamageColliderName),
                new Vector3(0.3f, 1f, 0.9f),
                damageColliderLayer);

            SetObjectReference(characterManager, "m_animator", animator);
            SetObjectReference(
                characterManager,
                "m_characterAnimatorManager",
                animatorManager);
            SetObjectReference(
                characterManager,
                "m_characterNetworkManager",
                networkManager);
            SetObjectReference(characterManager, "m_navMeshAgent", navMeshAgent);
            SetObjectReference(characterManager, "m_bodyCollider", bodyCollider);
            SetObjectReference(
                characterManager,
                "m_aiAnimatorManager",
                animatorManager);
            SetObjectReference(
                characterManager,
                "m_aiNetworkManager",
                networkManager);
            SetObjectReference(
                characterManager,
                "m_aiCombatManager",
                combatManager);
            SetObjectReference(
                combatManager,
                "m_leftHandDamageCollider",
                leftDamageCollider);
            SetObjectReference(
                combatManager,
                "m_rightHandDamageCollider",
                rightDamageCollider);
            SetObjectReference(
                effectsManager,
                "m_bloodSplatterVFX",
                LoadRequiredAsset<GameObject>(k_BloodVFXPath));
            EditorUtility.SetDirty(statsManager);
        }

        private static GameObject GetOrCreateVisualRoot(GameObject root)
        {
            Transform existing = root.transform.Find(k_VisualRootName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject sourcePrefab = LoadRequiredAsset<GameObject>(
                k_SourceVisualPrefabPath);
            GameObject visualRoot = (GameObject)PrefabUtility.InstantiatePrefab(
                sourcePrefab,
                root.scene);
            visualRoot.name = k_VisualRootName;
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            visualRoot.transform.localScale = Vector3.one;
            return visualRoot;
        }

        private static AIDamageCollider ConfigureDamageCollider(
            Transform colliderTransform,
            Vector3 localPosition,
            int damageColliderLayer)
        {
            colliderTransform.gameObject.layer = damageColliderLayer;
            colliderTransform.localPosition = localPosition;
            colliderTransform.localRotation = Quaternion.identity;
            colliderTransform.localScale = Vector3.one;
            BoxCollider boxCollider = GetOrAddComponent<BoxCollider>(
                colliderTransform.gameObject);
            boxCollider.center = Vector3.zero;
            boxCollider.size = new Vector3(0.75f, 1.2f, 1.6f);
            boxCollider.isTrigger = true;
            AIDamageCollider damageCollider =
                GetOrAddComponent<AIDamageCollider>(colliderTransform.gameObject);
            boxCollider.enabled = false;
            return damageCollider;
        }

        private static void ConfigureReactionLists(
            AICharacterAnimatorManager animatorManager)
        {
            foreach (ReactionDefinition definition in s_reactionDefinitions)
            {
                AnimationClip[] clips = definition.AnimationPaths
                    .Select(LoadRequiredAsset<AnimationClip>)
                    .ToArray();
                SetObjectArray(
                    animatorManager,
                    definition.SerializedPropertyName,
                    clips);
            }
        }

        private static void ConfigureWorldAIManagerPrefab(GameObject aiCharacterPrefab)
        {
            EditPrefab(
                k_WorldAIManagerPrefabPath,
                "World AI Manager",
                root =>
                {
                    root.name = "World AI Manager";
                    GetOrAddComponent<WorldAIManager>(root);
                    for (int spawnIndex = 0;
                        spawnIndex < s_spawnPositions.Length;
                        spawnIndex++)
                    {
                        Transform spawnPoint = GetOrCreateChild(
                            root.transform,
                            $"AI Spawn Point {spawnIndex + 1:00}");
                        spawnPoint.localPosition = s_spawnPositions[spawnIndex];
                        spawnPoint.localRotation = Quaternion.Euler(
                            0f,
                            spawnIndex * 120f,
                            0f);
                        spawnPoint.localScale = Vector3.one;
                        AISpawnPoint legacySpawnPoint =
                            spawnPoint.GetComponent<AISpawnPoint>();
                        if (legacySpawnPoint != null)
                        {
                            UnityEngine.Object.DestroyImmediate(
                                legacySpawnPoint,
                                true);
                        }

                        AICharacterSpawner characterSpawner =
                            GetOrAddComponent<AICharacterSpawner>(
                                spawnPoint.gameObject);
                        SetObjectReference(
                            characterSpawner,
                            "m_characterGameObject",
                            aiCharacterPrefab);
                    }
                });
        }

        private static void RegisterNetworkPrefab(GameObject aiCharacterPrefab)
        {
            NetworkPrefabsList prefabsList =
                LoadRequiredAsset<NetworkPrefabsList>(k_NetworkPrefabsPath);
            SerializedObject serializedList = new SerializedObject(prefabsList);
            SerializedProperty entries = GetRequiredProperty(serializedList, "List");
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                SerializedProperty prefab = entries.GetArrayElementAtIndex(entryIndex)
                    .FindPropertyRelative("Prefab");
                if (prefab != null && prefab.objectReferenceValue == aiCharacterPrefab)
                {
                    return;
                }
            }

            int newEntryIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newEntryIndex);
            SerializedProperty newEntry = entries.GetArrayElementAtIndex(newEntryIndex);
            SetRelativeInteger(newEntry, "Override", 0);
            SetRelativeObject(newEntry, "Prefab", aiCharacterPrefab);
            SetRelativeObject(newEntry, "SourcePrefabToOverride", null);
            SetRelativeInteger(newEntry, "SourceHashToOverride", 0);
            SetRelativeObject(newEntry, "OverridingTargetPrefab", null);
            serializedList.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefabsList);
        }

        private static void ConfigureWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                WorldAIManager manager = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<WorldAIManager>())
                    .FirstOrDefault(candidate => candidate != null);
                if (manager == null)
                {
                    GameObject managerPrefab = LoadRequiredAsset<GameObject>(
                        k_WorldAIManagerPrefabPath);
                    manager = ((GameObject)PrefabUtility.InstantiatePrefab(
                        managerPrefab,
                        scene)).GetComponent<WorldAIManager>();
                }

                GameObject navigationRoot = scene.GetRootGameObjects()
                    .FirstOrDefault(root => root.name == k_NavigationRootName);
                if (navigationRoot == null)
                {
                    navigationRoot = new GameObject(k_NavigationRootName);
                    SceneManager.MoveGameObjectToScene(navigationRoot, scene);
                }

                NavMeshSurface surface = GetOrAddComponent<NavMeshSurface>(
                    navigationRoot);
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                int excludedLayers = LayerMask.GetMask(
                    "Player",
                    k_DamageColliderLayerName,
                    k_DamageableLayerName);
                surface.layerMask = ~excludedLayers;
                surface.BuildNavMesh();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedForSetup)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateStateIdentifiers()
        {
            if ((byte)AICharacterStateId.Idle != 0 ||
                (byte)AICharacterStateId.PursueTarget != 1 ||
                (byte)AICharacterStateId.CombatStance != 2 ||
                (byte)AICharacterStateId.Attack != 3 ||
                (byte)AICharacterStateId.Dead != 4)
            {
                throw new InvalidOperationException(
                    "AI state network identifiers must remain stable.");
            }
        }

        private static void ValidateStateArchitecture()
        {
            Assembly runtimeAssembly = typeof(AICharacterManager).Assembly;
            string[] stateTypeNames =
            {
                "ZZ.IdleAIState",
                "ZZ.PursueTargetAIState",
                "ZZ.CombatStanceAIState",
                "ZZ.AttackAIState",
                "ZZ.DeadAIState"
            };
            Type stateBaseType = runtimeAssembly.GetType("ZZ.AICharacterState");
            bool hasInvalidStateType = stateBaseType == null ||
                stateTypeNames.Any(stateTypeName =>
                {
                    Type stateType = runtimeAssembly.GetType(stateTypeName);
                    return stateType == null ||
                        !stateBaseType.IsAssignableFrom(stateType);
                });
            if (hasInvalidStateType ||
                typeof(AICharacterManager).GetMethod(
                    "TryAcquireTarget",
                    BindingFlags.Instance | BindingFlags.NonPublic) == null ||
                typeof(WorldAIManager).GetMethod(
                    "RegisterAI",
                    BindingFlags.Instance | BindingFlags.Public) == null)
            {
                throw new InvalidOperationException(
                    "AI manager and state machine architecture is incomplete.");
            }
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIAnimatorControllerPath);
            AnimatorControllerLayer baseLayer = GetRequiredLayer(
                controller,
                k_BaseLayerName);
            AnimatorControllerLayer actionLayer = GetRequiredLayer(
                controller,
                k_ActionLayerName);
            if (!Mathf.Approximately(actionLayer.defaultWeight, 1f))
            {
                throw new InvalidOperationException(
                    "AI action animation layer must have full weight.");
            }

            AnimatorState locomotionState = GetRequiredState(
                baseLayer.stateMachine,
                k_LocomotionStateName);
            BlendTree locomotionTree = locomotionState.motion as BlendTree;
            if (locomotionTree == null ||
                locomotionTree.blendParameter != "Vertical" ||
                locomotionTree.children.Length != 2 ||
                locomotionTree.children[0].motion !=
                LoadRequiredAsset<AnimationClip>(k_IdleClipPath) ||
                locomotionTree.children[1].motion !=
                LoadRequiredAsset<AnimationClip>(k_WalkClipPath))
            {
                throw new InvalidOperationException(
                    "AI locomotion blend tree is not configured correctly.");
            }

            ValidateStateMotion(
                baseLayer.stateMachine,
                k_PivotLeftStateName,
                k_PivotLeftClipPath);
            ValidateStateMotion(
                baseLayer.stateMachine,
                k_PivotRightStateName,
                k_PivotRightClipPath);
            ValidateStateMotion(
                actionLayer.stateMachine,
                k_AttackStateName,
                k_AttackClipPath);
            ValidateStateMotion(
                actionLayer.stateMachine,
                k_DeathStateName,
                k_DeathClipPath);
            AnimatorState emptyState = GetRequiredState(
                actionLayer.stateMachine,
                k_EmptyStateName);
            if (!emptyState.behaviours.Any(behaviour => behaviour is ResetActionFlags))
            {
                throw new InvalidOperationException(
                    "AI Action Override.Empty must reset action flags.");
            }

            foreach (ReactionDefinition definition in s_reactionDefinitions)
            {
                foreach (string animationPath in definition.AnimationPaths)
                {
                    AnimationClip clip = LoadRequiredAsset<AnimationClip>(animationPath);
                    ValidateStateMotion(
                        actionLayer.stateMachine,
                        clip.name,
                        animationPath);
                }
            }

            ValidateAnimationEventReceivers();
        }

        private static void ValidateAnimationEventReceivers()
        {
            AnimationClip attackClip = LoadRequiredAsset<AnimationClip>(k_AttackClipPath);
            string[] requiredEvents =
            {
                "SetSwipeAttackDamage",
                "EnableCanRotate",
                "DisableCanRotate",
                "OpenLeftHandDamageCollider",
                "CloseLeftHandDamageCollider",
                "OpenRightHandDamageCollider",
                "CloseRightHandDamageCollider",
                "EnableCanDoCombo"
            };
            string[] authoredEvents = AnimationUtility.GetAnimationEvents(attackClip)
                .Select(animationEvent => animationEvent.functionName)
                .ToArray();
            foreach (string eventName in requiredEvents)
            {
                if (!authoredEvents.Contains(eventName) ||
                    typeof(AICharacterAnimatorManager).GetMethod(
                        eventName,
                        BindingFlags.Instance | BindingFlags.Public) == null)
                {
                    throw new InvalidOperationException(
                        $"AI attack animation event {eventName} is not wired.");
                }
            }
        }

        private static void ValidateAICharacterPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_AICharacterPrefabPath);
            try
            {
                AICharacterManager character =
                    GetRequiredComponent<AICharacterManager>(root);
                AICharacterNetworkManager network =
                    GetRequiredComponent<AICharacterNetworkManager>(root);
                AICharacterCombatManager combat =
                    GetRequiredComponent<AICharacterCombatManager>(root);
                GetRequiredComponent<CharacterStatsManager>(root);
                GetRequiredComponent<CharacterEffectsManager>(root);
                GetRequiredComponent<NetworkObject>(root);
                CharacterController controller =
                    GetRequiredComponent<CharacterController>(root);
                CapsuleCollider bodyCollider =
                    GetRequiredComponent<CapsuleCollider>(root);
                NavMeshAgent agent = GetRequiredComponent<NavMeshAgent>(root);
                AICharacterAnimatorManager animatorManager =
                    root.GetComponentInChildren<AICharacterAnimatorManager>(true);
                AIDamageCollider[] damageColliders =
                    root.GetComponentsInChildren<AIDamageCollider>(true);
                bool hasInvalidDamageCollider = damageColliders.Any(
                    damageCollider =>
                        damageCollider.gameObject.layer !=
                            LayerMask.NameToLayer(k_DamageColliderLayerName) ||
                        damageCollider.GetComponent<Collider>()?.enabled != false);
                bool hasExpectedAnimatorController = animatorManager != null &&
                    animatorManager.GetComponent<Animator>()?
                        .runtimeAnimatorController ==
                    LoadRequiredAsset<AnimatorController>(k_AIAnimatorControllerPath);
                if (controller.enabled ||
                    !bodyCollider.enabled ||
                    root.layer != LayerMask.NameToLayer(k_DamageableLayerName) ||
                    !hasExpectedAnimatorController ||
                    damageColliders.Length != 2 ||
                    hasInvalidDamageCollider)
                {
                    throw new InvalidOperationException(
                        "Undead AI prefab validation failed: " +
                        $"controllerEnabled={controller.enabled}, " +
                        $"bodyColliderEnabled={bodyCollider.enabled}, " +
                        $"rootLayer={root.layer}, " +
                        $"animatorController={hasExpectedAnimatorController}, " +
                        $"damageColliderCount={damageColliders.Length}, " +
                        $"invalidDamageCollider={hasInvalidDamageCollider}.");
                }

                ValidateObjectReference(character, "m_aiNetworkManager", network);
                ValidateObjectReference(character, "m_aiCombatManager", combat);
                ValidateReactionLists(animatorManager);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateReactionLists(
            AICharacterAnimatorManager animatorManager)
        {
            foreach (ReactionDefinition definition in s_reactionDefinitions)
            {
                SerializedProperty property = GetRequiredProperty(
                    new SerializedObject(animatorManager),
                    definition.SerializedPropertyName);
                if (property.arraySize != definition.AnimationPaths.Length)
                {
                    throw new InvalidOperationException(
                        $"{definition.SerializedPropertyName} has the wrong size.");
                }

                for (int clipIndex = 0;
                    clipIndex < definition.AnimationPaths.Length;
                    clipIndex++)
                {
                    if (property.GetArrayElementAtIndex(clipIndex)
                            .objectReferenceValue !=
                        LoadRequiredAsset<AnimationClip>(
                            definition.AnimationPaths[clipIndex]))
                    {
                        throw new InvalidOperationException(
                            $"{definition.SerializedPropertyName}[{clipIndex}] is invalid.");
                    }
                }
            }
        }

        private static void ValidateWorldAIManagerPrefab()
        {
            GameObject managerPrefab = LoadRequiredAsset<GameObject>(
                k_WorldAIManagerPrefabPath);
            GetRequiredComponent<WorldAIManager>(managerPrefab);
            GameObject aiCharacterPrefab = LoadRequiredAsset<GameObject>(
                k_AICharacterPrefabPath);
            AICharacterSpawner[] characterSpawners = managerPrefab
                .GetComponentsInChildren<AICharacterSpawner>(true)
                .Where(spawner => !spawner.IsBoss)
                .ToArray();
            if (characterSpawners.Length != s_spawnPositions.Length)
            {
                throw new InvalidOperationException(
                    "World AI Manager must contain three normal character spawners.");
            }

            foreach (AICharacterSpawner characterSpawner in characterSpawners)
            {
                ValidateObjectReference(
                    characterSpawner,
                    "m_characterGameObject",
                    aiCharacterPrefab);
            }
        }

        private static void ValidateNetworkPrefabRegistration()
        {
            GameObject aiCharacterPrefab = LoadRequiredAsset<GameObject>(
                k_AICharacterPrefabPath);
            NetworkPrefabsList prefabsList =
                LoadRequiredAsset<NetworkPrefabsList>(k_NetworkPrefabsPath);
            SerializedProperty entries = GetRequiredProperty(
                new SerializedObject(prefabsList),
                "List");
            int registrationCount = 0;
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                if (entries.GetArrayElementAtIndex(entryIndex)
                        .FindPropertyRelative("Prefab")
                        ?.objectReferenceValue == aiCharacterPrefab)
                {
                    registrationCount++;
                }
            }

            if (registrationCount != 1)
            {
                throw new InvalidOperationException(
                    "Undead AI must be registered exactly once as a network prefab.");
            }
        }

        private static void ValidateWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                WorldAIManager[] managers = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<WorldAIManager>())
                    .Where(manager => manager != null)
                    .ToArray();
                NavMeshSurface surface = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<NavMeshSurface>())
                    .FirstOrDefault(candidate => candidate != null);
                if (managers.Length != 1 ||
                    surface == null ||
                    surface.navMeshData == null)
                {
                    throw new InvalidOperationException(
                        "World Scene requires one AI manager and a baked NavMeshSurface.");
                }

                foreach (AICharacterSpawner characterSpawner in
                    managers[0].GetComponentsInChildren<AICharacterSpawner>(true))
                {
                    if (!NavMesh.SamplePosition(
                            characterSpawner.transform.position,
                            out _,
                            4f,
                            NavMesh.AllAreas))
                    {
                        throw new InvalidOperationException(
                            $"{characterSpawner.name} is not near the baked NavMesh.");
                    }
                }
            }
            finally
            {
                if (openedForValidation)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateStateMotion(
            AnimatorStateMachine stateMachine,
            string stateName,
            string animationPath)
        {
            AnimatorState state = GetRequiredState(stateMachine, stateName);
            if (state.motion != LoadRequiredAsset<AnimationClip>(animationPath))
            {
                throw new InvalidOperationException(
                    $"Animator state {stateName} has the wrong animation.");
            }
        }

        private static void EditPrefab(
            string prefabPath,
            string rootName,
            Action<GameObject> configure)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    configure(root);
                    if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    {
                        throw new InvalidOperationException(
                            $"Could not save {prefabPath}.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return;
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject(rootName);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                configure(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create {prefabPath}.");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static AnimatorControllerLayer GetOrCreateLayer(
            AnimatorController controller,
            string layerName)
        {
            AnimatorControllerLayer layer = controller.layers.FirstOrDefault(
                candidate => candidate.name == layerName);
            if (layer != null)
            {
                return layer;
            }

            controller.AddLayer(layerName);
            return controller.layers.First(candidate => candidate.name == layerName);
        }

        private static AnimatorControllerLayer GetRequiredLayer(
            AnimatorController controller,
            string layerName)
        {
            return controller.layers.FirstOrDefault(layer => layer.name == layerName) ??
                throw new InvalidOperationException(
                    $"Animator Controller is missing {layerName}.");
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Vector3 position)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                stateMachine.AddState(stateName, position);
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                throw new InvalidOperationException(
                    $"Animator state {stateName} is missing.");
        }

        private static void ConfigureConditionalTransition(
            AnimatorState sourceState,
            AnimatorState destinationState,
            string parameterName)
        {
            AnimatorStateTransition transition = sourceState.transitions
                .FirstOrDefault(candidate =>
                    candidate.destinationState == destinationState) ??
                sourceState.AddTransition(destinationState);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                parameterName);
            EditorUtility.SetDirty(transition);
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

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .FirstOrDefault(candidate => candidate.name == parameterName);
            if (parameter == null)
            {
                controller.AddParameter(parameterName, parameterType);
                return;
            }

            if (parameter.type != parameterType)
            {
                controller.RemoveParameter(parameter);
                controller.AddParameter(parameterName, parameterType);
            }
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
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

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
            {
                string nextPath = $"{currentPath}/{segments[segmentIndex]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);
                }

                currentPath = nextPath;
            }
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(
            UnityEngine.Object target,
            string propertyName,
            IReadOnlyList<UnityEngine.Object> values)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Count;
            for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                property.GetArrayElementAtIndex(valueIndex).objectReferenceValue =
                    values[valueIndex];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            if (GetRequiredProperty(new SerializedObject(target), propertyName)
                    .objectReferenceValue != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{target.name}.{propertyName} is not assigned correctly.");
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"Could not find {serializedObject.targetObject.GetType().Name}." +
                    propertyName);
        }

        private static void SetRelativeObject(
            SerializedProperty parent,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName) ??
                throw new InvalidOperationException(
                    $"Network prefab entry is missing {propertyName}.");
            property.objectReferenceValue = value;
        }

        private static void SetRelativeInteger(
            SerializedProperty parent,
            string propertyName,
            long value)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName) ??
                throw new InvalidOperationException(
                    $"Network prefab entry is missing {propertyName}.");
            property.longValue = value;
        }

        private static string GetHumanoidReactionPath(string fileName)
        {
            return "Assets/Art/Animations/Characters/Humanoid/Reactions/" + fileName;
        }

        private sealed class ReactionDefinition
        {
            internal ReactionDefinition(
                string serializedPropertyName,
                string[] animationPaths)
            {
                SerializedPropertyName = serializedPropertyName;
                AnimationPaths = animationPaths;
            }

            internal string SerializedPropertyName { get; }
            internal string[] AnimationPaths { get; }
        }
    }
}
