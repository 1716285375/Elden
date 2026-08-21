using UnityEngine;
using UnityEngine.EventSystems;

namespace ZZ
{
    public class UITitleScreenSelectNoSlot : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private TitleScreenManager m_titleScreenManager;

        private void Awake()
        {
            m_titleScreenManager ??= GetComponentInParent<TitleScreenManager>(true);
        }

        /// <summary>
        /// Clears the deletion target whenever focus leaves the character slot list.
        /// </summary>
        public void OnSelect(BaseEventData eventData)
        {
            m_titleScreenManager?.SelectNoSlot();
        }
    }
}
