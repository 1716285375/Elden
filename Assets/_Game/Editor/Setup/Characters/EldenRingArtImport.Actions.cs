using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        private const string k_ActionOutput = "Assets/_Game/Art/Characters/EldenRing/PlayerActions";

        /// <summary>Preserves original hierarchy and motion curves in a Generic source library for retargeting.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Import Original Player Actions")]
        public static void ImportOriginalPlayerActions()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before importing actions.");
            }
            string source = "F:/MyProject/RE/EldenRing/Converted/PlayerActions/Animations/c0000_a3x/";
            string[] names = { "a030_030000", "a030_030010", "a030_030020" };
            foreach (string name in names)
            {
                string metadata = File.ReadAllText(source + name + ".json");
                if (!metadata.Contains("verified_source_pose") || !File.Exists(source + name + ".fbx"))
                {
                    throw new InvalidDataException("Original pose export is incomplete: " + name);
                }
            }
            Directory.CreateDirectory(k_ActionOutput + "/Source");
            Directory.CreateDirectory(k_ActionOutput + "/Clips");
            string basePath = k_ActionOutput + "/Source/c0000.fbx";
            File.Copy("F:/MyProject/RE/EldenRing/Converted/c0000-OriginalNames/c0000.fbx", basePath, true);
            AssetDatabase.ImportAsset(basePath, ImportAssetOptions.ForceSynchronousImport);
            var baseImporter = (ModelImporter)AssetImporter.GetAtPath(basePath);
            baseImporter.animationType = ModelImporterAnimationType.Generic;
            baseImporter.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            baseImporter.sourceAvatar = null;
            baseImporter.importAnimation = false;
            baseImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            baseImporter.SaveAndReimport();
            var report = new StringBuilder();
            foreach (string name in names)
            {
                string path = k_ActionOutput + "/Source/" + name + ".fbx";
                File.Copy(source + name + ".fbx", path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.sourceAvatar = null;
                importer.importAnimation = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips.Length != 1)
                {
                    throw new InvalidDataException("Expected one original motion per FBX: " + name);
                }
                clips[0].name = name;
                clips[0].loopTime = false;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Single(item => !item.name.StartsWith("__preview__"));
                if (clip.length <= 0f || AnimationUtility.GetCurveBindings(clip).Length == 0)
                {
                    throw new InvalidDataException("Missing original motion curves: " + name);
                }
                string clipPath = k_ActionOutput + "/Clips/" + name + ".anim";
                AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (saved == null)
                {
                    saved = UnityEngine.Object.Instantiate(clip);
                    saved.name = name;
                    AssetDatabase.CreateAsset(saved, clipPath);
                }
                else
                {
                    EditorUtility.CopySerialized(clip, saved);
                    saved.name = name;
                    EditorUtility.SetDirty(saved);
                }
                report.AppendLine($"PASS {name}; Generic=True; seconds={clip.length:F4}; source=c0000_a3x.anibnd.dcx");
            }
            AssetDatabase.SaveAssets();
            report.AppendLine("Original hierarchy preserved. Player Humanoid retargeting and original TAE hit-window binding remain pending.");
            File.WriteAllText("Docs/ModularPlayer/original-actions-import.txt", report.ToString());
        }
    }
}
