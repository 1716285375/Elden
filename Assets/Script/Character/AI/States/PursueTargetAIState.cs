namespace ZZ
{
    internal sealed class PursueTargetAIState : AICharacterState
    {
        internal override AICharacterStateId StateId =>
            AICharacterStateId.PursueTarget;

        internal override AICharacterStateId Tick(
            AICharacterManager character,
            float deltaTime)
        {
            if (!character.HasValidTarget || character.IsTargetBeyondLoseDistance)
            {
                character.ClearTarget();
                return AICharacterStateId.Idle;
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
