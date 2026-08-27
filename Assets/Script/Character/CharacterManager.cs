using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterNetworkManager))]
    public class CharacterManager : NetworkBehaviour
    {
        [SerializeField] private Animator m_animator;
        [SerializeField] private CharacterAnimatorManager m_characterAnimatorManager;
        [SerializeField] private CharacterNetworkManager m_characterNetworkManager;

        [Header("Faction")]
        [SerializeField] private CharacterGroup m_characterGroup;

        [Header("Critical Attacks")]
        [SerializeField] private Transform m_lockOnTransform;

        [Header("Character UI")]
        [SerializeField] private bool m_hasFloatingHPBar = true;

        private CharacterEffectsManager m_characterEffectsManager;
        private CharacterSoundFXManager m_characterSoundFXManager;
        private CharacterStatsManager m_characterStatsManager;
        private CharacterCombatManager m_characterCombatManager;
        private CharacterLocomotionManager m_characterLocomotionManager;
        private CharacterUIManager m_characterUIManager;
        private bool m_isGrounded = true;
        private bool m_isPerformingAction;
        private bool m_canMove = true;
        private bool m_canRotate = true;
        private bool m_shouldApplyRootMotion;
        private bool m_isDeathEventRunning;
        private bool m_isInvulnerable;

        public CharacterAnimatorManager CharacterAnimatorManager => m_characterAnimatorManager;
        public CharacterEffectsManager CharacterEffectsManager => m_characterEffectsManager;
        /// <summary>Gets the spatial sound presenter attached to this character.</summary>
        public CharacterSoundFXManager CharacterSoundFXManager =>
            m_characterSoundFXManager;
        public CharacterNetworkManager CharacterNetworkManager => m_characterNetworkManager;
        public CharacterStatsManager CharacterStatsManager => m_characterStatsManager;
        public CharacterCombatManager CharacterCombatManager => m_characterCombatManager;
        /// <summary>Gets the faction used by damage and reward eligibility rules.</summary>
        public CharacterGroup CharacterGroup => m_characterGroup;
        /// <summary>Gets the character's cached movement-state controller.</summary>
        public CharacterLocomotionManager CharacterLocomotionManager =>
            m_characterLocomotionManager;
        /// <summary>Gets the character's cached world-space UI controller.</summary>
        public CharacterUIManager CharacterUIManager => m_characterUIManager;
        /// <summary>Gets the chest-height origin used for lock-on and critical queries.</summary>
        public Transform LockOnTransform => m_lockOnTransform != null
            ? m_lockOnTransform
            : transform;
        /// <summary>
        /// Gets whether the character's ground probe currently detects walkable environment.
        /// </summary>
        public bool IsGrounded => m_isGrounded;

        /// <summary>
        /// Gets the replicated deliberate-jump state owned by this character's network authority.
        /// </summary>
        public bool IsJumping => m_characterNetworkManager != null &&
            m_characterNetworkManager.IsJumping.Value;

        /// <summary>
        /// Gets the replicated death state owned by this character's network authority.
        /// </summary>
        public bool IsDead => m_characterNetworkManager != null &&
            m_characterNetworkManager.IsDead.Value;
        public bool IsPerformingAction => m_isPerformingAction;
        public bool CanMove => m_canMove;
        public bool CanRotate => m_canRotate;
        public bool ShouldApplyRootMotion => m_shouldApplyRootMotion;
        public bool IsInvulnerable => m_isInvulnerable;

        /// <summary>Gets whether this character type may present a world-space Health bar.</summary>
        public bool HasFloatingHPBar => m_hasFloatingHPBar;

        internal bool IsDeathEventRunning => m_isDeathEventRunning;

        protected virtual void Awake()
        {
            m_animator = GetComponent<Animator>();
            if (m_animator == null)
            {
                m_animator = GetComponentInChildren<Animator>(true);
            }

            m_characterAnimatorManager = GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterEffectsManager = GetComponent<CharacterEffectsManager>();
            m_characterSoundFXManager =
                GetComponentInChildren<CharacterSoundFXManager>(true);
            m_characterNetworkManager = GetComponent<CharacterNetworkManager>();
            m_characterStatsManager = GetComponent<CharacterStatsManager>();
            m_characterCombatManager = GetComponent<CharacterCombatManager>();
            m_characterLocomotionManager = GetComponent<CharacterLocomotionManager>();
            m_characterUIManager = GetComponentInChildren<CharacterUIManager>(true);
            m_lockOnTransform ??= FindCriticalAnchor();
            m_characterAnimatorManager?.Initialize(m_animator);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (this is PlayerManager)
            {
                m_characterUIManager?.BindNetworkHealth();
            }
        }

        public override void OnNetworkDespawn()
        {
            m_characterUIManager?.UnbindNetworkHealth();
            base.OnNetworkDespawn();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            m_characterUIManager?.RefreshVisibility();
        }

        public override void OnLostOwnership()
        {
            m_characterUIManager?.RefreshVisibility();
            base.OnLostOwnership();
        }

        /// <summary>
        /// Resolves self-collisions between this character's own body hitboxes.
        /// </summary>
        protected virtual void Start()
        {
            IgnoreMyOwnColliders();
        }

        /// <summary>
        /// Applies the movement restrictions and root-motion policy for the current character action.
        /// </summary>
        public void SetActionState(
            bool isPerformingAction,
            bool shouldApplyRootMotion,
            bool canRotate,
            bool canMove)
        {
            if (isPerformingAction && m_isPerformingAction)
            {
                m_characterEffectsManager?.DestroyAllCurrentActionEffects();
                if (this is PlayerManager player &&
                    player.PlayerCombatManager?.IsHoldingSpellInput == true)
                {
                    player.PlayerCombatManager.CancelChargingSpell();
                }

                if (this is PlayerManager rangedPlayer &&
                    rangedPlayer.PlayerCombatManager?.HasArrowNotched == true)
                {
                    rangedPlayer.PlayerCombatManager.CancelNotchedProjectile(false);
                }
            }

            if (isPerformingAction && this is PlayerManager itemPlayer &&
                itemPlayer.PlayerCombatManager?.IsUsingItem == true)
            {
                itemPlayer.PlayerCombatManager.CancelQuickSlotItemUse();
            }

            m_isPerformingAction = isPerformingAction;
            m_shouldApplyRootMotion = shouldApplyRootMotion;
            m_canRotate = canRotate;
            m_canMove = canMove;
            m_characterLocomotionManager?.SetCanRoll(!isPerformingAction);
        }

        /// <summary>
        /// Updates only the rotation permission without touching the other action flags.
        /// </summary>
        public void SetCanRotate(bool canRotate)
        {
            m_canRotate = canRotate;
        }

        /// <summary>Updates only the movement permission for an active action state.</summary>
        public void SetCanMove(bool canMove)
        {
            m_canMove = canMove;
        }

        /// <summary>Sets whether incoming instant damage should be ignored.</summary>
        public void SetInvulnerable(bool isInvulnerable)
        {
            m_isInvulnerable = isInvulnerable;
        }

        /// <summary>
        /// Restores the default action state after an action animation returns to Empty.
        /// </summary>
        public void ResetActionFlags()
        {
            m_characterEffectsManager?.DestroyAllCurrentActionEffects();
            m_isPerformingAction = false;
            m_canMove = true;
            m_canRotate = true;
            m_shouldApplyRootMotion = false;
            m_characterLocomotionManager?.SetCanRun(true);
            m_characterLocomotionManager?.SetCanRoll(true);
            m_characterCombatManager?.ResetActionState();
            m_characterNetworkManager?.SetRollingState(false);
            EndJump();
        }

        /// <summary>
        /// Updates the shared grounded state from the character's ground probe.
        /// </summary>
        public void SetGroundedState(bool isGrounded)
        {
            m_isGrounded = isGrounded;
        }

        /// <summary>
        /// Marks the character as performing a deliberate jump.
        /// </summary>
        public void BeginJump()
        {
            if (!IsSpawned || !IsOwner || m_characterNetworkManager == null)
            {
                return;
            }

            m_characterNetworkManager.IsJumping.Value = true;
        }

        /// <summary>
        /// Clears the deliberate jump state after landing or an animation fail-safe.
        /// </summary>
        public void EndJump()
        {
            if (!IsSpawned || !IsOwner || m_characterNetworkManager == null)
            {
                return;
            }

            m_characterNetworkManager.IsJumping.Value = false;
        }

        /// <summary>
        /// Enters the replicated death lifecycle and holds the character in its death action.
        /// </summary>
        public virtual IEnumerator ProcessDeathEvent(
            bool manuallySelectDeathAnimation = false)
        {
            if (!BeginDeathEvent(manuallySelectDeathAnimation))
            {
                yield break;
            }

            yield return WaitForRevive();
        }

        /// <summary>
        /// Restores the local character presentation after its authority clears the death state.
        /// </summary>
        public virtual void ReviveCharacter()
        {
            m_isDeathEventRunning = false;
            m_isInvulnerable = false;
            m_characterAnimatorManager?.PlayEmptyActionAnimation();
            ResetActionFlags();
        }

        protected bool BeginDeathEvent(bool manuallySelectDeathAnimation)
        {
            if (m_isDeathEventRunning)
            {
                return false;
            }

            m_isDeathEventRunning = true;
            if (IsSpawned && IsOwner && m_characterNetworkManager != null)
            {
                m_characterNetworkManager.CurrentHealth.Value = 0f;
                m_characterNetworkManager.IsDead.Value = true;
            }

            SetActionState(true, false, false, false);
            EndJump();
            if (!manuallySelectDeathAnimation)
            {
                m_characterAnimatorManager?.PlayTargetActionAnimation(
                    CharacterActionAnimation.Death,
                    true);
            }

            return true;
        }

        protected IEnumerator WaitForRevive()
        {
            while (m_isDeathEventRunning)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Ignores physics collisions between every collider owned by this character,
        /// so its body hitboxes never collide with each other or with the character itself.
        /// </summary>
        protected virtual void IgnoreMyOwnColliders()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int indexA = 0; indexA < colliders.Length; indexA++)
            {
                for (int indexB = indexA + 1; indexB < colliders.Length; indexB++)
                {
                    Physics.IgnoreCollision(colliders[indexA], colliders[indexB], true);
                }
            }
        }

        private Transform FindCriticalAnchor()
        {
            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (descendant != transform && descendant.name == "Target")
                {
                    return descendant;
                }
            }

            GameObject anchor = new GameObject("Lock On Transform");
            Transform anchorTransform = anchor.transform;
            anchorTransform.SetParent(transform, false);
            anchorTransform.localPosition = Vector3.up * 1.2f;
            return anchorTransform;
        }
    }
}
