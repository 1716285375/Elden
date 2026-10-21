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
        [MenuItem("Tools/ZZ/Elden Ring/Install Original Medium Shield Parry")]
        public static void InstallOriginalMediumShieldParry()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before importing the parry.");
            }
            const string name = "a692_040040";
            const string extracted = "F:/MyProject/RE/EldenRing/Converted/ParryRepair/";
            AnimationClip saved = ImportOriginalShieldMotion(extracted, "c0000_a9x", name, false);
            RestoreOriginalWeaponMotionCurves();
            var shield = new SerializedObject(AssetDatabase.LoadAssetAtPath<WeaponItem>(
                "Assets/_Game/Data/Items/Weapons/Melee Weapons/Medium Shield.asset"));
            var controller = (AnimatorOverrideController)shield.FindProperty("m_weaponAnimator").objectReferenceValue;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            int index = pairs.FindIndex(pair => pair.Key.name == "shield_off_parry_01_slow_start");
            if (index < 0)
            {
                throw new InvalidOperationException("Medium shield parry slot is missing.");
            }
            AnimationClip original = pairs[index].Value != null ? pairs[index].Value : pairs[index].Key;
            AnimationClip adapted = PlayerAnimationLibrarySetup.Adapt(original, saved, name + " Medium Shield Parry");
            // a692.tae/40040 invokes shield behavior 64 from frame 4 through frame 10 at 30 Hz.
            AnimationUtility.SetAnimationEvents(adapted, new[]
            {
                new AnimationEvent { functionName = "EnableIsParrying", time = 4f / 30f },
                new AnimationEvent { functionName = "DisableIsParrying", time = 10f / 30f }
            });
            pairs[index] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[index].Key, adapted);
            controller.ApplyOverrides(pairs);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/ParryRepair");
            File.WriteAllText("Docs/ParryRepair/install.txt", "Installed " + name +
                "; original humanoid motion and four equipment tracks; duration=" + saved.length +
                ". Shield behavior window copied from a692.tae/40040: 4/30 through 10/30 seconds.\n");
        }

        private static AnimationClip ImportOriginalShieldMotion(string extracted, string archive, string name, bool loop)
        {
            string output = k_ActionOutput + "/Humanoid";
            string path = output + "/Source/" + name + ".fbx";
            File.Copy(extracted + "HumanoidCalibrated/Animations/" + archive + "/" + name + ".fbx", path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(output + "/Source/c0000.fbx").OfType<Avatar>().Single();
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            clips[0].name = name;
            clips[0].loopTime = loop;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Single(item => !item.name.StartsWith("__preview__"));
            if (!source.humanMotion || source.length <= 0f)
            {
                throw new InvalidOperationException("The extracted shield motion is not a valid humanoid motion.");
            }
            string clipPath = output + "/Clips/" + name + ".anim";
            AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (saved == null)
            {
                saved = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(saved, clipPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, saved);
            }
            EditorUtility.SetDirty(saved);
            File.Copy(extracted + "WeaponCurves/" + name + ".json",
                "F:/MyProject/RE/EldenRing/Converted/PlayerActions/WeaponCurves/" + name + ".json", true);
            return saved;
        }
    }
}
