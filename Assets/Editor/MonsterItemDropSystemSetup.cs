using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP76 server-authoritative monster drop loop.</summary>
    public static class MonsterItemDropSystemSetup
    {
        private const string k_DatabasePrefabPath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";
        private const string k_PickupPrefabPath =
            "Assets/Data/Prefabs/Interactables/Item Pickup.prefab";
        private const string k_UndeadPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_NetworkPrefabsPath =
            "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset";
        private const string k_BroadswordPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Broadsword.asset";
        private const string k_ArmorPath =
            "Assets/Data/Items/Armor/Starter Armor.asset";
        private const string k_DropSoundPath =
            "Assets/Art/Audio/SFX/General/SFX_Pick_Up_Rare_Item_01.wav";
        private const string k_PlayerControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_PickupAnimationPath =
            "Assets/Art/Animations/Characters/Humanoid/Interactions/" +
            "core_item_pickup_mid_01.anim";
        private const string k_ActionLayerName = "Action Override";
        private const string k_PickupStateName = "Pickup_Item_01";
        private const string k_EmptyStateName = "Empty";

        private static readonly string[] s_aiPrefabPaths =
        {
            k_UndeadPrefabPath,
            k_BossPrefabPath
        };

        [MenuItem("Tools/Elden/Configure Monster Item Drop System")]
        public static void ConfigureMonsterItemDropSystem()
        {
            ConfigureItemDatabase();
            ConfigurePickupPrefab();
            ConfigureAIInventories();
            ConfigurePickupPopupIcon();
            ConfigurePickupAnimation();
            RegisterNetworkPrefab();
            AssetDatabase.SaveAssets();
            ValidateMonsterItemDropSystem();
            Debug.Log(
                "[MonsterItemDropSystemSetup] Configured server loot rolls, " +
                "replicated corpse-tracked pickups, and atomic multiplayer collection.");
        }

        [MenuItem("Tools/Elden/Validate Monster Item Drop System")]
        public static void ValidateMonsterItemDropSystem()
        {
            ValidateRuntimeContracts();
            ValidateItemDatabase();
            ValidatePickupPrefab();
            ValidateAIInventories();
            ValidatePickupPopupIcon();
            ValidatePickupAnimation();
            ValidateNetworkPrefabRegistration();
            Debug.Log(
                "[MonsterItemDropSystemValidation] EP76 data, network authority, " +
                "late-join state, presentation, and loot configuration are valid.");
        }

        private static void ConfigureItemDatabase()
        {
            EditPrefab(
                k_DatabasePrefabPath,
                root =>
                {
                    WorldItemDatabase database =
                        GetRequiredComponent<WorldItemDatabase>(root);
                    SetObjectReference(
                        database,
                        "m_creatureDropPickupPrefab",
                        LoadRequiredAsset<GameObject>(k_PickupPrefabPath));
                });
        }

        private static void ConfigurePickupPrefab()
        {
            EditPrefab(
                k_PickupPrefabPath,
                root =>
                {
                    AudioSource audioSource = root.GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        root.AddComponent<AudioSource>();
                        audioSource = root.GetComponent<AudioSource>();
                    }

                    if (audioSource == null)
                    {
                        throw new InvalidOperationException(
                            "Could not add AudioSource to the pickup prefab.");
                    }
                    audioSource.playOnAwake = false;
                    audioSource.loop = false;
                    audioSource.spatialBlend = 1f;
                    audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                    audioSource.minDistance = 1f;
                    audioSource.maxDistance = 18f;
                    PickupItemInteractable pickup =
                        GetRequiredComponent<PickupItemInteractable>(root);
                    SetBoolean(
                        pickup,
                        "m_trackDroppingCreaturePosition",
                        true);
                    SetObjectReference(pickup, "m_audioSource", audioSource);
                    SetObjectReference(
                        pickup,
                        "m_itemDropSoundEffect",
                        LoadRequiredAsset<AudioClip>(k_DropSoundPath));
                    EditorUtility.SetDirty(audioSource);
                });
        }

        private static void ConfigureAIInventories()
        {
            Item[] dropItems =
            {
                LoadRequiredAsset<Item>(k_BroadswordPath),
                LoadRequiredAsset<Item>(k_ArmorPath)
            };
            foreach (string prefabPath in s_aiPrefabPaths)
            {
                EditPrefab(
                    prefabPath,
                    root => ConfigureAIInventory(root, dropItems));
            }
        }

        private static void ConfigureAIInventory(GameObject root, Item[] dropItems)
        {
            AICharacterInventoryManager inventory =
                GetOrAddComponent<AICharacterInventoryManager>(root);
            SerializedObject serializedInventory = new SerializedObject(inventory);
            SerializedProperty items = GetRequiredProperty(
                serializedInventory,
                "m_droppableItems");
            items.arraySize = dropItems.Length;
            for (int itemIndex = 0; itemIndex < dropItems.Length; itemIndex++)
            {
                items.GetArrayElementAtIndex(itemIndex).objectReferenceValue =
                    dropItems[itemIndex];
            }

            GetRequiredProperty(serializedInventory, "m_dropItemChance").intValue =
                10;
            serializedInventory.ApplyModifiedPropertiesWithoutUndo();
            SetObjectReference(
                GetRequiredComponent<AICharacterManager>(root),
                "m_aiInventoryManager",
                inventory);
            EditorUtility.SetDirty(inventory);
        }

        private static void ConfigurePickupPopupIcon()
        {
            EditPrefab(
                k_PlayerUIPrefabPath,
                root =>
                {
                    PlayerUIPopUpManager popupManager =
                        GetRequiredComponent<PlayerUIPopUpManager>(root);
                    Image icon = GetObjectReference<Image>(
                        popupManager,
                        "m_itemIcon") ??
                        throw new InvalidOperationException(
                            "The item popup icon reference is missing.");
                    icon.type = Image.Type.Simple;
                    icon.preserveAspect = true;
                    EditorUtility.SetDirty(icon);
                });
        }

        private static void ConfigurePickupAnimation()
        {
            AnimatorController controller =
                LoadRequiredAsset<AnimatorController>(k_PlayerControllerPath);
            AnimationClip pickupClip =
                LoadRequiredAsset<AnimationClip>(k_PickupAnimationPath);
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller)
                .stateMachine;
            AnimatorState pickupState = GetOrCreateState(
                stateMachine,
                k_PickupStateName);
            AnimatorState emptyState = GetOrCreateState(
                stateMachine,
                k_EmptyStateName);
            pickupState.motion = pickupClip;
            ConfigureExitTransition(pickupState, emptyState);
            EditorUtility.SetDirty(pickupState);
            EditorUtility.SetDirty(controller);
        }

        private static void RegisterNetworkPrefab()
        {
            GameObject pickupPrefab =
                LoadRequiredAsset<GameObject>(k_PickupPrefabPath);
            NetworkPrefabsList prefabs =
                LoadRequiredAsset<NetworkPrefabsList>(k_NetworkPrefabsPath);
            SerializedObject serializedPrefabs = new SerializedObject(prefabs);
            SerializedProperty entries = GetRequiredProperty(
                serializedPrefabs,
                "List");
            if (CountPrefabEntries(entries, pickupPrefab) > 0)
            {
                return;
            }

            int entryIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(entryIndex);
            SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
            SetRelativeInteger(entry, "Override", 0);
            SetRelativeObject(entry, "Prefab", pickupPrefab);
            SetRelativeObject(entry, "SourcePrefabToOverride", null);
            SetRelativeInteger(entry, "SourceHashToOverride", 0);
            SetRelativeObject(entry, "OverridingTargetPrefab", null);
            serializedPrefabs.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefabs);
        }

        private static void ValidateRuntimeContracts()
        {
            MethodInfo dropMethod = typeof(AICharacterInventoryManager).GetMethod(
                nameof(AICharacterInventoryManager.DropItem));
            MethodInfo destroyRpc = typeof(PickupItemInteractable).GetMethod(
                nameof(PickupItemInteractable.DestroyThisNetworkObjectServerRpc));
            ServerRpcAttribute rpcAttribute =
                destroyRpc?.GetCustomAttribute<ServerRpcAttribute>();
            FieldInfo networkItemID = typeof(PickupItemInteractable).GetField(
                nameof(PickupItemInteractable.NetworkItemID));
            FieldInfo networkPosition = typeof(PickupItemInteractable).GetField(
                nameof(PickupItemInteractable.NetworkPosition));
            FieldInfo creatureID = typeof(PickupItemInteractable).GetField(
                nameof(PickupItemInteractable.DroppingCreatureID));
            if (dropMethod == null ||
                rpcAttribute == null ||
                rpcAttribute.RequireOwnership ||
                networkItemID?.FieldType != typeof(NetworkVariable<int>) ||
                networkPosition?.FieldType != typeof(NetworkVariable<Vector3>) ||
                creatureID?.FieldType != typeof(NetworkVariable<ulong>) ||
                !Enum.IsDefined(
                    typeof(CharacterActionAnimation),
                    CharacterActionAnimation.PickupItem))
            {
                throw new InvalidOperationException(
                    "The monster item drop runtime or RPC contract is incomplete.");
            }
        }

        private static void ValidateItemDatabase()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_DatabasePrefabPath);
            try
            {
                WorldItemDatabase database =
                    GetRequiredComponent<WorldItemDatabase>(root);
                GameObject pickup =
                    LoadRequiredAsset<GameObject>(k_PickupPrefabPath);
                HashSet<int> itemIDs = new HashSet<int>();
                bool stableIDs = database.Items.All(
                    item => item != null &&
                        item.ItemID >= 0 &&
                        itemIDs.Add(item.ItemID));
                if (database.CreatureDropPickupPrefab != pickup || !stableIDs)
                {
                    throw new InvalidOperationException(
                        "WorldItemDatabase requires one pickup prefab and unique stable item IDs.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidatePickupPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PickupPrefabPath);
            try
            {
                PickupItemInteractable pickup =
                    GetRequiredComponent<PickupItemInteractable>(root);
                AudioSource audioSource = GetRequiredComponent<AudioSource>(root);
                if (root.GetComponent<NetworkObject>() == null ||
                    pickup.PickupType != ItemPickupType.CharacterDrop ||
                    !pickup.TracksDroppingCreature ||
                    !Mathf.Approximately(audioSource.spatialBlend, 1f) ||
                    audioSource.playOnAwake ||
                    GetObjectReference<AudioClip>(
                        pickup,
                        "m_itemDropSoundEffect") == null)
                {
                    throw new InvalidOperationException(
                        "The CharacterDrop pickup prefab presentation is invalid.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateAIInventories()
        {
            Item broadsword = LoadRequiredAsset<Item>(k_BroadswordPath);
            Item armor = LoadRequiredAsset<Item>(k_ArmorPath);
            foreach (string prefabPath in s_aiPrefabPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    AICharacterInventoryManager inventory =
                        GetRequiredComponent<AICharacterInventoryManager>(root);
                    AICharacterManager character =
                        GetRequiredComponent<AICharacterManager>(root);
                    bool hasExpectedItems =
                        inventory.DroppableItems.Count == 2 &&
                        inventory.DroppableItems.Contains(broadsword) &&
                        inventory.DroppableItems.Contains(armor);
                    if (character.InventoryManager != inventory ||
                        inventory.DropItemChance != 10 ||
                        !hasExpectedItems)
                    {
                        throw new InvalidOperationException(
                            $"{root.name} has an invalid creature loot inventory.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ValidatePickupPopupIcon()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerUIPrefabPath);
            try
            {
                Image icon = GetObjectReference<Image>(
                    GetRequiredComponent<PlayerUIPopUpManager>(root),
                    "m_itemIcon");
                if (icon == null ||
                    icon.type != Image.Type.Simple ||
                    !icon.preserveAspect)
                {
                    throw new InvalidOperationException(
                        "The item pickup popup icon must preserve its aspect ratio.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidatePickupAnimation()
        {
            AnimatorController controller =
                LoadRequiredAsset<AnimatorController>(k_PlayerControllerPath);
            AnimationClip clip =
                LoadRequiredAsset<AnimationClip>(k_PickupAnimationPath);
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller)
                .stateMachine;
            AnimatorState state = FindState(stateMachine, k_PickupStateName);
            bool exitsToEmpty = state != null && state.transitions.Any(
                transition =>
                    transition.destinationState?.name == k_EmptyStateName &&
                    transition.hasExitTime);
            if (state?.motion != clip || !exitsToEmpty)
            {
                throw new InvalidOperationException(
                    "Pickup_Item_01 must play the authored pickup clip and exit to Empty.");
            }
        }

        private static void ValidateNetworkPrefabRegistration()
        {
            GameObject pickup = LoadRequiredAsset<GameObject>(k_PickupPrefabPath);
            NetworkPrefabsList prefabs =
                LoadRequiredAsset<NetworkPrefabsList>(k_NetworkPrefabsPath);
            SerializedProperty entries = GetRequiredProperty(
                new SerializedObject(prefabs),
                "List");
            if (CountPrefabEntries(entries, pickup) != 1)
            {
                throw new InvalidOperationException(
                    "The CharacterDrop pickup must be registered exactly once.");
            }
        }

        private static AnimatorControllerLayer GetRequiredLayer(
            AnimatorController controller)
        {
            return controller.layers.FirstOrDefault(
                layer => layer.name == k_ActionLayerName) ??
                throw new InvalidOperationException(
                    $"{controller.name} is missing {k_ActionLayerName}.");
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return FindState(stateMachine, stateName) ??
                stateMachine.AddState(stateName);
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == stateName);
        }

        private static void ConfigureExitTransition(
            AnimatorState source,
            AnimatorState destination)
        {
            AnimatorStateTransition transition = source.transitions.FirstOrDefault(
                candidate => candidate.destinationState == destination) ??
                source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.None;
            EditorUtility.SetDirty(transition);
        }

        private static int CountPrefabEntries(
            SerializedProperty entries,
            GameObject prefab)
        {
            int count = 0;
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                if (entries.GetArrayElementAtIndex(entryIndex)
                        .FindPropertyRelative("Prefab")
                        ?.objectReferenceValue == prefab)
                {
                    count++;
                }
            }

            return count;
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException($"Could not save {path}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T GetOrAddComponent<T>(GameObject root)
            where T : Component
        {
            return root.GetComponent<T>() ?? root.AddComponent<T>();
        }

        private static T GetRequiredComponent<T>(GameObject root)
            where T : Component
        {
            return root.GetComponent<T>() ??
                throw new InvalidOperationException(
                    $"{root.name} is missing {typeof(T).Name}.");
        }

        private static T LoadRequiredAsset<T>(string path)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path) ??
                throw new InvalidOperationException($"Missing asset: {path}");
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.name} is missing {propertyName}.");
        }

        private static void SetBoolean(
            UnityEngine.Object target,
            string propertyName,
            bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue =
                value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetObjectReference<T>(
            UnityEngine.Object target,
            string propertyName)
            where T : UnityEngine.Object
        {
            SerializedObject serializedObject = new SerializedObject(target);
            return GetRequiredProperty(serializedObject, propertyName)
                .objectReferenceValue as T;
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
    }
}
