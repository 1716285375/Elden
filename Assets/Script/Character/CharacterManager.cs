using System.Collections;
using System.Collections.Generic;
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
        private bool m_hasFrozenStateSnapshot;
        private bool m_canMoveBeforeFrozen;
        private bool m_canRotateBeforeFrozen;
        private bool m_wasPerformingActionBeforeFrozen;
        private bool m_shouldApplyRootMotionBeforeFrozen;
        private bool m_canRunBeforeFrozen;
        private bool m_canRollBeforeFrozen;
        private float m_animatorSpeedBeforeFrozen = 1f;
        private readonly List<FrozenRendererState> m_frozenRendererStates = new();
        private readonly List<FrozenBehaviourState> m_frozenBehaviourStates = new();

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
            SetFrozenState(false);
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
            if (m_hasFrozenStateSnapshot)
            {
                return;
            }

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
            if (m_hasFrozenStateSnapshot)
            {
                return;
            }

            m_canRotate = canRotate;
        }

        /// <summary>Updates only the movement permission for an active action state.</summary>
        public void SetCanMove(bool canMove)
        {
            if (m_hasFrozenStateSnapshot)
            {
                return;
            }

            m_canMove = canMove;
        }

        /// <summary>
        /// Freezes or restores animation, action permissions, IK behaviours, and renderer materials.
        /// </summary>
        public void SetFrozenState(bool isFrozen)
        {
            if (isFrozen)
            {
                ApplyFrozenState();
                return;
            }

            RestoreFrozenState();
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
            if (m_hasFrozenStateSnapshot)
            {
                m_wasPerformingActionBeforeFrozen = false;
                m_shouldApplyRootMotionBeforeFrozen = false;
                m_canMoveBeforeFrozen = true;
                m_canRotateBeforeFrozen = true;
                m_canRunBeforeFrozen = true;
                m_canRollBeforeFrozen = true;
                m_characterCombatManager?.ResetActionState();
                m_characterNetworkManager?.SetRollingState(false);
                EndJump();
                return;
            }

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
            if (IsSpawned && IsOwner && m_characterNetworkManager != null)
            {
                m_characterNetworkManager.TrySetPoisoned(false);
                m_characterNetworkManager.TrySetBuildup(Buildup.Poison, 0f);
                m_characterNetworkManager.TrySetFrostbitten(false);
                m_characterNetworkManager.TrySetFrozen(false);
                m_characterNetworkManager.TrySetBuildup(Buildup.Frost, 0f);
            }

            SetFrozenState(false);

            m_isDeathEventRunning = false;
            m_isInvulnerable = false;
            m_characterAnimatorManager?.PlayEmptyActionAnimation();
            ResetActionFlags();
        }

        private void ApplyFrozenState()
        {
            if (m_hasFrozenStateSnapshot)
            {
                return;
            }

            m_hasFrozenStateSnapshot = true;
            m_canMoveBeforeFrozen = m_canMove;
            m_canRotateBeforeFrozen = m_canRotate;
            m_wasPerformingActionBeforeFrozen = m_isPerformingAction;
            m_shouldApplyRootMotionBeforeFrozen = m_shouldApplyRootMotion;
            m_canRunBeforeFrozen = m_characterLocomotionManager?.CanRun ?? true;
            m_canRollBeforeFrozen = m_characterLocomotionManager?.CanRoll ?? true;
            m_animatorSpeedBeforeFrozen = m_animator != null
                ? m_animator.speed
                : 1f;

            m_canMove = false;
            m_canRotate = false;
            m_isPerformingAction = true;
            m_shouldApplyRootMotion = false;
            m_characterLocomotionManager?.SetCanRun(false);
            m_characterLocomotionManager?.SetCanRoll(false);
            if (m_animator != null)
            {
                m_animator.speed = 0f;
            }

            DisableIKBehaviours();
            ApplyFrozenMaterials();
        }

        private void RestoreFrozenState()
        {
            if (!m_hasFrozenStateSnapshot)
            {
                return;
            }

            RestoreFrozenMaterials();
            RestoreIKBehaviours();
            if (m_animator != null)
            {
                m_animator.speed = m_animatorSpeedBeforeFrozen;
            }

            m_canMove = m_canMoveBeforeFrozen;
            m_canRotate = m_canRotateBeforeFrozen;
            m_isPerformingAction = m_wasPerformingActionBeforeFrozen;
            m_shouldApplyRootMotion = m_shouldApplyRootMotionBeforeFrozen;
            m_characterLocomotionManager?.SetCanRun(m_canRunBeforeFrozen);
            m_characterLocomotionManager?.SetCanRoll(m_canRollBeforeFrozen);
            m_hasFrozenStateSnapshot = false;
        }

        private void DisableIKBehaviours()
        {
            m_frozenBehaviourStates.Clear();
            Behaviour[] behaviours = GetComponentsInChildren<Behaviour>(true);
            foreach (Behaviour behaviour in behaviours)
            {
                if (behaviour == null || !IsIKBehaviour(behaviour))
                {
                    continue;
                }

                m_frozenBehaviourStates.Add(new FrozenBehaviourState(
                    behaviour,
                    behaviour.enabled));
                behaviour.enabled = false;
            }
        }

        private void RestoreIKBehaviours()
        {
            foreach (FrozenBehaviourState state in m_frozenBehaviourStates)
            {
                if (state.Behaviour != null)
                {
                    state.Behaviour.enabled = state.WasEnabled;
                }
            }

            m_frozenBehaviourStates.Clear();
        }

        private void ApplyFrozenMaterials()
        {
            RestoreFrozenMaterials();
            Material frozenMaterial =
                WorldCharacterEffectsManager.Instance?.FrozenMaterial;
            if (frozenMaterial == null)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer is not SkinnedMeshRenderer &&
                    renderer is not MeshRenderer)
                {
                    continue;
                }

                Material[] originalMaterials = renderer.sharedMaterials;
                Material[] frozenMaterials = new Material[originalMaterials.Length];
                for (int materialIndex = 0;
                    materialIndex < frozenMaterials.Length;
                    materialIndex++)
                {
                    frozenMaterials[materialIndex] = new Material(frozenMaterial);
                }

                m_frozenRendererStates.Add(new FrozenRendererState(
                    renderer,
                    originalMaterials,
                    frozenMaterials));
                renderer.sharedMaterials = frozenMaterials;
            }
        }

        private void RestoreFrozenMaterials()
        {
            foreach (FrozenRendererState state in m_frozenRendererStates)
            {
                if (state.Renderer != null)
                {
                    state.Renderer.sharedMaterials = state.OriginalMaterials;
                }

                foreach (Material frozenMaterial in state.FrozenMaterials)
                {
                    DestroyMaterialInstance(frozenMaterial);
                }
            }

            m_frozenRendererStates.Clear();
        }

        private static bool IsIKBehaviour(Behaviour behaviour)
        {
            string typeName = behaviour.GetType().Name;
            return typeName.IndexOf(
                    "IK",
                    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf(
                    "RigBuilder",
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DestroyMaterialInstance(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }

        private sealed class FrozenRendererState
        {
            public FrozenRendererState(
                Renderer renderer,
                Material[] originalMaterials,
                Material[] frozenMaterials)
            {
                Renderer = renderer;
                OriginalMaterials = originalMaterials;
                FrozenMaterials = frozenMaterials;
            }

            public Renderer Renderer { get; }
            public Material[] OriginalMaterials { get; }
            public Material[] FrozenMaterials { get; }
        }

        private readonly struct FrozenBehaviourState
        {
            public FrozenBehaviourState(Behaviour behaviour, bool wasEnabled)
            {
                Behaviour = behaviour;
                WasEnabled = wasEnabled;
            }

            public Behaviour Behaviour { get; }
            public bool WasEnabled { get; }
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
