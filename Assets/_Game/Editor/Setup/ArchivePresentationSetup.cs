using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Applies supplied archive artwork while preserving save-slot bindings and data.</summary>
    public static class ArchivePresentationSetup
    {
        private const string k_Source = "Assets/_Game/Art/UI/Menus/Archive_Interface/";
        private const string k_Output = "Assets/_Game/UI/Archive/";

        [MenuItem("Tools/ZZ/Apply Archive Presentation")]
        public static void Apply()
        {
            Sprite idle = ImportTrimmed("UI_Slot_Unselected");
            Sprite selected = ImportTrimmed("UI_Slot_Selected");
            Sprite details = ImportTrimmed("right_info_background");
            Sprite title = ImportTrimmed("title_box");
            Sprite section = ImportTrimmed("Archive_sub_menu");
            TitleScreenManager manager = Object.FindFirstObjectByType<TitleScreenManager>(FindObjectsInactive.Include);
            Transform menu = manager.transform.Find("Load Character Menu");
            Skin(menu.Find("HiFi_LoadHeader"), title);
            Skin(menu.Find("HiFi_LoadDetails"), details);
            Transform sectionLabel = menu.Find("HiFi_LoadSectionLabel");
            Skin(sectionLabel, section);
            TMP_Text sectionText = sectionLabel.GetComponent<TMP_Text>();
            if (sectionText != null)
            {
                sectionText.enabled = true;
                sectionText.text = "CHOOSE YOUR JOURNEY";
                sectionText.fontSize = 24;
                sectionText.margin = new Vector4(34, 0, 20, 0);
            }
            foreach (UICharacterSaveSlot slot in menu.GetComponentsInChildren<UICharacterSaveSlot>(true))
            {
                var visual = new SerializedObject(slot.GetComponent<FrontendSelectableVisual>());
                visual.FindProperty("m_idleBackgroundSprite").objectReferenceValue = idle;
                visual.FindProperty("m_selectedBackgroundSprite").objectReferenceValue = selected;
                visual.FindProperty("m_normalTextColor").colorValue = new Color32(252, 244, 223, 255);
                visual.FindProperty("m_selectedTextColor").colorValue = new Color32(4, 24, 55, 255);
                SerializedProperty secondary = visual.FindProperty("m_secondaryLabels");
                secondary.arraySize = 2;
                secondary.GetArrayElementAtIndex(0).objectReferenceValue = slot.transform.Find("SlotMeta")
                    .GetComponent<TMP_Text>();
                secondary.GetArrayElementAtIndex(1).objectReferenceValue = slot.transform.Find("Time Played")
                    .GetComponent<TMP_Text>();
                Transform selection = slot.transform.Find("SelectionBackground");
                Skin(selection, idle);
                Image background = selection.Find("Archive Artwork").GetComponent<Image>();
                visual.FindProperty("m_selectionBackground").objectReferenceValue = background;
                visual.ApplyModifiedPropertiesWithoutUndo();
                background.sprite = idle;
                background.color = Color.white;
                background.type = Image.Type.Simple;
                background.preserveAspect = false;
                background.raycastTarget = false;
                background.gameObject.SetActive(true);
                TMP_Text label = slot.transform.Find("Label").GetComponent<TMP_Text>();
                label.fontSize = 27;
                label.enableAutoSizing = true;
                label.fontSizeMin = 22;
                label.fontSizeMax = 27;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
            const string path = "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SkinSavePanel(root.transform, details, title);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            foreach (PlayerUIManager playerUI in Object.FindObjectsByType<PlayerUIManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SkinSavePanel(playerUI.transform, details, title);
            }
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static void SkinSavePanel(Transform root, Sprite details, Sprite title)
        {
            Transform panel = root.Find("Player UI/Save Game Menu/Panel");
            Skin(panel, details);
            Skin(panel.Find("Title"), title);
            TMP_Text label = panel.Find("Title").GetComponent<TMP_Text>();
            label.enabled = true;
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                text.raycastTarget = false;
                if (text.GetComponentInParent<Button>() == null)
                {
                    text.color = new Color32(252, 244, 223, 255);
                }
            }
        }

        private static void Skin(Transform target, Sprite sprite)
        {
            foreach (Graphic graphic in target.GetComponents<Graphic>())
            {
                if (graphic is not TMP_Text)
                {
                    graphic.enabled = false;
                }
            }
            bool isText = target.GetComponent<TMP_Text>() != null;
            Transform parent = isText ? target.parent : target;
            string artworkName = isText ? target.name + " Artwork" : "Archive Artwork";
            Transform existing = parent.Find(artworkName);
            Image image;
            if (existing == null)
            {
                var child = new GameObject(artworkName, typeof(RectTransform), typeof(Image));
                child.transform.SetParent(parent, false);
                image = child.GetComponent<Image>();
            }
            else
            {
                image = existing.GetComponent<Image>();
            }
            image.transform.SetAsFirstSibling();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = Color.white;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            if (isText)
            {
                var targetRect = (RectTransform)target;
                image.rectTransform.anchorMin = targetRect.anchorMin;
                image.rectTransform.anchorMax = targetRect.anchorMax;
                image.rectTransform.sizeDelta = targetRect.sizeDelta;
                image.rectTransform.anchoredPosition = targetRect.anchoredPosition;
                image.transform.SetSiblingIndex(target.GetSiblingIndex());
            }
        }

        private static Sprite ImportTrimmed(string name)
        {
            Directory.CreateDirectory(k_Output);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(k_Source + name + ".png"));
            Color32[] pixels = texture.GetPixels32();
            int left = texture.width;
            int right = 0;
            int bottom = texture.height;
            int top = 0;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a > 20)
                    {
                        left = Mathf.Min(left, x);
                        right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y);
                        top = Mathf.Max(top, y);
                    }
                }
            }
            var trimmed = new Texture2D(right - left + 1, top - bottom + 1, TextureFormat.RGBA32, false);
            trimmed.SetPixels(texture.GetPixels(left, bottom, trimmed.width, trimmed.height));
            trimmed.Apply();
            string path = k_Output + name + ".png";
            File.WriteAllBytes(path, trimmed.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(trimmed);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
