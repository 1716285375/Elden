using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Maps one buffered command to the authored attack that should follow.</summary>
    [Serializable]
    public struct AttackTransition
    {
        [SerializeField] private CombatCommand m_command;
        [SerializeField] private AttackDefinition m_nextAttack;

        public AttackTransition(CombatCommand command, AttackDefinition nextAttack)
        {
            m_command = command;
            m_nextAttack = nextAttack;
        }

        public CombatCommand Command => m_command;
        public AttackDefinition NextAttack => m_nextAttack;
        public bool IsValid => m_command != CombatCommand.None && m_nextAttack != null;
    }
}
