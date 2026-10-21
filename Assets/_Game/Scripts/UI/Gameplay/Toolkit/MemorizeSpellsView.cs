using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Edits the inventory's existing spell slot within the grace menu input boundary.</summary>
    internal sealed class MemorizeSpellsView : IDisposable
    {
        private readonly VisualElement m_root;
        private readonly VisualElement m_container;
        private readonly VisualElement m_graceCard;
        private readonly PlayerInventoryManager m_inventory;
        private readonly List<Item> m_spells = new();
        private readonly InventoryItemGrid m_grid;
        private SpellItem m_selected;

        public bool IsOpen => m_root.style.display == DisplayStyle.Flex;

        public MemorizeSpellsView(VisualElement parent, PlayerInventoryManager inventory)
        {
            m_inventory = inventory;
            m_graceCard = parent.Q(className: "grace-menu__card");
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/MemorizeSpells");
            if (layout == null)
            {
                throw new InvalidOperationException("The Memorize Spells layout is missing.");
            }
            VisualElement container = layout.CloneTree();
            m_container = container;
            container.style.position = Position.Absolute;
            container.style.left = 0;
            container.style.right = 0;
            container.style.top = 0;
            container.style.bottom = 0;
            container.style.display = DisplayStyle.None;
            parent.Add(container);
            m_root = container.Q("memory-panel");
            m_grid = new InventoryItemGrid(m_root.Q<ListView>("memory-spells"), m_spells, Select, Equip, columns: 10);
            m_root.Q<Button>("memory-equip").clicked += Equip;
            m_root.Q<Button>("memory-back").clicked += Close;
            m_root.Q<Button>("memory-slot").clicked += m_grid.FocusSelected;
            m_inventory.CurrentSpellChanged += OnSpellChanged;
            m_inventory.InventoryChanged += Refresh;
        }

        public void Dispose()
        {
            m_inventory.CurrentSpellChanged -= OnSpellChanged;
            m_inventory.InventoryChanged -= Refresh;
            m_graceCard.style.display = DisplayStyle.Flex;
            m_container.RemoveFromHierarchy();
        }

        public void Open()
        {
            m_graceCard.style.display = DisplayStyle.None;
            m_container.style.display = DisplayStyle.Flex;
            m_root.style.display = DisplayStyle.Flex;
            Refresh();
            m_root.schedule.Execute(() => m_root.Q<Button>("memory-slot").Focus());
        }

        public void Close()
        {
            m_root.style.display = DisplayStyle.None;
            m_container.style.display = DisplayStyle.None;
            m_graceCard.style.display = DisplayStyle.Flex;
            m_graceCard.schedule.Execute(() => m_graceCard.Q<Button>("memorize").Focus());
        }

        private void Refresh()
        {
            m_spells.Clear();
            m_spells.AddRange(m_inventory.ItemsInInventory.OfType<SpellItem>()
                .Append(m_inventory.CurrentSpell).Where(spell => spell != null)
                .GroupBy(spell => spell.ItemID).Select(group => group.First()));
            int selected = m_selected != null ? m_spells.IndexOf(m_selected) : -1;
            m_grid.Refresh(selected >= 0 ? selected : m_spells.Count > 0 ? 0 : -1);
            m_root.Q("memory-empty").style.display = m_spells.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_root.Q<Image>("memory-slot-icon").sprite = m_inventory.CurrentSpell?.ItemIcon;
            m_root.Q<Label>("memory-slot-name").text = m_inventory.CurrentSpell?.ItemName ?? "空";
            m_root.Q<Label>("memory-summary").text =
                $"记忆空格  1\n空格数  {(m_inventory.CurrentSpell == null ? 1 : 0)}\n" +
                $"专注值  {PlayerUIManager.Instance.LocalPlayer.PlayerNetworkManager.CurrentFocusPoints.Value}";
        }

        private void Select(int index)
        {
            m_selected = index >= 0 ? m_spells[index] as SpellItem : null;
            m_root.Q<Label>("memory-name").text = m_selected?.ItemName ?? string.Empty;
            m_root.Q<Image>("memory-icon").sprite = m_selected?.ItemIcon;
            m_root.Q<Label>("memory-description").text = m_selected?.ItemDescription ?? string.Empty;
            m_root.Q<Label>("memory-cost").text = m_selected != null ?
                $"消耗专注值  {m_selected.FocusPointsCost}\n记忆格数  {m_selected.SpellSlotsUsed}" : string.Empty;
            // shortcut: inventory currently persists one spell slot; expand only with its save/network model.
            m_root.Q<Button>("memory-equip").SetEnabled(m_selected != null && m_selected.SpellSlotsUsed <= 1);
        }

        private void Equip()
        {
            if (m_selected == null || m_selected.SpellSlotsUsed > 1)
            {
                return;
            }
            m_root.Q<Label>("memory-feedback").text = m_inventory.EquipCurrentSpell(m_selected) ?
                "已记忆：" + m_selected.ItemName : "当前无法记忆此法术。";
        }

        private void OnSpellChanged(SpellItem spell)
        {
            Refresh();
        }
    }
}
