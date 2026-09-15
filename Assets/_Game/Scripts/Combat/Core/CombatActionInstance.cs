using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// One concrete swing of an attack. <see cref="AttackDefinition"/> is the static template;
    /// this instance is the runtime context of a single execution: its sequence, its snapshot, its
    /// strike-scoped hit registry and its independently tracked phase.
    /// </summary>
    public sealed class CombatActionInstance
    {
        public CombatWindowController Windows { get; } = new CombatWindowController();
        public HitRegistry Hits { get; } = new HitRegistry();

        public uint Sequence { get; private set; }
        public uint AttackId => Definition != null ? Definition.AttackId : 0u;
        public AttackDefinition Definition { get; private set; }
        public CharacterManager Attacker { get; private set; }
        public WeaponItem Weapon { get; private set; }
        public AttackSnapshot Snapshot { get; private set; }

        public CombatActionPhase Phase { get; private set; }
        public ActionEndReason EndReason { get; private set; } = ActionEndReason.Completed;
        public float NormalizedTime { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float RemainingHyperArmor { get; private set; }
        public bool IsRunning => Definition != null;

        private bool m_warnedAboutPhases;
        private bool m_warnedAboutHyperArmor;

        /// <summary>Begins presentation on any peer; never charges replicated costs.</summary>
        public void Begin(AttackDefinition definition, CharacterManager attacker, WeaponItem weapon,
            uint sequence, AttackType attackType = default)
        {
            Windows.Clear();
            Hits.Clear();
            m_warnedAboutPhases = false;
            m_warnedAboutHyperArmor = false;
            Definition = definition;
            Attacker = attacker;
            Weapon = weapon;
            Sequence = sequence;
            Phase = CombatActionPhase.Startup;
            EndReason = ActionEndReason.Completed;
            NormalizedTime = 0f;
            ElapsedSeconds = 0f;
            RemainingHyperArmor = definition != null ? definition.HyperArmorPoise : 0f;
            Snapshot = AttackSnapshot.Capture(definition, attacker, weapon, attackType);
        }

        /// <summary>Sets the lifecycle phase explicitly. Windows never change it.</summary>
        public void SetPhase(CombatActionPhase phase) => Phase = phase;

        public void OpenWindow(CombatWindowType window) => Windows.Set(window, true);

        public void CloseWindow(CombatWindowType window)
        {
            Windows.Set(window, false);
            if (window == CombatWindowType.HyperArmor)
            {
                RemainingHyperArmor = 0f;
            }
        }

        public void SetWindow(CombatWindowType window, bool isOpen)
        {
            if (isOpen)
            {
                OpenWindow(window);
                return;
            }

            CloseWindow(window);
        }

        /// <summary>
        /// Advances authored windows and the phase. The phase is resolved from the definition's
        /// authored phase spans, or from hit-window history for clips that never authored them.
        /// </summary>
        public void Tick(float normalizedTime, float elapsedSeconds = 0f)
        {
            if (!IsRunning)
            {
                return;
            }

            NormalizedTime = normalizedTime;
            ElapsedSeconds = elapsedSeconds;
            ActionWindow hyperArmor = Definition.HyperArmorWindow;
            if (hyperArmor.IsOpen)
            {
                Windows.Set(CombatWindowType.HyperArmor,
                    hyperArmor.Contains(normalizedTime) && RemainingHyperArmor > 0f);
            }
            else if (RemainingHyperArmor > 0f)
            {
                // Legacy: hyper armor used to be implied by the hit window. Warn until authored.
                WarnAboutMissingHyperArmorWindow();
                Windows.Set(CombatWindowType.HyperArmor, Windows.IsOpen(CombatWindowType.Hit));
            }

            Phase = ResolvePhase(normalizedTime);
        }

        private CombatActionPhase ResolvePhase(float normalizedTime)
        {
            if (normalizedTime >= 1f)
            {
                return CombatActionPhase.Finished;
            }

            if (Definition.HasAuthoredPhases)
            {
                if (normalizedTime >= Definition.RecoveryPhase.Start)
                {
                    return CombatActionPhase.Recovery;
                }

                return Definition.ActivePhase.Contains(normalizedTime)
                    ? CombatActionPhase.Active
                    : CombatActionPhase.Startup;
            }

            // Legacy clips never authored phases; mirror the hit window so they behave as before.
            WarnAboutMissingPhases();
            if (Windows.IsOpen(CombatWindowType.Hit))
            {
                return CombatActionPhase.Active;
            }

            return normalizedTime > 0f ? CombatActionPhase.Recovery : CombatActionPhase.Startup;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void WarnAboutMissingPhases()
        {
            if (m_warnedAboutPhases)
            {
                return;
            }

            m_warnedAboutPhases = true;
            Debug.LogWarning(
                $"Attack '{Definition.name}' has no authored phase windows; " +
                "ActionPhase falls back to hit-window history. Run the Combat Id migration tool.",
                Definition);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void WarnAboutMissingHyperArmorWindow()
        {
            if (m_warnedAboutHyperArmor)
            {
                return;
            }

            m_warnedAboutHyperArmor = true;
            Debug.LogWarning(
                $"Attack '{Definition.name}' has hyper armor but no authored HyperArmorWindow; " +
                "it falls back to the hit window. Author the window in the attack definition.",
                Definition);
        }

        public float PreviewPoiseDamage(float damage)
        {
            float armor = Windows.IsOpen(CombatWindowType.HyperArmor) ? RemainingHyperArmor : 0f;
            return Mathf.Max(0f, damage - armor);
        }

        public float ConsumeHyperArmor(float damage)
        {
            float remaining = PreviewPoiseDamage(damage);
            RemainingHyperArmor = Mathf.Max(0f, RemainingHyperArmor - Mathf.Max(0f, damage - remaining));
            if (RemainingHyperArmor <= 0f)
            {
                Windows.Set(CombatWindowType.HyperArmor, false);
            }

            return remaining;
        }

        /// <summary>Clears every transient permission on interruption, death and normal completion.</summary>
        public void End(ActionEndReason reason = ActionEndReason.Completed)
        {
            EndReason = reason;
            Windows.Clear();
            Hits.Clear();
            RemainingHyperArmor = 0f;
            NormalizedTime = 0f;
            ElapsedSeconds = 0f;
            Snapshot = default;
            Definition = null;
            Attacker = null;
            Weapon = null;
            Phase = CombatActionPhase.None;
        }
    }
}
