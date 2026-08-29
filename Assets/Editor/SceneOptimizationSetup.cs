#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.EditorTools
{
    /// <summary>Builds and bakes the EP101 scene-optimization configuration.</summary>
    public static class SceneOptimizationSetup
    {
        private const string k_ScenePath = WorldScenePathLayout.MasterScenePath;
        private const string k_LocationPath = "World/Location 01 - Ashen Crypt";
        private const string k_OptimizationRootName = "EP101 Scene Optimization";
        private const string k_MeshFolder = "Assets/_Game/Art/Environment/Shared/Meshes/LevelDesign";
        private const string k_MaterialFolder = "Assets/_Game/Art/Environment/Shared/Materials";
        private const string k_ImpostorMaterialPath =
            k_MaterialFolder + "/M_Crypt_Monument_Impostor.mat";

        private static bool s_waitingForOcclusionBake;
        private static bool s_occlusionBakeObservedWork;
        private static double s_occlusionBakeEarliestCompletionTime;

        /// <summary>Creates light activation volumes, local combined meshes, LODs, and an impostor.</summary>
        [MenuItem("Tools/ZZ/EP101/Build Scene Optimization")]
        public static void BuildSceneOptimization()
        {
            EnsureEditMode();
            EnsureFolder(k_MeshFolder);
            Scene scene = OpenWorldScene();
            Transform location = FindTransform(scene, k_LocationPath);
            if (location == null)
            {
                throw new InvalidOperationException(
                    "Build EP99-100 Ashen Crypt before applying EP101 optimization.");
            }

            Transform existingRoot = location.Find(k_OptimizationRootName);
            if (existingRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(existingRoot.gameObject);
            }

            GameObject optimizationRoot = new(k_OptimizationRootName);
            optimizationRoot.transform.SetParent(location, false);
            Transform lightVolumes = CreateChild(optimizationRoot.transform, "Light Activation Volumes");
            Transform lodObjects = CreateChild(optimizationRoot.transform, "LOD and Impostor Objects");

            CreateAreaLightVolumes(location, lightVolumes);
            CreateOcclusionArea(optimizationRoot.transform);
            CreateCryptMonumentLOD(lodObjects);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("EP101 scene optimization setup completed.");
        }

        /// <summary>Starts an asynchronous static-occlusion bake with dungeon-scaled parameters.</summary>
        [MenuItem("Tools/ZZ/EP101/Bake Occlusion Culling")]
        public static void BakeOcclusionCulling()
        {
            EnsureEditMode();
            Scene scene = OpenWorldScene();
            if (StaticOcclusionCulling.isRunning)
            {
                throw new InvalidOperationException(
                    "An occlusion-culling bake is already running.");
            }

            StaticOcclusionCulling.smallestOccluder = 3f;
            StaticOcclusionCulling.smallestHole = 0.5f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            StaticOcclusionCulling.Clear();
            s_waitingForOcclusionBake = true;
            s_occlusionBakeObservedWork = false;
            s_occlusionBakeEarliestCompletionTime =
                EditorApplication.timeSinceStartup + 1d;
            EditorApplication.update -= WaitForOcclusionBake;
            EditorApplication.update += WaitForOcclusionBake;
            StaticOcclusionCulling.GenerateInBackground();
            Debug.Log("EP101 occlusion-culling bake started.");
        }

        private static void CreateAreaLightVolumes(Transform location, Transform parent)
        {
            AreaDefinition[] areas =
            {
                new("Grace and Entry", "Sub Location 00 - Grace and Entry",
                    new Vector3(24f, 2f, 0f), new Vector3(18f, 12f, 24f)),
                new("Upper Path A", "Sub Location 01 - Upper Path A",
                    new Vector3(45f, 4f, 14f), new Vector3(46f, 16f, 22f)),
                new("Lower Path B", "Sub Location 02 - Lower Path B",
                    new Vector3(45f, -2f, -14f), new Vector3(46f, 16f, 22f)),
                new("Convergence", "Sub Location 03 - Convergence and Shortcut",
                    new Vector3(73f, 2f, 0f), new Vector3(50f, 16f, 30f)),
                new("Reward Wing", "Sub Location 04 - Visible Reward Wing",
                    new Vector3(78f, 2f, 17f), new Vector3(32f, 16f, 20f)),
                new("Boss Room", "Boss Room",
                    new Vector3(108f, 3f, 0f), new Vector3(34f, 18f, 30f))
            };

            foreach (AreaDefinition area in areas)
            {
                Transform zone = location.Find(area.ZoneName);
                if (zone == null)
                {
                    continue;
                }

                Light[] lights = zone.GetComponentsInChildren<Light>(true)
                    .Where(light => light.lightmapBakeType == LightmapBakeType.Realtime ||
                        light.lightmapBakeType == LightmapBakeType.Mixed)
                    .ToArray();
                if (lights.Length == 0)
                {
                    continue;
                }

                GameObject triggerObject = new(area.DisplayName + " Light Activation Trigger");
                triggerObject.transform.SetParent(parent, false);
                triggerObject.transform.position = area.Center;
                BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = area.Size;
                AreaLightActivationTrigger activation =
                    triggerObject.AddComponent<AreaLightActivationTrigger>();
                activation.Configure(lights);
            }
        }

        private static void CreateOcclusionArea(Transform parent)
        {
            GameObject areaObject = new("Ashen Crypt Occlusion View Volume");
            areaObject.transform.SetParent(parent, false);
            areaObject.transform.position = new Vector3(70f, 3f, 0f);
            OcclusionArea area = areaObject.AddComponent<OcclusionArea>();
            area.center = Vector3.zero;
            area.size = new Vector3(112f, 30f, 54f);
        }

        private static void CreateCryptMonumentLOD(Transform parent)
        {
            GameObject monument = new("Distant Crypt Monument LOD");
            monument.transform.SetParent(parent, false);
            monument.transform.position = new Vector3(98f, 0f, 0f);

            Mesh lod0Mesh = SaveMesh(
                CreateBoxMesh(
                    "EP101 Crypt Monument LOD0",
                    new[]
                    {
                        new BoxDefinition(new Vector3(0f, 4f, 0f), new Vector3(6f, 8f, 6f)),
                        new BoxDefinition(new Vector3(0f, 8.5f, 0f), new Vector3(7f, 1f, 7f)),
                        new BoxDefinition(new Vector3(0f, 11f, 0f), new Vector3(3f, 5f, 3f)),
                        new BoxDefinition(new Vector3(0f, 14f, 0f), new Vector3(4f, 1f, 4f)),
                        new BoxDefinition(new Vector3(3.5f, 3f, 0f), new Vector3(1f, 6f, 2f)),
                        new BoxDefinition(new Vector3(-3.5f, 3f, 0f), new Vector3(1f, 6f, 2f)),
                        new BoxDefinition(new Vector3(0f, 3f, 3.5f), new Vector3(2f, 6f, 1f)),
                        new BoxDefinition(new Vector3(0f, 3f, -3.5f), new Vector3(2f, 6f, 1f))
                    }),
                k_MeshFolder + "/EP101 Crypt Monument LOD0.asset");
            Mesh lod1Mesh = SaveMesh(
                CreateBoxMesh(
                    "EP101 Crypt Monument LOD1",
                    new[]
                    {
                        new BoxDefinition(new Vector3(0f, 4f, 0f), new Vector3(6f, 8f, 6f)),
                        new BoxDefinition(new Vector3(0f, 9.5f, 0f), new Vector3(4f, 3f, 4f)),
                        new BoxDefinition(new Vector3(0f, 12f, 0f), new Vector3(3f, 2f, 3f))
                    }),
                k_MeshFolder + "/EP101 Crypt Monument LOD1.asset");
            Mesh impostorMesh = SaveMesh(
                CreateCrossedImpostorMesh(),
                k_MeshFolder + "/EP101 Crypt Monument Impostor.asset");
            Material monumentMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                k_MaterialFolder + "/M_Graybox_Wall.mat");
            Material impostorMaterial = GetOrCreateImpostorMaterial();

            Renderer lod0Renderer = CreateLODRenderer(
                monument.transform,
                "LOD0 Combined Monument",
                lod0Mesh,
                monumentMaterial);
            Renderer lod1Renderer = CreateLODRenderer(
                monument.transform,
                "LOD1 Combined Monument",
                lod1Mesh,
                monumentMaterial);
            Renderer impostorRenderer = CreateLODRenderer(
                monument.transform,
                "LOD2 Crossed Impostor",
                impostorMesh,
                impostorMaterial);

            LODGroup lodGroup = monument.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(new[]
            {
                new LOD(0.35f, new[] { lod0Renderer }),
                new LOD(0.12f, new[] { lod1Renderer }),
                new LOD(0.03f, new[] { impostorRenderer })
            });
            lodGroup.RecalculateBounds();
        }

        private static Renderer CreateLODRenderer(
            Transform parent,
            string name,
            Mesh mesh,
            Material material)
        {
            GameObject lodObject = new(name);
            lodObject.transform.SetParent(parent, false);
            MeshFilter filter = lodObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = lodObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(
                lodObject,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic);
            return renderer;
        }

        private static Material GetOrCreateImpostorMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                k_ImpostorMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(shader)
                {
                    name = "M_Crypt_Monument_Impostor",
                    enableInstancing = true
                };
                AssetDatabase.CreateAsset(material, k_ImpostorMaterialPath);
            }

            Color color = new(0.12f, 0.15f, 0.18f, 1f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh CreateBoxMesh(string meshName, IEnumerable<BoxDefinition> boxes)
        {
            List<Vector3> vertices = new();
            List<int> triangles = new();
            foreach (BoxDefinition box in boxes)
            {
                AppendBox(vertices, triangles, box.Center, box.Size);
            }

            Mesh mesh = new() { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendBox(
            ICollection<Vector3> vertices,
            ICollection<int> triangles,
            Vector3 center,
            Vector3 size)
        {
            int start = vertices.Count;
            Vector3 half = size * 0.5f;
            vertices.Add(center + new Vector3(-half.x, -half.y, -half.z));
            vertices.Add(center + new Vector3(half.x, -half.y, -half.z));
            vertices.Add(center + new Vector3(half.x, half.y, -half.z));
            vertices.Add(center + new Vector3(-half.x, half.y, -half.z));
            vertices.Add(center + new Vector3(-half.x, -half.y, half.z));
            vertices.Add(center + new Vector3(half.x, -half.y, half.z));
            vertices.Add(center + new Vector3(half.x, half.y, half.z));
            vertices.Add(center + new Vector3(-half.x, half.y, half.z));

            int[] indices =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                1, 2, 6, 1, 6, 5,
                3, 0, 4, 3, 4, 7
            };
            foreach (int index in indices)
            {
                triangles.Add(start + index);
            }
        }

        private static Mesh CreateCrossedImpostorMesh()
        {
            Mesh mesh = new() { name = "EP101 Crypt Monument Impostor" };
            mesh.SetVertices(new[]
            {
                new Vector3(0f, 0f, -4f),
                new Vector3(0f, 15f, -4f),
                new Vector3(0f, 15f, 4f),
                new Vector3(0f, 0f, 4f),
                new Vector3(-4f, 0f, 0f),
                new Vector3(-4f, 15f, 0f),
                new Vector3(4f, 15f, 0f),
                new Vector3(4f, 0f, 0f)
            });
            mesh.SetTriangles(new[]
            {
                0, 1, 2, 0, 2, 3,
                4, 5, 6, 4, 6, 7
            }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh generatedMesh, string assetPath)
        {
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(generatedMesh, assetPath);
                return generatedMesh;
            }

            EditorUtility.CopySerialized(generatedMesh, asset);
            asset.name = generatedMesh.name;
            UnityEngine.Object.DestroyImmediate(generatedMesh);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void WaitForOcclusionBake()
        {
            if (!s_waitingForOcclusionBake)
            {
                return;
            }

            int dataSize = StaticOcclusionCulling.umbraDataSize;
            if (StaticOcclusionCulling.isRunning || dataSize <= 0)
            {
                s_occlusionBakeObservedWork = true;
                return;
            }

            if (!s_occlusionBakeObservedWork ||
                EditorApplication.timeSinceStartup <
                s_occlusionBakeEarliestCompletionTime)
            {
                return;
            }

            s_waitingForOcclusionBake = false;
            EditorApplication.update -= WaitForOcclusionBake;
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"EP101 occlusion-culling bake completed ({dataSize} bytes).");
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Scene OpenWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
            }

            SceneManager.SetActiveScene(scene);
            return scene;
        }

        private static Transform FindTransform(Scene scene, string path)
        {
            string[] segments = path.Split('/');
            Transform current = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == segments[0])
                ?.transform;
            for (int index = 1; index < segments.Length && current != null; index++)
            {
                current = current.Find(segments[index]);
            }

            return current;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "EP101 setup tools can only run after Play Mode has fully stopped.");
            }
        }

        private readonly struct AreaDefinition
        {
            public AreaDefinition(
                string displayName,
                string zoneName,
                Vector3 center,
                Vector3 size)
            {
                DisplayName = displayName;
                ZoneName = zoneName;
                Center = center;
                Size = size;
            }

            public string DisplayName { get; }
            public string ZoneName { get; }
            public Vector3 Center { get; }
            public Vector3 Size { get; }
        }

        private readonly struct BoxDefinition
        {
            public BoxDefinition(Vector3 center, Vector3 size)
            {
                Center = center;
                Size = size;
            }

            public Vector3 Center { get; }
            public Vector3 Size { get; }
        }
    }
}
#endif
