using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Tests
{
    public class MonsterItemDropSystemTests
    {
        private const string k_DatabasePrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World Item Database.prefab";
        private const string k_PickupPrefabPath =
            "Assets/_Game/Prefabs/Interactables/Item Pickup.prefab";
        private const string k_UndeadPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_NetworkPrefabsPath =
            "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset";
        private const string k_PlayerControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";

        [Test]
        public void DropChanceUsesExactPercentageBoundaries()
        {
            MethodInfo rollMethod = GetRuntimeType(
                    "ZZ.AICharacterInventoryManager")
                .GetMethod(
                "DidDropRollSucceed",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(rollMethod, Is.Not.Null);
            Assert.That(RollSucceeds(rollMethod, 0, 0), Is.False);
            Assert.That(RollSucceeds(rollMethod, 0, 1), Is.True);
            Assert.That(RollSucceeds(rollMethod, 99, 100), Is.True);
            Assert.That(RollSucceeds(rollMethod, 100, 100), Is.False);
        }

        [Test]
        public void DatabaseRegistersStableItemsAndCreatureDropPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_DatabasePrefabPath);
            try
            {
                Component database = GetComponentByName(
                    root,
                    "WorldItemDatabase");
                GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(
                    k_PickupPrefabPath);
                Assert.That(database, Is.Not.Null);
                SerializedObject serializedDatabase = new SerializedObject(database);
                SerializedProperty items = serializedDatabase.FindProperty("m_items");
                Assert.That(
                    serializedDatabase.FindProperty("m_creatureDropPickupPrefab")
                        .objectReferenceValue,
                    Is.EqualTo(pickup));
                int[] itemIDs = Enumerable.Range(0, items.arraySize)
                    .Select(index => items.GetArrayElementAtIndex(index)
                        .objectReferenceValue)
                    .Select(item => new SerializedObject(item)
                        .FindProperty("m_itemID").intValue)
                    .ToArray();
                Assert.That(itemIDs, Has.All.GreaterThanOrEqualTo(0));
                Assert.That(
                    itemIDs.Distinct().Count(),
                    Is.EqualTo(items.arraySize));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase(k_UndeadPrefabPath)]
        [TestCase(k_BossPrefabPath)]
        public void AIHasOneConfiguredLootInventory(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Component character = GetComponentByName(
                    root,
                    "AICharacterManager");
                Component inventory = GetComponentByName(
                    root,
                    "AICharacterInventoryManager");
                Assert.That(character, Is.Not.Null);
                Assert.That(inventory, Is.Not.Null);
                Assert.That(
                    new SerializedObject(character)
                        .FindProperty("m_aiInventoryManager")
                        .objectReferenceValue,
                    Is.SameAs(inventory));
                SerializedObject serializedInventory =
                    new SerializedObject(inventory);
                Assert.That(
                    serializedInventory.FindProperty("m_dropItemChance").intValue,
                    Is.EqualTo(10));
                SerializedProperty items =
                    serializedInventory.FindProperty("m_droppableItems");
                Assert.That(items.arraySize, Is.EqualTo(2));
                Assert.That(
                    Enumerable.Range(0, items.arraySize).All(index =>
                        items.GetArrayElementAtIndex(index)
                            .objectReferenceValue != null),
                    Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void CharacterDropReplicatesServerWrittenStateAndUsesOpenPickupRpc()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PickupPrefabPath);
            try
            {
                Component pickup = root.GetComponent("PickupItemInteractable");
                MethodInfo rpc = pickup?.GetType().GetMethod(
                    "DestroyThisNetworkObjectServerRpc");
                object attribute = rpc?.GetCustomAttributes(false)
                    .SingleOrDefault(candidate =>
                        candidate.GetType().Name == "RpcAttribute");
                Assert.That(pickup, Is.Not.Null);
                Assert.That(
                    new SerializedObject(pickup)
                        .FindProperty("m_pickupType").enumNames[
                            new SerializedObject(pickup)
                                .FindProperty("m_pickupType").enumValueIndex],
                    Is.EqualTo("CharacterDrop"));
                AssertServerWrittenNetworkVariable(pickup, "NetworkItemID");
                AssertServerWrittenNetworkVariable(pickup, "NetworkPosition");
                AssertServerWrittenNetworkVariable(pickup, "DroppingCreatureID");
                Assert.That(attribute, Is.Not.Null);
                Assert.That(
                    attribute.GetType().GetField("InvokePermission")
                        .GetValue(attribute).ToString(),
                    Is.EqualTo("Everyone"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void CharacterDropHasCorpseTrackingAndThreeDimensionalAudio()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PickupPrefabPath);
            try
            {
                Component pickup = root.GetComponent("PickupItemInteractable");
                AudioSource audioSource = root.GetComponent<AudioSource>();
                Assert.That(
                    new SerializedObject(pickup)
                        .FindProperty("m_trackDroppingCreaturePosition")
                        .boolValue,
                    Is.True);
                Assert.That(audioSource, Is.Not.Null);
                Assert.That(audioSource.spatialBlend, Is.EqualTo(1f));
                Assert.That(audioSource.playOnAwake, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void PickupPrefabIsRegisteredExactlyOnceForNetworkSpawning()
        {
            GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PickupPrefabPath);
            UnityEngine.Object prefabs = AssetDatabase.LoadMainAssetAtPath(
                k_NetworkPrefabsPath);
            SerializedProperty entries = new SerializedObject(prefabs)
                .FindProperty("List");
            int registrationCount = 0;
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                if (entries.GetArrayElementAtIndex(entryIndex)
                        .FindPropertyRelative("Prefab")
                        .objectReferenceValue == pickup)
                {
                    registrationCount++;
                }
            }

            Assert.That(registrationCount, Is.EqualTo(1));
        }

        [Test]
        public void PickupAnimationAndPopupIconAreConfigured()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    k_PlayerControllerPath);
            AnimatorStateMachine actionStateMachine = controller.layers
                .Single(layer => layer.name == "Action Override")
                .stateMachine;
            AnimatorState pickupState = actionStateMachine.states
                .Select(child => child.state)
                .SingleOrDefault(state => state.name == "Pickup_Item_01");
            Assert.That(pickupState, Is.Not.Null);
            Assert.That(pickupState.motion?.name, Is.EqualTo("core_item_pickup_mid_01"));
            Assert.That(
                pickupState.transitions.Any(
                    transition => transition.destinationState?.name == "Empty"),
                Is.True);

            GameObject uiRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Component popup = GetComponentByName(
                    uiRoot,
                    "PlayerUIPopUpManager");
                SerializedProperty iconProperty = new SerializedObject(popup)
                    .FindProperty("m_itemIcon");
                Image icon = iconProperty.objectReferenceValue as Image;
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.type, Is.EqualTo(Image.Type.Simple));
                Assert.That(icon.preserveAspect, Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(uiRoot);
            }
        }

        private static bool RollSucceeds(
            MethodInfo rollMethod,
            int roll,
            int chance)
        {
            return (bool)rollMethod.Invoke(null, new object[] { roll, chance });
        }

        private static void AssertServerWrittenNetworkVariable(
            Component pickup,
            string fieldName)
        {
            object networkVariable = pickup.GetType()
                .GetField(fieldName)
                .GetValue(pickup);
            object writePermission = networkVariable.GetType()
                .GetProperty("WritePerm")
                .GetValue(networkVariable);
            Assert.That(writePermission.ToString(), Is.EqualTo("Server"));
        }

        private static Component GetComponentByName(
            GameObject root,
            string componentName)
        {
            return root.GetComponents<Component>()
                .SingleOrDefault(component =>
                    component.GetType().Name == componentName);
        }

        private static System.Type GetRuntimeType(string fullName)
        {
            System.Type type = System.Type.GetType(
                $"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve {fullName}.");
            return type;
        }
    }
}
