using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Reads its visual swatch and previews or commits an exact hair color.</summary>
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Image))]
    public class UIColorButton : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private TitleScreenCharacterCreationManager m_creationManager;

        private Button m_button;
        private Image m_swatch;

        /// <summary>Assigns the creation owner for runtime-built color UI.</summary>
        public void Configure(TitleScreenCharacterCreationManager creationManager)
        {
            m_creationManager = creationManager;
        }

        private void Awake()
        {
            m_button = GetComponent<Button>();
            m_swatch = GetComponent<Image>();
            m_button.onClick.AddListener(SelectColor);
        }

        private void OnDestroy()
        {
            m_button?.onClick.RemoveListener(SelectColor);
        }

        /// <inheritdoc />
        public void OnSelect(BaseEventData eventData)
        {
            m_creationManager?.PreviewHairColor(GetSwatchColor());
        }

        private void SelectColor()
        {
            m_creationManager?.SelectHairColor(GetSwatchColor());
        }

        private Color32 GetSwatchColor()
        {
            return m_swatch != null ? (Color32)m_swatch.color : new Color32(0, 0, 0, 255);
        }
    }
}
