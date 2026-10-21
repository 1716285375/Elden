using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Deletes legacy equipment resources only after checking every remaining asset dependency.</summary>
    public static class RetiredEquipmentCleanup
    {
        [MenuItem("Tools/ZZ/Clean Unreferenced Legacy Equipment")]
        public static void Apply()
        {
            if (Application.isPlaying) { throw new InvalidOperationException("Stop Play Mode first."); }
            ReplaceLegacyMaterialReferences();
            string[] roots = { "Assets/_Game/Art/Equipment/Weapons/", "Assets/_Game/Art/Equipment/Shields/",
                "Assets/_Game/Art/Equipment/Accessories/",
                "Assets/_Game/Art/Shared/Models/Rigged/Props/Interactable_Chest_01/",
                "Assets/_Game/Art/Shared/Models/Rigged/Props/Mimic_Chest_01/" };
            DeleteUnreferenced(roots, "Docs/CollisionAssetRefresh/legacy-equipment-deletion.txt");
        }

        [MenuItem("Tools/ZZ/Elden Ring/Clean Unreferenced Stylized Player Meshes")]
        public static void CleanStylizedPlayerMeshes()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            DeleteUnreferenced(new[] { "Assets/_Game/Art/Characters/Player/Optimized/" },
                "Docs/ModularPlayer/legacy-player-deletion.txt");
        }

        private static void DeleteUnreferenced(string[] roots, string reportPath)
        {
            string[] all = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") &&
                !AssetDatabase.IsValidFolder(path)).ToArray();
            string[] candidates = all.Where(path => roots.Any(root => path.StartsWith(root, StringComparison.Ordinal))).ToArray();
            var candidateSet = new HashSet<string>(candidates);
            string[] consumers = all.Where(path => !candidateSet.Contains(path)).ToArray();
            var used = new HashSet<string>(AssetDatabase.GetDependencies(consumers, true));
            var report = new StringBuilder();
            int deleted = 0;
            long bytes = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string candidate in candidates)
                {
                    if (used.Contains(candidate))
                    {
                        report.AppendLine("KEPT_REFERENCED " + candidate);
                        continue;
                    }
                    long size = new FileInfo(candidate).Length;
                    if (!AssetDatabase.DeleteAsset(candidate))
                    {
                        throw new IOException("Cannot remove " + candidate);
                    }
                    deleted++;
                    bytes += size;
                    report.AppendLine("DELETED " + candidate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            report.AppendLine($"Deleted={deleted}; Bytes={bytes}; KeptReferenced={candidates.Length - deleted}");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report.ToString());
        }

        private static void ReplaceLegacyMaterialReferences()
        {
            const string legacy = "Assets/_Game/Art/Equipment/Weapons/Materials/";
            const string imported = "Assets/_Game/Art/Equipment/EldenRing/EquipmentRefresh/";
            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => AssetDatabase.GetDependencies(path, true)
                    .Any(dependency => dependency.StartsWith(legacy, StringComparison.Ordinal))).ToArray();
            foreach (string path in prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] materials = renderer.sharedMaterials;
                        for (int index = 0; index < materials.Length; index++)
                        {
                            string source = AssetDatabase.GetAssetPath(materials[index]);
                            if (!source.StartsWith(legacy, StringComparison.Ordinal)) { continue; }
                            string model = source.Contains("/Shield/") ? "WP_A_2020" :
                                source.Contains("/Bow/") ? "WP_A_1410" : "WP_A_1330";
                            Material replacement = AssetDatabase.LoadAssetAtPath<Material>(imported + model + ".mat");
                            if (replacement == null) { throw new InvalidOperationException("Missing imported material " + model); }
                            materials[index] = replacement;
                        }
                        renderer.sharedMaterials = materials;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
