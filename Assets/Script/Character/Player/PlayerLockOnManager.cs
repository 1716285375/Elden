using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    public class PlayerLockOnManager : MonoBehaviour
    {
        private const int k_MaximumDetectedColliders = 32;
        private const float k_SwitchInputThreshold = 0.75f;
        private const float k_SwitchInputResetThreshold = 0.35f;

        [Header("Target Detection")]
        [SerializeField, Min(0f)] private float m_lockOnRadius = 20f;
        [SerializeField] private LayerMask m_characterLayers = 1 << 8;
        [SerializeField] private LayerMask m_obstructionLayers = 1;
        [SerializeField, Min(0f)] private float m_targetHeightOffset = 1.5f;
        [SerializeField, Min(0f)] private float m_occlusionGracePeriod = 0.5f;

        private readonly Collider[] m_detectedColliders =
            new Collider[k_MaximumDetectedColliders];
        private readonly List<CharacterManager> m_possibleTargets = new();

        private PlayerManager m_player;
        private CharacterManager m_currentTarget;
        private float m_occludedDuration;
        private bool m_isSwitchInputReady = true;

        /// <summary>Gets the character currently targeted by the local player.</summary>
        public CharacterManager CurrentTarget => m_currentTarget;

        /// <summary>Gets whether a valid lock-on target is currently selected.</summary>
        public bool IsLockedOn => m_currentTarget != null;

        /// <summary>Gets the world-space point that cameras should frame.</summary>
        public Vector3 TargetAimPoint => m_currentTarget != null
            ? m_currentTarget.transform.position + Vector3.up * m_targetHeightOffset
            : transform.position;

        private void Awake()
        {
            m_player = GetComponent<PlayerManager>();
        }

        private void Update()
        {
            if (m_player == null || !m_player.IsOwner || !IsLockedOn)
            {
                return;
            }

            if (m_player.IsDead)
            {
                ClearLockOn();
                return;
            }

            if (!IsValidTarget(m_currentTarget) || !IsTargetWithinRange(m_currentTarget))
            {
                ClearLockOn();
                return;
            }

            if (HasLineOfSight(m_currentTarget))
            {
                m_occludedDuration = 0f;
                return;
            }

            m_occludedDuration += Time.deltaTime;
            if (m_occludedDuration >= m_occlusionGracePeriod)
            {
                ClearLockOn();
            }
        }

        private void OnDisable()
        {
            ClearLockOn();
        }

        /// <summary>
        /// Locks the nearest visible character, or clears the current lock when already locked.
        /// </summary>
        public void HandleLockOn()
        {
            if (m_player == null || !m_player.IsOwner || m_player.IsDead)
            {
                return;
            }

            if (IsLockedOn)
            {
                ClearLockOn();
                return;
            }

            FindPossibleTargets();
            SetTarget(PlayerLockOnTargetSelector.SelectClosestTarget(
                m_possibleTargets,
                transform.position));
        }

        /// <summary>
        /// Consumes horizontal camera input once per stick deflection to switch targets.
        /// </summary>
        public void HandleTargetSwitchInput(float horizontalInput)
        {
            float inputMagnitude = Mathf.Abs(horizontalInput);
            if (inputMagnitude <= k_SwitchInputResetThreshold)
            {
                m_isSwitchInputReady = true;
                return;
            }

            if (!IsLockedOn ||
                !m_isSwitchInputReady ||
                inputMagnitude < k_SwitchInputThreshold)
            {
                return;
            }

            m_isSwitchInputReady = false;
            FindPossibleTargets();
            Transform directionReference = PlayerCamera.Instance != null
                ? PlayerCamera.Instance.transform
                : transform;
            CharacterManager nextTarget =
                PlayerLockOnTargetSelector.SelectDirectionalTarget(
                    m_possibleTargets,
                    m_currentTarget,
                    directionReference,
                    transform.position,
                    Mathf.Sign(horizontalInput));
            if (nextTarget != null)
            {
                SetTarget(nextTarget);
            }
        }

        /// <summary>Clears local target state and restores free camera control.</summary>
        public void ClearLockOn()
        {
            m_currentTarget = null;
            m_occludedDuration = 0f;
        }

        private void FindPossibleTargets()
        {
            m_possibleTargets.Clear();
            int colliderCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                m_lockOnRadius,
                m_detectedColliders,
                m_characterLayers,
                QueryTriggerInteraction.Collide);
            for (int colliderIndex = 0;
                colliderIndex < colliderCount;
                colliderIndex++)
            {
                Collider detectedCollider = m_detectedColliders[colliderIndex];
                CharacterManager candidate = detectedCollider != null
                    ? detectedCollider.GetComponentInParent<CharacterManager>()
                    : null;
                if (!IsValidTarget(candidate) ||
                    m_possibleTargets.Contains(candidate) ||
                    !HasLineOfSight(candidate))
                {
                    continue;
                }

                m_possibleTargets.Add(candidate);
            }
        }

        private bool IsValidTarget(CharacterManager candidate)
        {
            return candidate != null &&
                candidate != m_player &&
                candidate.NetworkObject != null &&
                candidate.IsSpawned &&
                !candidate.IsDead;
        }

        private bool IsTargetWithinRange(CharacterManager target)
        {
            Vector3 offset = target.transform.position - transform.position;
            return offset.sqrMagnitude <= m_lockOnRadius * m_lockOnRadius;
        }

        private bool HasLineOfSight(CharacterManager target)
        {
            PlayerCamera playerCamera = PlayerCamera.Instance;
            Vector3 rayOrigin = playerCamera != null && playerCamera.CameraObject != null
                ? playerCamera.CameraObject.transform.position
                : transform.position + Vector3.up * m_targetHeightOffset;
            Vector3 targetPoint = target.transform.position +
                Vector3.up * m_targetHeightOffset;
            Vector3 targetDirection = targetPoint - rayOrigin;
            float targetDistance = targetDirection.magnitude;
            return targetDistance <= Mathf.Epsilon ||
                !Physics.Raycast(
                    rayOrigin,
                    targetDirection / targetDistance,
                    targetDistance,
                    m_obstructionLayers,
                    QueryTriggerInteraction.Ignore);
        }

        private void SetTarget(CharacterManager target)
        {
            m_currentTarget = target;
            m_occludedDuration = 0f;
        }
    }
}
