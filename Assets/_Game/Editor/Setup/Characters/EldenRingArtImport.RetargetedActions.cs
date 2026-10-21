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
        [MenuItem("Tools/ZZ/Elden Ring/Verify Retargeted Player Actions")]
        public static void VerifyRetargetedPlayerActions()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            var report = new System.Text.StringBuilder();
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                animator.fireEvents = false;
                animator.Rebind();
                player.GetComponent<PlayerSkinnedEquipment>().Refresh(true, 4, 5, 6, 7);
                AnimationMode.StartAnimationMode();
                foreach (string name in new[] { "a030_030000", "a030_030010", "a030_030020", "a000_000000",
                    "a030_030030", "a030_030040", "a030_030200", "a030_030210", "a030_030300", "a030_030400" })
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        k_ActionOutput + "/Humanoid/Clips/" + name + ".anim");
                    foreach (float phase in new[] { 0.1f, 0.45f, 0.8f })
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * phase);
                        AnimationMode.EndSampling();
                        foreach (SkinnedMeshRenderer renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .Where(item => item.enabled))
                        {
                            var mesh = new Mesh();
                            renderer.BakeMesh(mesh);
                            bool valid = mesh.bounds.size.magnitude < 5f && mesh.vertices.All(vertex =>
                                float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z));
                            UnityEngine.Object.DestroyImmediate(mesh);
                            if (!valid)
                            {
                                throw new InvalidOperationException("Invalid retargeted skin: " + renderer.name);
                            }
                        }
                    }
                    string filename = name + "-retargeted.png";
                    Capture(player, filename, k_ModularRoot);
                    File.Copy(Path.GetDirectoryName(k_Report) + "/" + filename,
                        "Docs/RigCombatRepair/" + filename, true);
                    report.AppendLine("PASS " + name + "; sampled phases=0.1,0.45,0.8; finite bounded armor skin");
                }
                File.WriteAllText("Docs/RigCombatRepair/animation-verification.txt", report.ToString());
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        [MenuItem("Tools/ZZ/Elden Ring/Install Retargeted Sword Actions")]
        public static void InstallRetargetedSwordActions()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before updating player actions.");
            }
            string[] controllers =
            {
                "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Straight Sword Animator.overrideController",
                "Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController"
            };
            int installed = 0;
            foreach (string path in controllers)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(pairs);
                for (int index = 0; index < pairs.Count; index++)
                {
                    AnimationClip original = pairs[index].Value != null ? pairs[index].Value : pairs[index].Key;
                    string slot = original.name;
                    string motion = slot.EndsWith("straight_sword_main_light_attack_01") ? "a030_030000" :
                        slot.EndsWith("straight_sword_main_light_attack_02") ? "a030_030010" :
                        pairs[index].Key.name == "unarmed_main_idle_01" ? "a000_000000" : null;
                    if (motion == null)
                    {
                        continue;
                    }
                    AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        k_ActionOutput + "/Humanoid/Clips/" + motion + ".anim");
                    AnimationClip adapted = PlayerAnimationLibrarySetup.Adapt(original, source, original.name);
                    pairs[index] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[index].Key, adapted);
                    installed++;
                }
                controller.ApplyOverrides(pairs);
                EditorUtility.SetDirty(controller);
            }
            if (installed != 6)
            {
                throw new InvalidOperationException("Expected six sword action slots, found " + installed);
            }
            var helmet = new SerializedObject(AssetDatabase.LoadAssetAtPath<HeadEquipmentItem>(
                "Assets/_Game/Data/Items/Armor/Starter Hood.asset"));
            helmet.FindProperty("m_headEquipmentType").enumValueIndex = (int)HeadEquipmentType.FullHelmet;
            helmet.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/RigCombatRepair/installed-actions.txt",
                "PASS: four light-attack and two idle slots use extracted c0000 motions; normalized events retained.\n");
        }

        [MenuItem("Tools/ZZ/Elden Ring/Import Retargeted Player Actions")]
        public static void ImportRetargetedPlayerActions()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before importing player actions.");
            }
            const string source = "F:/MyProject/RE/EldenRing/Converted/PlayerActions/HumanoidCalibrated/";
            string output = k_ActionOutput + "/Humanoid";
            Directory.CreateDirectory(output + "/Source");
            Directory.CreateDirectory(output + "/Clips");
            string rigPath = output + "/Source/c0000.fbx";
            File.Copy(source + "c0000.fbx", rigPath, true);
            AssetDatabase.ImportAsset(rigPath, ImportAssetOptions.ForceSynchronousImport);
            var rigImporter = (ModelImporter)AssetImporter.GetAtPath(rigPath);
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(k_Player);
            HumanDescription description = player.GetComponentInChildren<Animator>(true).avatar.humanDescription;
            HumanBone[] human = description.human;
            for (int index = 0; index < human.Length; index++)
            {
                human[index].boneName = human[index].boneName.Replace("ER_Set1100Avatar_", "").Replace("ER_", "");
            }
            description.human = human;
            GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
            description.skeleton = rig.GetComponentsInChildren<Transform>(true).Select(bone => new SkeletonBone
            {
                name = bone.name, position = bone.localPosition, rotation = bone.localRotation, scale = bone.localScale
            }).ToArray();
            rigImporter.animationType = ModelImporterAnimationType.Human;
            rigImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            rigImporter.humanDescription = description;
            rigImporter.importAnimation = false;
            rigImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            rigImporter.SaveAndReimport();
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(rigPath).OfType<Avatar>().Single();
            if (!avatar.isHuman || !avatar.isValid)
            {
                throw new InvalidOperationException("Retargeted c0000 Avatar is invalid.");
            }
            foreach (string name in new[] { "a030_030000", "a030_030010", "a030_030020", "a000_000000",
                "a030_030030", "a030_030040", "a030_030200", "a030_030210", "a030_030300", "a030_030400" })
            {
                string path = output + "/Source/" + name + ".fbx";
                string archive = name.StartsWith("a000_") ? "c0000_a00_lo" : "c0000_a3x";
                string original = source + "Animations/" + archive + "/" + name + ".fbx";
                if (!File.Exists(original))
                {
                    original = "F:/MyProject/RE/EldenRing/Converted/StartingSkills/HumanoidCalibrated/Animations/" +
                        archive + "/" + name + ".fbx";
                }
                File.Copy(original, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                clips[0].name = name;
                clips[0].loopTime = name == "a000_000000";
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Single(item => !item.name.StartsWith("__preview__"));
                if (!clip.humanMotion || clip.length <= 0f)
                {
                    throw new InvalidOperationException("Missing humanoid motion: " + name);
                }
                string clipPath = output + "/Clips/" + name + ".anim";
                AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (saved == null)
                {
                    saved = UnityEngine.Object.Instantiate(clip);
                    AssetDatabase.CreateAsset(saved, clipPath);
                }
                else
                {
                    EditorUtility.CopySerialized(clip, saved);
                    EditorUtility.SetDirty(saved);
                }
            }
            AssetDatabase.SaveAssets();
            RestoreOriginalWeaponMotionCurves();
            File.WriteAllText("Docs/RigCombatRepair/humanoid-import.txt",
                "PASS: valid c0000 humanoid Avatar, nine retargeted actions and one looping idle; " +
                "new motions are not automatically assigned skill behaviours.\n");
        }
    }
}
