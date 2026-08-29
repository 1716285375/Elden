using UnityEngine;

namespace ZZ
{
    public class PlayerAnimatorManager : CharacterAnimatorManager
    {
        private const string k_LadderOverrideLayerName = "Ladder Override";
        private const float k_LadderAnimationCompletionThreshold = 0.95f;

        private static readonly int s_isSlidingDownLadderParameter =
            Animator.StringToHash("isSlidingDownLadder");
        private static readonly int s_ladderEmptyState =
            Animator.StringToHash("Ladder Override.Empty");
        private static readonly int s_enterBottomState =
            Animator.StringToHash("Ladder Override.Enter Bottom");
        private static readonly int s_enterTopState =
            Animator.StringToHash("Ladder Override.Enter Top");
        private static readonly int s_idleLeftState =
            Animator.StringToHash("Ladder Override.Idle Left");
        private static readonly int s_idleRightState =
            Animator.StringToHash("Ladder Override.Idle Right");
        private static readonly int s_climbUpLeftState =
            Animator.StringToHash("Ladder Override.Climb Up Left");
        private static readonly int s_climbUpRightState =
            Animator.StringToHash("Ladder Override.Climb Up Right");
        private static readonly int s_climbDownLeftState =
            Animator.StringToHash("Ladder Override.Climb Down Left");
        private static readonly int s_climbDownRightState =
            Animator.StringToHash("Ladder Override.Climb Down Right");
        private static readonly int s_exitTopLeftState =
            Animator.StringToHash("Ladder Override.Exit Top Left");
        private static readonly int s_exitTopRightState =
            Animator.StringToHash("Ladder Override.Exit Top Right");
        private static readonly int s_exitBottomLeftState =
            Animator.StringToHash("Ladder Override.Exit Bottom Left");
        private static readonly int s_exitBottomRightState =
            Animator.StringToHash("Ladder Override.Exit Bottom Right");
        private static readonly int s_slideStartState =
            Animator.StringToHash("Ladder Override.Slide Start");
        private static readonly int s_slideMidState =
            Animator.StringToHash("Ladder Override.Slide Mid");
        private static readonly int s_slideEndState =
            Animator.StringToHash("Ladder Override.Slide End");
        private static readonly int s_jumpOffStartState =
            Animator.StringToHash("Ladder Override.Jump Off Start");
        private static readonly int s_jumpOffMidState =
            Animator.StringToHash("Ladder Override.Jump Off Mid");
        private static readonly int s_jumpOffEndState =
            Animator.StringToHash("Ladder Override.Jump Off End");
        private static readonly int s_fallStartState =
            Animator.StringToHash("Ladder Override.Fall Start");
        private static readonly int s_fallLoopState =
            Animator.StringToHash("Ladder Override.Fall Loop");

        private PlayerManager m_player;
        private CharacterController m_characterController;

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponentInParent<PlayerManager>();
            m_characterController = GetComponentInParent<CharacterController>();
        }

        /// <summary>
        /// Forwards the Jump Start animation event to the locally owned locomotion authority.
        /// </summary>
        public void ApplyJumpingVelocity()
        {
            m_player?.LocomotionManager?.ApplyJumpingVelocity();
        }

        /// <summary>
        /// Forwards the attack hit-frame event to enable the current weapon's damage collider.
        /// </summary>
        public void OpenDamageCollider()
        {
            m_player?.EquipmentManager?.OpenDamageCollider();
        }

        /// <summary>
        /// Forwards the attack end-frame event to disable the current weapon's damage collider.
        /// </summary>
        public void CloseDamageCollider()
        {
            m_player?.EquipmentManager?.CloseDamageCollider();
        }

        /// <summary>Animation Event: opens the dual attack's main-hand hit window.</summary>
        public void OpenMainHandDamageCollider()
        {
            m_player?.EquipmentManager?.OpenMainHandDamageCollider();
        }

        /// <summary>Animation Event: closes the dual attack's main-hand hit window.</summary>
        public void CloseMainHandDamageCollider()
        {
            m_player?.EquipmentManager?.CloseMainHandDamageCollider();
        }

        /// <summary>Animation Event: opens the dual attack's off-hand hit window.</summary>
        public void OpenOffHandDamageCollider()
        {
            m_player?.EquipmentManager?.OpenOffHandDamageCollider();
        }

        /// <summary>Animation Event: closes the dual attack's off-hand hit window.</summary>
        public void CloseOffHandDamageCollider()
        {
            m_player?.EquipmentManager?.CloseOffHandDamageCollider();
        }

        /// <summary>
        /// Forwards the attack animation event that drains attack stamina on the owner.
        /// </summary>
        public void DrainStaminaBasedOnAttack()
        {
            m_player?.PlayerCombatManager?.DrainStaminaBasedOnAttack();
        }

        /// <summary>Animation Event: creates the equipped spell's warm-up effect.</summary>
        public void InstantiateSpellWarmUpEffects()
        {
            m_player?.PlayerCombatManager?.InstantiateSpellWarmUpEffects();
        }

        /// <summary>Animation Event: releases the equipped spell on this peer.</summary>
        public void InstantiateSpell()
        {
            m_player?.PlayerCombatManager?.InstantiateCurrentSpell();
        }

        /// <summary>Animation Event: clears charge state and transient spell effects.</summary>
        public void CompleteSpellCast()
        {
            m_player?.PlayerCombatManager?.CompleteSpellCast();
        }

        /// <summary>Animation Event: releases the owner-authoritative notched arrow.</summary>
        public void ReleaseArrow()
        {
            m_player?.PlayerCombatManager?.ReleaseArrow();
        }

        /// <summary>Animation Event: resolves the presented quick-slot item's success frame.</summary>
        public void SuccessfullyUseQuickSlotItem()
        {
            m_player?.PlayerCombatManager?.SuccessfullyUseQuickSlotItem();
        }

        /// <summary>Receives the warm-up event already authored in the source spell clips.</summary>
        public void EnableSpellWarmUpFX()
        {
            InstantiateSpellWarmUpEffects();
        }

        /// <summary>Receives the release event already authored in the source spell clips.</summary>
        public void EnableSpellReleaseFX()
        {
            InstantiateSpell();
        }

        /// <summary>
        /// Receives the authored release-sound event. The release prefab owns spatial audio.
        /// </summary>
        public void PlayReleaseSpellSoundSFX()
        {
        }

        /// <summary>Allows rotation again during an attack's recovery window.</summary>
        public void EnableCanRotate()
        {
            m_player?.SetCanRotate(true);
        }

        /// <summary>Locks rotation during an attack's active frames.</summary>
        public void DisableCanRotate()
        {
            m_player?.SetCanRotate(false);
        }

        /// <summary>Forwards the authored combo-window start event to combat state.</summary>
        public void EnableCanDoCombo()
        {
            m_player?.PlayerCombatManager?.EnableCanCombo();
        }

        /// <summary>Forwards the authored combo-window end event to combat state.</summary>
        public void DisableCanDoCombo()
        {
            m_player?.PlayerCombatManager?.CloseAttackInputQueueWindow();
        }

        /// <summary>Opens the roll-attack input window during dodge recovery.</summary>
        public void EnableCanPerformRollAttack()
        {
            m_player?.PlayerCombatManager?.EnableCanPerformRollAttack();
        }

        /// <summary>Opens the backstep-attack input window during dodge recovery.</summary>
        public void EnableCanPerformBackStepAttack()
        {
            m_player?.PlayerCombatManager?.EnableCanPerformBackStepAttack();
        }

        /// <summary>Closes an unconsumed moving-attack input window.</summary>
        public void DisableCanPerformCommittedAttack()
        {
            m_player?.PlayerCombatManager?.DisableCanPerformCommittedAttack();
        }

        // The following receivers satisfy authored attack animation events.
        // They are reserved for roll-cancel, move-cancel, and weapon-trail systems.

        public void EnableCanRoll()
        {
            m_player?.LocomotionManager?.SetCanRoll(true);
        }

        public void EnableCanMoveCancel() { }

        public void ActivateMainHandWeaponTrail() { }

        public void DeactivateMainHandWeaponTrail() { }

        /// <summary>Rebuilds the full-body ladder layer for state changes and late join.</summary>
        public void SetLadderPresentation(
            bool isClimbingLadder,
            LadderAnimationState animationState)
        {
            if (CharacterAnimator == null)
            {
                return;
            }

            int layerIndex = CharacterAnimator.GetLayerIndex(
                k_LadderOverrideLayerName);
            if (layerIndex < 0)
            {
                Debug.LogError(
                    $"Animator {CharacterAnimator.name} is missing " +
                    $"{k_LadderOverrideLayerName}.",
                    CharacterAnimator);
                return;
            }

            bool shouldShowLayer = isClimbingLadder ||
                LadderAnimationStateUtility.RequiresLadderLayerAfterClimb(
                    animationState);
            CharacterAnimator.SetLayerWeight(layerIndex, shouldShowLayer ? 1f : 0f);
            if (shouldShowLayer && animationState != LadderAnimationState.None)
            {
                PlayLadderAnimation(animationState);
            }
            else if (!shouldShowLayer &&
                CharacterAnimator.HasState(layerIndex, s_ladderEmptyState))
            {
                CharacterAnimator.Play(s_ladderEmptyState, layerIndex, 0f);
            }
        }

        /// <summary>Updates the held slide condition on every peer.</summary>
        public void SetLadderSlidingState(bool isSlidingDownLadder)
        {
            CharacterAnimator?.SetBool(
                s_isSlidingDownLadderParameter,
                isSlidingDownLadder);
        }

        /// <summary>Immediately starts one deterministic ladder segment.</summary>
        public bool PlayLadderAnimation(LadderAnimationState animationState)
        {
            if (CharacterAnimator == null)
            {
                return false;
            }

            int layerIndex = CharacterAnimator.GetLayerIndex(
                k_LadderOverrideLayerName);
            int stateHash = GetLadderStateHash(animationState);
            if (layerIndex < 0 || stateHash == 0 ||
                !CharacterAnimator.HasState(layerIndex, stateHash))
            {
                return false;
            }

            CharacterAnimator.Play(stateHash, layerIndex, 0f);
            return true;
        }

        /// <summary>Returns whether the requested non-looping ladder segment finished.</summary>
        public bool IsLadderAnimationComplete(LadderAnimationState animationState)
        {
            if (CharacterAnimator == null)
            {
                return false;
            }

            int layerIndex = CharacterAnimator.GetLayerIndex(
                k_LadderOverrideLayerName);
            int stateHash = GetLadderStateHash(animationState);
            if (layerIndex < 0 || stateHash == 0 ||
                CharacterAnimator.IsInTransition(layerIndex))
            {
                return false;
            }

            AnimatorStateInfo stateInfo =
                CharacterAnimator.GetCurrentAnimatorStateInfo(layerIndex);
            return stateInfo.fullPathHash == stateHash &&
                stateInfo.normalizedTime >=
                    k_LadderAnimationCompletionThreshold;
        }

        /// <summary>Animation Event: releases gravity after the ladder fall start.</summary>
        public void FallFromLadderAnimationEvent()
        {
            m_player?.LocomotionManager?.CompleteFallFromLadder();
        }

        private void OnAnimatorMove()
        {
            bool usesLadderRootMotion =
                m_player?.PlayerNetworkManager?.IsClimbingLadder.Value == true ||
                LadderAnimationStateUtility.RequiresLadderLayerAfterClimb(
                    m_player?.PlayerNetworkManager
                        ?.CurrentLadderAnimationState.Value ??
                        LadderAnimationState.None);
            if (m_player == null ||
                !m_player.IsOwner ||
                (!m_player.ShouldApplyRootMotion && !usesLadderRootMotion) ||
                CharacterAnimator == null ||
                m_characterController == null)
            {
                return;
            }

            if (m_characterController.enabled)
            {
                m_characterController.Move(CharacterAnimator.deltaPosition);
            }
            else
            {
                m_player.transform.position += CharacterAnimator.deltaPosition;
            }

            m_player.transform.rotation *= CharacterAnimator.deltaRotation;
        }

        private static int GetLadderStateHash(LadderAnimationState state)
        {
            switch (state)
            {
                case LadderAnimationState.EnterBottom:
                    return s_enterBottomState;
                case LadderAnimationState.EnterTop:
                    return s_enterTopState;
                case LadderAnimationState.IdleLeft:
                    return s_idleLeftState;
                case LadderAnimationState.IdleRight:
                    return s_idleRightState;
                case LadderAnimationState.ClimbUpLeft:
                    return s_climbUpLeftState;
                case LadderAnimationState.ClimbUpRight:
                    return s_climbUpRightState;
                case LadderAnimationState.ClimbDownLeft:
                    return s_climbDownLeftState;
                case LadderAnimationState.ClimbDownRight:
                    return s_climbDownRightState;
                case LadderAnimationState.ExitTopLeft:
                    return s_exitTopLeftState;
                case LadderAnimationState.ExitTopRight:
                    return s_exitTopRightState;
                case LadderAnimationState.ExitBottomLeft:
                    return s_exitBottomLeftState;
                case LadderAnimationState.ExitBottomRight:
                    return s_exitBottomRightState;
                case LadderAnimationState.SlideStart:
                    return s_slideStartState;
                case LadderAnimationState.SlideMid:
                    return s_slideMidState;
                case LadderAnimationState.SlideEnd:
                    return s_slideEndState;
                case LadderAnimationState.JumpOffStart:
                    return s_jumpOffStartState;
                case LadderAnimationState.JumpOffMid:
                    return s_jumpOffMidState;
                case LadderAnimationState.JumpOffEnd:
                    return s_jumpOffEndState;
                case LadderAnimationState.FallStart:
                    return s_fallStartState;
                case LadderAnimationState.FallLoop:
                    return s_fallLoopState;
                default:
                    return s_ladderEmptyState;
            }
        }
    }
}
