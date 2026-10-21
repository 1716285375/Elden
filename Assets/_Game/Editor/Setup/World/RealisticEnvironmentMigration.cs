using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Audits legacy environment dependencies before removing superseded art.</summary>
    public static class RealisticEnvironmentMigration
    {
        private const string k_HiFi = "Assets/_Game/UI/Frontend/HiFi";
        private const string k_Report = "Docs/ArtRefresh";
        private const string k_Collision = "Assets/_Game/Art/Environment/Collision";

        /// <summary>Captures the starting region without entering Play Mode or writing scene data.</summary>
        [MenuItem("Tools/ZZ/Environment/Capture Starting Region")]
        public static void CaptureStartingRegion()
        {
            string prefix = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery/Regions/R01_MonasteryOutskirts/";
            Scene previous = SceneManager.GetActiveScene();
            var opened = new List<Scene>();
            string master = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery/SCN_LV01_AbandonedMonastery.unity";
            foreach (string path in new[] { master, prefix + "SCN_LV01_R01_A01_Base.unity", prefix + "SCN_LV01_R01_A01_Props.unity" })
            {
                if (!SceneManager.GetSceneByPath(path).isLoaded)
                {
                    opened.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
                }
            }
            Scene preview = SceneManager.GetSceneByPath(master);
            SceneManager.SetActiveScene(preview);
            Scene[] captureScenes = { preview, SceneManager.GetSceneByPath(prefix + "SCN_LV01_R01_A01_Base.unity"),
                SceneManager.GetSceneByPath(prefix + "SCN_LV01_R01_A01_Props.unity") };
            Renderer[] previewRenderers = captureScenes
                .SelectMany(scene => scene.GetRootGameObjects())
                .SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();
            var inactive = new HashSet<GameObject>();
            var hidden = previewRenderers.Where(renderer => renderer.forceRenderingOff).ToArray();
            var disabled = previewRenderers.Where(renderer => !renderer.enabled).ToArray();
            LODGroup[] previewLods = previewRenderers.Select(renderer => renderer.GetComponentInParent<LODGroup>())
                .Where(group => group != null).Distinct().ToArray();
            foreach (Renderer renderer in previewRenderers)
            {
                for (Transform ancestor = renderer.transform; ancestor != null; ancestor = ancestor.parent)
                {
                    if (!ancestor.gameObject.activeSelf)
                    {
                        inactive.Add(ancestor.gameObject);
                    }
                }
            }
            foreach (Terrain terrain in captureScenes.SelectMany(scene => scene.GetRootGameObjects())
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true)))
            {
                for (Transform ancestor = terrain.transform; ancestor != null; ancestor = ancestor.parent)
                {
                    if (!ancestor.gameObject.activeSelf)
                    {
                        inactive.Add(ancestor.gameObject);
                    }
                }
            }
            foreach (GameObject item in inactive)
            {
                item.SetActive(true);
            }
            foreach (Renderer renderer in hidden)
            {
                renderer.forceRenderingOff = false;
            }
            foreach (Renderer renderer in disabled)
            {
                renderer.enabled = true;
            }
            foreach (LODGroup group in previewLods)
            {
                group.ForceLOD(0);
            }
            var cameraObject = new GameObject("Art verification camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(4f, 24f, -28f);
            camera.transform.LookAt(new Vector3(32f, 10f, 20f));
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.3f, 0.37f, 0.43f);
            camera.farClipPlane = 1500f;
            camera.useOcclusionCulling = false;
            var sunlight = new GameObject("Art verification sun", typeof(Light));
            sunlight.hideFlags = HideFlags.HideAndDontSave;
            Light light = sunlight.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.7f;
            light.shadows = LightShadows.Soft;
            sunlight.transform.rotation = Quaternion.Euler(42f, -25f, 0f);
            var target = new RenderTexture(1920, 1080, 24);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                bool migrated = File.Exists(k_Report + "/level-migration-summary.txt");
                File.WriteAllBytes(k_Report + (migrated ? "/region-after.png" : "/region-before.png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sunlight);
                foreach (Renderer renderer in hidden)
                {
                    renderer.forceRenderingOff = true;
                }
                foreach (Renderer renderer in disabled)
                {
                    renderer.enabled = false;
                }
                foreach (LODGroup group in previewLods)
                {
                    group.ForceLOD(-1);
                }
                foreach (GameObject item in inactive)
                {
                    item.SetActive(false);
                }
                SceneManager.SetActiveScene(previous);
                foreach (Scene scene in opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>Replaces retired nature visuals while retaining level identities and collision geometry.</summary>
        [MenuItem("Tools/ZZ/Environment/Replace Legacy Level Art")]
        public static void ReplaceLevelArt()
        {
            if (Application.isPlaying || EditorSceneManager.GetSceneManagerSetup().Any(setup =>
                SceneManager.GetSceneByPath(setup.path).isDirty))
            {
                throw new InvalidOperationException("Stop Play Mode and save open scenes before migrating level art.");
            }
            string[] library = AssetDatabase.FindAssets("t:Prefab", new[] { RealisticNatureImport.Root + "/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            if (library.Length != 20)
            {
                throw new InvalidDataException("Build and verify the 20 realistic nature prefabs first.");
            }
            string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Game/Scenes/Levels" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.GetDependencies(path).Any(IsLegacy)).OrderBy(path => path).ToArray();
            Directory.CreateDirectory(k_Collision);
            AssetDatabase.Refresh();
            var report = new List<string>();
            int replaced = 0;
            int decorations = 0;
            foreach (string path in scenes)
            {
                string backup = ".utmp/art-refresh-levels/" + path;
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup))
                {
                    File.Copy(path, backup);
                }
                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasOpen = scene.isLoaded;
                if (!wasOpen)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                try
                {
                    string[] collisionBefore = CollisionSignature(scene);
                    Transform[] all = scene.GetRootGameObjects().SelectMany(root =>
                        root.GetComponentsInChildren<Transform>(true)).ToArray();
                    var targets = all.Where(transform => PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject) &&
                        IsLegacy(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform.gameObject)))
                        .ToArray();
                    targets = targets.Where(target => !targets.Any(other => other != target && target.IsChildOf(other)))
                        .ToArray();
                    foreach (Transform target in targets)
                    {
                        string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target.gameObject);
                        Renderer[] oldRenderers = target.GetComponentsInChildren<Renderer>(true);
                        Bounds bounds = LocalBounds(target, oldRenderers);
                        GameObject outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(target.gameObject);
                        if (outermost != null)
                        {
                            PrefabUtility.UnpackPrefabInstance(outermost, PrefabUnpackMode.Completely,
                                InteractionMode.AutomatedAction);
                        }
                        foreach (MeshCollider collider in target.GetComponentsInChildren<MeshCollider>(true))
                        {
                            if (IsLegacy(AssetDatabase.GetAssetPath(collider.sharedMesh)))
                            {
                                collider.sharedMesh = PreserveMesh(collider.sharedMesh);
                            }
                        }
                        foreach (MonoBehaviour behaviour in target.GetComponentsInChildren<MonoBehaviour>(true))
                        {
                            if (behaviour != null && IsLegacy(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(behaviour))))
                            {
                                Object.DestroyImmediate(behaviour);
                            }
                        }
                        foreach (Animator animator in target.GetComponentsInChildren<Animator>(true))
                        {
                            if (IsLegacy(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)))
                            {
                                Object.DestroyImmediate(animator);
                            }
                        }
                        foreach (LODGroup group in target.GetComponentsInChildren<LODGroup>(true))
                        {
                            Object.DestroyImmediate(group);
                        }
                        string name = Path.GetFileNameWithoutExtension(source);
                        bool water = name == "Water" || name == "Lake";
                        if (water)
                        {
                            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>(true))
                            {
                                filter.sharedMesh = PreserveMesh(filter.sharedMesh);
                            }
                            foreach (Renderer renderer in oldRenderers)
                            {
                                renderer.sharedMaterials = new[] { WaterMaterial() };
                            }
                        }
                        else
                        {
                            foreach (ParticleSystem particles in target.GetComponentsInChildren<ParticleSystem>(true))
                            {
                                Object.DestroyImmediate(particles);
                            }
                            foreach (Renderer renderer in oldRenderers.Where(renderer => renderer != null))
                            {
                                Object.DestroyImmediate(renderer);
                            }
                            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>(true))
                            {
                                Object.DestroyImmediate(filter);
                            }
                            string category = name.StartsWith("Fir_") ? "fir_tree_01" :
                                name.Contains("Tree") ? "tree_small_02" : name.StartsWith("Bush") ? "shrub_02" :
                                name.StartsWith("Cliff") ? "coastal_cliff_02" :
                                name.StartsWith("Rock") || name.StartsWith("Stone") ? "rock_moss_set_01" :
                                name.StartsWith("Cattail") ? "grass_medium_01" : null;
                            if (category != null)
                            {
                                string[] variants = library.Where(prefab => Path.GetFileName(prefab).StartsWith(category)).ToArray();
                                string replacement = variants[replaced % variants.Length];
                                var visual = (GameObject)PrefabUtility.InstantiatePrefab(
                                    AssetDatabase.LoadAssetAtPath<GameObject>(replacement), scene);
                                visual.name = "Realistic Visual";
                                visual.transform.SetParent(target, false);
                                Bounds sourceBounds = LocalBounds(visual.transform,
                                    visual.GetComponentsInChildren<Renderer>(true));
                                Vector3 scale = new Vector3(
                                    bounds.size.x / Mathf.Max(sourceBounds.size.x, 0.001f),
                                    bounds.size.y / Mathf.Max(sourceBounds.size.y, 0.001f),
                                    bounds.size.z / Mathf.Max(sourceBounds.size.z, 0.001f));
                                visual.transform.localScale = scale;
                                visual.transform.localPosition = bounds.center - Vector3.Scale(sourceBounds.center, scale);
                                replaced++;
                            }
                            else
                            {
                                decorations++;
                            }
                        }
                        report.Add(path + "\t" + AnimationUtility.CalculateTransformPath(target, null) + "\t" + source);
                    }
                    if (!collisionBefore.SequenceEqual(CollisionSignature(scene)))
                    {
                        throw new InvalidDataException("Collision state changed during art migration: " + path);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.Add("COLLISION_PRESERVED\t" + path + "\t" + collisionBefore.Length);
                    File.WriteAllLines(k_Report + "/level-migration-progress.tsv", report);
                }
                finally
                {
                    if (!wasOpen)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(k_Report + "/level-migration-summary.txt", "Scenes=" + scenes.Length +
                "\nReplacedVisuals=" + replaced + "\nRetiredDecorations=" + decorations + "\nCollision=PASS\n");
            AuditRemainingDependencies();
        }

        private static bool IsLegacy(string path)
        {
            return path != null && path.Contains("/IdyllicFantasyNature/");
        }

        private static Bounds LocalBounds(Transform root, Renderer[] renderers)
        {
            bool initialized = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Renderer renderer in renderers.Where(renderer => !(renderer is ParticleSystemRenderer)))
            {
                Bounds local = renderer.localBounds;
                Matrix4x4 matrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 point = matrix.MultiplyPoint3x4(local.center + offset);
                    if (!initialized)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        private static Mesh PreserveMesh(Mesh source)
        {
            if (source == null)
            {
                return null;
            }
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
            string path = k_Collision + "/" + guid + "_" + localId + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Object.Instantiate(source);
                mesh.name = source.name;
                AssetDatabase.CreateAsset(mesh, path);
            }
            if (mesh.vertexCount != source.vertexCount || mesh.subMeshCount != source.subMeshCount || mesh.bounds != source.bounds)
            {
                throw new InvalidDataException("Collision mesh copy differs from its source: " + path);
            }
            return mesh;
        }

        private static string[] CollisionSignature(Scene scene)
        {
            Physics.SyncTransforms();
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Collider>(true))
                .Select(collider => AnimationUtility.CalculateTransformPath(collider.transform, null) + "|" +
                    collider.GetType().FullName + "|" + collider.enabled + "|" + collider.isTrigger + "|" +
                    collider.gameObject.layer + "|" + collider.transform.position.ToString("R") + "|" +
                    collider.transform.rotation.ToString("R") + "|" + collider.bounds.ToString("R"))
                .OrderBy(value => value).ToArray();
        }

        private static Material WaterMaterial()
        {
            string path = RealisticNatureImport.Root + "/Materials/Water.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(0.06f, 0.14f, 0.13f, 0.82f));
                material.SetFloat("_Smoothness", 0.95f);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        /// <summary>Reports every non-retired asset that still depends directly on the old nature package.</summary>
        [MenuItem("Tools/ZZ/Environment/Audit Remaining Legacy Dependencies")]
        public static void AuditRemainingDependencies()
        {
            var remaining = new List<string>();
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") &&
                !AssetDatabase.IsValidFolder(path) && !IsLegacy(path)))
            {
                foreach (string dependency in AssetDatabase.GetDependencies(path, false).Where(IsLegacy))
                {
                    remaining.Add(path + "\t" + dependency);
                }
            }
            File.WriteAllLines(k_Report + "/remaining-legacy-dependencies.tsv", remaining);
            Debug.Log("LEGACY_DEPENDENCIES_REMAINING: " + remaining.Count);
        }

        /// <summary>Identifies remaining component references and terrain prototypes before final retirement.</summary>
        [MenuItem("Tools/ZZ/Environment/Inspect Remaining Legacy References")]
        public static void InspectRemainingReferences()
        {
            var report = new List<string>();
            string[] paths = File.ReadAllLines(k_Report + "/remaining-legacy-dependencies.tsv")
                .Select(line => line.Split('\t')[0]).Distinct().ToArray();
            foreach (string path in paths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasOpen = scene.isLoaded;
                if (!wasOpen)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                foreach (Component component in scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<Component>(true)).Where(component => component != null))
                {
                    using (var serialized = new SerializedObject(component))
                    {
                        SerializedProperty property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                                IsLegacy(AssetDatabase.GetAssetPath(property.objectReferenceValue)))
                            {
                                report.Add(path + "\t" + AnimationUtility.CalculateTransformPath(component.transform, null) +
                                    "\t" + component.GetType().FullName + "\t" + property.propertyPath + "\t" +
                                    AssetDatabase.GetAssetPath(property.objectReferenceValue));
                            }
                        }
                    }
                }
                foreach (Terrain terrain in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Terrain>(true)))
                {
                    TerrainData data = terrain.terrainData;
                    report.Add("TERRAIN\t" + data.name + "\tTrees=" + data.treeInstanceCount + "\tDetails=" +
                        data.detailPrototypes.Length + "\tLayers=" + string.Join(",", data.terrainLayers.Select(layer => layer.name)));
                }
                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            File.WriteAllLines(k_Report + "/remaining-reference-details.tsv", report);
        }

        /// <summary>Finishes terrain and lighting migration and refreshes the existing streaming caches.</summary>
        [MenuItem("Tools/ZZ/Environment/Finalize Realistic Level Art")]
        public static void FinalizeLevelArt()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before finalizing level art.");
            }
            string[] paths = File.ReadAllLines(k_Report + "/level-migration-progress.tsv")
                .Where(line => !line.StartsWith("COLLISION_PRESERVED"))
                .Select(line => line.Split('\t')[0]).Distinct().ToArray();
            var report = new List<string>();
            int cliffs = 0;
            foreach (string path in paths)
            {
                Scene previous = SceneManager.GetActiveScene();
                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasOpen = scene.isLoaded;
                if (!wasOpen)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                if (scene.isDirty)
                {
                    throw new InvalidOperationException("Save the open scene before finalization: " + path);
                }
                SceneManager.SetActiveScene(scene);
                try
                {
                    string[] before = CollisionSignature(scene);
                    foreach (Transform visual in scene.GetRootGameObjects().SelectMany(root =>
                        root.GetComponentsInChildren<Transform>(true)).Where(transform => transform.name == "Realistic Visual").ToArray())
                    {
                        string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.gameObject);
                        if (!prefab.Contains("coastal_cliff_02"))
                        {
                            continue;
                        }
                        Transform parent = visual.parent;
                        Bounds bounds = LocalBounds(parent, visual.GetComponentsInChildren<Renderer>(true));
                        string[] rocks = AssetDatabase.FindAssets("t:Prefab rock_moss_set_01", new[] { RealisticNatureImport.Root })
                            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value).ToArray();
                        GameObject replacement = AssetDatabase.LoadAssetAtPath<GameObject>(rocks[cliffs % rocks.Length]);
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(replacement, scene);
                        instance.transform.SetParent(parent, false);
                        Bounds source = LocalBounds(instance.transform, instance.GetComponentsInChildren<Renderer>(true));
                        Vector3 scale = new Vector3(bounds.size.x / source.size.x, bounds.size.y / source.size.y,
                            bounds.size.z / source.size.z);
                        instance.transform.localScale = scale;
                        instance.transform.localPosition = bounds.center - Vector3.Scale(source.center, scale);
                        instance.name = "Realistic Visual";
                        Object.DestroyImmediate(visual.gameObject);
                        cliffs++;
                    }
                    foreach (Transform transform in scene.GetRootGameObjects().SelectMany(root =>
                        root.GetComponentsInChildren<Transform>(true)).ToArray())
                    {
                        if (transform == null || !transform.name.StartsWith("Butterfly_"))
                        {
                            continue;
                        }
                        Component[] components = transform.GetComponentsInChildren<Component>(true);
                        if (components.All(component => component is Transform))
                        {
                            Object.DestroyImmediate(transform.gameObject);
                        }
                    }
                    RealisticTerrainImport.Replace(scene);
                    foreach (Volume volume in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true)))
                    {
                        if (IsLegacy(AssetDatabase.GetAssetPath(volume.sharedProfile)))
                        {
                            volume.sharedProfile = RealisticProfile();
                        }
                    }
                    if (IsLegacy(AssetDatabase.GetAssetPath(RenderSettings.skybox)))
                    {
                        string skyPath = RealisticNatureImport.Root + "/Materials/Sky.mat";
                        Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                        if (sky == null)
                        {
                            sky = new Material(Shader.Find("Skybox/Procedural"));
                            sky.SetFloat("_Exposure", 1.1f);
                            sky.SetColor("_SkyTint", new Color(0.42f, 0.47f, 0.52f));
                            AssetDatabase.CreateAsset(sky, skyPath);
                        }
                        RenderSettings.skybox = sky;
                    }
                    foreach (WorldLocationRendererManager manager in scene.GetRootGameObjects().SelectMany(root =>
                        root.GetComponentsInChildren<WorldLocationRendererManager>(true)))
                    {
                        manager.RefreshSceneObjects();
                        if (manager.MeshRenderers.Any(renderer => renderer == null))
                        {
                            throw new InvalidDataException("Streaming renderer cache contains missing references: " + path);
                        }
                    }
                    if (!before.SequenceEqual(CollisionSignature(scene)))
                    {
                        throw new InvalidDataException("Collision changed while finalizing: " + path);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.Add(path + "\tCollision=PASS\tStreamingCache=PASS");
                    File.WriteAllLines(k_Report + "/level-finalization.tsv", report);
                }
                finally
                {
                    SceneManager.SetActiveScene(previous);
                    if (!wasOpen)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            AssetDatabase.SaveAssets();
            AuditRemainingDependencies();
            Debug.Log("REALISTIC_LEVELS_FINALIZED: " + paths.Length + " scenes; solid cliff replacements=" + cliffs);
        }

        private static VolumeProfile RealisticProfile()
        {
            string path = RealisticNatureImport.Root + "/RealisticLighting.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                Tonemapping tone = profile.Add<Tonemapping>(true);
                tone.mode.value = TonemappingMode.ACES;
                AssetDatabase.AddObjectToAsset(tone, profile);
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.intensity.value = 0.12f;
                AssetDatabase.AddObjectToAsset(bloom, profile);
                EditorUtility.SetDirty(profile);
            }
            return profile;
        }

        /// <summary>Deletes the obsolete title images only when Unity finds no external asset dependencies.</summary>
        public static void DeleteUnusedHiFi()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before removing superseded art.");
            }
            Directory.CreateDirectory(k_Report);
            string[] images = Directory.Exists(k_HiFi) ? Directory.GetFiles(k_HiFi, "*.png") : Array.Empty<string>();
            if (images.Length == 0)
            {
                File.WriteAllText(k_Report + "/hifi-deleted.txt", "Already absent.\n");
                return;
            }
            string[] unexpected = Directory.GetFiles(k_HiFi, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".png") && !path.EndsWith(".meta")).ToArray();
            if (unexpected.Length != 0)
            {
                throw new InvalidOperationException("HiFi contains unexpected files; removal stopped.");
            }
            var references = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") &&
                !AssetDatabase.IsValidFolder(path) && !path.StartsWith(k_HiFi + "/"))
                .Where(path => AssetDatabase.GetDependencies(path, false).Any(dependency =>
                    dependency.StartsWith(k_HiFi + "/"))).ToArray();
            if (references.Length != 0)
            {
                File.WriteAllLines(k_Report + "/hifi-blocking-references.txt", references);
                throw new InvalidOperationException("HiFi still has Unity dependencies; removal stopped.");
            }
            File.WriteAllLines(k_Report + "/hifi-deleted.txt", images);
            if (!AssetDatabase.DeleteAsset(k_HiFi) || Directory.Exists(k_HiFi) || File.Exists(k_HiFi + ".meta"))
            {
                throw new IOException("Unity did not finish deleting the obsolete HiFi directory.");
            }
            Debug.Log("HIFI_REMOVED: " + images.Length + " images; zero external Unity dependencies.");
        }

        /// <summary>Lists each placed legacy prefab without changing the level or its gameplay components.</summary>
        [MenuItem("Tools/ZZ/Environment/Audit Legacy Placements")]
        public static void InspectPlacements()
        {
            Directory.CreateDirectory(k_Report);
            var report = new StringBuilder("Scene\tObject\tPrefab\tPosition\tBounds\tColliders\n");
            string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Game/Scenes/Levels" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (string path in scenes)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool wasOpen = scene.isLoaded;
                if (!wasOpen)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                foreach (Transform transform in scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<Transform>(true)))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject))
                    {
                        continue;
                    }
                    string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform.gameObject);
                    if (!prefab.Contains("/IdyllicFantasyNature/"))
                    {
                        continue;
                    }
                    Renderer[] renderers = transform.GetComponentsInChildren<Renderer>(true);
                    Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.zero);
                    foreach (Renderer renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    report.AppendLine(path + "\t" + AnimationUtility.CalculateTransformPath(transform, null) +
                        "\t" + prefab + "\t" + transform.position + "\t" + bounds.size + "\t" +
                        transform.GetComponentsInChildren<Collider>(true).Length);
                }
                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            File.WriteAllText(k_Report + "/legacy-placements.tsv", report.ToString());
            Debug.Log("LEGACY_PLACEMENTS_INSPECTED: " + scenes.Length + " scenes.");
        }

        /// <summary>Runs the authorized unused UI removal followed by a read-only level placement audit.</summary>
        public static void Prepare()
        {
            DeleteUnusedHiFi();
            InspectPlacements();
        }
    }
}
