using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public class SneakingSystemTests
    {
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_StealthObjectPrefabPath =
            "Assets/Data/Prefabs/World Objects/Stealth Object.prefab";

        [Test]
        public void NamedSneakActionSupportsKeyboardAndGamepadToggleInput()
        {
            string inputActions = File.ReadAllText("Assets/PlayerControls.inputactions");
            string generatedControls = File.ReadAllText("Assets/PlayerControls.cs");

            Assert.That(inputActions, Does.Contain("\"name\": \"Sneak\""));
            Assert.That(inputActions, Does.Contain("<Keyboard>/c"));
            Assert.That(inputActions, Does.Contain("<Gamepad>/leftStickPress"));
            Assert.That(generatedControls, Does.Contain("public InputAction @Sneak"));
            Assert.That(
                ReadRuntimeSource("World Managers/PlayerInputManager.cs"),
                Does.Contain("ToggleSneakingState()"));
        }

        [Test]
        public void SneakingAndHiddenStatesUseCorrectNetworkAuthorities()
        {
            string networkSource = ReadRuntimeSource(
                "Character/CharacterNetworkManager.cs");

            Assert.That(networkSource, Does.Contain("IsSneaking = new NetworkVariable<bool>"));
            Assert.That(networkSource, Does.Contain("IsHidden = new NetworkVariable<bool>"));
            Assert.That(
                networkSource,
                Does.Contain("NetworkVariableWritePermission.Server"));
            Assert.That(networkSource, Does.Contain("IsSneaking.OnValueChanged"));
            Assert.That(networkSource, Does.Contain("ApplySneakingState(IsSneaking.Value)"));
            Assert.That(
                ReadRuntimeSource("Character/CharacterAnimatorManager.cs"),
                Does.Contain("Animator.StringToHash(\"isSneaking\")"));
        }

        [Test]
        public void SneakMovementHasDistinctForwardAndBackwardSpeeds()
        {
            string locomotionSource = ReadRuntimeSource(
                "Character/Player/PlayerLocomotionManager.cs");

            Assert.That(locomotionSource, Does.Contain("m_runningBackwardsSpeed = 4f"));
            Assert.That(locomotionSource, Does.Contain("m_sneakingWalkingSpeed = 1.1f"));
            Assert.That(locomotionSource, Does.Contain("m_sneakingRunningSpeed = 3f"));
            Assert.That(locomotionSource, Does.Contain("m_sneakingBackwardsSpeed = 2.8f"));
            Assert.That(locomotionSource, Does.Contain("UsesStrafeMovement()"));
            Assert.That(locomotionSource, Does.Contain("networkManager.SetSneakingState(false)"));
        }

        [Test]
        public void TargetRelationsDriveHiddenAndConcealmentDetection()
        {
            string combatSource = ReadRuntimeSource(
                "Character/CharacterCombatManager.cs");
            string aiSource = ReadRuntimeSource(
                "Character/AI/AICharacterManager.cs");
            string aiNetworkSource = ReadRuntimeSource(
                "Character/AI/AICharacterNetworkManager.cs");

            Assert.That(combatSource, Does.Contain("m_charactersTargetingMe"));
            Assert.That(combatSource, Does.Contain("m_stealthObjectsCurrentlyStandingIn"));
            Assert.That(combatSource, Does.Contain("SetHiddenState(isHidden)"));
            Assert.That(aiSource, Does.Contain("m_sneakingDetectionRadiusMultiplier = 0.25f"));
            Assert.That(aiSource, Does.Contain("AddCharacterTargetingMe(this)"));
            Assert.That(aiSource, Does.Contain("RemoveCharacterTargetingMe(this)"));
            Assert.That(aiSource, Does.Contain("bool isFullyConcealed = isSneaking"));
            Assert.That(aiNetworkSource, Does.Contain("ReplicateTargetRelationship("));
            Assert.That(
                aiNetworkSource,
                Does.Contain("SynchronizeTargetRelationshipClientRpc("));
        }

        [Test]
        public void SneakingSuppressesFootstepsAndManualStateChangesWin()
        {
            string soundSource = ReadRuntimeSource(
                "Character/Player/PlayerSoundFXManager.cs");
            string stateMachineSource = ReadRuntimeSource(
                "Character/AI/AICharacterStateMachine.cs");
            string investigateSource = ReadRuntimeSource(
                "Character/AI/States/InvestigateSoundAIState.cs");

            Assert.That(soundSource, Does.Contain("IsSneaking.Value"));
            Assert.That(soundSource, Does.Contain("AlertNearbyCharactersToSound"));
            Assert.That(stateMachineSource, Does.Contain("stateAtTickStart"));
            Assert.That(
                stateMachineSource,
                Does.Contain("m_currentState != stateAtTickStart"));
            Assert.That(
                investigateSource,
                Does.Not.Contain("m_positionOfSound = Vector3.zero"));
        }

        [Test]
        public void AnimatorAndStealthPrefabContainRequiredAssets()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(k_ControllerPath);
            AnimatorStateMachine baseLayer = controller.layers
                .Single(layer => layer.name == "Base Layer")
                .stateMachine;
            string[] stateNames =
            {
                "Sneak Idle One Handed",
                "Sneak Locomotion One Handed",
                "Sneak Idle Two Handed",
                "Sneak Locomotion Two Handed"
            };
            foreach (string stateName in stateNames)
            {
                AnimatorState state = baseLayer.states
                    .Select(childState => childState.state)
                    .Single(candidate => candidate.name == stateName);
                Assert.That(state.motion, Is.Not.Null, stateName);
                Assert.That(state.transitions.Length, Is.GreaterThanOrEqualTo(2));
                Assert.That(state.transitions.All(transition => !transition.hasExitTime),
                    Is.True);
            }

            AnimatorStateMachine actionLayer = controller.layers
                .Single(layer => layer.name == "Action Override")
                .stateMachine;
            AnimatorState sneakBowDraw = actionLayer.states
                .Select(childState => childState.state)
                .Single(candidate => candidate.name == "Sneak Bow Draw");
            Assert.That(sneakBowDraw.motion, Is.Not.Null);
            Assert.That(sneakBowDraw.transitions.Length, Is.EqualTo(3));
            Assert.That(
                ReadRuntimeSource("Character/CharacterAnimatorManager.cs"),
                Does.Contain("s_sneakBowDrawState"));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_StealthObjectPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Collider>()?.isTrigger, Is.True);
            Type stealthObjectType = GetRuntimeType("ZZ.StealthObject");
            Assert.That(prefab.GetComponent(stealthObjectType), Is.Not.Null);
            Assert.That(
                stealthObjectType.GetProperty("CharactersStandingInStealthObject"),
                Is.Not.Null);
        }

        public static void RunAllFocusedTests()
        {
            SneakingSystemTests tests = new();
            tests.NamedSneakActionSupportsKeyboardAndGamepadToggleInput();
            tests.SneakingAndHiddenStatesUseCorrectNetworkAuthorities();
            tests.SneakMovementHasDistinctForwardAndBackwardSpeeds();
            tests.TargetRelationsDriveHiddenAndConcealmentDetection();
            tests.SneakingSuppressesFootstepsAndManualStateChangesWin();
            tests.AnimatorAndStealthPrefabContainRequiredAssets();
            Debug.Log("[SneakingSystemTests] 6 EP153-155 focused tests passed.");
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
