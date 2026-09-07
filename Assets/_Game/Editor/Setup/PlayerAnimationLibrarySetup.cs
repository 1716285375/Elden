using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Connects authored humanoid motions while preserving runtime animation-event contracts.</summary>
    public static class PlayerAnimationLibrarySetup
    {
        private const string k_Root = "Assets/_Game/Art/Characters/Shared/Humanoid/";
        private const string k_Output = "Assets/_Game/Data/Animations/Player";

        [MenuItem("Tools/ZZ/Connect Player Animation Library")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before updating animation assets.");
            }
            if (!AssetDatabase.IsValidFolder(k_Output))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data/Animations", "Player");
            }
            var paths = Directory.GetFiles(k_Root + "Animations", "*.anim", SearchOption.AllDirectories)
                .ToDictionary(Path.GetFileName, path => path.Replace('\\', '/'), StringComparer.Ordinal);
            var runtime = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Root + "AnimationControllers/Runtime/Humanoid Runtime.controller");
            var savedOverrides = AssetDatabase.FindAssets("t:AnimatorOverrideController", new[] { "Assets/_Game" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(controller => controller.runtimeAnimatorController == runtime)
                .ToDictionary(controller => controller, Snapshot);
            var replacements = new Dictionary<AnimationClip, AnimationClip>();
            var states = runtime.layers.SelectMany(layer => EnumerateStates(layer.stateMachine)).ToArray();
            var motions = new Dictionary<string, string>
            {
                { "Dead_01", "core_oh_death_01_Variant_01.anim" },
                { "Bow_Draw", "bow_th_draw_01_Variant_01.anim" },
                { "Bow_Draw 0", "bow_th_draw_01_Variant_01.anim" },
                { "Bow_Aim", "bow_th_aim_01_Variant_01.anim" },
                { "Bow_Fire", "bow_th_fire_01.anim" },
                { "Bow_Out_Of_Ammo", "bow_th_quickdraw_01.anim" },
                { "Drink Start", "core_main_flask_meidum_01_up.anim" },
                { "Drink 01", "core_main_flask_medium_01_drink.anim" },
                { "Drink 02", "core_main_flask_medium_01_drink.anim" },
                { "Drink End", "core_main_flask_meidum_01_down.anim" },
                { "Empty Flask", "core_flask_empty_01.anim" }
            };
            foreach (AnimatorState state in states)
            {
                if (!motions.TryGetValue(state.name, out string file) || state.motion is not AnimationClip original)
                {
                    continue;
                }
                AnimationClip motion = Adapt(original, Load(paths, file), "Player " + state.name);
                replacements[original] = motion;
                state.motion = motion;
                EditorUtility.SetDirty(state);
            }
            // Remap override keys when replacing a base clip; keep each weapon's authored replacement intact.
            foreach (var entry in savedOverrides)
            {
                entry.Key.ApplyOverrides(entry.Value.Select(pair => new KeyValuePair<AnimationClip, AnimationClip>(
                    replacements.TryGetValue(pair.Key, out AnimationClip changed) ? changed : pair.Key,
                    pair.Value == pair.Key ? null : pair.Value)).ToList());
                EditorUtility.SetDirty(entry.Key);
            }
            var bow = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(k_Root + "Animations/Combat/Bow/Bow.overrideController");
            var bowPairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (AnimationClip original in runtime.animationClips.Distinct())
            {
                string candidate = original.name.Replace("core_main_", "bow_th_").Replace("core_th_", "bow_th_");
                if (original.name == "unarmed_main_idle_01" || original.name == "straight_sword_th_idle_01")
                {
                    candidate = "bow_th_idle_01";
                }
                if (candidate == original.name || !paths.TryGetValue(candidate + ".anim", out string path))
                {
                    continue;
                }
                AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (!source.humanMotion)
                {
                    continue;
                }
                bowPairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(original,
                    Adapt(original, source, "Bow " + original.name)));
            }
            bow.ApplyOverrides(bowPairs);
            EditorUtility.SetDirty(bow);
            EditorUtility.SetDirty(runtime);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/player-animation-library.txt",
                $"Player states: {motions.Count}; bow locomotion overrides: {bowPairs.Count}; preserved weapon controllers: {savedOverrides.Count}\n" +
                string.Join("\n", motions.Select(pair => pair.Key + " -> " + pair.Value)));
        }

        private static List<KeyValuePair<AnimationClip, AnimationClip>> Snapshot(AnimatorOverrideController controller)
        {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            return pairs;
        }

        [MenuItem("Tools/ZZ/Connect Catalyst Animation Library")]
        public static void ConnectCatalyst()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before updating the catalyst controller.");
            }
            var runtime = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                k_Root + "AnimationControllers/Runtime/Humanoid Runtime.controller");
            var imported = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                k_Root + "AnimationControllers/Overrides/Overrides/Charm.overrideController");
            string path = k_Output + "/Catalyst.overrideController";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(runtime);
                AssetDatabase.CreateAsset(controller, path);
            }
            var importedPairs = Snapshot(imported);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (AnimationClip original in runtime.animationClips.Distinct())
            {
                string key = original.name.Replace("straight_sword_", "unarmed_");
                AnimationClip source = importedPairs.FirstOrDefault(pair => pair.Key.name == key).Value;
                if (source != null && source.humanMotion && source != original)
                {
                    pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(original,
                        Adapt(original, source, "Catalyst " + original.name)));
                }
            }
            controller.ApplyOverrides(pairs);
            var weapon = new SerializedObject(AssetDatabase.LoadAssetAtPath<WeaponItem>(
                "Assets/_Game/Data/Items/Weapons/Catalysts/Incantation Catalyst.asset"));
            weapon.FindProperty("m_weaponAnimator").objectReferenceValue = controller;
            weapon.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            File.WriteAllText(".utmp/catalyst-animation-library.txt", $"Connected {pairs.Count} motions to the gameplay state machine.");
        }

        private static AnimationClip Load(Dictionary<string, string> paths, string file)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(paths[file]);
            if (!clip.humanMotion)
            {
                throw new InvalidOperationException(file + " is not a humanoid motion.");
            }
            return clip;
        }

        private static AnimationClip Adapt(AnimationClip original, AnimationClip source, string name)
        {
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(original);
            string path = k_Output + "/" + name + ".anim";
            AnimationClip result = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            float originalDuration = original.length;
            if (result == null)
            {
                result = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(result, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, result);
            }
            result.name = name;
            foreach (AnimationEvent animationEvent in events)
            {
                animationEvent.time *= result.length / Mathf.Max(originalDuration, 0.001f);
                if (animationEvent.functionName == "ReleaseArrow")
                {
                    AnimationEvent fire = AnimationUtility.GetAnimationEvents(source)
                        .FirstOrDefault(candidate => candidate.functionName == "FireProjectile");
                    if (fire != null)
                    {
                        animationEvent.time = fire.time;
                    }
                }
            }
            AnimationUtility.SetAnimationEvents(result, events);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static IEnumerable<AnimatorState> EnumerateStates(AnimatorStateMachine machine)
        {
            foreach (ChildAnimatorState state in machine.states)
            {
                yield return state.state;
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                foreach (AnimatorState state in EnumerateStates(child.stateMachine))
                {
                    yield return state;
                }
            }
        }
    }
}
