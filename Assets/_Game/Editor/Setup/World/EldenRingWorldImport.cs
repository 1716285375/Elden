using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Installs selected original environment and feedback assets through existing game content.</summary>
    public static class EldenRingWorldImport
    {
        private const string k_Source = "F:/MyProject/Game/RE-Assets/EldenRing/Converted/Map_Stormveil/models.json";
        private const string k_Output = "Assets/_Game/Art/Environment/EldenRing/Stormveil";
        private const string k_Prefab = k_Output + "/Stormveil Rampart.prefab";
        private const string k_Scene = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery/Regions/" +
            "R01_MonasteryOutskirts/SCN_LV01_R01_A01_Props.unity";

        /// <summary>Builds a collidable map chunk and places it outside the existing starting path.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Install Stormveil Environment")]
        public static void InstallEnvironment()
        {
            RequireStopped();
            Directory.CreateDirectory(k_Output + "/Meshes");
            Directory.CreateDirectory(k_Output + "/Materials");
            AssetDatabase.Refresh();
            Model model = JsonUtility.FromJson<Document>(File.ReadAllText(k_Source)).Models.Single();
            Vector3[] positions = model.Meshes.SelectMany(mesh => Enumerable.Range(0, mesh.Positions.Length / 3)
                .Select(index => ReadVector(mesh.Positions, index))).ToArray();
            var bounds = new Bounds(positions[0], Vector3.zero);
            foreach (Vector3 position in positions)
            {
                bounds.Encapsulate(position);
            }
            Vector3 origin = new(bounds.center.x, bounds.min.y, bounds.center.z);
            var root = new GameObject("Stormveil Rampart");
            try
            {
                Material[] materials = BuildMaterials(model);
                for (int index = 0; index < model.Meshes.Length; index++)
                {
                    MeshRecord source = model.Meshes[index];
                    if (model.Materials[source.Material].Textures.All(texture =>
                        !texture.Type.Contains("AlbedoMap") || !texture.Name.StartsWith("AET")))
                    {
                        continue;
                    }
                    string path = k_Output + "/Meshes/Chunk_" + index + ".asset";
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    bool isNew = mesh == null;
                    mesh ??= new Mesh();
                    mesh.Clear();
                    mesh.indexFormat = IndexFormat.UInt32;
                    mesh.vertices = Enumerable.Range(0, source.Positions.Length / 3)
                        .Select(vertex => Mirror(ReadVector(source.Positions, vertex) - origin)).ToArray();
                    mesh.normals = Enumerable.Range(0, source.Normals.Length / 3)
                        .Select(vertex => Mirror(ReadVector(source.Normals, vertex)).normalized).ToArray();
                    mesh.uv = Enumerable.Range(0, source.UV.Length / 2)
                        .Select(vertex => new Vector2(source.UV[vertex * 2], source.UV[vertex * 2 + 1])).ToArray();
                    int[] triangles = (int[])source.Indices.Clone();
                    for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                    {
                        (triangles[triangle + 1], triangles[triangle + 2]) =
                            (triangles[triangle + 2], triangles[triangle + 1]);
                    }
                    mesh.triangles = triangles;
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    if (isNew)
                    {
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    EditorUtility.SetDirty(mesh);
                    var part = new GameObject("Rampart " + index);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = materials[source.Material];
                    part.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
                PrefabUtility.SaveAsPrefabAsset(root, k_Prefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            Scene scene = SceneManager.GetSceneByPath(k_Scene);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen)
            {
                scene = EditorSceneManager.OpenScene(k_Scene, OpenSceneMode.Additive);
            }
            try
            {
                GameObject instance = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Stormveil Rampart");
                if (instance == null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(k_Prefab), scene);
                }
                instance.transform.position = new Vector3(-12f, -6f, 75f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ELDEN_RING_MAP_INSTALLED " + k_Prefab);
        }

        /// <summary>Reuses the authored guard and parry emitters with original flare textures.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Install Effect Textures")]
        public static void InstallEffects()
        {
            RequireStopped();
            Directory.CreateDirectory("Assets/_Game/Art/VFX/EldenRing/Materials");
            foreach (string name in new[] { "Guard Spark", "Parry Flash" })
            {
                string prefabPath = "Assets/_Game/Resources/Effects/Anime/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    ParticleSystemRenderer[] renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
                    for (int index = 0; index < renderers.Length; index++)
                    {
                        string path = "Assets/_Game/Art/VFX/EldenRing/Materials/" + name + "_" + index + ".mat";
                        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material == null)
                        {
                            material = new Material(renderers[index].sharedMaterial);
                            AssetDatabase.CreateAsset(material, path);
                        }
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                            "Assets/_Game/Art/VFX/EldenRing/Textures/" +
                            (name == "Guard Spark" ? "s10030_a.png" : "s10061_a.png"));
                        if (texture == null)
                        {
                            throw new InvalidOperationException("Missing original effect texture.");
                        }
                        material.SetTexture("_BaseMap", texture);
                        renderers[index].sharedMaterial = material;
                        EditorUtility.SetDirty(material);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Installs a short original c3000 sound pool on the existing enemy damage feedback.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Install Enemy Audio")]
        public static void InstallAudio()
        {
            RequireStopped();
            AssetDatabase.Refresh();
            string[] paths = Directory.GetFiles("Assets/_Game/Art/Audio/EldenRing/c3000", "*.wav")
                .OrderBy(path => path).ToArray();
            foreach (string path in paths)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            const string prefabPath = "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var serialized = new SerializedObject(root.GetComponentInChildren<CharacterSoundFXManager>(true));
                SerializedProperty clips = serialized.FindProperty("m_damageGrunts");
                clips.arraySize = paths.Length;
                for (int index = 0; index < paths.Length; index++)
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[index].Replace('\\', '/'));
                    if (clip == null || clip.length <= 0f)
                    {
                        throw new InvalidOperationException("Original audio did not import: " + paths[index]);
                    }
                    clips.GetArrayElementAtIndex(index).objectReferenceValue = clip;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static Material[] BuildMaterials(Model model)
        {
            return model.Materials.Select((source, index) =>
            {
                string path = k_Output + "/Materials/Stone_" + index + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                TextureRecord albedo = source.Textures.FirstOrDefault(texture =>
                    texture.Type.Contains("AlbedoMap") && texture.Name.StartsWith("AET"));
                if (albedo != null)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        k_Output + "/Textures/" + albedo.Name + ".png");
                    if (texture == null)
                    {
                        throw new InvalidOperationException("Missing original map albedo: " + albedo.Name);
                    }
                    material.SetTexture("_BaseMap", texture);
                }
                // ponytail: one albedo layer; restore original vertex-blended materials when the shader is ported.
                material.SetFloat("_Smoothness", 0.15f);
                material.SetFloat("_Cull", 0f);
                EditorUtility.SetDirty(material);
                return material;
            }).ToArray();
        }

        private static Vector3 ReadVector(float[] values, int index) =>
            new(values[index * 3], values[index * 3 + 1], values[index * 3 + 2]);

        private static Vector3 Mirror(Vector3 vector) => new(-vector.x, vector.y, vector.z);

        private static void RequireStopped()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before installing original content.");
            }
        }

        [Serializable] private sealed class Document { public Model[] Models; }
        [Serializable] private sealed class Model { public MeshRecord[] Meshes; public MaterialRecord[] Materials; }
        [Serializable] private sealed class MaterialRecord { public TextureRecord[] Textures; }
        [Serializable] private sealed class TextureRecord { public string Type; public string Name; }
        [Serializable] private sealed class MeshRecord
        {
            public int Material;
            public float[] Positions;
            public float[] Normals;
            public float[] UV;
            public int[] Indices;
        }
    }
}
