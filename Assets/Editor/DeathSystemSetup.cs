using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    public static class DeathSystemSetup
    {
        private const string k_ActionLayerName = "Action Override";
        private const string k_DeadStateName = "Dead_01";
        private const string k_DeadParameterName = "isDead";
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_DeathClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/You_Died_01.anim";
        private const string k_PlayerUIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        private static readonly Color s_backgroundColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color s_shadowTextColor = new Color(0.2f, 0.005f, 0.005f, 0.7f);
        private static readonly Color s_popupTextColor = new Color(0.72f, 0.035f, 0.025f, 1f);

        [MenuItem("Tools/Elden/Configure Death System")]
        public static void ConfigureDeathSystem()
        {
            ConfigureAnimator();
            ConfigurePlayerUIPrefab();
            AssetDatabase.SaveAssets();
            ValidateDeathSystem();
            Debug.Log(
                "[DeathSystemSetup] Configured Health-driven death, Dead_01, YOU DIED UI, and revive support.");
        }

        [MenuItem("Tools/Elden/Validate Death System")]
        public static void ValidateDeathSystem()
        {
            ValidateAnimator();
            ValidatePlayerUIPrefab();
            Debug.Log(
                "[DeathSystemValidation] Death animation and YOU DIED popup assets are valid.");
        }

        private static void ConfigureAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            AnimationClip deathClip = LoadRequiredAsset<AnimationClip>(k_DeathClipPath);
            EnsureBoolParameter(controller, k_DeadParameterName);

            AnimatorStateMachine stateMachine = FindActionLayer(controller).stateMachine;
            AnimatorState deathState = FindState(stateMachine, k_DeadStateName) ??
                stateMachine.AddState(k_DeadStateName, new Vector3(1250f, 220f, 0f));
            deathState.motion = deathClip;
            deathState.speed = 1f;
            deathState.writeDefaultValues = true;

            foreach (AnimatorStateTransition transition in deathState.transitions.ToArray())
            {
                deathState.RemoveTransition(transition);
            }

            AnimatorStateTransition anyStateTransition =
                GetOrCreateAnyStateTransition(stateMachine, deathState);
            anyStateTransition.hasExitTime = false;
            anyStateTransition.hasFixedDuration = true;
            anyStateTransition.duration = 0.1f;
            anyStateTransition.canTransitionToSelf = false;
            anyStateTransition.interruptionSource = TransitionInterruptionSource.None;
            anyStateTransition.conditions = Array.Empty<AnimatorCondition>();
            anyStateTransition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                k_DeadParameterName);

            EditorUtility.SetDirty(deathState);
            EditorUtility.SetDirty(anyStateTransition);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigurePlayerUIPrefab()
        {
            GameObject playerUIRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerUIManagerPrefabPath);
            try
            {
                PlayerUIManager playerUIManager = GetRequiredComponent<PlayerUIManager>(
                    playerUIRoot);
                PlayerUIPopUpManager popupManager =
                    GetOrAddComponent<PlayerUIPopUpManager>(playerUIRoot);
                Transform canvas = playerUIRoot.transform.Find("Player UI");
                if (canvas == null)
                {
                    throw new InvalidOperationException(
                        "The Player UI Manager prefab is missing its Player UI Canvas.");
                }

                TMP_FontAsset font = playerUIRoot
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Select(text => text.font)
                    .FirstOrDefault(candidate => candidate != null);
                RectTransform popup = GetOrCreateRectTransform(canvas, "You Died Popup");
                StretchToParent(popup);
                popup.SetAsLastSibling();

                CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(popup.gameObject);
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                RectTransform background = GetOrCreateRectTransform(
                    popup,
                    "Background Image");
                ConfigureHorizontalBand(background, 220f);
                Image backgroundImage = GetOrAddComponent<Image>(background.gameObject);
                backgroundImage.color = s_backgroundColor;
                backgroundImage.raycastTarget = false;

                TMP_Text backgroundText = ConfigurePopupText(
                    GetOrCreateRectTransform(popup, "Background Text"),
                    font,
                    102f,
                    s_shadowTextColor,
                    new Vector2(4f, -3f));
                TMP_Text popupText = ConfigurePopupText(
                    GetOrCreateRectTransform(popup, "Popup Text"),
                    font,
                    96f,
                    s_popupTextColor,
                    Vector2.zero);

                SetObjectReference(popupManager, "m_youDiedPopup", popup.gameObject);
                SetObjectReference(popupManager, "m_popupCanvasGroup", canvasGroup);
                SetObjectReference(popupManager, "m_backgroundText", backgroundText);
                SetObjectReference(popupManager, "m_popupText", popupText);
                SetFloat(popupManager, "m_fadeInDuration", 0.8f);
                SetFloat(popupManager, "m_visibleDuration", 2f);
                SetFloat(popupManager, "m_fadeOutDuration", 1f);
                SetFloat(popupManager, "m_textStretchDuration", 3f);
                SetFloat(popupManager, "m_finalCharacterSpacing", 22f);
                SetObjectReference(
                    playerUIManager,
                    "m_playerUIPopUpManager",
                    popupManager);

                SetLayerRecursively(popup.gameObject, canvas.gameObject.layer);
                popup.gameObject.SetActive(false);
                EditorUtility.SetDirty(canvasGroup);
                EditorUtility.SetDirty(backgroundImage);
                EditorUtility.SetDirty(popupManager);
                EditorUtility.SetDirty(playerUIManager);
                PrefabUtility.SaveAsPrefabAsset(playerUIRoot, k_PlayerUIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerUIRoot);
            }
        }

        private static void ValidateAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            AnimationClip deathClip = LoadRequiredAsset<AnimationClip>(k_DeathClipPath);
            if (!HasBoolParameter(controller, k_DeadParameterName))
            {
                throw new InvalidOperationException(
                    "The Animator Controller is missing the isDead bool parameter.");
            }

            AnimatorStateMachine stateMachine = FindActionLayer(controller).stateMachine;
            AnimatorState deathState = FindState(stateMachine, k_DeadStateName) ??
                throw new InvalidOperationException(
                    "The Action Override layer is missing Dead_01.");
            if (deathState.motion != deathClip || deathState.transitions.Length != 0)
            {
                throw new InvalidOperationException(
                    "Dead_01 must use You_Died_01 and must not transition back to Empty.");
            }

            AnimatorStateTransition transition = stateMachine.anyStateTransitions
                .FirstOrDefault(candidate => candidate.destinationState == deathState);
            bool hasDeathCondition = transition != null &&
                transition.conditions.Any(condition =>
                    condition.parameter == k_DeadParameterName &&
                    condition.mode == AnimatorConditionMode.If);
            if (transition == null || transition.hasExitTime || !hasDeathCondition)
            {
                throw new InvalidOperationException(
                    "Any State must enter Dead_01 when isDead is true.");
            }
        }

        private static void ValidatePlayerUIPrefab()
        {
            GameObject playerUIRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerUIManagerPrefabPath);
            try
            {
                PlayerUIManager playerUIManager = GetRequiredComponent<PlayerUIManager>(
                    playerUIRoot);
                PlayerUIPopUpManager popupManager =
                    GetRequiredComponent<PlayerUIPopUpManager>(playerUIRoot);
                Transform canvas = playerUIRoot.transform.Find("Player UI");
                Transform popup = canvas?.Find("You Died Popup");
                if (popup == null ||
                    popup.Find("Background Image") == null ||
                    popup.Find("Background Text") == null ||
                    popup.Find("Popup Text") == null ||
                    popup.GetComponent<CanvasGroup>() == null ||
                    popup.gameObject.layer != canvas.gameObject.layer ||
                    popup.gameObject.activeSelf)
                {
                    throw new InvalidOperationException(
                        "The disabled YOU DIED popup hierarchy is incomplete.");
                }

                ValidateObjectReference(
                    popupManager,
                    "m_youDiedPopup",
                    popup.gameObject);
                ValidateObjectReference(
                    popupManager,
                    "m_popupCanvasGroup",
                    popup.GetComponent<CanvasGroup>());
                ValidateObjectReference(
                    popupManager,
                    "m_backgroundText",
                    popup.Find("Background Text").GetComponent<TMP_Text>());
                ValidateObjectReference(
                    popupManager,
                    "m_popupText",
                    popup.Find("Popup Text").GetComponent<TMP_Text>());
                ValidateObjectReference(
                    playerUIManager,
                    "m_playerUIPopUpManager",
                    popupManager);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerUIRoot);
            }
        }

        private static TMP_Text ConfigurePopupText(
            RectTransform rectTransform,
            TMP_FontAsset font,
            float fontSize,
            Color color,
            Vector2 anchoredPosition)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(1200f, 160f);

            TextMeshProUGUI text = GetOrAddComponent<TextMeshProUGUI>(
                rectTransform.gameObject);
            text.text = "YOU DIED";
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.SmallCaps;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.characterSpacing = 0f;
            if (font != null)
            {
                text.font = font;
            }

            EditorUtility.SetDirty(text);
            return text;
        }

        private static void ConfigureHorizontalBand(RectTransform rectTransform, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(1f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(0f, height);
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetLayerRecursively(GameObject rootObject, int layer)
        {
            rootObject.layer = layer;
            foreach (Transform child in rootObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string objectName)
        {
            Transform child = parent.Find(objectName);
            if (child != null)
            {
                RectTransform existingRect = child as RectTransform;
                return existingRect != null
                    ? existingRect
                    : throw new InvalidOperationException(
                        $"{objectName} must use a RectTransform.");
            }

            GameObject childObject = new GameObject(
                objectName,
                typeof(RectTransform));
            RectTransform rectTransform = childObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static AnimatorControllerLayer FindActionLayer(AnimatorController controller)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == k_ActionLayerName)
                {
                    return layer;
                }
            }

            throw new InvalidOperationException(
                $"The Animator Controller is missing {k_ActionLayerName}.");
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state.name == stateName)
                {
                    return childState.state;
                }
            }

            return null;
        }

        private static AnimatorStateTransition GetOrCreateAnyStateTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState destinationState)
        {
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == destinationState)
                {
                    return transition;
                }
            }

            return stateMachine.AddAnyStateTransition(destinationState);
        }

        private static void EnsureBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name != parameterName)
                {
                    continue;
                }

                if (parameter.type != AnimatorControllerParameterType.Bool)
                {
                    throw new InvalidOperationException(
                        $"Animator parameter {parameterName} must be a bool.");
                }

                return;
            }

            controller.AddParameter(parameterName, AnimatorControllerParameterType.Bool);
        }

        private static bool HasBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            return controller.parameters.Any(parameter =>
                parameter.name == parameterName &&
                parameter.type == AnimatorControllerParameterType.Bool);
        }

        private static T GetRequiredComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static T LoadRequiredAsset<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Could not find {propertyName}.");
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(
            UnityEngine.Object target,
            string propertyName,
            float value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Could not find {propertyName}.");
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Could not find {propertyName}.");
            if (property.objectReferenceValue != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{target.name}.{propertyName} is not assigned correctly.");
            }
        }
    }
}
