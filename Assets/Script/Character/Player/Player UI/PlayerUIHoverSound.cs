using UnityEngine.EventSystems;

namespace ZZ
{
    /// <summary>Routes pointer and navigation focus to the shared non-spatial UI sound.</summary>
    public sealed class PlayerUIHoverSound :
        UnityEngine.MonoBehaviour,
        IPointerEnterHandler,
        ISelectHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            PlayerUIManager.Instance?.PlayMenuHoverSound();
        }

        public void OnSelect(BaseEventData eventData)
        {
            PlayerUIManager.Instance?.PlayMenuHoverSound();
        }
    }
}
