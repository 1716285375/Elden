using UnityEngine;

namespace ZZ
{
    [GameAsset(
        FileName = "Take Stamina Damage Effect",
        MenuName = "ZZ/Character Effects/Instant/Take Stamina Damage")]
    public class TakeStaminaDamageEffect : InstantCharacterEffect
    {
        [SerializeField, Min(0f)] private float m_staminaDamage = 25f;

        /// <summary>
        /// Gets the authored amount of Stamina removed by this effect.
        /// </summary>
        public float StaminaDamage => m_staminaDamage;

        /// <inheritdoc />
        public override void ProcessEffect(CharacterManager character)
        {
            if (character == null)
            {
                return;
            }

            CalculateStaminaDamage(character);
        }

        private void CalculateStaminaDamage(CharacterManager character)
        {
            character.CharacterStatsManager?.TryConsumeStamina(m_staminaDamage);
        }
    }
}
