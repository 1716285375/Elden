using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Authored knock-back and stagger rules for one attack. Combined with the defender's state by
    /// <see cref="ReactionResolver"/> instead of branching on weapon or boss types.
    /// </summary>
    [Serializable]
    public struct ReactionProfile
    {
        [Tooltip("Scales with poise damage to pick Ping / Light / Medium / Heavy stagger.")]
        [SerializeField, Min(0f)] private float m_reactionPower;

        [SerializeField] private ReactionType m_poiseBreakReaction;
        [SerializeField] private ReactionType m_stanceBreakReaction;

        [Tooltip("Allows the attack to launch a defender that has no hyper armor.")]
        [SerializeField] private bool m_canLaunch;

        public float ReactionPower => Mathf.Max(0f, m_reactionPower);
        public ReactionType PoiseBreakReaction => m_poiseBreakReaction;
        public ReactionType StanceBreakReaction => m_stanceBreakReaction;
        public bool CanLaunch => m_canLaunch;

        public static ReactionProfile Default => new ReactionProfile
        {
            m_reactionPower = 10f,
            m_poiseBreakReaction = ReactionType.MediumStagger,
            m_stanceBreakReaction = ReactionType.StanceBreak,
            m_canLaunch = false
        };
    }
}
