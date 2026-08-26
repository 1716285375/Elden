using UnityEngine;

namespace ZZ
{
    /// <summary>Defines one item that can be activated from the gameplay quick slot.</summary>
    public abstract class QuickSlotItem : Item
    {
        [Header("Quick Slot Presentation")]
        [SerializeField] private GameObject m_itemModel;
        [SerializeField] private AnimationClip m_useItemAnimation;
        [SerializeField] private bool m_isConsumable = true;
        [SerializeField, Min(0)] private int m_currentAmount;

        public GameObject ItemModel => m_itemModel;
        public AnimationClip UseItemAnimation => m_useItemAnimation;
        public bool IsConsumable => m_isConsumable;

        /// <summary>Returns the owner-specific amount displayed by equipment and HUD UI.</summary>
        public virtual int GetCurrentAmount(PlayerManager player)
        {
            return m_currentAmount;
        }

        /// <summary>Restores per-instance quantity for non-Flask quick-slot items.</summary>
        public virtual void SetCurrentAmount(int currentAmount)
        {
            m_currentAmount = Mathf.Max(0, currentAmount);
        }

        /// <summary>Attempts to begin or continue this item's owner-authoritative action.</summary>
        public abstract void AttemptToUseItem(PlayerManager player);

        /// <summary>Returns whether gameplay state permits a new input for this item.</summary>
        public virtual bool CanIUseThisItem(PlayerManager player)
        {
            return player != null &&
                player.IsOwner &&
                player.IsSpawned &&
                player.IsGrounded &&
                !player.IsJumping &&
                !player.IsDead &&
                !player.IsPerformingAction;
        }

        /// <summary>Resolves the animation-event success frame for this item.</summary>
        public virtual bool SuccessfullyUseItem(PlayerManager player)
        {
            return false;
        }
    }
}
