using System;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZZ.Editor
{
    /// <summary>Builds and validates the EP107 networked Rune recovery point.</summary>
    public static class DeadSpotSystemSetup
    {
        private const string k_DeadSpotPrefabPath =
            "Assets/Resources/Effects/Dead Spot.prefab";
        private const string k_DeadSpotMaterialPath =
            "Assets/Data/Materials/Effects/Dead Spot.mat";
        private const string k_NetworkPrefabsPath =
            "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Tools/Elden/Configure Dead Spot System")]
        public static void ConfigureDeadSpotSystem()
        {
            EnsureFolder("Assets/Resources/Effects");
            EnsureFolder("Assets/Data/Materials/Effects");
            Material material = ConfigureMaterial();
            GameObject deadSpotPrefab = ConfigureDeadSpotPrefab(material);
            RegisterNetworkPrefab(deadSpotPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateDeadSpotSystem();
            Debug.Log(
                "[DeadSpotSystemSetup] Configured persistent Host-owned Rune recovery.");
        }

        [MenuItem("Tools/Elden/Validate Dead Spot System")]
        public static void ValidateDeadSpotSystem()
        {
            GameObject deadSpotPrefab = LoadRequiredAsset<GameObject>(
                k_DeadSpotPrefabPath);
            PickupRunesInteractable pickup =
                deadSpotPrefab.GetComponent<PickupRunesInteractable>();
            NetworkObject networkObject = deadSpotPrefab.GetComponent<NetworkObject>();
            Rigidbody rigidbody = deadSpotPrefab.GetComponent<Rigidbody>();
            SphereCollider sphereCollider = deadSpotPrefab.GetComponent<SphereCollider>();
            if (pickup == null ||
                networkObject == null ||
                rigidbody == null ||
                sphereCollider == null ||
                !sphereCollider.isTrigger ||
                !rigidbody.isKinematic ||
                rigidbody.useGravity ||
                deadSpotPrefab.layer != LayerMask.NameToLayer("Interactable"))
            {
                throw new InvalidOperationException(
                    "Dead Spot requires network, trigger, Rigidbody, and Interactable configuration.");
            }

            SerializedObject serializedPickup = new SerializedObject(pickup);
            if (GetRequiredProperty(serializedPickup, "m_hostOnlyInteractable")
                    .boolValue ||
                GetRequiredProperty(serializedPickup, "m_interactableCollider")
                    .objectReferenceValue != sphereCollider)
            {
                throw new InvalidOperationException(
                    "Dead Spot interaction authority or Collider reference is invalid.");
            }

            if (deadSpotPrefab.GetComponentInChildren<ParticleSystem>(true) == null ||
                Resources.Load<GameObject>("Effects/Dead Spot") != deadSpotPrefab)
            {
                throw new InvalidOperationException(
                    "Dead Spot presentation must be available through World effects Resources.");
            }

            ValidateNetworkPrefabRegistration(deadSpotPrefab);
            Debug.Log(
                "[DeadSpotSystemValidation] Prefab, interaction, VFX, and network registration are valid.");
        }

        private static Material ConfigureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                k_DeadSpotMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "A Lit shader is required for the Dead Spot material.");
                }

                material = new Material(shader)
                {
                    name = "Dead Spot"
                };
                AssetDatabase.CreateAsset(material, k_DeadSpotMaterialPath);
            }

            Color runeColor = new Color(0.82f, 0.68f, 0.22f, 1f);
            material.color = runeColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", runeColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", runeColor * 3f);
                material.EnableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject ConfigureDeadSpotPrefab(Material material)
        {
            GameObject root = new GameObject("Dead Spot");
            try
            {
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer < 0)
                {
                    throw new InvalidOperationException(
                        "The Interactable layer is required for Dead Spot.");
                }

                root.layer = interactableLayer;
                NetworkObject networkObject = root.AddComponent<NetworkObject>();
                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                SphereCollider sphereCollider = root.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 1.25f;
                PickupRunesInteractable pickup =
                    root.AddComponent<PickupRunesInteractable>();
                ConfigurePickup(pickup, sphereCollider);
                CreateCore(root.transform, material, interactableLayer);
                CreateParticles(root.transform, material, interactableLayer);
                CreateLight(root.transform, interactableLayer);

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    k_DeadSpotPrefabPath);
                if (savedPrefab == null || networkObject == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the Dead Spot prefab.");
                }

                return savedPrefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigurePickup(
            PickupRunesInteractable pickup,
            SphereCollider sphereCollider)
        {
            SerializedObject serializedPickup = new SerializedObject(pickup);
            GetRequiredProperty(serializedPickup, "m_interactableText")
                .stringValue = "Reclaim Runes";
            GetRequiredProperty(serializedPickup, "m_interactableCollider")
                .objectReferenceValue = sphereCollider;
            GetRequiredProperty(serializedPickup, "m_hostOnlyInteractable")
                .boolValue = false;
            GetRequiredProperty(
                    serializedPickup,
                    "m_shouldDisableColliderAfterInteraction")
                .boolValue = false;
            serializedPickup.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCore(
            Transform parent,
            Material material,
            int layer)
        {
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Rune Core";
            core.layer = layer;
            core.transform.SetParent(parent, false);
            core.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            core.transform.localScale = Vector3.one * 0.38f;
            UnityEngine.Object.DestroyImmediate(core.GetComponent<Collider>());
            core.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateParticles(
            Transform parent,
            Material material,
            int layer)
        {
            GameObject particlesObject = new GameObject("Rune Wisps");
            particlesObject.layer = layer;
            particlesObject.transform.SetParent(parent, false);
            particlesObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
            main.startColor = new Color(1f, 0.78f, 0.22f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 18f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.55f;
            ParticleSystem.VelocityOverLifetimeModule velocity =
                particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = 0.45f;

            ParticleSystemRenderer renderer =
                particlesObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void CreateLight(Transform parent, int layer)
        {
            GameObject lightObject = new GameObject("Rune Light");
            lightObject.layer = layer;
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            Light runeLight = lightObject.AddComponent<Light>();
            runeLight.type = LightType.Point;
            runeLight.color = new Color(1f, 0.68f, 0.18f, 1f);
            runeLight.intensity = 2.2f;
            runeLight.range = 3.5f;
            runeLight.shadows = LightShadows.None;
        }

        private static void RegisterNetworkPrefab(GameObject deadSpotPrefab)
        {
            NetworkPrefabsList prefabs = LoadRequiredAsset<NetworkPrefabsList>(
                k_NetworkPrefabsPath);
            SerializedObject serializedPrefabs = new SerializedObject(prefabs);
            SerializedProperty entries = GetRequiredProperty(
                serializedPrefabs,
                "List");
            for (int index = 0; index < entries.arraySize; index++)
            {
                if (entries.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("Prefab")
                        .objectReferenceValue == deadSpotPrefab)
                {
                    return;
                }
            }

            int newIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newEntry = entries.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("Override").intValue = 0;
            newEntry.FindPropertyRelative("Prefab").objectReferenceValue =
                deadSpotPrefab;
            newEntry.FindPropertyRelative("SourcePrefabToOverride")
                .objectReferenceValue = null;
            newEntry.FindPropertyRelative("SourceHashToOverride").longValue = 0L;
            newEntry.FindPropertyRelative("OverridingTargetPrefab")
                .objectReferenceValue = null;
            serializedPrefabs.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefabs);
        }

        private static void ValidateNetworkPrefabRegistration(
            GameObject deadSpotPrefab)
        {
            NetworkPrefabsList prefabs = LoadRequiredAsset<NetworkPrefabsList>(
                k_NetworkPrefabsPath);
            SerializedProperty entries = GetRequiredProperty(
                new SerializedObject(prefabs),
                "List");
            int matches = 0;
            for (int index = 0; index < entries.arraySize; index++)
            {
                if (entries.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("Prefab")
                        .objectReferenceValue == deadSpotPrefab)
                {
                    matches++;
                }
            }

            if (matches != 1)
            {
                throw new InvalidOperationException(
                    "Dead Spot must be registered exactly once as a network prefab.");
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(
                propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Could not find serialized property '{propertyName}'.");
            }

            return property;
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
            }

            return asset;
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            string currentPath = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string nextPath = $"{currentPath}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }
        }
    }
}
