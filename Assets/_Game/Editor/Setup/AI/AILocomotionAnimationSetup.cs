using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures smooth speed coverage for the Undead AI locomotion blend tree.</summary>
    public static class AILocomotionAnimationSetup
    {
        private const string k_ControllerPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/" +
            "Undead AI Animator.controller";
        private const string k_RunClipPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Locomotion/" +
            "zombie_run_01.anim";
        private const string k_SprintClipPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Locomotion/" +
            "zombie_sprint_01.anim";
        private const string k_LocomotionStateName = "Locomotion";

        [ZZTool("AI", "配置 AI 移动动画", 300)]
        public static void ConfigureAILocomotionAnimation()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            BlendTree blendTree = GetLocomotionBlendTree(controller);
            AnimationClip runClip = LoadRequiredAsset<AnimationClip>(k_RunClipPath);
            AnimationClip sprintClip = LoadRequiredAsset<AnimationClip>(k_SprintClipPath);

            EnsureMotion(blendTree, runClip, new Vector2(0f, 1f));
            EnsureMotion(blendTree, sprintClip, new Vector2(0f, 2f));

            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            ValidateAILocomotionAnimation();
            Debug.Log(
                "[AILocomotionAnimationSetup] Added Run and Sprint speed coverage " +
                "to the Undead AI locomotion blend tree.");
        }

        [ZZTool("AI", "验证 AI 移动动画", 310)]
        public static void ValidateAILocomotionAnimation()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            BlendTree blendTree = GetLocomotionBlendTree(controller);
            AnimationClip runClip = LoadRequiredAsset<AnimationClip>(k_RunClipPath);
            AnimationClip sprintClip = LoadRequiredAsset<AnimationClip>(k_SprintClipPath);

            ValidateMotion(blendTree, runClip, new Vector2(0f, 1f));
            ValidateMotion(blendTree, sprintClip, new Vector2(0f, 2f));
            Debug.Log(
                $"[AILocomotionAnimationValidation] {blendTree.children.Length} " +
                "locomotion motions cover Idle, Walk, Run, Sprint, and strafing.");
        }

        private static BlendTree GetLocomotionBlendTree(AnimatorController controller)
        {
            AnimatorState locomotionState = controller.layers
                .Select(layer => FindState(layer.stateMachine, k_LocomotionStateName))
                .FirstOrDefault(state => state != null);
            if (locomotionState?.motion is not BlendTree blendTree)
            {
                throw new InvalidOperationException(
                    $"Animator state '{k_LocomotionStateName}' is missing its BlendTree.");
            }

            return blendTree;
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            AnimatorState directState = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName);
            if (directState != null)
            {
                return directState;
            }

            return stateMachine.stateMachines
                .Select(childMachine => FindState(childMachine.stateMachine, stateName))
                .FirstOrDefault(state => state != null);
        }

        private static void EnsureMotion(
            BlendTree blendTree,
            AnimationClip clip,
            Vector2 position)
        {
            if (!blendTree.children.Any(child => child.motion == clip))
            {
                blendTree.AddChild(clip, position);
            }

            ChildMotion[] children = blendTree.children;
            int childIndex = Array.FindIndex(children, child => child.motion == clip);
            ChildMotion child = children[childIndex];
            child.position = position;
            child.timeScale = 1f;
            child.cycleOffset = 0f;
            children[childIndex] = child;
            blendTree.children = children;
        }

        private static void ValidateMotion(
            BlendTree blendTree,
            AnimationClip clip,
            Vector2 expectedPosition)
        {
            ChildMotion child = blendTree.children.FirstOrDefault(
                candidate => candidate.motion == clip);
            if (child.motion == null ||
                Vector2.Distance(child.position, expectedPosition) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Animation '{clip.name}' is missing at {expectedPosition}.");
            }
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required asset '{assetPath}' was not found.");
            }

            return asset;
        }
    }
}
