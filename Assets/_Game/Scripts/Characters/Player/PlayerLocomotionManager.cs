using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    public class PlayerLocomotionManager : CharacterLocomotionManager
    {
        private const float k_SprintMovementThreshold = 0.5f;

        [Header("Movement Speeds")]
        [FormerlySerializedAs("walkingSpeed")]
        [SerializeField, Min(0f)] private float m_walkingSpeed = 2f;
        [FormerlySerializedAs("runningSpeed")]
        [SerializeField, Min(0f)] private float m_runningSpeed = 5f;
        [SerializeField, Min(0f)] private float m_runningBackwardsSpeed = 4f;
        [SerializeField, Min(0f)] private float m_sprintingSpeed = 8f;
        [SerializeField, Min(0f)] private float m_sneakingWalkingSpeed = 1.1f;
        [SerializeField, Min(0f)] private float m_sneakingRunningSpeed = 3f;
        [SerializeField, Min(0f)] private float m_sneakingBackwardsSpeed = 2.8f;
        [FormerlySerializedAs("rotationSpeed")]
        [SerializeField, Min(0f)] private float m_rotationSpeed = 15f;

        [Header("Air Movement")]
        [SerializeField, Min(0f)] private float m_jumpHeight = 2f;
        [SerializeField, Min(0f)] private float m_jumpForwardSpeed = 5f;
        [SerializeField, Min(0f)] private float m_freeFallingSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float m_sprintJumpMomentum = 1f;
        [SerializeField, Range(0f, 1f)] private float m_runJumpMomentum = 0.5f;
        [SerializeField, Range(0f, 1f)] private float m_walkJumpMomentum = 0.25f;

        [Header("Stamina Costs")]
        [SerializeField, Min(0f)] private float m_sprintingStaminaCost = 10f;
        [SerializeField, Min(0f)] private float m_dodgeStaminaCost = 25f;
        [SerializeField, Min(0f)] private float m_jumpStaminaCost = 25f;

        [Header("Ladder Movement")]
        [SerializeField, Range(0.05f, 1f)]
        private float m_ladderInputThreshold = 0.2f;
        [SerializeField, Min(0.01f)]
        private float m_ladderHorizontalSmoothTime = 0.08f;
        [SerializeField, Min(0f)]
        private float m_topExitMinimumHeightDelay = 0.45f;
        [SerializeField, Min(0.1f)]
        private float m_knockOffLadderWindow = 1.25f;
        [SerializeField] private float m_jumpOffLadderUpwardVelocity = 2f;

        private PlayerManager m_player;
        private PlayerInputManager m_playerInputManager;
        private PlayerCamera m_playerCamera;
        private Vector3 m_jumpDirection;
        private LadderInteractable m_currentLadder;
        private LadderInteractable m_currentLadderExit;
        private Vector3 m_ladderHorizontalVelocity;
        private LadderHandState m_handBeforeSlide = LadderHandState.Left;
        private bool m_isExitingLadder;
        private bool m_canExitWithLeftHand;
        private bool m_canExitWithRightHand;
        private bool m_canBeKnockedOffLadder;
        private bool m_finishLadderAfterSlide;
        private Coroutine m_knockOffLadderRoutine;
        private Coroutine m_limitTopExitHeightRoutine;
        private Coroutine m_scheduleMinimumExitHeightRoutine;
        private Coroutine m_forceMinimumExitHeightRoutine;

        public bool IsSprinting =>
            m_player != null &&
            m_player.PlayerNetworkManager != null &&
            m_player.PlayerNetworkManager.IsSprinting.Value;

        public bool IsSneaking =>
            m_player?.CharacterNetworkManager?.IsSneaking.Value == true;

        public bool IsClimbingLadder =>
            m_player?.PlayerNetworkManager?.IsClimbingLadder.Value == true;

        public bool IsExitingLadder => m_isExitingLadder;
        public bool CanBeKnockedOffLadder => m_player != null &&
            m_player.IsSpawned &&
            m_player.CharacterNetworkManager != null
                ? m_player.CharacterNetworkManager.CanBeKnockedOffLadder.Value
                : m_canBeKnockedOffLadder;

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
        }

        private void Update()
        {
            HandleAllMovement();
        }

        private void LateUpdate()
        {
            LockToLadderHorizontalPosition();
        }

        public void HandleAllMovement()
        {
            if (m_player == null || !m_player.IsSpawned)
            {
                return;
            }

            if (!m_player.IsInGameplayScene)
            {
                ResetAirborneMovement();
                m_jumpDirection = Vector3.zero;
                if (m_player.IsOwner)
                {
                    SetSprinting(false);
                    PublishMovementState(0f, 0f, 0f);
                    m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(0f, 0f, false);
                }
                return;
            }

            if (IsInLadderMode())
            {
                if (m_player.IsOwner)
                {
                    HandleOwnerLadderMovement();
                }

                return;
            }

            HandleGroundCheck();
            CorrectRemotePositionDesynchronization();

            if (m_player.IsOwner)
            {
                HandleOwnerMovement();
                return;
            }

            HandleRemoteMovementAnimation();
        }

        public void WarpTo(Vector3 position, Quaternion rotation)
        {
            ResetAirborneMovement();
            m_jumpDirection = Vector3.zero;
            bool controllerWasEnabled = m_characterController != null && m_characterController.enabled;
            if (controllerWasEnabled)
            {
                m_characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);

            if (controllerWasEnabled)
            {
                m_characterController.enabled = true;
            }
        }

        /// <summary>Aligns the owner to one authored entrance and begins Root Motion.</summary>
        public bool BeginLadderClimb(LadderInteractable ladderEntrance)
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (m_player == null ||
                !m_player.IsOwner ||
                !m_player.IsSpawned ||
                m_player.IsDead ||
                m_player.IsPerformingAction ||
                IsClimbingLadder ||
                ladderEntrance?.StartPosition == null ||
                ladderEntrance.LadderHorizontalPosition == null ||
                networkManager == null)
            {
                return false;
            }

            StopLadderRoutines();
            m_currentLadder = ladderEntrance;
            m_currentLadderExit = ladderEntrance;
            m_isExitingLadder = false;
            m_canExitWithLeftHand = false;
            m_canExitWithRightHand = false;
            SetCanBeKnockedOffLadder(false);
            m_finishLadderAfterSlide = false;
            m_ladderHorizontalVelocity = Vector3.zero;
            StopSprinting();
            m_player.EndJump();
            ResetAirborneMovement();
            SetIgnoreGravity(true);
            if (m_characterController != null)
            {
                m_characterController.enabled = false;
            }

            Transform startPosition = ladderEntrance.StartPosition;
            transform.SetPositionAndRotation(
                startPosition.position,
                startPosition.rotation);
            m_player.SetActionState(true, true, false, false);
            LadderAnimationState entranceState = ladderEntrance.IsTopEntrance
                ? LadderAnimationState.EnterTop
                : LadderAnimationState.EnterBottom;
            networkManager.SetLadderAnimationState(entranceState);
            networkManager.SetClimbingLadderState(true);
            return true;
        }

        /// <summary>Tracks the ladder end currently overlapping the local owner.</summary>
        public void SetLadderExitInteractable(
            LadderInteractable ladderExit,
            bool isInsideExit)
        {
            if (!IsClimbingLadder || ladderExit == null ||
                m_currentLadder == null ||
                ladderExit.transform.parent != m_currentLadder.transform.parent)
            {
                return;
            }

            if (isInsideExit)
            {
                m_currentLadderExit = ladderExit;
            }
            else if (m_currentLadderExit == ladderExit)
            {
                m_currentLadderExit = null;
            }
        }

        /// <summary>Receives the hand-specific exit window from Ladder Idle states.</summary>
        public void SetCanExitLadder(LadderHandState handState, bool canExit)
        {
            if (handState == LadderHandState.Left)
            {
                m_canExitWithLeftHand = canExit;
            }
            else
            {
                m_canExitWithRightHand = canExit;
            }
        }

        /// <summary>Redirects held Sprint to the synchronized ladder slide.</summary>
        public bool HandleLadderSliding(bool isSprintInputHeld)
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (!IsClimbingLadder || networkManager == null)
            {
                return false;
            }

            bool shouldSlide = isSprintInputHeld && !m_isExitingLadder;
            networkManager.SetSlidingDownLadderState(shouldSlide);
            LadderAnimationState currentState =
                networkManager.CurrentLadderAnimationState.Value;
            if (!shouldSlide &&
                (currentState == LadderAnimationState.SlideStart ||
                    currentState == LadderAnimationState.SlideMid))
            {
                networkManager.SetLadderAnimationState(
                    LadderAnimationState.SlideEnd);
            }

            return true;
        }

        /// <summary>Redirects Dodge to a Root Motion jump away from the ladder.</summary>
        public bool JumpOffLadder()
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (!IsClimbingLadder ||
                m_isExitingLadder ||
                m_player == null ||
                !m_player.IsOwner ||
                networkManager == null)
            {
                return false;
            }

            m_isExitingLadder = true;
            m_currentLadderExit = null;
            networkManager.SetSlidingDownLadderState(false);
            networkManager.SetLadderAnimationState(
                LadderAnimationState.JumpOffStart);
            networkManager.SetClimbingLadderState(false);
            if (m_characterController != null)
            {
                m_characterController.enabled = true;
            }

            SetIgnoreGravity(false);
            m_verticalVelocity.y = m_jumpOffLadderUpwardVelocity;
            m_player.SetGroundedState(false);
            m_player.SetActionState(true, true, false, false);
            return true;
        }

        /// <summary>Consumes ladder hits and drops the owner on the second timed hit.</summary>
        public bool RegisterLadderHit()
        {
            if (!IsClimbingLadder)
            {
                return false;
            }

            if (m_player == null || !m_player.IsOwner)
            {
                return true;
            }

            if (CanBeKnockedOffLadder)
            {
                FallFromLadder();
                return true;
            }

            SetCanBeKnockedOffLadder(true);
            if (m_knockOffLadderRoutine != null)
            {
                StopCoroutine(m_knockOffLadderRoutine);
            }

            m_knockOffLadderRoutine = StartCoroutine(
                ResetKnockOffLadderWindow());
            return true;
        }

        /// <summary>Animation Event: releases the player into ordinary falling.</summary>
        public void CompleteFallFromLadder()
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (m_player == null ||
                !m_player.IsOwner ||
                networkManager?.CurrentLadderAnimationState.Value !=
                    LadderAnimationState.FallStart)
            {
                return;
            }

            networkManager.SetLadderAnimationState(
                LadderAnimationState.FallLoop);
            networkManager.SetClimbingLadderState(false);
            if (m_characterController != null)
            {
                m_characterController.enabled = true;
            }

            SetIgnoreGravity(false);
            m_verticalVelocity.y = Mathf.Min(m_verticalVelocity.y, -1f);
            m_player.SetGroundedState(false);
            m_player.SetActionState(false, false, true, true);
        }

        /// <summary>
        /// Validates a jump intent, consumes stamina, and starts the authored take-off animation.
        /// </summary>
        public void AttemptToPerformJump()
        {
            if (m_player == null ||
                !m_player.IsOwner ||
                m_player.PlayerCombatManager?.IsUsingItem == true)
            {
                return;
            }

            float currentStamina = m_player.CharacterNetworkManager != null
                ? m_player.CharacterNetworkManager.CurrentStamina.Value
                : 0f;
            if (!CanJump(
                m_player.IsPerformingAction,
                currentStamina,
                m_player.IsJumping,
                m_player.IsGrounded) ||
                m_player.PlayerAnimatorManager == null ||
                !m_player.PlayerAnimatorManager.CanPlayJumpStartAnimation())
            {
                return;
            }

            PlayerStatsManager statsManager = m_player.PlayerStatsManager;
            if (statsManager == null || !statsManager.TryConsumeStamina(m_jumpStaminaCost))
            {
                return;
            }

            m_playerInputManager ??= PlayerInputManager.Instance;
            m_playerCamera ??= PlayerCamera.Instance;
            CaptureJumpDirection();
            SetSprinting(false);
            m_player.BeginJump();
            m_player.PlayerAnimatorManager.PlayJumpStartAnimation();
        }

        /// <summary>
        /// Applies the calculated upward velocity at the Jump Start animation's take-off frame.
        /// </summary>
        public void ApplyJumpingVelocity()
        {
            if (m_player == null || !m_player.IsOwner || !m_player.IsJumping)
            {
                return;
            }

            m_verticalVelocity.y = CalculateJumpVelocity(m_jumpHeight, GravityForce);
        }

        /// <summary>
        /// Validates a dodge request and selects a roll or backstep from the current movement input.
        /// </summary>
        public void AttemptToPerformDodge()
        {
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }

            if (IsClimbingLadder)
            {
                JumpOffLadder();
                return;
            }

            if (!CanRoll)
            {
                return;
            }

            if (m_player.IsPerformingAction)
            {
                if (m_player.PlayerCombatManager?.HasArrowNotched != true)
                {
                    return;
                }

                m_player.PlayerCombatManager.CancelNotchedProjectile(true);
            }

            m_playerInputManager ??= PlayerInputManager.Instance;
            m_playerCamera ??= PlayerCamera.Instance;
            if (m_playerInputManager == null)
            {
                return;
            }

            if (m_playerInputManager.MoveAmount > 0f)
            {
                if (m_playerCamera == null || m_playerCamera.CameraObject == null)
                {
                    return;
                }

                if (!TryConsumeDodgeStamina())
                {
                    return;
                }

                SetSprinting(false);
                PerformRoll();
                return;
            }

            if (!TryConsumeDodgeStamina())
            {
                return;
            }

            SetSprinting(false);
            PerformBackstep();
        }

        /// <summary>
        /// Resolves held sprint input into the player's validated sprint gameplay state.
        /// </summary>
        public void HandleSprinting(bool isSprintInputHeld)
        {
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }

            if (HandleLadderSliding(isSprintInputHeld))
            {
                SetSprinting(false);
                return;
            }

            m_playerInputManager ??= PlayerInputManager.Instance;
            float moveAmount = m_playerInputManager != null
                ? m_playerInputManager.MoveAmount
                : 0f;
            float currentStamina = m_player.CharacterNetworkManager != null
                ? m_player.CharacterNetworkManager.CurrentStamina.Value
                : 0f;
            SetSprinting(CanSprint(
                isSprintInputHeld,
                m_player.IsPerformingAction,
                moveAmount,
                currentStamina) && CanRun);
        }

        /// <summary>Stops the replicated sprint state when a committed action begins.</summary>
        public void StopSprinting()
        {
            if (m_player != null && m_player.IsOwner)
            {
                SetSprinting(false);
            }
        }

        private void HandleOwnerLadderMovement()
        {
            PlayerNetworkManager networkManager = m_player.PlayerNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            LadderAnimationState currentState =
                networkManager.CurrentLadderAnimationState.Value;
            if (!IsClimbingLadder)
            {
                HandleGroundCheck();
                HandleVerticalMovement();
                AdvanceLadderTransition(currentState);
                return;
            }

            m_playerInputManager ??= PlayerInputManager.Instance;
            float verticalInput = m_playerInputManager != null
                ? m_playerInputManager.VerticalInput
                : 0f;
            m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(
                0f,
                verticalInput,
                false);
            TryBeginLadderExit(currentState, verticalInput);
            AdvanceClimbingLadderState(
                networkManager.CurrentLadderAnimationState.Value,
                verticalInput);
        }

        private void AdvanceClimbingLadderState(
            LadderAnimationState currentState,
            float verticalInput)
        {
            PlayerNetworkManager networkManager = m_player.PlayerNetworkManager;
            PlayerAnimatorManager animatorManager =
                m_player.PlayerAnimatorManager;
            if (networkManager == null || animatorManager == null)
            {
                return;
            }

            if (currentState == LadderAnimationState.FallStart)
            {
                if (animatorManager.IsLadderAnimationComplete(currentState))
                {
                    CompleteFallFromLadder();
                }

                return;
            }

            if (IsExitAnimation(currentState))
            {
                if (animatorManager.IsLadderAnimationComplete(currentState))
                {
                    FinishLadderExit();
                }

                return;
            }

            if (currentState == LadderAnimationState.SlideStart)
            {
                if (animatorManager.IsLadderAnimationComplete(currentState))
                {
                    networkManager.SetLadderAnimationState(
                        LadderAnimationState.SlideMid);
                }

                return;
            }

            if (currentState == LadderAnimationState.SlideMid)
            {
                if (!networkManager.IsSlidingDownLadder.Value)
                {
                    networkManager.SetLadderAnimationState(
                        LadderAnimationState.SlideEnd);
                }

                return;
            }

            if (currentState == LadderAnimationState.SlideEnd)
            {
                if (!animatorManager.IsLadderAnimationComplete(currentState))
                {
                    return;
                }

                if (m_finishLadderAfterSlide)
                {
                    FinishLadderExit();
                }
                else
                {
                    SetIdleLadderState(m_handBeforeSlide);
                }

                return;
            }

            if (LadderAnimationStateUtility.IsIdle(currentState))
            {
                LadderHandState currentHand =
                    LadderAnimationStateUtility.GetIdleHand(currentState);
                if (networkManager.IsSlidingDownLadder.Value)
                {
                    m_handBeforeSlide = currentHand;
                    networkManager.SetLadderAnimationState(
                        LadderAnimationState.SlideStart);
                    return;
                }

                if (Mathf.Abs(verticalInput) < m_ladderInputThreshold)
                {
                    return;
                }

                networkManager.SetLadderAnimationState(
                    LadderAnimationStateUtility.GetSegment(
                        currentHand,
                        verticalInput));
                return;
            }

            if (animatorManager.IsLadderAnimationComplete(currentState))
            {
                networkManager.SetLadderAnimationState(
                    LadderAnimationStateUtility.GetIdleAfterCompletedState(
                        currentState));
            }
        }

        private void AdvanceLadderTransition(LadderAnimationState currentState)
        {
            PlayerNetworkManager networkManager = m_player.PlayerNetworkManager;
            PlayerAnimatorManager animatorManager =
                m_player.PlayerAnimatorManager;
            if (networkManager == null || animatorManager == null)
            {
                return;
            }

            switch (currentState)
            {
                case LadderAnimationState.JumpOffStart:
                    if (animatorManager.IsLadderAnimationComplete(currentState))
                    {
                        networkManager.SetLadderAnimationState(
                            LadderAnimationState.JumpOffMid);
                    }
                    break;
                case LadderAnimationState.JumpOffMid:
                    if (animatorManager.IsLadderAnimationComplete(currentState))
                    {
                        networkManager.SetLadderAnimationState(
                            LadderAnimationState.JumpOffEnd);
                    }
                    break;
                case LadderAnimationState.JumpOffEnd:
                    if (animatorManager.IsLadderAnimationComplete(currentState))
                    {
                        FinishLadderTransition();
                    }
                    break;
                case LadderAnimationState.FallLoop:
                    if (m_player.IsGrounded)
                    {
                        FinishLadderTransition();
                    }
                    break;
            }
        }

        private void TryBeginLadderExit(
            LadderAnimationState currentState,
            float verticalInput)
        {
            if (m_isExitingLadder || m_currentLadderExit == null)
            {
                return;
            }

            if (!m_currentLadderExit.IsTopEntrance &&
                LadderAnimationStateUtility.IsSliding(currentState))
            {
                BeginBottomLadderExit(true, LadderHandState.Left);
                return;
            }

            if (!LadderAnimationStateUtility.IsIdle(currentState) ||
                !TryGetExitHand(currentState, out LadderHandState handState))
            {
                return;
            }

            if (m_currentLadderExit.IsTopEntrance)
            {
                float minimumExitHeight =
                    m_currentLadderExit.GetTopExitHeight(handState);
                if (verticalInput >= m_ladderInputThreshold &&
                    transform.position.y >= minimumExitHeight - 0.1f)
                {
                    BeginTopLadderExit(handState, minimumExitHeight);
                }

                return;
            }

            if (verticalInput <= -m_ladderInputThreshold)
            {
                BeginBottomLadderExit(false, handState);
            }
        }

        private void BeginTopLadderExit(
            LadderHandState handState,
            float minimumExitHeight)
        {
            m_isExitingLadder = true;
            m_finishLadderAfterSlide = false;
            m_player.SetActionState(true, true, false, false);
            m_player.PlayerNetworkManager.SetSlidingDownLadderState(false);
            m_player.PlayerNetworkManager.SetLadderAnimationState(
                handState == LadderHandState.Left
                    ? LadderAnimationState.ExitTopLeft
                    : LadderAnimationState.ExitTopRight);
            m_limitTopExitHeightRoutine = StartCoroutine(
                LimitTopExitHeight(m_currentLadderExit.MaxTopExitHeight));
            m_scheduleMinimumExitHeightRoutine = StartCoroutine(
                ScheduleMinimumTopExitHeight(minimumExitHeight));
        }

        private void BeginBottomLadderExit(
            bool isSliding,
            LadderHandState handState)
        {
            m_isExitingLadder = true;
            m_finishLadderAfterSlide = isSliding;
            m_player.SetActionState(true, true, false, false);
            m_player.PlayerNetworkManager.SetSlidingDownLadderState(false);
            m_player.PlayerNetworkManager.SetLadderAnimationState(
                isSliding
                    ? LadderAnimationState.SlideEnd
                    : handState == LadderHandState.Left
                        ? LadderAnimationState.ExitBottomLeft
                        : LadderAnimationState.ExitBottomRight);
        }

        private void FinishLadderExit()
        {
            PlayerNetworkManager networkManager = m_player.PlayerNetworkManager;
            networkManager?.SetSlidingDownLadderState(false);
            networkManager?.SetClimbingLadderState(false);
            networkManager?.SetLadderAnimationState(LadderAnimationState.None);
            if (m_characterController != null)
            {
                m_characterController.enabled = true;
            }

            SetIgnoreGravity(false);
            m_player.SetActionState(false, false, true, true);
            ClearLadderContext();
        }

        private void FinishLadderTransition()
        {
            m_player.PlayerNetworkManager?.SetLadderAnimationState(
                LadderAnimationState.None);
            if (!m_player.IsDead)
            {
                m_player.SetActionState(false, false, true, true);
            }

            if (m_characterController != null)
            {
                m_characterController.enabled = true;
            }

            SetIgnoreGravity(false);
            ClearLadderContext();
        }

        private void FallFromLadder()
        {
            if (!IsClimbingLadder || m_isExitingLadder)
            {
                return;
            }

            m_isExitingLadder = true;
            SetCanBeKnockedOffLadder(false);
            if (m_knockOffLadderRoutine != null)
            {
                StopCoroutine(m_knockOffLadderRoutine);
                m_knockOffLadderRoutine = null;
            }

            m_player.PlayerNetworkManager?.SetSlidingDownLadderState(false);
            m_player.PlayerNetworkManager?.SetLadderAnimationState(
                LadderAnimationState.FallStart);
            m_player.SetActionState(true, true, false, false);
        }

        private void SetCanBeKnockedOffLadder(bool canBeKnockedOffLadder)
        {
            m_canBeKnockedOffLadder = canBeKnockedOffLadder;
            CharacterNetworkManager networkManager =
                m_player?.CharacterNetworkManager;
            if (m_player != null &&
                m_player.IsSpawned &&
                m_player.IsOwner &&
                networkManager != null)
            {
                networkManager.CanBeKnockedOffLadder.Value =
                    canBeKnockedOffLadder;
            }
        }

        private IEnumerator ResetKnockOffLadderWindow()
        {
            yield return new WaitForSeconds(m_knockOffLadderWindow);
            SetCanBeKnockedOffLadder(false);
            m_knockOffLadderRoutine = null;
        }

        private IEnumerator LimitTopExitHeight(float maximumHeight)
        {
            while (m_isExitingLadder && IsClimbingLadder)
            {
                if (transform.position.y > maximumHeight)
                {
                    Vector3 position = transform.position;
                    position.y = maximumHeight;
                    transform.position = position;
                }

                yield return null;
            }

            m_limitTopExitHeightRoutine = null;
        }

        private IEnumerator ScheduleMinimumTopExitHeight(float minimumHeight)
        {
            yield return new WaitForSeconds(m_topExitMinimumHeightDelay);
            m_scheduleMinimumExitHeightRoutine = null;
            if (m_isExitingLadder && IsClimbingLadder)
            {
                m_forceMinimumExitHeightRoutine = StartCoroutine(
                    ForceMinimumTopExitHeight(minimumHeight));
            }
        }

        private IEnumerator ForceMinimumTopExitHeight(float minimumHeight)
        {
            while (m_isExitingLadder && IsClimbingLadder)
            {
                if (transform.position.y < minimumHeight)
                {
                    Vector3 position = transform.position;
                    position.y = minimumHeight;
                    transform.position = position;
                }

                yield return null;
            }

            m_forceMinimumExitHeightRoutine = null;
        }

        private void LockToLadderHorizontalPosition()
        {
            if (!IsClimbingLadder ||
                m_isExitingLadder ||
                m_currentLadder?.LadderHorizontalPosition == null)
            {
                return;
            }

            Vector3 targetPosition =
                m_currentLadder.LadderHorizontalPosition.position;
            targetPosition.y = transform.position.y;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref m_ladderHorizontalVelocity,
                m_ladderHorizontalSmoothTime);
        }

        private void SetIdleLadderState(LadderHandState handState)
        {
            m_player.PlayerNetworkManager?.SetLadderAnimationState(
                handState == LadderHandState.Left
                    ? LadderAnimationState.IdleLeft
                    : LadderAnimationState.IdleRight);
        }

        private bool TryGetExitHand(
            LadderAnimationState currentState,
            out LadderHandState handState)
        {
            if (currentState == LadderAnimationState.IdleLeft &&
                m_canExitWithLeftHand)
            {
                handState = LadderHandState.Left;
                return true;
            }

            if (currentState == LadderAnimationState.IdleRight &&
                m_canExitWithRightHand)
            {
                handState = LadderHandState.Right;
                return true;
            }

            handState = default;
            return false;
        }

        private bool IsInLadderMode()
        {
            LadderAnimationState animationState =
                m_player?.PlayerNetworkManager?.CurrentLadderAnimationState.Value ??
                    LadderAnimationState.None;
            return IsClimbingLadder ||
                LadderAnimationStateUtility.RequiresLadderLayerAfterClimb(
                    animationState);
        }

        private static bool IsExitAnimation(LadderAnimationState state)
        {
            return state == LadderAnimationState.ExitTopLeft ||
                state == LadderAnimationState.ExitTopRight ||
                state == LadderAnimationState.ExitBottomLeft ||
                state == LadderAnimationState.ExitBottomRight;
        }

        private void ClearLadderContext()
        {
            StopLadderRoutines();
            m_currentLadder = null;
            m_currentLadderExit = null;
            m_isExitingLadder = false;
            m_canExitWithLeftHand = false;
            m_canExitWithRightHand = false;
            SetCanBeKnockedOffLadder(false);
            m_finishLadderAfterSlide = false;
            m_ladderHorizontalVelocity = Vector3.zero;
        }

        private void StopLadderRoutines()
        {
            StopLadderRoutine(ref m_knockOffLadderRoutine);
            StopLadderRoutine(ref m_limitTopExitHeightRoutine);
            StopLadderRoutine(ref m_scheduleMinimumExitHeightRoutine);
            StopLadderRoutine(ref m_forceMinimumExitHeightRoutine);
        }

        private void StopLadderRoutine(ref Coroutine routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private void HandleOwnerMovement()
        {
            m_playerInputManager ??= PlayerInputManager.Instance;
            m_playerCamera ??= PlayerCamera.Instance;
            SetGroundedVelocity();
            HandleSlopeSlideCheck();

            if (m_playerInputManager == null)
            {
                SetSprinting(false);
                PublishMovementState(0f, 0f, 0f);
                m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(0f, 0f, false);
                HandleVerticalMovement();
                UpdateAnimatorAirParameters();
                return;
            }

            ConsumeSprintingStamina();
            HandleGroundedMovement();
            HandleJumpingMovement();
            HandleFreeFallMovement();
            HandleRotation();
            HandleVerticalMovement();

            PublishMovementState(
                m_playerInputManager.HorizontalInput,
                m_playerInputManager.VerticalInput,
                m_playerInputManager.MoveAmount);
            bool usesStrafeMovement = UsesStrafeMovement();
            m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(
                usesStrafeMovement ? m_playerInputManager.HorizontalInput : 0f,
                usesStrafeMovement
                    ? m_playerInputManager.VerticalInput
                    : m_playerInputManager.MoveAmount,
                IsSprinting);
            UpdateAnimatorAirParameters();
        }

        private void HandleRemoteMovementAnimation()
        {
            CharacterNetworkManager networkManager = m_player.CharacterNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            bool usesStrafeMovement = UsesStrafeMovement();
            m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(
                usesStrafeMovement ? networkManager.HorizontalMovement.Value : 0f,
                usesStrafeMovement
                    ? networkManager.VerticalMovement.Value
                    : networkManager.MoveAmount.Value,
                m_player.PlayerNetworkManager != null &&
                m_player.PlayerNetworkManager.IsSprinting.Value);
            UpdateAnimatorAirParameters();
        }

        private void PublishMovementState(float horizontal, float vertical, float amount)
        {
            CharacterNetworkManager networkManager = m_player.CharacterNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            networkManager.HorizontalMovement.Value = horizontal;
            networkManager.VerticalMovement.Value = vertical;
            networkManager.MoveAmount.Value = amount;
        }

        private void HandleGroundedMovement()
        {
            if (!m_player.CanMove || !m_player.IsGrounded)
            {
                return;
            }

            if (m_playerInputManager == null || m_playerCamera == null || m_playerCamera.CameraObject == null)
            {
                return;
            }

            Vector3 moveDirection = GetMovementDirection();
            moveDirection.y = 0f;
            moveDirection.Normalize();

            float movementAmount = CanRun
                ? m_playerInputManager.MoveAmount
                : Mathf.Min(0.5f, m_playerInputManager.MoveAmount);
            float movementSpeed = ResolveGroundMovementSpeed(movementAmount);
            m_characterController.Move(moveDirection * movementSpeed * Time.deltaTime);
        }

        private float ResolveGroundMovementSpeed(float movementAmount)
        {
            if (IsSprinting)
            {
                return m_sprintingSpeed;
            }

            bool isMovingBackwards = UsesStrafeMovement() &&
                m_playerInputManager.VerticalInput < 0f;
            if (IsSneaking)
            {
                if (isMovingBackwards)
                {
                    return m_sneakingBackwardsSpeed;
                }

                return movementAmount > k_SprintMovementThreshold
                    ? m_sneakingRunningSpeed
                    : m_sneakingWalkingSpeed;
            }

            if (isMovingBackwards &&
                movementAmount > k_SprintMovementThreshold)
            {
                return m_runningBackwardsSpeed;
            }

            return movementAmount > k_SprintMovementThreshold
                ? m_runningSpeed
                : m_walkingSpeed;
        }

        private void HandleJumpingMovement()
        {
            if (!m_player.IsJumping || m_jumpDirection == Vector3.zero)
            {
                return;
            }

            m_characterController.Move(
                m_jumpDirection * m_jumpForwardSpeed * Time.deltaTime);
        }

        private void HandleFreeFallMovement()
        {
            if (m_player.IsGrounded ||
                m_playerInputManager == null ||
                m_playerCamera == null ||
                m_playerCamera.CameraObject == null)
            {
                return;
            }

            Vector3 freeFallDirection = GetCameraRelativeDirection();
            freeFallDirection.y = 0f;
            freeFallDirection.Normalize();
            m_characterController.Move(
                freeFallDirection * m_freeFallingSpeed * Time.deltaTime);
        }

        private void HandleRotation()
        {
            if (!m_player.CanRotate)
            {
                return;
            }

            if (m_playerInputManager == null || m_playerCamera == null || m_playerCamera.CameraObject == null)
            {
                return;
            }

            Vector3 targetRotationDirection = GetTargetRotationDirection();
            targetRotationDirection.y = 0f;
            targetRotationDirection.Normalize();

            if (targetRotationDirection == Vector3.zero)
            {
                targetRotationDirection = transform.forward;
            }

            Quaternion targetRotation = Quaternion.LookRotation(targetRotationDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                m_rotationSpeed * Time.deltaTime);
        }

        private Vector3 GetCameraRelativeDirection()
        {
            Vector3 forward = m_playerCamera.CameraForward;
            Vector3 right = m_playerCamera.CameraRight;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return forward * m_playerInputManager.VerticalInput + right * m_playerInputManager.HorizontalInput;
        }

        private Vector3 GetMovementDirection()
        {
            if (m_player?.PlayerNetworkManager?.IsAiming.Value == true)
            {
                return transform.forward * m_playerInputManager.VerticalInput +
                    transform.right * m_playerInputManager.HorizontalInput;
            }

            return GetCameraRelativeDirection();
        }

        private Vector3 GetTargetRotationDirection()
        {
            PlayerLockOnManager lockOnManager = m_player.LockOnManager;
            if (lockOnManager != null && lockOnManager.IsLockedOn)
            {
                return lockOnManager.CurrentTarget.transform.position - transform.position;
            }

            if (m_player?.PlayerNetworkManager?.IsAiming.Value == true)
            {
                Vector3 aimDirection = m_playerCamera.AimDirection;
                aimDirection.y = 0f;
                return aimDirection;
            }

            return GetCameraRelativeDirection();
        }

        private bool UsesStrafeMovement()
        {
            return m_player?.PlayerNetworkManager?.IsAiming.Value == true ||
                m_player?.LockOnManager?.IsLockedOn == true;
        }

        private void PerformRoll()
        {
            if (m_playerCamera == null || m_playerCamera.CameraObject == null)
            {
                return;
            }

            Vector3 rollDirection = GetCameraRelativeDirection();
            rollDirection.y = 0f;
            rollDirection.Normalize();
            if (rollDirection == Vector3.zero)
            {
                rollDirection = transform.forward;
            }

            transform.rotation = Quaternion.LookRotation(rollDirection);
            m_player.CharacterNetworkManager?.SetRollingState(true);
            PlayDodgeAction(CharacterActionAnimation.RollForward);
        }

        private void PerformBackstep()
        {
            PlayDodgeAction(CharacterActionAnimation.BackStep);
        }

        private void CaptureJumpDirection()
        {
            if (m_playerInputManager == null ||
                m_playerCamera == null ||
                m_playerCamera.CameraObject == null)
            {
                m_jumpDirection = Vector3.zero;
                return;
            }

            m_jumpDirection = GetCameraRelativeDirection();
            m_jumpDirection.y = 0f;
            m_jumpDirection.Normalize();
            float momentumScale = ResolveJumpMomentumScale(
                IsSprinting,
                m_playerInputManager.MoveAmount,
                m_sprintJumpMomentum,
                m_runJumpMomentum,
                m_walkJumpMomentum);
            m_jumpDirection *= momentumScale;
        }

        private void UpdateAnimatorAirParameters()
        {
            m_player.PlayerAnimatorManager?.UpdateAnimatorAirParameters(
                m_player.IsGrounded,
                InAirTimer);
        }

        private bool TryConsumeDodgeStamina()
        {
            return m_player.PlayerStatsManager != null &&
                m_player.PlayerStatsManager.TryConsumeStamina(m_dodgeStaminaCost);
        }

        private void ConsumeSprintingStamina()
        {
            if (!IsSprinting)
            {
                return;
            }

            float staminaCost = m_sprintingStaminaCost * Time.deltaTime;
            if (staminaCost <= 0f)
            {
                return;
            }

            PlayerStatsManager statsManager = m_player.PlayerStatsManager;
            if (statsManager == null ||
                !statsManager.TryConsumeStamina(staminaCost) ||
                m_player.CharacterNetworkManager.CurrentStamina.Value <= 0f)
            {
                SetSprinting(false);
            }
        }

        private void PlayDodgeAction(CharacterActionAnimation targetAnimation)
        {
            const bool k_IsPerformingAction = true;
            const bool k_ShouldApplyRootMotion = true;
            const bool k_CanRotate = false;
            const bool k_CanMove = false;

            m_player.PlayerAnimatorManager?.PlayTargetActionAnimation(
                targetAnimation,
                k_IsPerformingAction,
                k_ShouldApplyRootMotion,
                k_CanRotate,
                k_CanMove);
            m_player.CharacterNetworkManager?.NotifyServerOfActionAnimationServerRpc(
                targetAnimation,
                k_IsPerformingAction,
                k_ShouldApplyRootMotion,
                k_CanRotate,
                k_CanMove);
        }

        private static bool CanSprint(
            bool isSprintInputHeld,
            bool isPerformingAction,
            float moveAmount,
            float currentStamina)
        {
            return isSprintInputHeld &&
                !isPerformingAction &&
                moveAmount >= k_SprintMovementThreshold &&
                currentStamina > 0f;
        }

        private static bool CanJump(
            bool isPerformingAction,
            float currentStamina,
            bool isJumping,
            bool isGrounded)
        {
            return !isPerformingAction &&
                currentStamina > 0f &&
                !isJumping &&
                isGrounded;
        }

        private static float CalculateJumpVelocity(float jumpHeight, float gravityForce)
        {
            if (jumpHeight <= 0f || gravityForce >= 0f)
            {
                return 0f;
            }

            return Mathf.Sqrt(jumpHeight * -2f * gravityForce);
        }

        private static float ResolveJumpMomentumScale(
            bool isSprinting,
            float moveAmount,
            float sprintMomentum,
            float runMomentum,
            float walkMomentum)
        {
            if (moveAmount <= 0f)
            {
                return 0f;
            }

            if (isSprinting)
            {
                return sprintMomentum;
            }

            return moveAmount > k_SprintMovementThreshold
                ? runMomentum
                : walkMomentum;
        }

        private void SetSprinting(bool isSprinting)
        {
            PlayerNetworkManager networkManager = m_player.PlayerNetworkManager;
            if (networkManager == null ||
                !networkManager.IsSpawned ||
                !networkManager.IsOwner ||
                networkManager.IsSprinting.Value == isSprinting)
            {
                return;
            }

            if (isSprinting)
            {
                networkManager.SetSneakingState(false);
            }

            networkManager.IsSprinting.Value = isSprinting;
        }
    }
}
