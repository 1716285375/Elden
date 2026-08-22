namespace ZZ
{
    internal sealed class AttackAIState : AICharacterState
    {
        private bool m_attackStarted;

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
            m_attackStarted = false;
        }
    }
}
