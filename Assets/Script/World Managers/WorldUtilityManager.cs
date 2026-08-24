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
    }
}
