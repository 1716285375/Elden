using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class ProjectileUISystemTests
    {
        private const string k_StandardArrowPath =
            "Assets/_Game/Data/Items/Projectiles/Standard Arrow.asset";
        private const string k_FireArrowPath =
            "Assets/_Game/Data/Items/Projectiles/Fire Arrow.asset";
        private const string k_BowPath =
            "Assets/_Game/Data/Items/Weapons/Ranged Weapons/Longbow.asset";
        private const string k_SwordPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset";
        private const string k_EquipmentSlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Equipment Slot.prefab";
        private const string k_QuickSlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Quick Slot UI.prefab";
        private const string k_UIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [Test]
        public void ProjectileEquipmentSlotsUseStableTrailingEnumValues()
        {
            Type slotType = Type.GetType("ZZ.EquipmentSlotType, Assembly-CSharp");

            Assert.That(GetEnumValue(slotType, "MainProjectile"), Is.EqualTo(10));
            Assert.That(GetEnumValue(slotType, "SecondaryProjectile"), Is.EqualTo(11));
        }

        [Test]
        public void ProjectileItemsProvideIconsAndAuthoredStartingCounts()
        {
            UnityEngine.Object standardArrow = LoadAsset(k_StandardArrowPath);
            UnityEngine.Object fireArrow = LoadAsset(k_FireArrowPath);

            Assert.That(GetProperty<object>(standardArrow, "ItemIcon"), Is.Not.Null);
            Assert.That(GetProperty<object>(fireArrow, "ItemIcon"), Is.Not.Null);
            Assert.That(
                GetProperty<int>(standardArrow, "CurrentAmmoAmount"),
                Is.EqualTo(30));
            Assert.That(
                GetProperty<int>(fireArrow, "CurrentAmmoAmount"),
                Is.EqualTo(30));
        }

        [Test]
        public void ProjectileEquipmentSlotsAcceptOnlyProjectileItems()
        {
            Type managerType = Type.GetType(
                "ZZ.PlayerUIEquipmentManager, Assembly-CSharp");
            Type slotType = Type.GetType("ZZ.EquipmentSlotType, Assembly-CSharp");
            MethodInfo compatibility = managerType.GetMethod(
                "IsItemCompatibleWithSlot",
                BindingFlags.Public | BindingFlags.Static);
            UnityEngine.Object arrow = LoadAsset(k_StandardArrowPath);
            UnityEngine.Object sword = LoadAsset(k_SwordPath);
            object mainSlot = Enum.Parse(slotType, "MainProjectile");
            object secondarySlot = Enum.Parse(slotType, "SecondaryProjectile");

            Assert.That(
                compatibility.Invoke(null, new[] { arrow, mainSlot }),
                Is.True);
            Assert.That(
                compatibility.Invoke(null, new[] { arrow, secondarySlot }),
                Is.True);
            Assert.That(
                compatibility.Invoke(null, new[] { sword, mainSlot }),
                Is.False);
        }

        [Test]
        public void EquipmentMenuContainsTwoProjectileSlotsAndQuantityLabels()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_UIManagerPrefabPath);
            try
            {
                Component manager = FindComponent(root, "PlayerUIEquipmentManager");
                SerializedObject serializedManager = new SerializedObject(manager);
                SerializedProperty icons = serializedManager.FindProperty(
                    "m_equipmentSlotIcons");
                SerializedProperty buttons = serializedManager.FindProperty(
                    "m_equipmentSlotButtons");
                SerializedProperty quantities = serializedManager.FindProperty(
                    "m_equipmentSlotQuantityTexts");

                Assert.That(icons.arraySize, Is.EqualTo(15));
                Assert.That(buttons.arraySize, Is.EqualTo(15));
                Assert.That(quantities.arraySize, Is.EqualTo(15));
                Assert.That(
                    quantities.GetArrayElementAtIndex(10).objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    quantities.GetArrayElementAtIndex(11).objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    root.GetComponentsInChildren<Transform>(true)
                        .Select(transform => transform.name),
                    Does.Contain("Main Projectile"));
                Assert.That(
                    root.GetComponentsInChildren<Transform>(true)
                        .Select(transform => transform.name),
                    Does.Contain("Secondary Projectile"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            GameObject slotRoot = PrefabUtility.LoadPrefabContents(
                k_EquipmentSlotPrefabPath);
            try
            {
                Assert.That(
                    slotRoot.GetComponentsInChildren<Transform>(true)
                        .Any(transform => transform.name == "Quantity"),
                    Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(slotRoot);
            }
        }

        [Test]
        public void ProjectileHudStartsHiddenWithTwoConfiguredQuickSlots()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_UIManagerPrefabPath);
            try
            {
                Component hud = FindComponent(root, "PlayerUIHUDManager");
                SerializedObject serializedHud = new SerializedObject(hud);
                GameObject container = serializedHud.FindProperty(
                    "m_projectileQuickSlotsGameObject").objectReferenceValue as
                    GameObject;

                Assert.That(container, Is.Not.Null);
                Assert.That(container.activeSelf, Is.False);
                Assert.That(
                    serializedHud.FindProperty("m_mainProjectileQuickSlot")
                        .objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    serializedHud.FindProperty("m_secondaryProjectileQuickSlot")
                        .objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    container.GetComponentsInChildren<Component>(true)
                        .Count(component => component.GetType().Name == "UIQuickSlot"),
                    Is.EqualTo(2));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ProjectileQuickSlotShowsEveryCountAndClearsNullItem()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_QuickSlotPrefabPath);
            UnityEngine.Object runtimeArrow = UnityEngine.Object.Instantiate(
                LoadAsset(k_StandardArrowPath));
            try
            {
                Component quickSlot = FindComponent(root, "UIQuickSlot");
                MethodInfo setProjectile = quickSlot.GetType().GetMethod("SetProjectile");
                SerializedObject serializedArrow = new SerializedObject(runtimeArrow);
                SerializedProperty amount = serializedArrow.FindProperty(
                    "m_currentAmmoAmount");

                amount.intValue = 1;
                serializedArrow.ApplyModifiedPropertiesWithoutUndo();
                setProjectile.Invoke(quickSlot, new[] { runtimeArrow });
                AssertQuantity(quickSlot, "1", true);

                amount.intValue = 0;
                serializedArrow.ApplyModifiedPropertiesWithoutUndo();
                setProjectile.Invoke(quickSlot, new[] { runtimeArrow });
                AssertQuantity(quickSlot, "0", true);

                setProjectile.Invoke(quickSlot, new object[] { null });
                AssertQuantity(quickSlot, string.Empty, false);

                SerializedObject serializedQuickSlot = new SerializedObject(quickSlot);
                Component icon = serializedQuickSlot.FindProperty("m_iconImage")
                    .objectReferenceValue as Component;
                Assert.That(GetProperty<object>(icon, "sprite"), Is.Null);
                Assert.That(GetProperty<bool>(icon, "enabled"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeArrow);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ProjectileHudVisibilityRequiresABowInEitherHand()
        {
            Type hudType = Type.GetType("ZZ.PlayerUIHUDManager, Assembly-CSharp");
            MethodInfo visibility = hudType.GetMethod(
                "ShouldShowProjectileQuickSlots",
                BindingFlags.Public | BindingFlags.Static);
            UnityEngine.Object bow = LoadAsset(k_BowPath);
            UnityEngine.Object sword = LoadAsset(k_SwordPath);

            Assert.That(visibility.Invoke(null, new[] { bow, null }), Is.True);
            Assert.That(visibility.Invoke(null, new[] { sword, bow }), Is.True);
            Assert.That(visibility.Invoke(null, new[] { sword, null }), Is.False);
            Assert.That(visibility.Invoke(null, new object[] { null, null }), Is.False);
        }

        [Test]
        public void ProjectileConsumptionAndItemUseConflictsRefreshThroughRuntimeEvents()
        {
            Type inventoryType = Type.GetType(
                "ZZ.PlayerInventoryManager, Assembly-CSharp");
            Assert.That(inventoryType.GetEvent("MainProjectileChanged"), Is.Not.Null);
            Assert.That(inventoryType.GetEvent("SecondaryProjectileChanged"), Is.Not.Null);
            Assert.That(
                inventoryType.GetMethod("NotifyProjectileAmountChanged"),
                Is.Not.Null);

            string combatSource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Player/PlayerCombatManager.cs");
            string inventorySource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Common/Inventory/PlayerInventoryManager.cs");
            string interactionSource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Player/PlayerInteractionManager.cs");

            Assert.That(
                combatSource,
                Does.Contain("NotifyProjectileAmountChanged"));
            Assert.That(inventorySource, Does.Contain("IsUsingItem != true"));
            Assert.That(interactionSource, Does.Contain("IsUsingItem"));
        }

        private static UnityEngine.Object LoadAsset(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static Component FindComponent(GameObject root, string typeName)
        {
            Component component = root.GetComponentsInChildren<Component>(true)
                .Single(candidate => candidate.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null);
            return component;
        }

        private static int GetEnumValue(Type enumType, string valueName)
        {
            Assert.That(enumType, Is.Not.Null);
            return Convert.ToInt32(Enum.Parse(enumType, valueName));
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target?.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(target);
        }

        private static void AssertQuantity(
            Component quickSlot,
            string expectedText,
            bool expectedActive)
        {
            SerializedObject serializedQuickSlot = new SerializedObject(quickSlot);
            Component quantity = serializedQuickSlot.FindProperty("m_quantityDisplay")
                .objectReferenceValue as Component;

            Assert.That(quantity, Is.Not.Null);
            var digits = quantity.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .Where(image => image.name == "Digit" && image.gameObject.activeSelf)
                .OrderBy(image => image.rectTransform.anchoredPosition.x);
            Assert.That(digits.Select(image => image.sprite.name),
                Is.EqualTo(expectedText.Select(character => $"hud_{character}")));
            Assert.That(quantity.gameObject.activeSelf, Is.EqualTo(expectedActive));
        }
    }
}
