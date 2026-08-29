using UnityEngine;

namespace ZZ
{
    /// <summary>Displays one reusable local message without consuming the interaction.</summary>
    public class MessageInteractable : Interactable
    {
        [SerializeField, TextArea(2, 5)]
        private string m_message = "Cannot open from this side.";

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            PlayerUIManager.Instance?.PlayerUIPopUpManager
                ?.SendPlayerMessagePopup(m_message);
        }

        /// <summary>Message interactions remain available until another system disables them.</summary>
        public override void CompleteInteraction()
        {
        }
    }
}
