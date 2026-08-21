using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterManager))]
    public class CharacterEffectsManager : MonoBehaviour
    {
        [SerializeField] private CharacterManager m_character;

        protected CharacterManager Character => m_character;

        protected virtual void Awake()
        {
            m_character ??= GetComponent<CharacterManager>();
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

            try
            {
                runtimeEffect.ProcessEffect(m_character);
            }
            finally
            {
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
}
