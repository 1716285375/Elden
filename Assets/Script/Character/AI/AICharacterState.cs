namespace ZZ
{
    /// <summary>
    /// Base state for one server-authoritative AI behavior step.
    /// </summary>
    internal abstract class AICharacterState
    {
        internal abstract AICharacterStateId StateId { get; }

        internal virtual void Enter(AICharacterManager character)
        {
        }

        internal abstract AICharacterStateId Tick(
            AICharacterManager character,
            float deltaTime);

        internal virtual void Exit(AICharacterManager character)
        {
        }
    }
}
