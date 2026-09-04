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
        [SerializeField] private GameObject m_poisonedVFX;
        [SerializeField] private GameObject m_frostbiteVFX;

        [SerializeField] private CharacterManager m_character;

        [Header("Static Effects")]
        [SerializeField] private List<StaticCharacterEffect> m_staticEffects = new();

        [Header("Timed Effects")]
        [SerializeField, Min(0.01f)] private float m_defaultEffectTickTime = 1f;
        [SerializeField] private List<TimedCharacterEffect> m_timedEffects = new();
        private readonly List<TimedCharacterEffect> m_timedEffectsSnapshot = new();

        private readonly List<GameObject> m_currentActionEffects = new();
        private GameObject m_activePoisonedVFX;
        private GameObject m_activeFrostbiteVFX;
        private float m_effectTickTimer;

        protected CharacterManager Character => m_character;

        /// <summary>Gets the transient timed effects currently owned by this character.</summary>
        public IReadOnlyList<TimedCharacterEffect> TimedEffects => m_timedEffects;

        protected virtual void Awake()
        {
            m_character ??= GetComponent<CharacterManager>();
            m_timedEffects ??= new List<TimedCharacterEffect>();
            m_effectTickTimer = Mathf.Max(0.01f, m_defaultEffectTickTime);
        }

        protected virtual void Update()
        {
            if (m_timedEffects.Count == 0 ||
                m_character != null && m_character.IsSpawned && !m_character.IsOwner)
            {
                return;
            }

            m_effectTickTimer -= Time.deltaTime;
            if (m_effectTickTimer > 0f)
            {
                return;
            }

            m_effectTickTimer = Mathf.Max(0.01f, m_defaultEffectTickTime);
            ProcessTimedEffects();
        }

        protected virtual void OnDestroy()
        {
            SetPoisonedVFX(false);
            SetFrostbiteVFX(false);
            DestroyAllCurrentActionEffects();
            RemoveAllTimedEffects();
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

        /// <summary>Spawns the shared Health or Focus flask feedback on this character.</summary>
        public void PlayFlaskRestorationVFX(bool restoresHealth)
        {
            WorldCharacterEffectsManager worldEffects =
                WorldCharacterEffectsManager.Instance;
            GameObject flaskVFX = restoresHealth
                ? worldEffects?.HealingFlaskVFX
                : worldEffects?.FocusFlaskVFX;
            if (flaskVFX == null || m_character == null)
            {
                return;
            }

            Transform anchor = m_character.LockOnTransform;
            GameObject instance = Instantiate(
                flaskVFX,
                anchor.position,
                Quaternion.identity,
                m_character.transform);
            Destroy(instance, 3f);
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

        /// <summary>Executes a transient damage payload in one explicit network phase.</summary>
        public void ProcessRuntimeDamageEffect(
            DamageEffect runtimeEffect,
            DamageProcessingMode processingMode)
        {
            if (runtimeEffect == null)
            {
                Debug.LogWarning("A runtime damage effect is required.", this);
                return;
            }

            if ((runtimeEffect.hideFlags & HideFlags.DontSave) !=
                HideFlags.DontSave)
            {
                Debug.LogWarning(
                    "ProcessRuntimeDamageEffect only accepts transient effects.",
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
                runtimeEffect.ProcessDamage(m_character, processingMode);
            }
            finally
            {
                DestroyRuntimeEffect(runtimeEffect);
            }
        }

        /// <summary>Processes authored status buildup payloads on the target owner.</summary>
        public void ProcessBuildupEffects(
            float poisonBuildup,
            float bleedBuildup,
            float frostBuildup)
        {
            WorldCharacterEffectsManager worldEffects =
                WorldCharacterEffectsManager.Instance;
            ProcessBuildupEffect(
                worldEffects?.TakePoisonBuildupEffect,
                poisonBuildup);
            ProcessBuildupEffect(
                worldEffects?.TakeBleedBuildupEffect,
                bleedBuildup);
            ProcessBuildupEffect(
                worldEffects?.TakeFrostBuildupEffect,
                frostBuildup);
        }

        /// <summary>Adds one owner-authoritative accumulation amount.</summary>
        public bool AddBuildup(Buildup buildupType, float buildupAmount)
        {
            if (m_character == null ||
                !m_character.IsSpawned ||
                !m_character.IsOwner ||
                buildupAmount <= 0f)
            {
                return false;
            }

            CharacterNetworkManager networkManager =
                m_character.CharacterNetworkManager;
            if (networkManager == null)
            {
                return false;
            }

            if (buildupType == Buildup.Poison &&
                networkManager.IsPoisoned.Value)
            {
                return false;
            }
            if (buildupType == Buildup.Frost &&
                networkManager.IsFrostbitten.Value)
            {
                return false;
            }

            float currentBuildup = networkManager.GetBuildup(buildupType);
            if (!networkManager.TrySetBuildup(
                    buildupType,
                    currentBuildup + buildupAmount))
            {
                return false;
            }

            float capacity = Mathf.Max(0f, networkManager.BuildupCapacity.Value);
            if (buildupType == Buildup.Poison &&
                capacity > 0f &&
                networkManager.GetBuildup(buildupType) >= capacity)
            {
                TriggerPoison(networkManager);
            }
            else if (buildupType == Buildup.Frost &&
                capacity > 0f &&
                networkManager.GetBuildup(buildupType) >= capacity)
            {
                TriggerFrostbite(networkManager);
            }

            return true;
        }

        /// <summary>Applies owner-authoritative status damage and shared death state.</summary>
        public bool ProcessEffectDamage(float effectDamage)
        {
            CharacterNetworkManager networkManager =
                m_character?.CharacterNetworkManager;
            if (m_character == null ||
                !m_character.IsSpawned ||
                !m_character.IsOwner ||
                m_character.IsDead ||
                networkManager == null ||
                effectDamage <= 0f)
            {
                return false;
            }

            float remainingHealth = Mathf.Max(
                0f,
                networkManager.CurrentHealth.Value - effectDamage);
            networkManager.CurrentHealth.Value = remainingHealth;
            if (remainingHealth <= 0f)
            {
                networkManager.IsDead.Value = true;
            }

            return true;
        }

        /// <summary>Applies one owner-authoritative Poison damage tick.</summary>
        public bool ProcessPoisonDamage(float poisonDamage)
        {
            CharacterNetworkManager networkManager =
                m_character?.CharacterNetworkManager;
            if (m_character == null ||
                !m_character.IsSpawned ||
                !m_character.IsOwner ||
                m_character.IsDead ||
                networkManager == null ||
                !networkManager.IsPoisoned.Value ||
                poisonDamage <= 0f)
            {
                return false;
            }

            return ProcessEffectDamage(poisonDamage);
        }

        /// <summary>Applies replicated Poison presentation and owner cleanup.</summary>
        public void SetPoisonedState(bool isPoisoned)
        {
            SetPoisonedVFX(isPoisoned);
            if (isPoisoned ||
                m_character == null ||
                !m_character.IsSpawned ||
                !m_character.IsOwner)
            {
                return;
            }

            PoisonedEffect poisonedEffect =
                WorldCharacterEffectsManager.Instance?.PoisonedEffect;
            if (poisonedEffect != null)
            {
                RemoveTimedEffect(poisonedEffect.TimedEffectID);
            }
        }

        /// <summary>Applies replicated Frostbite VFX and owner effect cleanup.</summary>
        public void SetFrostbittenState(bool isFrostbitten)
        {
            SetFrostbiteVFX(isFrostbitten);
            if (isFrostbitten ||
                m_character == null ||
                !m_character.IsSpawned ||
                !m_character.IsOwner)
            {
                return;
            }

            FrostbiteEffect frostbiteEffect =
                WorldCharacterEffectsManager.Instance?.FrostbiteEffect;
            if (frostbiteEffect != null)
            {
                RemoveTimedEffect(frostbiteEffect.TimedEffectID);
            }
        }

        /// <summary>Adds a runtime clone, or refreshes an active effect with the same ID.</summary>
        public TimedCharacterEffect AddTimedEffect(TimedCharacterEffect effect)
        {
            m_character ??= GetComponent<CharacterManager>();
            if (effect == null ||
                m_character == null ||
                m_character.IsSpawned && !m_character.IsOwner)
            {
                return null;
            }

            TimedCharacterEffect activeEffect =
                CheckForTimedEffect(effect.TimedEffectID);
            if (activeEffect != null)
            {
                activeEffect.RefreshDuration();
                return activeEffect;
            }

            bool wasEmpty = m_timedEffects.Count == 0;
            TimedCharacterEffect runtimeEffect = effect.CreateRuntimeInstance();
            m_timedEffects.Add(runtimeEffect);
            if (wasEmpty)
            {
                m_effectTickTimer = Mathf.Max(0.01f, m_defaultEffectTickTime);
            }

            runtimeEffect.ProcessEffect(m_character);
            return runtimeEffect;
        }

        /// <summary>Removes one active effect after allowing it to clean up its state.</summary>
        public bool RemoveTimedEffect(int timedEffectID)
        {
            TimedCharacterEffect activeEffect =
                CheckForTimedEffect(timedEffectID);
            if (activeEffect == null)
            {
                m_timedEffects.RemoveAll(effect => effect == null);
                return false;
            }

            m_timedEffects.Remove(activeEffect);
            activeEffect.RemoveEffect(m_character);
            DestroyTimedEffect(activeEffect);
            m_timedEffects.RemoveAll(effect => effect == null);
            return true;
        }

        /// <summary>Finds the runtime effect matching one stable identifier.</summary>
        public TimedCharacterEffect CheckForTimedEffect(int timedEffectID)
        {
            return m_timedEffects.Find(effect =>
                effect != null && effect.TimedEffectID == timedEffectID);
        }

        /// <summary>Processes one shared tick for every currently active timed effect.</summary>
        public void ProcessTimedEffects()
        {
            float tickTime = Mathf.Max(0.01f, m_defaultEffectTickTime);
            m_timedEffectsSnapshot.Clear();
            m_timedEffectsSnapshot.AddRange(m_timedEffects);
            foreach (TimedCharacterEffect runtimeEffect in m_timedEffectsSnapshot)
            {
                if (runtimeEffect == null || !m_timedEffects.Contains(runtimeEffect))
                {
                    continue;
                }

                runtimeEffect.ProcessEffect(m_character);
                if (!m_timedEffects.Contains(runtimeEffect))
                {
                    continue;
                }

                runtimeEffect.AdvanceTime(tickTime);
                if (runtimeEffect.TimeRemainingOnEffect <= 0f)
                {
                    RemoveTimedEffect(runtimeEffect.TimedEffectID);
                }
            }

            m_timedEffectsSnapshot.Clear();
            m_timedEffects.RemoveAll(effect => effect == null);
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

        private void ProcessBuildupEffect(
            TakeBuildupEffect effectTemplate,
            float buildupAmount)
        {
            if (effectTemplate == null || buildupAmount <= 0f)
            {
                return;
            }

            ProcessRuntimeInstantEffect(
                effectTemplate.CreateRuntimeBuildupEffect(buildupAmount));
        }

        private void TriggerPoison(CharacterNetworkManager networkManager)
        {
            PoisonedEffect poisonedEffect =
                WorldCharacterEffectsManager.Instance?.PoisonedEffect;
            if (poisonedEffect == null)
            {
                Debug.LogWarning(
                    "Poison buildup reached capacity without a Poisoned Effect asset.",
                    this);
                return;
            }

            networkManager.TrySetBuildup(Buildup.Poison, 0f);
            networkManager.TrySetPoisoned(true);
            AddTimedEffect(poisonedEffect);
        }

        private void TriggerFrostbite(CharacterNetworkManager networkManager)
        {
            FrostbiteEffect frostbiteEffect =
                WorldCharacterEffectsManager.Instance?.FrostbiteEffect;
            if (frostbiteEffect == null)
            {
                Debug.LogWarning(
                    "Frost buildup reached capacity without a Frostbite Effect asset.",
                    this);
                return;
            }

            networkManager.TrySetBuildup(Buildup.Frost, 0f);
            networkManager.TrySetFrostbitten(true);
            AddTimedEffect(frostbiteEffect);
        }

        private void SetPoisonedVFX(bool isPoisoned)
        {
            if (!isPoisoned)
            {
                DestroyEffectInstance(m_activePoisonedVFX);
                m_activePoisonedVFX = null;
                return;
            }

            if (m_activePoisonedVFX != null || m_character == null)
            {
                return;
            }

            GameObject poisonedVFX = m_poisonedVFX != null
                ? m_poisonedVFX
                : WorldCharacterEffectsManager.Instance?.PoisonedVFX;
            if (poisonedVFX == null)
            {
                return;
            }

            Transform anchor = m_character.LockOnTransform;
            m_activePoisonedVFX = Instantiate(
                poisonedVFX,
                anchor.position,
                anchor.rotation,
                anchor);
            m_activePoisonedVFX.transform.localPosition = Vector3.zero;
        }

        private void SetFrostbiteVFX(bool isFrostbitten)
        {
            if (!isFrostbitten)
            {
                DestroyEffectInstance(m_activeFrostbiteVFX);
                m_activeFrostbiteVFX = null;
                return;
            }

            if (m_activeFrostbiteVFX != null || m_character == null)
            {
                return;
            }

            GameObject frostbiteVFX = m_frostbiteVFX != null
                ? m_frostbiteVFX
                : WorldCharacterEffectsManager.Instance?.FrostbiteVFX;
            if (frostbiteVFX == null)
            {
                return;
            }

            Transform anchor = m_character.LockOnTransform;
            m_activeFrostbiteVFX = Instantiate(
                frostbiteVFX,
                anchor.position,
                anchor.rotation,
                anchor);
            m_activeFrostbiteVFX.transform.localPosition = Vector3.zero;
        }

        private void RemoveAllTimedEffects()
        {
            for (int effectIndex = m_timedEffects.Count - 1;
                effectIndex >= 0;
                effectIndex--)
            {
                TimedCharacterEffect runtimeEffect = m_timedEffects[effectIndex];
                m_timedEffects.RemoveAt(effectIndex);
                runtimeEffect?.RemoveEffect(m_character);
                DestroyTimedEffect(runtimeEffect);
            }

            m_timedEffects.Clear();
        }

        private static void DestroyEffectInstance(GameObject effectInstance)
        {
            if (effectInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(effectInstance);
            }
            else
            {
                DestroyImmediate(effectInstance);
            }
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

        private static void DestroyTimedEffect(TimedCharacterEffect runtimeEffect)
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
