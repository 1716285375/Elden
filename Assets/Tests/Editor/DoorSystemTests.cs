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
    public class DoorSystemTests
    {
        private const string k_DoorPrefabPath =
            "Assets/Data/Prefabs/World Objects/Doors/Dungeon Door.prefab";
        private const string k_LockedDoorPrefabPath =
            "Assets/Data/Prefabs/World Objects/Doors/Locked Dungeon Door.prefab";
        private const string k_GatePrefabPath =
            "Assets/Data/Prefabs/World Objects/Doors/Lever Gate.prefab";
        private const string k_KeyItemPath =
            "Assets/Data/Items/Key Items/Old Dungeon Key.asset";
        private const string k_KeyPickupPath =
            "Assets/Data/Prefabs/Interactables/Old Dungeon Key Pickup.prefab";
        private const string k_ItemDatabasePath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";
        private const string k_DoorSourcePath =
            "Assets/Script/World Managers/DoorInteractable.cs";
        private const string k_LeverSourcePath =
            "Assets/Script/World Managers/ActivateOtherInteractableInteractable.cs";
        private const string k_MessageSourcePath =
            "Assets/Script/World Managers/MessageInteractable.cs";

        [Test]
        public void OpenedDoorIDsRoundTripWithoutDuplicates()
        {
            CharacterSaveData saveData = new();

            Assert.That(saveData.RecordOpenedDoor("3_DungeonDoor01"), Is.True);
            Assert.That(saveData.RecordOpenedDoor("3_DungeonDoor01"), Is.False);
            string json = JsonUtility.ToJson(saveData);
            CharacterSaveData restored =
                JsonUtility.FromJson<CharacterSaveData>(json);

            Assert.That(restored.DoorsOpened,
                Is.EqualTo(new[] { "3_DungeonDoor01" }));
            Assert.That(restored.IsDoorOpened("3_DungeonDoor01"), Is.True);
        }

        [Test]
        public void DoorUsesServerAuthorityAndStaticLateJoinPresentation()
        {
            string source = File.ReadAllText(k_DoorSourcePath);

            Assert.That(source,
                Does.Contain("NetworkVariableWritePermission.Server"));
            Assert.That(source,
                Does.Contain("[ServerRpc(RequireOwnership = false)]"));
            Assert.That(source, Does.Contain("OpenDoorClientRpc"));
            Assert.That(source, Does.Contain("ApplyOpenedPresentation"));
            Assert.That(source,
                Does.Contain("IsOpen.OnValueChanged += OnOpenStateChanged"));
            Assert.That(source,
                Does.Contain("IsOpen.OnValueChanged -= OnOpenStateChanged"));
            Assert.That(source,
                Does.Contain("gameObject.scene.buildIndex"));
        }

        [Test]
        public void LockedFailureReturnsBeforeAnyOpenRequest()
        {
            string source = File.ReadAllText(k_DoorSourcePath);
            int requirementCheck = source.IndexOf(
                "if (!HasRequiredItem(player))",
                StringComparison.Ordinal);
            int lockedReturn = source.IndexOf(
                "return;",
                requirementCheck,
                StringComparison.Ordinal);
            int openRequest = source.IndexOf(
                "OpenDoorServerRpc(player.NetworkObjectId)",
                requirementCheck,
                StringComparison.Ordinal);

            Assert.That(requirementCheck, Is.GreaterThanOrEqualTo(0));
            Assert.That(lockedReturn, Is.GreaterThan(requirementCheck));
            Assert.That(openRequest, Is.GreaterThan(lockedReturn));
        }

        [Test]
        public void KeyIsCatalogedAndHasReusablePickup()
        {
            UnityEngine.Object key = AssetDatabase.LoadAssetAtPath<
                UnityEngine.Object>(k_KeyItemPath);
            GameObject databasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_ItemDatabasePath);
            GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_KeyPickupPath);
            Type databaseType = GetRuntimeType("ZZ.WorldItemDatabase");
            Type pickupType = GetRuntimeType("ZZ.PickupItemInteractable");
            Component database = databasePrefab.GetComponent(databaseType);
            Component pickup = pickupPrefab.GetComponent(pickupType);
            int itemID = (int)key.GetType().GetProperty("ItemID")
                .GetValue(key);

            UnityEngine.Object resolvedKey = databaseType.GetMethod("GetKeyByID")
                .Invoke(database, new object[] { itemID }) as UnityEngine.Object;
            SerializedObject serializedPickup = new(pickup);
            Assert.That(resolvedKey, Is.EqualTo(key));
            Assert.That(
                serializedPickup.FindProperty("m_item").objectReferenceValue,
                Is.EqualTo(key));
        }

        [TestCase(k_DoorPrefabPath, false)]
        [TestCase(k_LockedDoorPrefabPath, true)]
        public void DoorPrefabsSeparatePhysicalAndTwoSidedInteractionColliders(
            string prefabPath,
            bool requiresItem)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            Type doorType = GetRuntimeType("ZZ.DoorInteractable");
            Type messageType = GetRuntimeType("ZZ.MessageInteractable");
            Component door = prefab.GetComponent(doorType);
            Component message = prefab.GetComponent(messageType);
            SerializedObject serializedDoor = new(door);

            Assert.That(
                prefab.GetComponent(GetRuntimeType("Unity.Netcode.NetworkObject")),
                Is.Not.Null);
            Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(door, Is.Not.Null);
            Assert.That(message, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true),
                Has.Some.Matches<Collider>(collider => !collider.isTrigger));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true),
                Has.Exactly(2).Matches<Collider>(collider => collider.isTrigger));
            Assert.That(
                serializedDoor.FindProperty("m_requiresItem").boolValue,
                Is.EqualTo(requiresItem));
        }

        [Test]
        public void GateSharesOneNetworkObjectAndHasNoDirectInteraction()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_GatePrefabPath);
            Type doorType = GetRuntimeType("ZZ.DoorInteractable");
            Type leverType = GetRuntimeType(
                "ZZ.ActivateOtherInteractableInteractable");
            Component gate = prefab.GetComponent(doorType);
            Component lever = prefab.GetComponent(leverType);
            SerializedObject serializedGate = new(gate);
            SerializedObject serializedLever = new(lever);

            Assert.That(
                prefab.GetComponents(
                    GetRuntimeType("Unity.Netcode.NetworkObject")),
                Has.Length.EqualTo(1));
            Assert.That(
                serializedGate.FindProperty("m_interactableCollider")
                    .objectReferenceValue,
                Is.Null);
            Assert.That(
                serializedLever.FindProperty("m_interactableCollider")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serializedLever.FindProperty("m_interactableToActivate")
                    .objectReferenceValue,
                Is.EqualTo(gate));
        }

        [Test]
        public void LeverAndMessageRemainGenericReusableComponents()
        {
            string leverSource = File.ReadAllText(k_LeverSourcePath);
            string messageSource = File.ReadAllText(k_MessageSourcePath);

            Assert.That(leverSource,
                Does.Contain("Interactable m_interactableToActivate"));
            Assert.That(leverSource,
                Does.Contain("ActivateFromServer(player)"));
            Assert.That(leverSource, Does.Contain("m_useOnce"));
            Assert.That(leverSource, Does.Contain("ResetLeverAfterDelay"));
            Assert.That(messageSource,
                Does.Contain("SendPlayerMessagePopup(m_message)"));
            Assert.That(messageSource,
                Does.Not.Contain("CompleteInteraction();"));
        }

        [TestCase("Door.controller", "Empty", "DoorOpen", "DoorOpened")]
        [TestCase("Gate.controller", "Empty", "GateOpen", "GateOpened")]
        public void DoorControllersUseDirectPlayStatesWithoutTransitions(
            string controllerName,
            params string[] expectedStates)
        {
            string controllerPath =
                "Assets/Data/Animations/Environment/Doors/" + controllerName;
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    controllerPath);
            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;
            string[] stateNames = stateMachine.states
                .Select(childState => childState.state.name)
                .ToArray();

            Assert.That(stateMachine.defaultState.name, Is.EqualTo("Empty"));
            Assert.That(expectedStates, Is.SubsetOf(stateNames));
            Assert.That(stateMachine.states,
                Has.None.Matches<ChildAnimatorState>(
                    state => state.state.transitions.Length > 0));
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state.motion is AnimationClip clip)
                {
                    AnimationClipSettings settings =
                        AnimationUtility.GetAnimationClipSettings(clip);
                    Assert.That(settings.loopTime, Is.False);
                }
            }
        }

        /// <summary>Runs the focused EP157-158 tests without entering Play Mode.</summary>
        public static void RunAllFocusedTests()
        {
            DoorSystemTests tests = new();
            tests.OpenedDoorIDsRoundTripWithoutDuplicates();
            tests.DoorUsesServerAuthorityAndStaticLateJoinPresentation();
            tests.LockedFailureReturnsBeforeAnyOpenRequest();
            tests.KeyIsCatalogedAndHasReusablePickup();
            tests.DoorPrefabsSeparatePhysicalAndTwoSidedInteractionColliders(
                k_DoorPrefabPath,
                false);
            tests.DoorPrefabsSeparatePhysicalAndTwoSidedInteractionColliders(
                k_LockedDoorPrefabPath,
                true);
            tests.GateSharesOneNetworkObjectAndHasNoDirectInteraction();
            tests.LeverAndMessageRemainGenericReusableComponents();
            tests.DoorControllersUseDirectPlayStatesWithoutTransitions(
                "Door.controller",
                "Empty",
                "DoorOpen",
                "DoorOpened");
            tests.DoorControllersUseDirectPlayStatesWithoutTransitions(
                "Gate.controller",
                "Empty",
                "GateOpen",
                "GateOpened");
            Debug.Log(
                "[DoorSystemTests] 10 EP157-158 focused tests passed.");
        }

        private static Type GetRuntimeType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException(
                $"Runtime type {fullName} could not be found.");
        }
    }
}
