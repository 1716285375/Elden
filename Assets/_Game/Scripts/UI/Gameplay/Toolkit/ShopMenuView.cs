using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Native merchant context and virtualized stock presentation; transactions stay in the manager.</summary>
    public sealed class ShopMenuView : MonoBehaviour
    {
        private readonly List<Item> m_items = new();
        private UIDocument m_document;
        private PanelSettings m_panel;
        private PlayerUIShopManager m_owner;
        private VisualElement m_root;
        private ListView m_list;
        private InventoryItemGrid m_grid;
        private Item m_pendingTrade;

        public bool IsConfirmationOpen => m_pendingTrade != null;

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            Bind("talk", m_owner.Talk);
            Bind("buy", m_owner.OpenBuyMenu);
            Bind("sell", m_owner.OpenSellMenu);
            Bind("smith", m_owner.OpenSmithService);
            Bind("leave", m_owner.CloseMenu);
            Bind("back", m_owner.NavigateBack);
            Bind("all", m_owner.SetAllCategory);
            Bind("armor", m_owner.SetArmorCategory);
            Bind("weapons", m_owner.SetWeaponsCategory);
            Bind("trade", RequestTrade);
            Bind("confirm-trade", ConfirmTrade);
            Bind("cancel-trade", CancelTrade);
            m_list = m_root.Q<ListView>("shop-items");
            m_grid = new InventoryItemGrid(m_list, m_items,
                index => m_owner.SelectItem(index >= 0 ? m_items[index] : null), RequestTrade,
                item => $"{m_owner.GetDisplayedValue(item):N0}");
            m_pendingTrade = null;
            m_root.Q("shop-trading").SetEnabled(true);
            m_root.Q("trade-confirmation").style.display = DisplayStyle.None;
        }

        private void OnDisable()
        {
            m_pendingTrade = null;
            m_grid = null;
            m_items.Clear();
            m_list = null;
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

        /// <summary>Creates the document under the existing menu input ownership boundary.</summary>
        public static ShopMenuView Create(Transform parent, PlayerUIShopManager owner)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/ShopMenu");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Shop Toolkit assets are missing.");
            }
            GameObject instance = new GameObject("Shop Menu Toolkit");
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
            ShopMenuView view = instance.AddComponent<ShopMenuView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            instance.SetActive(true);
            return view;
        }

        /// <summary>Shows only services that the interacting NPC actually provides.</summary>
        public void ShowContext(bool isShop, bool isSmith)
        {
            m_root.Q("shop-context").style.display = DisplayStyle.Flex;
            m_root.Q("shop-trading").style.display = DisplayStyle.None;
            m_root.Q<Label>("merchant-name").text = isSmith ? "锻造师" : "旅途中的相逢";
            m_root.Q("buy").style.display = isShop ? DisplayStyle.Flex : DisplayStyle.None;
            m_root.Q("sell").style.display = isShop ? DisplayStyle.Flex : DisplayStyle.None;
            m_root.Q("smith").style.display = isSmith ? DisplayStyle.Flex : DisplayStyle.None;
            Focus("talk");
        }

        /// <summary>Opens the stock browser with a fresh interaction hint.</summary>
        public void ShowTrading()
        {
            m_root.Q("shop-context").style.display = DisplayStyle.None;
            m_root.Q("shop-trading").style.display = DisplayStyle.Flex;
            SetFeedback("选择物品查看详情，使用右侧按钮确认交易。");
            m_grid.FocusSelected();
        }

        /// <summary>Rebuilds the filtered snapshot while preserving the selected item or its nearest row.</summary>
        public void RefreshItems(IReadOnlyList<Item> source)
        {
            if (m_root == null)
            {
                return;
            }
            Item previous = m_owner.CurrentItem;
            int previousIndex = m_grid.SelectedIndex;
            m_items.Clear();
            bool buying = m_owner.CurrentMode == PlayerUIShopManager.ShopBuyOrSell.Buying;
            if (source != null)
            {
                foreach (Item item in source)
                {
                    if (PlayerUIShopManager.MatchesCategory(item, m_owner.CurrentCategory) &&
                        (buying ? item.IsInfinite || item.ShopStockAmount > 0 :
                            AICharacterInventoryManager.CanSellItem(item)))
                    {
                        m_items.Add(item);
                    }
                }
            }
            m_root.Q<Label>("shop-heading").text = buying ? "选购补给" : "出售物品";
            m_root.Q<Label>("shop-count").text = $"{m_items.Count} 件可交易物品";
            m_root.Q("shop-empty").style.display = m_items.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            string[] categories = { "all", "armor", "weapons" };
            for (int index = 0; index < categories.Length; index++)
            {
                m_root.Q(categories[index]).EnableInClassList("menu-tab--selected",
                    index == (int)m_owner.CurrentCategory);
            }
            int selected = previous != null ? m_items.IndexOf(previous) : -1;
            if (selected < 0 && m_items.Count > 0)
            {
                selected = Mathf.Clamp(previousIndex, 0, m_items.Count - 1);
            }
            m_grid.Refresh(selected);
            CharacterStatusPresentation.PopulateSummary(m_root.Q("shop-character-stats"),
                PlayerUIManager.Instance.LocalPlayer);
        }

        /// <summary>Shows exact backend pricing, including whole-stack sale quantity.</summary>
        public void ShowDetails(Item item, int runes)
        {
            if (m_root == null)
            {
                return;
            }
            bool buying = m_owner.CurrentMode == PlayerUIShopManager.ShopBuyOrSell.Buying;
            int price = m_owner.GetDisplayedValue(item);
            m_root.Q<Label>("runes").text = $"持有卢恩  {runes:N0}";
            m_root.Q<Label>("item-title").text = item != null ? item.ItemName : "暂无可交易物品";
            m_root.Q<Image>("item-portrait").sprite = item != null ? item.ItemIcon : null;
            m_root.Q<Label>("item-price").text = item != null ? $"{price:N0} 卢恩" : "—";
            m_root.Q("item-price").EnableInClassList("shop-menu__price--unaffordable", buying && price > runes);
            m_root.Q<Label>("item-info").text = item == null ? "请选择其他分类。" : buying ?
                $"每次购买 1 件\n商店库存：{StockText(item)}\n重量：{GetWeight(item):0.0}" :
                $"本次出售 {m_owner.GetDisplayedAmount(item)} 件\n将移除整组物品，价格为整组总价。\n重量：{GetWeight(item):0.0}";
            Button trade = m_root.Q<Button>("trade");
            trade.text = buying ? "购买 1 件" : $"出售整组 · {m_owner.GetDisplayedAmount(item)} 件";
            trade.SetEnabled(item != null && (!buying || price <= runes &&
                (item.IsInfinite || item.ShopStockAmount > 0)));
        }

        /// <summary>Reports a completed or rejected transaction without moving list focus.</summary>
        public void SetFeedback(string message)
        {
            if (m_root != null)
            {
                m_root.Q<Label>("shop-feedback").text = message;
            }
        }

        private void Bind(string name, Action action)
        {
            m_root.Q<Button>(name).clicked += action;
        }

        private static float GetWeight(Item item) => item is EquipmentItem equipment ? equipment.ItemWeight : 0f;

        private string StockText(Item item)
        {
            return m_owner.CurrentMode == PlayerUIShopManager.ShopBuyOrSell.Buying && item.IsInfinite ?
                "不限量" : m_owner.GetDisplayedAmount(item).ToString();
        }

        /// <summary>Closes only the pending transaction and restores its item focus.</summary>
        public void CancelTrade()
        {
            m_pendingTrade = null;
            m_root.Q("trade-confirmation").style.display = DisplayStyle.None;
            m_root.Q("shop-trading").SetEnabled(true);
            m_grid.FocusSelected();
        }

        private void RequestTrade()
        {
            Item item = m_owner.CurrentItem;
            if (IsConfirmationOpen || item == null || !m_root.Q<Button>("trade").enabledSelf)
            {
                return;
            }
            m_pendingTrade = item;
            bool buying = m_owner.CurrentMode == PlayerUIShopManager.ShopBuyOrSell.Buying;
            m_root.Q<Label>("trade-confirmation-description").text =
                $"{(buying ? "购买" : "出售")} {item.ItemName} × {(buying ? 1 : m_owner.GetDisplayedAmount(item))}\n" +
                $"{(buying ? "所需卢恩" : "获得卢恩")}  {m_owner.GetDisplayedValue(item):N0}";
            m_root.Q("shop-trading").SetEnabled(false);
            m_root.Q("trade-confirmation").style.display = DisplayStyle.Flex;
            Focus("cancel-trade");
        }

        private void ConfirmTrade()
        {
            Item item = m_pendingTrade;
            if (item == null)
            {
                return;
            }
            CancelTrade();
            m_owner.BuyOrSellItem(item);
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
