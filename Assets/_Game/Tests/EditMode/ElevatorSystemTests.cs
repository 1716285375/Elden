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
    public class ElevatorSystemTests
    {
        private const string k_PrefabFolder =
            "Assets/_Game/Prefabs/World/Objects/Elevator";
        private const string k_AnimationFolder =
            "Assets/_Game/Art/Environment/Shared/Animations/Elevator";
        private const string k_ElevatorPrefabPath =
            k_PrefabFolder + "/Elevator.prefab";
        private const string k_CallStationPrefabPath =
            k_PrefabFolder + "/Call Elevator.prefab";
        private const string k_LeverStationPrefabPath =
            k_PrefabFolder + "/Call Elevator Lever.prefab";

        [Test]
        public void ElevatorPrefabSeparatesPhysicalAndTriggerColliders()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_ElevatorPrefabPath);
            try
            {
                Transform platform = FindRequiredChild(root, "Platform");
                Collider physical = platform.GetComponent<Collider>();
                Collider interaction = FindRequiredChild(
                    root,
                    "Interaction Trigger").GetComponent<Collider>();
                Collider occupancy = FindRequiredChild(
                    root,
                    "Occupancy Trigger").GetComponent<Collider>();

                Assert.That(physical, Is.Not.Null);
                Assert.That(physical.isTrigger, Is.False);
                Assert.That(interaction, Is.Not.Null);
                Assert.That(interaction.isTrigger, Is.True);
                Assert.That(occupancy, Is.Not.Null);
                Assert.That(occupancy.isTrigger, Is.True);
                Assert.That(
                    root.GetComponentsInChildren(
                        GetRuntimeType("Unity.Netcode.NetworkObject"),
                        true).Length,
                    Is.EqualTo(1));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ElevatorPrefabWiresReusableInteractionAndPassengers()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_ElevatorPrefabPath);
            try
            {
                Component elevator = root.GetComponent(
                    GetRuntimeType("ZZ.ElevatorInteractable"));
                Component occupancy = root.GetComponentInChildren(
                    GetRuntimeType("ZZ.IsOnElevatorTrigger"),
                    true);
                Component button = root.GetComponentInChildren(
                    GetRuntimeType("ZZ.ElevatorButtonTrigger"),
                    true);
                SerializedObject serializedElevator = new(elevator);
                SerializedObject serializedOccupancy = new(occupancy);
                SerializedObject serializedButton = new(button);

                Assert.That(elevator, Is.Not.Null);
                Assert.That(occupancy, Is.Not.Null);
                Assert.That(button, Is.Not.Null);
                Assert.That(GetRequiredProperty(
                    serializedElevator,
                    "m_hostOnlyInteractable").boolValue, Is.False);
                Assert.That(GetRequiredProperty(
                    serializedElevator,
                    "m_shouldDisableColliderAfterInteraction").boolValue,
                    Is.False);
                Assert.That(GetRequiredProperty(
                    serializedOccupancy,
                    "m_elevator").objectReferenceValue, Is.SameAs(elevator));
                Assert.That(GetRequiredProperty(
                    serializedButton,
                    "m_elevator").objectReferenceValue, Is.SameAs(elevator));
                Assert.That(GetRequiredProperty(
                    serializedButton,
                    "m_minimumButtonReleaseTime").floatValue,
                    Is.EqualTo(2f).Within(0.001f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void CallStationPrefabsAreIndependentNetworkInteractables()
        {
            AssertStationPrefab(
                k_CallStationPrefabPath,
                "ZZ.CallElevatorInteractable");
            AssertStationPrefab(
                k_LeverStationPrefabPath,
                "ZZ.CallElevatorLeverInteractable");
        }

        [Test]
        public void ButtonControllerContainsEveryMechanicalState()
        {
            AssertControllerStates(
                k_AnimationFolder + "/Elevator Button.controller",
                "Idle",
                "PushDown",
                "PushedDown",
                "Release");
        }

        [Test]
        public void LeverControllerContainsPullAndReleaseStates()
        {
            AssertControllerStates(
                k_AnimationFolder + "/Elevator Lever.controller",
                "Idle",
                "PullLever",
                "ReleaseLever");

            GameObject root = PrefabUtility.LoadPrefabContents(
                k_LeverStationPrefabPath);
            try
            {
                Component lever = root.GetComponent(
                    GetRuntimeType("ZZ.CallElevatorLeverInteractable"));
                SerializedObject serializedLever = new(lever);
                Assert.That(GetRequiredProperty(
                    serializedLever,
                    "m_timeToWaitAfterPullingLeverToMoveElevator").floatValue,
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(GetRequiredProperty(
                    serializedLever,
                    "m_leverAnimator").objectReferenceValue,
                    Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ElevatorSourceUsesServerAuthorityAndLateJoinState()
        {
            string source = ReadSource(
                "Assets/_Game/Scripts/World/Interactions/ElevatorInteractable.cs");

            Assert.That(source, Does.Contain(
                "NetworkVariable<Vector3> NetworkPosition"));
            Assert.That(source, Does.Contain(
                "NetworkVariable<bool> ElevatorIsRising"));
            Assert.That(source, Does.Contain(
                "NetworkVariable<bool> ElevatorIsDescending"));
            Assert.That(source, Does.Contain(
                "[Rpc(SendTo.Server, InvokePermission = " +
                "RpcInvokePermission.Everyone)]"));
            Assert.That(source, Does.Contain("[ClientRpc]"));
            Assert.That(source, Does.Contain("Vector3.MoveTowards"));
            Assert.That(source, Does.Contain(
                "SetPlatformLocalPosition(NetworkPosition.Value)"));
            Assert.That(source, Does.Contain(
                "SetInteractionColliderEnabled(false)"));
        }

        [Test]
        public void ButtonAndLeverSourcesEnforceRequiredStateLifecycles()
        {
            string buttonSource = ReadSource(
                "Assets/_Game/Scripts/World/Interactions/ElevatorButtonTrigger.cs");
            string leverSource = ReadSource(
                "Assets/_Game/Scripts/World/Interactions/CallElevatorLeverInteractable.cs");

            Assert.That(buttonSource, Does.Contain("ButtonHasBeenPressed"));
            Assert.That(buttonSource, Does.Contain("m_overlapCounts"));
            Assert.That(buttonSource, Does.Contain("k_PushDownState"));
            Assert.That(buttonSource, Does.Contain("k_PushedDownState"));
            Assert.That(buttonSource, Does.Contain("k_ReleaseState"));
            Assert.That(leverSource, Does.Contain(
                "NetworkVariable<bool> m_leverHasBeenPulled"));
            Assert.That(leverSource, Does.Contain("m_oppositeLever"));
            Assert.That(leverSource, Does.Contain("PullLeverClientRpc"));
            Assert.That(leverSource, Does.Contain("ReleaseLeverClientRpc"));
        }

        [Test]
        public void PassengerMotionPreservesLiftOwnedVerticalPosition()
        {
            string elevatorSource = ReadSource(
                "Assets/_Game/Scripts/World/Interactions/ElevatorInteractable.cs");
            string occupancySource = ReadSource(
                "Assets/_Game/Scripts/World/Interactions/IsOnElevatorTrigger.cs");
            string locomotionSource = ReadSource(
                "Assets/_Game/Scripts/Characters/Common/CharacterLocomotionManager.cs");
            string networkSource = ReadSource(
                "Assets/_Game/Scripts/Characters/Common/CharacterNetworkManager.cs");

            Assert.That(occupancySource, Does.Contain("m_overlapCounts"));
            Assert.That(occupancySource, Does.Contain("AddCharacter(character)"));
            Assert.That(occupancySource, Does.Contain("RemoveCharacter(character)"));
            Assert.That(elevatorSource, Does.Contain("character.IsJumping"));
            Assert.That(elevatorSource, Does.Contain("MoveWithLiftToHeight"));
            Assert.That(locomotionSource, Does.Contain("SetRidingLift"));
            Assert.That(locomotionSource, Does.Contain(
                "m_isRidingLift && !m_characterManager.IsJumping"));
            Assert.That(locomotionSource, Does.Contain(
                "networkPosition.y = transform.position.y"));
            Assert.That(networkSource, Does.Contain("?.IsRidingLift == true"));
            Assert.That(networkSource, Does.Contain(
                "m_characterManager.IsJumping == false"));
            Assert.That(networkSource, Does.Contain(
                "networkPosition.y = transform.position.y"));
        }

        public static void RunAllFocusedTests()
        {
            ElevatorSystemTests tests = new();
            RunFocusedTest(
                tests.ElevatorPrefabSeparatesPhysicalAndTriggerColliders);
            RunFocusedTest(
                tests.ElevatorPrefabWiresReusableInteractionAndPassengers);
            RunFocusedTest(
                tests.CallStationPrefabsAreIndependentNetworkInteractables);
            RunFocusedTest(tests.ButtonControllerContainsEveryMechanicalState);
            RunFocusedTest(tests.LeverControllerContainsPullAndReleaseStates);
            RunFocusedTest(tests.ElevatorSourceUsesServerAuthorityAndLateJoinState);
            RunFocusedTest(
                tests.ButtonAndLeverSourcesEnforceRequiredStateLifecycles);
            RunFocusedTest(
                tests.PassengerMotionPreservesLiftOwnedVerticalPosition);
            Debug.Log("[ElevatorSystemTests] 8 focused tests passed.");
        }

        private static void RunFocusedTest(Action test)
        {
            try
            {
                test();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ElevatorSystemTests] {test.Method.Name} failed: " +
                    exception);
                throw;
            }
        }

        private static void AssertStationPrefab(
            string assetPath,
            string interactableTypeName)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                Type networkObjectType = GetRuntimeType(
                    "Unity.Netcode.NetworkObject");
                Component interactable = root.GetComponent(
                    GetRuntimeType(interactableTypeName));
                Collider interactionCollider = root.GetComponent<Collider>();

                Assert.That(interactable, Is.Not.Null);
                Assert.That(interactionCollider, Is.Not.Null);
                Assert.That(interactionCollider.isTrigger, Is.True);
                Assert.That(root.GetComponent(networkObjectType), Is.Not.Null);
                Assert.That(root.GetComponentsInChildren(
                    networkObjectType,
                    true).Length, Is.EqualTo(1));

                SerializedObject serialized = new(interactable);
                Assert.That(GetRequiredProperty(
                    serialized,
                    "m_hostOnlyInteractable").boolValue, Is.False);
                Assert.That(GetRequiredProperty(
                    serialized,
                    "m_shouldDisableColliderAfterInteraction").boolValue,
                    Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssertControllerStates(
            string controllerPath,
            params string[] requiredStates)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<
                AnimatorController>(controllerPath);
            Assert.That(controller, Is.Not.Null, controllerPath);
            string[] stateNames = controller.layers[0].stateMachine.states
                .Select(childState => childState.state.name)
                .ToArray();
            Assert.That(stateNames, Is.SupersetOf(requiredStates));
        }

        private static Transform FindRequiredChild(
            GameObject root,
            string childName)
        {
            Transform child = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == childName);
            Assert.That(child, Is.Not.Null, childName);
            return child;
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(
                propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            return property;
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static string ReadSource(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                .FullName;
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }
    }
}
