using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Selects one authored default button whenever its menu window opens.</summary>
    [RequireComponent(typeof(Button))]
    public class PlayerUISelectButtonOnEnable : MonoBehaviour
    {
        private Button m_button;

        private void Awake()
        {
            m_button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            m_button ??= GetComponent<Button>();
            if (m_button == null || !m_button.IsInteractable())
            {
                return;
            }

            m_button.Select();
            if (Application.isPlaying)
            {
                StartCoroutine(RestoreInitialFocusAfterEnable());
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private IEnumerator RestoreInitialFocusAfterEnable()
        {
            // Other menu OnEnable callbacks can replace or clear the EventSystem's first selection.
            yield return null;
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (m_button.IsInteractable() && (selected == null || !selected.activeInHierarchy))
            {
                m_button.Select();
            }
        }
    }
}
