using System;
using System.Collections.Generic;

namespace ZZ
{
    /// <summary>
    /// Owns the current AI state and applies explicit enter/tick/exit transitions.
    /// </summary>
    internal sealed class AICharacterStateMachine
    {
        private readonly AICharacterManager m_character;
        private readonly Dictionary<AICharacterStateId, AICharacterState> m_states = new();

        private AICharacterState m_currentState;

        internal AICharacterStateMachine(
            AICharacterManager character,
            params AICharacterState[] states)
        {
            m_character = character ??
                throw new ArgumentNullException(nameof(character));
            foreach (AICharacterState state in states)
            {
                if (state != null)
                {
                    m_states[state.StateId] = state;
                }
            }
        }

        internal AICharacterStateId CurrentStateId =>
            m_currentState?.StateId ?? AICharacterStateId.Idle;

        internal void Tick(float deltaTime)
        {
            if (m_currentState == null)
            {
                ChangeState(AICharacterStateId.Idle);
            }

            AICharacterState stateAtTickStart = m_currentState;
            AICharacterStateId nextState = stateAtTickStart.Tick(
                m_character,
                deltaTime);
            if (m_currentState != stateAtTickStart)
            {
                return;
            }

            if (nextState != m_currentState.StateId)
            {
                ChangeState(nextState);
            }
        }

        internal void ChangeState(AICharacterStateId stateId)
        {
            if (m_currentState != null && m_currentState.StateId == stateId)
            {
                return;
            }

            if (!m_states.TryGetValue(stateId, out AICharacterState nextState))
            {
                throw new InvalidOperationException(
                    $"AI state {stateId} is not registered.");
            }

            m_currentState?.Exit(m_character);
            m_currentState = nextState;
            m_character.PublishState(stateId);
            m_currentState.Enter(m_character);
        }
    }
}
