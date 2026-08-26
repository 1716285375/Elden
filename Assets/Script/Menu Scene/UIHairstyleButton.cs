using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Separates hairstyle preview from the replicated committed selection.</summary>
    [RequireComponent(typeof(Button))]
    public class UIHairstyleButton : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private TitleScreenCharacterCreationManager m_creationManager;
        [SerializeField, Min(0)] private int m_hairstyleID;

        private Button m_button;

        /// <summary>Assigns the creation owner and hairstyle index for runtime-built UI.</summary>
        public void Configure(
            TitleScreenCharacterCreationManager creationManager,
            int hairstyleID)
        {
            m_creationManager = creationManager;
            m_hairstyleID = Mathf.Max(0, hairstyleID);
        }

        private void Awake()
        {
            m_button = GetComponent<Button>();
            m_button.onClick.AddListener(SelectHairstyle);
        }

        private void OnDestroy()
        {
            m_button?.onClick.RemoveListener(SelectHairstyle);
        }

        /// <inheritdoc />
        public void OnSelect(BaseEventData eventData)
        {
            m_creationManager?.PreviewHairstyle(m_hairstyleID);
        }

        private void SelectHairstyle()
        {
            m_creationManager?.SelectHairstyle(m_hairstyleID);
        }
    }
}
