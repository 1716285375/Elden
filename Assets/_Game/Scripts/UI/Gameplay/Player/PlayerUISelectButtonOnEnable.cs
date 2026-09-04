using UnityEngine;
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
        }
    }
}
