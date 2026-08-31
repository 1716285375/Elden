using System;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Routes one reusable item-category button to its owning menu.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIItemCategory : MonoBehaviour
    {
        private Button m_button;
        private ShopItemCategory m_category;
        private Action<ShopItemCategory> m_categorySelected;

        private void Awake()
        {
            m_button = GetComponent<Button>();
        }

        private void OnDestroy()
        {
            m_button?.onClick.RemoveListener(SelectCategory);
        }

        /// <summary>Connects this category button to one Shop or Storage menu.</summary>
        public void Initialize(
            ShopItemCategory category,
            Action<ShopItemCategory> categorySelected)
        {
            m_button ??= GetComponent<Button>();
            m_button.onClick.RemoveListener(SelectCategory);
            m_category = category;
            m_categorySelected = categorySelected;
            m_button.onClick.AddListener(SelectCategory);
        }

        public void SelectCategory()
        {
            m_categorySelected?.Invoke(m_category);
        }
    }
}
