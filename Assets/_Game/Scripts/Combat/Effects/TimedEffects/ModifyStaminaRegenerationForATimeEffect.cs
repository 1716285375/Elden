using UnityEngine;

namespace ZZ
{
    [GameAsset(
        FileName = "Modify Stamina Regeneration Effect",
        MenuName = "ZZ/Character Effects/Timed/Modify Stamina Regeneration")]
    public class ModifyStaminaRegenerationForATimeEffect : TimedCharacterEffect
    {
        [SerializeField] private float m_modifierPercentage = -80f;

        private bool m_effectHasBeenInitialized;

        /// <summary>Gets the signed reusable percentage applied by this effect.</summary>
        public float ModifierPercentage => m_modifierPercentage;

        /// <summary>Gets whether this runtime clone has applied its modifier once.</summary>
        public bool EffectHasBeenInitialized => m_effectHasBeenInitialized;

        /// <inheritdoc />
        public override void ProcessEffect(CharacterManager character)
        {
            if (character == null || character.IsDead)
            {
                character?.CharacterEffectsManager?.RemoveTimedEffect(
                    TimedEffectID);
                return;
            }

            if (!character.IsSpawned ||
                !character.IsOwner ||
                m_effectHasBeenInitialized)
            {
                return;
            }

            CharacterStatsManager statsManager = character.CharacterStatsManager;
            if (statsManager == null)
            {
                return;
            }

            m_effectHasBeenInitialized = true;
            statsManager.AddStaminaRegenerationModifier(m_modifierPercentage);
        }

        /// <inheritdoc />
        public override void RemoveEffect(CharacterManager character)
        {
            if (!m_effectHasBeenInitialized)
            {
                return;
            }

            character?.CharacterStatsManager?.RemoveStaminaRegenerationModifier(
                m_modifierPercentage);
            m_effectHasBeenInitialized = false;
        }
    }
}
