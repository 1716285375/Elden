using UnityEngine;

namespace ZZ
{
    [CreateAssetMenu(
        fileName = "Frostbite Effect",
        menuName = "ZZ/Character Effects/Timed/Frostbite")]
    public class FrostbiteEffect : TimedCharacterEffect
    {
        [SerializeField, Range(0f, 100f)] private float m_hpPercentageDamage = 10f;
        [SerializeField]
        private ModifyStaminaRegenerationForATimeEffect
            m_staminaRegenerationModifierEffect;

        private bool m_effectHasBeenInitialized;

        /// <summary>Gets the maximum-Health percentage dealt once on trigger.</summary>
        public float HPPercentageDamage =>
            Mathf.Clamp(m_hpPercentageDamage, 0f, 100f);

        /// <summary>Gets the reusable Stamina modifier started by Frostbite.</summary>
        public ModifyStaminaRegenerationForATimeEffect
            StaminaRegenerationModifierEffect =>
                m_staminaRegenerationModifierEffect;

        /// <summary>Gets whether this runtime clone applied its one-shot payload.</summary>
        public bool EffectHasBeenInitialized => m_effectHasBeenInitialized;

        /// <inheritdoc />
        public override void ProcessEffect(CharacterManager character)
        {
            CharacterNetworkManager networkManager =
                character?.CharacterNetworkManager;
            if (character == null || networkManager == null)
            {
                return;
            }

            if (!character.IsSpawned || !character.IsOwner)
            {
                return;
            }

            if (character.IsDead || !networkManager.IsFrostbitten.Value)
            {
                character.CharacterEffectsManager?.RemoveTimedEffect(
                    TimedEffectID);
                return;
            }

            if (m_effectHasBeenInitialized)
            {
                return;
            }

            m_effectHasBeenInitialized = true;
            networkManager.CurrentStamina.Value = 0f;
            if (m_staminaRegenerationModifierEffect != null)
            {
                character.CharacterEffectsManager?.AddTimedEffect(
                    m_staminaRegenerationModifierEffect);
            }

            float damage = CalculatePercentageDamage(
                networkManager.MaxHealth.Value,
                HPPercentageDamage);
            character.CharacterEffectsManager?.ProcessEffectDamage(damage);
            if (!character.IsDead)
            {
                networkManager.TrySetFrozen(true);
            }
        }

        /// <inheritdoc />
        public override void RemoveEffect(CharacterManager character)
        {
            CharacterEffectsManager effectsManager =
                character?.CharacterEffectsManager;
            if (m_staminaRegenerationModifierEffect != null)
            {
                effectsManager?.RemoveTimedEffect(
                    m_staminaRegenerationModifierEffect.TimedEffectID);
            }

            CharacterNetworkManager networkManager =
                character?.CharacterNetworkManager;
            if (character != null &&
                character.IsSpawned &&
                character.IsOwner &&
                networkManager != null)
            {
                networkManager.TrySetFrozen(false);
                networkManager.TrySetFrostbitten(false);
            }

            m_effectHasBeenInitialized = false;
        }

        /// <summary>Calculates one clamped maximum-Health percentage hit.</summary>
        public static float CalculatePercentageDamage(
            float maximumHealth,
            float percentageDamage)
        {
            return Mathf.Max(0f, maximumHealth) *
                Mathf.Clamp(percentageDamage, 0f, 100f) /
                100f;
        }
    }
}
