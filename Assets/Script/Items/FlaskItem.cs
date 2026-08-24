using UnityEngine;

namespace ZZ
{
    /// <summary>Restores Health or Focus Points when its drink animation reaches the success frame.</summary>
    [CreateAssetMenu(fileName = "Flask", menuName = "ZZ/Items/Quick Slot/Flask")]
    public class FlaskItem : QuickSlotItem
    {
        [Header("Flask")]
        [SerializeField] private GameObject m_emptyFlaskItemModel;
        [SerializeField] private bool m_restoresHealth = true;
        [SerializeField, Min(0f)] private float m_flaskRestoration = 55f;

        public GameObject EmptyFlaskItemModel => m_emptyFlaskItemModel;
        public bool RestoresHealth => m_restoresHealth;
        public float FlaskRestoration => Mathf.Max(0f, m_flaskRestoration);

        /// <inheritdoc />
        public override void AttemptToUseItem(PlayerManager player)
        {
            if (!CanIUseThisItem(player))
            {
                return;
            }

            player.PlayerCombatManager?.AttemptToUseFlask(this);
        }

        /// <inheritdoc />
        public override bool SuccessfullyUseItem(PlayerManager player)
        {
            PlayerNetworkManager networkManager = player?.PlayerNetworkManager;
            CharacterNetworkManager characterNetwork =
                player?.CharacterNetworkManager;
            if (networkManager == null ||
                characterNetwork == null ||
                !player.IsOwner)
            {
                return false;
            }

            if (networkManager.GetRemainingFlaskCount(m_restoresHealth) <= 0)
            {
                return false;
            }

            if (!networkManager.TryConsumeFlaskCharge(m_restoresHealth))
            {
                return false;
            }

            if (m_restoresHealth)
            {
                characterNetwork.CurrentHealth.Value = CalculateRestoredValue(
                    characterNetwork.CurrentHealth.Value,
                    characterNetwork.MaxHealth.Value,
                    FlaskRestoration);
            }
            else
            {
                characterNetwork.CurrentFocusPoints.Value =
                    CalculateRestoredValue(
                        characterNetwork.CurrentFocusPoints.Value,
                        characterNetwork.MaxFocusPoints.Value,
                        FlaskRestoration);
            }

            PlaySuccessfulUseFeedback(player);
            return true;
        }

        /// <summary>Presents a successful replicated drink without mutating resources.</summary>
        public void PlaySuccessfulUseFeedback(PlayerManager player)
        {
            player?.CharacterEffectsManager?.PlayFlaskRestorationVFX(
                m_restoresHealth);
            player?.CharacterSoundFXManager?.PlayFlaskSound(false);
        }

        /// <summary>Clamps one restoration to its non-negative resource maximum.</summary>
        public static float CalculateRestoredValue(
            float currentValue,
            float maximumValue,
            float restoration)
        {
            float resolvedMaximum = Mathf.Max(0f, maximumValue);
            return Mathf.Clamp(
                currentValue + Mathf.Max(0f, restoration),
                0f,
                resolvedMaximum);
        }
    }
}
