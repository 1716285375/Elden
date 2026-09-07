using UnityEngine;

namespace ZZ
{
    /// <summary>Grants the rounded fifty-percent Strength bonus used while two-handing.</summary>
    [GameAsset(FileName = "Two Handing Effect", MenuName = "ZZ/Effects/Two Handing")]
    public class TwoHandingEffect : StaticCharacterEffect
    {
        private int m_strengthGained;

        /// <summary>Gets the exact modifier retained by this runtime instance.</summary>
        public int StrengthGained => m_strengthGained;

        /// <inheritdoc />
        public override void ProcessStaticEffect(CharacterManager character)
        {
            CharacterStatsManager statsManager = character?.CharacterStatsManager;
            if (statsManager == null || m_strengthGained != 0)
            {
                return;
            }

            m_strengthGained = CalculateStrengthBonus(statsManager.StrengthLevel);
            statsManager.ModifyStrengthModifier(m_strengthGained);
        }

        /// <inheritdoc />
        public override void RemoveStaticEffect(CharacterManager character)
        {
            CharacterStatsManager statsManager = character?.CharacterStatsManager;
            if (statsManager != null && m_strengthGained != 0)
            {
                statsManager.ModifyStrengthModifier(-m_strengthGained);
            }

            m_strengthGained = 0;
        }

        /// <summary>Calculates the rounded fifty-percent bonus from the base Strength level.</summary>
        public static int CalculateStrengthBonus(int strengthLevel)
        {
            return Mathf.RoundToInt(Mathf.Max(0, strengthLevel) / 2f);
        }
    }
}
