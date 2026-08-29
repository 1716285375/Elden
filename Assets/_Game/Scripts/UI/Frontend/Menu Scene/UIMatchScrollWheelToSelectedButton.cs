using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    public class UIMatchScrollWheelToSelectedButton : MonoBehaviour
    {
        [SerializeField] private ScrollRect m_scrollRect;
        [SerializeField] private RectTransform m_content;

        private GameObject m_previouslySelected;

        private void Awake()
        {
            m_scrollRect ??= GetComponent<ScrollRect>();
            m_content ??= m_scrollRect != null ? m_scrollRect.content : null;
        }

        private void Update()
        {
            if (EventSystem.current == null || m_scrollRect == null || m_content == null)
            {
                return;
            }

            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            if (currentSelected == m_previouslySelected)
            {
                return;
            }

            m_previouslySelected = currentSelected;
            RectTransform selectedTransform = currentSelected?.GetComponent<RectTransform>();
            if (selectedTransform == null || !selectedTransform.IsChildOf(m_content))
            {
                return;
            }

            SnapTo(selectedTransform);
        }

        private void SnapTo(RectTransform selectedTransform)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform viewport = m_scrollRect.viewport;
            if (viewport == null)
            {
                return;
            }

            Bounds viewportBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                m_content,
                viewport);
            Bounds selectedBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                m_content,
                selectedTransform);
            Vector2 newPosition = m_content.anchoredPosition;

            if (selectedBounds.max.y > viewportBounds.max.y)
            {
                newPosition.y -= selectedBounds.max.y - viewportBounds.max.y;
            }
            else if (selectedBounds.min.y < viewportBounds.min.y)
            {
                newPosition.y += viewportBounds.min.y - selectedBounds.min.y;
            }

            newPosition.x = 0f;
            m_scrollRect.StopMovement();
            m_content.anchoredPosition = newPosition;
        }
    }
}
