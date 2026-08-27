using UnityEngine;

namespace ZZ
{
    /// <summary>Owns persistent world-wide gameplay constants and shared classifiers.</summary>
    public class WorldUtilityManager : MonoBehaviour
    {
        private const int k_DefaultEnvironmentLayer = 0;
        private const int k_PlayerLayer = 8;
        private const int k_DamageableCharacterLayer = 10;
        private const int k_SlipperyEnvironmentLayer = 15;
        private const float k_LightPoiseThreshold = 10f;
        private const float k_MediumPoiseThreshold = 30f;
        private const float k_HeavyPoiseThreshold = 70f;
        private const float k_ColossalPoiseThreshold = 120f;

        private static WorldUtilityManager s_instance;

        [Header("Environment Layers")]
        [SerializeField] private LayerMask m_environmentLayers =
            (1 << k_DefaultEnvironmentLayer) |
            (1 << k_SlipperyEnvironmentLayer);
        [SerializeField] private LayerMask m_groundLayers =
            (1 << k_DefaultEnvironmentLayer) |
            (1 << k_SlipperyEnvironmentLayer);
        [SerializeField] private LayerMask m_slipperyEnvironmentLayers =
            1 << k_SlipperyEnvironmentLayer;
        [SerializeField] private LayerMask m_characterLayers =
            (1 << k_PlayerLayer) |
            (1 << k_DamageableCharacterLayer);

        public static WorldUtilityManager Instance => s_instance;

        /// <summary>Gets all surfaces that can affect falling characters.</summary>
        public LayerMask GetEnvironmentLayers()
        {
            return IncludeRequiredLayer(
                m_environmentLayers,
                k_SlipperyEnvironmentLayer);
        }

        /// <summary>Gets all surfaces recognized by the shared ground probe.</summary>
        public LayerMask GetGroundLayers()
        {
            return IncludeRequiredLayer(
                m_groundLayers,
                k_SlipperyEnvironmentLayer);
        }

        /// <summary>Gets surfaces that force already-grounded characters to slide.</summary>
        public LayerMask GetSlipperyEnviroLayers()
        {
            return IncludeRequiredLayer(
                m_slipperyEnvironmentLayers,
                k_SlipperyEnvironmentLayer);
        }

        /// <summary>Gets the Player and AI layers used for character collisions.</summary>
        public LayerMask GetCharacterLayers()
        {
            LayerMask characterLayers = m_characterLayers;
            characterLayers.value |= 1 << k_PlayerLayer;
            characterLayers.value |= 1 << k_DamageableCharacterLayer;
            return characterLayers;
        }

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

        /// <summary>Returns the attacker-local Backstab receiver offset.</summary>
        public static Vector3 GetBackstabPositionBasedOnWeaponClass(
            WeaponClass weaponClass)
        {
            switch (weaponClass)
            {
                case WeaponClass.Dagger:
                    return new Vector3(0.08f, 0f, 0.64f);
                case WeaponClass.Spear:
                    return new Vector3(0.12f, 0f, 0.88f);
                case WeaponClass.Unarmed:
                    return new Vector3(0f, 0f, 0.68f);
                default:
                    return new Vector3(0.12f, 0f, 0.74f);
            }
        }

        private static LayerMask IncludeRequiredLayer(
            LayerMask layerMask,
            int requiredLayer)
        {
            layerMask.value |= 1 << requiredLayer;
            return layerMask;
        }
    }
}
