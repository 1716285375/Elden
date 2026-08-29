using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the EP87-88 flask data and upper-body flow.</summary>
    public static class FlaskSystemSetup
    {
        private const string k_ControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_DatabasePrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World Item Database.prefab";
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_MainMenuScenePath =
            WorldScenePathLayout.MainMenuScenePath;
        private const string k_HealthFlaskPath =
            "Assets/_Game/Data/Items/Quick Slot Items/Flask of Crimson Tears.asset";
        private const string k_FocusFlaskPath =
            "Assets/_Game/Data/Items/Quick Slot Items/Flask of Cerulean Tears.asset";
        private const string k_HealthModelPath =
            "Assets/_Game/Prefabs/Items/Health Flask.prefab";
        private const string k_FocusModelPath =
            "Assets/_Game/Prefabs/Items/Focus Flask.prefab";
        private const string k_EmptyModelPath =
            "Assets/_Game/Prefabs/Items/Empty Flask.prefab";
        private const string k_HealthVFXPath =
            "Assets/_Game/Prefabs/Effects/Health Flask VFX.prefab";
        private const string k_FocusVFXPath =
            "Assets/_Game/Prefabs/Effects/Focus Flask VFX.prefab";
        private const string k_HealthIconPath =
            "Assets/_Game/Art/UI/Icons/Health Flask Icon.asset";
        private const string k_FocusIconPath =
            "Assets/_Game/Art/UI/Icons/Focus Flask Icon.asset";
        private const string k_UpperBodyLayerName = "Upper Body Override";
        private const string k_ChuggingParameter = "isChuggingFlask";
        private const int k_HealthFlaskID = 14;
        private const int k_FocusFlaskID = 15;

        private const string k_DrinkStartClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Drink Start.anim";
        private const string k_Drink01ClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Drink 01.anim";
        private const string k_Drink02ClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Drink 02.anim";
        private const string k_DrinkEndClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Drink End.anim";
        private const string k_EmptyFlaskClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Empty Flask.anim";

        [MenuItem("Tools/Elden/Configure Flask System")]
        public static void ConfigureFlaskSystem()
        {
            EnsureFolders();
            ConfigureAnimationClips();
            ConfigureAnimator();
            ConfigurePresentationAssets();
            ConfigureItems();
            ConfigureDatabase();
            ConfigurePlayer();
            ConfigureWorldManagers();
            AssetDatabase.SaveAssets();
            ValidateFlaskSystem();
            Debug.Log(
                "[FlaskSystemSetup] Configured EP87-88 quick-slot data, " +
                "chained drinking, feedback, player defaults, and network-ready IDs.");
        }

        [MenuItem("Tools/Elden/Validate Flask System")]
        public static void ValidateFlaskSystem()
        {
            ValidateItems();
            ValidateAnimator();
            ValidatePrefabs();
            Debug.Log(
                "[FlaskSystemValidation] Flask IDs, models, icons, Animator flow, " +
                "success events, and player defaults are valid.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game/Art/Characters/Shared/Humanoid/Animations", "Flasks");
            EnsureFolder("Assets/_Game/Data/Items", "Quick Slot Items");
            EnsureFolder("Assets/_Game/Prefabs", "Quick Slot Items");
            EnsureFolder("Assets/_Game/Prefabs", "Effects");
            EnsureFolder("Assets/_Game/Art/UI", "Icons");
            EnsureFolder("Assets/_Game/Art/Shared/Materials", "Flasks");
        }

        private static void ConfigureAnimationClips()
        {
            ConfigureTimingClip(k_DrinkStartClipPath, 0.55f, null);
            ConfigureTimingClip(
                k_Drink01ClipPath,
                0.8f,
                "SuccessfullyUseQuickSlotItem");
            ConfigureTimingClip(
                k_Drink02ClipPath,
                0.8f,
                "SuccessfullyUseQuickSlotItem");
            ConfigureTimingClip(k_DrinkEndClipPath, 0.45f, null);
            ConfigureTimingClip(k_EmptyFlaskClipPath, 0.55f, null);
        }

        private static void ConfigureTimingClip(
            string assetPath,
            float duration,
            string eventFunction)
        {
            AnimationClip clip = LoadOrCreateAnimationClip(assetPath);
            clip.frameRate = 30f;
            clip.ClearCurves();
            clip.SetCurve(
                "__FlaskTiming__",
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Constant(0f, duration, 0f));
            AnimationUtility.SetAnimationEvents(
                clip,
                string.IsNullOrEmpty(eventFunction)
                    ? Array.Empty<AnimationEvent>()
                    : new[]
                    {
                        new AnimationEvent
                        {
                            functionName = eventFunction,
                            time = duration * 0.45f,
                            messageOptions = SendMessageOptions.RequireReceiver
                        }
                    });
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            EnsureBoolParameter(controller, k_ChuggingParameter);
            AnimatorStateMachine stateMachine = controller.layers
                .First(layer => layer.name == k_UpperBodyLayerName)
                .stateMachine;
            AnimatorState empty = GetRequiredState(stateMachine, "Empty");
            AnimatorState drinkStart = ConfigureState(
                stateMachine,
                "Drink Start",
                k_DrinkStartClipPath,
                new Vector3(900f, 250f, 0f));
            AnimatorState drink01 = ConfigureState(
                stateMachine,
                "Drink 01",
                k_Drink01ClipPath,
                new Vector3(1140f, 160f, 0f));
            AnimatorState drink02 = ConfigureState(
                stateMachine,
                "Drink 02",
                k_Drink02ClipPath,
                new Vector3(1380f, 160f, 0f));
            AnimatorState drinkEnd = ConfigureState(
                stateMachine,
                "Drink End",
                k_DrinkEndClipPath,
                new Vector3(1620f, 250f, 0f));
            AnimatorState emptyFlask = ConfigureState(
                stateMachine,
                "Empty Flask",
                k_EmptyFlaskClipPath,
                new Vector3(1140f, 410f, 0f));

            ClearTransitions(drinkStart);
            ClearTransitions(drink01);
            ClearTransitions(drink02);
            ClearTransitions(drinkEnd);
            ClearTransitions(emptyFlask);
            AddExitTransition(drinkStart, drink01, 0.9f, 0.05f);
            AddBoolTransition(
                drink01,
                drink02,
                k_ChuggingParameter,
                true,
                0.82f);
            AddBoolTransition(
                drink01,
                drinkEnd,
                k_ChuggingParameter,
                false,
                0.82f);
            AddBoolTransition(
                drink02,
                drink01,
                k_ChuggingParameter,
                true,
                0.82f);
            AddBoolTransition(
                drink02,
                drinkEnd,
                k_ChuggingParameter,
                false,
                0.82f);
            AddExitTransition(drinkEnd, empty, 0.9f, 0.05f);
            AddExitTransition(emptyFlask, empty, 0.9f, 0.05f);

            AddBehaviourIfMissing<ResetUpperBodyAction>(empty);
            AddBehaviourIfMissing<ResetIsChugging>(drink01);
            AddBehaviourIfMissing<ResetIsChugging>(drink02);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigurePresentationAssets()
        {
            Material healthMaterial = ConfigureMaterial(
                "Assets/_Game/Art/Shared/Materials/Health Flask.mat",
                new Color(0.65f, 0.05f, 0.03f));
            Material focusMaterial = ConfigureMaterial(
                "Assets/_Game/Art/Shared/Materials/Focus Flask.mat",
                new Color(0.04f, 0.18f, 0.75f));
            Material emptyMaterial = ConfigureMaterial(
                "Assets/_Game/Art/Shared/Materials/Empty Flask.mat",
                new Color(0.25f, 0.25f, 0.25f));
            CreateFlaskPrefab(k_HealthModelPath, "Health Flask", healthMaterial);
            CreateFlaskPrefab(k_FocusModelPath, "Focus Flask", focusMaterial);
            CreateFlaskPrefab(k_EmptyModelPath, "Empty Flask", emptyMaterial);
            CreateFlaskVFX(k_HealthVFXPath, new Color(1f, 0.2f, 0.08f, 0.8f));
            CreateFlaskVFX(k_FocusVFXPath, new Color(0.1f, 0.45f, 1f, 0.8f));
            CreateIcon(k_HealthIconPath, "Health Flask Icon", healthMaterial.color);
            CreateIcon(k_FocusIconPath, "Focus Flask Icon", focusMaterial.color);
        }

        private static Material ConfigureMaterial(string assetPath, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateFlaskPrefab(
            string assetPath,
            string objectName,
            Material material)
        {
            GameObject root = new GameObject(objectName);
            try
            {
                CreatePrimitiveChild(
                    root.transform,
                    "Bottle",
                    PrimitiveType.Capsule,
                    new Vector3(0f, 0.08f, 0f),
                    new Vector3(0.07f, 0.12f, 0.07f),
                    Vector3.zero,
                    material);
                CreatePrimitiveChild(
                    root.transform,
                    "Neck",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.24f, 0f),
                    new Vector3(0.035f, 0.04f, 0.035f),
                    Vector3.zero,
                    material);
                CreatePrimitiveChild(
                    root.transform,
                    "Stopper",
                    PrimitiveType.Sphere,
                    new Vector3(0f, 0.3f, 0f),
                    new Vector3(0.045f, 0.035f, 0.045f),
                    Vector3.zero,
                    material);
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateFlaskVFX(string assetPath, Color color)
        {
            GameObject root = new GameObject(
                System.IO.Path.GetFileNameWithoutExtension(assetPath));
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.duration = 0.8f;
                main.loop = false;
                main.startLifetime = 0.7f;
                main.startSpeed = 0.6f;
                main.startSize = 0.12f;
                main.startColor = color;
                main.playOnAwake = true;
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 24f;
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.35f;
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Sprite CreateIcon(
            string assetPath,
            string objectName,
            Color color)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
                {
                    name = $"{objectName} Texture",
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(texture, assetPath);
            }

            Color border = new Color(0.85f, 0.7f, 0.3f, 1f);
            Color transparent = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float centeredX = Mathf.Abs(x - 15.5f);
                    bool isBottle = y >= 4 && y <= 23 && centeredX <= 8f;
                    bool isNeck = y > 23 && y <= 29 && centeredX <= 4f;
                    bool isEdge = isBottle &&
                        (centeredX >= 7f || y <= 5 || y >= 22);
                    texture.SetPixel(
                        x,
                        y,
                        isEdge ? border : isBottle || isNeck ? color : transparent);
                }
            }

            texture.Apply();
            EditorUtility.SetDirty(texture);
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault();
            if (sprite == null)
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    32f);
                sprite.name = objectName;
                AssetDatabase.AddObjectToAsset(sprite, texture);
            }

            EditorUtility.SetDirty(sprite);
            return sprite;
        }

        private static void ConfigureItems()
        {
            FlaskItem healthFlask = LoadOrCreateAsset<FlaskItem>(k_HealthFlaskPath);
            FlaskItem focusFlask = LoadOrCreateAsset<FlaskItem>(k_FocusFlaskPath);
            ConfigureFlaskItem(
                healthFlask,
                "Flask of Crimson Tears",
                "A sacred flask that restores Health at the drink success frame.",
                LoadRequiredSprite(k_HealthIconPath),
                LoadRequiredAsset<GameObject>(k_HealthModelPath),
                true,
                55f,
                k_HealthFlaskID);
            ConfigureFlaskItem(
                focusFlask,
                "Flask of Cerulean Tears",
                "A sacred flask that restores Focus Points at the drink success frame.",
                LoadRequiredSprite(k_FocusIconPath),
                LoadRequiredAsset<GameObject>(k_FocusModelPath),
                false,
                50f,
                k_FocusFlaskID);
        }

        private static void ConfigureFlaskItem(
            FlaskItem flask,
            string itemName,
            string description,
            Sprite icon,
            GameObject model,
            bool restoresHealth,
            float restoration,
            int itemID)
        {
            SerializedObject serializedFlask = new SerializedObject(flask);
            SetString(serializedFlask, "m_itemName", itemName);
            SetString(serializedFlask, "m_itemDescription", description);
            SetObject(serializedFlask, "m_itemIcon", icon);
            SetObject(serializedFlask, "m_itemModel", model);
            SetObject(
                serializedFlask,
                "m_useItemAnimation",
                LoadRequiredAsset<AnimationClip>(k_DrinkStartClipPath));
            SetObject(
                serializedFlask,
                "m_emptyFlaskItemModel",
                LoadRequiredAsset<GameObject>(k_EmptyModelPath));
            SetBool(serializedFlask, "m_restoresHealth", restoresHealth);
            SetFloat(serializedFlask, "m_flaskRestoration", restoration);
            SetInt(serializedFlask, "m_itemID", itemID);
            serializedFlask.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flask);
        }

        private static void ConfigureDatabase()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_DatabasePrefabPath);
            try
            {
                WorldItemDatabase database = root.GetComponent<WorldItemDatabase>();
                SerializedObject serializedDatabase = new SerializedObject(database);
                FlaskItem healthFlask = LoadRequiredAsset<FlaskItem>(
                    k_HealthFlaskPath);
                FlaskItem focusFlask = LoadRequiredAsset<FlaskItem>(
                    k_FocusFlaskPath);
                AppendUnique(serializedDatabase.FindProperty("m_items"), healthFlask);
                AppendUnique(serializedDatabase.FindProperty("m_items"), focusFlask);
                AppendUnique(
                    serializedDatabase.FindProperty("m_quickSlotItems"),
                    healthFlask);
                AppendUnique(
                    serializedDatabase.FindProperty("m_quickSlotItems"),
                    focusFlask);
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                AssignItemID(healthFlask, k_HealthFlaskID);
                AssignItemID(focusFlask, k_FocusFlaskID);
                PrefabUtility.SaveAsPrefabAsset(root, k_DatabasePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigurePlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerInventoryManager inventory =
                    root.GetComponent<PlayerInventoryManager>();
                SerializedObject serializedInventory = new SerializedObject(inventory);
                SetObject(
                    serializedInventory,
                    "m_startingQuickSlotItem",
                    LoadRequiredAsset<FlaskItem>(k_HealthFlaskPath));
                serializedInventory.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureWorldManagers()
        {
            Scene scene = EditorSceneManager.OpenScene(
                k_MainMenuScenePath,
                OpenSceneMode.Single);
            WorldCharacterEffectsManager effectsManager =
                UnityEngine.Object.FindFirstObjectByType<WorldCharacterEffectsManager>(
                    FindObjectsInactive.Include);
            WorldSoundFXManager soundManager =
                UnityEngine.Object.FindFirstObjectByType<WorldSoundFXManager>(
                    FindObjectsInactive.Include);
            if (effectsManager == null || soundManager == null)
            {
                throw new InvalidOperationException(
                    "The main menu scene requires world effect and sound managers.");
            }

            SerializedObject serializedEffects = new SerializedObject(effectsManager);
            SetObject(
                serializedEffects,
                "m_healingFlaskVFX",
                LoadRequiredAsset<GameObject>(k_HealthVFXPath));
            SetObject(
                serializedEffects,
                "m_focusFlaskVFX",
                LoadRequiredAsset<GameObject>(k_FocusVFXPath));
            serializedEffects.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedSounds = new SerializedObject(soundManager);
            SetObject(
                serializedSounds,
                "m_flaskRestorationSoundEffect",
                LoadRequiredAsset<AudioClip>(
                    "Assets/_Game/Audio/SFX/Abilities/SFX_Heal_01.wav"));
            SetObject(
                serializedSounds,
                "m_emptyFlaskSoundEffect",
                LoadRequiredAsset<AudioClip>(
                    "Assets/_Game/Audio/SFX/Abilities/" +
                    "SFX_Fail_Corpse_Revival_01.wav"));
            serializedSounds.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effectsManager);
            EditorUtility.SetDirty(soundManager);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ValidateItems()
        {
            FlaskItem healthFlask = LoadRequiredAsset<FlaskItem>(k_HealthFlaskPath);
            FlaskItem focusFlask = LoadRequiredAsset<FlaskItem>(k_FocusFlaskPath);
            if (healthFlask.ItemID != k_HealthFlaskID ||
                focusFlask.ItemID != k_FocusFlaskID ||
                !healthFlask.RestoresHealth ||
                focusFlask.RestoresHealth ||
                healthFlask.ItemIcon == null ||
                focusFlask.ItemIcon == null ||
                healthFlask.EmptyFlaskItemModel == null ||
                focusFlask.EmptyFlaskItemModel == null)
            {
                throw new InvalidOperationException(
                    "Flask item IDs, restoration categories, icons, or models are invalid.");
            }
        }

        private static void ValidateAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .First(layer => layer.name == k_UpperBodyLayerName)
                .stateMachine;
            AnimatorState empty = GetRequiredState(stateMachine, "Empty");
            AnimatorState drink01 = GetRequiredState(stateMachine, "Drink 01");
            AnimatorState drink02 = GetRequiredState(stateMachine, "Drink 02");
            bool hasParameter = controller.parameters.Any(parameter =>
                parameter.name == k_ChuggingParameter &&
                parameter.type == AnimatorControllerParameterType.Bool);
            bool clipsHaveEvents = new[] { k_Drink01ClipPath, k_Drink02ClipPath }
                .All(path => AnimationUtility.GetAnimationEvents(
                        LoadRequiredAsset<AnimationClip>(path))
                    .Count(animationEvent =>
                        animationEvent.functionName ==
                        "SuccessfullyUseQuickSlotItem") == 1);
            if (!hasParameter ||
                empty.behaviours.All(behaviour =>
                    behaviour is not ResetUpperBodyAction) ||
                drink01.behaviours.All(behaviour =>
                    behaviour is not ResetIsChugging) ||
                drink02.behaviours.All(behaviour =>
                    behaviour is not ResetIsChugging) ||
                !clipsHaveEvents)
            {
                throw new InvalidOperationException(
                    "The upper-body Animator is missing the flask parameter, " +
                    "state reset behaviours, or success events.");
            }
        }

        private static void ValidatePrefabs()
        {
            GameObject databaseRoot = PrefabUtility.LoadPrefabContents(
                k_DatabasePrefabPath);
            try
            {
                WorldItemDatabase database =
                    databaseRoot.GetComponent<WorldItemDatabase>();
                SerializedObject serializedDatabase = new SerializedObject(database);
                if (serializedDatabase.FindProperty("m_quickSlotItems").arraySize < 2)
                {
                    throw new InvalidOperationException(
                        "The world database requires both flask items.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(databaseRoot);
            }

            GameObject playerRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerPrefabPath);
            try
            {
                SerializedObject inventory = new SerializedObject(
                    playerRoot.GetComponent<PlayerInventoryManager>());
                if (inventory.FindProperty("m_startingQuickSlotItem")
                        .objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        "The player requires a default Health flask quick-slot item.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static AnimatorState ConfigureState(
            AnimatorStateMachine stateMachine,
            string stateName,
            string clipPath,
            Vector3 position)
        {
            AnimatorState state = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(candidate => candidate.name == stateName) ??
                stateMachine.AddState(stateName, position);
            state.motion = LoadRequiredAsset<AnimationClip>(clipPath);
            EditorUtility.SetDirty(state);
            return state;
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                throw new InvalidOperationException(
                    $"Animator state {stateName} is missing.");
        }

        private static void ClearTransitions(AnimatorState state)
        {
            foreach (AnimatorStateTransition transition in state.transitions.ToArray())
            {
                state.RemoveTransition(transition);
            }
        }

        private static void AddExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            EditorUtility.SetDirty(transition);
        }

        private static void AddBoolTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameter,
            bool expectedValue,
            float exitTime)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = 0.04f;
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameter);
            EditorUtility.SetDirty(transition);
        }

        private static void AddBehaviourIfMissing<T>(AnimatorState state)
            where T : StateMachineBehaviour
        {
            if (state.behaviours.All(behaviour => behaviour is not T))
            {
                state.AddStateMachineBehaviour<T>();
            }
        }

        private static void EnsureBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .FirstOrDefault(candidate => candidate.name == parameterName);
            if (parameter == null)
            {
                controller.AddParameter(
                    parameterName,
                    AnimatorControllerParameterType.Bool);
            }
            else if (parameter.type != AnimatorControllerParameterType.Bool)
            {
                throw new InvalidOperationException(
                    $"Animator parameter {parameterName} must be Bool.");
            }
        }

        private static GameObject CreatePrimitiveChild(
            Transform parent,
            string objectName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material material)
        {
            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = objectName;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            child.transform.localRotation = Quaternion.Euler(localEulerAngles);
            child.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return child;
        }

        private static void AppendUnique(
            SerializedProperty array,
            UnityEngine.Object value)
        {
            for (int index = 0; index < array.arraySize; index++)
            {
                if (array.GetArrayElementAtIndex(index).objectReferenceValue == value)
                {
                    return;
                }
            }

            int newIndex = array.arraySize;
            array.InsertArrayElementAtIndex(newIndex);
            array.GetArrayElementAtIndex(newIndex).objectReferenceValue = value;
        }

        private static void AssignItemID(Item item, int itemID)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            SetInt(serializedItem, "m_itemID", itemID);
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void EnsureFolder(string parentFolder, string childFolder)
        {
            string path = $"{parentFolder}/{childFolder}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolder);
            }
        }

        private static T LoadOrCreateAsset<T>(string assetPath)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static AnimationClip LoadOrCreateAnimationClip(string assetPath)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null)
            {
                return clip;
            }

            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, assetPath);
            return clip;
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static Sprite LoadRequiredSprite(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault() ??
                throw new InvalidOperationException(
                    $"Required Sprite is missing: {assetPath}");
        }

        private static void SetObject(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetProperty(serializedObject, propertyName).objectReferenceValue = value;
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void SetInt(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetProperty(serializedObject, propertyName).boolValue = value;
        }

        private static SerializedProperty GetProperty(
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
