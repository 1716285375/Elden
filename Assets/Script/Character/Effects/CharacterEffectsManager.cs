using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterManager))]
    public class CharacterEffectsManager : MonoBehaviour
    {
        [Header("Visual Effects")]
        [SerializeField] private GameObject m_bloodSplatterVFX;

        [SerializeField] private CharacterManager m_character;

        [Header("Static Effects")]
        [SerializeField] private List<StaticCharacterEffect> m_staticEffects = new();

        protected CharacterManager Character => m_character;

        protected virtual void Awake()
        {
            m_character ??= GetComponent<CharacterManager>();
        }

        protected virtual void OnDestroy()
        {
            RemoveAllStaticEffects();
        }

        /// <summary>
        /// Spawns the blood splatter effect at a damage contact point, facing the hit direction.
        /// </summary>
        public void PlayBloodSplatterVFX(Vector3 contactPoint, Vector3 hitDirection)
        {
            if (m_bloodSplatterVFX == null)
            {
                return;
            }

            Quaternion rotation = hitDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(hitDirection)
                : Quaternion.identity;
            Instantiate(m_bloodSplatterVFX, contactPoint, rotation);
        }

        /// <summary>
        /// Creates and executes a runtime copy of an authored instant effect.
        /// </summary>
        public virtual void ProcessInstantEffect(InstantCharacterEffect effect)
        {
            if (effect == null || m_character == null)
            {
                Debug.LogWarning("An instant effect and target character are required.", this);
                return;
            }

            InstantCharacterEffect runtimeEffect = effect.CreateRuntimeInstance();
            ProcessRuntimeInstantEffect(runtimeEffect);
        }

        /// <summary>
        /// Executes and disposes a caller-configured runtime effect instance.
        /// </summary>
        public virtual void ProcessRuntimeInstantEffect(InstantCharacterEffect runtimeEffect)
        {
            if (runtimeEffect == null)
            {
                Debug.LogWarning("A runtime effect and target character are required.", this);
                return;
            }

            if ((runtimeEffect.hideFlags & HideFlags.DontSave) != HideFlags.DontSave)
            {
                Debug.LogWarning(
                    "ProcessRuntimeInstantEffect only accepts transient effect instances.",
                    this);
                return;
            }

            if (m_character == null)
            {
                Debug.LogWarning("A target character is required.", this);
                DestroyRuntimeEffect(runtimeEffect);
                return;
            }

            try
            {
                runtimeEffect.ProcessEffect(m_character);
            }
            finally
            {
                DestroyRuntimeEffect(runtimeEffect);
            }
        }

        /// <summary>Clones and applies a static effect unless that identifier is already active.</summary>
        public bool ProcessStaticEffect(StaticCharacterEffect effect)
        {
            if (effect == null || m_character == null || HasStaticEffect(effect.StaticEffectID))
            {
                return false;
            }

            StaticCharacterEffect runtimeEffect = effect.CreateRuntimeInstance();
            runtimeEffect.ProcessStaticEffect(m_character);
            m_staticEffects.Add(runtimeEffect);
            return true;
        }

        /// <summary>Removes and disposes the active static effect with the supplied identifier.</summary>
        public bool RemoveStaticEffect(int staticEffectID)
        {
            for (int effectIndex = m_staticEffects.Count - 1; effectIndex >= 0; effectIndex--)
            {
                StaticCharacterEffect runtimeEffect = m_staticEffects[effectIndex];
                if (runtimeEffect == null || runtimeEffect.StaticEffectID != staticEffectID)
                {
                    continue;
                }

                runtimeEffect.RemoveStaticEffect(m_character);
                m_staticEffects.RemoveAt(effectIndex);
                DestroyStaticEffect(runtimeEffect);
                return true;
            }

            return false;
        }

        /// <summary>Gets whether one static effect identifier is currently active.</summary>
        public bool HasStaticEffect(int staticEffectID)
        {
            return m_staticEffects.Exists(effect =>
                effect != null && effect.StaticEffectID == staticEffectID);
        }

        private void RemoveAllStaticEffects()
        {
            for (int effectIndex = m_staticEffects.Count - 1; effectIndex >= 0; effectIndex--)
            {
                StaticCharacterEffect runtimeEffect = m_staticEffects[effectIndex];
                runtimeEffect?.RemoveStaticEffect(m_character);
                DestroyStaticEffect(runtimeEffect);
            }

            m_staticEffects.Clear();
        }

        private static void DestroyStaticEffect(StaticCharacterEffect runtimeEffect)
        {
            if (runtimeEffect == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeEffect);
            }
            else
            {
                DestroyImmediate(runtimeEffect);
            }
        }

        private static void DestroyRuntimeEffect(InstantCharacterEffect runtimeEffect)
        {
            if (runtimeEffect == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeEffect);
            }
            else
            {
                DestroyImmediate(runtimeEffect);
            }
        }
    }
}
