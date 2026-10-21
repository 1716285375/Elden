using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Imports authored icon rectangles and matches presentation to existing catalog items.</summary>
    public static class EldenRingIconImport
    {
        private const string k_Source = "F:/MyProject/RE/EldenRing/Converted";
        private const string k_Output = "Assets/_Game/Art/UI/EldenRing/Icons";

        /// <summary>Creates sprites with stable identifiers and updates exact item-name matches.</summary>
        [MenuItem("Tools/ZZ/Import Elden Ring Item Icons")]
        public static void Apply()
        {
            Directory.CreateDirectory(k_Output);
            string[] layouts = Directory.GetFiles(k_Source + "/UI/Layouts", "SB_Icon*.layout");
            foreach (string layout in layouts)
            {
                string name = Path.GetFileNameWithoutExtension(layout);
                File.Copy(k_Source + "/UI/Textures/" + name + ".png", k_Output + "/" + name + ".png", true);
            }
            AssetDatabase.Refresh();

            var icons = new Dictionary<string, Sprite>();
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            foreach (string layout in layouts)
            {
                string path = k_Output + "/" + Path.GetFileNameWithoutExtension(layout) + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.maxTextureSize = 4096;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                Dictionary<string, GUID> existing = provider.GetSpriteRects()
                    .ToDictionary(rect => rect.name, rect => rect.spriteID);
                var rectangles = new List<SpriteRect>();
                foreach (XElement element in XDocument.Load(layout).Root.Elements("SubTexture"))
                {
                    string name = Path.GetFileNameWithoutExtension((string)element.Attribute("name"));
                    int x = (int)element.Attribute("x");
                    int y = (int)element.Attribute("y");
                    int spriteWidth = (int)element.Attribute("width");
                    int spriteHeight = (int)element.Attribute("height");
                    if (x < 0 || y < 0 || x + spriteWidth > width || y + spriteHeight > height)
                    {
                        throw new InvalidDataException("Icon rectangle exceeds atlas: " + name);
                    }
                    rectangles.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(x, height - y - spriteHeight, spriteWidth, spriteHeight),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        spriteID = existing.TryGetValue(name, out GUID id) ? id : GUID.Generate()
                    });
                }
                provider.SetSpriteRects(rectangles.ToArray());
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                    rectangles.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
                provider.Apply();
                importer.SaveAndReimport();
                foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    icons.Add(sprite.name, sprite);
                }
            }

            Catalog catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(k_Source + "/Catalog/items.json"));
            var report = new StringBuilder();
            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Item", new[] { "Assets/_Game/Data/Items" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Item item = AssetDatabase.LoadAssetAtPath<Item>(path);
                CatalogItem match = catalog.Items.Where(entry => entry.Icon > 0 &&
                    string.Equals(entry.Name, item.ItemName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(entry => entry.ID).FirstOrDefault();
                if (match == null || !icons.TryGetValue("MENU_ItemIcon_" + match.Icon.ToString("D5"), out Sprite icon))
                {
                    report.AppendLine("UNCHANGED " + item.ItemName);
                    continue;
                }
                var serialized = new SerializedObject(item);
                serialized.FindProperty("m_itemIcon").objectReferenceValue = icon;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
                assigned++;
                report.AppendLine($"ASSIGNED {item.ItemName}: {match.Type}/{match.ID}, icon {match.Icon}");
            }
            AssetDatabase.SaveAssets();
            report.AppendLine($"Sprites={icons.Count}; AssignedItems={assigned}");
            File.WriteAllText("F:/MyProject/RE/EldenRing/Logs/unity-icons.txt", report.ToString());
            Debug.Log("ELDEN_RING_ICONS_COMPLETE " + report);
        }

        [Serializable]
        private sealed class Catalog
        {
            public CatalogItem[] Items;
        }

        [Serializable]
        private sealed class CatalogItem
        {
            public string Type;
            public int ID;
            public string Name;
            public int Icon;
        }
    }
}
