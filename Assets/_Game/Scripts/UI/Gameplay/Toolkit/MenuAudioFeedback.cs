using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Routes Toolkit control feedback through the existing shared UI audio source.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuAudioFeedback : MonoBehaviour
    {
        private VisualElement m_root;

        private void OnEnable()
        {
            m_root = GetComponent<UIDocument>().rootVisualElement;
            m_root.RegisterCallback<FocusInEvent>(OnFocus);
            m_root.RegisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            m_root.RegisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
        }

        private void OnDisable()
        {
            if (m_root == null) { return; }
            m_root.UnregisterCallback<FocusInEvent>(OnFocus);
            m_root.UnregisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            m_root.UnregisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
            m_root = null;
        }

        private void OnFocus(FocusInEvent evt)
        {
            if (evt.target is VisualElement target && target.enabledInHierarchy)
            {
                PlayerUIManager.Instance?.PlayMenuHoverSound();
            }
        }

        private void OnClick(ClickEvent evt)
        {
            if (evt.button == 0 && FindButton(evt.target as VisualElement) != null)
            {
                PlayerUIManager.Instance?.PlayMenuConfirmSound();
            }
        }

        private void OnSubmit(NavigationSubmitEvent evt)
        {
            if (FindButton(evt.target as VisualElement) != null)
            {
                PlayerUIManager.Instance?.PlayMenuConfirmSound();
            }
        }

        private static Button FindButton(VisualElement target)
        {
            Button button = target as Button ?? target?.GetFirstAncestorOfType<Button>();
            return button?.enabledInHierarchy == true ? button : null;
        }
    }
}
