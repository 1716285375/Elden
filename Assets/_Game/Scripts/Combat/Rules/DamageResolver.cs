using UnityEngine;

namespace ZZ
{
    /// <summary>Pure channel calculation shared by weapon, spell, critical and environmental damage.</summary>
    public static class DamageResolver
    {
        public static float ResolveChannel(float attackPower, float defense, float absorption)
        {
            float power = Mathf.Max(0f, attackPower);
            float mitigated = power * power / Mathf.Max(0.00001f, power + Mathf.Max(0f, defense));
            return mitigated * (1f - Mathf.Clamp(absorption, 0f, 100f) * 0.01f);
        }
    }
}
