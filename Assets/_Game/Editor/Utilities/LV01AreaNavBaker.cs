using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ZZ.Editor
{
    /// <summary>
    /// Bakes the NavMeshSurface of an open Base scene into a per-Area NavMeshData asset.
    /// The scene must be open and contain a root named <paramref name="navigationRootName"/>
    /// with a NavMeshSurface configured (volume, layer mask, agent type).
    /// </summary>
    public static class LV01AreaNavBaker
    {
        public static string BakeAreaNavMesh(string navigationRootName, string navDataAssetPath)
        {
            GameObject root = GameObject.Find(navigationRootName);
            if (root == null)
            {
                return $"FAIL: root '{navigationRootName}' not found in open scene";
            }

            NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                return $"FAIL: no NavMeshSurface on '{navigationRootName}'";
            }

            string directory = Path.GetDirectoryName(navDataAssetPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            NavMeshData data = AssetDatabase.LoadAssetAtPath<NavMeshData>(navDataAssetPath);
            if (data == null)
            {
                data = new NavMeshData(surface.agentTypeID);
                AssetDatabase.CreateAsset(data, navDataAssetPath);
            }

            surface.navMeshData = data;
            surface.BuildNavMesh();
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
            return $"OK: baked {surface.navMeshData.name} into {navDataAssetPath}";
        }
    }
}
