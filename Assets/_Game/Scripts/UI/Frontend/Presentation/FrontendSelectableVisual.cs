using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// One reusable selection visual for every frontend selectable (main menu, save
    /// slots, settings, credits, popups). Mouse hover funnels into the EventSystem
    /// selection so keyboard, mouse, and gamepad always share a single selection
    /// source, and the same visual state is driven from <see cref="ISelectHandler"/>
    /// regardless of input device.
    /// </summary>
    /// <remarks>
    /// Expected child layout on the host selectable:
    /// <code>
    /// BTN_Entry
    /// ├── SelectionBackground  (Image)
    /// ├── SelectionMarker      (Image or plain GameObject)
    /// └── Label                (TMP_Text, first text child when unnamed)
    /// </code>
    /// Normal: background hidden, marker hidden, warm-white label.
    /// Selected: background opaque, marker visible, dark label shifted +12 on X.
    /// The Button's own transition is disabled by default so the two systems do
    /// not fight; this component owns all selection feedback.
    ///
    /// When <see cref="m_idleBackgroundSprite"/> and
    /// <see cref="m_selectedBackgroundSprite"/> are both assigned and the
    /// background graphic is an <see cref="Image"/>, the component swaps sprites
    /// instead of fading background colors; the color fields are then unused.
    /// </remarks>
    public class FrontendSelectableVisual : MonoBehaviour,
        ISelectHandler,
        IDeselectHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [Header("References")]
        [SerializeField] private Selectable m_selectable;
        [SerializeField] private Graphic m_selectionBackground;
        [SerializeField] private GameObject m_selectionMarker;
        [SerializeField] private TMP_Text m_label;

        [Header("Appearance")]
        [SerializeField] private Color m_normalBackgroundColor =
            new(1f, 0.78f, 0.02f, 1f);
        [SerializeField] private Color m_idleBackgroundColor =
            new(0.72f, 0.03f, 0.08f, 0.92f);
        [SerializeField] private Color m_disabledBackgroundColor =
            new(0.12f, 0.12f, 0.12f, 0.72f);
        [SerializeField] private Color m_normalTextColor = new(0.93f, 0.86f, 0.72f, 1f);
        [SerializeField] private Color m_selectedTextColor = new(0.08f, 0.05f, 0.03f, 1f);
        [SerializeField] private Color m_disabledTextColor = new(0.52f, 0.52f, 0.52f, 1f);
        [SerializeField] private Sprite m_idleBackgroundSprite;
        [SerializeField] private Sprite m_selectedBackgroundSprite;
        [SerializeField] private float m_labelShiftX = 12f;
        [SerializeField, Min(0.01f)] private float m_transitionDuration = 0.14f;
        [SerializeField] private Ease m_transitionEase = Ease.InOutQuad;

        [Header("Behaviour")]
        [SerializeField] private bool m_disableButtonTransition = true;

        private Image m_selectionImage;
        private bool m_useSpriteSwap;
        private float m_normalLabelX;
        private bool m_usesAnchoredLabelPosition;
        private bool m_isSelected;
        private Sequence m_transitionSequence;

        private void Awake()
        {
            m_selectable ??= GetComponent<Selectable>();
            ResolveReferences();
            CaptureNormalLabelPosition();

            if (m_selectable != null && m_disableButtonTransition)
            {
                m_selectable.transition = Selectable.Transition.None;
            }
        }

        private void OnEnable()
        {
            ApplyStateInstant(m_isSelected);
        }

        private void OnDisable()
        {
            KillTransitionSequence();
            m_isSelected = false;
            ApplyStateInstant(false);
        }

        /// <summary>Routes pointer hover into the shared EventSystem selection.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (m_selectable != null &&
                m_selectable.interactable &&
                EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Selection is kept until the user moves to another entry; a real
            // pointer click on empty space is handled by deselecting via the
            // EventSystem's normal deselection path.
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetSelected(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetSelected(false);
        }

        private void SetSelected(bool isSelected)
        {
            if (m_isSelected == isSelected)
            {
                return;
            }

            m_isSelected = isSelected;
            KillTransitionSequence();
            m_transitionSequence = BuildTransitionSequence(isSelected);
        }

        /// <summary>
        /// Runs the background colour, label colour, and label shift in parallel. The marker is
        /// toggled immediately because the original transition kept it constant for the whole
        /// duration and only <see cref="ApplyStateInstant"/> re-evaluated interactability.
        /// </summary>
        private Sequence BuildTransitionSequence(bool isSelected)
        {
            if (m_useSpriteSwap)
            {
                ApplySprite(isSelected);
            }

            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);

            if (m_selectionBackground != null)
            {
                sequence.Join(m_selectionBackground
                    .DOColor(ResolveBackgroundColor(isSelected), m_transitionDuration)
                    .SetEase(m_transitionEase)
                    .SetUpdate(true));
            }

            if (m_label != null)
            {
                sequence.Join(m_label
                    .DOColor(ResolveTextColor(isSelected), m_transitionDuration)
                    .SetEase(m_transitionEase)
                    .SetUpdate(true));
                sequence.Join(DOTween.To(
                        GetLabelShift,
                        SetLabelShift,
                        isSelected ? m_labelShiftX : 0f,
                        m_transitionDuration)
                    .SetEase(m_transitionEase)
                    .SetUpdate(true));
            }

            sequence.OnComplete(() =>
            {
                m_transitionSequence = null;
                ApplyStateInstant(isSelected);
            });

            if (m_selectionMarker != null)
            {
                m_selectionMarker.SetActive(isSelected);
            }

            return sequence;
        }

        /// <summary>
        /// Killing without completing is deliberate: <see cref="ApplyStateInstant"/> snaps to the
        /// real state right afterwards, so no partially interpolated value can survive.
        /// </summary>
        private void KillTransitionSequence()
        {
            m_transitionSequence?.Kill();
            m_transitionSequence = null;
        }

        private void ApplyStateInstant(bool isSelected)
        {
            if (m_useSpriteSwap)
            {
                ApplySprite(isSelected);
            }

            if (m_selectionBackground != null)
            {
                m_selectionBackground.color = ResolveBackgroundColor(isSelected);
            }

            if (m_label != null)
            {
                m_label.color = ResolveTextColor(isSelected);
            }

            SetLabelShift(isSelected ? m_labelShiftX : 0f);

            if (m_selectionMarker != null)
            {
                m_selectionMarker.SetActive(isSelected && IsInteractable());
            }
        }

        private Color ResolveBackgroundColor(bool isSelected)
        {
            if (m_useSpriteSwap)
            {
                return IsInteractable() ? Color.white : m_disabledBackgroundColor;
            }

            if (!IsInteractable())
            {
                return m_disabledBackgroundColor;
            }

            return isSelected ? m_normalBackgroundColor : m_idleBackgroundColor;
        }

        private void ApplySprite(bool isSelected)
        {
            if (m_selectionImage == null)
            {
                return;
            }

            m_selectionImage.sprite =
                isSelected ? m_selectedBackgroundSprite : m_idleBackgroundSprite;
        }

        private Color ResolveTextColor(bool isSelected)
        {
            if (!IsInteractable())
            {
                return m_disabledTextColor;
            }

            return isSelected ? m_selectedTextColor : m_normalTextColor;
        }

        private bool IsInteractable()
        {
            return m_selectable == null || m_selectable.interactable;
        }

        private void ResolveReferences()
        {
            if (m_selectionBackground == null)
            {
                Transform backgroundTransform = transform.Find("SelectionBackground");
                if (backgroundTransform != null)
                {
                    m_selectionBackground = backgroundTransform.GetComponent<Graphic>();
                }
            }

            if (m_selectionBackground == null)
            {
                m_selectionBackground = GetComponent<Graphic>();
            }

            if (m_selectionMarker == null)
            {
                Transform markerTransform = transform.Find("SelectionMarker");
                if (markerTransform != null)
                {
                    m_selectionMarker = markerTransform.gameObject;
                }
            }

            m_selectionImage = m_selectionBackground as Image;
            m_useSpriteSwap = m_selectionImage != null
                && m_idleBackgroundSprite != null
                && m_selectedBackgroundSprite != null;

            if (m_label == null)
            {
                Transform labelTransform = transform.Find("Label");
                m_label = labelTransform != null
                    ? labelTransform.GetComponent<TMP_Text>()
                    : GetComponentInChildren<TMP_Text>();
            }
        }

        private float GetLabelShift()
        {
            if (m_label == null)
            {
                return 0f;
            }

            RectTransform rectTransform = m_label.rectTransform;
            if (m_usesAnchoredLabelPosition)
            {
                return rectTransform.anchoredPosition.x - m_normalLabelX;
            }

            return rectTransform.offsetMin.x - m_normalLabelX;
        }

        private void SetLabelShift(float shift)
        {
            if (m_label == null)
            {
                return;
            }

            RectTransform rectTransform = m_label.rectTransform;
            if (m_usesAnchoredLabelPosition)
            {
                rectTransform.anchoredPosition = new Vector2(
                    m_normalLabelX + shift,
                    rectTransform.anchoredPosition.y);
                return;
            }

            float targetOffsetMinX = m_normalLabelX + shift;
            float offset = targetOffsetMinX - rectTransform.offsetMin.x;
            rectTransform.offsetMin = new Vector2(
                rectTransform.offsetMin.x + offset,
                rectTransform.offsetMin.y);
            rectTransform.offsetMax = new Vector2(
                rectTransform.offsetMax.x + offset,
                rectTransform.offsetMax.y);
        }

        private void CaptureNormalLabelPosition()
        {
            if (m_label == null)
            {
                return;
            }

            RectTransform rectTransform = m_label.rectTransform;
            m_usesAnchoredLabelPosition = rectTransform.anchorMin == rectTransform.anchorMax;
            m_normalLabelX = m_usesAnchoredLabelPosition
                ? rectTransform.anchoredPosition.x
                : rectTransform.offsetMin.x;
        }
    }
}
