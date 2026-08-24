using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterManager))]
    public class CharacterEffectsManager : MonoBehaviour
    {
        [Header("Visual Effects")]
        [SerializeField] private GameObject m_bloodSplatterVFX;
        [SerializeField] private GameObject m_criticalBloodSplatterVFX;

        [SerializeField] private CharacterManager m_character;

        [Header("Static Effects")]
        [SerializeField] private List<StaticCharacterEffect> m_staticEffects = new();

        private readonly List<GameObject> m_currentActionEffects = new();

        protected CharacterManager Character => m_character;

        protected virtual void Awake()
        {
            m_character ??= GetComponent<CharacterManager>();
        }

        protected virtual void OnDestroy()
        {
            DestroyAllCurrentActionEffects();
            RemoveAllStaticEffects();
        }

        /// <summary>Tracks one locally instantiated effect owned by the current action.</summary>
        public void RegisterCurrentActionEffect(GameObject actionEffect)
        {
            m_currentActionEffects.RemoveAll(effect => effect == null);
            if (actionEffect != null && !m_currentActionEffects.Contains(actionEffect))
            {
                m_currentActionEffects.Add(actionEffect);
            }
        }

        /// <summary>Destroys every local warm-up or charge effect owned by the current action.</summary>
        public void DestroyAllCurrentActionEffects()
        {
            for (int effectIndex = m_currentActionEffects.Count - 1;
                effectIndex >= 0;
                effectIndex--)
            {
                GameObject actionEffect = m_currentActionEffects[effectIndex];
                if (actionEffect == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(actionEffect);
                }
                else
                {
                    DestroyImmediate(actionEffect);
                }
            }

            m_currentActionEffects.Clear();
        }

        /// <summary>
        /// Spawns the blood splatter effect at a damage contact point, facing the hit direction.
        /// </summary>
        public void PlayBloodSplatterVFX(Vector3 contactPoint, Vector3 hitDirection)
        {
            SpawnBloodSplatter(m_bloodSplatterVFX, contactPoint, hitDirection, 1f);
        }

        /// <summary>Spawns an emphasized blood burst at a critical hit frame.</summary>
        public void PlayCriticalBloodSplatterVFX(
            Vector3 contactPoint,
            Vector3 hitDirection)
        {
            GameObject criticalVFX = m_criticalBloodSplatterVFX != null
                ? m_criticalBloodSplatterVFX
                : m_bloodSplatterVFX;
            SpawnBloodSplatter(criticalVFX, contactPoint, hitDirection, 1.5f);
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

        private static void SpawnBloodSplatter(
            GameObject bloodVFX,
            Vector3 contactPoint,
            Vector3 hitDirection,
            float scaleMultiplier)
        {
            if (bloodVFX == null)
            {
                return;
            }

            Quaternion rotation = hitDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(hitDirection)
                : Quaternion.identity;
            GameObject instance = Instantiate(bloodVFX, contactPoint, rotation);
            instance.transform.localScale *= Mathf.Max(0f, scaleMultiplier);
        }
    }
}
