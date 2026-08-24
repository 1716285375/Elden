using UnityEngine;

namespace ZZ
{
    /// <summary>Owns persistent world-wide gameplay constants and shared classifiers.</summary>
    public class WorldUtilityManager : MonoBehaviour
    {
        private const float k_LightPoiseThreshold = 10f;
        private const float k_MediumPoiseThreshold = 30f;
        private const float k_HeavyPoiseThreshold = 70f;
        private const float k_ColossalPoiseThreshold = 120f;

        private static WorldUtilityManager s_instance;

        public static WorldUtilityManager Instance => s_instance;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>Classifies a hit using the shared EP55 poise-damage thresholds.</summary>
        public static DamageIntensity GetDamageIntensityBasedOnPoiseDamage(
            float poiseDamage)
        {
            if (poiseDamage >= k_ColossalPoiseThreshold)
            {
                return DamageIntensity.Colossal;
            }

            if (poiseDamage >= k_HeavyPoiseThreshold)
            {
                return DamageIntensity.Heavy;
            }

            if (poiseDamage >= k_MediumPoiseThreshold)
            {
                return DamageIntensity.Medium;
            }

            return poiseDamage >= k_LightPoiseThreshold
                ? DamageIntensity.Light
                : DamageIntensity.Ping;
        }

        /// <summary>Returns whether two character archetypes are hostile damage peers.</summary>
        public static bool CanDamageCharacter(
            CharacterManager attacker,
            CharacterManager target)
        {
            if (attacker == null ||
                target == null ||
                attacker == target ||
                target.IsDead)
            {
                return false;
            }

            bool samePlayerFaction = attacker is PlayerManager &&
                target is PlayerManager;
            bool sameAIFaction = attacker is AICharacterManager &&
                target is AICharacterManager;
            return !samePlayerFaction && !sameAIFaction;
        }

        /// <summary>Returns the target-local Riposte receiver offset for a weapon class.</summary>
        public static Vector3 GetRipostingPositionBasedOnWeaponClass(
            WeaponClass weaponClass)
        {
            switch (weaponClass)
            {
                case WeaponClass.Dagger:
                    return new Vector3(0.08f, 0f, 0.58f);
                case WeaponClass.Spear:
                    return new Vector3(0.1f, 0f, 0.9f);
                case WeaponClass.Unarmed:
                    return new Vector3(0f, 0f, 0.65f);
                default:
                    return new Vector3(0.1f, 0f, 0.7f);
            }
        }
    }
}
