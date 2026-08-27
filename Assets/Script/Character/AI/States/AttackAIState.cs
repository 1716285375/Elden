namespace ZZ
{
    internal sealed class AttackAIState : AICharacterState
    {
        private bool m_attackStarted;
        private bool m_hasPerformedCombo;
        private bool m_onlyPerformComboIfInitialAttackHits;
        private bool m_willPerformCombo;

        internal override AICharacterStateId StateId => AICharacterStateId.Attack;

        internal override void Enter(AICharacterManager character)
        {
            character.StopMoving();
            character.FaceTarget();
            m_attackStarted = character.TryStartAttack();
        }

        internal override AICharacterStateId Tick(
            AICharacterManager character,
            float deltaTime)
        {
            PerformCombo(character);
            if (!m_attackStarted || !character.IsPerformingAction)
            {
                return AICharacterStateId.CombatStance;
            }

            if (character.HasValidTarget && character.CanRotate)
            {
                character.FaceTarget();
            }

            return StateId;
        }

        internal override void Exit(AICharacterManager character)
        {
            character.CloseAttackDamageColliders();
            character.DisableComboWindow();
            m_attackStarted = false;
            m_hasPerformedCombo = false;
            m_onlyPerformComboIfInitialAttackHits = false;
            m_willPerformCombo = false;
        }

        internal void ConfigureComboDecision(
            bool willPerformCombo,
            bool onlyPerformComboIfInitialAttackHits)
        {
            m_willPerformCombo = willPerformCombo;
            m_onlyPerformComboIfInitialAttackHits =
                onlyPerformComboIfInitialAttackHits;
        }

        private void PerformCombo(AICharacterManager character)
        {
            if (!m_willPerformCombo || m_hasPerformedCombo)
            {
                return;
            }

            AICharacterAttackAction comboAction =
                character.CurrentAttackAction?.ComboAction;
            if (comboAction == null ||
                !character.CanEnterComboWindow ||
                !comboAction.IsInRange(character.TargetDistance) ||
                m_onlyPerformComboIfInitialAttackHits &&
                !character.HasHitTargetDuringCombo)
            {
                return;
            }

            m_hasPerformedCombo = character.TryStartCombo(comboAction);
        }
    }
}
