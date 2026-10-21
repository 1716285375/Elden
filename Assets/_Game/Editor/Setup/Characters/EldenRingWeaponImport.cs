using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Replaces sword presentation without changing gameplay prefab references.</summary>
    public static class EldenRingWeaponImport
    {
        private const string k_Source = "F:/MyProject/RE/EldenRing/Converted/Weapons";

        /// <summary>Imports the original weapon mesh, fitted to the authored damage volume.</summary>
        [MenuItem("Tools/ZZ/Import Elden Ring Broadsword")]
        public static void Apply()
        {
            ImportSword("Broadsword", "WP_A_0201");
        }

        /// <summary>Imports the Longsword model into the existing starter Straight Sword prefab.</summary>
        [MenuItem("Tools/ZZ/Import Elden Ring Straight Sword")]
        public static void ApplyStraightSword()
        {
            ImportSword("Straight Sword", "WP_A_0200");
        }

        private static void ImportSword(string weaponName, string modelName)
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before importing weapon art.");
            }
            string output = "Assets/_Game/Art/Equipment/EldenRing/" + weaponName;
            string prefabPath = "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/" + weaponName + ".prefab";
            string visualName = "EldenRing " + weaponName;
            Directory.CreateDirectory(output);
            File.Copy(k_Source + "/Textures/" + modelName + "_a.png", output + "/Albedo.png", true);
            AssetDatabase.Refresh();
            var document = JsonUtility.FromJson<Document>(File.ReadAllText(k_Source + "/models.json"));
            MeshRecord source = document.Models.Single(model => model.Name == modelName).Meshes.Single();
            Mesh mesh = ImportMesh(source, output, weaponName);
            Material material = ImportMaterial(output, weaponName);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform pivot = root.transform.Find("Weapon Pivot");
                BoxCollider damage = root.GetComponentInChildren<MeleeWeaponDamageCollider>(true)
                    .GetComponent<BoxCollider>();
                if (pivot == null || damage.transform.parent != pivot)
                {
                    throw new InvalidOperationException(weaponName + " damage volume no longer uses the expected pivot.");
                }
                foreach (MeshRenderer renderer in pivot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.enabled = false;
                }
                Transform visual = pivot.Find(visualName);
                if (visual == null)
                {
                    visual = new GameObject(visualName).transform;
                    visual.SetParent(pivot, false);
                    visual.gameObject.AddComponent<MeshFilter>();
                    visual.gameObject.AddComponent<MeshRenderer>();
                }
                pivot.localPosition = Vector3.zero;
                pivot.localRotation = Quaternion.identity;
                pivot.localScale = Vector3.one;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
                visual.localPosition = Vector3.zero;
                damage.transform.localPosition = Vector3.zero;
                damage.transform.localRotation = Quaternion.identity;
                damage.center = mesh.bounds.center;
                damage.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.04f);
                visual.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer importedRenderer = visual.GetComponent<MeshRenderer>();
                importedRenderer.sharedMaterial = material;
                importedRenderer.enabled = true;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("ELDEN_RING_WEAPON_COMPLETE " + weaponName + " vertices=" + mesh.vertexCount);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Mesh ImportMesh(MeshRecord source, string output, string weaponName)
        {
            string path = output + "/" + weaponName + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            mesh ??= new Mesh();
            mesh.Clear();
            mesh.name = "EldenRing " + weaponName;
            int count = source.Positions.Length / 3;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            for (int index = 0; index < count; index++)
            {
                // Preserve the original grip origin and axes used by the c0000 hand dummy.
                vertices[index] = Rotate(source.Positions, index * 3);
                normals[index] = Rotate(source.Normals, index * 3).normalized;
                uv[index] = new Vector2(source.UV[index * 2], source.UV[index * 2 + 1]);
            }
            int[] triangles = (int[])source.Indices.Clone();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                (triangles[index + 1], triangles[index + 2]) = (triangles[index + 2], triangles[index + 1]);
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (isNew)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        private static Material ImportMaterial(string output, string weaponName)
        {
            string path = output + "/" + weaponName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            material ??= new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(output + "/Albedo.png"));
            material.SetFloat("_Metallic", 0.4f);
            material.SetFloat("_Smoothness", 0.4f);
            if (isNew)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Vector3 Rotate(float[] values, int index)
        {
            return new Vector3(values[index], values[index + 1], values[index + 2]);
        }

        [Serializable] private sealed class Document { public Model[] Models; }
        [Serializable] private sealed class Model { public string Name; public MeshRecord[] Meshes; }
        [Serializable]
        private sealed class MeshRecord
        {
            public float[] Positions;
            public float[] Normals;
            public float[] UV;
            public int[] Indices;
        }
    }
}
