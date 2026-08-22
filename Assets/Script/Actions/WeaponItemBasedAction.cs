using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Base ScriptableObject for a data-driven weapon action slot.
    /// A weapon references one action per input slot; the action decides what happens.
    /// </summary>
    public abstract class WeaponItemBasedAction : ScriptableObject
    {
        [SerializeField, HideInInspector] private int m_actionID;

        /// <summary>Gets the stable catalog identifier assigned by the world action manager.</summary>
        public int ActionID => m_actionID;

        /// <summary>
        /// Executes this action for the supplied player and weapon.
        /// </summary>
        public abstract void AttemptToPerformAction(PlayerManager player, WeaponItem weapon);

        protected static bool CanPerformAttack(PlayerManager player)
        {
            if (player == null || !player.IsOwner || player.IsPerformingAction)
            {
                return false;
            }

            if (!player.IsGrounded)
            {
                return false;
            }

            return player.CharacterNetworkManager != null &&
                player.CharacterNetworkManager.CurrentStamina.Value > 0f;
        }

        internal void AssignActionID(int actionID)
        {
            m_actionID = actionID;
        }
    }
}
