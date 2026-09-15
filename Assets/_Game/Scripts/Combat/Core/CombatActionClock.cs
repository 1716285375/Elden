using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Supplies gameplay time for the running action. Gameplay must never read the Animator directly:
    /// transitions, layer blending, hit stop and remote peers all make that clock unreliable.
    /// </summary>
    public interface ICombatActionClock
    {
        /// <summary>
        /// Reports how far the current action clip has progressed.
        /// </summary>
        /// <param name="normalizedTime">0..1 progress through the clip; can exceed 1 when looping.</param>
        /// <param name="elapsedSeconds">Seconds since the clip started.</param>
        /// <returns>Whether an action clip is actually playing.</returns>
        bool TryGetActionProgress(out float normalizedTime, out float elapsedSeconds);
    }

    /// <summary>The current production clock: reads the action override layer from the Animator.</summary>
    public sealed class AnimatorCombatClock : ICombatActionClock
    {
        private readonly CharacterAnimatorManager m_animatorManager;

        public AnimatorCombatClock(CharacterAnimatorManager animatorManager)
        {
            m_animatorManager = animatorManager;
        }

        public bool TryGetActionProgress(out float normalizedTime, out float elapsedSeconds)
        {
            normalizedTime = 0f;
            elapsedSeconds = 0f;
            if (m_animatorManager == null ||
                !m_animatorManager.TryGetActionLayerStateInfo(out AnimatorStateInfo state))
            {
                return false;
            }

            normalizedTime = state.normalizedTime;
            elapsedSeconds = state.normalizedTime * state.length;
            return true;
        }
    }
}
