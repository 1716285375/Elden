using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ
{
    /// <summary>Owns equipment-only input while the Equipment Menu is active.</summary>
    public class PlayerUIEquipmentManagerInputManager : MonoBehaviour
    {
        private const string k_UnequipItemActionName = "Unequip Item";

        [SerializeField] private PlayerUIEquipmentManager m_equipmentManager;

        private PlayerControls m_playerControls;
        private InputAction m_unequipItemAction;
        private bool m_hasUnequipItemInput;

        private void Awake()
        {
            m_equipmentManager ??=
                GetComponentInParent<PlayerUIEquipmentManager>(true);
        }

        private void OnEnable()
        {
            m_playerControls ??= new PlayerControls();
            m_unequipItemAction = m_playerControls.UI.Get()
                .FindAction(k_UnequipItemActionName, true);
            m_unequipItemAction.performed += OnUnequipItemPerformed;
            m_playerControls.UI.Enable();
        }

        private void OnDisable()
        {
            if (m_unequipItemAction != null)
            {
                m_unequipItemAction.performed -= OnUnequipItemPerformed;
            }

            m_playerControls?.UI.Disable();
            m_hasUnequipItemInput = false;
        }

        private void OnDestroy()
        {
            m_playerControls?.Dispose();
        }

        private void Update()
        {
            if (!m_hasUnequipItemInput)
            {
                return;
            }

            m_hasUnequipItemInput = false;
            m_equipmentManager?.UnequipSelectedItem();
        }

        private void OnUnequipItemPerformed(InputAction.CallbackContext context)
        {
            m_hasUnequipItemInput = true;
        }
    }
}
