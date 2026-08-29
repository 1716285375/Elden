using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Maps one whole-number Level Up slider to a character attribute.</summary>
    [RequireComponent(typeof(Slider))]
    public class UICharacterAttributeSlider : MonoBehaviour
    {
        [SerializeField] private CharacterAttribute m_characterAttribute;
        [SerializeField] private Slider m_slider;
        [SerializeField] private PlayerUILevelUpManager m_levelUpManager;

        /// <summary>Gets the attribute controlled by this slider.</summary>
        public CharacterAttribute CharacterAttribute => m_characterAttribute;

        /// <summary>Gets the current whole-number projected value.</summary>
        public int ProjectedValue => Mathf.RoundToInt(m_slider?.value ?? 0f);

        private void Awake()
        {
            m_slider ??= GetComponent<Slider>();
        }

        private void OnEnable()
        {
            m_slider ??= GetComponent<Slider>();
            m_slider.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnDisable()
        {
            m_slider?.onValueChanged.RemoveListener(OnValueChanged);
        }

        /// <summary>Sets the non-decreasing range without emitting a preview event.</summary>
        public void SetRangeAndValue(int minimumValue, int maximumValue)
        {
            m_slider.wholeNumbers = true;
            m_slider.minValue = Mathf.Max(0, minimumValue);
            m_slider.maxValue = Mathf.Max(m_slider.minValue, maximumValue);
            m_slider.SetValueWithoutNotify(m_slider.minValue);
        }

        /// <summary>Moves controller focus to this attribute row.</summary>
        public void Select()
        {
            if (m_slider != null && m_slider.IsInteractable())
            {
                m_slider.Select();
                m_slider.OnSelect(null);
            }
        }

        /// <summary>Assigns the authored attribute and menu callback.</summary>
        public void Configure(
            CharacterAttribute characterAttribute,
            PlayerUILevelUpManager levelUpManager)
        {
            m_characterAttribute = characterAttribute;
            m_levelUpManager = levelUpManager;
            m_slider ??= GetComponent<Slider>();
        }

        private void OnValueChanged(float value)
        {
            m_levelUpManager?.SetProjectedAttribute(
                m_characterAttribute,
                Mathf.RoundToInt(value));
        }
    }
}
