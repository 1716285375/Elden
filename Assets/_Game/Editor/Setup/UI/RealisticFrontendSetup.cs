using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor.Events;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Recomposes the existing title screen without replacing its commands or save flow.</summary>
    public static class RealisticFrontendSetup
    {
        private const string k_Scene = "Assets/_Game/Scenes/Frontend/SCN_MainMenu.unity";
        private const string k_Art = "Assets/_Game/Resources/UI/EldenRing/Elements/";
        private const string k_Output = "Docs/ArtRefresh";
        private const string k_PlayerUI = "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [MenuItem("Tools/ZZ/Frontend/Remove Menu Presentation")]
        public static void RemovePresentation()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before changing the title scene.");
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(k_Scene);
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(k_Scene, OpenSceneMode.Additive);
            }
            if (scene.isDirty)
            {
                throw new InvalidOperationException("Save existing title scene edits before removing presentation objects.");
            }
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject presentation = roots.SingleOrDefault(root => root.name == "_MENU_PRESENTATION");
            if (presentation == null)
            {
                return;
            }
            Directory.CreateDirectory(k_Output);
            string backup = k_Output + "/SCN_MainMenu.before-presentation-removal.unity.txt";
            if (!File.Exists(backup))
            {
                File.Copy(k_Scene, backup);
            }
            TitleScreenManager title = roots.SelectMany(root => root.GetComponentsInChildren<TitleScreenManager>(true)).Single();
            var serialized = new SerializedObject(title);
            SerializedProperty objects = serialized.FindProperty("m_primaryPresentationObjects");
            GameObject[] retained = Enumerable.Range(0, objects.arraySize)
                .Select(index => objects.GetArrayElementAtIndex(index).objectReferenceValue as GameObject)
                .Where(item => item != null && !item.transform.IsChildOf(presentation.transform)).ToArray();
            objects.arraySize = retained.Length;
            for (int index = 0; index < retained.Length; index++)
            {
                objects.GetArrayElementAtIndex(index).objectReferenceValue = retained[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            int removed = presentation.GetComponentsInChildren<Transform>(true).Length;
            Object.DestroyImmediate(presentation);
            string[] fields = { "m_continueButton", "m_newGameButton", "m_loadGameButton",
                "m_settingsButton", "m_creditsButton", "m_quitButton" };
            if (fields.Any(field => serialized.FindProperty(field).objectReferenceValue is not Button button ||
                button.onClick.GetPersistentEventCount() == 0))
            {
                throw new InvalidDataException("The title command bindings changed during presentation removal.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText(k_Output + "/menu-presentation-removal.txt",
                $"RemovedObjects={removed}; PresentationReferences={retained.Length}; TitleBindings=6/6\n");
        }

        /// <summary>Applies a restrained title layout and verifies the six existing command bindings.</summary>
        [MenuItem("Tools/ZZ/Frontend/Rebuild Realistic Title")]
        public static void Rebuild()
        {
            if (Application.isPlaying || EditorSceneManager.GetSceneManagerSetup().Any(scene =>
                scene.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty))
            {
                throw new InvalidOperationException("Stop Play Mode and save open scenes before rebuilding the title.");
            }

            Directory.CreateDirectory(k_Output);
            string backup = Path.Combine(k_Output, "SCN_MainMenu.before.unity.txt");
            if (!File.Exists(backup))
            {
                File.Copy(k_Scene, backup);
            }

            var scene = EditorSceneManager.OpenScene(k_Scene);
            GameObject[] roots = scene.GetRootGameObjects();
            Transform[] transforms = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            TitleScreenManager title = roots.SelectMany(root => root.GetComponentsInChildren<TitleScreenManager>(true)).Single();
            var serialized = new SerializedObject(title);
            string[] fields = { "m_continueButton", "m_newGameButton", "m_loadGameButton",
                "m_settingsButton", "m_creditsButton", "m_quitButton" };
            Button[] buttons = fields.Select(field =>
                serialized.FindProperty(field).objectReferenceValue as Button).ToArray();
            if (buttons.Any(button => button == null || button.onClick.GetPersistentEventCount() == 0))
            {
                throw new InvalidOperationException("The title must retain all six authored command bindings.");
            }

            foreach (Button button in buttons)
            {
                var bound = new HashSet<(Object Target, string Method)>();
                for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                {
                    var key = (button.onClick.GetPersistentTarget(index), button.onClick.GetPersistentMethodName(index));
                    if (!bound.Add(key))
                    {
                        UnityEventTools.RemovePersistentListener(button.onClick, index--);
                    }
                }
            }

            RectTransform menu = (RectTransform)transforms.Single(item => item.name == "LeftMenuRoot");
            menu.anchorMin = new Vector2(0.5f, 0.22f);
            menu.anchorMax = menu.anchorMin;
            menu.anchoredPosition = Vector2.zero;
            menu.sizeDelta = new Vector2(390f, 96f);
            var layout = menu.GetComponent<VerticalLayoutGroup>();
            layout.enabled = false;
            foreach (Button button in buttons)
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(390f, 30f);
            }
            TitleMenuCarousel carousel = menu.GetComponent<TitleMenuCarousel>();
            if (carousel == null)
            {
                carousel = menu.gameObject.AddComponent<TitleMenuCarousel>();
            }
            carousel.Configure(buttons);
            foreach (Button button in buttons)
            {
                button.transform.localRotation = Quaternion.identity;
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.color = Color.clear;
                }
                LayoutElement element = button.GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.minHeight = 27f;
                    element.preferredHeight = 27f;
                }
                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    label.alignment = TextAlignmentOptions.Center;
                    label.font = Resources.Load<TMP_FontAsset>("UI/Fonts/OriginalLatin SDF") ?? label.font;
                    label.enableAutoSizing = false;
                    label.fontSize = 20f;
                    label.fontStyle = FontStyles.Normal;
                    label.characterSpacing = 1f;
                    label.color = new Color(0.79f, 0.76f, 0.66f);
                    label.rectTransform.anchorMin = Vector2.zero;
                    label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = Vector2.zero;
                    label.rectTransform.offsetMax = Vector2.zero;
                    label.rectTransform.localRotation = Quaternion.identity;
                    label.rectTransform.localScale = Vector3.one;
                }
                SpriteState state = button.spriteState;
                state.highlightedSprite = Sprite("MENU_Title_Cursor");
                state.pressedSprite = state.highlightedSprite;
                state.selectedSprite = state.highlightedSprite;
                button.spriteState = state;
            }

            RectTransform logo = (RectTransform)transforms.Single(item => item.name == "Elden Ring Title Art");
            logo.anchorMin = new Vector2(0.175f, 0.27f);
            logo.anchorMax = new Vector2(0.825f, 0.96f);
            logo.offsetMin = Vector2.zero;
            logo.offsetMax = Vector2.zero;
            foreach (Transform item in transforms.Where(item => item.name == "HiFi_SecondaryBackdrop" ||
                item.name == "HiFi_CornerTag" || item.name == "HiFi_Logo"))
            {
                foreach (Graphic graphic in item.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.enabled = false;
                }
            }

            foreach (FrontendSelectableVisual visual in roots.SelectMany(root =>
                root.GetComponentsInChildren<FrontendSelectableVisual>(true)))
            {
                var appearance = new SerializedObject(visual);
                appearance.FindProperty("m_idleBackgroundColor").colorValue = Color.clear;
                appearance.FindProperty("m_normalBackgroundColor").colorValue = Color.white;
                appearance.FindProperty("m_idleBackgroundSprite").objectReferenceValue = null;
                appearance.FindProperty("m_selectedBackgroundSprite").objectReferenceValue = null;
                appearance.FindProperty("m_normalTextColor").colorValue = new Color(0.79f, 0.76f, 0.66f);
                appearance.FindProperty("m_selectedTextColor").colorValue = new Color(1f, 0.91f, 0.67f);
                appearance.FindProperty("m_labelShiftX").floatValue = 0f;
                appearance.ApplyModifiedPropertiesWithoutUndo();
                Transform background = visual.transform.Find("SelectionBackground");
                if (background != null && background.TryGetComponent(out Image image))
                {
                    image.sprite = Sprite("MENU_Title_Cursor");
                    image.color = Color.clear;
                }
            }

            foreach (Button button in buttons)
            {
                FrontendSelectableVisual visual = button.GetComponent<FrontendSelectableVisual>();
                if (visual == null)
                {
                    visual = button.gameObject.AddComponent<FrontendSelectableVisual>();
                }
                Transform highlight = button.transform.Find("SelectionBackground");
                Image background = highlight != null ? highlight.GetComponent<Image>() : button.targetGraphic as Image;
                if (background != null)
                {
                    background.sprite = Sprite("MENU_Title_Cursor");
                    background.color = Color.clear;
                }
                var appearance = new SerializedObject(visual);
                appearance.FindProperty("m_selectable").objectReferenceValue = button;
                appearance.FindProperty("m_selectionBackground").objectReferenceValue = background;
                appearance.FindProperty("m_label").objectReferenceValue = button.GetComponentInChildren<TMP_Text>(true);
                appearance.FindProperty("m_idleBackgroundColor").colorValue = Color.clear;
                appearance.FindProperty("m_normalBackgroundColor").colorValue = Color.white;
                appearance.FindProperty("m_normalTextColor").colorValue = new Color(0.79f, 0.76f, 0.66f);
                appearance.FindProperty("m_selectedTextColor").colorValue = new Color(1f, 0.91f, 0.67f);
                appearance.FindProperty("m_labelShiftX").floatValue = 0f;
                appearance.FindProperty("m_idleBackgroundSprite").objectReferenceValue = null;
                appearance.FindProperty("m_selectedBackgroundSprite").objectReferenceValue = null;
                appearance.ApplyModifiedPropertiesWithoutUndo();
                button.transition = Selectable.Transition.None;
            }
            foreach (Transform item in transforms.Where(item => item.name == "HiFi_HintStrip"))
            {
                foreach (Graphic graphic in item.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.enabled = false;
                }
            }
            foreach (Graphic graphic in roots.SelectMany(root => root.GetComponentsInChildren<Graphic>(true)))
            {
                if (graphic.name == "HiFi_BackgroundArt" || graphic.name == "Title Screen Background")
                {
                    graphic.color = Color.black;
                }
            }

            foreach (Selectable selectable in roots.SelectMany(root => root.GetComponentsInChildren<Selectable>(true)))
            {
                SpriteState state = selectable.spriteState;
                if (AssetDatabase.GetAssetPath(state.highlightedSprite).Contains("/Frontend/HiFi/"))
                {
                    state.highlightedSprite = Sprite("MENU_Title_Cursor");
                }
                if (AssetDatabase.GetAssetPath(state.pressedSprite).Contains("/Frontend/HiFi/"))
                {
                    state.pressedSprite = Sprite("MENU_Title_Cursor");
                }
                if (AssetDatabase.GetAssetPath(state.selectedSprite).Contains("/Frontend/HiFi/"))
                {
                    state.selectedSprite = Sprite("MENU_Title_Cursor");
                }
                selectable.spriteState = state;
            }

            GameObject pressStart = (GameObject)serialized.FindProperty("m_pressStartMenu").objectReferenceValue;
            Button startButton = pressStart.GetComponentInChildren<Button>(true);
            TMP_Text startLabel = startButton.GetComponentInChildren<TMP_Text>(true);
            InputHintText.SetTemplate(startLabel, "PRESS ANY BUTTON");
            startLabel.font = Resources.Load<TMP_FontAsset>("UI/Fonts/OriginalLatin SDF") ?? startLabel.font;
            startLabel.fontSize = 18f;
            startLabel.fontStyle = FontStyles.Normal;
            startLabel.characterSpacing = 1f;
            startLabel.alignment = TextAlignmentOptions.Center;
            var startRect = (RectTransform)startButton.transform;
            startRect.anchorMin = startRect.anchorMax = new Vector2(0.5f, 0.206f);
            startRect.pivot = new Vector2(0.5f, 0.5f);
            startRect.anchoredPosition = Vector2.zero;
            startRect.sizeDelta = new Vector2(390f, 32f);
            startButton.transition = Selectable.Transition.None;
            startLabel.rectTransform.anchorMin = Vector2.zero;
            startLabel.rectTransform.anchorMax = Vector2.one;
            startLabel.rectTransform.offsetMin = Vector2.zero;
            startLabel.rectTransform.offsetMax = Vector2.zero;
            foreach (Image image in startButton.GetComponentsInChildren<Image>(true))
            {
                image.sprite = Sprite("MENU_Title_Cursor");
                image.color = Color.white;
                image.enabled = true;
            }
            FrontendSelectableVisual startVisual = startButton.GetComponent<FrontendSelectableVisual>();
            if (startVisual != null)
            {
                var appearance = new SerializedObject(startVisual);
                appearance.FindProperty("m_idleBackgroundColor").colorValue = Color.white;
                appearance.FindProperty("m_normalTextColor").colorValue = new Color(1f, 0.91f, 0.67f);
                appearance.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            UseSystemCursor();
            string[] bindings = buttons.Select(button => button.name + ": " +
                string.Join(", ", Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                    .Select(index => button.onClick.GetPersistentMethodName(index)))).ToArray();
            File.WriteAllLines(Path.Combine(k_Output, "frontend-bindings.txt"), bindings);
            CapturePreview(title);
            Debug.Log("REALISTIC_FRONTEND_VERIFIED: six command bindings preserved; scene saved.");
        }

        private static Sprite Sprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(k_Art + name + ".png")
                ?? throw new InvalidOperationException("Missing title sprite: " + name);
        }

        private static void UseSystemCursor()
        {
            string backup = Path.Combine(k_Output, "PlayerUI.before.prefab.txt");
            if (!File.Exists(backup))
            {
                File.Copy(k_PlayerUI, backup);
            }
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerUI);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<PlayerUIManager>());
                serialized.FindProperty("m_uiCursorTexture").objectReferenceValue = null;
                serialized.FindProperty("m_uiCursorHotspot").vector2Value = Vector2.zero;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerUI);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CapturePreview(TitleScreenManager title)
        {
            // Preview scene state is discarded; no network host or save operation is started.
            var serialized = new SerializedObject(title);
            ((GameObject)serialized.FindProperty("m_pressStartMenu").objectReferenceValue).SetActive(false);
            ((GameObject)serialized.FindProperty("m_mainMenu").objectReferenceValue).SetActive(true);
            Canvas canvas = title.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = title.GetComponentInParent<Canvas>();
            }
            var cameraObject = new GameObject("Frontend Preview Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.015f, 0.018f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.nearClipPlane = 0.1f;
            var target = new RenderTexture(1920, 1080, 24);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate(true, true);
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(k_Output, "frontend-preview.png"), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                EditorSceneManager.OpenScene(k_Scene);
            }
        }
    }
}
