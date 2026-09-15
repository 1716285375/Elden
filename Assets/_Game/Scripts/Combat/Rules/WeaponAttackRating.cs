using UnityEngine;

namespace ZZ
{
    /// <summary>Shared weapon requirement and soft-cap scaling rules.</summary>
    public static class WeaponAttackRating
    {
        public static bool MeetsRequirements(int strength, int dexterity, int intelligence, int faith,
            bool twoHanded, int requiredStrength, int requiredDexterity, int requiredIntelligence, int requiredFaith)
        {
            float effectiveStrength = Mathf.Max(0, strength) * (twoHanded ? 1.5f : 1f);
            return Mathf.FloorToInt(effectiveStrength) >= requiredStrength && dexterity >= requiredDexterity &&
                intelligence >= requiredIntelligence && faith >= requiredFaith;
        }

        /// <summary>Sums the scaling bonus a channel receives from all four attributes.</summary>
        public static float ResolveChannel(in DamageChannel channel, int strength, int dexterity,
            int intelligence, int faith)
        {
            return AttributeBonus(strength, channel.StrengthScaling) +
                AttributeBonus(dexterity, channel.DexterityScaling) +
                AttributeBonus(intelligence, channel.IntelligenceScaling) +
                AttributeBonus(faith, channel.FaithScaling);
        }

        public static float AttributeBonus(int level, float coefficient)
        {
            int attribute = Mathf.Clamp(level, 0, 99);
            float saturation = attribute <= 40 ? attribute * 0.015f :
                attribute <= 60 ? 0.6f + (attribute - 40) * 0.01f : 0.8f + (attribute - 60) * (0.2f / 39f);
            return saturation * Mathf.Clamp(coefficient, 0f, 2f);
        }
    }
}
