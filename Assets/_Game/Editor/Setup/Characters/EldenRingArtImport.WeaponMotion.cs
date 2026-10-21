using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        [MenuItem("Tools/ZZ/Elden Ring/Restore Original Weapon Motion Curves")]
        public static void RestoreOriginalWeaponMotionCurves()
        {
            if (Application.isPlaying) { throw new InvalidOperationException("Stop Play Mode first."); }
            Directory.CreateDirectory("Docs/OriginalActionRepair");
            const string source = "F:/MyProject/RE/EldenRing/Converted/PlayerActions/WeaponCurves";
            foreach (string prefabPath in new[] { k_Player, "Assets/_Game/Resources/UI/CharacterPreview.prefab" })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    ApplyOriginalIdleWeaponPose(root);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(k_Player);
            Animator animator = player.GetComponentInChildren<Animator>(true);
            var report = new StringBuilder();
            foreach (string path in Directory.GetFiles(source, "a*.json"))
            {
                var document = JsonUtility.FromJson<WeaponMotionDocument>(File.ReadAllText(path));
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    k_ActionOutput + "/Humanoid/Clips/" + document.Name + ".anim");
                if (clip == null || document.Frames.Length < 2 || Mathf.Abs(clip.length - document.Duration) > 0.05f)
                {
                    throw new InvalidOperationException("Weapon track duration does not match " + document.Name);
                }
                foreach (string boneName in new[] { "L_Weapon", "R_Weapon", "L_Shield", "R_Shield" })
                {
                    Transform anchor = player.GetComponentsInChildren<Transform>(true)
                        .Single(item => item.name == "ER_" + boneName);
                    string bindingPath = AnimationUtility.CalculateTransformPath(anchor, animator.transform);
                    foreach (EditorCurveBinding stale in AnimationUtility.GetCurveBindings(clip).Where(binding =>
                        binding.path.EndsWith("/ER_" + boneName) && binding.path != bindingPath))
                    {
                        AnimationUtility.SetEditorCurve(clip, stale, null);
                    }
                    for (int channel = 0; channel < 7; channel++)
                    {
                        int component = channel < 3 ? channel : channel - 3;
                        var keys = new Keyframe[document.Frames.Length];
                        for (int index = 0; index < keys.Length; index++)
                        {
                            WeaponMotionFrame frame = document.Frames[index];
                            float[] values = frame.GetTrack(boneName, channel >= 3);
                            keys[index] = new Keyframe(frame.Time, values[component]);
                        }
                        var curve = new AnimationCurve(keys);
                        for (int index = 0; index < keys.Length; index++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                        }
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bindingPath,
                            typeof(Transform), (channel < 3 ? "m_LocalPosition." : "m_LocalRotation.") + "xyzw"[component]), curve);
                    }
                }
                clip.EnsureQuaternionContinuity();
                EditorUtility.SetDirty(clip);
                report.AppendLine(document.Name + ": original left/right weapon translation and rotation; frames=" +
                    document.Frames.Length);
            }
            int gameplaySlots = 0;
            foreach (string path in new[]
            {
                "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Straight Sword Animator.overrideController",
                "Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController"
            })
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(pairs);
                foreach (var pair in pairs)
                {
                    AnimationClip destination = pair.Value;
                    if (destination == null) { continue; }
                    string name = destination.name;
                    string motion = name.EndsWith("straight_sword_main_light_attack_01") ? "a030_030000" :
                        name.EndsWith("straight_sword_main_light_attack_02") ? "a030_030010" :
                        pair.Key.name == "unarmed_main_idle_01" ? "a000_000000" : null;
                    if (motion == null) { continue; }
                    AnimationClip original = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        k_ActionOutput + "/Humanoid/Clips/" + motion + ".anim");
                    foreach (EditorCurveBinding stale in AnimationUtility.GetCurveBindings(destination).Where(binding =>
                        binding.path.EndsWith("ER_L_Shield") || binding.path.EndsWith("ER_R_Shield")))
                    {
                        AnimationUtility.SetEditorCurve(destination, stale, null);
                    }
                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(original)
                        .Where(item => item.path.EndsWith("ER_L_Weapon") || item.path.EndsWith("ER_R_Weapon") ||
                            item.path.EndsWith("ER_L_Shield") || item.path.EndsWith("ER_R_Shield")))
                    {
                        AnimationUtility.SetEditorCurve(destination, binding, AnimationUtility.GetEditorCurve(original, binding));
                    }
                    destination.EnsureQuaternionContinuity();
                    EditorUtility.SetDirty(destination);
                    gameplaySlots++;
                    report.AppendLine("GAMEPLAY: " + destination.name + " <- " + motion);
                }
            }
            if (gameplaySlots != 6)
            {
                throw new InvalidOperationException("Expected six installed sword motion slots, found " + gameplaySlots);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/OriginalActionRepair/weapon-motion-import.txt", report.ToString());
        }

        private static void ApplyOriginalIdleWeaponPose(GameObject root)
        {
            var document = JsonUtility.FromJson<WeaponMotionDocument>(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/PlayerActions/WeaponCurves/a000_000000.json"));
            WeaponMotionFrame frame = document.Frames[0];
            Animator animator = root.GetComponentInChildren<Animator>(true);
            foreach (string boneName in new[] { "L_Weapon", "R_Weapon", "L_Shield", "R_Shield" })
            {
                Transform anchor = root.GetComponentsInChildren<Transform>(true)
                    .SingleOrDefault(item => item.name == "ER_" + boneName);
                if (anchor == null)
                {
                    anchor = new GameObject("ER_" + boneName).transform;
                }
                anchor.SetParent(animator.GetBoneTransform(boneName.StartsWith("L_")
                    ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand), false);
                float[] position = frame.GetTrack(boneName, false);
                float[] rotation = frame.GetTrack(boneName, true);
                anchor.localPosition = new Vector3(position[0], position[1], position[2]);
                anchor.localRotation = new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]).normalized;
            }
            Transform shieldAnchor = root.GetComponentsInChildren<Transform>(true).Single(item => item.name == "ER_L_Shield");
            WeaponModelInstantiationSlot shieldSlot = root.GetComponentsInChildren<WeaponModelInstantiationSlot>(true)
                .Single(item => item.WeaponModelSlot == WeaponModelSlot.LeftHandShieldSlot);
            shieldSlot.transform.SetParent(shieldAnchor, false);
            shieldSlot.transform.localPosition = Vector3.zero;
            shieldSlot.transform.localRotation = Quaternion.identity;
            shieldSlot.transform.localScale = Vector3.one;
        }

        [Serializable]
        private sealed class WeaponMotionDocument
        {
            public string Name;
            public float Duration;
            public WeaponMotionFrame[] Frames;
        }

        [Serializable]
        private sealed class WeaponMotionFrame
        {
            public float Time;
            public float[] LeftPosition;
            public float[] LeftRotation;
            public float[] RightPosition;
            public float[] RightRotation;
            public float[] LeftShieldPosition;
            public float[] LeftShieldRotation;
            public float[] RightShieldPosition;
            public float[] RightShieldRotation;

            public float[] GetTrack(string bone, bool rotation)
            {
                return bone switch
                {
                    "L_Weapon" => rotation ? LeftRotation : LeftPosition,
                    "R_Weapon" => rotation ? RightRotation : RightPosition,
                    "L_Shield" => rotation ? LeftShieldRotation : LeftShieldPosition,
                    "R_Shield" => rotation ? RightShieldRotation : RightShieldPosition,
                    _ => throw new ArgumentOutOfRangeException(nameof(bone), bone, "Unknown original equipment bone")
                };
            }
        }
    }
}
