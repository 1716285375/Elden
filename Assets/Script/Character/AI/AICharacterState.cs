using UnityEngine;

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

        /// <summary>
        /// Resolves a complete NavMesh path, sampling near the requested point when it is unreachable.
        /// </summary>
        protected static bool IsDestinationReachable(
            AICharacterManager character,
            Vector3 requestedDestination,
            out Vector3 reachableDestination)
        {
            reachableDestination = requestedDestination;
            return character != null &&
                character.TryResolveReachableDestination(
                    requestedDestination,
                    out reachableDestination);
        }
    }
}
