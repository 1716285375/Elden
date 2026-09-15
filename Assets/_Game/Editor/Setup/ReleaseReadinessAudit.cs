using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Produces a dependency-based art rights inventory without assuming recovered assets are licensed.</summary>
    public static class ReleaseReadinessAudit
    {
        private const string k_LedgerPath = "Docs/Release/AssetRights.json";
        private static readonly HashSet<string> s_mediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".fbx", ".obj", ".blend", ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".hdr",
            ".wav", ".ogg", ".mp3", ".aiff", ".anim", ".ttf", ".otf", ".mp4", ".shader", ".hlsl",
            ".shadergraph", ".shadersubgraph"
        };

        /// <summary>Writes an exact asset inventory for enabled scenes plus Resources content.</summary>
        [MenuItem("Tools/ZZ/Release/Audit Asset Rights")]
        public static void Run()
        {
            ValidateAssetRights();
        }

        /// <summary>Returns whether every included media dependency has a recorded rights status and evidence.</summary>
        public static bool ValidateAssetRights()
        {
            Directory.CreateDirectory("Docs/Release");
            Directory.CreateDirectory(".utmp");
            if (!File.Exists(k_LedgerPath))
            {
                File.WriteAllText(k_LedgerPath, JsonUtility.ToJson(new RightsLedger(), true));
            }
            RightsLedger ledger = JsonUtility.FromJson<RightsLedger>(File.ReadAllText(k_LedgerPath));
            Dictionary<string, RightsEntry> entries = new(StringComparer.Ordinal);
            foreach (RightsEntry entry in ledger?.Entries ?? Array.Empty<RightsEntry>())
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Guid))
                {
                    if (!entries.TryAdd(entry.Guid, entry))
                    {
                        throw new InvalidOperationException("Duplicate asset GUID in rights ledger: " + entry.Guid);
                    }
                }
            }
            var roots = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToList();
            roots.AddRange(AssetDatabase.GetAllAssetPaths().Where(path =>
                path.StartsWith("Assets/", StringComparison.Ordinal) && path.Contains("/Resources/") &&
                !AssetDatabase.IsValidFolder(path)));
            string[] media = AssetDatabase.GetDependencies(roots.ToArray(), true)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                    s_mediaExtensions.Contains(Path.GetExtension(path)))
                .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            StringBuilder csv = new("Guid,Path,Status,Evidence\n");
            int missing = 0;
            foreach (string path in media)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                entries.TryGetValue(guid, out RightsEntry entry);
                bool verified = HasRightsEvidence(entry?.Status, entry?.Evidence);
                if (!verified)
                {
                    missing++;
                }
                csv.AppendLine(string.Join(",", Escape(guid), Escape(path),
                    Escape(verified ? entry.Status : "Unverified"), Escape(entry?.Evidence ?? string.Empty)));
            }
            File.WriteAllText("Docs/Release/IncludedAssetRights.csv", csv.ToString(), new UTF8Encoding(true));
            string summary = $"Media dependencies: {media.Length}\nUnverified: {missing}\n" +
                "Scope: enabled scenes and Assets/Resources dependencies.\n" +
                "Plugins, package licenses, external bundles and Steam dashboard checks require separate review.\n";
            File.WriteAllText(".utmp/release-readiness.txt", summary);
            return media.Length > 0 && missing == 0;
        }

        /// <summary>Requires both an explicit classification and a nonempty evidence reference.</summary>
        public static bool HasRightsEvidence(string status, string evidence)
        {
            return (status == "Original" || status == "Licensed") && !string.IsNullOrWhiteSpace(evidence);
        }

        private static string Escape(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        [Serializable]
        private sealed class RightsLedger
        {
            public RightsEntry[] Entries = Array.Empty<RightsEntry>();
        }

        [Serializable]
        private sealed class RightsEntry
        {
            public string Guid;
            public string Status;
            public string Evidence;
        }
    }
}
