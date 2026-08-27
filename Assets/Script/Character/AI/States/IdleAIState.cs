using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Dispatches idle, patrol, and sleep behavior before combat begins.</summary>
    internal sealed class IdleAIState : AICharacterState
    {
        private const float k_PatrolArrivalTolerance = 2f;

        private bool m_patrolComplete;
        private int m_patrolDestinationIndex;
        private bool m_hasPatrolDestination;
        private bool m_hasSelectedInitialPatrolPoint;
        private Vector3 m_currentPatrolDestination;
        private float m_restTimer;
        private bool m_sleepAnimationSet;

        internal override AICharacterStateId StateId => AICharacterStateId.Idle;

        internal override void Enter(AICharacterManager character)
        {
            character.StopMoving();
            m_hasPatrolDestination = false;
            if (character.IdleMode == IdleStateMode.Patrol)
            {
                character.SetNavigationEnabled(true);
            }
            else
            {
                character.SetNavigationEnabled(false);
            }
        }

        internal override AICharacterStateId Tick(
            AICharacterManager character,
            float deltaTime)
        {
            return character.IdleMode switch
            {
                IdleStateMode.Patrol => Patrol(character, deltaTime),
                IdleStateMode.Sleep => SleepUntilDisturbed(character),
                _ => Idle(character)
            };
        }

        internal override void Exit(AICharacterManager character)
        {
            character.StopMoving();
            m_sleepAnimationSet = false;
        }

        private static AICharacterStateId Idle(AICharacterManager character)
        {
            return character.TryAcquireTarget()
                ? AICharacterStateId.PursueTarget
                : AICharacterStateId.Idle;
        }

        private AICharacterStateId Patrol(
            AICharacterManager character,
            float deltaTime)
        {
            if (character.TryAcquireTarget())
            {
                return AICharacterStateId.PursueTarget;
            }

            IReadOnlyList<Vector3> patrolPoints =
                character.PatrolPath?.PatrolPoints;
            if (patrolPoints == null || patrolPoints.Count == 0)
            {
                character.SetNavigationEnabled(false);
                return AICharacterStateId.Idle;
            }

            if (!m_hasSelectedInitialPatrolPoint)
            {
                m_patrolDestinationIndex = Mathf.Max(
                    0,
                    character.PatrolPath.GetClosestPatrolPointIndex(
                        character.transform.position));
                m_hasSelectedInitialPatrolPoint = true;
            }

            if (m_patrolComplete)
            {
                character.StopMoving();
                if (!character.RepeatPatrol)
                {
                    character.SetNavigationEnabled(false);
                    return AICharacterStateId.Idle;
                }

                m_restTimer += deltaTime;
                if (m_restTimer < character.TimeBetweenPatrols)
                {
                    return AICharacterStateId.Idle;
                }

                m_patrolComplete = false;
                m_patrolDestinationIndex = 0;
                m_hasPatrolDestination = false;
                m_restTimer = 0f;
            }

            character.SetNavigationEnabled(true);
            if (!m_hasPatrolDestination)
            {
                if (m_patrolDestinationIndex >= patrolPoints.Count)
                {
                    m_patrolComplete = true;
                    return AICharacterStateId.Idle;
                }

                Vector3 patrolPoint = patrolPoints[m_patrolDestinationIndex];
                if (!IsDestinationReachable(
                        character,
                        patrolPoint,
                        out m_currentPatrolDestination) ||
                    !character.SetNavigationDestination(
                        m_currentPatrolDestination))
                {
                    AdvancePatrolPoint();
                    return AICharacterStateId.Idle;
                }

                m_hasPatrolDestination = true;
            }

            if (character.HasReachedNavigationDestination(
                    m_currentPatrolDestination,
                    k_PatrolArrivalTolerance))
            {
                character.StopMoving();
                AdvancePatrolPoint();
                return AICharacterStateId.Idle;
            }

            character.RotateTowardsAgent();
            return AICharacterStateId.Idle;
        }

        private AICharacterStateId SleepUntilDisturbed(
            AICharacterManager character)
        {
            character.SetNavigationEnabled(false);
            if (!m_sleepAnimationSet)
            {
                character.PlaySleepingAnimation();
                m_sleepAnimationSet = true;
            }

            character.TryAcquireTarget();
            if (!character.HasValidTarget)
            {
                return AICharacterStateId.Idle;
            }

            character.WakeFromSleep();
            return AICharacterStateId.PursueTarget;
        }

        private void AdvancePatrolPoint()
        {
            m_hasPatrolDestination = false;
            m_patrolDestinationIndex++;
        }
    }
}
