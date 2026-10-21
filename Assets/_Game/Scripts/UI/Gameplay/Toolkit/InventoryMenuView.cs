using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Standalone item browsing with its own document, categories, focus, and return route.</summary>
    public sealed class InventoryMenuView : MonoBehaviour
    {
        private readonly List<Item> m_items = new();
        private UIDocument m_document;
        private PanelSettings m_panel;
        private VisualElement m_root;
        private InventoryItemGrid m_grid;
        private PlayerInventoryManager m_inventory;
        private TextField m_search;
        private PlayerControls m_controls;
        private ItemDetailDisplay m_display;
        private int m_category;
        private static readonly string[] s_categoryNames =
            { "道具", "箭矢／弩箭", "魔法", "祷告", "战灰", "制作材料", "强化材料", "贵重物品" };
        private Item m_selected;
        private IVisualElementScheduledItem m_refresh;

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            m_display = new ItemDetailDisplay(m_root);
            m_inventory = PlayerUIManager.Instance.LocalPlayer.InventoryManager;
            m_inventory.InventoryChanged += ScheduleRefresh;
            m_grid = new InventoryItemGrid(m_root.Q<ListView>("inventory-list"), m_items, SelectItem, InspectItem);
            m_search = m_root.Q<TextField>("item-search");
            m_search.RegisterValueChangedCallback(_ => Refresh());
            VisualElement tabs = m_root.Q("item-categories");
            tabs.Clear();
            string[] icons = { "00", "14", "30_PyroxeneMagic", "31_FaithMagic", "32_MagicStone", "01", "19", "03" };
            for (int index = 0; index < s_categoryNames.Length; index++)
            {
                int category = index;
                var tab = new Button(() => SelectCategory(category))
                {
                    name = "item-category-" + index,
                    tooltip = s_categoryNames[index]
                };
                var icon = new Image
                {
                    image = Resources.Load<Texture2D>("UI/EldenRing/Elements/MENU_Tab_" + icons[index]),
                    pickingMode = PickingMode.Ignore
                };
                icon.AddToClassList("inventory-menu__tab-icon");
                tab.Add(icon);
                tab.AddToClassList("menu-tab");
                tabs.Add(tab);
            }
            m_controls = new PlayerControls();
            m_controls.UI.PreviousShopCategory.performed += PreviousCategory;
            m_controls.UI.NextShopCategory.performed += NextCategory;
            m_controls.UI.PreviousShopCategory.Enable();
            m_controls.UI.NextShopCategory.Enable();
            InputHintDevice.DeviceChanged += RefreshHints;
            RefreshHints();
            m_root.Q<Button>("back").clicked += () =>
                PlayerUIManager.Instance.PlayerUIInventoryManager.ReturnToCharacterMenu();
            m_root.Q<Button>("inspect").clicked += InspectItem;
            SelectCategory(m_category);
            EventSystem.current?.SetSelectedGameObject(null);
            m_root.schedule.Execute(() =>
            {
                if (m_root == null)
                {
                    return;
                }
                if (m_items.Count > 0)
                {
                    m_grid.FocusSelected();
                }
                else
                {
                    m_root.Q<Button>("item-category-" + m_category).Focus();
                }
            });
        }

        private void OnDisable()
        {
            if (m_inventory != null)
            {
                m_inventory.InventoryChanged -= ScheduleRefresh;
            }
            m_display?.Dispose();
            m_display = null;
            InputHintDevice.DeviceChanged -= RefreshHints;
            if (m_controls != null)
            {
                m_controls.UI.PreviousShopCategory.performed -= PreviousCategory;
                m_controls.UI.NextShopCategory.performed -= NextCategory;
                m_controls.Dispose();
                m_controls = null;
            }
            m_refresh?.Pause();
            m_refresh = null;
            m_inventory = null;
            m_root = null;
            m_grid = null;
            m_items.Clear();
            m_selected = null;
        }

        private void OnDestroy()
        {
            if (m_panel != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(m_panel);
                }
                else
                {
                    DestroyImmediate(m_panel);
                }
            }
        }

        public static InventoryMenuView Create(Transform parent)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/InventoryMenu");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Standalone inventory Toolkit assets are missing.");
            }
            var instance = new GameObject("Inventory Menu Toolkit");
            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 100;
            UIDocument document = instance.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;
            document.gameObject.AddComponent<MenuAudioFeedback>();
            InventoryMenuView view = instance.AddComponent<InventoryMenuView>();
            view.m_document = document;
            view.m_panel = panel;
            return view;
        }

        private void Refresh()
        {
            Item selected = m_selected;
            m_items.Clear();
            string query = m_search.value?.Trim() ?? string.Empty;
            IEnumerable<Item> owned = m_inventory.ItemsInInventory.Concat(Enumerable.Range(0, 15)
                .Select(index => m_inventory.GetEquipmentSlotItem((EquipmentSlotType)index)));
            m_items.AddRange(owned.Where(item => ItemInventoryFilter.Matches(item, InventoryCategory.Items))
                .Distinct().Where(item => GetItemCategory(item) == m_category)
                .Where(item => (query.Length == 0 ||
                    (item.ItemName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(item => item.ItemID));
            int selectedIndex = selected != null ? m_items.IndexOf(selected) : -1;
            m_grid.Refresh(selectedIndex >= 0 ? selectedIndex : m_items.Count > 0 ? 0 : -1);
            m_root.Q("inventory-empty").style.display = m_items.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            CharacterStatusPresentation.PopulateSummary(m_root.Q("inventory-character-stats"),
                PlayerUIManager.Instance.LocalPlayer);
        }

        private void SelectItem(int index)
        {
            m_selected = index >= 0 ? m_items[index] : null;
            m_root.Q<Label>("detail-name").text = m_selected?.ItemName ?? string.Empty;
            m_root.Q<Image>("detail-icon").sprite = m_selected?.ItemIcon;
            m_root.Q<Label>("detail-description").text = m_selected?.ItemDescription ?? string.Empty;
            int amount = m_selected switch
            {
                RangedProjectileItem projectile => projectile.CurrentAmmoAmount,
                QuickSlotItem quickSlot when quickSlot.IsConsumable =>
                    quickSlot.GetCurrentAmount(PlayerUIManager.Instance.LocalPlayer),
                _ => m_selected?.CurrentItemAmount ?? 0
            };
            m_root.Q<Label>("detail-quantity").text = m_selected != null ? $"持有数量  {amount}" : string.Empty;
            m_root.Q<Button>("inspect").SetEnabled(m_selected != null);
        }

        private void InspectItem()
        {
            if (m_selected != null)
            {
                m_root.Q<ScrollView>("item-details").Focus();
            }
        }

        /// <summary>Maps owned items to exactly one item tab, excluding hand-held gear and armor.</summary>
        public static int GetItemCategory(Item item)
        {
            return item switch
            {
                null => -1,
                WeaponItem or ArmorItem => -1,
                QuickSlotItem => 0,
                RangedProjectileItem => 1,
                SpellItem spell => spell.SpellClass == SpellClass.Sorcery ? 2 : 3,
                AshOfWar => 4,
                UpgradeMaterial => 6,
                KeyItem => 7,
                _ => 5
            };
        }

        private void PreviousCategory(InputAction.CallbackContext context)
        {
            SelectCategory((m_category + s_categoryNames.Length - 1) % s_categoryNames.Length);
        }

        private void NextCategory(InputAction.CallbackContext context)
        {
            SelectCategory((m_category + 1) % s_categoryNames.Length);
        }

        private void SelectCategory(int category)
        {
            m_category = category;
            m_selected = null;
            m_search.SetValueWithoutNotify(string.Empty);
            m_root.Q<Label>("item-category-name").text = s_categoryNames[category];
            for (int index = 0; index < s_categoryNames.Length; index++)
            {
                m_root.Q("item-category-" + index).EnableInClassList("menu-tab--selected", index == category);
            }
            Refresh();
            if (m_items.Count > 0)
            {
                m_grid.FocusSelected();
            }
            else
            {
                m_root.Q<Button>("item-category-" + category).Focus();
            }
        }

        private void RefreshHints()
        {
            bool gamepad = InputHintDevice.IsGamepad;
            string prefix = ItemDetailDisplay.GetGamepadPrefix();
            bool playStation = prefix.StartsWith("PS", StringComparison.Ordinal);
            m_root.Q<Label>("category-previous").text = gamepad ? playStation ? "L1" : "LB" : "Z";
            m_root.Q<Label>("category-next").text = gamepad ? playStation ? "R1" : "RB" : "C";
            ItemDetailDisplay.SetHint(m_root.Q<Button>("inspect"), "确认",
                gamepad ? prefix + "R_D" : "Key_E");
            ItemDetailDisplay.SetHint(m_root.Q<Button>("back"), "返回",
                gamepad ? prefix + "R_R" : "Key_Q");
        }

        private void ScheduleRefresh()
        {
            m_refresh?.Pause();
            m_refresh = m_root?.schedule.Execute(Refresh);
        }
    }
}
