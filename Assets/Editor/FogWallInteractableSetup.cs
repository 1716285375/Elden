using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP53 networked fog-wall interaction.</summary>
    public static class FogWallInteractableSetup
    {
        private const string k_WorldScenePath = WorldScenePathLayout.MasterScenePath;
        private const string k_WorldNetworkManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World Network Manager.prefab";
        private const string k_AnimatorControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_PassThroughClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Interactions/" +
            "core_main_fog_door_01.anim";
        private const string k_PassThroughSoundPath =
            "Assets/_Game/Audio/SFX/Characters/Movement/" +
            "SFX_Walk_Through_Fog_01.wav";
        private const string k_FogMaterialPath =
            "Assets/_Game/Art/Shared/Materials/Fallen Watcher Fog Wall.mat";
        private const string k_WorldSaveManagerPath =
            "Assets/_Game/Scripts/World/Managers/WorldSaveGameManager.cs";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_PassThroughStateName = "Pass Through Fog";
        private const string k_FogWallObjectName =
            "Fallen Watcher Fog Wall Interactable";
        private const string k_BossArenaName = "Fallen Watcher Boss Arena";
        private const string k_InteractableLayerName = "Interactable";

        [MenuItem("Tools/Elden/Configure Fog Wall Interactable")]
        public static void ConfigureFogWallInteractable()
        {
            ConfigureNetworkSceneManagement();
            ConfigurePassThroughAnimation();
            ConfigureWorldScene();
            AssetDatabase.SaveAssets();
            ValidateFogWallInteractable();
            Debug.Log(
                "[FogWallInteractableSetup] Configured an in-scene NetworkObject, " +
                "all-player RPC traversal, collision window, invulnerability, and SFX.");
        }

        [MenuItem("Tools/Elden/Validate Fog Wall Interactable")]
        public static void ValidateFogWallInteractable()
        {
            ValidateNetworkSceneManagement();
            ValidatePassThroughAnimation();
            ValidateWorldScene();
            ValidateRuntimeArchitecture();
            ValidateNetworkSceneLoading();
            Debug.Log(
                "[FogWallInteractableValidation] Scene management, scene NetworkObject, " +
                "dual colliders, non-Host RPC, animation, immunity, and audio are valid.");
        }

        private static void ConfigureNetworkSceneManagement()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldNetworkManagerPrefabPath);
            try
            {
                NetworkManager networkManager =
                    GetRequiredComponent<NetworkManager>(root);
                networkManager.NetworkConfig.EnableSceneManagement = true;
                EditorUtility.SetDirty(networkManager);
                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        k_WorldNetworkManagerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the World Network Manager prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigurePassThroughAnimation()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AnimatorControllerPath);
            AnimationClip clip = LoadRequiredAsset<AnimationClip>(
                k_PassThroughClipPath);
            AnimatorStateMachine stateMachine = GetActionStateMachine(controller);
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            AnimatorState passThroughState = FindState(
                    stateMachine,
                    k_PassThroughStateName) ??
                stateMachine.AddState(
                    k_PassThroughStateName,
                    new Vector3(950f, 360f, 0f));
            passThroughState.motion = clip;
            passThroughState.speed = 1f;
            passThroughState.writeDefaultValues = true;

            foreach (AnimatorStateTransition transition in
                passThroughState.transitions.ToArray())
            {
                passThroughState.RemoveTransition(transition);
            }

            AnimatorStateTransition returnTransition =
                passThroughState.AddTransition(emptyState);
            returnTransition.hasExitTime = true;
            returnTransition.exitTime = 0.95f;
            returnTransition.hasFixedDuration = true;
            returnTransition.duration = 0.1f;
            returnTransition.interruptionSource =
                TransitionInterruptionSource.None;

            AnimationEvent[] safeEvents = AnimationUtility.GetAnimationEvents(clip)
                .Where(animationEvent =>
                    animationEvent.functionName != "EnableIsInvulnerable" &&
                    animationEvent.functionName != "ReEnableFogWallCollision")
                .ToArray();
            AnimationUtility.SetAnimationEvents(clip, safeEvents);
            EditorUtility.SetDirty(passThroughState);
            EditorUtility.SetDirty(returnTransition);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(clip);
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
                GameObject fogWallRoot = FindRoot(scene, k_FogWallObjectName);
                if (fogWallRoot == null)
                {
                    fogWallRoot = new GameObject(k_FogWallObjectName);
                    SceneManager.MoveGameObjectToScene(fogWallRoot, scene);
                }

                ConfigureFogWallObject(fogWallRoot);
                BossArenaController arena = FindBossArena(scene);
                SerializedObject serializedArena = new SerializedObject(arena);
                GetRequiredProperty(serializedArena, "m_fogWallRoot")
                    .objectReferenceValue = fogWallRoot;
                serializedArena.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(arena);
                EditorUtility.SetDirty(arena);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        "Could not save the World Scene fog wall.");
                }
            }
            finally
            {
                if (openedForSetup && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ConfigureFogWallObject(GameObject root)
        {
            root.name = k_FogWallObjectName;
            root.transform.SetPositionAndRotation(
                new Vector3(0f, 1.5f, 12.9f),
                Quaternion.identity);
            root.transform.localScale = Vector3.one;
            root.SetActive(true);

            GetOrAddComponent<NetworkObject>(root);
            Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

            Transform visual = GetOrCreateVisual(root.transform);
            MeshRenderer renderer = GetRequiredComponent<MeshRenderer>(
                visual.gameObject);
            renderer.sharedMaterial = LoadRequiredAsset<Material>(k_FogMaterialPath);

            int interactableLayer = LayerMask.NameToLayer(k_InteractableLayerName);
            if (interactableLayer < 0)
            {
                throw new InvalidOperationException(
                    "EP52 must configure the Interactable layer before EP53.");
            }

            Transform solidColliderTransform = GetOrCreateChild(
                root.transform,
                "Fog Wall Collider");
            solidColliderTransform.localPosition = Vector3.zero;
            solidColliderTransform.localRotation = Quaternion.identity;
            solidColliderTransform.localScale = Vector3.one;
            solidColliderTransform.gameObject.layer = interactableLayer;
            BoxCollider solidCollider = GetOrAddComponent<BoxCollider>(
                solidColliderTransform.gameObject);
            solidCollider.isTrigger = false;
            solidCollider.center = Vector3.zero;
            solidCollider.size = new Vector3(18f, 3f, 0.35f);

            Transform triggerTransform = GetOrCreateChild(
                root.transform,
                "Interactable Collider");
            triggerTransform.localPosition = Vector3.zero;
            triggerTransform.localRotation = Quaternion.identity;
            triggerTransform.localScale = Vector3.one;
            triggerTransform.gameObject.layer = interactableLayer;
            BoxCollider interactableCollider = GetOrAddComponent<BoxCollider>(
                triggerTransform.gameObject);
            interactableCollider.isTrigger = true;
            interactableCollider.center = Vector3.zero;
            interactableCollider.size = new Vector3(20f, 4f, 3f);

            AudioSource audioSource = GetOrAddComponent<AudioSource>(root);
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 30f;

            FogWallInteractable fogWall =
                GetOrAddComponent<FogWallInteractable>(root);
            SerializedObject serializedFogWall = new SerializedObject(fogWall);
            SetString(serializedFogWall, "m_interactableText", "Traverse the mist");
            SetObjectReference(
                serializedFogWall,
                "m_interactableCollider",
                interactableCollider);
            SetBoolean(serializedFogWall, "m_hostOnlyInteractable", false);
            SetBoolean(
                serializedFogWall,
                "m_shouldDisableColliderAfterInteraction",
                false);
            SetObjectReference(
                serializedFogWall,
                "m_fogWallCollider",
                solidCollider);
            SetObjectArray(
                serializedFogWall,
                "m_fogWallRenderers",
                new UnityEngine.Object[] { renderer });
            SetObjectReference(serializedFogWall, "m_audioSource", audioSource);
            SetObjectReference(
                serializedFogWall,
                "m_passThroughSound",
                LoadRequiredAsset<AudioClip>(k_PassThroughSoundPath));
            SetFloat(serializedFogWall, "m_passThroughDuration", 3f);
            SetFloat(serializedFogWall, "m_maxInteractionDistance", 5f);
            serializedFogWall.ApplyModifiedPropertiesWithoutUndo();

            renderer.enabled = false;
            solidCollider.isTrigger = false;
            solidCollider.enabled = false;
            interactableCollider.enabled = false;
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(rigidbody);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(solidCollider);
            EditorUtility.SetDirty(interactableCollider);
            EditorUtility.SetDirty(audioSource);
            EditorUtility.SetDirty(fogWall);
        }

        private static Transform GetOrCreateVisual(Transform parent)
        {
            Transform visual = parent.Find("Fog Wall Visual");
            if (visual == null)
            {
                GameObject visualObject = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                visualObject.name = "Fog Wall Visual";
                UnityEngine.Object.DestroyImmediate(
                    visualObject.GetComponent<Collider>());
                visual = visualObject.transform;
                visual.SetParent(parent, false);
            }

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(18f, 3f, 0.35f);
            return visual;
        }

        private static void ValidateNetworkSceneManagement()
        {
            GameObject root = LoadRequiredAsset<GameObject>(
                k_WorldNetworkManagerPrefabPath);
            NetworkManager networkManager =
                GetRequiredComponent<NetworkManager>(root);
            if (!networkManager.NetworkConfig.EnableSceneManagement)
            {
                throw new InvalidOperationException(
                    "NetworkManager Scene Management must be enabled.");
            }
        }

        private static void ValidatePassThroughAnimation()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AnimatorControllerPath);
            AnimationClip clip = LoadRequiredAsset<AnimationClip>(
                k_PassThroughClipPath);
            AnimatorState passThroughState = GetRequiredState(
                GetActionStateMachine(controller),
                k_PassThroughStateName);
            if (passThroughState.motion != clip ||
                passThroughState.transitions.Length != 1 ||
                passThroughState.transitions[0].destinationState?.name !=
                    k_EmptyStateName ||
                AnimationUtility.GetAnimationEvents(clip).Any(animationEvent =>
                    animationEvent.functionName == "EnableIsInvulnerable" ||
                    animationEvent.functionName == "ReEnableFogWallCollision"))
            {
                throw new InvalidOperationException(
                    "Pass Through Fog must use its authored clip and return safely to Empty.");
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
                GameObject root = FindRoot(scene, k_FogWallObjectName) ??
                    throw new InvalidOperationException(
                        "The World Scene is missing its fog wall NetworkObject.");
                FogWallInteractable fogWall =
                    GetRequiredComponent<FogWallInteractable>(root);
                NetworkObject networkObject =
                    GetRequiredComponent<NetworkObject>(root);
                Rigidbody rigidbody = GetRequiredComponent<Rigidbody>(root);
                Transform solidTransform = root.transform.Find("Fog Wall Collider");
                Transform triggerTransform = root.transform.Find("Interactable Collider");
                BoxCollider solidCollider = solidTransform?.GetComponent<BoxCollider>();
                BoxCollider triggerCollider = triggerTransform?.GetComponent<BoxCollider>();
                BossArenaController arena = FindBossArena(scene);
                SerializedObject serializedFogWall = new SerializedObject(fogWall);
                SerializedObject serializedArena = new SerializedObject(arena);
                bool isPrefabObject = PrefabUtility.IsPartOfAnyPrefab(root);
                bool isRootActive = root.activeSelf;
                bool isRigidbodyValid = rigidbody.isKinematic && !rigidbody.useGravity;
                bool isAuthorityValid = !fogWall.IsHostOnlyInteractable;
                bool isSolidColliderValid = solidCollider != null &&
                    !solidCollider.isTrigger &&
                    !solidCollider.enabled;
                bool isTriggerColliderValid = triggerCollider != null &&
                    triggerCollider.isTrigger &&
                    !triggerCollider.enabled;
                bool hasSound = GetRequiredProperty(
                        serializedFogWall,
                        "m_passThroughSound")
                    .objectReferenceValue != null;
                bool isArenaLinked = GetRequiredProperty(
                        serializedArena,
                        "m_fogWallRoot")
                    .objectReferenceValue == root;
                if (isPrefabObject ||
                    networkObject == null ||
                    !isRootActive ||
                    !isRigidbodyValid ||
                    !isAuthorityValid ||
                    !isSolidColliderValid ||
                    !isTriggerColliderValid ||
                    !hasSound ||
                    !isArenaLinked)
                {
                    throw new InvalidOperationException(
                        "Fog wall validation failed: " +
                        $"Prefab={isPrefabObject}, RootActive={isRootActive}, " +
                        $"Rigidbody={isRigidbodyValid}, Authority={isAuthorityValid}, " +
                        $"Solid={isSolidColliderValid}, Trigger={isTriggerColliderValid}, " +
                        $"Sound={hasSound}, Arena={isArenaLinked}.");
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

        private static void ValidateRuntimeArchitecture()
        {
            BindingFlags privateInstance =
                BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo serverRpc = typeof(FogWallInteractable).GetMethod(
                "RequestPassThroughServerRpc",
                privateInstance);
            ServerRpcAttribute serverRpcAttribute =
                serverRpc?.GetCustomAttribute<ServerRpcAttribute>();
            MethodInfo clientRpc = typeof(FogWallInteractable).GetMethod(
                "BeginPassThroughClientRpc",
                privateInstance);
            if (!typeof(Interactable).IsAssignableFrom(typeof(FogWallInteractable)) ||
                serverRpcAttribute == null ||
                serverRpcAttribute.RequireOwnership ||
                clientRpc?.GetCustomAttribute<ClientRpcAttribute>() == null ||
                !Enum.IsDefined(
                    typeof(CharacterActionAnimation),
                    CharacterActionAnimation.PassThroughFog) ||
                typeof(CharacterManager).GetProperty("IsInvulnerable") == null ||
                typeof(CharacterManager).GetMethod("SetInvulnerable") == null)
            {
                throw new InvalidOperationException(
                    "Fog wall RPC, animation, or invulnerability contracts are incomplete.");
            }
        }

        private static void ValidateNetworkSceneLoading()
        {
            string source = File.ReadAllText(k_WorldSaveManagerPath);
            if (!source.Contains(
                    "networkManager.SceneManager.LoadScene(",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Networked Scene loads must use NetworkManager.SceneManager.LoadScene.");
            }
        }

        private static BossArenaController FindBossArena(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<BossArenaController>(true))
                .FirstOrDefault(arena => arena.gameObject.name == k_BossArenaName) ??
                throw new InvalidOperationException(
                    "The World Scene is missing the Fallen Watcher Boss Arena.");
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == objectName);
        }

        private static AnimatorStateMachine GetActionStateMachine(
            AnimatorController controller)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == k_ActionLayerName)
                {
                    return layer.stateMachine;
                }
            }

            throw new InvalidOperationException(
                $"Animator is missing the {k_ActionLayerName} layer.");
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName);
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return FindState(stateMachine, stateName) ??
                throw new InvalidOperationException(
                    $"Animator is missing state {stateName}.");
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string objectName)
        {
            Transform child = parent.Find(objectName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(objectName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
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
            return gameObject.GetComponent<T>() ??
                throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetRequiredProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
        }

        private static void SetObjectArray(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object[] values)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
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
    }
}
