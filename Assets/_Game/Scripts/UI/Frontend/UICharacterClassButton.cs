using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Separates controller-focus class preview from click-to-commit selection.</summary>
    [RequireComponent(typeof(Button))]
    public class UICharacterClassButton : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private TitleScreenCharacterCreationManager m_creationManager;
        [SerializeField, Min(0)] private int m_classIndex;

        private Button m_button;

        /// <summary>Assigns the creation owner and class index for runtime-built UI.</summary>
        public void Configure(
            TitleScreenCharacterCreationManager creationManager,
            int classIndex)
        {
            m_creationManager = creationManager;
            m_classIndex = Mathf.Max(0, classIndex);
        }

        private void Awake()
        {
            m_button = GetComponent<Button>();
            m_button.onClick.AddListener(SelectClass);
        }

        private void OnDestroy()
        {
            m_button?.onClick.RemoveListener(SelectClass);
        }

        /// <inheritdoc />
        public void OnSelect(BaseEventData eventData)
        {
            m_creationManager?.PreviewClass(m_classIndex);
        }

        private void SelectClass()
        {
            m_creationManager?.SelectClass(m_classIndex);
        }
    }
}
