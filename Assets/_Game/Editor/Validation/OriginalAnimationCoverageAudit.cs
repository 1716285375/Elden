using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Records effective controller clips without counting a converted file as an installed action.</summary>
    public static class OriginalAnimationCoverageAudit
    {
        [MenuItem("Tools/ZZ/Elden Ring/Audit Original Animation Coverage")]
        public static void Audit()
        {
            string[] controllers = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets/_Game" })
                .Concat(AssetDatabase.FindAssets("t:AnimatorOverrideController", new[] { "Assets/_Game" }))
                .Distinct().Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            var records = controllers.SelectMany(path =>
            {
                RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                return controller.animationClips.Distinct().Select(clip =>
                {
                    string asset = AssetDatabase.GetAssetPath(clip);
                    Match source = Regex.Match(clip.name, @"a\d{3}_\d{6}");
                    bool mixed = source.Value == "a000_000100" && clip.name.Contains("guard_") &&
                        !clip.name.Contains("guard_idle");
                    return new Record
                    {
                        Controller = path,
                        Clip = clip.name,
                        Asset = asset,
                        SourceId = source.Success ? source.Value : string.Empty,
                        Classification = mixed ? "mixed_original_upper_body_legacy_footwork" :
                            source.Success ? "original_named_requires_source_verification" : "legacy_or_unmapped",
                        Humanoid = clip.humanMotion,
                        Duration = clip.length,
                        EquipmentCurves = AnimationUtility.GetCurveBindings(clip).Count(binding =>
                            binding.path.EndsWith("ER_L_Weapon") || binding.path.EndsWith("ER_R_Weapon") ||
                            binding.path.EndsWith("ER_L_Shield") || binding.path.EndsWith("ER_R_Shield"))
                    };
                });
            }).ToArray();
            Directory.CreateDirectory("Docs/OriginalMigration");
            File.WriteAllText("Docs/OriginalMigration/animation-coverage.json", JsonUtility.ToJson(new Report
            {
                Controllers = controllers.Length,
                Bindings = records.Length,
                LegacyBindings = records.Count(record => record.Classification == "legacy_or_unmapped"),
                MixedBindings = records.Count(record => record.Classification.StartsWith("mixed_")),
                Records = records
            }, true));
        }

        [Serializable]
        private sealed class Report
        {
            public int Controllers;
            public int Bindings;
            public int LegacyBindings;
            public int MixedBindings;
            public Record[] Records;
        }

        [Serializable]
        private sealed class Record
        {
            public string Controller;
            public string Clip;
            public string Asset;
            public string SourceId;
            public string Classification;
            public bool Humanoid;
            public float Duration;
            public int EquipmentCurves;
        }
    }
}
