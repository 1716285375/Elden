using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    public class PlayerLockOnManager : MonoBehaviour
    {
        private const int k_MaximumDetectedColliders = 128;
        private const float k_SwitchInputThreshold = 0.75f;
        private const float k_SwitchInputResetThreshold = 0.35f;

        [Header("Target Detection")]
        [SerializeField, Min(0f)] private float m_lockOnRadius = 20f;
        [SerializeField] private LayerMask m_characterLayers = (1 << 8) | (1 << 10);
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
            ? GetAimPoint(m_currentTarget)
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

        /// <summary>Projects the marker after the owning player's camera has completed its frame.</summary>
        public void PresentTarget(Camera camera)
        {
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }
            PlayerUIManager.Instance?.PlayerUIHUDManager?.Toolkit?.SetLockOnTarget(
                camera, IsLockedOn ? TargetAimPoint : (Vector3?)null);
        }

        /// <summary>
        /// Locks the visible hostile closest to the view direction, or clears the current lock.
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
            Transform view = GetViewReference();
            SetTarget(PlayerLockOnTargetSelector.SelectClosestTarget(
                m_possibleTargets,
                view.position,
                view));
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
            Transform directionReference = GetViewReference();
            CharacterManager nextTarget =
                PlayerLockOnTargetSelector.SelectDirectionalTarget(
                    m_possibleTargets,
                    m_currentTarget,
                    directionReference,
                    directionReference.position,
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
            if (m_player != null && m_player.IsOwner)
            {
                PlayerUIManager.Instance?.PlayerUIHUDManager?.Toolkit?.SetLockOnTarget(null, null);
            }
        }

        private void FindPossibleTargets()
        {
            m_possibleTargets.Clear();
            int colliderCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                m_lockOnRadius,
                m_detectedColliders,
                // Imported enemies and their hurtboxes use DamageableCharacter, including older prefab overrides.
                m_characterLayers | (1 << 10),
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
                    !IsInView(candidate) ||
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
                WorldUtilityManager.CanDamageCharacter(m_player, candidate);
        }

        private Transform GetViewReference()
        {
            Camera camera = PlayerCamera.Instance != null ? PlayerCamera.Instance.CameraObject : null;
            return camera != null ? camera.transform : transform;
        }

        private Vector3 GetAimPoint(CharacterManager target)
        {
            return target.LockOnTransform != target.transform ? target.LockOnTransform.position :
                target.transform.position + Vector3.up * m_targetHeightOffset;
        }

        private bool IsInView(CharacterManager target)
        {
            Camera camera = PlayerCamera.Instance != null ? PlayerCamera.Instance.CameraObject : null;
            if (camera == null)
            {
                return Vector3.Dot(transform.forward, target.transform.position - transform.position) > 0f;
            }
            Vector3 point = camera.WorldToViewportPoint(GetAimPoint(target));
            return point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
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
            Vector3 targetPoint = GetAimPoint(target);
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
