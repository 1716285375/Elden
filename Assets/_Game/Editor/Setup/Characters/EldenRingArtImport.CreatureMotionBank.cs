using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        private const string k_CreatureMotionReport = "Docs/OriginalSingleMap/animation-import.json";
        private static Queue<string> s_creatureMotionQueue;
        private static JObject s_creatureMotionReport;
        private static GameObject s_creatureMotionRoot;
        private static Transform[] s_creatureMotionBones;
        private static string s_creatureMotionActor;

        [MenuItem("Tools/ZZ/Elden Ring/Import Verified Creature Motion Banks")]
        public static void ImportVerifiedCreatureMotionBanks()
        {
            if (Application.isPlaying || s_creatureMotionQueue != null)
            {
                throw new InvalidOperationException("Stop Play Mode or wait for the current motion import.");
            }
            const string source = "F:/MyProject/RE/EldenRing/Converted/SingleMapCharacters/Actions";
            var files = new List<string>();
            foreach (string actor in new[] { "c3970", "c4070", "c4071" })
            {
                JObject verification = JObject.Parse(File.ReadAllText(source + "/" + actor + "/verification.json"));
                if (verification.Value<string>("Status") != "verified_source_pose_and_hashes" ||
                    verification.Value<int>("Failures") != 0)
                {
                    throw new InvalidDataException("Source motion verification failed: " + actor);
                }
                string[] actorFiles = Directory.GetFiles(source + "/" + actor + "/Animations", "*.matrixbin",
                    SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                if (actorFiles.Length != verification.Value<int>("Verified") ||
                    actorFiles.Select(Path.GetFileName).Distinct().Count() != actorFiles.Length)
                {
                    throw new InvalidDataException("Motion inventory is incomplete or has ambiguous IDs: " + actor);
                }
                files.AddRange(actorFiles);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(k_CreatureMotionReport));
            s_creatureMotionReport = File.Exists(k_CreatureMotionReport)
                ? JObject.Parse(File.ReadAllText(k_CreatureMotionReport))
                : new JObject { ["Imported"] = new JObject() };
            s_creatureMotionReport["Status"] = "running";
            s_creatureMotionReport["Expected"] = files.Count;
            s_creatureMotionReport["Error"] = null;
            s_creatureMotionQueue = new Queue<string>(files);
            WriteCreatureMotionProgress();
            AssemblyReloadEvents.beforeAssemblyReload += PauseCreatureMotionImport;
            EditorApplication.update += ImportNextCreatureMotion;
        }

        private static void ImportNextCreatureMotion()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }
            if (Application.isPlaying)
            {
                PauseCreatureMotionImport();
                return;
            }
            try
            {
                if (s_creatureMotionQueue.Count == 0)
                {
                    s_creatureMotionReport["Status"] = "completed";
                    StopCreatureMotionImport();
                    return;
                }
                string source = s_creatureMotionQueue.Dequeue();
                string actor = new[] { "c3970", "c4070", "c4071" }.Single(value =>
                    source.Replace('\\', '/').Contains("/" + value + "/"));
                string name = Path.GetFileNameWithoutExtension(source);
                string output = "Assets/_Game/Art/Characters/EldenRing/OriginalCreatures/" + actor;
                string asset = output + "/Animations/" + name + ".anim";
                string key = actor + "/" + name;
                JObject imported = (JObject)s_creatureMotionReport["Imported"];
                JToken previous = imported[key];
                if (previous?.Value<long>("SourceTime") == File.GetLastWriteTimeUtc(source).Ticks &&
                    File.Exists(asset) && previous.Value<long>("AssetTime") == File.GetLastWriteTimeUtc(asset).Ticks)
                {
                    return;
                }
                if (s_creatureMotionActor != actor)
                {
                    ReleaseCreatureMotionRoot();
                    s_creatureMotionRoot = PrefabUtility.LoadPrefabContents(output + "/" + actor + ".prefab");
                    s_creatureMotionBones = s_creatureMotionRoot.GetComponentInChildren<SkinnedMeshRenderer>(true).bones;
                    s_creatureMotionActor = actor;
                }
                AnimationClip clip = ImportOriginalCreatureMotion(s_creatureMotionRoot, s_creatureMotionBones,
                    source, asset, out float maximumError);
                if (name is "a000_000000" or "a000_002000" or "a000_002001" or
                    "a000_002002" or "a000_002003" or "a000_002100")
                {
                    AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                }
                AssetDatabase.SaveAssetIfDirty(clip);
                imported[key] = new JObject
                {
                    ["SourceTime"] = File.GetLastWriteTimeUtc(source).Ticks,
                    ["AssetTime"] = File.GetLastWriteTimeUtc(asset).Ticks,
                    ["MaximumMatrixError"] = maximumError,
                    ["Asset"] = asset
                };
                s_creatureMotionReport["Last"] = key;
                WriteCreatureMotionProgress();
                if (imported.Count % 8 == 0)
                {
                    EditorUtility.UnloadUnusedAssetsImmediate();
                    GC.Collect();
                }
            }
            catch (Exception exception)
            {
                s_creatureMotionReport["Status"] = "failed";
                s_creatureMotionReport["Error"] = exception.ToString();
                StopCreatureMotionImport();
                Debug.LogException(exception);
            }
        }

        private static void PauseCreatureMotionImport()
        {
            s_creatureMotionReport["Status"] = "paused";
            StopCreatureMotionImport();
        }

        private static void StopCreatureMotionImport()
        {
            EditorApplication.update -= ImportNextCreatureMotion;
            AssemblyReloadEvents.beforeAssemblyReload -= PauseCreatureMotionImport;
            WriteCreatureMotionProgress();
            ReleaseCreatureMotionRoot();
            s_creatureMotionQueue = null;
        }

        private static void ReleaseCreatureMotionRoot()
        {
            if (s_creatureMotionRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(s_creatureMotionRoot);
            }
            s_creatureMotionRoot = null;
            s_creatureMotionActor = null;
            s_creatureMotionBones = null;
        }

        private static void WriteCreatureMotionProgress()
        {
            s_creatureMotionReport["VerifiedNativeClips"] = ((JObject)s_creatureMotionReport["Imported"]).Count;
            File.WriteAllText(k_CreatureMotionReport, s_creatureMotionReport.ToString(Formatting.Indented));
        }
    }
}
