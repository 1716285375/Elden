using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterManager))]
    [RequireComponent(typeof(CharacterNetworkManager))]
    public class CharacterStatsManager : NetworkBehaviour
    {
        private const float k_StaminaPerEnduranceLevel = 10f;

        [Header("Stamina Regeneration")]
        [SerializeField, Min(0f)] private float m_staminaRegenerationDelay = 2f;
        [SerializeField, Min(0.01f)] private float m_staminaRegenerationTickInterval = 0.1f;
        [SerializeField, Min(0f)] private float m_staminaRegenerationAmount = 2f;

        private CharacterManager m_characterManager;
        private CharacterNetworkManager m_characterNetworkManager;
        private float m_staminaRegenerationTimer;
        private float m_staminaTickTimer;

        protected CharacterManager CharacterManager => m_characterManager;
        protected CharacterNetworkManager CharacterNetworkManager => m_characterNetworkManager;

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

            if (IsOwner)
            {
                InitializeStamina();
            }
        }

        public override void OnNetworkDespawn()
        {
            m_characterNetworkManager.CurrentStamina.OnValueChanged -= OnCurrentStaminaChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Calculates the current stamina cap from the endurance attribute.
        /// </summary>
        public float CalculateStaminaBasedOnEnduranceLevel(int enduranceLevel)
        {
            return Mathf.Max(0, enduranceLevel) * k_StaminaPerEnduranceLevel;
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

        private void InitializeStamina()
        {
            float maximumStamina = CalculateStaminaBasedOnEnduranceLevel(
                m_characterNetworkManager.Endurance.Value);
            m_characterNetworkManager.MaxStamina.Value = maximumStamina;
            m_characterNetworkManager.CurrentStamina.Value = maximumStamina;
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
    }
}
