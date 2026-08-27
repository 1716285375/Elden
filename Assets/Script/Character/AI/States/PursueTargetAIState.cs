namespace ZZ
{
    internal sealed class PursueTargetAIState : AICharacterState
    {
        internal override AICharacterStateId StateId =>
            AICharacterStateId.PursueTarget;

        internal override void Enter(AICharacterManager character)
        {
            character.SetNavigationEnabled(true);
            character.ResetMovementAnimationForPursuit();
        }

        internal override AICharacterStateId Tick(
            AICharacterManager character,
            float deltaTime)
        {
            if (!character.HasValidTarget || character.IsTargetBeyondLoseDistance)
            {
                character.ClearTarget();
                return AICharacterStateId.Idle;
            }

            if (character.IsPerformingAction && !character.CanMove)
            {
                character.StopMoving();
                return StateId;
            }

            if (character.IsTargetWithinCombatRange)
            {
                character.StopMoving();
                return AICharacterStateId.CombatStance;
            }

            character.MoveTowardsTarget();
            return StateId;
        }
    }
}
