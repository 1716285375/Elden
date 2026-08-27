using UnityEngine;

namespace ZZ
{
    /// <summary>Provides a reusable world entry point into the shared weapon-upgrade menu.</summary>
    public sealed class AnvilInteractable : Interactable
    {
        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            return base.CanInteract(player) &&
                player != null &&
                !player.IsDead &&
                !player.IsPerformingAction &&
                PlayerUIManager.Instance?.IsMenuWindowOpen != true;
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            PlayerUIManager.Instance?.PlayerUIWeaponUpgradeManager
                ?.OpenWeaponUpgradeMenu();
        }

        /// <inheritdoc />
        protected override void OnTriggerExit(Collider other)
        {
            base.OnTriggerExit(other);
            PlayerManager player = other.GetComponentInParent<PlayerManager>();
            if (player?.IsOwner == true)
            {
                PlayerUIManager.Instance?.PlayerUIWeaponUpgradeManager
                    ?.CloseMenu();
            }
        }
    }
}
