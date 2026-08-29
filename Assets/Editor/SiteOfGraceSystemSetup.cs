using System;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP54 Site of Grace gameplay loop.</summary>
    public static class SiteOfGraceSystemSetup
    {
        private const int k_SiteOfGraceID = 54001;
        private const int k_SaveValidationID = 54999;
        private const string k_WorldScenePath =
            WorldScenePathLayout.MasterScenePath;
        private const string k_AnimatorControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_RestClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Emotes/" +
            "core_stand_to_sit_02.anim";
        private const string k_SiteModelPath =
            "Assets/Art/Models/Props/SM_Prop_Camp_Firepit_01.obj";
        private const string k_ParticleMaterialPath =
            "Assets/Art/Materials/VFX/Fire_Additive_Emission_Mat_01.mat";
        private const string k_RestSoundPath =
            "Assets/Art/Audio/SFX/General/SFX_Rest_At_Well_01.wav";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World AI Manager.prefab";
        private const string k_SiteObjectName = "First Step Site of Grace";
        private const string k_SpawnPointName = "Player Spawn Point";
        private const string k_InteractableLayerName = "Interactable";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_RestStateName = "Rest At Site Of Grace";

        [MenuItem("Tools/Elden/Configure Site Of Grace System")]
        public static void ConfigureSiteOfGraceSystem()
        {
            ConfigureRestAnimation();
            ConfigureWorldScene();
            AssetDatabase.SaveAssets();
            ValidateSiteOfGraceSystem();
            Debug.Log(
                "[SiteOfGraceSystemSetup] Configured persistent activation, " +
                "late-join presentation, rest recovery, and server AI reset.");
        }

        [MenuItem("Tools/Elden/Validate Site Of Grace System")]
        public static void ValidateSiteOfGraceSystem()
        {
            ValidateRuntimeContracts();
            ValidateSaveRoundTrip();
            ValidateRestAnimation();
            ValidateWorldScene();
            ValidateSpawnerPrefab();
            Debug.Log(
                "[SiteOfGraceSystemValidation] Save data, NetworkVariable, RPC, " +
                "rest animation, world presentation, and AI reset are valid.");
        }

        private static void ConfigureRestAnimation()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AnimatorControllerPath);
            AnimationClip clip = LoadRequiredAsset<AnimationClip>(k_RestClipPath);
            AnimatorStateMachine stateMachine = GetActionStateMachine(controller);
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            AnimatorState restState = FindState(stateMachine, k_RestStateName) ??
                stateMachine.AddState(
                    k_RestStateName,
                    new Vector3(1125f, 400f, 0f));
            restState.motion = clip;
            restState.speed = 1f;
            restState.writeDefaultValues = true;

            foreach (AnimatorStateTransition transition in
                restState.transitions.ToArray())
            {
                restState.RemoveTransition(transition);
            }

            AnimatorStateTransition returnTransition =
                restState.AddTransition(emptyState);
            returnTransition.hasExitTime = true;
            returnTransition.exitTime = 0.95f;
            returnTransition.hasFixedDuration = true;
            returnTransition.duration = 0.15f;
            returnTransition.interruptionSource =
                TransitionInterruptionSource.None;

            EditorUtility.SetDirty(restState);
            EditorUtility.SetDirty(returnTransition);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                Transform spawnPoint = FindTransform(scene, k_SpawnPointName) ??
                    throw new InvalidOperationException(
                        $"The World Scene is missing {k_SpawnPointName}.");
                GameObject siteRoot = FindRoot(scene, k_SiteObjectName);
                if (siteRoot == null)
                {
                    siteRoot = new GameObject(k_SiteObjectName);
                    SceneManager.MoveGameObjectToScene(siteRoot, scene);
                }

                ConfigureSiteObject(siteRoot, spawnPoint);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        "Could not save the World Scene Site of Grace.");
                }
            }
            finally
            {
                if (openedForSetup && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ConfigureSiteObject(
            GameObject root,
            Transform spawnPoint)
        {
            root.name = k_SiteObjectName;
            root.transform.SetPositionAndRotation(
                spawnPoint.position + spawnPoint.right * 4f +
                    spawnPoint.forward * 2f,
                spawnPoint.rotation);
            root.transform.localScale = Vector3.one;
            root.SetActive(true);

            GetOrAddComponent<NetworkObject>(root);
            Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

            int interactableLayer = LayerMask.NameToLayer(k_InteractableLayerName);
            if (interactableLayer < 0)
            {
                throw new InvalidOperationException(
                    "EP52 must configure the Interactable layer before EP54.");
            }

            Transform triggerTransform = GetOrCreateChild(
                root.transform,
                "Interactable Collider");
            triggerTransform.localPosition = Vector3.zero;
            triggerTransform.localRotation = Quaternion.identity;
            triggerTransform.localScale = Vector3.one;
            triggerTransform.gameObject.layer = interactableLayer;
            SphereCollider interactableCollider =
                GetOrAddComponent<SphereCollider>(triggerTransform.gameObject);
            interactableCollider.isTrigger = true;
            interactableCollider.center = Vector3.up;
            interactableCollider.radius = 3f;
            interactableCollider.enabled = true;

            ConfigureSiteModel(root.transform);
            ParticleSystem particles = ConfigureGraceParticles(root.transform);
            Light graceLight = ConfigureGraceLight(root.transform);
            AudioSource audioSource = GetOrAddComponent<AudioSource>(root);
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 24f;

            SiteOfGraceInteractable site =
                GetOrAddComponent<SiteOfGraceInteractable>(root);
            SerializedObject serializedSite = new SerializedObject(site);
            SetString(
                serializedSite,
                "m_interactableText",
                "Restore Site of Grace");
            SetObjectReference(
                serializedSite,
                "m_interactableCollider",
                interactableCollider);
            SetBoolean(serializedSite, "m_hostOnlyInteractable", false);
            SetBoolean(
                serializedSite,
                "m_shouldDisableColliderAfterInteraction",
                false);
            SetInteger(serializedSite, "m_siteOfGraceID", k_SiteOfGraceID);
            SetObjectArray(
                serializedSite,
                "m_graceParticles",
                new UnityEngine.Object[] { particles });
            SetObjectReference(serializedSite, "m_graceLight", graceLight);
            SetObjectReference(serializedSite, "m_audioSource", audioSource);
            AudioClip restSound = LoadRequiredAsset<AudioClip>(k_RestSoundPath);
            SetObjectReference(serializedSite, "m_activationSound", restSound);
            SetObjectReference(serializedSite, "m_restSound", restSound);
            AnimationClip restClip = LoadRequiredAsset<AnimationClip>(k_RestClipPath);
            SetFloat(
                serializedSite,
                "m_restDuration",
                Mathf.Max(3f, restClip.length + 0.2f));
            SetFloat(serializedSite, "m_maxInteractionDistance", 5f);
            serializedSite.ApplyModifiedPropertiesWithoutUndo();

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            graceLight.enabled = false;
            interactableCollider.isTrigger = true;
            interactableCollider.enabled = true;
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(rigidbody);
            EditorUtility.SetDirty(interactableCollider);
            EditorUtility.SetDirty(particles);
            EditorUtility.SetDirty(graceLight);
            EditorUtility.SetDirty(audioSource);
            EditorUtility.SetDirty(site);
        }

        private static void ConfigureSiteModel(Transform parent)
        {
            Transform modelTransform = parent.Find("Site of Grace Model");
            if (modelTransform == null)
            {
                GameObject modelAsset = LoadRequiredAsset<GameObject>(
                    k_SiteModelPath);
                GameObject modelInstance = PrefabUtility.InstantiatePrefab(
                    modelAsset,
                    parent) as GameObject;
                if (modelInstance == null)
                {
                    throw new InvalidOperationException(
                        "Could not instantiate the Site of Grace model.");
                }

                modelInstance.name = "Site of Grace Model";
                modelTransform = modelInstance.transform;
            }

            modelTransform.localPosition = Vector3.zero;
            modelTransform.localRotation = Quaternion.identity;
            modelTransform.localScale = Vector3.one * 0.65f;
        }

        private static ParticleSystem ConfigureGraceParticles(Transform parent)
        {
            Transform particleTransform = GetOrCreateChild(
                parent,
                "Grace Particles");
            particleTransform.localPosition = Vector3.up * 0.35f;
            particleTransform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            particleTransform.localScale = Vector3.one;
            ParticleSystem particles = GetOrAddComponent<ParticleSystem>(
                particleTransform.gameObject);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 2f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.66f, 0.15f, 0.9f),
                new Color(1f, 0.94f, 0.55f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 28f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.28f;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.2f;
            noise.frequency = 0.65f;

            ParticleSystemRenderer particleRenderer =
                GetOrAddComponent<ParticleSystemRenderer>(
                    particleTransform.gameObject);
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = LoadRequiredAsset<Material>(
                k_ParticleMaterialPath);
            return particles;
        }

        private static Light ConfigureGraceLight(Transform parent)
        {
            Transform lightTransform = GetOrCreateChild(parent, "Grace Light");
            lightTransform.localPosition = Vector3.up * 0.75f;
            lightTransform.localRotation = Quaternion.identity;
            lightTransform.localScale = Vector3.one;
            Light graceLight = GetOrAddComponent<Light>(lightTransform.gameObject);
            graceLight.type = LightType.Point;
            graceLight.color = new Color(1f, 0.72f, 0.24f, 1f);
            graceLight.intensity = 4f;
            graceLight.range = 7f;
            graceLight.shadows = LightShadows.Soft;
            graceLight.enabled = false;
            return graceLight;
        }

        private static void ValidateRuntimeContracts()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            BindingFlags privateInstance =
                BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo serverRpc = typeof(SiteOfGraceInteractable).GetMethod(
                "RequestSiteOfGraceInteractionServerRpc",
                privateInstance);
            ServerRpcAttribute serverRpcAttribute =
                serverRpc?.GetCustomAttribute<ServerRpcAttribute>();
            FieldInfo activationVariable = typeof(SiteOfGraceInteractable).GetField(
                "m_isActivated",
                privateInstance);
            if (!typeof(Interactable).IsAssignableFrom(
                    typeof(SiteOfGraceInteractable)) ||
                serverRpcAttribute == null ||
                serverRpcAttribute.RequireOwnership ||
                activationVariable?.FieldType != typeof(NetworkVariable<bool>) ||
                typeof(WorldAIManager).GetMethod(
                    nameof(WorldAIManager.ResetAllCharacters),
                    publicInstance) == null ||
                typeof(AICharacterSpawner).GetMethod(
                    nameof(AICharacterSpawner.ResetSpawnState),
                    publicInstance) == null ||
                !Enum.IsDefined(
                    typeof(CharacterActionAnimation),
                    CharacterActionAnimation.RestAtSiteOfGrace))
            {
                throw new InvalidOperationException(
                    "The Site of Grace network or AI reset contracts are incomplete.");
            }
        }

        private static void ValidateSaveRoundTrip()
        {
            CharacterSaveData saveData = new CharacterSaveData();
            if (saveData.IsSiteOfGraceActivated(k_SaveValidationID) ||
                !saveData.SetSiteOfGraceActivated(k_SaveValidationID, true) ||
                !saveData.IsSiteOfGraceActivated(k_SaveValidationID) ||
                saveData.SetSiteOfGraceActivated(k_SaveValidationID, true))
            {
                throw new InvalidOperationException(
                    "Site of Grace activation must be keyed and idempotent.");
            }

            string json = JsonUtility.ToJson(saveData);
            CharacterSaveData restoredData =
                JsonUtility.FromJson<CharacterSaveData>(json);
            if (restoredData == null ||
                !restoredData.IsSiteOfGraceActivated(k_SaveValidationID))
            {
                throw new InvalidOperationException(
                    "Site of Grace activation must survive a JSON save round trip.");
            }
        }

        private static void ValidateRestAnimation()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AnimatorControllerPath);
            AnimationClip restClip = LoadRequiredAsset<AnimationClip>(k_RestClipPath);
            AnimatorState restState = GetRequiredState(
                GetActionStateMachine(controller),
                k_RestStateName);
            if (restState.motion != restClip ||
                restState.transitions.Length != 1 ||
                restState.transitions[0].destinationState?.name != k_EmptyStateName)
            {
                throw new InvalidOperationException(
                    "The rest action must play its authored clip and return to Empty.");
            }
        }

        private static void ValidateWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject root = FindRoot(scene, k_SiteObjectName) ??
                    throw new InvalidOperationException(
                        "The World Scene is missing its Site of Grace.");
                SiteOfGraceInteractable site =
                    GetRequiredComponent<SiteOfGraceInteractable>(root);
                Rigidbody rigidbody = GetRequiredComponent<Rigidbody>(root);
                SphereCollider trigger = root.transform
                    .Find("Interactable Collider")
                    ?.GetComponent<SphereCollider>();
                Transform spawnPoint = FindTransform(scene, k_SpawnPointName);
                SerializedObject serializedSite = new SerializedObject(site);
                SerializedProperty particleProperty = GetRequiredProperty(
                    serializedSite,
                    "m_graceParticles");
                bool hasPresentation = particleProperty.arraySize > 0 &&
                    GetRequiredProperty(serializedSite, "m_graceLight")
                        .objectReferenceValue != null &&
                    GetRequiredProperty(serializedSite, "m_activationSound")
                        .objectReferenceValue != null &&
                    GetRequiredProperty(serializedSite, "m_restSound")
                        .objectReferenceValue != null;
                bool isNearby = spawnPoint != null &&
                    Vector3.Distance(root.transform.position, spawnPoint.position) <= 6f;
                if (!root.activeSelf ||
                    root.GetComponent<NetworkObject>() == null ||
                    !rigidbody.isKinematic ||
                    rigidbody.useGravity ||
                    trigger == null ||
                    !trigger.isTrigger ||
                    !trigger.enabled ||
                    site.SiteOfGraceID != k_SiteOfGraceID ||
                    site.IsHostOnlyInteractable ||
                    !hasPresentation ||
                    !isNearby)
                {
                    throw new InvalidOperationException(
                        "The Site of Grace scene object is not fully configured.");
                }
            }
            finally
            {
                if (openedForValidation)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateSpawnerPrefab()
        {
            GameObject managerPrefab = LoadRequiredAsset<GameObject>(
                k_WorldAIManagerPrefabPath);
            AICharacterSpawner[] spawners = managerPrefab
                .GetComponentsInChildren<AICharacterSpawner>(true);
            if (managerPrefab.GetComponent<WorldAIManager>() == null ||
                spawners.Length == 0 ||
                spawners.Any(spawner => spawner == null))
            {
                throw new InvalidOperationException(
                    "World AI reset requires registered character spawners.");
            }
        }

        private static AnimatorStateMachine GetActionStateMachine(
            AnimatorController controller)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == k_ActionLayerName)
                {
                    return layer.stateMachine;
                }
            }

            throw new InvalidOperationException(
                $"Animator is missing the {k_ActionLayerName} layer.");
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName);
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return FindState(stateMachine, stateName) ??
                throw new InvalidOperationException(
                    $"Animator is missing state {stateName}.");
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == objectName);
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == objectName);
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string objectName)
        {
            Transform child = parent.Find(objectName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(objectName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            return gameObject.GetComponent<T>() ??
                throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetRequiredProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
        }

        private static void SetObjectArray(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object[] values)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
        }
    }
}
