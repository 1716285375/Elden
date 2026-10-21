using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Keeps the selected title command centered between two dim neighbours.</summary>
    public sealed class TitleMenuCarousel : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private Button[] m_buttons = System.Array.Empty<Button>();
        private CanvasGroup[] m_groups;
        private int m_selectedIndex;

        private void OnEnable()
        {
            Configure(m_buttons);
        }

        private void OnDisable()
        {
            foreach (Button button in m_buttons)
            {
                button.transform.DOKill();
            }
        }

        /// <summary>Preserves the authored command buttons and builds their circular navigation.</summary>
        public void Configure(Button[] buttons)
        {
            m_buttons = buttons;
            m_groups = new CanvasGroup[buttons.Length];
            for (int index = 0; index < buttons.Length; index++)
            {
                m_groups[index] = buttons[index].GetComponent<CanvasGroup>();
                if (m_groups[index] == null)
                {
                    m_groups[index] = buttons[index].gameObject.AddComponent<CanvasGroup>();
                }
                Navigation navigation = buttons[index].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = FindNeighbour(index, -1);
                navigation.selectOnDown = FindNeighbour(index, 1);
                buttons[index].navigation = navigation;
            }
            Refresh(false);
        }

        /// <summary>Updates the three visible entries from the EventSystem selection.</summary>
        public void Select(Button button)
        {
            int index = System.Array.IndexOf(m_buttons, button);
            if (index < 0)
            {
                return;
            }
            m_selectedIndex = index;
            Refresh(Application.isPlaying);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData.scrollDelta.y == 0f || m_buttons.Length == 0)
            {
                return;
            }
            FindNeighbour(m_selectedIndex, eventData.scrollDelta.y > 0f ? -1 : 1)?.Select();
            eventData.Use();
        }

        private Button FindNeighbour(int index, int direction)
        {
            for (int step = 1; step < m_buttons.Length; step++)
            {
                Button candidate = m_buttons[(index + direction * step + m_buttons.Length) % m_buttons.Length];
                if (candidate.gameObject.activeSelf && candidate.interactable)
                {
                    return candidate;
                }
            }
            return null;
        }

        private void Refresh(bool animate)
        {
            if (m_buttons.Length == 0 || m_groups == null)
            {
                return;
            }
            Button previous = FindNeighbour(m_selectedIndex, -1);
            Button next = FindNeighbour(m_selectedIndex, 1);
            for (int index = 0; index < m_buttons.Length; index++)
            {
                Button button = m_buttons[index];
                Navigation navigation = button.navigation;
                navigation.selectOnUp = FindNeighbour(index, -1);
                navigation.selectOnDown = FindNeighbour(index, 1);
                button.navigation = navigation;
                bool selected = index == m_selectedIndex;
                bool visible = selected || button == previous || button == next;
                m_groups[index].alpha = selected ? 1f : visible ? 0.35f : 0f;
                m_groups[index].blocksRaycasts = visible;
                RectTransform rect = (RectTransform)button.transform;
                Vector2 position = new(0f, button == previous ? 32f : button == next ? -32f : 0f);
                rect.DOKill();
                if (animate && visible)
                {
                    rect.DOAnchorPos(position, 0.14f).SetUpdate(true);
                }
                else
                {
                    rect.anchoredPosition = position;
                }
            }
        }
    }
}
