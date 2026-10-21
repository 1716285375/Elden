using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Tests
{
    public class EldenRingUIThemeTests
    {
        [TestCase("CombatHud")]
        [TestCase("CharacterMenu")]
        [TestCase("EquipmentMenu")]
        [TestCase("WeaponMenu")]
        [TestCase("InventoryMenu")]
        [TestCase("MemorizeSpells")]
        [TestCase("LevelUp")]
        [TestCase("SiteOfGrace")]
        [TestCase("TravelMenu")]
        [TestCase("StorageMenu")]
        [TestCase("ShopMenu")]
        [TestCase("WeaponUpgrade")]
        [TestCase("SettingsPanel")]
        [TestCase("SaveGame")]
        [TestCase("LoadingScreen")]
        public void EveryWindowLoadsOriginalSharedTheme(string window)
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/_Game/Resources/UI/" + window + ".uxml");
            var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_Game/Resources/UI/EldenRingTheme.uss");
            Assert.That(layout, Is.Not.Null);
            Assert.That(theme, Is.Not.Null);
            VisualElement root = layout.CloneTree();
            Assert.That(root.Query<VisualElement>().ToList().Any(element => element.styleSheets.Contains(theme)),
                Is.True, window + " does not attach the original theme");
        }

        [Test]
        public void SharedThemeReferencesImportedOriginalTextures()
        {
            const string folder = "Assets/_Game/Resources/UI/";
            string source = File.ReadAllText(folder + "EldenRingTheme.uss");
            MatchCollection references = Regex.Matches(source, "url\\(\"([^\"]+)\"\\)");
            Assert.That(references.Count, Is.GreaterThan(25));
            foreach (Match reference in references)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(folder + reference.Groups[1].Value),
                    Is.Not.Null, reference.Value);
            }
        }

        [Test]
        public void CatalogItemsNoLongerUseOldGameIconAssets()
        {
            string[] itemGuids = AssetDatabase.FindAssets("t:Item", new[] { "Assets/_Game/Data/Items" });
            Assert.That(itemGuids.Length, Is.GreaterThan(10));
            foreach (string guid in itemGuids)
            {
                var item = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
                Object icon = item.FindProperty("m_itemIcon").objectReferenceValue;
                Assert.That(icon, Is.Not.Null, guid);
                Assert.That(AssetDatabase.GetAssetPath(icon), Does.Contain("EldenRing"), guid);
            }
        }
    }
}
