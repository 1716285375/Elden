using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Connects weapon-specific off-hand, aerial and two-handed locomotion clips.</summary>
    public static class WeaponMotionExpansionSetup
    {
        private const string k_Root = "Assets/_Game/Art/Characters/Shared/Humanoid/";
        private const string k_Output = "Assets/_Game/Data/Animations/Player";

        [MenuItem("Tools/ZZ/Expand Weapon Motions")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before editing weapon animation assets.");
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                k_Root + "AnimationControllers/Runtime/Humanoid Runtime.controller");
            var clips = Directory.GetFiles(k_Root + "Animations", "*.anim", SearchOption.AllDirectories)
                .ToDictionary(Path.GetFileNameWithoutExtension, path => path.Replace('\\', '/'), StringComparer.Ordinal);
            AnimatorStateMachine action = controller.layers.Single(layer => layer.name == "Action Override").stateMachine;
            AnimatorState empty = action.states.Single(child => child.state.name == "Empty").state;
            for (int number = 1; number <= 2; number++)
            {
                string name = "OffHand_Attack_0" + number;
                AnimatorState template = action.states.Single(child => child.state.name == (number == 1 ? "Attack_01" : "Attack_Light_02")).state;
                AnimatorState state = action.states.FirstOrDefault(child => child.state.name == name).state;
                if (state == null)
                {
                    state = action.AddState(name);
                    foreach (StateMachineBehaviour behaviour in template.behaviours)
                    {
                        EditorUtility.CopySerialized(behaviour, state.AddStateMachineBehaviour(behaviour.GetType()));
                    }
                    AnimatorStateTransition transition = state.AddTransition(empty);
                    transition.hasExitTime = true;
                    transition.exitTime = 0.95f;
                    transition.duration = 0.1f;
                }
                state.motion = Adapt((AnimationClip)template.motion, Load(clips, "straight_sword_off_light_attack_0" + number),
                    "OffHand_Attack_0" + number, true);
                state.writeDefaultValues = template.writeDefaultValues;
                EditorUtility.SetDirty(state);
            }
            if (!controller.parameters.Any(parameter => parameter.name == "isTwoHandingLeftWeapon"))
            {
                controller.AddParameter("isTwoHandingLeftWeapon", AnimatorControllerParameterType.Bool);
            }
            foreach (AnimatorState state in controller.layers.SelectMany(layer => States(layer.stateMachine)))
            {
                if (state.name.StartsWith("TwoHand") || state.name.Contains("Two Handed"))
                {
                    state.mirrorParameter = "isTwoHandingLeftWeapon";
                    state.mirrorParameterActive = true;
                    EditorUtility.SetDirty(state);
                }
            }
            ApplyWeapon(controller, clips, "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Straight Sword Animator.overrideController", "straight_sword", false);
            ApplyWeapon(controller, clips, "Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController", "ultra", true);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyWeapon(AnimatorController controller, Dictionary<string, string> clips,
            string path, string prefix, bool heavy)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            var replacements = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (AnimationClip original in controller.animationClips.Distinct())
            {
                string candidate = original.name;
                if (candidate.StartsWith("unarmed_") && candidate.Contains("jump_"))
                {
                    candidate = candidate.Replace("unarmed_", prefix + "_");
                }
                else if (candidate.StartsWith("OffHand_Attack_"))
                {
                    candidate = prefix + "_off_light_attack_" + candidate.Substring(candidate.Length - 2);
                }
                else if (heavy && candidate.StartsWith("core_th_"))
                {
                    candidate = candidate.Replace("core_th_", prefix + "_th_");
                }
                else if (heavy && candidate == "straight_sword_th_idle_01")
                {
                    candidate = "ultra_th_idle_01";
                }
                if (candidate == original.name || !clips.ContainsKey(candidate))
                {
                    continue;
                }
                AnimationClip replacement = Adapt(original, Load(clips, candidate), prefix + " " + original.name,
                    original.name.StartsWith("OffHand_"));
                replacements.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, replacement));
            }
            weapon.ApplyOverrides(replacements);
            EditorUtility.SetDirty(weapon);
            File.WriteAllText(".utmp/weapon-motion-" + prefix + ".txt", string.Join("\n", replacements.Select(pair => pair.Key.name + " -> " + pair.Value.name)));
        }

        private static AnimationClip Load(Dictionary<string, string> clips, string name)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clips[name]);
            if (!clip.humanMotion)
            {
                throw new InvalidOperationException(name + " has no humanoid motion.");
            }
            return clip;
        }

        private static AnimationClip Adapt(AnimationClip template, AnimationClip source, string name, bool offHand)
        {
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(template);
            float templateLength = template.length;
            string path = k_Output + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(clip, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, clip);
            }
            clip.name = name;
            foreach (AnimationEvent animationEvent in events)
            {
                animationEvent.time *= clip.length / Mathf.Max(templateLength, 0.001f);
                if (offHand)
                {
                    animationEvent.functionName = animationEvent.functionName.Replace("MainHandWeaponTrail", "OffHandWeaponTrail");
                }
            }
            AnimationUtility.SetAnimationEvents(clip, events);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
        {
            foreach (ChildAnimatorState state in machine.states)
            {
                yield return state.state;
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                foreach (AnimatorState state in States(child.stateMachine))
                {
                    yield return state;
                }
            }
        }
    }
}
