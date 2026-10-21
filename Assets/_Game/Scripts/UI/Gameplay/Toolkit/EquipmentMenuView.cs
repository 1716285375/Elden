using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Native equipment slots, virtualized inventory and item comparison presentation.</summary>
    public sealed class EquipmentMenuView : MonoBehaviour
    {
        private static readonly string[] s_slotNames =
        {
            "右手 · 一", "右手 · 二", "右手 · 三", "左手 · 一", "左手 · 二", "左手 · 三",
            "头部", "身体", "腿部", "手部", "主弹药", "副弹药", "道具 · 一", "道具 · 二", "道具 · 三"
        };
        private UIDocument m_document;
        private PanelSettings m_panel;
        private PlayerUIEquipmentManager m_owner;
        private PlayerInventoryManager m_inventory;
        private VisualElement m_root;
        private InventoryItemGrid m_grid;
        private TextField m_search;
        private DropdownField m_sort;
        private readonly List<Item> m_items = new();
        private readonly List<Button> m_slots = new();
        private Item m_selectedItem;
        private bool m_isBrowsingItems;
        private IVisualElementScheduledItem m_pendingRefresh;
        private InventoryCategory m_category;
        private VisualElement m_statsColumn;
        private ItemDetailDisplay m_display;

        public bool IsBrowsingItems => m_isBrowsingItems;
        public bool IsEditingText => m_search != null &&
            (m_root?.panel?.focusController.focusedElement == m_search ||
             m_root?.panel?.focusController.focusedElement is VisualElement focused && m_search.Contains(focused));

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            m_inventory = PlayerUIManager.Instance?.LocalPlayer?.InventoryManager;
            if (m_inventory != null)
            {
                m_inventory.InventoryChanged += ScheduleRefresh;
            }
            BindDocument();
            ShowOverview();
        }

        private void OnDisable()
        {
            if (m_inventory != null)
            {
                m_inventory.InventoryChanged -= ScheduleRefresh;
            }
            m_display?.Dispose();
            m_display = null;
            m_pendingRefresh?.Pause();
            m_pendingRefresh = null;
            m_inventory = null;
            m_root = null;
            m_slots.Clear();
            m_items.Clear();
            m_selectedItem = null;
            m_grid = null;
            m_isBrowsingItems = false;
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

        /// <summary>Creates the native view beneath its existing menu lifecycle boundary.</summary>
        public static EquipmentMenuView Create(Transform parent, PlayerUIEquipmentManager owner, InventoryCategory category)
        {
            string resource = category == InventoryCategory.Weapons ? "UI/WeaponMenu" : "UI/EquipmentMenu";
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>(resource);
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Equipment Toolkit assets are missing.");
            }
            GameObject instance = new GameObject(category == InventoryCategory.Weapons ?
                "Weapon Menu Toolkit" : "Equipment Menu Toolkit");
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
            EquipmentMenuView view = instance.AddComponent<EquipmentMenuView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            view.m_category = category;
            instance.SetActive(true);
            return view;
        }

        /// <summary>Replays the current inventory and equipment snapshot without mutating it.</summary>
        public void Refresh()
        {
            if (m_root == null)
            {
                return;
            }
            RefreshSlots();
            PlayerManager player = PlayerUIManager.Instance?.LocalPlayer;
            if (player != null)
            {
                CharacterStatusPresentation.PopulateSummary(m_root.Q("equipment-character-stats"), player);
            }
            if (m_isBrowsingItems)
            {
                RefreshCandidates();
            }
            else
            {
                ShowDetails(m_inventory?.GetEquipmentSlotItem(m_owner.CurrentSelectedEquipmentSlot));
            }
            RefreshActions();
        }

        /// <summary>Returns to equipped slots and clears the active candidate selection.</summary>
        public void ShowOverview()
        {
            if (m_root == null)
            {
                return;
            }
            m_isBrowsingItems = false;
            RefreshLayout();
            ClearCandidates();
            m_root.Q<Label>("inventory-heading").text = "选择装备栏";
            m_root.Q<Label>("inventory-empty").text = "选择左侧装备栏，查看可装备的物品。";
            m_root.Q("inventory-empty").style.display = DisplayStyle.Flex;
            SetFeedback("选择装备栏，查看适用物品。");
            Refresh();
        }

        /// <summary>Displays items compatible with the controller's currently selected equipment slot.</summary>
        public void ShowCandidates()
        {
            m_isBrowsingItems = true;
            RefreshLayout();
            m_search.SetValueWithoutNotify(string.Empty);
            m_root.Q<Label>("inventory-heading").text = s_slotNames[(int)m_owner.CurrentSelectedEquipmentSlot];
            Refresh();
            FocusList();
        }

        /// <summary>Releases candidate references without destroying inventory-owned objects.</summary>
        public void ClearCandidates()
        {
            m_selectedItem = null;
            m_items.Clear();
            m_grid?.Refresh(-1);
        }

        /// <summary>Restores native focus after a transaction unless another list was opened meanwhile.</summary>
        public void FocusCurrentSlot()
        {
            int index = (int)m_owner.CurrentSelectedEquipmentSlot;
            if (m_root == null || index < 0 || index >= m_slots.Count)
            {
                return;
            }
            EventSystem.current?.SetSelectedGameObject(null);
            Button target = m_slots[index];
            m_root.schedule.Execute(() =>
            {
                if (target.panel != null && gameObject.activeInHierarchy && !m_isBrowsingItems)
                {
                    target.Focus();
                    m_root.Q<ScrollView>("equipment-scroll").ScrollTo(target);
                }
            });
        }

        private void BindDocument()
        {
            m_display = new ItemDetailDisplay(m_root);
            VisualElement slots = m_root.Q("equipment-slots");
            slots.Clear();
            m_slots.Clear();
            for (int index = 0; index < s_slotNames.Length; index++)
            {
                int slotIndex = index;
                Button button = new Button(() => m_owner.SelectEquipmentSlot(slotIndex));
                button.name = "equipment-slot-" + index;
                button.AddToClassList("equipment-slot");
                var icon = new Image { name = "slot-icon", pickingMode = PickingMode.Ignore };
                icon.AddToClassList("equipment-slot__icon");
                button.Add(icon);
                var title = new Label(s_slotNames[index]) { pickingMode = PickingMode.Ignore };
                title.AddToClassList("equipment-slot__title");
                button.Add(title);
                var quantity = new Label { name = "slot-quantity", pickingMode = PickingMode.Ignore };
                quantity.AddToClassList("equipment-slot__quantity");
                button.Add(quantity);
                button.RegisterCallback<FocusInEvent>(_ =>
                {
                    m_owner.HighlightEquipmentSlot(slotIndex);
                    ShowOverview();
                    m_root.Q<ScrollView>("equipment-scroll").ScrollTo(button);
                });
                m_slots.Add(button);
                if (m_category == InventoryCategory.Weapons ? index < 6 : index >= 6 && index < 10)
                {
                    slots.Add(button);
                }
            }
            m_search = m_root.Q<TextField>("item-search");
            m_search.RegisterValueChangedCallback(_ => RefreshCandidates());
            m_sort = m_root.Q<DropdownField>("item-sort");
            m_sort.choices = new List<string> { "名称", "重量", "数量" };
            m_sort.SetValueWithoutNotify(m_sort.choices[0]);
            m_sort.RegisterValueChangedCallback(_ => RefreshCandidates());
            ListView list = m_root.Q<ListView>("inventory-list");
            m_grid = new InventoryItemGrid(list, m_items, index =>
            {
                m_selectedItem = index >= 0 ? m_items[index] : null;
                ShowDetails(m_selectedItem);
                RefreshActions();
            }, EquipSelected);
            m_root.Q<Button>("equip").clicked += EquipSelected;
            m_root.Q<Button>("unequip").clicked += m_owner.UnequipSelectedItem;
            m_root.Q<Button>("back").clicked += () =>
            {
                if (m_isBrowsingItems)
                {
                    m_owner.CloseEquipmentInventoryWindow();
                }
                else
                {
                    PlayerUIManager.Instance.PlayerUICharacterMenuManager.OpenCharacterMenu();
                }
            };
        }

        private void RefreshSlots()
        {
            for (int index = 0; index < m_slots.Count; index++)
            {
                Item item = m_inventory?.GetEquipmentSlotItem((EquipmentSlotType)index);
                bool isEmpty = item == null || item is WeaponItem weapon && weapon.IsUnarmed;
                m_slots[index].Q<Image>("slot-icon").sprite = isEmpty ? null : item.ItemIcon;
                m_slots[index].Q<Label>("slot-quantity").text = isEmpty ? "空" : GetQuantityText(item);
                m_slots[index].EnableInClassList("equipment-slot--selected",
                    index == (int)m_owner.CurrentSelectedEquipmentSlot);
                m_slots[index].tooltip = isEmpty ? s_slotNames[index] : item.ItemName;
            }
        }

        private void RefreshLayout()
        {
            m_root.Q(className: "equipment-menu").EnableInClassList("equipment-menu--browsing", m_isBrowsingItems);
            m_root.Q("switch-display").style.display = m_isBrowsingItems ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshCandidates()
        {
            if (m_root == null || !m_isBrowsingItems)
            {
                return;
            }
            Item selected = m_selectedItem;
            m_items.Clear();
            string query = m_search.value?.Trim() ?? string.Empty;
            if (m_inventory != null)
            {
                foreach (Item item in m_inventory.ItemsInInventory.Append(
                    m_inventory.GetEquipmentSlotItem(m_owner.CurrentSelectedEquipmentSlot)).Distinct())
                {
                    if (!ItemInventoryFilter.Matches(item, m_category) ||
                        !PlayerUIEquipmentManager.IsItemCompatibleWithSlot(
                            item, m_owner.CurrentSelectedEquipmentSlot) ||
                        query.Length > 0 && (item.ItemName ?? string.Empty).IndexOf(
                            query, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    m_items.Add(item);
                }
            }
            m_items.Sort(CompareItems);
            int selectedIndex = selected != null ? m_items.IndexOf(selected) : -1;
            if (selectedIndex < 0 && m_items.Count > 0)
            {
                selectedIndex = 0;
            }
            m_grid.Refresh(selectedIndex);
            m_selectedItem = selectedIndex >= 0 ? m_items[selectedIndex] : null;
            m_root.Q("inventory-empty").style.display = m_items.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_root.Q<Label>("inventory-empty").text = query.Length > 0 ? "没有匹配的物品。" : "没有可用的物品。";
            ShowDetails(m_selectedItem);
            RefreshActions();
        }

        private int CompareItems(Item left, Item right)
        {
            int comparison = m_sort.index switch
            {
                1 => GetWeight(left).CompareTo(GetWeight(right)),
                2 => GetQuantity(right).CompareTo(GetQuantity(left)),
                _ => StringComparer.CurrentCultureIgnoreCase.Compare(left.ItemName, right.ItemName)
            };
            return comparison != 0 ? comparison : left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private void RefreshActions()
        {
            if (m_root == null)
            {
                return;
            }
            Item equipped = m_inventory?.GetEquipmentSlotItem(m_owner.CurrentSelectedEquipmentSlot);
            m_root.Q<Button>("equip").SetEnabled(m_isBrowsingItems && m_selectedItem != null);
            m_root.Q<Button>("unequip").SetEnabled(equipped != null &&
                !(equipped is WeaponItem weapon && weapon.IsUnarmed));
        }

        private void EquipSelected()
        {
            if (!m_isBrowsingItems || m_selectedItem == null)
            {
                return;
            }
            m_owner.EquipItem(m_selectedItem);
            if (m_isBrowsingItems)
            {
                Refresh();
                SetFeedback("无法装备该物品，请检查物品是否仍在背包中。 ");
            }
        }

        private void ShowDetails(Item item)
        {
            if (m_root == null)
            {
                return;
            }
            Label name = m_root.Q<Label>("detail-name");
            name.enableRichText = false;
            name.text = item != null ? item.ItemName : "尚未选择物品";
            m_root.Q<Image>("detail-icon").sprite = item != null ? item.ItemIcon : null;
            m_root.Q<Label>("detail-description").text = item != null ? item.ItemDescription : "";
            m_root.Q<Label>("detail-quantity").text = item != null ? GetQuantityText(item) : "";
            m_root.Q<Label>("detail-requirements").text = item is WeaponItem weapon ?
                $"需求  力量 {weapon.StrengthRequirement} · 灵巧 {weapon.DexterityRequirement}\n" +
                $"智力 {weapon.IntelligenceRequirement} · 信仰 {weapon.FaithRequirement}" : "";
            VisualElement stats = m_root.Q("comparison-stats");
            stats.Clear();
            m_statsColumn = stats;
            Item current = m_inventory?.GetEquipmentSlotItem(m_owner.CurrentSelectedEquipmentSlot);
            bool compare = m_isBrowsingItems;
            m_root.Q<Label>("comparison-heading").text = compare ?
                $"对比：{(current != null ? current.ItemName : "空栏位")}" : "物品属性";
            if (item is EquipmentItem)
            {
                AddStat("重量", GetWeight(item), GetWeight(current), false, compare);
            }
            if (item is WeaponItem selectedWeapon)
            {
                WeaponItem currentWeapon = current as WeaponItem;
                VisualElement columns = CreateStatColumns(stats);
                m_statsColumn = AddStatColumn(columns, "攻击力");
                AddDamage("物理攻击", DamageType.Physical, selectedWeapon, currentWeapon, compare);
                AddDamage("魔力攻击", DamageType.Magic, selectedWeapon, currentWeapon, compare);
                AddDamage("火焰攻击", DamageType.Fire, selectedWeapon, currentWeapon, compare);
                AddDamage("雷电攻击", DamageType.Lightning, selectedWeapon, currentWeapon, compare);
                AddDamage("神圣攻击", DamageType.Holy, selectedWeapon, currentWeapon, compare);
                m_statsColumn = AddStatColumn(columns, "防御时减伤率");
                AddStat("格挡物理减伤", selectedWeapon.BlockingPhysicalAbsorption,
                    currentWeapon?.BlockingPhysicalAbsorption ?? 0f, true, compare);
                AddStat("格挡魔力减伤", selectedWeapon.BlockingMagicAbsorption,
                    currentWeapon?.BlockingMagicAbsorption ?? 0f, true, compare);
                AddStat("格挡火减伤", selectedWeapon.BlockingFireAbsorption,
                    currentWeapon?.BlockingFireAbsorption ?? 0f, true, compare);
                AddStat("格挡雷减伤", selectedWeapon.BlockingLightningAbsorption,
                    currentWeapon?.BlockingLightningAbsorption ?? 0f, true, compare);
                AddStat("格挡圣减伤", selectedWeapon.BlockingHolyAbsorption,
                    currentWeapon?.BlockingHolyAbsorption ?? 0f, true, compare);
                AddStat("防御强度", selectedWeapon.BlockingStability,
                    currentWeapon?.BlockingStability ?? 0f, true, compare);
            }
            if (item is ArmorItem armor)
            {
                ArmorItem currentArmor = current as ArmorItem;
                VisualElement columns = CreateStatColumns(stats);
                m_statsColumn = AddStatColumn(columns, "减伤率");
                AddStat("物理减伤 %", armor.PhysicalAbsorption, currentArmor?.PhysicalAbsorption ?? 0f, true, compare);
                AddStat("魔力减伤 %", armor.MagicAbsorption, currentArmor?.MagicAbsorption ?? 0f, true, compare);
                AddStat("火焰减伤 %", armor.FireAbsorption, currentArmor?.FireAbsorption ?? 0f, true, compare);
                AddStat("雷电减伤 %", armor.LightningAbsorption, currentArmor?.LightningAbsorption ?? 0f, true, compare);
                AddStat("神圣减伤 %", armor.HolyAbsorption, currentArmor?.HolyAbsorption ?? 0f, true, compare);
                m_statsColumn = AddStatColumn(columns, "抵抗力");
                AddStat("强韧", armor.Poise, currentArmor?.Poise ?? 0f, true, compare);
                AddStat("免疫力", armor.Immunity, currentArmor?.Immunity ?? 0f, true, compare);
                AddStat("健壮度", armor.Robustness, currentArmor?.Robustness ?? 0f, true, compare);
                AddStat("理智度", armor.Focus, currentArmor?.Focus ?? 0f, true, compare);
                AddStat("抗死度", armor.Vitality, currentArmor?.Vitality ?? 0f, true, compare);
            }
        }

        private static VisualElement CreateStatColumns(VisualElement parent)
        {
            var columns = new VisualElement();
            columns.AddToClassList("equipment-stat-columns");
            parent.Add(columns);
            return columns;
        }

        private static VisualElement AddStatColumn(VisualElement columns, string title)
        {
            var column = new VisualElement();
            column.AddToClassList("equipment-stat-column");
            var heading = new Label(title);
            heading.AddToClassList("equipment-stat-heading");
            column.Add(heading);
            columns.Add(column);
            return column;
        }

        private void AddDamage(string label, DamageType channel, WeaponItem item, WeaponItem current, bool compare)
        {
            PlayerManager player = PlayerUIManager.Instance?.LocalPlayer;
            AddStat(label, item.GetChannelAttackPower(channel, player),
                current != null ? current.GetChannelAttackPower(channel, player) : 0f, true, compare);
        }

        private void AddStat(string title, float candidate, float current, bool higherIsBetter, bool compare)
        {
            var row = new VisualElement();
            row.AddToClassList("equipment-stat");
            var label = new Label(title);
            label.AddToClassList("equipment-stat__label");
            row.Add(label);
            float difference = candidate - current;
            string text = $"{candidate:0.#}";
            var value = new Label(text);
            value.AddToClassList("equipment-stat__value");
            if (compare && Mathf.Abs(difference) > 0.01f)
            {
                value.AddToClassList((difference > 0f) == higherIsBetter ?
                    "equipment-stat__value--better" : "equipment-stat__value--worse");
            }
            row.Add(value);
            m_statsColumn.Add(row);
        }

        private void ScheduleRefresh()
        {
            m_pendingRefresh?.Pause();
            m_pendingRefresh = m_root?.schedule.Execute(Refresh);
        }

        private void FocusList()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            m_root.schedule.Execute(() =>
            {
                if (m_root != null && m_isBrowsingItems)
                {
                    if (m_items.Count > 0)
                    {
                        m_grid.FocusSelected();
                    }
                    else
                    {
                        m_root.Q<Button>("back").Focus();
                    }
                }
            });
        }

        private void SetFeedback(string text)
        {
            m_root.Q<Label>("equipment-feedback").text = text;
        }

        private static float GetWeight(Item item) => item is EquipmentItem equipment ? equipment.ItemWeight : 0f;

        private static int GetQuantity(Item item)
        {
            if (item is RangedProjectileItem projectile)
            {
                return projectile.CurrentAmmoAmount;
            }
            if (item is QuickSlotItem quickSlot && quickSlot.IsConsumable)
            {
                return quickSlot.GetCurrentAmount(PlayerUIManager.Instance?.LocalPlayer);
            }
            return item.CurrentItemAmount;
        }

        private static string GetQuantityText(Item item)
        {
            if (item is WeaponItem weapon)
            {
                return $"强化 +{(int)weapon.UpgradeLevel}";
            }
            if (item is RangedProjectileItem || item is QuickSlotItem quickSlot && quickSlot.IsConsumable || item.IsStackable)
            {
                return $"数量 {GetQuantity(item)}";
            }
            return "";
        }
    }
}
