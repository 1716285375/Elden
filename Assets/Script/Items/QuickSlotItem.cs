using UnityEngine;

namespace ZZ
{
    /// <summary>Defines one item that can be activated from the gameplay quick slot.</summary>
    public abstract class QuickSlotItem : Item
    {
        [Header("Quick Slot Presentation")]
        [SerializeField] private GameObject m_itemModel;
        [SerializeField] private AnimationClip m_useItemAnimation;

        public GameObject ItemModel => m_itemModel;
        public AnimationClip UseItemAnimation => m_useItemAnimation;

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
