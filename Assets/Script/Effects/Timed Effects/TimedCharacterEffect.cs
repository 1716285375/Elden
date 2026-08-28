using UnityEngine;

namespace ZZ
{
    /// <summary>Defines one runtime-cloned effect processed by the shared effect tick.</summary>
    public abstract class TimedCharacterEffect : ScriptableObject
    {
        [SerializeField, Min(0)] private int m_timedEffectID;
        [SerializeField, Min(0.01f)] private float m_defaultTimeLengthOnEffect = 60f;

        private float m_timeRemainingOnEffect;

        /// <summary>Gets the stable identifier used to refresh equivalent runtime effects.</summary>
        public int TimedEffectID => m_timedEffectID;

        /// <summary>Gets the authored duration restored when the effect is reapplied.</summary>
        public float DefaultTimeLengthOnEffect =>
            Mathf.Max(0.01f, m_defaultTimeLengthOnEffect);

        /// <summary>Gets the runtime duration remaining before automatic removal.</summary>
        public float TimeRemainingOnEffect => Mathf.Max(0f, m_timeRemainingOnEffect);

        /// <summary>Creates a transient copy so the authored asset is never mutated.</summary>
        public TimedCharacterEffect CreateRuntimeInstance()
        {
            TimedCharacterEffect runtimeEffect = Instantiate(this);
            runtimeEffect.hideFlags = HideFlags.DontSave;
            runtimeEffect.RefreshDuration();
            return runtimeEffect;
        }

        /// <summary>Applies one tick of this effect to the supplied character.</summary>
        public virtual void ProcessEffect(CharacterManager character)
        {
        }

        /// <summary>Reverts any state owned by this effect before it is discarded.</summary>
        public virtual void RemoveEffect(CharacterManager character)
        {
        }

        /// <summary>Restores the runtime duration without creating another effect instance.</summary>
        public void RefreshDuration()
        {
            m_timeRemainingOnEffect = DefaultTimeLengthOnEffect;
        }

        /// <summary>Consumes one non-negative amount of runtime duration.</summary>
        public void AdvanceTime(float elapsedTime)
        {
            m_timeRemainingOnEffect = Mathf.Max(
                0f,
                m_timeRemainingOnEffect - Mathf.Max(0f, elapsedTime));
        }

        internal void AssignTimedEffectID(int timedEffectID)
        {
            m_timedEffectID = Mathf.Max(0, timedEffectID);
        }
    }
}
