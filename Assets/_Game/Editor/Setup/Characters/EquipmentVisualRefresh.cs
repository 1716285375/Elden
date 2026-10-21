using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Imports extracted rigid equipment without replacing gameplay components or prefab identities.</summary>
    public static class EquipmentVisualRefresh
    {
        private const string k_Source = "F:/MyProject/RE/EldenRing/Converted/EquipmentRefresh";
        private const string k_Output = "Assets/_Game/Art/Equipment/EldenRing/EquipmentRefresh";

        /// <summary>Restores in-hand rigid equipment to the same raw axes as the extracted character skeleton.</summary>
        public static void RestoreOriginalRigidEquipmentAxes()
        {
            var document = JsonUtility.FromJson<Document>(File.ReadAllText(k_Source + "/models.json"));
            foreach (string name in new[] { "WP_A_2020", "WP_A_1410", "WP_A_1330" })
            {
                BuildMesh(document.Models.Single(model => model.Name == name).Meshes, name, false);
            }
        }

        [MenuItem("Tools/ZZ/Import Extracted Equipment Refresh")]
        public static void Apply()
        {
            if (Application.isPlaying) { throw new InvalidOperationException("Stop Play Mode first."); }
            Directory.CreateDirectory(k_Output);
            foreach (string file in Directory.GetFiles(k_Source, "*.png"))
            {
                File.Copy(file, k_Output + "/" + Path.GetFileName(file), true);
            }
            AssetDatabase.Refresh();
            Document document = JsonUtility.FromJson<Document>(File.ReadAllText(k_Source + "/models.json"));
            var report = new StringBuilder();
            Replace(document, "WP_A_2020", "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Medium Shield.prefab", 0.69f, report);
            Replace(document, "WP_A_1410", "Assets/_Game/Prefabs/Equipment/Weapons/Longbow.prefab", 1.6f, report);
            Replace(document, "WP_A_1330", "Assets/_Game/Prefabs/Abilities/Incantation Catalyst.prefab", 0.16f, report);
            Replace(document, "WP_A_7030", "Assets/_Game/Prefabs/Projectiles/Draw Arrow.prefab", 0.8f, report);
            Replace(document, "WP_A_7030", "Assets/_Game/Prefabs/Projectiles/Released Arrow.prefab", 0.8f, report);
            foreach (string name in new[] { "Broadsword", "Straight Sword" })
            {
                string path = "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    UnpackVisualInstances(root);
                    foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true).Where(item => !item.enabled).ToArray())
                    {
                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        UnityEngine.Object.DestroyImmediate(renderer);
                        if (filter != null) { UnityEngine.Object.DestroyImmediate(filter); }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            RefreshVfxFixtures();
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/CollisionAssetRefresh/equipment-import.txt", report.ToString());
        }

        private static void Replace(Document document, string modelName, string path, float length, StringBuilder report)
        {
            Model model = document.Models.Single(item => item.Name == modelName);
            bool arrow = modelName == "WP_A_7030";
            Mesh mesh = BuildMesh(model.Meshes.Take(arrow ? 2 : model.Meshes.Length).ToArray(), modelName, arrow);
            string materialPath = k_Output + "/" + modelName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(k_Output + "/" + modelName + "_a.png"));
            material.SetFloat("_Metallic", modelName == "WP_A_1410" ? 0.05f : 0.35f);
            material.SetFloat("_Smoothness", 0.35f);
            material.SetFloat("_Cull", 0f);
            if (arrow)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cutoff", 0.4f);
            }
            EditorUtility.SetDirty(material);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                UnpackVisualInstances(root);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)
                    .Where(item => item is MeshRenderer || item is SkinnedMeshRenderer).ToArray())
                {
                    UnityEngine.Object.DestroyImmediate(renderer);
                }
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    UnityEngine.Object.DestroyImmediate(filter);
                }
                Transform parent = root.transform.Find("Weapon Pivot") ?? root.transform;
                Transform old = parent.Find("Extracted Equipment Visual");
                if (old != null) { UnityEngine.Object.DestroyImmediate(old.gameObject); }
                var visual = new GameObject("Extracted Equipment Visual");
                visual.transform.SetParent(parent, false);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                visual.AddComponent<MeshRenderer>().sharedMaterial = material;
                float scale = arrow ? length / mesh.bounds.size.z : 1f;
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = arrow
                    ? new Vector3(-mesh.bounds.center.x, -mesh.bounds.center.y, -mesh.bounds.min.z) * scale
                    : Vector3.zero;
                if (!arrow)
                {
                    parent.localPosition = Vector3.zero;
                    parent.localRotation = Quaternion.identity;
                }
                // The shield's authored damage volume is centered on the same equipment pivot.
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.AppendLine($"{path}: {modelName}; vertices={mesh.vertexCount}; length={length}; old rendered meshes removed");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Mesh BuildMesh(MeshRecord[] records, string name, bool arrow)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            Quaternion rotation = arrow ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
            foreach (MeshRecord record in records)
            {
                int offset = vertices.Count;
                for (int index = 0; index < record.Positions.Length / 3; index++)
                {
                    int position = index * 3;
                    vertices.Add(rotation * new Vector3(arrow ? -record.Positions[position] : record.Positions[position], record.Positions[position + 1], record.Positions[position + 2]));
                    normals.Add(rotation * new Vector3(arrow ? -record.Normals[position] : record.Normals[position], record.Normals[position + 1], record.Normals[position + 2]).normalized);
                    uv.Add(new Vector2(record.UV[index * 2], record.UV[index * 2 + 1]));
                }
                for (int index = 0; index < record.Indices.Length; index += 3)
                {
                    indices.Add(offset + record.Indices[index]);
                    indices.Add(offset + record.Indices[index + 2]);
                    indices.Add(offset + record.Indices[index + 1]);
                }
            }
            string path = k_Output + "/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void UnpackVisualInstances(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root.transform && PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                {
                    PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }
            }
        }

        private static void RefreshVfxFixtures()
        {
            foreach (string name in new[] { "Broadsword", "Straight Sword" })
            {
                string path = "Assets/_Dev/VFXAuthoring/SwordSlash/Fixtures/Weapons/" + name + ".prefab";
                GameObject fixture = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (fixture == null) { continue; }
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/" + name + ".prefab");
                Transform sourceVisual = source.transform.Find("Weapon Pivot/EldenRing " + name);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    UnpackVisualInstances(root);
                    foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (!AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith("Assets/_Game/Art/Equipment/Weapons/"))
                        {
                            continue;
                        }
                        MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                        if (renderer != null) { UnityEngine.Object.DestroyImmediate(renderer); }
                        UnityEngine.Object.DestroyImmediate(filter);
                    }
                    Transform pivot = root.transform.Find("Weapon Pivot");
                    if (pivot.Find(sourceVisual.name) == null)
                    {
                        Transform copy = UnityEngine.Object.Instantiate(sourceVisual.gameObject, pivot).transform;
                        copy.name = sourceVisual.name;
                        copy.localPosition = sourceVisual.localPosition;
                        copy.localRotation = sourceVisual.localRotation;
                        copy.localScale = sourceVisual.localScale;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        [Serializable] private sealed class Document { public Model[] Models; }
        [Serializable] private sealed class Model { public string Name; public MeshRecord[] Meshes; }
        [Serializable] private sealed class MeshRecord
        {
            public float[] Positions;
            public float[] Normals;
            public float[] UV;
            public int[] Indices;
        }
    }
}
