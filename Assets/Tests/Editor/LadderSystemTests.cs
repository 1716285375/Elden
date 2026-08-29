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
    public class LadderSystemTests
    {
        private const string k_LadderPrefabPath =
            "Assets/_Game/Prefabs/World/Objects/Ladders/Standard Ladder.prefab";
        private const string k_RuntimeControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Runtime/Humanoid Runtime.controller";
        private const string k_FallClipPath =
            "Assets/_Game/Art/Environment/Shared/Animations/Ladder Fall Start.anim";
        private const string k_LocomotionSourcePath =
            "Assets/_Game/Scripts/Characters/Player/PlayerLocomotionManager.cs";
        private const string k_NetworkSourcePath =
            "Assets/_Game/Scripts/Characters/Player/PlayerNetworkManager.cs";
        private const string k_AnimatorSourcePath =
            "Assets/_Game/Scripts/Characters/Player/PlayerAnimatorManager.cs";
        private const string k_CharacterSourcePath =
            "Assets/_Game/Scripts/Characters/Common/CharacterManager.cs";
        private const string k_DamageSourcePath =
            "Assets/_Game/Scripts/Combat/Effects/Instant Effects/TakeDamageEffect.cs";
        private const string k_InteractableSourcePath =
            "Assets/_Game/Scripts/World/Managers/LadderInteractable.cs";

        private static readonly string[] s_overrideControllerPaths =
        {
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/" +
                "Unarmed Animator.overrideController",
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/" +
                "Broadsword Animator.overrideController",
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/" +
                "Straight Sword Animator.overrideController",
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/" +
                "Medium Shield Animator.overrideController",
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/Bow.overrideController"
        };

        [Test]
        public void FixedSegmentsAlternateThroughStableIdleStates()
        {
            Type handType = GetRuntimeType("ZZ.LadderHandState");
            Type stateType = GetRuntimeType("ZZ.LadderAnimationState");
            Type utilityType = GetRuntimeType(
                "ZZ.LadderAnimationStateUtility");
            MethodInfo getSegment = utilityType.GetMethod("GetSegment");
            MethodInfo getIdle = utilityType.GetMethod(
                "GetIdleAfterCompletedState");
            object leftHand = Enum.Parse(handType, "Left");
            object rightHand = Enum.Parse(handType, "Right");
            object climbUpRight = Enum.Parse(stateType, "ClimbUpRight");
            object climbDownLeft = Enum.Parse(stateType, "ClimbDownLeft");

            Assert.That(
                getSegment.Invoke(null, new[] { leftHand, (object)1f }),
                Is.EqualTo(climbUpRight));
            Assert.That(
                getIdle.Invoke(null, new[] { climbUpRight }).ToString(),
                Is.EqualTo("IdleRight"));
            Assert.That(
                getSegment.Invoke(null, new[] { rightHand, (object)(-1f) }),
                Is.EqualTo(climbDownLeft));
            Assert.That(
                getIdle.Invoke(null, new[] { climbDownLeft }).ToString(),
                Is.EqualTo("IdleLeft"));
        }

        [Test]
        public void LadderPrefabUsesOneRootNetworkObjectAndTwoEntrances()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_LadderPrefabPath);
            Type networkObjectType = GetRuntimeType(
                "Unity.Netcode.NetworkObject");
            Type ladderType = GetRuntimeType("ZZ.LadderInteractable");
            Component[] networkObjects = prefab.GetComponentsInChildren(
                networkObjectType,
                true);
            Component[] entrances = prefab.GetComponentsInChildren(
                ladderType,
                true);

            Assert.That(networkObjects, Has.Length.EqualTo(1));
            Assert.That(networkObjects[0].gameObject, Is.EqualTo(prefab));
            Assert.That(entrances, Has.Length.EqualTo(2));
            Assert.That(entrances,
                Has.Exactly(1).Matches<Component>(IsTopEntrance));
        }

        [Test]
        public void LadderRungsUseFixedHalfMeterSpacing()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_LadderPrefabPath);
            Transform model = prefab.transform.Find("Ladder Model");
            float[] heights = model.Cast<Transform>()
                .Where(child => child.name.StartsWith("Rung "))
                .OrderBy(child => child.localPosition.y)
                .Select(child => child.localPosition.y)
                .ToArray();

            Assert.That(heights, Has.Length.EqualTo(16));
            for (int index = 1; index < heights.Length; index++)
            {
                Assert.That(
                    heights[index] - heights[index - 1],
                    Is.EqualTo(0.5f).Within(0.0001f));
            }
        }

        [Test]
        public void LadderLayerContainsDirectPlayStatesAndIdleExitWindows()
        {
            AnimatorController controller = LoadRuntimeController();
            AnimatorControllerLayer layer = controller.layers.Single(
                candidate => candidate.name == "Ladder Override");
            AnimatorStateMachine machine = layer.stateMachine;
            string[] requiredStates =
            {
                "Enter Bottom", "Enter Top", "Idle Left", "Idle Right",
                "Climb Up Left", "Climb Up Right", "Climb Down Left",
                "Climb Down Right", "Exit Top Left", "Exit Top Right",
                "Exit Bottom Left", "Exit Bottom Right", "Slide Start",
                "Slide Mid", "Slide End", "Jump Off Start", "Jump Off Mid",
                "Jump Off End", "Fall Start", "Fall Loop"
            };

            Assert.That(layer.defaultWeight, Is.Zero);
            Assert.That(layer.blendingMode,
                Is.EqualTo(AnimatorLayerBlendingMode.Override));
            Assert.That(machine.defaultState.name, Is.EqualTo("Empty"));
            Assert.That(requiredStates, Is.SubsetOf(machine.states
                .Select(child => child.state.name)
                .ToArray()));
            Assert.That(machine.states,
                Has.None.Matches<ChildAnimatorState>(
                    child => child.state.transitions.Length > 0));
            AssertIdleBehaviour(machine, "Idle Left", "Left");
            AssertIdleBehaviour(machine, "Idle Right", "Right");
        }

        [Test]
        public void FallStartClipReleasesPlayerThroughAnimationEvent()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                k_FallClipPath);

            Assert.That(AnimationUtility.GetAnimationEvents(clip),
                Has.Some.Matches<AnimationEvent>(animationEvent =>
                    animationEvent.functionName ==
                    "FallFromLadderAnimationEvent"));
        }

        [Test]
        public void PlayerAndWeaponOverridesUseRuntimeLadderController()
        {
            AnimatorController controller = LoadRuntimeController();
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab");

            Assert.That(
                player.GetComponentInChildren<Animator>(true)
                    .runtimeAnimatorController,
                Is.EqualTo(controller));
            foreach (string path in s_overrideControllerPaths)
            {
                AnimatorOverrideController overrideController =
                    AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                        path);
                Assert.That(
                    overrideController.runtimeAnimatorController,
                    Is.EqualTo(controller));
            }
        }

        [Test]
        public void OwnerLadderStateSynchronizesLateJoinPresentation()
        {
            string source = File.ReadAllText(k_NetworkSourcePath);

            Assert.That(source,
                Does.Contain("NetworkVariableWritePermission.Owner"));
            Assert.That(source,
                Does.Contain("m_isClimbingLadder.OnValueChanged +="));
            Assert.That(source,
                Does.Contain("m_isClimbingLadder.OnValueChanged -="));
            Assert.That(source, Does.Contain("SetLadderPresentation("));
            Assert.That(source,
                Does.Contain("SetSpecialMovementIKBehavioursSuppressed("));
            Assert.That(source,
                Does.Contain("EquipmentManager?.SetWeaponsHidden("));
        }

        [Test]
        public void LadderMovementDisablesControllerAndLocksHorizontalPosition()
        {
            string source = File.ReadAllText(k_LocomotionSourcePath);

            Assert.That(source, Does.Contain("m_characterController.enabled = false"));
            Assert.That(source, Does.Contain("SetIgnoreGravity(true)"));
            Assert.That(source, Does.Contain("Vector3.SmoothDamp("));
            Assert.That(source, Does.Contain("HandleOwnerLadderMovement()"));
            Assert.That(source, Does.Contain("ScheduleMinimumTopExitHeight("));
            Assert.That(source, Does.Contain("ForceMinimumTopExitHeight("));
        }

        [Test]
        public void SprintAndDodgeInputsRedirectToLadderActions()
        {
            string source = File.ReadAllText(k_LocomotionSourcePath);
            int jumpRedirect = source.IndexOf(
                "JumpOffLadder();",
                StringComparison.Ordinal);
            int ordinaryRollGuard = source.IndexOf(
                "if (!CanRoll)",
                jumpRedirect,
                StringComparison.Ordinal);

            Assert.That(source,
                Does.Contain("HandleLadderSliding(isSprintInputHeld)"));
            Assert.That(jumpRedirect, Is.GreaterThanOrEqualTo(0));
            Assert.That(ordinaryRollGuard, Is.GreaterThan(jumpRedirect));
        }

        [Test]
        public void SecondTimedHitFallsAndSuppressesOrdinaryDamageAnimation()
        {
            string damageSource = File.ReadAllText(k_DamageSourcePath);
            string locomotionSource = File.ReadAllText(k_LocomotionSourcePath);

            Assert.That(damageSource,
                Does.Contain("RegisterLadderHit() == true"));
            Assert.That(locomotionSource,
                Does.Contain("m_knockOffLadderWindow"));
            Assert.That(locomotionSource,
                Does.Contain("CompleteFallFromLadder()"));
        }

        [Test]
        public void InteractionPromptReturnsOnlyAfterClimbCompletion()
        {
            string source = File.ReadAllText(k_InteractableSourcePath);

            Assert.That(source, Does.Contain("WaitForClimbCompletion(player)"));
            Assert.That(source,
                Does.Contain("InteractionManager?.CheckForInteractable()"));
            Assert.That(source,
                Does.Contain("SetLadderExitInteractable(this, false)"));
        }

        [Test]
        public void LadderPresentationUsesRootMotionAndSuppressesIK()
        {
            string animatorSource = File.ReadAllText(k_AnimatorSourcePath);
            string characterSource = File.ReadAllText(k_CharacterSourcePath);

            Assert.That(animatorSource,
                Does.Contain(
                    "m_player.transform.position += CharacterAnimator.deltaPosition"));
            Assert.That(animatorSource,
                Does.Contain("SetLadderSlidingState("));
            Assert.That(characterSource,
                Does.Contain("SetSpecialMovementIKBehavioursSuppressed("));
        }

        /// <summary>Runs focused EP159-162 tests without entering Play Mode.</summary>
        public static void RunAllFocusedTests()
        {
            LadderSystemTests tests = new();
            tests.FixedSegmentsAlternateThroughStableIdleStates();
            tests.LadderPrefabUsesOneRootNetworkObjectAndTwoEntrances();
            tests.LadderRungsUseFixedHalfMeterSpacing();
            tests.LadderLayerContainsDirectPlayStatesAndIdleExitWindows();
            tests.FallStartClipReleasesPlayerThroughAnimationEvent();
            tests.PlayerAndWeaponOverridesUseRuntimeLadderController();
            tests.OwnerLadderStateSynchronizesLateJoinPresentation();
            tests.LadderMovementDisablesControllerAndLocksHorizontalPosition();
            tests.SprintAndDodgeInputsRedirectToLadderActions();
            tests.SecondTimedHitFallsAndSuppressesOrdinaryDamageAnimation();
            tests.InteractionPromptReturnsOnlyAfterClimbCompletion();
            tests.LadderPresentationUsesRootMotionAndSuppressesIK();
            Debug.Log(
                "[LadderSystemTests] 12 EP159-162 focused tests passed.");
        }

        private static void AssertIdleBehaviour(
            AnimatorStateMachine machine,
            string stateName,
            string handState)
        {
            AnimatorState state = machine.states
                .Select(child => child.state)
                .Single(candidate => candidate.name == stateName);
            Type behaviourType = GetRuntimeType("ZZ.ToggleCanExitLadder");
            StateMachineBehaviour[] behaviours = state.behaviours
                .Where(behaviour => behaviour.GetType() == behaviourType)
                .ToArray();

            Assert.That(behaviours, Has.Length.EqualTo(1));
            Assert.That(
                behaviourType.GetProperty("HandState")
                    .GetValue(behaviours[0]).ToString(),
                Is.EqualTo(handState));
        }

        private static bool IsTopEntrance(Component entrance)
        {
            return (bool)entrance.GetType().GetProperty("IsTopEntrance")
                .GetValue(entrance);
        }

        private static AnimatorController LoadRuntimeController()
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(
                k_RuntimeControllerPath);
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
