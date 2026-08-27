using UnityEngine;

namespace ZZ
{
    /// <summary>Turns toward and inspects one server-authorized sound position.</summary>
    internal sealed class InvestigateSoundAIState : AICharacterState
    {
        private const float k_InvestigationTime = 3f;

        private Vector3 m_positionOfSound;
        private Vector3 m_reachableDestination;
        private bool m_hasPivoted;
        private bool m_destinationSet;
        private bool m_destinationReached;
        private float m_investigationTimer;

        internal override AICharacterStateId StateId =>
            AICharacterStateId.InvestigateSound;

        internal void SetSoundPosition(Vector3 positionOfSound)
        {
            m_positionOfSound = positionOfSound;
        }

        internal override void Enter(AICharacterManager character)
        {
            ResetStateFlags();
            character.SetNavigationEnabled(true);
        }

        internal override AICharacterStateId Tick(
            AICharacterManager character,
            float deltaTime)
        {
            if (character.TryAcquireTarget())
            {
                return AICharacterStateId.PursueTarget;
            }

            if (character.IsPerformingAction && !character.CanMove)
            {
                character.StopMoving();
                return StateId;
            }

            if (!m_hasPivoted)
            {
                character.PivotTowardsPosition(m_positionOfSound);
                m_hasPivoted = true;
                return StateId;
            }

            if (!m_destinationSet)
            {
                if (!IsDestinationReachable(
                        character,
                        m_positionOfSound,
                        out m_reachableDestination) ||
                    !character.SetNavigationDestination(m_reachableDestination))
                {
                    return AICharacterStateId.Idle;
                }

                m_destinationSet = true;
            }

            if (!m_destinationReached &&
                character.HasReachedNavigationDestination(
                    m_reachableDestination,
                    character.NavigationStoppingDistance))
            {
                m_destinationReached = true;
                character.StopMoving();
            }

            if (!m_destinationReached)
            {
                character.RotateTowardsAgent();
                return StateId;
            }

            m_investigationTimer += deltaTime;
            return m_investigationTimer >= k_InvestigationTime
                ? AICharacterStateId.Idle
                : StateId;
        }

        internal override void Exit(AICharacterManager character)
        {
            character.StopMoving();
            ResetStateFlags();
        }

        private void ResetStateFlags()
        {
            m_hasPivoted = false;
            m_destinationSet = false;
            m_destinationReached = false;
            m_investigationTimer = 0f;
        }
    }
}
