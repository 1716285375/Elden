namespace ZZ
{
    /// <summary>
    /// The held half of player input, kept apart from <see cref="CombatCommandBuffer"/> so that
    /// releasing a key can never be replayed later from a buffer.
    /// </summary>
    public sealed class CombatIntentState
    {
        private bool m_guardHeld;
        private bool m_aimHeld;
        private bool m_sprintHeld;
        private bool m_chargeHeld;
        private bool m_lockOnHeld;

        public bool IsGuardHeld => m_guardHeld;
        public bool IsAimHeld => m_aimHeld;
        public bool IsSprintHeld => m_sprintHeld;
        public bool IsChargeHeld => m_chargeHeld;
        public bool IsLockOnHeld => m_lockOnHeld;

        public bool IsHeld(CombatIntent intent)
        {
            return intent switch
            {
                CombatIntent.Guard => m_guardHeld,
                CombatIntent.Aim => m_aimHeld,
                CombatIntent.Sprint => m_sprintHeld,
                CombatIntent.Charge => m_chargeHeld,
                CombatIntent.LockOn => m_lockOnHeld,
                _ => false
            };
        }

        public void Set(CombatIntent intent, bool isHeld)
        {
            switch (intent)
            {
                case CombatIntent.Guard:
                    m_guardHeld = isHeld;
                    break;
                case CombatIntent.Aim:
                    m_aimHeld = isHeld;
                    break;
                case CombatIntent.Sprint:
                    m_sprintHeld = isHeld;
                    break;
                case CombatIntent.Charge:
                    m_chargeHeld = isHeld;
                    break;
                case CombatIntent.LockOn:
                    m_lockOnHeld = isHeld;
                    break;
            }
        }

        public void Clear()
        {
            m_guardHeld = false;
            m_aimHeld = false;
            m_sprintHeld = false;
            m_chargeHeld = false;
            m_lockOnHeld = false;
        }
    }
}
