using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Applies original UI art to existing frontend and gameplay presentation without rewiring commands.</summary>
    public static class EldenRingUIImport
    {
        private const string k_Elements = "Assets/_Game/Resources/UI/EldenRing/Elements/";
        private const string k_Frontend = "Assets/_Game/Scenes/Frontend/SCN_MainMenu.unity";
        private const string k_PlayerUI = "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_Report = "F:/MyProject/Game/RE-Assets/EldenRing/Logs/ui-replacement.txt";

        /// <summary>Shows the original themed title without opening or writing any character save.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Preview Title UI")]
        public static void PreviewTitle()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("Enter Play Mode in the main menu scene first.");
            }
            Object.FindFirstObjectByType<TitleScreenManager>().PressStart();
            EditorApplication.delayCall += () => ScreenCapture.CaptureScreenshot(
                "F:/MyProject/Game/RE-Assets/EldenRing/Logs/ui-original-title.png");
        }

        /// <summary>Shows the existing creation flow without submitting a new character.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Preview Creation UI")]
        public static void PreviewCreation()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("Enter Play Mode in the main menu scene first.");
            }
            Object.FindFirstObjectByType<TitleScreenManager>().StartNewGame();
            EditorApplication.delayCall += () => ScreenCapture.CaptureScreenshot(
                "F:/MyProject/Game/RE-Assets/EldenRing/Logs/ui-original-creation.png");
        }

        /// <summary>Imports source rectangles and updates serialized presentation while preserving asset identities.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Install All UI Presentation")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before replacing UI presentation.");
            }
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(k_Elements, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                if (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled)
                {
                    continue;
                }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var report = new StringBuilder();
            GameObject prefab = PrefabUtility.LoadPrefabContents(k_PlayerUI);
            try
            {
                ApplyPresentation(prefab, report);
                PrefabUtility.SaveAsPrefabAsset(prefab, k_PlayerUI);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            foreach (string path in Directory.GetFiles("Assets/_Game/Prefabs/UI", "*.prefab"))
            {
                GameObject uiPrefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyPresentation(uiPrefab, report);
                    PrefabUtility.SaveAsPrefabAsset(uiPrefab, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(uiPrefab);
                }
            }
            Scene scene = SceneManager.GetSceneByPath(k_Frontend);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen)
            {
                scene = EditorSceneManager.OpenScene(k_Frontend, OpenSceneMode.Additive);
            }
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ApplyPresentation(root, report);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            ApplyItemIcons(report);
            AssetDatabase.SaveAssets();
            File.WriteAllText(k_Report, report.ToString());
            Debug.Log("ELDEN_RING_UI_INSTALLED " + k_Report);
        }

        private static void ApplyPresentation(GameObject root, StringBuilder report)
        {
            foreach (Transform decoration in root.GetComponentsInChildren<Transform>(true))
            {
                if (decoration.name == "HiFi_SecondaryBackdrop" || decoration.name == "HiFi_CornerTag")
                {
                    foreach (Graphic graphic in decoration.GetComponentsInChildren<Graphic>(true))
                    {
                        graphic.enabled = decoration.name == "HiFi_SecondaryBackdrop" && graphic is TMP_Text;
                    }
                }
            }
            Transform titleBackground = root.transform.Find("Title Screen Background");
            if (titleBackground != null)
            {
                Transform backdrop = titleBackground.Find("Elden Ring Opaque Backdrop");
                if (backdrop == null)
                {
                    backdrop = new GameObject("Elden Ring Opaque Backdrop",
                        typeof(RectTransform), typeof(Image)).transform;
                    backdrop.SetParent(titleBackground, false);
                }
                backdrop.SetAsFirstSibling();
                var backdropRect = (RectTransform)backdrop;
                backdropRect.anchorMin = Vector2.zero;
                backdropRect.anchorMax = Vector2.one;
                backdropRect.offsetMin = Vector2.zero;
                backdropRect.offsetMax = Vector2.zero;
                Image backdropImage = backdrop.GetComponent<Image>();
                backdropImage.color = Color.black;
                backdropImage.raycastTarget = false;
                Transform logo = titleBackground.Find("Elden Ring Title Art");
                if (logo == null)
                {
                    logo = new GameObject("Elden Ring Title Art", typeof(RectTransform), typeof(Image)).transform;
                    logo.SetParent(titleBackground, false);
                }
                logo.SetAsLastSibling();
                var rect = (RectTransform)logo;
                rect.anchorMin = new Vector2(0.48f, 0.60f);
                rect.anchorMax = new Vector2(0.95f, 0.96f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                Image image = logo.GetComponent<Image>();
                image.sprite = Sprite("MENU_Title_EldenRing_01");
                image.preserveAspect = true;
                image.raycastTarget = false;
                foreach (TitleScreenManager title in root.scene.GetRootGameObjects()
                    .SelectMany(sceneRoot => sceneRoot.GetComponentsInChildren<TitleScreenManager>(true)))
                {
                    var serialized = new SerializedObject(title);
                    SerializedProperty presentations = serialized.FindProperty("m_primaryPresentationObjects");
                    bool containsLogo = Enumerable.Range(0, presentations.arraySize).Any(index =>
                        presentations.GetArrayElementAtIndex(index).objectReferenceValue == logo.gameObject);
                    if (!containsLogo)
                    {
                        presentations.arraySize++;
                        presentations.GetArrayElementAtIndex(presentations.arraySize - 1).objectReferenceValue =
                            logo.gameObject;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                report.AppendLine("TITLE_LOGO " + AnimationUtility.CalculateTransformPath(logo, null));
            }
            foreach (RawImage image in root.GetComponentsInChildren<RawImage>(true))
            {
                if (image.name == "HiFi_BackgroundArt")
                {
                    image.enabled = true;
                    image.texture = Texture("MENU_BaseMain_01");
                    image.color = new Color(0.35f, 0.35f, 0.35f, 1f);
                    continue;
                }
                if (image.name == "HiFi_Logo")
                {
                    // The original title sprite supplies the logo; retain the legacy object and its references.
                    image.enabled = false;
                    report.AppendLine("DISABLED_LOGO " + AnimationUtility.CalculateTransformPath(image.transform, null));
                    continue;
                }
                string path = AssetDatabase.GetAssetPath(image.texture);
                if (image.texture is Texture2D && (path.Contains("/UI/") || path.Contains("/Ui/")) &&
                    !path.Contains("EldenRing"))
                {
                    image.texture = Texture("MENU_BaseMain_01");
                    image.color = Color.white;
                    report.AppendLine("RAW_IMAGE " + AnimationUtility.CalculateTransformPath(image.transform, null));
                }
            }
            foreach (ComicHalftoneGraphic halftone in root.GetComponentsInChildren<ComicHalftoneGraphic>(true))
            {
                halftone.enabled = false;
                EditorUtility.SetDirty(halftone);
            }
            foreach (ComicSelectionMarkerGraphic marker in root.GetComponentsInChildren<ComicSelectionMarkerGraphic>(true))
            {
                // The original textured highlight now supplies selection feedback; retain references to this object.
                marker.enabled = false;
                EditorUtility.SetDirty(marker);
            }
            foreach (ComicPanelGraphic panel in root.GetComponentsInChildren<ComicPanelGraphic>(true))
            {
                var serialized = new SerializedObject(panel);
                string art = panel.name.Contains("Selection") ? "MENU_Title_Cursor" : "MENU_BaseMain_01";
                serialized.FindProperty("m_originalTexture").objectReferenceValue = Texture(art);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                panel.color = Color.white;
                report.AppendLine("PANEL " + AnimationUtility.CalculateTransformPath(panel.transform, null) + " -> " + art);
            }
            foreach (FrontendSelectableVisual visual in root.GetComponentsInChildren<FrontendSelectableVisual>(true))
            {
                var serialized = new SerializedObject(visual);
                SetColor(serialized, "m_idleBackgroundColor", new Color(1f, 1f, 1f, 0.08f));
                SetColor(serialized, "m_normalBackgroundColor", Color.white);
                SetColor(serialized, "m_normalTextColor", new Color(0.9f, 0.86f, 0.76f));
                SetColor(serialized, "m_selectedTextColor", new Color(1f, 0.93f, 0.72f));
                serialized.FindProperty("m_labelShiftX").floatValue = 4f;
                serialized.FindProperty("m_idleBackgroundSprite").objectReferenceValue = Sprite("MENU_FE_NameBase");
                serialized.FindProperty("m_selectedBackgroundSprite").objectReferenceValue = Sprite("MENU_Title_Cursor");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(image.transform, null);
                string name = image.name;
                string spritePath = AssetDatabase.GetAssetPath(image.sprite);
                string art = null;
                if (name.Contains("Logo") || name.Contains("Brandmark"))
                {
                    art = "MENU_Title_EldenRing_01";
                    image.preserveAspect = true;
                }
                else if (name == "SelectionBackground")
                {
                    art = "MENU_Title_Cursor";
                }
                else if (name.Contains("Artwork") || name == "HiFi_BackgroundArt" || name == "Title Screen Background")
                {
                    art = "MENU_BaseMain_01";
                }
                else if (name == "Background")
                {
                    art = path.Contains("Bar") || path.Contains("Status Bars") ? "MENU_FL_HP_Base" : "MENU_BaseMain_01";
                }
                else if (name == "Fill" && image.GetComponentInParent<Slider>(true) != null)
                {
                    art = path.Contains("Stamina") || path.Contains("GREEN Slider") ? "MENU_FL_Green" :
                        path.Contains("Focus") || path.Contains("BLUE Slider") ? "MENU_FL_Blue" : "MENU_FL_Red";
                }
                else if (name.Contains("Highlight"))
                {
                    art = "MENU_SL_Cursor";
                }
                else if (spritePath.Contains("UI/Frontend/HiFi"))
                {
                    art = spritePath.Contains("Selected") ? "MENU_Title_Cursor" : "MENU_FE_NameBase";
                }
                else if (image.GetComponent<UIQuickSlot>() != null || name.Contains("Slot") && name.Contains("Background"))
                {
                    art = "MENU_FL_Equip_waku";
                }
                else if (spritePath == "Resources/unity_builtin_extra" && image.GetComponent<Button>() != null)
                {
                    art = "MENU_FE_NameBase";
                }
                if (art != null)
                {
                    image.sprite = Sprite(art);
                    image.color = Color.white;
                    if (name == "Title Screen Background")
                    {
                        image.color = new Color(0.16f, 0.16f, 0.16f, 1f);
                    }
                    report.AppendLine("IMAGE " + path + " -> " + art);
                }
                else if (!string.IsNullOrEmpty(spritePath) && !spritePath.Contains("EldenRing"))
                {
                    report.AppendLine("RETAINED " + path + " -> " + spritePath);
                }
            }
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.fontStyle &= ~FontStyles.Italic;
                if (text.color.r > 0.7f && text.color.b < 0.3f || text.name.Contains("Title"))
                {
                    text.color = new Color(0.9f, 0.84f, 0.67f);
                }
                else if (text.color.r < 0.4f && text.color.g < 0.4f && text.color.b < 0.4f)
                {
                    text.color = new Color(0.9f, 0.86f, 0.76f);
                }
            }
        }

        private static void ApplyItemIcons(StringBuilder report)
        {
            // These presentation aliases retain this project's item IDs and rules; they are not original item identity.
            var aliases = new Dictionary<string, int>
            {
                ["Starter Hood"] = 13020, ["Starter Armor"] = 13021,
                ["Starter Gauntlets"] = 13022, ["Starter Greaves"] = 13023,
                ["Straight Sword"] = 10040, ["Standard Arrow"] = 17000,
                ["Small Smithing Stone"] = 2000, ["Medium Smithing Stone"] = 2001,
                ["Large Smithing Stone"] = 2002, ["Incantation Catalyst"] = 11240,
                ["Medium Shield"] = 12069, ["Old Dungeon Key"] = 228, ["Fireball"] = 6202
            };
            var icons = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Art/UI/EldenRing/Icons" })
                .SelectMany(guid => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<Sprite>().ToDictionary(sprite => sprite.name);
            foreach (string guid in AssetDatabase.FindAssets("t:Item", new[] { "Assets/_Game/Data/Items" }))
            {
                Item item = AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(guid));
                Sprite icon = null;
                if (aliases.TryGetValue(item.ItemName, out int id))
                {
                    icon = icons["MENU_ItemIcon_" + id.ToString("D5")];
                }
                else if (item.ItemName == "Parry (Slow)" || item.ItemName == "Unarmed")
                {
                    icon = Sprite(item.ItemName == "Unarmed" ? "MENU_FL_Attack" : "MENU_FL_Gurd");
                }
                if (icon != null)
                {
                    var serialized = new SerializedObject(item);
                    serialized.FindProperty("m_itemIcon").objectReferenceValue = icon;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                    report.AppendLine("ICON_ALIAS " + item.ItemName + " -> " + icon.name);
                }
            }
        }

        private static Texture2D Texture(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(k_Elements + name + ".png") ??
            throw new InvalidOperationException("Missing original UI texture: " + name);

        private static Sprite Sprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(k_Elements + name + ".png") ??
            throw new InvalidOperationException("Missing original UI sprite: " + name);

        private static void SetColor(SerializedObject serialized, string name, Color color)
        {
            serialized.FindProperty(name).colorValue = color;
        }
    }
}
