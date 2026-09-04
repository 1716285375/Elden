using UnityEngine;

namespace ZZ
{
    public abstract class InstantCharacterEffect : ScriptableObject
    {
        [SerializeField, Min(0)] private int m_instantEffectId;

        /// <summary>
        /// Gets the stable catalog identifier assigned by the world effects manager.
        /// </summary>
        public int InstantEffectId => m_instantEffectId;

        /// <summary>
        /// Creates a transient copy so callers never mutate the authored effect asset.
        /// </summary>
        public InstantCharacterEffect CreateRuntimeInstance()
        {
            InstantCharacterEffect runtimeEffect = Instantiate(this);
            runtimeEffect.hideFlags = HideFlags.DontSave;
            return runtimeEffect;
        }

        /// <summary>
        /// Applies this effect's rules to the supplied character.
        /// </summary>
        public virtual void ProcessEffect(CharacterManager character)
        {
        }

        internal void AssignInstantEffectId(int instantEffectId)
        {
            m_instantEffectId = Mathf.Max(0, instantEffectId);
        }
    }
}
