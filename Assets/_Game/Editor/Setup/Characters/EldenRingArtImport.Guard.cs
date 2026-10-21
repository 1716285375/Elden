using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        [MenuItem("Tools/ZZ/Elden Ring/Install Original Shield Guard")]
        public static void InstallOriginalShieldGuard()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before importing guard motions.");
            }
            AnimationClip source = ImportOriginalShieldMotion(
                "F:/MyProject/RE/EldenRing/Converted/GuardRepair/", "c0000_a00_lo", "a000_000100", true);
            RestoreOriginalWeaponMotionCurves();
            int installed = 0;
            foreach (string path in new[]
            {
                "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Straight Sword Animator.overrideController",
                "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Medium Shield Animator.overrideController",
                "Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController"
            })
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(pairs);
                for (int index = 0; index < pairs.Count; index++)
                {
                    string slot = pairs[index].Key.name;
                    bool idle = slot == "core_off_guard_idle_01";
                    if (!idle && !slot.StartsWith("core_off_walk_guard_") && !slot.StartsWith("core_off_run_guard_"))
                    {
                        continue;
                    }
                    AnimationClip original = pairs[index].Value != null ? pairs[index].Value : pairs[index].Key;
                    string name = "a000_000100 " + slot;
                    AnimationClip adapted = PlayerAnimationLibrarySetup.Adapt(original, idle ? source : original, name);
                    if (!idle)
                    {
                        // Retain the existing directional footwork; source guard muscles replace only the upper body.
                        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
                        {
                            string property = binding.propertyName;
                            if (binding.type == typeof(Animator) && (property.StartsWith("Root") ||
                                property.Contains("Leg") || property.Contains("Foot") || property.Contains("Toes")))
                            {
                                continue;
                            }
                            AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                            var keys = curve.keys;
                            for (int key = 0; key < keys.Length; key++)
                            {
                                keys[key].time *= adapted.length / source.length;
                                keys[key].inTangent *= source.length / adapted.length;
                                keys[key].outTangent *= source.length / adapted.length;
                            }
                            AnimationUtility.SetEditorCurve(adapted, binding, new AnimationCurve(keys));
                        }
                    }
                    adapted.EnsureQuaternionContinuity();
                    EditorUtility.SetDirty(adapted);
                    pairs[index] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[index].Key, adapted);
                    installed++;
                }
                controller.ApplyOverrides(pairs);
                EditorUtility.SetDirty(controller);
            }
            if (installed < 15)
            {
                throw new InvalidOperationException("Expected guard idle and four directional slots on three controllers.");
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/ParryRepair");
            File.WriteAllText("Docs/ParryRepair/guard-install.txt", "Installed a000_000100 guard; slots=" + installed +
                "; source upper body and four equipment tracks; directional footwork retained.\n");
        }
    }
}
