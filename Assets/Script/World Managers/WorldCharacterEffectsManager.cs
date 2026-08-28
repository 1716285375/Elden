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
        private const string k_TakePoisonBuildupEffectResourcePath =
            "Effects/Take Poison Buildup Effect";
        private const string k_TakeBleedBuildupEffectResourcePath =
            "Effects/Take Bleed Buildup Effect";
        private const string k_DegradePoisonBuildupEffectResourcePath =
            "Effects/Degrade Poison Buildup Effect";
        private const string k_DegradeBleedBuildupEffectResourcePath =
            "Effects/Degrade Bleed Buildup Effect";
        private const string k_PoisonedEffectResourcePath =
            "Effects/Poisoned Effect";
        private const string k_PoisonedVFXResourcePath =
            "Effects/Poisoned VFX";
        private const string k_DeadSpotResourcePath = "Effects/Dead Spot";

        private static WorldCharacterEffectsManager s_instance;

        [SerializeField] private List<InstantCharacterEffect> m_instantEffects = new();
        [SerializeField] private List<TimedCharacterEffect> m_timedEffects = new();
        [SerializeField] private TakeDamageEffect m_takeDamageEffect;
        [SerializeField] private TakeBlockedDamageEffect m_takeBlockedDamageEffect;
        [SerializeField] private TakeCriticalDamageEffect m_takeCriticalDamageEffect;
        [SerializeField] private TakeBuildupEffect m_takePoisonBuildupEffect;
        [SerializeField] private TakeBuildupEffect m_takeBleedBuildupEffect;
        [SerializeField] private BuildupEffect m_degradePoisonBuildupEffect;
        [SerializeField] private BuildupEffect m_degradeBleedBuildupEffect;
        [SerializeField] private PoisonedEffect m_poisonedEffect;

        [Header("Quick Slot Effects")]
        [SerializeField] private GameObject m_healingFlaskVFX;
        [SerializeField] private GameObject m_focusFlaskVFX;
        [SerializeField] private GameObject m_deadSpotVFX;
        [SerializeField] private GameObject m_poisonedVFX;

        public static WorldCharacterEffectsManager Instance => s_instance;
        public TakeDamageEffect TakeDamageEffect => m_takeDamageEffect;
        public TakeBlockedDamageEffect TakeBlockedDamageEffect =>
            m_takeBlockedDamageEffect ??= Resources.Load<TakeBlockedDamageEffect>(
                k_BlockedDamageEffectResourcePath);
        public TakeCriticalDamageEffect TakeCriticalDamageEffect =>
            m_takeCriticalDamageEffect ??= Resources.Load<TakeCriticalDamageEffect>(
                k_CriticalDamageEffectResourcePath);
        public TakeBuildupEffect TakePoisonBuildupEffect =>
            m_takePoisonBuildupEffect ??= Resources.Load<TakeBuildupEffect>(
                k_TakePoisonBuildupEffectResourcePath);
        public TakeBuildupEffect TakeBleedBuildupEffect =>
            m_takeBleedBuildupEffect ??= Resources.Load<TakeBuildupEffect>(
                k_TakeBleedBuildupEffectResourcePath);
        public BuildupEffect DegradePoisonBuildupEffect =>
            m_degradePoisonBuildupEffect ??= Resources.Load<BuildupEffect>(
                k_DegradePoisonBuildupEffectResourcePath);
        public BuildupEffect DegradeBleedBuildupEffect =>
            m_degradeBleedBuildupEffect ??= Resources.Load<BuildupEffect>(
                k_DegradeBleedBuildupEffectResourcePath);
        /// <summary>Gets the timed Health-drain template applied at Poison capacity.</summary>
        public PoisonedEffect PoisonedEffect =>
            m_poisonedEffect ??= Resources.Load<PoisonedEffect>(
                k_PoisonedEffectResourcePath);
        public GameObject HealingFlaskVFX => m_healingFlaskVFX;
        public GameObject FocusFlaskVFX => m_focusFlaskVFX != null
            ? m_focusFlaskVFX
            : m_healingFlaskVFX;
        /// <summary>Gets the networked Rune recovery-point presentation.</summary>
        public GameObject DeadSpotVFX => m_deadSpotVFX ??=
            Resources.Load<GameObject>(k_DeadSpotResourcePath);
        /// <summary>Gets the replicated local presentation used by poisoned characters.</summary>
        public GameObject PoisonedVFX => m_poisonedVFX ??=
            Resources.Load<GameObject>(k_PoisonedVFXResourcePath);

        /// <summary>
        /// Gets the authored instant effects in their stable identifier order.
        /// </summary>
        public IReadOnlyList<InstantCharacterEffect> InstantEffects => m_instantEffects;

        /// <summary>Gets authored timed effects in their stable identifier order.</summary>
        public IReadOnlyList<TimedCharacterEffect> TimedEffects => m_timedEffects;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            RegisterResourceEffects();
            AssignEffectIDs();
            DontDestroyOnLoad(gameObject);
        }

        private void OnValidate()
        {
            AssignEffectIDs();
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

        /// <summary>Finds a timed effect by its catalog identifier.</summary>
        public bool TryGetTimedEffect(
            int timedEffectID,
            out TimedCharacterEffect timedEffect)
        {
            if (timedEffectID < 0 || timedEffectID >= m_timedEffects.Count)
            {
                timedEffect = null;
                return false;
            }

            timedEffect = m_timedEffects[timedEffectID];
            return timedEffect != null &&
                timedEffect.TimedEffectID == timedEffectID;
        }

        private void AssignEffectIDs()
        {
            for (int effectIndex = 0; effectIndex < m_instantEffects.Count; effectIndex++)
            {
                m_instantEffects[effectIndex]?.AssignInstantEffectId(effectIndex);
            }

            for (int effectIndex = 0; effectIndex < m_timedEffects.Count; effectIndex++)
            {
                m_timedEffects[effectIndex]?.AssignTimedEffectID(effectIndex);
            }
        }

        private void RegisterResourceEffects()
        {
            m_takePoisonBuildupEffect ??= Resources.Load<TakeBuildupEffect>(
                k_TakePoisonBuildupEffectResourcePath);
            m_takeBleedBuildupEffect ??= Resources.Load<TakeBuildupEffect>(
                k_TakeBleedBuildupEffectResourcePath);
            m_degradePoisonBuildupEffect ??= Resources.Load<BuildupEffect>(
                k_DegradePoisonBuildupEffectResourcePath);
            m_degradeBleedBuildupEffect ??= Resources.Load<BuildupEffect>(
                k_DegradeBleedBuildupEffectResourcePath);
            m_poisonedEffect ??= Resources.Load<PoisonedEffect>(
                k_PoisonedEffectResourcePath);
            RegisterEffect(m_instantEffects, m_takePoisonBuildupEffect);
            RegisterEffect(m_instantEffects, m_takeBleedBuildupEffect);
            RegisterEffect(m_timedEffects, m_degradePoisonBuildupEffect);
            RegisterEffect(m_timedEffects, m_degradeBleedBuildupEffect);
            RegisterEffect(m_timedEffects, m_poisonedEffect);
        }

        private static void RegisterEffect<T>(List<T> effects, T effect)
            where T : Object
        {
            if (effect != null && !effects.Contains(effect))
            {
                effects.Add(effect);
            }
        }
    }
}
