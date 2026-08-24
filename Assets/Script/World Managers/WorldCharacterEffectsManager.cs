using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    public class WorldCharacterEffectsManager : MonoBehaviour
    {
        private const string k_BlockedDamageEffectResourcePath =
            "Effects/Take Blocked Damage Effect";
        private const string k_CriticalDamageEffectResourcePath =
            "Effects/Take Critical Damage Effect";

        private static WorldCharacterEffectsManager s_instance;

        [SerializeField] private List<InstantCharacterEffect> m_instantEffects = new();
        [SerializeField] private TakeDamageEffect m_takeDamageEffect;
        [SerializeField] private TakeBlockedDamageEffect m_takeBlockedDamageEffect;
        [SerializeField] private TakeCriticalDamageEffect m_takeCriticalDamageEffect;

        public static WorldCharacterEffectsManager Instance => s_instance;
        public TakeDamageEffect TakeDamageEffect => m_takeDamageEffect;
        public TakeBlockedDamageEffect TakeBlockedDamageEffect =>
            m_takeBlockedDamageEffect ??= Resources.Load<TakeBlockedDamageEffect>(
                k_BlockedDamageEffectResourcePath);
        public TakeCriticalDamageEffect TakeCriticalDamageEffect =>
            m_takeCriticalDamageEffect ??= Resources.Load<TakeCriticalDamageEffect>(
                k_CriticalDamageEffectResourcePath);

        /// <summary>
        /// Gets the authored instant effects in their stable identifier order.
        /// </summary>
        public IReadOnlyList<InstantCharacterEffect> InstantEffects => m_instantEffects;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            AssignInstantEffectIds();
            DontDestroyOnLoad(gameObject);
        }

        private void OnValidate()
        {
            AssignInstantEffectIds();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// Finds an instant effect by its catalog identifier.
        /// </summary>
        public bool TryGetInstantEffect(
            int instantEffectId,
            out InstantCharacterEffect instantEffect)
        {
            if (instantEffectId < 0 || instantEffectId >= m_instantEffects.Count)
            {
                instantEffect = null;
                return false;
            }

            instantEffect = m_instantEffects[instantEffectId];
            return instantEffect != null && instantEffect.InstantEffectId == instantEffectId;
        }

        private void AssignInstantEffectIds()
        {
            for (int effectIndex = 0; effectIndex < m_instantEffects.Count; effectIndex++)
            {
                m_instantEffects[effectIndex]?.AssignInstantEffectId(effectIndex);
            }
        }
    }
}
