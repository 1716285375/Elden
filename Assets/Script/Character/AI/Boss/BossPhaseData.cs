using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Defines the attack set and movement tuning used by one Boss phase.</summary>
    [CreateAssetMenu(
        menuName = "Character/AI/Boss Phase Data",
        fileName = "Boss Phase Data")]
    public class BossPhaseData : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float m_healthThreshold = 1f;
        [SerializeField, Min(0f)] private float m_movementSpeed = 2.4f;
        [SerializeField] private List<BossAttackData> m_attacks = new();

        public float HealthThreshold => m_healthThreshold;
        public float MovementSpeed => m_movementSpeed;
        public IReadOnlyList<BossAttackData> Attacks => m_attacks;

        /// <summary>Gets the largest authored attack range in this phase.</summary>
        public float GetMaximumAttackRange()
        {
            float maximumRange = 0f;
            foreach (BossAttackData attack in m_attacks)
            {
                if (attack != null)
                {
                    maximumRange = Mathf.Max(maximumRange, attack.MaximumRange);
                }
            }

            return maximumRange;
        }

        /// <summary>Gets whether at least one attack can reach the supplied distance.</summary>
        public bool HasAttackInRange(float targetDistance)
        {
            foreach (BossAttackData attack in m_attacks)
            {
                if (attack != null &&
                    attack.SelectionWeight > 0f &&
                    attack.IsInRange(targetDistance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Selects one valid attack using its authored relative weight.</summary>
        public BossAttackData SelectAttack(float targetDistance)
        {
            float totalWeight = 0f;
            foreach (BossAttackData attack in m_attacks)
            {
                if (attack != null && attack.IsInRange(targetDistance))
                {
                    totalWeight += Mathf.Max(0f, attack.SelectionWeight);
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float selection = Random.value * totalWeight;
            foreach (BossAttackData attack in m_attacks)
            {
                if (attack == null || !attack.IsInRange(targetDistance))
                {
                    continue;
                }

                selection -= Mathf.Max(0f, attack.SelectionWeight);
                if (selection <= 0f)
                {
                    return attack;
                }
            }

            return null;
        }
    }
}
