using System;

namespace ZZ
{
    /// <summary>
    /// Owns the running <see cref="CombatActionInstance"/> for one character and mints the sequence
    /// numbers that identify each swing. Separates local payment from replicated presentation.
    /// </summary>
    public sealed class CombatActionRunner
    {
        public CombatActionInstance Current { get; } = new CombatActionInstance();

        private uint m_nextSequence;

        public CombatWindowController Windows => Current.Windows;
        public HitRegistry Hits => Current.Hits;
        public AttackDefinition Attack => Current.Definition;
        public CombatActionPhase Phase => Current.Phase;
        public uint Sequence => Current.Sequence;
        public uint AttackId => Current.AttackId;
        public AttackSnapshot Snapshot => Current.Snapshot;
        public bool IsRunning => Current.IsRunning;

        /// <summary>Checks the entire cost before touching the resource or changing action state.</summary>
        public static bool TryPay(float available, float cost, Func<float, bool> consume)
        {
            if (float.IsNaN(available) || float.IsInfinity(available) ||
                float.IsNaN(cost) || float.IsInfinity(cost) || cost < 0f || available < cost)
            {
                return false;
            }

            return cost == 0f || (consume != null && consume(cost));
        }

        /// <summary>Begins presentation on any peer; never charges replicated attacks.</summary>
        public void Begin(AttackDefinition definition, CharacterManager attacker = null,
            WeaponItem weapon = null, AttackType attackType = default)
        {
            Current.Begin(definition, attacker, weapon, m_nextSequence + 1u, attackType);
            m_nextSequence = Current.Sequence;
        }

        /// <summary>Advances data-authored windows and the phase from the combat clock.</summary>
        public void Tick(ICombatActionClock clock)
        {
            if (clock == null ||
                !clock.TryGetActionProgress(out float normalizedTime, out float elapsedSeconds))
            {
                return;
            }

            Current.Tick(normalizedTime, elapsedSeconds);
        }

        /// <summary>Advances data-authored windows and the phase from explicit progress.</summary>
        public void Tick(float normalizedTime, float elapsedSeconds = 0f)
        {
            Current.Tick(normalizedTime, elapsedSeconds);
        }

        public void SetPhase(CombatActionPhase phase) => Current.SetPhase(phase);

        public void OpenWindow(CombatWindowType window) => Current.OpenWindow(window);

        public void CloseWindow(CombatWindowType window) => Current.CloseWindow(window);

        public void SetWindow(CombatWindowType window, bool isOpen)
        {
            Current.SetWindow(window, isOpen);
        }

        public float PreviewPoiseDamage(float damage) => Current.PreviewPoiseDamage(damage);

        public float ConsumeHyperArmor(float damage) => Current.ConsumeHyperArmor(damage);

        /// <summary>
        /// Clears every transient permission on interruption, death and normal completion. The
        /// reason lets cleanup systems tell a completed swing from one that was cut short.
        /// </summary>
        public void End(ActionEndReason reason = ActionEndReason.Completed)
        {
            Current.End(reason);
        }
    }
}
