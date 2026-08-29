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
    public class FlaskSystemTests
    {
        private const string k_HealthFlaskPath =
            "Assets/Data/Items/Quick Slot Items/Flask of Crimson Tears.asset";
        private const string k_FocusFlaskPath =
            "Assets/Data/Items/Quick Slot Items/Flask of Cerulean Tears.asset";
        private const string k_DatabasePrefabPath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";
        private const string k_PlayerPrefabPath =
            "Assets/Data/Prefabs/Player.prefab";
        private const string k_UIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_DrinkStartClipPath =
            "Assets/Data/Animations/Flasks/Drink Start.anim";
        private const string k_Drink01ClipPath =
            "Assets/Data/Animations/Flasks/Drink 01.anim";
        private const string k_Drink02ClipPath =
            "Assets/Data/Animations/Flasks/Drink 02.anim";

        [Test]
        public void FlaskItemsUseStableIDsAndSeparateRestorationCategories()
        {
            UnityEngine.Object healthFlask = AssetDatabase.LoadMainAssetAtPath(
                k_HealthFlaskPath);
            UnityEngine.Object focusFlask = AssetDatabase.LoadMainAssetAtPath(
                k_FocusFlaskPath);

            Assert.That(healthFlask, Is.Not.Null);
            Assert.That(focusFlask, Is.Not.Null);
            Assert.That(GetProperty<int>(healthFlask, "ItemID"), Is.EqualTo(14));
            Assert.That(GetProperty<int>(focusFlask, "ItemID"), Is.EqualTo(15));
            Assert.That(GetProperty<bool>(healthFlask, "RestoresHealth"), Is.True);
            Assert.That(GetProperty<bool>(focusFlask, "RestoresHealth"), Is.False);
            Assert.That(GetProperty<float>(healthFlask, "FlaskRestoration"),
                Is.EqualTo(55f));
            Assert.That(GetProperty<float>(focusFlask, "FlaskRestoration"),
                Is.EqualTo(50f));
            Assert.That(GetProperty<object>(healthFlask, "ItemIcon"), Is.Not.Null);
            Assert.That(GetProperty<object>(focusFlask, "ItemIcon"), Is.Not.Null);
            Assert.That(GetProperty<object>(healthFlask, "ItemModel"), Is.Not.Null);
            Assert.That(GetProperty<object>(focusFlask, "ItemModel"), Is.Not.Null);
            Assert.That(GetProperty<object>(healthFlask, "EmptyFlaskItemModel"),
                Is.Not.Null);
            Assert.That(GetProperty<object>(focusFlask, "EmptyFlaskItemModel"),
                Is.Not.Null);
        }

        [Test]
        public void DatabaseAndPlayerPrefabRegisterTheHealthFlaskDefault()
        {
            UnityEngine.Object healthFlask = AssetDatabase.LoadMainAssetAtPath(
                k_HealthFlaskPath);
            GameObject databaseRoot = PrefabUtility.LoadPrefabContents(
                k_DatabasePrefabPath);
            try
            {
                Component database = databaseRoot.GetComponents<Component>()
                    .Single(component =>
                        component.GetType().Name == "WorldItemDatabase");
                SerializedObject serializedDatabase = new SerializedObject(database);
                SerializedProperty items = serializedDatabase.FindProperty("m_items");
                SerializedProperty quickItems = serializedDatabase.FindProperty(
                    "m_quickSlotItems");
                Assert.That(items.arraySize, Is.GreaterThan(15));
                Assert.That(
                    items.GetArrayElementAtIndex(14).objectReferenceValue,
                    Is.EqualTo(healthFlask));
                Assert.That(quickItems.arraySize, Is.EqualTo(2));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(databaseRoot);
            }

            GameObject playerRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerPrefabPath);
            try
            {
                Component inventory = playerRoot.GetComponents<Component>()
                    .Single(component =>
                        component.GetType().Name == "PlayerInventoryManager");
                Assert.That(
                    new SerializedObject(inventory)
                        .FindProperty("m_startingQuickSlotItem")
                        .objectReferenceValue,
                    Is.EqualTo(healthFlask));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        [Test]
        public void PlayerReplicatesQuickItemCountsAndChuggingAsOwnerState()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Component network = root.GetComponents<Component>()
                    .Single(component =>
                        component.GetType().Name == "PlayerNetworkManager");
                AssertOwnerWrittenNetworkVariable(network, "CurrentQuickSlotItemID");
                AssertOwnerWrittenNetworkVariable(network, "RemainingHealthFlasks");
                AssertOwnerWrittenNetworkVariable(
                    network,
                    "RemainingFocusPointFlasks");
                AssertOwnerWrittenNetworkVariable(network, "IsChugging");
                Assert.That(GetNetworkVariableValue<int>(
                    network,
                    "CurrentQuickSlotItemID"), Is.EqualTo(14));
                Assert.That(GetNetworkVariableValue<int>(
                    network,
                    "RemainingHealthFlasks"), Is.EqualTo(3));
                Assert.That(GetNetworkVariableValue<int>(
                    network,
                    "RemainingFocusPointFlasks"), Is.EqualTo(1));
                Assert.That(GetNetworkVariableValue<bool>(
                    network,
                    "IsChugging"), Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void GameplayQuickSlotInputSupportsGamepadWestAndKeyboardX()
        {
            InputActionsDocument inputAsset = JsonUtility.FromJson<InputActionsDocument>(
                File.ReadAllText("Assets/PlayerControls.inputactions"));
            InputBindingData[] bindings = inputAsset.maps
                .Single(map => map.name == "Player Movement")
                .bindings
                .Where(binding => binding.action == "Use Quick Slot Item")
                .ToArray();

            Assert.That(bindings.Select(binding => binding.path),
                Does.Contain("<Gamepad>/buttonWest"));
            Assert.That(bindings.Select(binding => binding.path),
                Does.Contain("<Keyboard>/x"));
            Assert.That(
                Type.GetType("ZZ.PlayerInputManager, Assembly-CSharp")
                    ?.GetMethod(
                        "HandleQuickSlotItemInput",
                        BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
        }

        [Test]
        public void UpperBodyAnimatorOwnsChainedDrinkAndResetStates()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                k_ControllerPath);
            AnimatorControllerLayer layer = controller.layers
                .Single(candidate => candidate.name == "Upper Body Override");
            AnimatorState[] states = layer.stateMachine.states
                .Select(childState => childState.state)
                .ToArray();

            Assert.That(controller.parameters.Any(parameter =>
                parameter.name == "isChuggingFlask" &&
                parameter.type == AnimatorControllerParameterType.Bool), Is.True);
            Assert.That(states.Select(state => state.name),
                Is.SupersetOf(new[]
                {
                    "Drink Start",
                    "Drink 01",
                    "Drink 02",
                    "Drink End",
                    "Empty Flask"
                }));
            Assert.That(
                states.Single(state => state.name == "Empty").behaviours
                    .Any(behaviour => behaviour.GetType().Name ==
                        "ResetUpperBodyAction"),
                Is.True);
            Assert.That(
                states.Where(state => state.name is "Drink 01" or "Drink 02")
                    .All(state => state.behaviours.Any(behaviour =>
                        behaviour.GetType().Name == "ResetIsChugging")),
                Is.True);
        }

        [Test]
        public void FlaskDrinkEventHasReceiverOnTheAnimatorGameObject()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);
                Component receiver = animator.GetComponents<Component>()
                    .FirstOrDefault(component =>
                        component.GetType().Name == "PlayerAnimatorManager");
                Assert.That(receiver, Is.Not.Null,
                    "Drink animation events fire on the Animator's GameObject, " +
                    "which requires the PlayerAnimatorManager receiver.");
                Assert.That(
                    receiver.GetType().GetMethod("SuccessfullyUseQuickSlotItem"),
                    Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void FlaskSuccessOccursOnlyAtDrinkAnimationEvents()
        {
            string[] startEvents = AnimationUtility.GetAnimationEvents(
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        k_DrinkStartClipPath))
                .Select(animationEvent => animationEvent.functionName)
                .ToArray();
            string[] drinkEvents = new[] { k_Drink01ClipPath, k_Drink02ClipPath }
                .SelectMany(path => AnimationUtility.GetAnimationEvents(
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(path)))
                .Select(animationEvent => animationEvent.functionName)
                .ToArray();

            Assert.That(startEvents, Does.Not.Contain(
                "SuccessfullyUseQuickSlotItem"));
            Assert.That(drinkEvents.Count(eventName =>
                eventName == "SuccessfullyUseQuickSlotItem"), Is.EqualTo(2));
            Assert.That(
                Type.GetType("ZZ.FlaskItem, Assembly-CSharp")
                    ?.GetMethod("AttemptToUseItem"),
                Is.Not.EqualTo(Type.GetType("ZZ.FlaskItem, Assembly-CSharp")
                    ?.GetMethod("SuccessfullyUseItem")));
        }

        [TestCase(80f, 100f, 55f, 100f)]
        [TestCase(10f, 100f, 55f, 65f)]
        [TestCase(10f, 100f, -4f, 10f)]
        [TestCase(-5f, 100f, 10f, 5f)]
        public void FlaskRestorationClampsToTheResourceRange(
            float current,
            float maximum,
            float restoration,
            float expected)
        {
            MethodInfo calculate = Type.GetType("ZZ.FlaskItem, Assembly-CSharp")
                ?.GetMethod("CalculateRestoredValue");
            Assert.That(
                calculate?.Invoke(
                    null,
                    new object[] { current, maximum, restoration }),
                Is.EqualTo(expected));
        }

        [Test]
        public void QuickItemUseHasIndependentRollAndWeaponPresentationGates()
        {
            GameObject root = new GameObject("Locomotion Test");
            try
            {
                Type locomotionType = Type.GetType(
                    "ZZ.CharacterLocomotionManager, Assembly-CSharp");
                Component locomotion = root.AddComponent(locomotionType);
                Assert.That(GetProperty<bool>(locomotion, "CanRoll"), Is.True);
                locomotionType.GetMethod("SetCanRoll")
                    ?.Invoke(locomotion, new object[] { false });
                Assert.That(GetProperty<bool>(locomotion, "CanRoll"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Type combatType = Type.GetType(
                "ZZ.PlayerCombatManager, Assembly-CSharp");
            Type equipmentType = Type.GetType(
                "ZZ.PlayerEquipmentManager, Assembly-CSharp");
            Assert.That(combatType?.GetProperty("IsUsingItem"), Is.Not.Null);
            Assert.That(combatType?.GetMethod("CancelQuickSlotItemUse"), Is.Not.Null);
            Assert.That(equipmentType?.GetMethod("SetWeaponsHidden"), Is.Not.Null);
            Assert.That(equipmentType?.GetProperty("QuickSlotItemParent"), Is.Not.Null);
        }

        [Test]
        public void QuickSlotRpcUsesStableItemIDAndServerValidation()
        {
            Type networkType = Type.GetType(
                "ZZ.PlayerNetworkManager, Assembly-CSharp");
            MethodInfo rpc = networkType.GetMethod(
                "NotifyServerOfQuickSlotItemActionServerRpc");

            Assert.That(rpc, Is.Not.Null);
            Assert.That(rpc.GetParameters()[0].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(
                rpc.GetCustomAttributes(false)
                    .Any(attribute =>
                        attribute.GetType().Name == "ServerRpcAttribute"),
                Is.True);
        }

        [Test]
        public void HUDAndWorldManagersHaveFlaskPresentationReferences()
        {
            GameObject uiRoot = PrefabUtility.LoadPrefabContents(k_UIManagerPrefabPath);
            try
            {
                Component hud = uiRoot.GetComponentsInChildren<Component>(true)
                    .Single(component =>
                        component.GetType().Name == "PlayerUIHUDManager");
                Assert.That(
                    new SerializedObject(hud).FindProperty("m_itemQuickSlot")
                        .objectReferenceValue,
                    Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(uiRoot);
            }

            string mainMenuScene = File.ReadAllText(
                "Assets/Scenes/Scene_Main_Menu_01.unity");
            Assert.That(mainMenuScene, Does.Match(
                @"m_healingFlaskVFX: \{fileID: (?!0)"));
            Assert.That(mainMenuScene, Does.Match(
                @"m_focusFlaskVFX: \{fileID: (?!0)"));
            Assert.That(mainMenuScene, Does.Match(
                @"m_flaskRestorationSoundEffect: \{fileID: (?!0)"));
            Assert.That(mainMenuScene, Does.Match(
                @"m_emptyFlaskSoundEffect: \{fileID: (?!0)"));
        }

        private static T GetNetworkVariableValue<T>(
            Component network,
            string propertyName)
        {
            object networkVariable = network.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(network);
            return (T)networkVariable?.GetType()
                .GetProperty("Value")
                ?.GetValue(networkVariable);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target?.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(target);
        }

        private static void AssertOwnerWrittenNetworkVariable(
            Component network,
            string propertyName)
        {
            object networkVariable = network.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(network);
            object writePermission = networkVariable?.GetType()
                .GetProperty("WritePerm")
                ?.GetValue(networkVariable);
            Assert.That(writePermission?.ToString(), Is.EqualTo("Owner"));
        }

        [Serializable]
        private sealed class InputActionsDocument
        {
            public InputActionMapData[] maps;
        }

        [Serializable]
        private sealed class InputActionMapData
        {
            public string name;
            public InputBindingData[] bindings;
        }

        [Serializable]
        private sealed class InputBindingData
        {
            public string action;
            public string path;
        }
    }
}
