using System.Collections.Generic;

namespace ZZ
{
    /// <summary>
    /// Per-action deduplication of hit targets, scoped by strike group. A multi-hit attack declares
    /// one group per hit window, so the same target can be struck once per group instead of once
    /// per action.
    /// </summary>
    public sealed class HitRegistry
    {
        private readonly Dictionary<CharacterManager, List<int>> m_groups = new();

        /// <summary>Number of distinct targets settled by this action.</summary>
        public int Count => m_groups.Count;

        /// <summary>Total contacts settled across every target and group.</summary>
        public int TotalContacts
        {
            get
            {
                int total = 0;
                foreach (List<int> groups in m_groups.Values)
                {
                    total += groups.Count;
                }

                return total;
            }
        }

        public IReadOnlyCollection<CharacterManager> Targets => m_groups.Keys;

        public bool Contains(CharacterManager target)
        {
            return target != null && m_groups.ContainsKey(target);
        }

        public bool Contains(CharacterManager target, int hitGroup)
        {
            return target != null &&
                m_groups.TryGetValue(target, out List<int> groups) &&
                groups.Contains(hitGroup);
        }

        /// <summary>
        /// Registers one target/group pair once. <paramref name="hitIndex"/> is the target's contact
        /// ordinal across the whole action, which keeps multi-group hits distinguishable.
        /// </summary>
        public bool TryRegister(CharacterManager target, int hitGroup, out int hitIndex)
        {
            if (target == null)
            {
                hitIndex = -1;
                return false;
            }

            if (!m_groups.TryGetValue(target, out List<int> groups))
            {
                groups = new List<int>(2);
                m_groups[target] = groups;
            }

            if (groups.Contains(hitGroup))
            {
                hitIndex = groups.Count;
                return false;
            }

            groups.Add(hitGroup);
            hitIndex = groups.Count - 1;
            return true;
        }

        public void Clear() => m_groups.Clear();
    }
}
