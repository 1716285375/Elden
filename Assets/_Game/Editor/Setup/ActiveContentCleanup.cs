using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Audits the playable scenes and runtime resources before pruning retired art.</summary>
    public static class ActiveContentCleanup
    {
        private const string k_Output = ".utmp/asset-cleanup";

        [MenuItem("Tools/ZZ/Cleanup/Prune Retired Network Characters")]
        public static void PruneNetworkCharacters()
        {
            if (Application.isPlaying) { throw new InvalidOperationException("Stop Play Mode first."); }
            Directory.CreateDirectory(k_Output);
            var removed = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:NetworkPrefabsList", new[] { "Assets/_Game" }))
            {
                var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (NetworkPrefab entry in list.PrefabList.ToArray())
                {
                    string path = AssetDatabase.GetAssetPath(entry.Prefab);
                    bool legacyAI = entry.Prefab != null && entry.Prefab.GetComponent<AICharacterManager>() != null &&
                        !path.StartsWith(OriginalCreatureGameplaySetup.Output + "/", StringComparison.Ordinal);
                    if (entry.Prefab == null || legacyAI)
                    {
                        list.Remove(entry);
                        removed.Add(path);
                    }
                }
                EditorUtility.SetDirty(list);
                AssetDatabase.SaveAssetIfDirty(list);
            }
            File.WriteAllLines(k_Output + "/removed-network-prefabs.txt", removed);
            Audit();
        }

        [MenuItem("Tools/ZZ/Cleanup/Audit Active Content")]
        public static void Audit()
        {
            if (Application.isPlaying) { throw new InvalidOperationException("Stop Play Mode first."); }
            Directory.CreateDirectory(k_Output);
            string[] all = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") &&
                !AssetDatabase.IsValidFolder(path)).ToArray();
            var literals = new HashSet<string>();
            foreach (string script in Directory.GetFiles("Assets/_Game/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(script), "\"([^\"\\r\\n]+)\""))
                {
                    literals.Add(match.Groups[1].Value);
                }
            }
            var dynamicNames = new HashSet<string>(literals);
            foreach (string prefix in new[] { "MENU_Tab_", "KG_", "KG_PS5_", "KG_PS4_", "KG_XboxSeries_" })
            {
                foreach (string literal in literals) { dynamicNames.Add(prefix + literal); }
            }
            var roots = new HashSet<string>(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path));
            foreach (string path in all.Where(path => path.Contains("/Resources/")))
            {
                bool atlas = path.Contains("/UI/EldenRing/Elements/") || path.Contains("/UI/EldenRing/Loading/");
                if (!atlas || dynamicNames.Contains(Path.GetFileNameWithoutExtension(path))) { roots.Add(path); }
            }
            foreach (string path in new[] { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" })
            {
                roots.Add(path);
            }
            foreach (UnityEngine.Object pipeline in new UnityEngine.Object[]
            {
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline,
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline,
                QualitySettings.renderPipeline
            })
            {
                if (pipeline != null) { roots.Add(AssetDatabase.GetAssetPath(pipeline)); }
            }
            foreach (UnityEngine.Object asset in PlayerSettings.GetPreloadedAssets())
            {
                if (asset != null) { roots.Add(AssetDatabase.GetAssetPath(asset)); }
            }
            string[] used = AssetDatabase.GetDependencies(roots.ToArray(), true).Where(path => path.StartsWith("Assets/"))
                .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var usedSet = new HashSet<string>(used);
            var sourceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".shader", ".hlsl", ".cginc", ".compute", ".shadergraph", ".shadersubgraph"
            };
            // Unity does not report every text include or license as an asset dependency.
            string[] retired = all.Where(path => !sourceExtensions.Contains(Path.GetExtension(path)) &&
                !Regex.IsMatch(Path.GetFileName(path), "license|copyright|notice", RegexOptions.IgnoreCase) && path.StartsWith("Assets/_Game/Art/", StringComparison.Ordinal) &&
                !path.Contains("/EldenRing/") && !usedSet.Contains(path)).ToArray();
            File.WriteAllLines(k_Output + "/roots.txt", roots.OrderBy(path => path));
            File.WriteAllLines(k_Output + "/used-assets.txt", used);
            File.WriteAllLines(k_Output + "/retired-art-candidates.txt", retired);
            long bytes = used.Where(File.Exists).Sum(path => new FileInfo(path).Length);
            File.WriteAllText(k_Output + "/summary.txt", $"Roots={roots.Count}\nUsed={used.Length}\nBytes={bytes}\nRetiredCandidates={retired.Length}\n");
            Debug.Log("Active content audit saved to " + k_Output);
        }

        [MenuItem("Tools/ZZ/Cleanup/Delete Unreferenced Retired Art")]
        public static void DeleteRetiredArt()
        {
            Audit();
            string[] candidates = File.ReadAllLines(k_Output + "/retired-art-candidates.txt");
            var candidateSet = new HashSet<string>(candidates);
            string[] consumers = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") &&
                !AssetDatabase.IsValidFolder(path) && !candidateSet.Contains(path)).ToArray();
            var external = new HashSet<string>(AssetDatabase.GetDependencies(consumers, true));
            var deleted = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in candidates.Where(path => !external.Contains(path)))
                {
                    string backup = k_Output + "/backup/" + path;
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.Copy(path, backup, true);
                    if (File.Exists(path + ".meta")) { File.Copy(path + ".meta", backup + ".meta", true); }
                    if (!AssetDatabase.DeleteAsset(path)) { throw new IOException("Cannot delete " + path); }
                    deleted.Add(path);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            File.WriteAllLines(k_Output + "/deleted-retired-art.txt", deleted);
            Audit();
        }
    }
}
