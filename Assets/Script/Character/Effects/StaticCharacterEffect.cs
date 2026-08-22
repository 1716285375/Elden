using UnityEngine;

namespace ZZ
{
    /// <summary>Defines a persistent character effect that can be applied and removed by identifier.</summary>
    public abstract class StaticCharacterEffect : ScriptableObject
    {
        [SerializeField, Min(0)] private int m_staticEffectID;

        /// <summary>Gets the stable identifier used to prevent duplicate runtime effects.</summary>
        public int StaticEffectID => m_staticEffectID;

        /// <summary>Creates a transient per-character copy that may retain removal state.</summary>
        public StaticCharacterEffect CreateRuntimeInstance()
        {
            StaticCharacterEffect runtimeEffect = Instantiate(this);
            runtimeEffect.name = $"{name} (Runtime)";
            runtimeEffect.hideFlags = HideFlags.DontSave;
            return runtimeEffect;
        }

        /// <summary>Applies this runtime effect to the supplied character.</summary>
        public abstract void ProcessStaticEffect(CharacterManager character);

        /// <summary>Removes this runtime effect from the supplied character.</summary>
        public abstract void RemoveStaticEffect(CharacterManager character);
    }
}
