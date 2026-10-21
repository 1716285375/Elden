using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Native paired container lists with explicit whole-entry transfer commands.</summary>
    public sealed class StorageMenuView : MonoBehaviour
    {
        private readonly List<Item> m_inventoryItems = new();
        private readonly List<Item> m_storageItems = new();
        private UIDocument m_document;
        private PanelSettings m_panel;
        private PlayerUIStorageManager m_owner;
        private VisualTreeAsset m_rowTemplate;
        private VisualElement m_root;
        private ListView m_inventoryList;
        private ListView m_storageList;

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            m_inventoryList = ConfigureList("inventory-items", m_inventoryItems, true);
            m_storageList = ConfigureList("storage-items", m_storageItems, false);
            Bind("all", () => m_owner.SetCategory(ShopItemCategory.All));
            Bind("armor", () => m_owner.SetCategory(ShopItemCategory.Armor));
            Bind("weapons", () => m_owner.SetCategory(ShopItemCategory.Weapons));
            Bind("back", m_owner.ReturnToSiteOfGrace);
            Bind("transfer", () => m_owner.SwapItemLocation(m_owner.CurrentItem,
                m_owner.IsSelectingFromPlayerInventory));
        }

        private void OnDisable()
        {
            m_inventoryItems.Clear();
            m_storageItems.Clear();
            m_inventoryList = null;
            m_storageList = null;
            m_root = null;
        }

        private void OnDestroy()
        {
            if (m_panel == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(m_panel);
            }
            else
            {
                DestroyImmediate(m_panel);
            }
        }

        /// <summary>Creates the native storage document without legacy runtime controls.</summary>
        public static StorageMenuView Create(Transform parent, PlayerUIStorageManager owner)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/StorageMenu");
            VisualTreeAsset row = Resources.Load<VisualTreeAsset>("UI/InventoryItemRow");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || row == null || theme == null)
            {
                throw new InvalidOperationException("Storage Toolkit assets are missing.");
            }
            GameObject instance = new GameObject("Storage Menu Toolkit");
            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 500;
            UIDocument document = instance.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;
            document.gameObject.AddComponent<MenuAudioFeedback>();
            StorageMenuView view = instance.AddComponent<StorageMenuView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            view.m_rowTemplate = row;
            instance.SetActive(true);
            return view;
        }

        /// <summary>Refreshes both containers after a completed transfer and keeps a valid source selection.</summary>
        public void RefreshItems(IReadOnlyList<Item> inventory, IReadOnlyList<Item> storage)
        {
            if (m_root == null)
            {
                return;
            }
            bool fromInventory = m_owner.IsSelectingFromPlayerInventory;
            Item previous = m_owner.CurrentItem;
            int previousIndex = fromInventory ? m_inventoryList.selectedIndex : m_storageList.selectedIndex;
            Fill(m_inventoryItems, inventory);
            Fill(m_storageItems, storage);
            m_inventoryList.RefreshItems();
            m_storageList.RefreshItems();
            if ((fromInventory ? m_inventoryItems : m_storageItems).Count == 0)
            {
                fromInventory = !fromInventory;
            }
            List<Item> items = fromInventory ? m_inventoryItems : m_storageItems;
            int index = previous != null ? items.IndexOf(previous) : -1;
            if (index < 0 && items.Count > 0)
            {
                index = Mathf.Clamp(previousIndex, 0, items.Count - 1);
            }
            m_inventoryList.SetSelectionWithoutNotify(fromInventory && index >= 0 ? new[] { index } : Array.Empty<int>());
            m_storageList.SetSelectionWithoutNotify(!fromInventory && index >= 0 ? new[] { index } : Array.Empty<int>());
            m_owner.SelectItem(index >= 0 ? items[index] : null, fromInventory);
            m_root.Q<Label>("inventory-count").text = $"{m_inventoryItems.Count} 组物品";
            m_root.Q<Label>("storage-count").text = $"{m_storageItems.Count} 组物品";
            m_root.Q("inventory-empty").style.display = m_inventoryItems.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_root.Q("storage-empty").style.display = m_storageItems.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            string[] categories = { "all", "armor", "weapons" };
            for (int category = 0; category < categories.Length; category++)
            {
                m_root.Q(categories[category]).EnableInClassList("menu-tab--selected",
                    category == (int)m_owner.CurrentCategory);
            }
        }

        /// <summary>Shows the selected source and quantity before the explicit transfer.</summary>
        public void ShowDetails(Item item, bool fromInventory)
        {
            if (m_root == null)
            {
                return;
            }
            m_root.Q<Label>("selected-item").text = item == null ? "此分类暂无物品" :
                $"{item.ItemName}  ·  {GetAmount(item)} 件  ·  {(fromInventory ? "背包 → 仓库" : "仓库 → 背包")}";
            Button transfer = m_root.Q<Button>("transfer");
            transfer.text = fromInventory ? "存入整组 →" : "← 取出整组";
            transfer.SetEnabled(item != null);
        }

        /// <summary>Focuses the selected container, or the return command when both are empty.</summary>
        public void FocusSelection()
        {
            Focus(m_owner.CurrentItem == null ? "back" :
                m_owner.IsSelectingFromPlayerInventory ? "inventory-items" : "storage-items");
        }

        /// <summary>Displays a successful transfer or a capacity rejection.</summary>
        public void SetFeedback(string message)
        {
            if (m_root != null)
            {
                m_root.Q<Label>("storage-feedback").text = message;
            }
        }

        private ListView ConfigureList(string name, List<Item> items, bool fromInventory)
        {
            ListView list = m_root.Q<ListView>(name);
            list.itemsSource = items;
            list.fixedItemHeight = 76;
            list.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            list.selectionType = SelectionType.Single;
            list.makeItem = () => m_rowTemplate.CloneTree();
            list.bindItem = (row, index) =>
            {
                Item item = items[index];
                row.Q<Image>("item-icon").sprite = item.ItemIcon;
                row.Q<Label>("item-name").text = item is WeaponItem weapon && (int)weapon.UpgradeLevel > 0 ?
                    $"{item.ItemName} +{(int)weapon.UpgradeLevel}" : item.ItemName;
                row.Q<Label>("item-quantity").text = $"数量 {GetAmount(item)}";
            };
            list.selectionChanged += selection =>
            {
                foreach (object entry in selection)
                {
                    (fromInventory ? m_storageList : m_inventoryList)?.SetSelectionWithoutNotify(Array.Empty<int>());
                    m_owner.SelectItem(entry as Item, fromInventory);
                    return;
                }
            };
            list.RegisterCallback<FocusInEvent>(_ =>
            {
                if (list.selectedIndex < 0 && items.Count > 0)
                {
                    list.SetSelection(0);
                }
            });
            list.itemsChosen += _ => Focus("transfer");
            return list;
        }

        private void Fill(List<Item> target, IReadOnlyList<Item> source)
        {
            target.Clear();
            foreach (Item item in source)
            {
                if (PlayerUIShopManager.MatchesCategory(item, m_owner.CurrentCategory))
                {
                    target.Add(item);
                }
            }
        }

        private static int GetAmount(Item item)
        {
            return item switch
            {
                RangedProjectileItem projectile => projectile.CurrentAmmoAmount,
                QuickSlotItem quickSlot when quickSlot.IsConsumable =>
                    quickSlot.GetCurrentAmount(PlayerUIManager.Instance?.LocalPlayer),
                _ => item.IsStackable ? item.CurrentItemAmount : 1
            };
        }

        private void Bind(string name, Action action)
        {
            m_root.Q<Button>(name).clicked += action;
        }

        private void Focus(string name)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            VisualElement root = m_root;
            root.schedule.Execute(() =>
            {
                if (m_root == root && isActiveAndEnabled)
                {
                    root.Q(name)?.Focus();
                }
            });
        }
    }
}
