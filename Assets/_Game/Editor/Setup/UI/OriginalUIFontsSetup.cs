using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace ZZ.Editor
{
    public static class OriginalUIFontsSetup
    {
        [MenuItem("Tools/ZZ/UI Toolkit/Apply Original Gameplay Fonts")]
        public static void ApplyGameplay()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before updating the gameplay UI.");
            }
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("UI/Fonts/OriginalLatin SDF");
            if (font == null || !font.HasCharacters("法姆·亚兹拉的兽人", out uint[] missing, true, true))
            {
                throw new InvalidDataException("The original font and Chinese fallback must contain the Boss name.");
            }
            const string path = "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.font = font;
                    text.fontSharedMaterial = font.material;
                }
                PlayerUIBossHealthBar bar = root.GetComponentInChildren<PlayerUIBossHealthBar>(true);
                var rectangle = (RectTransform)bar.transform;
                rectangle.anchorMin = new Vector2(0.125f, 0.065f);
                rectangle.anchorMax = new Vector2(0.875f, 0.065f);
                rectangle.pivot = new Vector2(0.5f, 0f);
                rectangle.anchoredPosition = Vector2.zero;
                rectangle.sizeDelta = new Vector2(0f, 58f);
                Image panel = bar.GetComponent<Image>();
                if (panel != null)
                {
                    panel.enabled = false;
                }
                TMP_Text name = bar.GetComponentInChildren<TMP_Text>(true);
                name.alignment = TextAlignmentOptions.MidlineLeft;
                name.fontSize = 26f;
                name.enableAutoSizing = false;
                name.color = new Color(0.9f, 0.88f, 0.83f);
                name.rectTransform.anchorMin = new Vector2(0f, 1f);
                name.rectTransform.anchorMax = Vector2.one;
                name.rectTransform.pivot = new Vector2(0f, 1f);
                name.rectTransform.anchoredPosition = Vector2.zero;
                name.rectTransform.sizeDelta = new Vector2(0f, 34f);
                UIStatBar health = bar.GetComponentInChildren<UIStatBar>(true);
                var track = (RectTransform)health.transform;
                track.anchorMin = Vector2.zero;
                track.anchorMax = Vector2.right;
                track.pivot = new Vector2(0.5f, 0f);
                track.anchoredPosition = Vector2.zero;
                track.sizeDelta = new Vector2(0f, 16f);
                Image background = health.transform.Find("Background").GetComponent<Image>();
                background.sprite = GameplaySprite("MENU_FL_HP_Base");
                background.type = Image.Type.Simple;
                background.color = Color.white;
                Slider slider = health.GetComponent<Slider>();
                Image fill = slider.fillRect.GetComponent<Image>();
                fill.sprite = GameplaySprite("MENU_HP_Bar");
                fill.type = Image.Type.Simple;
                fill.color = new Color(0.65f, 0.12f, 0.09f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/OriginalSingleMap");
            File.WriteAllText("Docs/OriginalSingleMap/gameplay-fonts.txt",
                "PASS: gameplay TMP labels use original Latin font with original Chinese fallback; Boss name glyphs verified.\n" +
                "Boss frame/fill use extracted atlas art; dimensions still require original screenshot alignment.\n");
        }

        private static Sprite GameplaySprite(string name)
        {
            string path = "Assets/_Game/Resources/UI/EldenRing/Elements/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidDataException("Missing source sprite: " + path);
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Install Original Fonts")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before installing fonts.");
            }
            const string folder = "Assets/_Game/Resources/UI/Fonts";
            Directory.CreateDirectory(folder);
            foreach (string name in new[] { "OriginalLatin", "OriginalChinese" })
            {
                string path = folder + "/" + name + ".ttf";
                File.Copy("F:/MyProject/RE/EldenRing/Converted/UI/Fonts/" + name + ".ttf", path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(folder + "/" + name + " SDF.asset") != null)
                {
                    continue;
                }
                TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(path),
                    64, 8, GlyphRenderMode.SDFAA, 2048, 2048);
                font.name = name + " SDF";
                AssetDatabase.CreateAsset(font, folder + "/" + name + " SDF.asset");
                foreach (Texture2D atlas in font.atlasTextures)
                {
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
                AssetDatabase.AddObjectToAsset(font.material, font);
                EditorUtility.SetDirty(font);
            }
            TMP_FontAsset latin = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(folder + "/OriginalLatin SDF.asset");
            TMP_FontAsset chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(folder + "/OriginalChinese SDF.asset");
            latin.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (!latin.fallbackFontAssetTable.Contains(chinese))
            {
                latin.fallbackFontAssetTable.Add(chinese);
            }
            EditorUtility.SetDirty(latin);
            AssetDatabase.SaveAssets();
            Debug.Log("ORIGINAL_FONTS: Agmena and GR-FZShuSong outlines installed.");
        }
    }
}
