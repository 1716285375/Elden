namespace ZZ
{
    internal sealed class CombatStanceAIState : AICharacterState
    {
        internal override AICharacterStateId StateId =>
            AICharacterStateId.CombatStance;

        internal override void Enter(AICharacterManager character)
        {
            character.StopMoving();
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

            if (!character.IsTargetWithinCombatRange)
            {
                return AICharacterStateId.PursueTarget;
            }

            character.FaceTarget();
            return character.CanStartAttack
                ? AICharacterStateId.Attack
                : StateId;
        }
    }
}
