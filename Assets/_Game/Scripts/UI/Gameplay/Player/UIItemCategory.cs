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
        private bool m_isListenerRegistered;

        private void Awake()
        {
            m_button = GetComponent<Button>();
        }

        private void OnDestroy()
        {
            UnregisterListener();
            m_categorySelected = null;
        }

        /// <summary>Connects this category button to one Shop or Storage menu.</summary>
        public void Initialize(
            ShopItemCategory category,
            Action<ShopItemCategory> categorySelected)
        {
            if (categorySelected == null)
            {
                throw new ArgumentNullException(nameof(categorySelected));
            }

            m_button ??= GetComponent<Button>();
            UnregisterListener();
            m_category = category;
            m_categorySelected = categorySelected;
            m_button.onClick.AddListener(SelectCategory);
            m_isListenerRegistered = true;
        }

        public void SelectCategory()
        {
            m_categorySelected?.Invoke(m_category);
        }

        private void UnregisterListener()
        {
            if (!m_isListenerRegistered || m_button == null)
            {
                return;
            }

            m_button.onClick.RemoveListener(SelectCategory);
            m_isListenerRegistered = false;
        }
    }
}
