using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Virtualizes inventory rows while preserving two-dimensional keyboard and gamepad navigation.</summary>
    internal sealed class InventoryItemGrid
    {
        private readonly int m_columns;
        private readonly ListView m_list;
        private readonly IReadOnlyList<Item> m_items;
        private readonly Action<int> m_selected;
        private readonly Action m_chosen;
        private readonly Func<Item, string> m_caption;
        private readonly List<int> m_rows = new();
        private int m_selectedIndex = -1;

        public InventoryItemGrid(ListView list, IReadOnlyList<Item> items, Action<int> selected, Action chosen,
            Func<Item, string> caption = null, int columns = 5)
        {
            m_columns = Mathf.Max(1, columns);
            m_list = list;
            m_items = items;
            m_selected = selected;
            m_chosen = chosen;
            m_caption = caption ?? GetAmount;
            list.selectionType = SelectionType.None;
            list.fixedItemHeight = 94f;
            list.itemsSource = m_rows;
            list.makeItem = CreateRow;
            list.bindItem = BindRow;
        }

        public int SelectedIndex => m_selectedIndex;

        public void Refresh(int selectedIndex)
        {
            m_rows.Clear();
            for (int index = 0; index < (m_items.Count + m_columns - 1) / m_columns; index++)
            {
                m_rows.Add(index);
            }
            m_list.RefreshItems();
            Select(selectedIndex);
        }

        public void FocusSelected()
        {
            if (m_selectedIndex < 0)
            {
                return;
            }
            m_list.ScrollToItem(m_selectedIndex / m_columns);
            m_list.schedule.Execute(() => m_list.Q<Button>("item-grid-cell-" + m_selectedIndex)?.Focus());
        }

        private VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.AddToClassList("item-grid__row");
            for (int column = 0; column < m_columns; column++)
            {
                var cell = new Button();
                cell.AddToClassList("item-grid__cell");
                cell.style.width = Length.Percent(100f / m_columns);
                var icon = new Image { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("item-grid__icon");
                cell.Add(icon);
                var amount = new Label { pickingMode = PickingMode.Ignore };
                amount.AddToClassList("item-grid__amount");
                cell.Add(amount);
                var equipped = new VisualElement { name = "equipped-mark", pickingMode = PickingMode.Ignore };
                equipped.AddToClassList("item-grid__equipped");
                cell.Add(equipped);
                cell.clicked += () => Select((int)cell.userData);
                cell.RegisterCallback<FocusInEvent>(_ => Select((int)cell.userData));
                cell.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.clickCount == 2)
                    {
                        m_chosen();
                    }
                });
                cell.RegisterCallback<NavigationSubmitEvent>(evt =>
                {
                    Select((int)cell.userData);
                    evt.StopImmediatePropagation();
                    m_chosen();
                }, TrickleDown.TrickleDown);
                cell.RegisterCallback<NavigationMoveEvent>(evt => Move(cell, evt));
                row.Add(cell);
            }
            return row;
        }

        private void BindRow(VisualElement row, int rowIndex)
        {
            for (int column = 0; column < m_columns; column++)
            {
                int index = rowIndex * m_columns + column;
                var cell = (Button)row[column];
                bool hasItem = index < m_items.Count;
                cell.userData = index;
                cell.name = "item-grid-cell-" + index;
                cell.style.visibility = hasItem ? Visibility.Visible : Visibility.Hidden;
                cell.SetEnabled(hasItem);
                cell.EnableInClassList("item-grid__cell--selected", index == m_selectedIndex);
                cell.Q<Image>().sprite = hasItem ? m_items[index].ItemIcon : null;
                cell.tooltip = hasItem ? m_items[index].ItemName : string.Empty;
                cell.Q<Label>().text = hasItem ? m_caption(m_items[index]) : string.Empty;
                cell.Q("equipped-mark").style.display = hasItem && IsEquipped(m_items[index]) ?
                    DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void Select(int index)
        {
            m_selectedIndex = index >= 0 && index < m_items.Count ? index : -1;
            m_list.Query<Button>(className: "item-grid__cell").ForEach(cell =>
                cell.EnableInClassList("item-grid__cell--selected", (int)cell.userData == m_selectedIndex));
            m_selected(m_selectedIndex);
        }

        private void Move(Button cell, NavigationMoveEvent evt)
        {
            int offset = evt.direction switch
            {
                NavigationMoveEvent.Direction.Left => -1,
                NavigationMoveEvent.Direction.Right => 1,
                NavigationMoveEvent.Direction.Up => -m_columns,
                NavigationMoveEvent.Direction.Down => m_columns,
                _ => 0
            };
            if (offset == 0)
            {
                return;
            }
            evt.StopPropagation();
            Select(Mathf.Clamp((int)cell.userData + offset, 0, m_items.Count - 1));
            FocusSelected();
        }

        private static string GetAmount(Item item)
        {
            if (item is WeaponItem weapon)
            {
                return (int)weapon.UpgradeLevel > 0 ? $"+{(int)weapon.UpgradeLevel}" : string.Empty;
            }
            if (item is RangedProjectileItem projectile)
            {
                return projectile.CurrentAmmoAmount.ToString();
            }
            if (item is QuickSlotItem quickSlot && quickSlot.IsConsumable)
            {
                return quickSlot.GetCurrentAmount(PlayerUIManager.Instance?.LocalPlayer).ToString();
            }
            return item.IsStackable ? item.CurrentItemAmount.ToString() : string.Empty;
        }

        private static bool IsEquipped(Item item)
        {
            PlayerInventoryManager inventory = PlayerUIManager.Instance?.LocalPlayer?.InventoryManager;
            if (inventory == null)
            {
                return false;
            }
            for (int index = 0; index < 15; index++)
            {
                if (inventory.GetEquipmentSlotItem((EquipmentSlotType)index) == item)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
