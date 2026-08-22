namespace ZZ
{
    internal sealed class IdleAIState : AICharacterState
    {
        internal override AICharacterStateId StateId => AICharacterStateId.Idle;

        internal override void Enter(AICharacterManager character)
        {
            character.StopMoving();
        }

        internal override AICharacterStateId Tick(
            AICharacterManager character,
            float deltaTime)
        {
            return character.TryAcquireTarget()
                ? AICharacterStateId.PursueTarget
                : StateId;
        }
    }
}
