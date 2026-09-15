using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Builds all enabled streaming scenes into a distributable Windows player.</summary>
    public static class WindowsPlayerBuild
    {
        private const string k_ReportPath = ".utmp/windows-build.txt";

        /// <summary>Builds immediately, including when the editor is running without an active Inspector.</summary>
        [MenuItem("Tools/ZZ/Build/Windows Player")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                throw new InvalidOperationException("Stop Play Mode and finish the current build first.");
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(k_ReportPath, "RUNNING " + DateTime.Now.ToString("O"));
            Build();
        }

        private static void Build()
        {
            try
            {
                string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                    .Select(scene => scene.path).ToArray();
                if (scenes.Length == 0 || scenes.Any(scene => !File.Exists(scene)))
                {
                    throw new InvalidOperationException("Enabled build scenes are missing.");
                }
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Releases/Elden-Windows");
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = "Releases/Elden-Windows/Elden.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                BuildSummary summary = report.summary;
                File.WriteAllText(k_ReportPath,
                    $"{summary.result}\nScenes: {scenes.Length}\nErrors: {summary.totalErrors}\n" +
                    $"Warnings: {summary.totalWarnings}\nBytes: {summary.totalSize}\n" +
                    $"Duration: {summary.totalTime}\nPath: {summary.outputPath}\n");
            }
            catch (Exception exception)
            {
                File.WriteAllText(k_ReportPath, "FAILED\n" + exception);
                Debug.LogException(exception);
            }
        }

        /// <summary>Blocks commercial candidate generation while included media lacks provenance evidence.</summary>
        [MenuItem("Tools/ZZ/Build/Commercial Candidate (Rights Check)")]
        public static void StartCommercialCandidate()
        {
            if (!ReleaseReadinessAudit.ValidateAssetRights())
            {
                throw new InvalidOperationException(
                    "Commercial candidate blocked: review Docs/Release/IncludedAssetRights.csv and AssetRights.json.");
            }
            Start();
        }
    }
}
