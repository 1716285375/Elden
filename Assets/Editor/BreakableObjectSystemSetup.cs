using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the EP127-128 networked breakable crate.</summary>
    public static class BreakableObjectSystemSetup
    {
        private const int k_BreakableObjectLayer = 16;
        private const int k_BrokenObjectLayer = 17;
        private const string k_BreakableObjectLayerName = "Breakable Object";
        private const string k_BrokenObjectLayerName = "Broken Object";
        private const string k_PrefabFolder =
            "Assets/_Game/Prefabs/World/Objects/Breakables";
        private const string k_BrokenPrefabPath =
            k_PrefabFolder + "/Wooden Crate Broken.prefab";
        private const string k_BreakablePrefabPath =
            k_PrefabFolder + "/Wooden Crate Breakable.prefab";
        private const string k_WholeModelPath =
            "Assets/_Game/Art/Environment/Props/Models/SM_Prop_Crate_Wood_05.obj";
        private const string k_FragmentModelPathPrefix =
            "Assets/_Game/Art/Environment/Props/Models/SM_Prop_Crate_Wood_05_Breakable_";
        private static readonly string k_AreaScenePath =
            WorldScenePathLayout.GetScenePath(0, 0);
        private const string k_SceneObjectName = "Breakable Wooden Crate";

        private static readonly string[] s_breakSoundPaths =
        {
            "Assets/_Game/Audio/SFX/Environment/SFX_Wood_Break_00.wav",
            "Assets/_Game/Audio/SFX/Environment/SFX_Wood_Break_01.wav",
            "Assets/_Game/Audio/SFX/Environment/SFX_Wood_Break_02.wav",
            "Assets/_Game/Audio/SFX/Environment/SFX_Wood_Break_03.wav"
        };

        /// <summary>Configures layers, prefabs, collisions, and one additive Scene example.</summary>
        [MenuItem("Tools/Elden/Configure Breakable Object System")]
        public static void ConfigureBreakableObjectSystem()
        {
            try
            {
                ConfigureLayers();
                ConfigureLayerCollisions();
                EnsureAssetFolder(k_PrefabFolder);
                GameObject brokenPrefab = ConfigureBrokenPrefab();
                GameObject breakablePrefab = ConfigureBreakablePrefab(
                    brokenPrefab);
                ConfigureAreaScene(breakablePrefab);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ValidateBreakableObjectSystem();
                Debug.Log(
                    "[BreakableObjectSystemSetup] Configured EP127-128 " +
                    "predicted network breakage, fragments, sound, and " +
                    "additive Scene placement.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        /// <summary>Validates the reusable crate and additive Scene placement.</summary>
        [MenuItem("Tools/Elden/Validate Breakable Object System")]
        public static void ValidateBreakableObjectSystem()
        {
            ValidateLayers();
            ValidateBreakablePrefab();
            ValidateBrokenPrefab();
            ValidateAreaScene();
            Debug.Log(
                "[BreakableObjectSystemValidation] EP127-128 breakable " +
                "object configuration is valid.");
        }

        private static void ConfigureLayers()
        {
            UnityEngine.Object tagManager = AssetDatabase
                .LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")
                .FirstOrDefault();
            if (tagManager == null)
            {
                throw new InvalidOperationException(
                    "Could not load ProjectSettings/TagManager.asset.");
            }

            SerializedObject serializedTagManager =
                new SerializedObject(tagManager);
            SerializedProperty layers =
                serializedTagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(k_BreakableObjectLayer).stringValue =
                k_BreakableObjectLayerName;
            layers.GetArrayElementAtIndex(k_BrokenObjectLayer).stringValue =
                k_BrokenObjectLayerName;
            serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tagManager);
        }

        private static void ConfigureLayerCollisions()
        {
            HashSet<int> breakableCollisionLayers = new()
            {
                0,
                8,
                9,
                10,
                12,
                k_BreakableObjectLayer,
                k_BrokenObjectLayer
            };
            HashSet<int> brokenCollisionLayers = new()
            {
                0,
                9,
                15,
                k_BreakableObjectLayer,
                k_BrokenObjectLayer
            };

            for (int layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(
                    k_BreakableObjectLayer,
                    layer,
                    !breakableCollisionLayers.Contains(layer));
                Physics.IgnoreLayerCollision(
                    k_BrokenObjectLayer,
                    layer,
                    !brokenCollisionLayers.Contains(layer));
            }
        }

        private static GameObject ConfigureBrokenPrefab()
        {
            GameObject brokenRoot = new GameObject("Wooden Crate Broken");
            SetLayerRecursively(brokenRoot, k_BrokenObjectLayer);
            try
            {
                for (int fragmentIndex = 0;
                    fragmentIndex <= 6;
                    fragmentIndex++)
                {
                    string fragmentPath =
                        $"{k_FragmentModelPathPrefix}{fragmentIndex:00}.obj";
                    GameObject fragmentAsset =
                        AssetDatabase.LoadAssetAtPath<GameObject>(fragmentPath);
                    if (fragmentAsset == null)
                    {
                        throw new InvalidOperationException(
                            $"Missing crate fragment model: {fragmentPath}");
                    }

                    GameObject fragment = UnityEngine.Object.Instantiate(
                        fragmentAsset,
                        brokenRoot.transform);
                    fragment.name = $"Crate Fragment {fragmentIndex:00}";
                    fragment.transform.localPosition = Vector3.zero;
                    fragment.transform.localRotation = Quaternion.identity;
                    SetLayerRecursively(fragment, k_BrokenObjectLayer);
                    ConfigureFragmentPhysics(fragment);
                }

                return PrefabUtility.SaveAsPrefabAsset(
                    brokenRoot,
                    k_BrokenPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(brokenRoot);
            }
        }

        private static void ConfigureFragmentPhysics(GameObject fragment)
        {
            foreach (MeshFilter meshFilter in
                fragment.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshCollider meshCollider =
                    meshFilter.GetComponent<MeshCollider>();
                if (meshCollider == null)
                {
                    meshCollider = meshFilter.gameObject
                        .AddComponent<MeshCollider>();
                }

                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = true;
            }

            Rigidbody rigidbody = fragment.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = fragment.AddComponent<Rigidbody>();
            }

            rigidbody.mass = 0.5f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
        }

        private static GameObject ConfigureBreakablePrefab(
            GameObject brokenPrefab)
        {
            GameObject breakableRoot = new GameObject("Wooden Crate Breakable");
            breakableRoot.layer = k_BreakableObjectLayer;
            try
            {
                GameObject wholeAsset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(k_WholeModelPath);
                if (wholeAsset == null)
                {
                    throw new InvalidOperationException(
                        $"Missing whole crate model: {k_WholeModelPath}");
                }

                GameObject wholeModel = UnityEngine.Object.Instantiate(
                    wholeAsset,
                    breakableRoot.transform);
                wholeModel.name = "Whole Crate";
                wholeModel.transform.localPosition = Vector3.zero;
                wholeModel.transform.localRotation = Quaternion.identity;
                SetLayerRecursively(wholeModel, k_BreakableObjectLayer);

                Bounds localBounds = CalculateLocalRendererBounds(
                    breakableRoot.transform,
                    wholeModel.GetComponentsInChildren<Renderer>(true));
                BoxCollider physicalCollider =
                    breakableRoot.AddComponent<BoxCollider>();
                physicalCollider.center = localBounds.center;
                physicalCollider.size = localBounds.size;
                BoxCollider triggerCollider =
                    breakableRoot.AddComponent<BoxCollider>();
                triggerCollider.center = localBounds.center;
                triggerCollider.size = localBounds.size +
                    new Vector3(0.3f, 0.2f, 0.3f);
                triggerCollider.isTrigger = true;

                Rigidbody rigidbody = breakableRoot.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                AudioSource audioSource =
                    breakableRoot.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.maxDistance = 18f;
                breakableRoot.AddComponent<NetworkObject>();
                BreakableObject breakableObject =
                    breakableRoot.AddComponent<BreakableObject>();
                ConfigureBreakableComponent(
                    breakableObject,
                    brokenPrefab,
                    wholeModel.GetComponentsInChildren<Renderer>(true),
                    new Collider[] { physicalCollider, triggerCollider });

                return PrefabUtility.SaveAsPrefabAsset(
                    breakableRoot,
                    k_BreakablePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(breakableRoot);
            }
        }

        private static void ConfigureBreakableComponent(
            BreakableObject breakableObject,
            GameObject brokenPrefab,
            Renderer[] renderers,
            Collider[] colliders)
        {
            SerializedObject serializedBreakable =
                new SerializedObject(breakableObject);
            SetObjectReferenceArray(
                serializedBreakable.FindProperty("m_wholeObjectRenderers"),
                renderers);
            SetObjectReferenceArray(
                serializedBreakable.FindProperty("m_wholeObjectColliders"),
                colliders);
            serializedBreakable.FindProperty("m_brokenObjectPrefab")
                .objectReferenceValue = brokenPrefab;
            SetObjectReferenceArray(
                serializedBreakable.FindProperty("m_brokenSoundEffects"),
                s_breakSoundPaths
                    .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                    .Where(sound => sound != null)
                    .ToArray());
            serializedBreakable.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(breakableObject);
        }

        private static void ConfigureAreaScene(GameObject breakablePrefab)
        {
            Scene scene = SceneManager.GetSceneByPath(k_AreaScenePath);
            bool openedBySetup = !scene.IsValid() || !scene.isLoaded;
            if (openedBySetup)
            {
                scene = EditorSceneManager.OpenScene(
                    k_AreaScenePath,
                    OpenSceneMode.Additive);
            }
            else if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    $"Refusing to modify dirty Scene: {k_AreaScenePath}");
            }

            try
            {
                GameObject sceneObject = scene.GetRootGameObjects()
                    .FirstOrDefault(root => root.name == k_SceneObjectName);
                if (sceneObject == null)
                {
                    sceneObject = (GameObject)PrefabUtility.InstantiatePrefab(
                        breakablePrefab,
                        scene);
                    sceneObject.name = k_SceneObjectName;
                }

                sceneObject.transform.SetPositionAndRotation(
                    new Vector3(0f, 0.75f, 3f),
                    Quaternion.identity);
                EditorUtility.SetDirty(sceneObject);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedBySetup && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateLayers()
        {
            if (LayerMask.LayerToName(k_BreakableObjectLayer) !=
                    k_BreakableObjectLayerName ||
                LayerMask.LayerToName(k_BrokenObjectLayer) !=
                    k_BrokenObjectLayerName)
            {
                throw new InvalidOperationException(
                    "Breakable Object or Broken Object layer is missing.");
            }
        }

        private static void ValidateBreakablePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BreakablePrefabPath);
            BreakableObject breakable = prefab?.GetComponent<BreakableObject>();
            BoxCollider[] colliders = prefab?.GetComponents<BoxCollider>();
            AudioSource audioSource = prefab?.GetComponent<AudioSource>();
            Rigidbody rigidbody = prefab?.GetComponent<Rigidbody>();
            if (prefab == null ||
                prefab.layer != k_BreakableObjectLayer ||
                breakable == null ||
                prefab.GetComponent<NetworkObject>() == null ||
                rigidbody == null ||
                !rigidbody.isKinematic ||
                rigidbody.constraints != RigidbodyConstraints.FreezeAll ||
                audioSource == null ||
                !Mathf.Approximately(audioSource.spatialBlend, 1f) ||
                colliders == null ||
                colliders.Length < 2 ||
                colliders.Count(collider => collider.isTrigger) != 1)
            {
                throw new InvalidOperationException(
                    "Breakable crate prefab configuration is incomplete.");
            }

            SerializedObject serializedBreakable =
                new SerializedObject(breakable);
            if (serializedBreakable.FindProperty("m_brokenObjectPrefab")
                    .objectReferenceValue == null ||
                serializedBreakable.FindProperty("m_brokenSoundEffects")
                    .arraySize == 0)
            {
                throw new InvalidOperationException(
                    "Breakable crate fragments or sounds are missing.");
            }
        }

        private static void ValidateBrokenPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BrokenPrefabPath);
            Rigidbody[] rigidbodies =
                prefab?.GetComponentsInChildren<Rigidbody>(true);
            MeshCollider[] colliders =
                prefab?.GetComponentsInChildren<MeshCollider>(true);
            if (prefab == null ||
                prefab.layer != k_BrokenObjectLayer ||
                rigidbodies == null ||
                rigidbodies.Length != 7 ||
                colliders == null ||
                colliders.Length < 7 ||
                colliders.Any(collider => !collider.convex))
            {
                throw new InvalidOperationException(
                    "Broken crate fragment physics is incomplete.");
            }
        }

        private static void ValidateAreaScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_AreaScenePath);
            bool openedByValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedByValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    k_AreaScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject sceneObject = scene.GetRootGameObjects()
                    .FirstOrDefault(root => root.name == k_SceneObjectName);
                if (sceneObject?.GetComponent<BreakableObject>() == null ||
                    sceneObject.GetComponent<NetworkObject>() == null)
                {
                    throw new InvalidOperationException(
                        "The additive Scene breakable object is missing.");
                }
            }
            finally
            {
                if (openedByValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static Bounds CalculateLocalRendererBounds(
            Transform root,
            IEnumerable<Renderer> renderers)
        {
            bool hasBounds = false;
            Bounds localBounds = default;
            foreach (Renderer renderer in renderers)
            {
                Bounds rendererBounds = renderer.bounds;
                Vector3 localCenter = root.InverseTransformPoint(
                    rendererBounds.center);
                Vector3 localSize = root.InverseTransformVector(
                    rendererBounds.size);
                Bounds candidate = new Bounds(
                    localCenter,
                    new Vector3(
                        Mathf.Abs(localSize.x),
                        Mathf.Abs(localSize.y),
                        Mathf.Abs(localSize.z)));
                if (!hasBounds)
                {
                    localBounds = candidate;
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(candidate);
                }
            }

            if (!hasBounds)
            {
                throw new InvalidOperationException(
                    "Whole crate model has no Renderers.");
            }

            return localBounds;
        }

        private static void SetObjectReferenceArray<T>(
            SerializedProperty arrayProperty,
            IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            arrayProperty.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                arrayProperty.GetArrayElementAtIndex(index)
                    .objectReferenceValue = values[index];
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform child in
                root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)
                ?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureAssetFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
    }
}
