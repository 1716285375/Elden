using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ
{
    public class TitleScreenLoadMenuInputManager : MonoBehaviour
    {
        [SerializeField] private TitleScreenManager m_titleScreenManager;

        private PlayerControls m_playerControls;
        private bool m_hasDeleteCharacterSlotInput;

        private void Awake()
        {
            m_titleScreenManager ??= GetComponentInParent<TitleScreenManager>(true);
        }

        private void OnEnable()
        {
            m_playerControls ??= new PlayerControls();
            m_playerControls.UI.Delete.performed += OnDeleteCharacterSlotPerformed;
            m_playerControls.UI.Enable();
        }

        private void OnDisable()
        {
            if (m_playerControls == null)
            {
                return;
            }

            m_playerControls.UI.Delete.performed -= OnDeleteCharacterSlotPerformed;
            m_playerControls.UI.Disable();
            m_hasDeleteCharacterSlotInput = false;
        }

        private void OnDestroy()
        {
            m_playerControls?.Dispose();
        }

        private void Update()
        {
            if (!m_hasDeleteCharacterSlotInput)
            {
                return;
            }

            m_hasDeleteCharacterSlotInput = false;
            m_titleScreenManager?.AttemptToDeleteCharacterSlot();
        }

        private void OnDeleteCharacterSlotPerformed(InputAction.CallbackContext context)
        {
            m_hasDeleteCharacterSlotInput = true;
        }
    }
}
