using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// The command-driven entry point into a weapon's attacks. Attack lookup by
    /// <see cref="CombatCommand"/> and <see cref="AttackDefinition.AttackId"/> replaces growing
    /// <see cref="AttackType"/> switches.
    /// </summary>
    [CreateAssetMenu(menuName = "ZZ/Combat/Weapon Moveset")]
    public sealed class WeaponMoveset : ScriptableObject
    {
        [SerializeField] private AttackDefinition[] m_attacks = Array.Empty<AttackDefinition>();
        [SerializeField] private AttackDefinition m_guardCounter;
        [SerializeField] private AttackDefinition m_rollAttack;
        [SerializeField] private AttackDefinition m_backstepAttack;
        [SerializeField] private AttackDefinition m_runningAttack;
        [SerializeField] private AttackDefinition m_jumpAttack;

        public AttackDefinition[] Attacks => m_attacks;

        /// <summary>The attack a weapon performs after a successful block. Replaces the hard-coded HeavyAttack02.</summary>
        public AttackDefinition GuardCounter => m_guardCounter;

        public AttackDefinition RollAttack => m_rollAttack;
        public AttackDefinition BackstepAttack => m_backstepAttack;
        public AttackDefinition RunningAttack => m_runningAttack;
        public AttackDefinition JumpAttack => m_jumpAttack;

        /// <summary>Resolves the opening attack for a command issued from neutral.</summary>
        public AttackDefinition GetEntry(CombatCommand command)
        {
            if (m_attacks == null)
            {
                return null;
            }

            foreach (AttackDefinition attack in m_attacks)
            {
                if (attack != null && attack.EntryCommand == command)
                {
                    return attack;
                }
            }

            return null;
        }

        public AttackDefinition GetAttack(uint attackId)
        {
            if (m_attacks == null || attackId == 0u)
            {
                return null;
            }

            foreach (AttackDefinition attack in m_attacks)
            {
                if (attack != null && attack.AttackId == attackId)
                {
                    return attack;
                }
            }

            return null;
        }

        /// <summary>Legacy adapter for animation states still keyed by <see cref="AttackType"/>.</summary>
        public AttackDefinition GetAttack(AttackType attackType)
        {
            if (m_attacks == null)
            {
                return null;
            }

            foreach (AttackDefinition attack in m_attacks)
            {
                if (attack != null && attack.AttackType == attackType)
                {
                    return attack;
                }
            }

            return null;
        }

        /// <summary>Resolves a context attack (guard counter, roll, backstep, running, jump) by name.</summary>
        public AttackDefinition GetContextAttack(CombatMoveContext context)
        {
            return context switch
            {
                CombatMoveContext.GuardCounter => m_guardCounter,
                CombatMoveContext.Roll => m_rollAttack,
                CombatMoveContext.Backstep => m_backstepAttack,
                CombatMoveContext.Running => m_runningAttack,
                CombatMoveContext.Jump => m_jumpAttack,
                _ => null
            };
        }
    }

    /// <summary>Movement-state attacks that are selected by context instead of by button.</summary>
    public enum CombatMoveContext
    {
        None = 0,
        GuardCounter = 1,
        Roll = 2,
        Backstep = 3,
        Running = 4,
        Jump = 5
    }
}
