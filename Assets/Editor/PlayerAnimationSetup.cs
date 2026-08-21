using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    public static class PlayerAnimationSetup
    {
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_RiggedPlayerPrefabPath =
            "Assets/Art/Models/Rigged/Characters/Humanoid/Player/Player.prefab";
        private const string k_PlayerRigObjectName = "Nephilite";
        private const string k_PlayerAvatarPath =
            "Assets/Art/Models/Rigged/Shared/Avatars/NephilitePlayerAvatar.asset";
        private const string k_LegacyPlayerModelPath =
            "Assets/Art/Models/Md_Char_Low_Poly_Man.obj";
        private const string k_ControllerFolderPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid";
        private const string k_ControllerPath =
            k_ControllerFolderPath + "/Humanoid Animator Controller.controller";
        private const string k_VisualObjectName = "Player Visual";

        private const string k_LocomotionFolderPath =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion";

        private readonly struct BlendMotion
        {
            public BlendMotion(string clipName, float horizontal, float vertical)
            {
                ClipPath = k_LocomotionFolderPath + "/" + clipName + ".anim";
                Position = new Vector2(horizontal, vertical);
            }

            public string ClipPath { get; }
            public Vector2 Position { get; }
        }

        private static readonly BlendMotion[] s_LocomotionMotions =
        {
            new BlendMotion("unarmed_main_idle_01", 0f, 0f),

            new BlendMotion("core_main_walk_F_01", 0f, 0.5f),
            new BlendMotion("core_main_walk_B_01", 0f, -0.5f),
            new BlendMotion("core_main_walk_R_01", 0.5f, 0f),
            new BlendMotion("core_main_walk_L_01", -0.5f, 0f),
            new BlendMotion("core_main_walk_FR_01", 0.5f, 0.5f),
            new BlendMotion("core_main_walk_FL_01", -0.5f, 0.5f),
            new BlendMotion("core_main_walk_BR_01", 0.5f, -0.5f),
            new BlendMotion("core_main_walk_BL_01", -0.5f, -0.5f),

            new BlendMotion("core_main_run_F_01", 0f, 1f),
            new BlendMotion("core_main_run_B_01", 0f, -1f),
            new BlendMotion("core_main_run_R_01", 1f, 0f),
            new BlendMotion("core_main_run_L_01", -1f, 0f),
            new BlendMotion("core_main_run_FR_01", 1f, 1f),
            new BlendMotion("core_main_run_FL_01", -1f, 1f),
            new BlendMotion("core_main_run_BR_01", 1f, -1f),
            new BlendMotion("core_main_run_BL_01", -1f, -1f),

            new BlendMotion("core_main_sprint_F_01", 0f, 2f)
        };

        [MenuItem("Tools/Elden/Configure Player Animation")]
        public static void ConfigurePlayerAnimation()
        {
            EnsureFolder(k_ControllerFolderPath);
            AnimatorController controller = GetOrCreateController();
            ValidateAnimatorController(controller);
            Avatar playerAvatar = ConfigureRiggedPlayerPrefab();
            ConfigurePlayerPrefab(controller, playerAvatar);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[PlayerAnimationSetup] Configured {s_LocomotionMotions.Length} unique " +
                $"Idle/Walk/Run/Sprint motions and wired {k_PlayerPrefabPath}.");
        }

        [MenuItem("Tools/Elden/Validate Player Animation")]
        public static void ValidatePlayerAnimation()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(k_ControllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException(
                    $"Could not load Animator Controller at {k_ControllerPath}.");
            }

            ValidateAnimatorController(controller);
            ValidatePlayerPrefab(controller);

            Debug.Log(
                $"[PlayerAnimationValidation] Controller, {s_LocomotionMotions.Length} " +
                "Blend Tree motions, Player prefab, and network permissions are valid.");
        }

        [MenuItem("Tools/Elden/Validate Player Animation Evaluation")]
        public static void ValidatePlayerAnimationEvaluation()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(k_ControllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException(
                    $"Could not load Animator Controller at {k_ControllerPath}.");
            }

            ValidateAnimatorController(controller);
            ValidatePlayerPrefab(controller);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_PlayerPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);

                SampleAnimator(animator, 0f, 0.5f);
                SampleAnimator(animator, 0f, 0f);
                SampleAnimator(animator, 0f, 1f);
                SampleAnimator(animator, 0f, 0f);
                SampleAnimator(animator, 0f, 2f);
                SampleAnimator(animator, 0f, 0f);

                Debug.Log(
                    "[PlayerAnimationEvaluationValidation] " +
                    "Idle/Walk/Idle/Run/Idle/Sprint/Idle Humanoid evaluation completed " +
                    "without a native crash.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static AnimatorController GetOrCreateController()
        {
            AnimatorController existingController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(k_ControllerPath);
            if (existingController != null)
            {
                EnsureLocomotionMotions(existingController);
                return existingController;
            }

            ValidateBlendMotions();

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(k_ControllerPath);
            controller.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
            controller.AddParameter("Vertical", AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = stateMachine.AddState("Locomotion One Handed");
            BlendTree blendTree = new BlendTree
            {
                name = "Locomotion One Handed",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "Horizontal",
                blendParameterY = "Vertical",
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            locomotionState.motion = blendTree;
            stateMachine.defaultState = locomotionState;

            foreach (BlendMotion blendMotion in s_LocomotionMotions)
            {
                blendTree.AddChild(LoadLocomotionClip(blendMotion), blendMotion.Position);
            }

            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Avatar ConfigureRiggedPlayerPrefab()
        {
            GameObject riggedPlayerRoot =
                PrefabUtility.LoadPrefabContents(k_RiggedPlayerPrefabPath);

            try
            {
                Animator sourceAnimator =
                    riggedPlayerRoot.GetComponentInChildren<Animator>(true);
                if (sourceAnimator == null || sourceAnimator.avatar == null)
                {
                    throw new InvalidOperationException(
                        $"{k_RiggedPlayerPrefabPath} needs a source Animator and Avatar.");
                }

                Transform rigRoot = riggedPlayerRoot.transform.Find(k_PlayerRigObjectName);
                if (rigRoot == null)
                {
                    throw new InvalidOperationException(
                        $"{k_RiggedPlayerPrefabPath} has no {k_PlayerRigObjectName} rig root.");
                }

                Avatar playerAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(k_PlayerAvatarPath);
                if (playerAvatar == null)
                {
                    playerAvatar = AvatarBuilder.BuildHumanAvatar(
                        rigRoot.gameObject,
                        sourceAnimator.avatar.humanDescription);
                    playerAvatar.name = "Nephilite Player Avatar";
                    if (!playerAvatar.isValid || !playerAvatar.isHuman)
                    {
                        UnityEngine.Object.DestroyImmediate(playerAvatar);
                        throw new InvalidOperationException(
                            "Could not build a valid Humanoid Avatar for the Nephilite rig.");
                    }

                    AssetDatabase.CreateAsset(playerAvatar, k_PlayerAvatarPath);
                }

                if (!playerAvatar.isValid || !playerAvatar.isHuman)
                {
                    throw new InvalidOperationException(
                        $"{k_PlayerAvatarPath} is not a valid Humanoid Avatar.");
                }

                RuntimeAnimatorController sourceController =
                    sourceAnimator.runtimeAnimatorController;
                bool sourceEnabled = sourceAnimator.enabled;
                AnimatorCullingMode sourceCullingMode = sourceAnimator.cullingMode;
                AnimatorUpdateMode sourceUpdateMode = sourceAnimator.updateMode;

                Animator rigAnimator = rigRoot.GetComponent<Animator>();
                if (sourceAnimator != rigAnimator)
                {
                    UnityEngine.Object.DestroyImmediate(sourceAnimator);
                    rigAnimator = rigRoot.gameObject.AddComponent<Animator>();
                }

                rigAnimator.enabled = sourceEnabled;
                rigAnimator.avatar = playerAvatar;
                rigAnimator.runtimeAnimatorController = sourceController;
                rigAnimator.cullingMode = sourceCullingMode;
                rigAnimator.updateMode = sourceUpdateMode;
                rigAnimator.applyRootMotion = false;
                EditorUtility.SetDirty(rigAnimator);

                PrefabUtility.SaveAsPrefabAsset(
                    riggedPlayerRoot,
                    k_RiggedPlayerPrefabPath);
                return playerAvatar;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(riggedPlayerRoot);
            }
        }

        private static void ConfigurePlayerPrefab(
            RuntimeAnimatorController controller,
            Avatar playerAvatar)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    GameObject riggedPlayerPrefab =
                        AssetDatabase.LoadAssetAtPath<GameObject>(k_RiggedPlayerPrefabPath);
                    if (riggedPlayerPrefab == null)
                    {
                        throw new InvalidOperationException(
                            $"Could not load rigged player prefab at {k_RiggedPlayerPrefabPath}.");
                    }

                    GameObject visualObject = (GameObject)PrefabUtility.InstantiatePrefab(
                        riggedPlayerPrefab,
                        playerRoot.transform);
                    visualObject.name = k_VisualObjectName;
                    visualObject.transform.SetLocalPositionAndRotation(
                        Vector3.zero,
                        Quaternion.identity);
                    visualObject.transform.localScale = Vector3.one;

                    animator = visualObject.GetComponentInChildren<Animator>(true);
                }

                if (animator == null || animator.avatar == null || !animator.avatar.isValid)
                {
                    throw new InvalidOperationException(
                        "The Player prefab does not contain an Animator with a valid Avatar.");
                }

                animator.avatar = playerAvatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                RemoveComponentsOnGameObject<PlayerAnimatorManager>(playerRoot);
                PlayerAnimatorManager animatorManager =
                    GetOrAddComponent<PlayerAnimatorManager>(animator.gameObject);
                PlayerManager playerManager = playerRoot.GetComponent<PlayerManager>();
                if (playerManager != null)
                {
                    SerializedObject serializedPlayerManager = new SerializedObject(playerManager);
                    SerializedProperty animatorManagerProperty =
                        serializedPlayerManager.FindProperty("m_playerAnimatorManager");
                    animatorManagerProperty.objectReferenceValue = animatorManager;
                    serializedPlayerManager.ApplyModifiedPropertiesWithoutUndo();
                }
                DisableLegacyRenderers(playerRoot);
                EditorUtility.SetDirty(animator);

                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateAnimatorController(AnimatorController controller)
        {
            ValidateBlendMotions();
            ValidateFloatParameter(controller, "Horizontal");
            ValidateFloatParameter(controller, "Vertical");

            if (controller.layers.Length == 0)
            {
                throw new InvalidOperationException("The Animator Controller has no layers.");
            }

            AnimatorState defaultState = controller.layers[0].stateMachine.defaultState;
            if (defaultState == null || defaultState.name != "Locomotion One Handed")
            {
                throw new InvalidOperationException(
                    "Layer 0 must default to Locomotion One Handed.");
            }

            if (!(defaultState.motion is BlendTree blendTree) ||
                blendTree.blendType != BlendTreeType.FreeformDirectional2D ||
                blendTree.blendParameter != "Horizontal" ||
                blendTree.blendParameterY != "Vertical")
            {
                throw new InvalidOperationException(
                    "Locomotion One Handed must use a Horizontal/Vertical " +
                    "FreeformDirectional2D Blend Tree.");
            }

            ChildMotion[] children = blendTree.children;
            if (children.Length != s_LocomotionMotions.Length)
            {
                throw new InvalidOperationException(
                    $"Expected {s_LocomotionMotions.Length} Blend Tree motions, " +
                    $"but found {children.Length}.");
            }

            HashSet<Vector2> actualPositions = new HashSet<Vector2>();
            foreach (ChildMotion child in children)
            {
                if (child.motion == null || !actualPositions.Add(child.position))
                {
                    throw new InvalidOperationException(
                        "The Blend Tree contains a null motion or duplicate position.");
                }
            }

            foreach (BlendMotion expectedMotion in s_LocomotionMotions)
            {
                ChildMotion? matchingChild = null;
                foreach (ChildMotion child in children)
                {
                    if (child.position == expectedMotion.Position)
                    {
                        matchingChild = child;
                        break;
                    }
                }

                if (!matchingChild.HasValue)
                {
                    throw new InvalidOperationException(
                        $"The Blend Tree is missing position {expectedMotion.Position}.");
                }

                string actualClipPath =
                    AssetDatabase.GetAssetPath(matchingChild.Value.motion);
                if (actualClipPath != expectedMotion.ClipPath)
                {
                    throw new InvalidOperationException(
                        $"Position {expectedMotion.Position} should use " +
                        $"{expectedMotion.ClipPath}, but uses {actualClipPath}.");
                }
            }
        }

        private static void ValidatePlayerPrefab(RuntimeAnimatorController controller)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                PlayerNetworkManager networkManager =
                    playerRoot.GetComponent<PlayerNetworkManager>();
                if (networkManager == null)
                {
                    throw new InvalidOperationException(
                        "The Player prefab is missing PlayerNetworkManager.");
                }

                ValidateNetworkVariable(networkManager.HorizontalMovement, "HorizontalMovement");
                ValidateNetworkVariable(networkManager.VerticalMovement, "VerticalMovement");
                ValidateNetworkVariable(networkManager.MoveAmount, "MoveAmount");
                ValidateNetworkVariable(networkManager.IsSprinting, "IsSprinting");

                PlayerLocomotionManager locomotionManager =
                    playerRoot.GetComponent<PlayerLocomotionManager>();
                ValidateSprintingSpeed(locomotionManager);

                Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null ||
                    !animator.avatar.isValid || !animator.avatar.isHuman)
                {
                    throw new InvalidOperationException(
                        "The Player prefab needs an Animator with a valid Humanoid Avatar.");
                }

                PlayerAnimatorManager animatorManager =
                    animator.GetComponent<PlayerAnimatorManager>();
                if (animatorManager == null)
                {
                    throw new InvalidOperationException(
                        "The Player Animator is missing PlayerAnimatorManager.");
                }

                Animator[] animators = playerRoot.GetComponentsInChildren<Animator>(true);
                if (animators.Length != 1 || animator.gameObject.name != k_PlayerRigObjectName)
                {
                    throw new InvalidOperationException(
                        $"The Player prefab must have exactly one Animator on the " +
                        $"{k_PlayerRigObjectName} rig root, not on a multi-rig parent.");
                }

                Avatar expectedAvatar =
                    AssetDatabase.LoadAssetAtPath<Avatar>(k_PlayerAvatarPath);
                if (expectedAvatar == null || animator.avatar != expectedAvatar)
                {
                    throw new InvalidOperationException(
                        $"The Player Animator is not using {k_PlayerAvatarPath}.");
                }

                ValidateUniqueHumanBones(animator);

                if (animator.runtimeAnimatorController != controller)
                {
                    throw new InvalidOperationException(
                        "The Player Animator is not using the generated controller.");
                }

                if (animator.applyRootMotion)
                {
                    throw new InvalidOperationException(
                        "Root motion must remain disabled for code-driven movement.");
                }

                bool foundLegacyRenderer = false;
                foreach (Renderer renderer in playerRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (!IsLegacyPlayerRenderer(renderer))
                    {
                        continue;
                    }

                    foundLegacyRenderer = true;
                    if (renderer.enabled)
                    {
                        throw new InvalidOperationException(
                            $"Legacy renderer {renderer.name} is still enabled.");
                    }
                }

                if (!foundLegacyRenderer)
                {
                    throw new InvalidOperationException(
                        $"Could not find the renderer inherited from {k_LegacyPlayerModelPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateFloatParameter(
            AnimatorController controller,
            string parameterName)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == parameterName &&
                    parameter.type == AnimatorControllerParameterType.Float)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"The Animator Controller is missing float parameter {parameterName}.");
        }

        private static void ValidateNetworkVariable(
            NetworkVariable<float> variable,
            string variableName)
        {
            if (variable.ReadPerm != NetworkVariableReadPermission.Everyone ||
                variable.WritePerm != NetworkVariableWritePermission.Owner)
            {
                throw new InvalidOperationException(
                    $"{variableName} must be readable by Everyone and writable by Owner.");
            }
        }

        private static void ValidateNetworkVariable(
            NetworkVariable<bool> variable,
            string variableName)
        {
            if (variable.ReadPerm != NetworkVariableReadPermission.Everyone ||
                variable.WritePerm != NetworkVariableWritePermission.Owner)
            {
                throw new InvalidOperationException(
                    $"{variableName} must be readable by Everyone and writable by Owner.");
            }
        }

        private static void ValidateSprintingSpeed(PlayerLocomotionManager locomotionManager)
        {
            if (locomotionManager == null)
            {
                throw new InvalidOperationException(
                    "The Player prefab is missing PlayerLocomotionManager.");
            }

            SerializedObject serializedLocomotion = new SerializedObject(locomotionManager);
            SerializedProperty runningSpeed = serializedLocomotion.FindProperty("m_runningSpeed");
            SerializedProperty sprintingSpeed = serializedLocomotion.FindProperty("m_sprintingSpeed");
            if (runningSpeed == null ||
                sprintingSpeed == null ||
                sprintingSpeed.floatValue <= runningSpeed.floatValue)
            {
                throw new InvalidOperationException(
                    "The Player sprinting speed must be greater than its running speed.");
            }
        }

        private static void DisableLegacyRenderers(GameObject playerRoot)
        {
            foreach (Renderer renderer in playerRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (IsLegacyPlayerRenderer(renderer))
                {
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        private static bool IsLegacyPlayerRenderer(Renderer renderer)
        {
            Renderer originalRenderer =
                PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
            return originalRenderer != null &&
                AssetDatabase.GetAssetPath(originalRenderer) == k_LegacyPlayerModelPath;
        }

        private static void ValidateBlendMotions()
        {
            HashSet<Vector2> positions = new HashSet<Vector2>();
            foreach (BlendMotion blendMotion in s_LocomotionMotions)
            {
                if (!positions.Add(blendMotion.Position))
                {
                    throw new InvalidOperationException(
                        $"Duplicate Blend Tree position {blendMotion.Position}.");
                }
            }
        }

        private static void EnsureLocomotionMotions(AnimatorController controller)
        {
            if (controller.layers.Length == 0 ||
                !(controller.layers[0].stateMachine.defaultState?.motion is BlendTree blendTree))
            {
                return;
            }

            foreach (BlendMotion blendMotion in s_LocomotionMotions)
            {
                AnimationClip expectedClip = LoadLocomotionClip(blendMotion);
                ChildMotion[] children = blendTree.children;
                bool hasPosition = false;
                for (int index = 0; index < children.Length; index++)
                {
                    if (children[index].position != blendMotion.Position)
                    {
                        continue;
                    }

                    hasPosition = true;
                    if (children[index].motion != expectedClip)
                    {
                        ChildMotion correctedChild = children[index];
                        correctedChild.motion = expectedClip;
                        children[index] = correctedChild;
                        blendTree.children = children;
                    }

                    break;
                }

                if (!hasPosition)
                {
                    blendTree.AddChild(expectedClip, blendMotion.Position);
                }
            }

            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
        }

        private static AnimationClip LoadLocomotionClip(BlendMotion blendMotion)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(blendMotion.ClipPath);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"Could not load locomotion clip at {blendMotion.ClipPath}.");
            }

            return clip;
        }

        private static void ValidateUniqueHumanBones(Animator animator)
        {
            Dictionary<string, int> transformNameCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Transform transform in
                animator.GetComponentsInChildren<Transform>(true))
            {
                transformNameCounts.TryGetValue(transform.name, out int count);
                transformNameCounts[transform.name] = count + 1;
            }

            foreach (HumanBone humanBone in animator.avatar.humanDescription.human)
            {
                if (!transformNameCounts.TryGetValue(humanBone.boneName, out int count) ||
                    count != 1)
                {
                    throw new InvalidOperationException(
                        $"Humanoid bone {humanBone.boneName} must resolve exactly once " +
                        $"below {animator.gameObject.name}, but resolved {count} times.");
                }
            }
        }

        private static void SampleAnimator(
            Animator animator,
            float horizontal,
            float vertical)
        {
            const float k_DeltaTime = 0.1f;
            animator.SetFloat("Horizontal", horizontal, 0.1f, k_DeltaTime);
            animator.SetFloat("Vertical", vertical, 0.1f, k_DeltaTime);
            animator.Update(k_DeltaTime);
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];

            for (int index = 1; index < parts.Length; index++)
            {
                string nextPath = currentPath + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[index]);
                }

                currentPath = nextPath;
            }
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void RemoveComponentsOnGameObject<T>(GameObject gameObject) where T : Component
        {
            foreach (T component in gameObject.GetComponents<T>())
            {
                UnityEngine.Object.DestroyImmediate(component, true);
            }
        }
    }
}
