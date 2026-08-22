namespace ZZ
{
    internal sealed class DeadAIState : AICharacterState
    {
        internal override AICharacterStateId StateId => AICharacterStateId.Dead;

        internal override void Enter(AICharacterManager character)
        {
            character.StopMoving();
            character.CloseAttackDamageColliders();
            character.ClearTarget();
        }

        internal override AICharacterStateId Tick(
            AICharacterManager character,
            float deltaTime)
        {
            return StateId;
        }
    }
}
