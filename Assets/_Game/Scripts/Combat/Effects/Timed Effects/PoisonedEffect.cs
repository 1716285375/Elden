using UnityEngine;

namespace ZZ
{
    [CreateAssetMenu(
        fileName = "Poisoned Effect",
        menuName = "ZZ/Character Effects/Timed/Poisoned")]
    public class PoisonedEffect : TimedCharacterEffect
    {
        [SerializeField, Min(0f)] private float m_poisonDamage = 10f;

        /// <summary>Gets the non-negative Health damage applied by each shared tick.</summary>
        public float PoisonDamage => Mathf.Max(0f, m_poisonDamage);

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

            if (character.IsDead || !networkManager.IsPoisoned.Value)
            {
                character.CharacterEffectsManager?.RemoveTimedEffect(
                    TimedEffectID);
                return;
            }

            character.CharacterEffectsManager?.ProcessPoisonDamage(
                PoisonDamage);
        }

        /// <inheritdoc />
        public override void RemoveEffect(CharacterManager character)
        {
            CharacterNetworkManager networkManager =
                character?.CharacterNetworkManager;
            if (character != null &&
                character.IsSpawned &&
                character.IsOwner &&
                networkManager != null &&
                networkManager.IsPoisoned.Value)
            {
                networkManager.TrySetPoisoned(false);
            }
        }

        /// <summary>Clamps a Poison tick so Health never becomes negative.</summary>
        public static float CalculateRemainingHealth(
            float currentHealth,
            float poisonDamage)
        {
            return Mathf.Max(
                0f,
                Mathf.Max(0f, currentHealth) - Mathf.Max(0f, poisonDamage));
        }
    }
}
