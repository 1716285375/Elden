using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns the stable catalog of weapon actions used by data-driven combat.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public class WorldActionManager : MonoBehaviour
    {
        private static WorldActionManager s_instance;

        [SerializeField] private List<WeaponItemBasedAction> m_weaponActions = new();

        /// <summary>Gets the persistent weapon-action catalog instance.</summary>
        public static WorldActionManager Instance => s_instance;

        /// <summary>
        /// Gets the authored weapon actions in their stable identifier order.
        /// </summary>
        public IReadOnlyList<WeaponItemBasedAction> WeaponActions => m_weaponActions;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            AssignActionIDs();
            DontDestroyOnLoad(gameObject);
        }

        private void OnValidate()
        {
            AssignActionIDs();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// Returns the weapon action assigned to a stable identifier.
        /// </summary>
        public WeaponItemBasedAction GetWeaponActionByID(int actionID)
        {
            if (actionID < 0 || actionID >= m_weaponActions.Count)
            {
                return null;
            }

            WeaponItemBasedAction action = m_weaponActions[actionID];
            return action != null && action.ActionID == actionID ? action : null;
        }

        private void AssignActionIDs()
        {
            for (int actionIndex = 0; actionIndex < m_weaponActions.Count; actionIndex++)
            {
                m_weaponActions[actionIndex]?.AssignActionID(actionIndex);
            }
        }
    }
}
