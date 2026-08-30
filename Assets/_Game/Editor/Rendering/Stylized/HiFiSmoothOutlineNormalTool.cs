using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor.Rendering.Stylized
{
    /// <summary>
    /// Generates smooth outline normals for inverted-hull outlines.
    ///
    /// Hard-edged meshes (cube, armor, low-poly props) split the outline shell
    /// when it is extruded with the raw per-vertex normal. This tool averages
    /// the normals of all vertices that share a position and stores the result
    /// in UV3 (TEXCOORD3), which HiFiOutline.hlsl reads when present. The
    /// original normals are untouched, so toon lighting is unaffected.
    ///
    /// Usage: select one or more GameObjects (MeshFilter / SkinnedMeshRenderer
    /// or model assets) in the Hierarchy/Project and run
    /// Tools &gt; Stylized &gt; Generate Smooth Outline Normals (UV3).
    /// Imported model assets are written back through their ModelImporter
    /// (the tool re-imports the model once per selection).
    /// </summary>
    public static class HiFiSmoothOutlineNormalTool
    {
        private const string k_MenuPath = "Tools/Stylized/Generate Smooth Outline Normals (UV3)";

        [MenuItem(k_MenuPath)]
        public static void GenerateSmoothOutlineNormals()
        {
            int processed = 0;
            int skipped = 0;

            foreach (Object obj in Selection.objects)
            {
                GameObject go = obj as GameObject;
                if (go != null)
                {
                    MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(true);
                    foreach (MeshFilter f in filters)
                    {
                        if (f.sharedMesh != null && WriteSmoothNormalsToUv3(f.sharedMesh))
                        {
                            processed++;
                        }
                    }

                    SkinnedMeshRenderer[] skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    foreach (SkinnedMeshRenderer s in skinned)
                    {
                        if (s.sharedMesh != null && WriteSmoothNormalsToUv3(s.sharedMesh))
                        {
                            processed++;
                        }
                    }
                    continue;
                }

                if (obj is Mesh mesh)
                {
                    if (WriteSmoothNormalsToUv3(mesh))
                    {
                        processed++;
                    }
                    continue;
                }

                if (obj is GameObject modelGo)
                {
                    string assetPath = AssetDatabase.GetAssetPath(modelGo);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        skipped++;
                    }
                }
            }

            // Handle selected model assets (e.g. .fbx / .obj) through the importer.
            foreach (Object obj in Selection.objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
                {
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                // Re-import then write UV3 on the imported meshes.
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                bool changed = false;
                foreach (Object sub in subAssets)
                {
                    if (sub is Mesh m && WriteSmoothNormalsToUv3(m))
                    {
                        changed = true;
                        processed++;
                    }
                }

                if (changed)
                {
                    AssetDatabase.SaveAssets();
                }
            }

            EditorUtility.DisplayDialog(
                "HiFi Smooth Outline Normals",
                $"Processed {processed} mesh(es).\n" +
                $"Skipped {skipped} object(s) without importable meshes.\n\n" +
                "UV3 now contains averaged normals; HiFiOutline.hlsl uses them for extrusion.",
                "OK");
        }

        /// <summary>
        /// Averages normals of vertices sharing the same position and stores the
        /// result in uv3 (TEXCOORD3). Returns true when the mesh was modified.
        /// </summary>
        private static bool WriteSmoothNormalsToUv3(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0)
            {
                return false;
            }

            Vector3[] positions = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length != positions.Length)
            {
                return false;
            }

            // Average per shared position (snap to a small grid to merge
            // near-duplicate vertices from import).
            var accumulator = new Dictionary<Vector3Int, Vector3>();
            var counter = new Dictionary<Vector3Int, int>();
            const float snap = 0.0001f;

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(positions[i].x / snap),
                    Mathf.RoundToInt(positions[i].y / snap),
                    Mathf.RoundToInt(positions[i].z / snap));

                if (accumulator.TryGetValue(key, out Vector3 sum))
                {
                    accumulator[key] = sum + normals[i];
                    counter[key] = counter[key] + 1;
                }
                else
                {
                    accumulator[key] = normals[i];
                    counter[key] = 1;
                }
            }

            var smooth = new Vector3[positions.Length];
            var tmpUv3 = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(positions[i].x / snap),
                    Mathf.RoundToInt(positions[i].y / snap),
                    Mathf.RoundToInt(positions[i].z / snap));

                Vector3 avg = accumulator[key] / counter[key];
                smooth[i] = avg.normalized;
                tmpUv3[i] = smooth[i];
            }

            mesh.SetUVs(3, new List<Vector3>(tmpUv3));
            mesh.UploadMeshData(false);
            return true;
        }
    }
}
