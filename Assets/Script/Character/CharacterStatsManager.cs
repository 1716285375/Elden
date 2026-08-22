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

        [Header("Stamina Regeneration")]
        [SerializeField, Min(0f)] private float m_staminaRegenerationDelay = 2f;
        [SerializeField, Min(0.01f)] private float m_staminaRegenerationTickInterval = 0.1f;
        [SerializeField, Min(0f)] private float m_staminaRegenerationAmount = 2f;

        [Header("Blocking Absorption")]
        [SerializeField, Range(0f, 100f)] private float m_blockingPhysicalAbsorption = 85f;
        [SerializeField, Range(0f, 100f)] private float m_blockingMagicAbsorption = 40f;
        [SerializeField, Range(0f, 100f)] private float m_blockingFireAbsorption = 35f;
        [SerializeField, Range(0f, 100f)] private float m_blockingLightningAbsorption = 25f;
        [SerializeField, Range(0f, 100f)] private float m_blockingHolyAbsorption = 35f;
        [SerializeField, Range(0f, 100f)] private float m_blockingStability = 50f;

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

        protected virtual void Awake()
        {
            m_characterManager = GetComponent<CharacterManager>();
            m_characterNetworkManager = GetComponent<CharacterNetworkManager>();
        }

        private void Update()
        {
            RegenerateStamina();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_characterNetworkManager.CurrentStamina.OnValueChanged += OnCurrentStaminaChanged;
            m_characterNetworkManager.Vitality.OnValueChanged += OnVitalityChanged;
            m_characterNetworkManager.Endurance.OnValueChanged += OnEnduranceChanged;

            if (IsOwner)
            {
                InitializeResources();
            }
        }

        public override void OnNetworkDespawn()
        {
            m_characterNetworkManager.CurrentStamina.OnValueChanged -= OnCurrentStaminaChanged;
            m_characterNetworkManager.Vitality.OnValueChanged -= OnVitalityChanged;
            m_characterNetworkManager.Endurance.OnValueChanged -= OnEnduranceChanged;
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
        }

        private void OnVitalityChanged(int previousVitality, int currentVitality)
        {
            if (IsOwner)
            {
                SetNewMaxHealthValue();
            }
        }

        private void OnEnduranceChanged(int previousEndurance, int currentEndurance)
        {
            if (IsOwner)
            {
                SetNewMaxStaminaValue();
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
    }
}
