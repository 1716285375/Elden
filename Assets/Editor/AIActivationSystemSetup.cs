using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP111 distance-based AI activation assets.</summary>
    public static class AIActivationSystemSetup
    {
        private const int k_BeaconLayer = 13;
        private const int k_BeaconDetectorLayer = 14;
        private const float k_DetectionRadius = 65f;
        private const string k_BeaconLayerName = "Beacon";
        private const string k_BeaconDetectorLayerName = "BeaconDetector";
        private const string k_DamageableCharacterLayerName =
            "Damageable Character";
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_AIActivationBeaconPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/AI Activation Beacon.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World AI Manager.prefab";

        /// <summary>Builds the layers, collision matrix, and shared activation prefabs.</summary>
        [MenuItem("Tools/ZZ/AI/Configure EP111 Activation System")]
        public static void ConfigureAIActivationSystem()
        {
            ConfigureLayers();
            ConfigureCollisionMatrix();
            AIActivationBeacon beaconPrefab = ConfigureActivationBeaconPrefab();
            ConfigurePlayerPrefab();
            ConfigureWorldAIManagerPrefab(beaconPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateAIActivationSystem();
            Debug.Log(
                "[AIActivationSystemSetup] Configured the 65m server activation gate.");
        }

        /// <summary>Throws when any EP111 layer, collider, or prefab reference is incomplete.</summary>
        [MenuItem("Tools/ZZ/AI/Validate EP111 Activation System")]
        public static void ValidateAIActivationSystem()
        {
            int beaconLayer = LayerMask.NameToLayer(k_BeaconLayerName);
            int detectorLayer = LayerMask.NameToLayer(k_BeaconDetectorLayerName);
            int damageableLayer =
                LayerMask.NameToLayer(k_DamageableCharacterLayerName);
            if (beaconLayer != k_BeaconLayer ||
                detectorLayer != k_BeaconDetectorLayer ||
                damageableLayer < 0)
            {
                throw new InvalidOperationException(
                    "The Beacon, BeaconDetector, or Damageable Character layer is missing.");
            }

            ValidateCollisionMatrix(beaconLayer, detectorLayer, damageableLayer);
            ValidateActivationBeaconPrefab(beaconLayer);
            ValidatePlayerPrefab(detectorLayer);
            ValidateWorldAIManagerPrefab();
            Debug.Log(
                "[AIActivationSystemValidation] Layers, collision matrix, and prefabs are valid.");
        }

        private static void ConfigureLayers()
        {
            UnityEngine.Object tagManager = AssetDatabase
                .LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")
                .FirstOrDefault();
            if (tagManager == null)
            {
                throw new InvalidOperationException("TagManager.asset is unavailable.");
            }

            SerializedObject serializedTagManager = new SerializedObject(tagManager);
            SerializedProperty layers = serializedTagManager.FindProperty("layers");
            SetLayer(layers, k_BeaconLayer, k_BeaconLayerName);
            SetLayer(layers, k_BeaconDetectorLayer, k_BeaconDetectorLayerName);
            serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tagManager);
        }

        private static void ConfigureCollisionMatrix()
        {
            int damageableLayer =
                LayerMask.NameToLayer(k_DamageableCharacterLayerName);
            for (int layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(
                    k_BeaconLayer,
                    layer,
                    layer != k_BeaconDetectorLayer);
                Physics.IgnoreLayerCollision(
                    k_BeaconDetectorLayer,
                    layer,
                    layer != k_BeaconLayer && layer != damageableLayer);
            }

            UnityEngine.Object physicsSettings = AssetDatabase
                .LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")
                .FirstOrDefault();
            if (physicsSettings != null)
            {
                EditorUtility.SetDirty(physicsSettings);
            }
        }

        private static AIActivationBeacon ConfigureActivationBeaconPrefab()
        {
            bool prefabExists = File.Exists(k_AIActivationBeaconPrefabPath);
            GameObject root = prefabExists
                ? PrefabUtility.LoadPrefabContents(k_AIActivationBeaconPrefabPath)
                : new GameObject("AI Activation Beacon");
            try
            {
                root.name = "AI Activation Beacon";
                root.layer = k_BeaconLayer;
                SphereCollider sphereCollider =
                    GetOrAddComponent<SphereCollider>(root);
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 0.75f;
                sphereCollider.center = Vector3.up * 0.75f;

                Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                GetOrAddComponent<AIActivationBeacon>(root);

                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        k_AIActivationBeaconPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save {k_AIActivationBeaconPrefabPath}.");
                }
            }
            finally
            {
                if (prefabExists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(
                    k_AIActivationBeaconPrefabPath)
                ?.GetComponent<AIActivationBeacon>();
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerManager player = GetRequiredComponent<PlayerManager>(root);
                Transform detectorTransform = root.transform.Find("Beacon Detector");
                if (detectorTransform == null)
                {
                    GameObject detectorObject = new GameObject("Beacon Detector");
                    detectorTransform = detectorObject.transform;
                    detectorTransform.SetParent(root.transform, false);
                }

                GameObject detector = detectorTransform.gameObject;
                detector.layer = k_BeaconDetectorLayer;
                detectorTransform.localPosition = Vector3.zero;
                detectorTransform.localRotation = Quaternion.identity;
                detectorTransform.localScale = Vector3.one;

                SphereCollider sphereCollider =
                    GetOrAddComponent<SphereCollider>(detector);
                sphereCollider.isTrigger = true;
                sphereCollider.radius = k_DetectionRadius;
                sphereCollider.center = Vector3.up;

                Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(detector);
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.constraints = RigidbodyConstraints.FreezeAll;

                BeaconDetector beaconDetector =
                    GetOrAddComponent<BeaconDetector>(detector);
                SerializedObject serializedDetector =
                    new SerializedObject(beaconDetector);
                serializedDetector.FindProperty("m_player").objectReferenceValue =
                    player;
                serializedDetector.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureWorldAIManagerPrefab(
            AIActivationBeacon beaconPrefab)
        {
            if (beaconPrefab == null)
            {
                throw new InvalidOperationException(
                    "The AI activation beacon prefab was not created.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldAIManagerPrefabPath);
            try
            {
                WorldAIManager worldAIManager =
                    GetRequiredComponent<WorldAIManager>(root);
                SerializedObject serializedManager =
                    new SerializedObject(worldAIManager);
                serializedManager
                    .FindProperty("m_aiActivationBeaconPrefab")
                    .objectReferenceValue = beaconPrefab;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_WorldAIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateCollisionMatrix(
            int beaconLayer,
            int detectorLayer,
            int damageableLayer)
        {
            for (int layer = 0; layer < 32; layer++)
            {
                bool beaconShouldCollide = layer == detectorLayer;
                bool detectorShouldCollide =
                    layer == beaconLayer || layer == damageableLayer;
                if (Physics.GetIgnoreLayerCollision(beaconLayer, layer) ==
                        beaconShouldCollide ||
                    Physics.GetIgnoreLayerCollision(detectorLayer, layer) ==
                        detectorShouldCollide)
                {
                    throw new InvalidOperationException(
                        $"The activation collision matrix is invalid at layer {layer}.");
                }
            }
        }

        private static void ValidateActivationBeaconPrefab(int beaconLayer)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_AIActivationBeaconPrefabPath);
            SphereCollider collider = prefab?.GetComponent<SphereCollider>();
            Rigidbody rigidbody = prefab?.GetComponent<Rigidbody>();
            if (prefab == null ||
                prefab.layer != beaconLayer ||
                prefab.GetComponent<AIActivationBeacon>() == null ||
                collider == null ||
                !collider.isTrigger ||
                rigidbody == null ||
                !rigidbody.isKinematic ||
                rigidbody.useGravity)
            {
                throw new InvalidOperationException(
                    "The AI activation beacon prefab is incomplete.");
            }
        }

        private static void ValidatePlayerPrefab(int detectorLayer)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PlayerPrefabPath);
            Transform detector = prefab?.transform.Find("Beacon Detector");
            SphereCollider collider = detector?.GetComponent<SphereCollider>();
            Rigidbody rigidbody = detector?.GetComponent<Rigidbody>();
            BeaconDetector beaconDetector = detector?.GetComponent<BeaconDetector>();
            if (detector == null ||
                detector.gameObject.layer != detectorLayer ||
                collider == null ||
                !collider.isTrigger ||
                !Mathf.Approximately(collider.radius, k_DetectionRadius) ||
                rigidbody == null ||
                !rigidbody.isKinematic ||
                rigidbody.useGravity ||
                beaconDetector?.Player != prefab.GetComponent<PlayerManager>())
            {
                throw new InvalidOperationException(
                    "The Player Beacon Detector is incomplete.");
            }
        }

        private static void ValidateWorldAIManagerPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_WorldAIManagerPrefabPath);
            WorldAIManager manager = prefab?.GetComponent<WorldAIManager>();
            if (manager?.AIActivationBeaconPrefab == null)
            {
                throw new InvalidOperationException(
                    "World AI Manager does not reference the activation beacon prefab.");
            }
        }

        private static void SetLayer(
            SerializedProperty layers,
            int layerIndex,
            string layerName)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);
            if (!string.IsNullOrEmpty(layer.stringValue) &&
                layer.stringValue != layerName)
            {
                throw new InvalidOperationException(
                    $"Layer {layerIndex} is already named {layer.stringValue}.");
            }

            layer.stringValue = layerName;
        }

        private static T GetOrAddComponent<T>(GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static T GetRequiredComponent<T>(GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"{target.name} requires {typeof(T).Name}.");
            }

            return component;
        }
    }
}
