using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Authors licensed particle families and replaces existing visual assets without changing their GUIDs.</summary>
    public static class AnimeVfxSetup
    {
        private const string k_TextureRoot = "Assets/_Game/Art/VFX/Licensed/KenneyParticles/";
        private const string k_MaterialRoot = "Assets/_Game/Art/VFX/Anime/Materials/";
        private const string k_PrefabRoot = "Assets/_Game/Prefabs/Effects/Anime/";
        private const string k_CombatRoot = "Assets/_Game/Resources/Effects/Anime/";
        private static readonly Color s_gold = new(1f, 0.73f, 0.28f);
        private static readonly Color s_cyan = new(0.2f, 0.75f, 1f);
        private static readonly Color s_violet = new(0.66f, 0.28f, 0.95f);
        private static readonly string[] s_paths =
        {
            "Assets/_Game/Prefabs/Effects/BloodSplatterVFX.prefab",
            k_CombatRoot + "Critical Impact.prefab",
            k_CombatRoot + "Guard Spark.prefab",
            k_CombatRoot + "Parry Flash.prefab",
            k_PrefabRoot + "Crescent Slash.prefab",
            k_PrefabRoot + "Ember Cast.prefab",
            k_PrefabRoot + "Frost Cast.prefab",
            "Assets/_Game/Prefabs/Effects/Health Flask VFX.prefab",
            "Assets/_Game/Prefabs/Effects/Focus Flask VFX.prefab",
            "Assets/_Game/Resources/Effects/Poisoned VFX.prefab",
            "Assets/_Game/Resources/Effects/Frostbite VFX.prefab",
            k_PrefabRoot + "Ambient Wisps.prefab"
        };

        /// <summary>Creates repeatable VFX assets. Existing replacement targets are backed up once before modification.</summary>
        [MenuItem("Tools/ZZ/Art/Build Anime VFX")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before authoring VFX.");
            }
            Directory.CreateDirectory(k_MaterialRoot);
            Directory.CreateDirectory(k_PrefabRoot);
            Directory.CreateDirectory(k_CombatRoot);
            AssetDatabase.Refresh();
            foreach (string name in new[] { "Critical Impact", "Guard Spark", "Parry Flash" })
            {
                string oldPath = k_PrefabRoot + name + ".prefab";
                if (File.Exists(oldPath) && !File.Exists(k_CombatRoot + name + ".prefab"))
                {
                    string error = AssetDatabase.MoveAsset(oldPath, k_CombatRoot + name + ".prefab");
                    if (!string.IsNullOrEmpty(error))
                    {
                        throw new InvalidOperationException(error);
                    }
                }
            }
            ConfigureTextures();
            Shader shader = Shader.Find("ZZ/Anime/VFX");
            Shader graph = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/_Game/Shaders/Anime/AnimeParticleGraph.shadergraph");
            if (shader == null || graph == null || ShaderUtil.ShaderHasError(shader) || ShaderUtil.ShaderHasError(graph))
            {
                throw new InvalidOperationException("Anime VFX shaders must import successfully first.");
            }

            Material spark = MaterialFor("Gold Spark", shader, "spark_04", s_gold);
            Material star = MaterialFor("Ivory Star", shader, "star_04", Color.white);
            Material ring = MaterialFor("Gold Ring", shader, "circle_02", s_gold);
            Material slash = MaterialFor("Crescent", shader, "slash_02", s_gold);
            Material ember = MaterialFor("Ember", shader, "flame_01", new Color(1f, 0.32f, 0.08f));
            Material frost = MaterialFor("Frost Sigil", shader, "magic_03", s_cyan);
            Material mote = MaterialFor("Mote", shader, "light_01", Color.white);
            Material smoke = MaterialFor("Cel Smoke Graph", graph, "smoke_07", Color.white);
            for (int index = 0; index < s_paths.Length; index++)
            {
                bool loops = index >= 9;
                string path = s_paths[index];
                Backup(path);
                bool exists = File.Exists(path);
                GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(Path.GetFileNameWithoutExtension(path));
                try
                {
                    ClearVisuals(root);
                    if (!loops)
                    {
                        root.AddComponent<Utility_DestroyAfterTime>();
                    }
                    switch (index)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                            Layer(root, "Impact star", star, Color.white, 1, 0.18f, index == 1 ? 1.2f : 0.7f, 0f, false);
                            Layer(root, "Flying sparks", spark, Color.white, index == 1 ? 14 : 7, 0.38f, 0.25f, 2.2f, false);
                            if (index != 0)
                            {
                                Layer(root, "Impact ring", ring, index == 3 ? s_cyan : Color.white, 1, 0.4f, 1.3f, 0f, false);
                            }
                            break;
                        case 4:
                            Layer(root, "Blade crescent", slash, Color.white, 1, 0.25f, 2.3f, 0f, false);
                            Layer(root, "Edge flecks", spark, Color.white, 5, 0.3f, 0.14f, 1f, false);
                            break;
                        case 5:
                        case 6:
                            Layer(root, "Element core", index == 5 ? ember : frost, Color.white, 3, 0.7f, 1.1f, 0.35f, false);
                            Layer(root, "Element motes", mote, index == 5 ? s_gold : s_cyan, 12, 0.9f, 0.14f, 1.1f, false);
                            break;
                        case 7:
                        case 8:
                            Layer(root, "Restoration ring", ring, index == 7 ? Color.white : s_cyan, 1, 0.9f, 1.5f, 0f, false);
                            Layer(root, "Rising restoration", mote, index == 7 ? s_gold : s_cyan, 18, 1.4f, 0.13f, 0.5f, false);
                            break;
                        case 9:
                            Layer(root, "Violet wisps", smoke, s_violet, 6, 1.1f, 0.65f, 0.2f, true);
                            Layer(root, "Poison motes", mote, s_violet, 5, 0.8f, 0.08f, 0.3f, true);
                            break;
                        case 10:
                            Layer(root, "Frost stars", frost, Color.white, 4, 0.9f, 0.35f, 0.2f, true);
                            Layer(root, "Cold wisps", smoke, s_cyan, 4, 1.1f, 0.5f, 0.18f, true);
                            break;
                        case 11:
                            Layer(root, "Ambient motes", mote, s_cyan, 5, 2f, 0.06f, 0.08f, true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(index));
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    if (exists)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }
            MaterialFor("Telegraph", Shader.Find("ZZ/Anime/Telegraph"), null, Color.white);
            ReplaceWeaponTrail(shader);
            AssetDatabase.SaveAssets();
            File.WriteAllLines(".utmp/anime-vfx-assets.txt", s_paths);
        }

        /// <summary>Renders the particle family in an isolated preview scene without changing game scenes.</summary>
        [MenuItem("Tools/ZZ/Art/Preview Anime VFX")]
        public static void RenderPreview()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = RenderTexture.GetTemporary(1600, 1000, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                GameObject cameraObject = new("VFX preview camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = 4.8f;
                camera.transform.position = new Vector3(0f, 0f, -20f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.04f, 0.075f);
                camera.targetTexture = target;
                for (int index = 0; index < s_paths.Length; index++)
                {
                    GameObject effect = (GameObject)PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(s_paths[index]), scene);
                    effect.transform.position = new Vector3((index % 4 - 1.5f) * 3.5f, (1 - index / 4) * 2.9f + 0.3f, 0f);
                    foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
                    {
                        particles.useAutoRandomSeed = false;
                        particles.randomSeed = (uint)(index + 21);
                        particles.Simulate(index >= 9 ? 1.7f : 0.12f, false, true);
                    }
                    GameObject label = new("Label", typeof(TextMesh));
                    SceneManager.MoveGameObjectToScene(label, scene);
                    label.transform.position = effect.transform.position + new Vector3(0, -1.15f, 0);
                    TextMesh text = label.GetComponent<TextMesh>();
                    text.text = index == 0 ? "Hit Spark (replacement)" : Path.GetFileNameWithoutExtension(s_paths[index]);
                    text.fontSize = 32;
                    text.characterSize = 0.055f;
                    text.anchor = TextAnchor.MiddleCenter;
                    text.color = new Color(0.7f, 0.8f, 0.95f);
                }
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(".utmp/anime-vfx-preview.png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (image != null)
                {
                    UnityEngine.Object.DestroyImmediate(image);
                }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ReplaceWeaponTrail(Shader shader)
        {
            string path = "Assets/_Game/Prefabs/Effects/Weapon Trail.prefab";
            Backup(path);
            Material material = MaterialFor("Blade Trail", shader, "trace_01", s_gold);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    renderer.sharedMaterials = new[] { material };
                    renderer.trailMaterial = material;
                }
                foreach (TrailRenderer renderer in root.GetComponentsInChildren<TrailRenderer>(true))
                {
                    renderer.sharedMaterial = material;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureTextures()
        {
            foreach (string path in Directory.GetFiles(k_TextureRoot, "*.png"))
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static Material MaterialFor(string name, Shader shader, string texture, Color color)
        {
            string path = k_MaterialRoot + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (texture != null)
            {
                string property = material.HasProperty("_BaseMap") ? "_BaseMap" : "_Base_Map";
                material.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture2D>(k_TextureRoot + texture + ".png"));
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
                material.SetColor("_CoreColor", Color.Lerp(color, Color.white, 0.8f) * 1.4f);
                material.SetFloat("_DstBlend", (float)BlendMode.One);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Layer(GameObject root, string name, Material material, Color color,
            int count, float lifetime, float size, float speed, bool loops)
        {
            GameObject layer = new(name);
            layer.transform.SetParent(root.transform, false);
            ParticleSystem particles = layer.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = 2f;
            main.loop = loops;
            main.startLifetime = lifetime;
            main.startSize = size;
            main.startSpeed = speed;
            main.startColor = color;
            main.maxParticles = 64;
            main.simulationSpace = loops ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = true;
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = loops ? count : 0;
            if (!loops)
            {
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            }
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = count > 1;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = loops ? 0.4f : 0.08f;
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.SizeOverLifetimeModule growth = particles.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.2f));
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
            });
        }

        private static void ClearVisuals(GameObject root)
        {
            // Keep the root object so existing prefab references retain their local file ID as well as GUID.
            foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null || script is not Utility_DestroyAfterTime)
                {
                    throw new InvalidOperationException("Replacement target contains non-visual behaviour: " + root.name);
                }
            }
            while (root.transform.childCount > 0)
            {
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
            }
            foreach (ParticleSystem particles in root.GetComponents<ParticleSystem>())
            {
                UnityEngine.Object.DestroyImmediate(particles);
            }
            foreach (Component component in root.GetComponents<Component>())
            {
                if (component is not Transform)
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }

        private static void Backup(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }
            string directory = ".utmp/vfx-before";
            Directory.CreateDirectory(directory);
            string backup = directory + "/" + AssetDatabase.AssetPathToGUID(path) + ".prefab";
            if (!File.Exists(backup))
            {
                File.Copy(path, backup);
            }
        }
    }
}
