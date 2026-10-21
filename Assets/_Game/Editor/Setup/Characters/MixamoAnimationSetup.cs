using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Imports the official Mixamo library and connects the broadsword's one-handed idle.</summary>
    public static class MixamoAnimationSetup
    {
        private const string k_Root = "Assets/_Game/Art/Characters/Shared/Humanoid/Mixamo/SwordAndShield";
        private const string k_Controller = "Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController";

        [MenuItem("Tools/ZZ/Connect Mixamo Sword And Shield")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before importing Mixamo motions.");
            }
            string[] paths = Directory.GetFiles(k_Root, "*.fbx").OrderBy(path => path).ToArray();
            if (paths.Length != 49)
            {
                throw new InvalidDataException("Expected the verified 49-animation Sword and Shield Pack.");
            }
            var report = new List<string>();
            foreach (string file in paths)
            {
                string path = file.Replace('\\', '/');
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    clip.name = name;
                    clip.loopTime = name.Contains("idle") || name.Contains("walk") || name.Contains("run") ||
                        name.Contains("strafe");
                    if (clip.loopTime)
                    {
                        clip.lockRootRotation = true;
                        clip.lockRootHeightY = true;
                        clip.lockRootPositionXZ = true;
                        clip.keepOriginalOrientation = true;
                        clip.keepOriginalPositionY = true;
                        clip.keepOriginalPositionXZ = true;
                    }
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                AnimationClip motion = LoadMotion(path);
                Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().Single();
                if (!avatar.isValid || !avatar.isHuman || motion.length <= 0f)
                {
                    throw new InvalidDataException("Invalid humanoid import: " + path);
                }
                report.Add(path + "\tHumanoid=PASS\tSeconds=" + motion.length + "\tLoop=" + motion.isLooping);
                File.WriteAllLines("Docs/ArtRefresh/mixamo-import.tsv", report);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(k_Controller);
            if (controller == null)
            {
                throw new InvalidDataException("Missing runtime broadsword override controller.");
            }
            string backup = "Docs/ArtRefresh/Broadsword.before-mixamo.overrideController.txt";
            if (!File.Exists(backup))
            {
                File.Copy(k_Controller, backup);
            }
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            AnimationClip original = pairs.Select(pair => pair.Key).Single(clip => clip.name == "unarmed_main_idle_01");
            AnimationClip source = LoadMotion(k_Root + "/sword and shield idle.fbx");
            AnimationClip adapted = PlayerAnimationLibrarySetup.Adapt(original, source, "Mixamo Broadsword Idle");
            int eventCount = AnimationUtility.GetAnimationEvents(original).Length;
            if (AnimationUtility.GetAnimationEvents(adapted).Length != eventCount || !adapted.humanMotion)
            {
                throw new InvalidDataException("The adapted idle lost its animation contract.");
            }
            controller.ApplyOverrides(new[] { new KeyValuePair<AnimationClip, AnimationClip>(original, adapted) });
            if (controller[original] != adapted)
            {
                throw new InvalidDataException("The broadsword idle override was not applied.");
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/ArtRefresh/mixamo-integration.txt",
                "Imported=49; Humanoid=PASS; BroadswordOneHandedIdle=PASS; AnimationEvents=PASS\n" +
                "Attack, dodge and projectile timings retain their authored clips.\n");
        }

        private static AnimationClip LoadMotion(string path)
        {
            AnimationClip motion = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Single(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (!motion.humanMotion)
            {
                throw new InvalidDataException("Expected humanoid motion: " + path);
            }
            return motion;
        }
    }
}
