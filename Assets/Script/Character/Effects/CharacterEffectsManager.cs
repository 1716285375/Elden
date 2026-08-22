using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterManager))]
    public class CharacterEffectsManager : MonoBehaviour
    {
        [Header("Visual Effects")]
        [SerializeField] private GameObject m_bloodSplatterVFX;

        [SerializeField] private CharacterManager m_character;

        protected CharacterManager Character => m_character;

        protected virtual void Awake()
        {
            m_character ??= GetComponent<CharacterManager>();
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
