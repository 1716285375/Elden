using System;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the reusable world assets required by EP157-158.</summary>
    public static class DoorSystemSetup
    {
        private const string k_PrefabFolder =
            "Assets/Data/Prefabs/World Objects/Doors";
        private const string k_AnimationFolder =
            "Assets/Data/Animations/Environment/Doors";
        private const string k_KeyItemFolder =
            "Assets/Data/Items/Key Items";
        private const string k_DoorPrefabPath =
            k_PrefabFolder + "/Dungeon Door.prefab";
        private const string k_LockedDoorPrefabPath =
            k_PrefabFolder + "/Locked Dungeon Door.prefab";
        private const string k_GatePrefabPath =
            k_PrefabFolder + "/Lever Gate.prefab";
        private const string k_DoorControllerPath =
            k_AnimationFolder + "/Door.controller";
        private const string k_GateControllerPath =
            k_AnimationFolder + "/Gate.controller";
        private const string k_LeverControllerPath =
            k_AnimationFolder + "/Gate Lever.controller";
        private const string k_KeyItemPath =
            k_KeyItemFolder + "/Old Dungeon Key.asset";
        private const string k_KeyPickupPath =
            "Assets/Data/Prefabs/Interactables/Old Dungeon Key Pickup.prefab";
        private const string k_ItemPickupTemplatePath =
            "Assets/Data/Prefabs/Interactables/Item Pickup.prefab";
        private const string k_ItemDatabasePath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";

        [MenuItem("Tools/Elden/Configure Door System")]
        public static void ConfigureDoorSystem()
        {
            EnsureFolder(k_PrefabFolder);
            EnsureFolder(k_AnimationFolder);
            EnsureFolder(k_KeyItemFolder);

            AnimatorController doorController = ConfigureDoorController();
            AnimatorController gateController = ConfigureGateController();
            AnimatorController leverController = ConfigureLeverController();
            KeyItem key = ConfigureKeyItem();
            ConfigureItemDatabase(key);
            ConfigureKeyPickup(key);
            ConfigureDoorPrefab(
                k_DoorPrefabPath,
                "Dungeon Door",
                doorController,
                false,
                null);
            ConfigureDoorPrefab(
                k_LockedDoorPrefabPath,
                "Locked Dungeon Door",
                doorController,
                true,
                key);
            ConfigureGatePrefab(gateController, leverController);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateDoorSystem();
            Debug.Log(
                "[DoorSystemSetup] Configured EP157-158 doors, locked door, " +
                "key pickup, reverse-side message, and lever gate.");
        }

        [MenuItem("Tools/Elden/Validate Door System")]
        public static void ValidateDoorSystem()
        {
            KeyItem key = LoadRequiredAsset<KeyItem>(k_KeyItemPath);
            ValidateItemDatabase(key);
            ValidateDoorPrefab(k_DoorPrefabPath, false, null);
            ValidateDoorPrefab(k_LockedDoorPrefabPath, true, key);
            ValidateGatePrefab();
            ValidateController(
                LoadRequiredAsset<AnimatorController>(k_DoorControllerPath),
                "Empty",
                "DoorOpen",
                "DoorOpened");
            ValidateController(
                LoadRequiredAsset<AnimatorController>(k_GateControllerPath),
                "Empty",
                "GateOpen",
                "GateOpened");
            ValidateController(
                LoadRequiredAsset<AnimatorController>(k_LeverControllerPath),
                "Empty",
                "LeverPull",
                "LeverPulled",
                "LeverReset");
            Debug.Log(
                "[DoorSystemSetup] EP157-158 door system validation passed.");
        }

        private static AnimatorController ConfigureDoorController()
        {
            AnimationClip open = ConfigureClip(
                k_AnimationFolder + "/Door Open.anim",
                "localEulerAnglesRaw.y",
                0f,
                -95f,
                1.2f);
            AnimationClip opened = ConfigureClip(
                k_AnimationFolder + "/Door Opened.anim",
                "localEulerAnglesRaw.y",
                -95f,
                -95f,
                0.01f);
            return ConfigureController(
                k_DoorControllerPath,
                ("DoorOpen", open),
                ("DoorOpened", opened));
        }

        private static AnimatorController ConfigureGateController()
        {
            AnimationClip open = ConfigureClip(
                k_AnimationFolder + "/Gate Open.anim",
                "m_LocalPosition.y",
                0f,
                3.4f,
                1.6f);
            AnimationClip opened = ConfigureClip(
                k_AnimationFolder + "/Gate Opened.anim",
                "m_LocalPosition.y",
                3.4f,
                3.4f,
                0.01f);
            return ConfigureController(
                k_GateControllerPath,
                ("GateOpen", open),
                ("GateOpened", opened));
        }

        private static AnimatorController ConfigureLeverController()
        {
            AnimationClip pull = ConfigureClip(
                k_AnimationFolder + "/Gate Lever Pull.anim",
                "localEulerAnglesRaw.z",
                0f,
                -55f,
                0.4f);
            AnimationClip pulled = ConfigureClip(
                k_AnimationFolder + "/Gate Lever Pulled.anim",
                "localEulerAnglesRaw.z",
                -55f,
                -55f,
                0.01f);
            AnimationClip reset = ConfigureClip(
                k_AnimationFolder + "/Gate Lever Reset.anim",
                "localEulerAnglesRaw.z",
                -55f,
                0f,
                0.4f);
            return ConfigureController(
                k_LeverControllerPath,
                ("LeverPull", pull),
                ("LeverPulled", pulled),
                ("LeverReset", reset));
        }

        private static AnimationClip ConfigureClip(
            string assetPath,
            string propertyName,
            float startValue,
            float endValue,
            float duration)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                assetPath);
            if (clip == null)
            {
                clip = new AnimationClip
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath)
                };
                AssetDatabase.CreateAsset(clip, assetPath);
            }

            clip.ClearCurves();
            AnimationCurve curve = AnimationCurve.EaseInOut(
                0f,
                startValue,
                Mathf.Max(0.01f, duration),
                endValue);
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    string.Empty,
                    typeof(Transform),
                    propertyName),
                curve);
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController ConfigureController(
            string controllerPath,
            params (string Name, AnimationClip Clip)[] states)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(
                    controllerPath);
            }

            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;
            AnimatorState emptyState = GetOrCreateState(
                stateMachine,
                "Empty");
            emptyState.motion = null;
            foreach ((string stateName, AnimationClip clip) in states)
            {
                AnimatorState state = GetOrCreateState(
                    stateMachine,
                    stateName);
                state.motion = clip;
                foreach (AnimatorStateTransition transition in state.transitions)
                {
                    state.RemoveTransition(transition);
                }
            }

            stateMachine.defaultState = emptyState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                stateMachine.AddState(stateName);
        }

        private static KeyItem ConfigureKeyItem()
        {
            KeyItem key = AssetDatabase.LoadAssetAtPath<KeyItem>(k_KeyItemPath);
            if (key == null)
            {
                key = ScriptableObject.CreateInstance<KeyItem>();
                key.name = "Old Dungeon Key";
                AssetDatabase.CreateAsset(key, k_KeyItemPath);
            }

            SerializedObject serializedKey = new(key);
            GetRequiredProperty(serializedKey, "m_itemName").stringValue =
                "Old Dungeon Key";
            GetRequiredProperty(serializedKey, "m_itemDescription").stringValue =
                "A timeworn key for an old dungeon door.";
            GetRequiredProperty(serializedKey, "m_maxItemAmount").intValue = 1;
            GetRequiredProperty(serializedKey, "m_currentItemAmount").intValue = 1;
            serializedKey.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(key);
            return key;
        }

        private static void ConfigureItemDatabase(KeyItem key)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_ItemDatabasePath);
            try
            {
                WorldItemDatabase database =
                    root.GetComponent<WorldItemDatabase>();
                SerializedObject serializedDatabase = new(database);
                AddUniqueObjectReference(
                    GetRequiredProperty(serializedDatabase, "m_keys"),
                    key);
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                database.RefreshItemCatalog();
                EditorUtility.SetDirty(database);
                PrefabUtility.SaveAsPrefabAsset(root, k_ItemDatabasePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureKeyPickup(KeyItem key)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_ItemPickupTemplatePath);
            try
            {
                root.name = "Old Dungeon Key Pickup";
                PickupItemInteractable pickup =
                    root.GetComponent<PickupItemInteractable>();
                SerializedObject serializedPickup = new(pickup);
                GetRequiredProperty(serializedPickup, "m_item")
                    .objectReferenceValue = key;
                GetRequiredProperty(serializedPickup, "m_itemID").intValue =
                    key.ItemID;
                GetRequiredProperty(serializedPickup, "m_pickupType")
                    .enumValueIndex = (int)ItemPickupType.WorldSpawn;
                serializedPickup.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_KeyPickupPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureDoorPrefab(
            string prefabPath,
            string rootName,
            RuntimeAnimatorController controller,
            bool requiresItem,
            Item requiredItem)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(
                    rootName,
                    typeof(NetworkObject),
                    typeof(Rigidbody),
                    typeof(AudioSource));
            try
            {
                root.name = rootName;
                EnsureNetworkPhysicsRoot(root);
                AudioSource audioSource = GetOrAddComponent<AudioSource>(root);
                ConfigureSpatialAudio(audioSource);
                DoorInteractable door = GetOrAddComponent<DoorInteractable>(root);
                MessageInteractable reverseMessage =
                    GetOrAddComponent<MessageInteractable>(root);

                Transform doorPivot = GetOrCreateChild(
                    root.transform,
                    "Door Pivot");
                doorPivot.localPosition = new Vector3(-1.25f, 0f, 0f);
                Animator animator = GetOrAddComponent<Animator>(
                    doorPivot.gameObject);
                animator.runtimeAnimatorController = controller;
                Transform panel = GetOrCreatePrimitive(
                    doorPivot,
                    "Door Panel",
                    PrimitiveType.Cube);
                panel.localPosition = new Vector3(1.25f, 1.5f, 0f);
                panel.localScale = new Vector3(2.5f, 3f, 0.25f);

                BoxCollider frontCollider = ConfigureInteractionCollider(
                    root.transform,
                    "Open Door Interaction",
                    new Vector3(0f, 1.3f, 1.05f),
                    new Vector3(2.8f, 2.6f, 0.9f));
                BoxCollider reverseCollider = ConfigureInteractionCollider(
                    root.transform,
                    "Reverse Side Message",
                    new Vector3(0f, 1.3f, -1.05f),
                    new Vector3(2.8f, 2.6f, 0.9f));

                SerializedObject serializedDoor = new(door);
                SetBaseInteractionProperties(
                    serializedDoor,
                    "Open",
                    frontCollider,
                    false,
                    true);
                GetRequiredProperty(serializedDoor, "m_doorAnimator")
                    .objectReferenceValue = animator;
                GetRequiredProperty(serializedDoor, "m_openAnimationState")
                    .stringValue = "DoorOpen";
                GetRequiredProperty(serializedDoor, "m_openedAnimationState")
                    .stringValue = "DoorOpened";
                GetRequiredProperty(serializedDoor, "m_audioSource")
                    .objectReferenceValue = audioSource;
                GetRequiredProperty(serializedDoor, "m_requiresItem")
                    .boolValue = requiresItem;
                GetRequiredProperty(serializedDoor, "m_itemRequiredToOpen")
                    .objectReferenceValue = requiredItem;
                SetObjectReferenceArray(
                    GetRequiredProperty(
                        serializedDoor,
                        "m_interactionsToDisable"),
                    reverseMessage);
                serializedDoor.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedMessage = new(reverseMessage);
                SetBaseInteractionProperties(
                    serializedMessage,
                    "Examine",
                    reverseCollider,
                    false,
                    false);
                GetRequiredProperty(serializedMessage, "m_message").stringValue =
                    "Cannot open from this side.";
                serializedMessage.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                ReleasePrefabContents(prefab, root);
            }
        }

        private static void ConfigureGatePrefab(
            RuntimeAnimatorController gateController,
            RuntimeAnimatorController leverController)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_GatePrefabPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(k_GatePrefabPath)
                : new GameObject(
                    "Lever Gate",
                    typeof(NetworkObject),
                    typeof(Rigidbody),
                    typeof(AudioSource));
            try
            {
                root.name = "Lever Gate";
                EnsureNetworkPhysicsRoot(root);
                AudioSource audioSource = GetOrAddComponent<AudioSource>(root);
                ConfigureSpatialAudio(audioSource);
                DoorInteractable gate = GetOrAddComponent<DoorInteractable>(root);
                ActivateOtherInteractableInteractable lever =
                    GetOrAddComponent<ActivateOtherInteractableInteractable>(root);

                Transform gatePivot = GetOrCreateChild(
                    root.transform,
                    "Gate Pivot");
                Animator gateAnimator = GetOrAddComponent<Animator>(
                    gatePivot.gameObject);
                gateAnimator.runtimeAnimatorController = gateController;
                Transform gateVisual = GetOrCreatePrimitive(
                    gatePivot,
                    "Gate Visual",
                    PrimitiveType.Cube);
                gateVisual.localPosition = new Vector3(0f, 1.7f, 0f);
                gateVisual.localScale = new Vector3(4f, 3.4f, 0.35f);

                Transform leverStand = GetOrCreatePrimitive(
                    root.transform,
                    "Lever Stand",
                    PrimitiveType.Cube);
                leverStand.localPosition = new Vector3(3.1f, 0.7f, 0f);
                leverStand.localScale = new Vector3(0.6f, 1.4f, 0.6f);
                Transform leverPivot = GetOrCreateChild(
                    root.transform,
                    "Lever Pivot");
                leverPivot.localPosition = new Vector3(3.1f, 1.35f, 0f);
                Animator leverAnimator = GetOrAddComponent<Animator>(
                    leverPivot.gameObject);
                leverAnimator.runtimeAnimatorController = leverController;
                Transform handle = GetOrCreatePrimitive(
                    leverPivot,
                    "Lever Handle",
                    PrimitiveType.Cube);
                handle.localPosition = new Vector3(0f, 0.45f, 0f);
                handle.localScale = new Vector3(0.12f, 0.9f, 0.12f);
                RemoveCollider(handle.gameObject);
                BoxCollider leverCollider = ConfigureInteractionCollider(
                    root.transform,
                    "Lever Interaction",
                    new Vector3(3.1f, 1.1f, 0.8f),
                    new Vector3(1.4f, 2.2f, 1.4f));

                SerializedObject serializedGate = new(gate);
                SetBaseInteractionProperties(
                    serializedGate,
                    string.Empty,
                    null,
                    false,
                    true);
                GetRequiredProperty(serializedGate, "m_doorAnimator")
                    .objectReferenceValue = gateAnimator;
                GetRequiredProperty(serializedGate, "m_openAnimationState")
                    .stringValue = "GateOpen";
                GetRequiredProperty(serializedGate, "m_openedAnimationState")
                    .stringValue = "GateOpened";
                GetRequiredProperty(serializedGate, "m_audioSource")
                    .objectReferenceValue = audioSource;
                SetObjectReferenceArray(
                    GetRequiredProperty(
                        serializedGate,
                        "m_interactionsToDisable"),
                    lever);
                serializedGate.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedLever = new(lever);
                SetBaseInteractionProperties(
                    serializedLever,
                    "Pull Lever",
                    leverCollider,
                    false,
                    false);
                GetRequiredProperty(
                    serializedLever,
                    "m_interactableToActivate").objectReferenceValue = gate;
                GetRequiredProperty(serializedLever, "m_useOnce").boolValue =
                    true;
                GetRequiredProperty(serializedLever, "m_leverAnimator")
                    .objectReferenceValue = leverAnimator;
                serializedLever.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_GatePrefabPath);
            }
            finally
            {
                ReleasePrefabContents(prefab, root);
            }
        }

        private static void ValidateDoorPrefab(
            string prefabPath,
            bool requiresItem,
            Item requiredItem)
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(prefabPath);
            DoorInteractable door = prefab.GetComponent<DoorInteractable>();
            MessageInteractable message = prefab.GetComponent<MessageInteractable>();
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            SerializedObject serializedDoor = new(door);
            if (door == null ||
                message == null ||
                prefab.GetComponent<NetworkObject>() == null ||
                prefab.GetComponent<Rigidbody>() == null ||
                door.InteractableCollider == null ||
                !door.InteractableCollider.isTrigger ||
                message.InteractableCollider == null ||
                !message.InteractableCollider.isTrigger ||
                !colliders.Any(collider => !collider.isTrigger) ||
                door.RequiresItem != requiresItem ||
                GetRequiredProperty(
                    serializedDoor,
                    "m_itemRequiredToOpen").objectReferenceValue != requiredItem)
            {
                throw new InvalidOperationException(
                    $"Door prefab is incomplete: {prefabPath}");
            }
        }

        private static void ValidateGatePrefab()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(k_GatePrefabPath);
            DoorInteractable gate = prefab.GetComponent<DoorInteractable>();
            ActivateOtherInteractableInteractable lever = prefab.GetComponent<
                ActivateOtherInteractableInteractable>();
            if (gate == null ||
                lever == null ||
                prefab.GetComponents<NetworkObject>().Length != 1 ||
                gate.InteractableCollider != null ||
                lever.InteractableCollider == null ||
                !lever.InteractableCollider.isTrigger ||
                lever.InteractableToActivate != gate ||
                !lever.UseOnce)
            {
                throw new InvalidOperationException(
                    "Lever Gate must share one NetworkObject and expose only " +
                    "the lever interaction.");
            }
        }

        private static void ValidateItemDatabase(KeyItem key)
        {
            GameObject databasePrefab = LoadRequiredAsset<GameObject>(
                k_ItemDatabasePath);
            WorldItemDatabase database =
                databasePrefab.GetComponent<WorldItemDatabase>();
            GameObject pickupPrefab = LoadRequiredAsset<GameObject>(
                k_KeyPickupPath);
            PickupItemInteractable pickup =
                pickupPrefab.GetComponent<PickupItemInteractable>();
            if (database == null ||
                key.ItemID < 0 ||
                database.GetKeyByID(key.ItemID) != key ||
                pickup == null ||
                pickup.Item != key ||
                pickup.ItemID != key.ItemID)
            {
                throw new InvalidOperationException(
                    "Old Dungeon Key must be cataloged and available as a pickup.");
            }
        }

        private static void ValidateController(
            AnimatorController controller,
            params string[] requiredStates)
        {
            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;
            string[] stateNames = stateMachine.states
                .Select(childState => childState.state.name)
                .ToArray();
            bool hasTransition = stateMachine.states.Any(
                childState => childState.state.transitions.Length > 0);
            if (requiredStates.Any(stateName => !stateNames.Contains(stateName)) ||
                stateMachine.defaultState?.name != "Empty" ||
                hasTransition)
            {
                throw new InvalidOperationException(
                    $"{controller.name} must use Empty plus direct-play states.");
            }
        }

        private static void EnsureNetworkPhysicsRoot(GameObject root)
        {
            GetOrAddComponent<NetworkObject>(root);
            Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            GameObjectUtility.SetStaticEditorFlags(root, 0);
        }

        private static void ConfigureSpatialAudio(AudioSource audioSource)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.maxDistance = 16f;
        }

        private static BoxCollider ConfigureInteractionCollider(
            Transform parent,
            string childName,
            Vector3 center,
            Vector3 size)
        {
            Transform child = GetOrCreateChild(parent, childName);
            child.localPosition = center;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            int interactionLayer = LayerMask.NameToLayer("Interactable");
            child.gameObject.layer = interactionLayer >= 0
                ? interactionLayer
                : parent.gameObject.layer;
            BoxCollider collider = GetOrAddComponent<BoxCollider>(
                child.gameObject);
            collider.center = Vector3.zero;
            collider.size = size;
            collider.isTrigger = true;
            return collider;
        }

        private static void SetBaseInteractionProperties(
            SerializedObject serializedInteractable,
            string prompt,
            Collider interactionCollider,
            bool hostOnly,
            bool disableAfterInteraction)
        {
            GetRequiredProperty(serializedInteractable, "m_interactableText")
                .stringValue = prompt;
            GetRequiredProperty(
                serializedInteractable,
                "m_interactableCollider").objectReferenceValue =
                    interactionCollider;
            GetRequiredProperty(
                serializedInteractable,
                "m_autoDiscoverInteractableCollider").boolValue = false;
            GetRequiredProperty(
                serializedInteractable,
                "m_hostOnlyInteractable").boolValue = hostOnly;
            GetRequiredProperty(
                serializedInteractable,
                "m_shouldDisableColliderAfterInteraction").boolValue =
                    disableAfterInteraction;
        }

        private static void AddUniqueObjectReference(
            SerializedProperty arrayProperty,
            UnityEngine.Object value)
        {
            for (int index = 0; index < arrayProperty.arraySize; index++)
            {
                if (arrayProperty.GetArrayElementAtIndex(index)
                        .objectReferenceValue == value)
                {
                    return;
                }
            }

            int newIndex = arrayProperty.arraySize;
            arrayProperty.InsertArrayElementAtIndex(newIndex);
            arrayProperty.GetArrayElementAtIndex(newIndex)
                .objectReferenceValue = value;
        }

        private static void SetObjectReferenceArray(
            SerializedProperty arrayProperty,
            params UnityEngine.Object[] values)
        {
            arrayProperty.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                arrayProperty.GetArrayElementAtIndex(index)
                    .objectReferenceValue = values[index];
            }
        }

        private static Transform GetOrCreatePrimitive(
            Transform parent,
            string childName,
            PrimitiveType primitiveType)
        {
            Transform existing = FindDirectChild(parent, childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = childName;
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string childName)
        {
            Transform existing = FindDirectChild(parent, childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = new(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform child = parent.GetChild(childIndex);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            if (component == null)
            {
                throw new InvalidOperationException(
                    $"Failed to add {typeof(T).Name} to {gameObject.name}.");
            }

            return component;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void ReleasePrefabContents(
            GameObject prefab,
            GameObject root)
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

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int segmentIndex = 1;
                segmentIndex < segments.Length;
                segmentIndex++)
            {
                string nextPath = currentPath + "/" + segments[segmentIndex];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(
                        currentPath,
                        segments[segmentIndex]);
                }

                currentPath = nextPath;
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.name} is missing " +
                    $"{propertyName}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }
    }
}
