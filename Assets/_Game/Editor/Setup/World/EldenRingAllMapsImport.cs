using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Consumes prepared maps by original ID without loading the whole world at once.</summary>
    [InitializeOnLoad]
    public static class EldenRingAllMapsImport
    {
        private const string k_Root = "F:/MyProject/RE/EldenRing/Converted/MapWorld";
        private const string k_Enabled = "ZZ.EldenRing.AllMapsImport";
        private const int k_Version = 3;
        private static double s_NextCheck;

        static EldenRingAllMapsImport()
        {
            s_NextCheck = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += ImportNext;
        }

        /// <summary>Starts or resumes importing prepared maps, preserving existing gameplay scenes.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Import All Prepared Maps")]
        public static void Start()
        {
            SessionState.SetBool(k_Enabled, true);
            s_NextCheck = 0;
            Debug.Log("ALL_MAP_IMPORT_STARTED " + k_Root);
        }

        /// <summary>Stops before the next map, leaving completed imports intact.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Stop All Maps Import")]
        public static void Stop()
        {
            SessionState.SetBool(k_Enabled, false);
        }

        private static void ImportNext()
        {
            if (!SessionState.GetBool(k_Enabled, false) || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < s_NextCheck)
            {
                return;
            }
            s_NextCheck = EditorApplication.timeSinceStartup + 10;
            string maps = k_Root + "/Maps";
            if (!Directory.Exists(maps))
            {
                return;
            }
            string source = Directory.GetFiles(maps, "world.json", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault(path => !HasCurrentResult(Path.GetDirectoryName(path) + "/unity-result.json"));
            if (source == null)
            {
                WriteProgress(maps);
                return;
            }
            if (new DriveInfo(Application.dataPath.Substring(0, 3)).AvailableFreeSpace < 12L * 1024 * 1024 * 1024)
            {
                Stop();
                File.WriteAllText(k_Root + "/unity-blocker.txt", "Less than 12 GiB free; resume after freeing disk space.");
                return;
            }
            string mapId = new DirectoryInfo(Path.GetDirectoryName(source)).Name;
            var result = new ImportResult { Map = mapId, Status = "failed", Version = k_Version };
            try
            {
                PreparedMap prepared = JsonUtility.FromJson<PreparedMap>(File.ReadAllText(source));
                if (prepared.Status == "failed")
                {
                    throw new InvalidDataException("Source preparation failed; see " + source);
                }
                EldenRingMapSceneImport.ImportMap(mapId);
                result.Status = prepared.Status == "ready" ? "verified_static_geometry" : "partial";
                result.SourceStatus = prepared.Status;
                result.Scene = "Assets/_Game/Scenes/Maps/" + mapId.Substring(0, 3) + "/SCN_" + mapId + ".unity";
                result.Scope = "Saved static geometry, source vertices/topology/UV and local placements; source shaders, " +
                    "Havok collision, events, lighting and streaming are not restored.";
            }
            catch (Exception error)
            {
                result.Error = error.ToString();
                if (error is IOException && error.Message.StartsWith("Less than 12 GiB", StringComparison.Ordinal))
                {
                    result.Status = "blocked_disk";
                    Stop();
                }
                Debug.LogException(error);
            }
            File.WriteAllText(Path.GetDirectoryName(source) + "/unity-result.json", JsonUtility.ToJson(result, true));
            WriteProgress(maps);
            Debug.Log("ALL_MAP_IMPORT " + mapId + " " + result.Status);
            GC.Collect();
            EditorUtility.UnloadUnusedAssetsImmediate();
            s_NextCheck = EditorApplication.timeSinceStartup + 10;
        }

        private static bool HasCurrentResult(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }
            ImportResult result = JsonUtility.FromJson<ImportResult>(File.ReadAllText(path));
            return result != null && result.Version == k_Version && result.Status != "blocked_disk";
        }

        private static void WriteProgress(string maps)
        {
            ImportResult[] results = Directory.GetFiles(maps, "unity-result.json", SearchOption.AllDirectories)
                .Select(path => JsonUtility.FromJson<ImportResult>(File.ReadAllText(path)))
                .Where(result => result.Version == k_Version).ToArray();
            var progress = new ImportProgress
            {
                Expected = 935,
                Verified = results.Count(item => item.Status == "verified_static_geometry"),
                Partial = results.Count(item => item.Status == "partial"),
                Failed = results.Count(item => item.Status == "failed"),
                Status = results.Length == 935 ? "finished" : "running"
            };
            File.WriteAllText(k_Root + "/unity-progress.json", JsonUtility.ToJson(progress, true));
            if (results.Length == 935)
            {
                Stop();
            }
        }

        [Serializable]
        private sealed class ImportProgress
        {
            public int Expected;
            public int Verified;
            public int Partial;
            public int Failed;
            public string Status;
        }

        [Serializable]
        private sealed class PreparedMap
        {
            public string Status;
        }

        [Serializable]
        private sealed class ImportResult
        {
            public int Version;
            public string Map;
            public string Status;
            public string SourceStatus;
            public string Scene;
            public string Scope;
            public string Error;
        }
    }
}
