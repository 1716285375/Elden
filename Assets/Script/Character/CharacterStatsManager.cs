using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterManager))]
    [RequireComponent(typeof(CharacterNetworkManager))]
    public class CharacterStatsManager : NetworkBehaviour
    {
        private const float k_HealthPerVitalityLevel = 15f;
        private const float k_StaminaPerEnduranceLevel = 10f;
        private const float k_FocusPointsPerMindLevel = 10f;
        private const float k_BuildupCapacityPerVitalityLevel = 3.25f;
        private const float k_PoiseTimerEpsilon = 0.0001f;

        [Header("Stamina Regeneration")]
        [SerializeField, Min(0f)] private float m_staminaRegenerationDelay = 2f;
        [SerializeField, Min(0.01f)] private float m_staminaRegenerationTickInterval = 0.1f;
        [SerializeField, Min(0f)] private float m_staminaRegenerationAmount = 2f;

        [Header("Poise")]
        [SerializeField] private float m_totalPoiseDamage;
        [SerializeField, Min(0f)] private float m_basePoiseDefense = 50f;
        [SerializeField, Min(0f)] private float m_armorPoiseDefense;
        [SerializeField, Min(0f)] private float m_offensivePoiseBonus;
        [SerializeField, Min(0f)] private float m_defaultPoiseResetTime = 8f;
        [SerializeField, Min(0f)] private float m_poiseResetTimer;

        [Header("Attributes")]
        [SerializeField, Min(0)] private int m_strengthLevel = 10;
        [SerializeField] private int m_strengthModifier;

        [Header("Rewards")]
        [SerializeField, Min(0)] private int m_runesDroppedOnDeath;

        [Header("Blocking Absorption")]
        [SerializeField, Range(0f, 100f)] private float m_blockingPhysicalAbsorption = 85f;
        [SerializeField, Range(0f, 100f)] private float m_blockingMagicAbsorption = 40f;
        [SerializeField, Range(0f, 100f)] private float m_blockingFireAbsorption = 35f;
        [SerializeField, Range(0f, 100f)] private float m_blockingLightningAbsorption = 25f;
        [SerializeField, Range(0f, 100f)] private float m_blockingHolyAbsorption = 35f;
        [SerializeField, Range(0f, 100f)] private float m_blockingStability = 50f;

        [Header("Armor Absorption")]
        [SerializeField, Range(0f, 100f)] private float m_armorPhysicalAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_armorMagicAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_armorFireAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_armorLightningAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_armorHolyAbsorption;

        [Header("Armor Resistance")]
        [SerializeField, Min(0f)] private float m_armorImmunity;
        [SerializeField, Min(0f)] private float m_armorRobustness;
        [SerializeField, Min(0f)] private float m_armorFocus;
        [SerializeField, Min(0f)] private float m_armorVitality;

        private CharacterManager m_characterManager;
        private CharacterNetworkManager m_characterNetworkManager;
        private float m_staminaRegenerationTimer;
        private float m_staminaTickTimer;

        protected CharacterManager CharacterManager => m_characterManager;
        protected CharacterNetworkManager CharacterNetworkManager => m_characterNetworkManager;

        /// <summary>Gets Physical damage absorption while a valid block is active.</summary>
        public float BlockingPhysicalAbsorption =>
            Mathf.Clamp(m_blockingPhysicalAbsorption, 0f, 100f);

        /// <summary>Gets Magic damage absorption while a valid block is active.</summary>
        public float BlockingMagicAbsorption =>
            Mathf.Clamp(m_blockingMagicAbsorption, 0f, 100f);

        /// <summary>Gets Fire damage absorption while a valid block is active.</summary>
        public float BlockingFireAbsorption =>
            Mathf.Clamp(m_blockingFireAbsorption, 0f, 100f);

        /// <summary>Gets Lightning damage absorption while a valid block is active.</summary>
        public float BlockingLightningAbsorption =>
            Mathf.Clamp(m_blockingLightningAbsorption, 0f, 100f);

        /// <summary>Gets Holy damage absorption while a valid block is active.</summary>
        public float BlockingHolyAbsorption =>
            Mathf.Clamp(m_blockingHolyAbsorption, 0f, 100f);

        /// <summary>Gets the percentage of incoming guard stamina damage prevented.</summary>
        public float BlockingStability => Mathf.Clamp(m_blockingStability, 0f, 100f);

        /// <summary>Gets the accumulated negative Poise modifier from recent hits.</summary>
        public float TotalPoiseDamage => Mathf.Min(0f, m_totalPoiseDamage);

        /// <summary>Gets the passive Poise defense supplied by the character and future armor.</summary>
        public float BasePoiseDefense =>
            Mathf.Max(0f, m_basePoiseDefense) + Mathf.Max(0f, m_armorPoiseDefense);

        /// <summary>Gets passive Poise contributed only by equipped armor.</summary>
        public float ArmorPoiseDefense => Mathf.Max(0f, m_armorPoiseDefense);

        /// <summary>Gets Physical absorption contributed only by equipped armor.</summary>
        public float ArmorPhysicalAbsorption => ClampArmorAbsorption(
            m_armorPhysicalAbsorption);

        /// <summary>Gets Magic absorption contributed only by equipped armor.</summary>
        public float ArmorMagicAbsorption => ClampArmorAbsorption(m_armorMagicAbsorption);

        /// <summary>Gets Fire absorption contributed only by equipped armor.</summary>
        public float ArmorFireAbsorption => ClampArmorAbsorption(m_armorFireAbsorption);

        /// <summary>Gets Lightning absorption contributed only by equipped armor.</summary>
        public float ArmorLightningAbsorption => ClampArmorAbsorption(
            m_armorLightningAbsorption);

        /// <summary>Gets Holy absorption contributed only by equipped armor.</summary>
        public float ArmorHolyAbsorption => ClampArmorAbsorption(m_armorHolyAbsorption);

        /// <summary>Gets Immunity contributed only by equipped armor.</summary>
        public float ArmorImmunity => Mathf.Max(0f, m_armorImmunity);

        /// <summary>Gets Robustness contributed only by equipped armor.</summary>
        public float ArmorRobustness => Mathf.Max(0f, m_armorRobustness);

        /// <summary>Gets Focus contributed only by equipped armor.</summary>
        public float ArmorFocus => Mathf.Max(0f, m_armorFocus);

        /// <summary>Gets Vitality contributed only by equipped armor.</summary>
        public float ArmorVitality => Mathf.Max(0f, m_armorVitality);

        /// <summary>Gets the temporary Poise bonus reserved for offensive actions.</summary>
        public float OffensivePoiseBonus => Mathf.Max(0f, m_offensivePoiseBonus);

        /// <summary>Gets the Poise remaining after defense, bonuses, and recent damage.</summary>
        public float RemainingPoise => CalculateRemainingPoise(
            BasePoiseDefense,
            OffensivePoiseBonus,
            TotalPoiseDamage);

        /// <summary>Gets the time remaining before accumulated Poise damage clears.</summary>
        public float PoiseResetTimer => Mathf.Max(0f, m_poiseResetTimer);

        /// <summary>Gets the persistent base Strength level.</summary>
        public int StrengthLevel => Mathf.Max(
            0,
            m_characterNetworkManager != null
                ? m_characterNetworkManager.Strength.Value
                : m_strengthLevel);

        /// <summary>Gets the additive Strength supplied by persistent effects.</summary>
        public int StrengthModifier => m_strengthModifier;

        /// <summary>Gets base Strength plus every active additive modifier.</summary>
        public int TotalStrength => Mathf.Max(0, StrengthLevel + m_strengthModifier);

        /// <summary>Gets the base Rune reward granted when this character dies.</summary>
        public int RunesDroppedOnDeath => Mathf.Max(0, m_runesDroppedOnDeath);

        protected virtual void Awake()
        {
            m_characterManager = GetComponent<CharacterManager>();
            m_characterNetworkManager = GetComponent<CharacterNetworkManager>();
        }

        private void Update()
        {
            HandlePoiseResetTimer();
            RegenerateStamina();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_characterNetworkManager.CurrentStamina.OnValueChanged += OnCurrentStaminaChanged;
            m_characterNetworkManager.Vitality.OnValueChanged += OnVitalityChanged;
            m_characterNetworkManager.Endurance.OnValueChanged += OnEnduranceChanged;
            m_characterNetworkManager.Mind.OnValueChanged += OnMindChanged;

            if (IsOwner)
            {
                InitializeResources();
            }

            ResetPoise();
        }

        public override void OnNetworkDespawn()
        {
            m_characterNetworkManager.CurrentStamina.OnValueChanged -= OnCurrentStaminaChanged;
            m_characterNetworkManager.Vitality.OnValueChanged -= OnVitalityChanged;
            m_characterNetworkManager.Endurance.OnValueChanged -= OnEnduranceChanged;
            m_characterNetworkManager.Mind.OnValueChanged -= OnMindChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Calculates the current health cap from the vitality attribute.
        /// </summary>
        public float CalculateHealthBasedOnVitalityLevel(int vitalityLevel)
        {
            return Mathf.Max(0, vitalityLevel) * k_HealthPerVitalityLevel;
        }

        /// <summary>
        /// Calculates the current stamina cap from the endurance attribute.
        /// </summary>
        public float CalculateStaminaBasedOnEnduranceLevel(int enduranceLevel)
        {
            return Mathf.Max(0, enduranceLevel) * k_StaminaPerEnduranceLevel;
        }

        /// <summary>Calculates the current Focus Point cap from the Mind attribute.</summary>
        public static float CalculateFocusPointsBasedOnMindLevel(int mindLevel)
        {
            return Mathf.Max(0, mindLevel) * k_FocusPointsPerMindLevel;
        }

        /// <summary>Calculates the shared status buildup cap from Vitality.</summary>
        public static float CalculateBuildupCapacityBasedOnVitalityLevel(
            int vitalityLevel)
        {
            return Mathf.Max(0, vitalityLevel) *
                k_BuildupCapacityPerVitalityLevel;
        }

        /// <summary>
        /// Recalculates maximum Health and fills the owner resource to that maximum.
        /// </summary>
        public void SetNewMaxHealthValue()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            float maximumHealth = CalculateHealthBasedOnVitalityLevel(
                m_characterNetworkManager.Vitality.Value);
            m_characterNetworkManager.MaxHealth.Value = maximumHealth;
            m_characterNetworkManager.CurrentHealth.Value = maximumHealth;
        }

        /// <summary>Recalculates shared status capacity and clamps active accumulation.</summary>
        public void SetNewBuildupCapacityValue()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            float capacity = CalculateBuildupCapacityBasedOnVitalityLevel(
                m_characterNetworkManager.Vitality.Value);
            m_characterNetworkManager.BuildupCapacity.Value = capacity;
            m_characterNetworkManager.TrySetBuildup(
                Buildup.Poison,
                m_characterNetworkManager.PoisonBuildup.Value);
            m_characterNetworkManager.TrySetBuildup(
                Buildup.Bleed,
                m_characterNetworkManager.BleedBuildup.Value);
        }

        /// <summary>Applies one decay tick and returns the replicated buildup remaining.</summary>
        public float DegradeBuildup(BuildupEffect buildupEffect)
        {
            if (buildupEffect == null ||
                !IsSpawned ||
                !IsOwner ||
                m_characterNetworkManager == null)
            {
                return 0f;
            }

            Buildup buildupType = buildupEffect.BuildupType;
            float currentBuildup = m_characterNetworkManager.GetBuildup(
                buildupType);
            m_characterNetworkManager.TrySetBuildup(
                buildupType,
                currentBuildup + buildupEffect.BuildupAmountDegradation);
            return m_characterNetworkManager.GetBuildup(buildupType);
        }

        /// <summary>
        /// Recalculates maximum Stamina and fills the owner resource to that maximum.
        /// </summary>
        public void SetNewMaxStaminaValue()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            float maximumStamina = CalculateStaminaBasedOnEnduranceLevel(
                m_characterNetworkManager.Endurance.Value);
            m_characterNetworkManager.MaxStamina.Value = maximumStamina;
            m_characterNetworkManager.CurrentStamina.Value = maximumStamina;
        }

        /// <summary>
        /// Recalculates maximum Focus Points and fills the owner resource to that maximum.
        /// </summary>
        public void SetNewMaxFocusPointsValue()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            float maximumFocusPoints = CalculateFocusPointsBasedOnMindLevel(
                m_characterNetworkManager.Mind.Value);
            m_characterNetworkManager.MaxFocusPoints.Value = maximumFocusPoints;
            m_characterNetworkManager.CurrentFocusPoints.Value = maximumFocusPoints;
        }

        /// <summary>
        /// Consumes stamina for an owner-authoritative action while preserving resource bounds.
        /// </summary>
        public bool TryConsumeStamina(float staminaCost)
        {
            if (!IsSpawned || !IsOwner || staminaCost <= 0f)
            {
                return false;
            }

            float maximumStamina = Mathf.Max(0f, m_characterNetworkManager.MaxStamina.Value);
            float currentStamina = Mathf.Clamp(
                m_characterNetworkManager.CurrentStamina.Value,
                0f,
                maximumStamina);
            if (currentStamina <= 0f)
            {
                return false;
            }

            m_characterNetworkManager.CurrentStamina.Value = CalculateStaminaAfterConsumption(
                currentStamina,
                maximumStamina,
                staminaCost);
            return true;
        }

        /// <summary>Consumes owner Focus Points and clamps the resource to its valid range.</summary>
        public bool TryConsumeFocusPoints(float focusPointsCost)
        {
            if (!IsSpawned || !IsOwner || focusPointsCost <= 0f)
            {
                return false;
            }

            float maximumFocusPoints = Mathf.Max(
                0f,
                m_characterNetworkManager.MaxFocusPoints.Value);
            float currentFocusPoints = Mathf.Clamp(
                m_characterNetworkManager.CurrentFocusPoints.Value,
                0f,
                maximumFocusPoints);
            if (currentFocusPoints <= 0f)
            {
                return false;
            }

            m_characterNetworkManager.CurrentFocusPoints.Value = Mathf.Clamp(
                currentFocusPoints - focusPointsCost,
                0f,
                maximumFocusPoints);
            return true;
        }

        /// <summary>Accumulates an incoming hit and returns whether it breaks current Poise.</summary>
        public bool ApplyPoiseDamage(float poiseDamage)
        {
            float resolvedPoiseDamage = Mathf.Max(0f, poiseDamage);
            if (resolvedPoiseDamage <= 0f)
            {
                return false;
            }

            m_totalPoiseDamage = Mathf.Min(
                0f,
                m_totalPoiseDamage - resolvedPoiseDamage);
            m_poiseResetTimer = Mathf.Max(0f, m_defaultPoiseResetTime);
            return IsPoiseBroken(RemainingPoise);
        }

        /// <summary>Clears accumulated Poise damage after its recovery delay expires.</summary>
        public void HandlePoiseResetTimer()
        {
            AdvancePoiseResetTimer(Time.deltaTime);
        }

        private void AdvancePoiseResetTimer(float deltaTime)
        {
            if (m_poiseResetTimer <= 0f)
            {
                return;
            }

            m_poiseResetTimer = Mathf.Max(
                0f,
                m_poiseResetTimer - Mathf.Max(0f, deltaTime));
            if (m_poiseResetTimer <= k_PoiseTimerEpsilon)
            {
                m_poiseResetTimer = 0f;
                m_totalPoiseDamage = 0f;
            }
        }

        /// <summary>Restores full Poise and clears its recovery countdown.</summary>
        public void ResetPoise()
        {
            m_totalPoiseDamage = 0f;
            m_poiseResetTimer = 0f;
        }

        /// <summary>Updates the passive Poise contribution used by equipment aggregation.</summary>
        public void SetBasePoiseDefense(float basePoiseDefense)
        {
            m_basePoiseDefense = Mathf.Max(0f, basePoiseDefense);
        }

        /// <summary>Rebuilds armor-only defense values from the currently equipped slots.</summary>
        public void CalculateTotalArmorValues(params ArmorItem[] armorItems)
        {
            ResetArmorValues();
            if (armorItems == null)
            {
                return;
            }

            foreach (ArmorItem armorItem in armorItems)
            {
                if (armorItem == null)
                {
                    continue;
                }

                m_armorPhysicalAbsorption += armorItem.PhysicalAbsorption;
                m_armorMagicAbsorption += armorItem.MagicAbsorption;
                m_armorFireAbsorption += armorItem.FireAbsorption;
                m_armorLightningAbsorption += armorItem.LightningAbsorption;
                m_armorHolyAbsorption += armorItem.HolyAbsorption;
                m_armorImmunity += armorItem.Immunity;
                m_armorRobustness += armorItem.Robustness;
                m_armorFocus += armorItem.Focus;
                m_armorVitality += armorItem.Vitality;
                m_armorPoiseDefense += armorItem.Poise;
            }

            m_armorPhysicalAbsorption = ClampArmorAbsorption(
                m_armorPhysicalAbsorption);
            m_armorMagicAbsorption = ClampArmorAbsorption(m_armorMagicAbsorption);
            m_armorFireAbsorption = ClampArmorAbsorption(m_armorFireAbsorption);
            m_armorLightningAbsorption = ClampArmorAbsorption(
                m_armorLightningAbsorption);
            m_armorHolyAbsorption = ClampArmorAbsorption(m_armorHolyAbsorption);
        }

        /// <summary>Clears armor contributions without modifying blocking or base Poise values.</summary>
        public void ResetArmorValues()
        {
            m_armorPhysicalAbsorption = 0f;
            m_armorMagicAbsorption = 0f;
            m_armorFireAbsorption = 0f;
            m_armorLightningAbsorption = 0f;
            m_armorHolyAbsorption = 0f;
            m_armorImmunity = 0f;
            m_armorRobustness = 0f;
            m_armorFocus = 0f;
            m_armorVitality = 0f;
            m_armorPoiseDefense = 0f;
        }

        /// <summary>Updates the temporary Poise bonus granted by an offensive action.</summary>
        public void SetOffensivePoiseBonus(float offensivePoiseBonus)
        {
            m_offensivePoiseBonus = Mathf.Max(0f, offensivePoiseBonus);
        }

        /// <summary>Adds or removes an independent Strength modifier.</summary>
        public void ModifyStrengthModifier(int modifierDelta)
        {
            m_strengthModifier += modifierDelta;
        }

        /// <summary>Calculates remaining Poise from defense, offense, and negative hit buildup.</summary>
        public static float CalculateRemainingPoise(
            float basePoiseDefense,
            float offensivePoiseBonus,
            float totalPoiseDamage)
        {
            return Mathf.Max(0f, basePoiseDefense) +
                Mathf.Max(0f, offensivePoiseBonus) +
                Mathf.Min(0f, totalPoiseDamage);
        }

        /// <summary>Returns whether the supplied remaining Poise has reached its break point.</summary>
        public static bool IsPoiseBroken(float remainingPoise)
        {
            return remainingPoise <= 0f;
        }

        /// <summary>Copies the equipped blocking weapon's defense data into character state.</summary>
        public void SetBlockingStats(WeaponItem blockingWeapon)
        {
            m_blockingPhysicalAbsorption = ClampBlockingValue(
                blockingWeapon?.BlockingPhysicalAbsorption ?? 0f);
            m_blockingMagicAbsorption = ClampBlockingValue(
                blockingWeapon?.BlockingMagicAbsorption ?? 0f);
            m_blockingFireAbsorption = ClampBlockingValue(
                blockingWeapon?.BlockingFireAbsorption ?? 0f);
            m_blockingLightningAbsorption = ClampBlockingValue(
                blockingWeapon?.BlockingLightningAbsorption ?? 0f);
            m_blockingHolyAbsorption = ClampBlockingValue(
                blockingWeapon?.BlockingHolyAbsorption ?? 0f);
            m_blockingStability = ClampBlockingValue(
                blockingWeapon?.BlockingStability ?? 0f);
        }

        /// <summary>
        /// Removes owner-authoritative guard stamina and checks Guard Break last.
        /// </summary>
        public bool ApplyBlockingStaminaDamage(
            float poiseDamage,
            float blockingStability)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            float staminaDamage = CalculateBlockingStaminaDamage(
                poiseDamage,
                blockingStability);
            if (staminaDamage > 0f)
            {
                TryConsumeStamina(staminaDamage);
            }

            return CheckForGuardBreak();
        }

        /// <summary>Calculates Poise-based guard damage after Stability mitigation.</summary>
        public static float CalculateBlockingStaminaDamage(
            float poiseDamage,
            float blockingStability)
        {
            return Mathf.Max(0f, poiseDamage) *
                (1f - ClampBlockingValue(blockingStability) / 100f);
        }

        /// <summary>Breaks an active guard after owner Stamina reaches zero.</summary>
        public bool CheckForGuardBreak()
        {
            if (!IsSpawned ||
                !IsOwner ||
                m_characterNetworkManager == null ||
                !m_characterNetworkManager.IsBlocking.Value ||
                m_characterNetworkManager.CurrentStamina.Value > 0f)
            {
                return false;
            }

            m_characterNetworkManager.SetBlockingState(false);
            m_characterManager?.CharacterAnimatorManager?.PlayTargetActionAnimation(
                CharacterActionAnimation.GuardBreak,
                true);
            m_characterNetworkManager.NotifyServerOfActionAnimationServerRpc(
                CharacterActionAnimation.GuardBreak,
                true,
                false,
                false,
                false);
            return true;
        }

        /// <summary>
        /// Regenerates owner stamina after its delay and according to the configured fixed tick.
        /// </summary>
        public void RegenerateStamina()
        {
            if (!IsSpawned || !IsOwner || IsStaminaRegenerationBlocked())
            {
                return;
            }

            float maximumStamina = Mathf.Max(0f, m_characterNetworkManager.MaxStamina.Value);
            float currentStamina = Mathf.Clamp(
                m_characterNetworkManager.CurrentStamina.Value,
                0f,
                maximumStamina);
            if (currentStamina >= maximumStamina)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            float delayTimeRemaining = Mathf.Max(
                0f,
                m_staminaRegenerationDelay - m_staminaRegenerationTimer);
            m_staminaRegenerationTimer += deltaTime;
            if (m_staminaRegenerationTimer < m_staminaRegenerationDelay)
            {
                return;
            }

            m_staminaTickTimer += Mathf.Max(0f, deltaTime - delayTimeRemaining);
            float tickInterval = Mathf.Max(0.01f, m_staminaRegenerationTickInterval);
            int elapsedTicks = CalculateElapsedStaminaRegenerationTicks(
                m_staminaTickTimer,
                tickInterval);
            if (elapsedTicks <= 0)
            {
                return;
            }

            m_staminaTickTimer -= elapsedTicks * tickInterval;
            m_characterNetworkManager.CurrentStamina.Value = Mathf.Clamp(
                currentStamina + Mathf.Max(0f, m_staminaRegenerationAmount) * elapsedTicks,
                0f,
                maximumStamina);
        }

        /// <summary>
        /// Restarts the stamina recovery delay after resource consumption.
        /// </summary>
        public void ResetStaminaRegenerationTimer()
        {
            m_staminaRegenerationTimer = 0f;
            m_staminaTickTimer = 0f;
        }

        protected virtual bool IsStaminaRegenerationBlocked()
        {
            return m_characterManager == null || m_characterManager.IsPerformingAction;
        }

        private void InitializeResources()
        {
            if (m_characterNetworkManager.MaxHealth.Value <= 0f)
            {
                SetNewMaxHealthValue();
            }

            if (m_characterNetworkManager.MaxStamina.Value <= 0f)
            {
                SetNewMaxStaminaValue();
            }

            if (m_characterNetworkManager.MaxFocusPoints.Value <= 0f)
            {
                SetNewMaxFocusPointsValue();
            }

            if (m_characterNetworkManager.BuildupCapacity.Value <= 0f)
            {
                SetNewBuildupCapacityValue();
            }
        }

        private void OnVitalityChanged(int previousVitality, int currentVitality)
        {
            if (IsOwner)
            {
                SetNewMaxHealthValue();
                SetNewBuildupCapacityValue();
            }
        }

        private void OnEnduranceChanged(int previousEndurance, int currentEndurance)
        {
            if (IsOwner)
            {
                SetNewMaxStaminaValue();
            }
        }

        private void OnMindChanged(int previousMind, int currentMind)
        {
            if (IsOwner)
            {
                SetNewMaxFocusPointsValue();
            }
        }

        private void OnCurrentStaminaChanged(float previousStamina, float currentStamina)
        {
            if (IsOwner && ShouldResetStaminaRegenerationTimer(previousStamina, currentStamina))
            {
                ResetStaminaRegenerationTimer();
            }
        }

        private static bool ShouldResetStaminaRegenerationTimer(
            float previousStamina,
            float currentStamina)
        {
            return currentStamina < previousStamina;
        }

        private static float CalculateStaminaAfterConsumption(
            float currentStamina,
            float maximumStamina,
            float staminaCost)
        {
            return Mathf.Clamp(
                currentStamina - Mathf.Max(0f, staminaCost),
                0f,
                Mathf.Max(0f, maximumStamina));
        }

        private static int CalculateElapsedStaminaRegenerationTicks(
            float staminaTickTimer,
            float staminaTickInterval)
        {
            return Mathf.Max(
                0,
                Mathf.FloorToInt(
                    Mathf.Max(0f, staminaTickTimer) /
                    Mathf.Max(0.01f, staminaTickInterval)));
        }

        private static float ClampBlockingValue(float value)
        {
            return Mathf.Clamp(value, 0f, 100f);
        }

        private static float ClampArmorAbsorption(float value)
        {
            return Mathf.Clamp(value, 0f, 100f);
        }
    }
}
