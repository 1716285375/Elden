using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    public class CharacterAnimatorManager : MonoBehaviour
    {
        private const float k_MovementParameterDampTime = 0.1f;
        private const float k_ActionTransitionDuration = 0.2f;
        private const float k_SprintVerticalValue = 2f;
        private const string k_ActionOverrideLayerName = "Action Override";
        private const string k_UpperBodyOverrideLayerName = "Upper Body Override";
        private const string k_PingDamageOverrideLayerName = "Ping Damage Override";

        private static readonly int s_horizontalParameter = Animator.StringToHash("Horizontal");
        private static readonly int s_verticalParameter = Animator.StringToHash("Vertical");
        private static readonly int s_isMovingParameter = Animator.StringToHash("isMoving");
        private static readonly int s_isGroundedParameter = Animator.StringToHash("isGrounded");
        private static readonly int s_inAirTimerParameter = Animator.StringToHash("inAirTimer");
        private static readonly int s_isDeadParameter = Animator.StringToHash("isDead");
        private static readonly int s_isChargingAttackParameter =
            Animator.StringToHash("isChargingAttack");
        private static readonly int s_isBlockingParameter =
            Animator.StringToHash("isBlocking");
        private static readonly int s_isTwoHandingWeaponParameter =
            Animator.StringToHash("isTwoHandingWeapon");
        private static readonly int s_isChargingRightSpellParameter =
            Animator.StringToHash("isChargingRightSpell");
        private static readonly int s_isChargingLeftSpellParameter =
            Animator.StringToHash("isChargingLeftSpell");
        private static readonly int s_isSpellFullyChargedParameter =
            Animator.StringToHash("isSpellFullyCharged");
        private static readonly int s_hasArrowNotchedParameter =
            Animator.StringToHash("hasArrowNotched");
        private static readonly int s_isHoldingArrowParameter =
            Animator.StringToHash("isHoldingArrow");
        private static readonly int s_isAimingParameter =
            Animator.StringToHash("isAiming");
        private static readonly int s_isChuggingFlaskParameter =
            Animator.StringToHash("isChuggingFlask");
        private static readonly int s_emptyActionState =
            Animator.StringToHash("Action Override.Empty");
        private static readonly int s_rollForwardState =
            Animator.StringToHash("Action Override.Roll_Forward_01");
        private static readonly int s_backStepState =
            Animator.StringToHash("Action Override.Back_Step_01");
        private static readonly int s_jumpStartState =
            Animator.StringToHash("Action Override.Jump Start");
        private static readonly int s_deathState =
            Animator.StringToHash("Action Override.Dead_01");
        private static readonly int s_passThroughFogState =
            Animator.StringToHash("Action Override.Pass Through Fog");
        private static readonly int s_restAtSiteOfGraceState =
            Animator.StringToHash("Action Override.Rest At Site Of Grace");
        private static readonly int s_guardBreakState =
            Animator.StringToHash("Action Override.Guard_Break_01");
        private static readonly int s_stanceBreakState =
            Animator.StringToHash("Action Override.Stance_Break_01");
        private static readonly int s_riposteState =
            Animator.StringToHash("Action Override.Riposte_01");
        private static readonly int s_ripostedState =
            Animator.StringToHash("Action Override.Riposted_01");
        private static readonly int s_backstabState =
            Animator.StringToHash("Action Override.Backstab_01");
        private static readonly int s_backstabbedState =
            Animator.StringToHash("Action Override.Backstabbed_01");
        private static readonly int s_parryFastState =
            Animator.StringToHash("Action Override.Parry_Fast_01");
        private static readonly int s_parryMediumState =
            Animator.StringToHash("Action Override.Parry_Medium_01");
        private static readonly int s_parrySlowState =
            Animator.StringToHash("Action Override.Parry_Slow_01");
        private static readonly int s_parryLandState =
            Animator.StringToHash("Action Override.Parry_Land_01");
        private static readonly int s_parriedState =
            Animator.StringToHash("Action Override.Parried_01");
        private static readonly int s_pickupItemState =
            Animator.StringToHash("Action Override.Pickup_Item_01");
        private static readonly int s_chargeSpellRightState =
            Animator.StringToHash("Action Override.Cast_Spell_Right_Charge");
        private static readonly int s_chargeSpellLeftState =
            Animator.StringToHash("Action Override.Cast_Spell_Left_Charge");
        private static readonly int s_releaseSpellRightState =
            Animator.StringToHash("Action Override.Cast_Spell_Right_Release");
        private static readonly int s_releaseSpellLeftState =
            Animator.StringToHash("Action Override.Cast_Spell_Left_Release");
        private static readonly int s_releaseFullChargeSpellRightState =
            Animator.StringToHash("Action Override.Cast_Spell_Right_Release_Full");
        private static readonly int s_releaseFullChargeSpellLeftState =
            Animator.StringToHash("Action Override.Cast_Spell_Left_Release_Full");
        private static readonly int s_bowDrawState =
            Animator.StringToHash("Action Override.Bow_Draw");
        private static readonly int s_bowOutOfAmmoState =
            Animator.StringToHash("Action Override.Bow_Out_Of_Ammo");
        private static readonly int s_swapRightWeaponState =
            Animator.StringToHash("Upper Body Override.Swap_Right_Weapon_01");
        private static readonly int s_swapLeftWeaponState =
            Animator.StringToHash("Upper Body Override.Swap_Left_Weapon_01");
        private static readonly int s_drinkStartState =
            Animator.StringToHash("Upper Body Override.Drink Start");
        private static readonly int s_emptyFlaskState =
            Animator.StringToHash("Upper Body Override.Empty Flask");
        private static readonly int s_emptyUpperBodyState =
            Animator.StringToHash("Upper Body Override.Empty");
        private static readonly int s_lightAttack01State =
            Animator.StringToHash("Action Override.Attack_01");
        private static readonly int s_lightAttack02State =
            Animator.StringToHash("Action Override.Attack_Light_02");
        private static readonly int s_lightAttack03State =
            Animator.StringToHash("Action Override.Attack_Light_03");
        private static readonly int s_heavyAttack01State =
            Animator.StringToHash("Action Override.Attack_02");
        private static readonly int s_heavyAttack02State =
            Animator.StringToHash("Action Override.Attack_Heavy_02");
        private static readonly int s_chargedAttack01State =
            Animator.StringToHash("Action Override.Attack_Charged_01");
        private static readonly int s_chargingAttackState =
            Animator.StringToHash("Action Override.Attack_Charge_01");
        private static readonly int s_runningAttack01State =
            Animator.StringToHash("Action Override.MainCore_RunAttack01");
        private static readonly int s_rollAttack01State =
            Animator.StringToHash("Action Override.MainCore_RollAttack01");
        private static readonly int s_backStepAttack01State =
            Animator.StringToHash("Action Override.MainCore_BackStepAttack01");
        private static readonly int s_lightJumpingAttack01State =
            Animator.StringToHash("Action Override.MainJumpLightAttack");
        private static readonly int s_heavyJumpingAttack01State =
            Animator.StringToHash("Action Override.MainJumpHeavyStart");
        private static readonly int s_twoHandLightAttack01State =
            Animator.StringToHash("Action Override.TwoHand_Attack_Light_01");
        private static readonly int s_twoHandLightAttack02State =
            Animator.StringToHash("Action Override.TwoHand_Attack_Light_02");
        private static readonly int s_twoHandLightAttack03State =
            Animator.StringToHash("Action Override.TwoHand_Attack_Light_03");
        private static readonly int s_twoHandHeavyAttack01State =
            Animator.StringToHash("Action Override.TwoHand_Attack_Heavy_01");
        private static readonly int s_twoHandHeavyAttack02State =
            Animator.StringToHash("Action Override.TwoHand_Attack_Heavy_02");
        private static readonly int s_twoHandChargedAttack01State =
            Animator.StringToHash("Action Override.TwoHand_Attack_Charged_01");
        private static readonly int s_twoHandChargingAttackState =
            Animator.StringToHash("Action Override.TwoHand_Attack_Charge_01");
        private static readonly int s_twoHandRunningAttack01State =
            Animator.StringToHash("Action Override.TwoHand_RunAttack01");
        private static readonly int s_twoHandRollAttack01State =
            Animator.StringToHash("Action Override.TwoHand_RollAttack01");
        private static readonly int s_twoHandBackStepAttack01State =
            Animator.StringToHash("Action Override.TwoHand_BackStepAttack01");
        private static readonly int s_twoHandLightJumpingAttack01State =
            Animator.StringToHash("Action Override.TwoHandJumpLightAttack");
        private static readonly int s_twoHandHeavyJumpingAttack01State =
            Animator.StringToHash("Action Override.TwoHandJumpHeavyStart");
        private static readonly int s_dualAttack01State =
            Animator.StringToHash("Action Override.Dual_Attack_01");
        private static readonly int s_dualAttack02State =
            Animator.StringToHash("Action Override.Dual_Attack_02");
        private static readonly int s_dualJumpAttackState =
            Animator.StringToHash("Action Override.Dual_Jump_Attack_Start");
        private static readonly int s_dualRunAttackState =
            Animator.StringToHash("Action Override.Dual_Run_Attack");
        private static readonly int s_dualRollAttackState =
            Animator.StringToHash("Action Override.Dual_Roll_Attack");
        private static readonly int s_dualBackstepAttackState =
            Animator.StringToHash("Action Override.Dual_BackStep_Attack");

        [SerializeField] private Animator m_animator;

        [Header("Damage Reactions")]
        [SerializeField] private List<AnimationClip> m_hitForwardAnimations = new();
        [SerializeField] private List<AnimationClip> m_hitBackwardAnimations = new();
        [SerializeField] private List<AnimationClip> m_hitLeftAnimations = new();
        [SerializeField] private List<AnimationClip> m_hitRightAnimations = new();

        [Header("Ping Damage Reactions")]
        [SerializeField] private List<AnimationClip> m_pingForwardAnimations = new();
        [SerializeField] private List<AnimationClip> m_pingBackwardAnimations = new();
        [SerializeField] private List<AnimationClip> m_pingLeftAnimations = new();
        [SerializeField] private List<AnimationClip> m_pingRightAnimations = new();

        private CharacterManager m_characterManager;
        private AnimationClip m_lastDamageAnimationPlayed;
        private AnimationClip m_lastPingDamageAnimationPlayed;

        protected Animator CharacterAnimator => m_animator;

        protected virtual void Awake()
        {
            if (m_animator == null)
            {
                m_animator = GetComponent<Animator>();
            }

            if (m_animator == null)
            {
                m_animator = GetComponentInChildren<Animator>(true);
            }

            m_characterManager = GetComponentInParent<CharacterManager>();
        }

        public void Initialize(Animator characterAnimator)
        {
            m_animator = characterAnimator;
            m_characterManager ??= GetComponentInParent<CharacterManager>();
        }

        /// <summary>Switches the Animator to the complete override set authored by a weapon.</summary>
        public bool UpdateAnimatorController(WeaponItem weapon)
        {
            if (m_animator == null || weapon?.WeaponAnimator == null)
            {
                Debug.LogError(
                    $"Weapon {weapon?.name ?? "None"} requires an AnimatorOverrideController.",
                    this);
                return false;
            }

            if (m_animator.runtimeAnimatorController != weapon.WeaponAnimator)
            {
                m_animator.runtimeAnimatorController = weapon.WeaponAnimator;
            }

            if (m_characterManager is PlayerManager player)
            {
                SetTwoHandingWeaponState(
                    player.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true);
                SetBlockingState(
                    player.CharacterNetworkManager?.IsBlocking.Value == true);
            }

            return true;
        }

        public void UpdateAnimatorMovementParameters(
            float horizontalValue,
            float verticalValue,
            bool isSprinting)
        {
            if (m_animator == null)
            {
                return;
            }

            float resolvedVerticalValue = isSprinting
                ? k_SprintVerticalValue
                : verticalValue;

            m_animator.SetFloat(
                s_horizontalParameter,
                horizontalValue,
                k_MovementParameterDampTime,
                Time.deltaTime);
            m_animator.SetFloat(
                s_verticalParameter,
                resolvedVerticalValue,
                k_MovementParameterDampTime,
                Time.deltaTime);
            m_animator.SetBool(
                s_isMovingParameter,
                Mathf.Abs(horizontalValue) + Mathf.Abs(verticalValue) > 0.1f);
        }

        /// <summary>
        /// Applies unsnapped locomotion values immediately for replicated AI blend trees.
        /// </summary>
        public void SetAnimatorMovementParameters(
            float horizontalValue,
            float verticalValue)
        {
            if (m_animator == null)
            {
                return;
            }

            m_animator.SetFloat(s_horizontalParameter, horizontalValue);
            m_animator.SetFloat(s_verticalParameter, verticalValue);
            m_animator.SetBool(
                s_isMovingParameter,
                Mathf.Abs(horizontalValue) + Mathf.Abs(verticalValue) > 0.1f);
        }

        /// <summary>Applies the replicated sustained block condition to the Animator.</summary>
        public void SetBlockingState(bool isBlocking)
        {
            m_animator?.SetBool(s_isBlockingParameter, isBlocking);
        }

        /// <summary>Applies the replicated two-hand locomotion and blocking condition.</summary>
        public void SetTwoHandingWeaponState(bool isTwoHandingWeapon)
        {
            m_animator?.SetBool(s_isTwoHandingWeaponParameter, isTwoHandingWeapon);
        }

        /// <summary>Applies the two independent replicated spell-charge branches.</summary>
        public void SetSpellChargingState(
            bool isChargingRightSpell,
            bool isChargingLeftSpell)
        {
            m_animator?.SetBool(
                s_isChargingRightSpellParameter,
                isChargingRightSpell);
            m_animator?.SetBool(
                s_isChargingLeftSpellParameter,
                isChargingLeftSpell);
        }

        /// <summary>Applies the replicated full-charge presentation condition.</summary>
        public void SetSpellFullyChargedState(bool isSpellFullyCharged)
        {
            m_animator?.SetBool(
                s_isSpellFullyChargedParameter,
                isSpellFullyCharged);
        }

        /// <summary>Applies replicated bow hold and aim conditions to the character Animator.</summary>
        public void SetRangedWeaponState(
            bool hasArrowNotched,
            bool isHoldingArrow,
            bool isAiming)
        {
            if (m_animator == null)
            {
                return;
            }

            m_animator.SetBool(s_hasArrowNotchedParameter, hasArrowNotched);
            m_animator.SetBool(s_isHoldingArrowParameter, isHoldingArrow);
            m_animator.SetBool(s_isAimingParameter, isAiming);
        }

        /// <summary>Applies the replicated request for another authored flask sip.</summary>
        public void SetFlaskChuggingState(bool isChugging)
        {
            m_animator?.SetBool(s_isChuggingFlaskParameter, isChugging);
        }

        /// <summary>Starts either the normal drink sequence or its empty-flask response.</summary>
        public void PlayQuickSlotItemAnimation(bool isEmpty)
        {
            PlayUpperBodyState(isEmpty ? s_emptyFlaskState : s_drinkStartState);
        }

        /// <summary>Returns the upper-body action layer to its neutral locomotion state.</summary>
        public void PlayEmptyUpperBodyAnimation()
        {
            PlayUpperBodyState(s_emptyUpperBodyState);
        }

        /// <summary>
        /// Presents the gameplay-owned ground contact and airborne duration to the Animator.
        /// </summary>
        public void UpdateAnimatorAirParameters(bool isGrounded, float inAirTimer)
        {
            if (m_animator == null)
            {
                return;
            }

            m_animator.SetBool(s_isGroundedParameter, isGrounded);
            m_animator.SetFloat(s_inAirTimerParameter, inAirTimer);
        }

        /// <summary>
        /// Starts the local jump action without extending the existing dodge RPC protocol.
        /// </summary>
        public void PlayJumpStartAnimation()
        {
            if (!CanPlayJumpStartAnimation())
            {
                Debug.LogError(
                    $"Animator {m_animator?.name} does not contain Action Override.Jump Start.",
                    m_animator);
                return;
            }

            m_characterManager.SetActionState(true, false, false, false);
            int actionLayerIndex = m_animator.GetLayerIndex(k_ActionOverrideLayerName);
            m_animator.CrossFade(
                s_jumpStartState,
                k_ActionTransitionDuration,
                actionLayerIndex);
        }

        internal bool CanPlayJumpStartAnimation()
        {
            if (m_animator == null || m_characterManager == null)
            {
                return false;
            }

            int actionLayerIndex = m_animator.GetLayerIndex(k_ActionOverrideLayerName);
            return actionLayerIndex >= 0 && m_animator.HasState(actionLayerIndex, s_jumpStartState);
        }

        /// <summary>
        /// Applies a character action state and cross-fades the action override layer to its animation.
        /// </summary>
        public void PlayTargetActionAnimation(
            CharacterActionAnimation targetAnimation,
            bool isPerformingAction,
            bool shouldApplyRootMotion = false,
            bool canRotate = false,
            bool canMove = false)
        {
            if (m_animator == null || m_characterManager == null)
            {
                return;
            }

            int actionLayerIndex = m_animator.GetLayerIndex(k_ActionOverrideLayerName);
            if (actionLayerIndex < 0)
            {
                Debug.LogError(
                    $"Animator {m_animator.name} is missing the {k_ActionOverrideLayerName} layer.",
                    m_animator);
                return;
            }

            if (!TryGetActionStateHash(targetAnimation, out int actionStateHash) ||
                !m_animator.HasState(actionLayerIndex, actionStateHash))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} does not contain action {targetAnimation}.",
                    m_animator);
                return;
            }

            m_characterManager.SetActionState(
                isPerformingAction,
                shouldApplyRootMotion,
                canRotate,
                canMove);
            if (targetAnimation == CharacterActionAnimation.Death)
            {
                m_animator.SetBool(s_isDeadParameter, true);
            }

            m_animator.CrossFade(
                actionStateHash,
                k_ActionTransitionDuration,
                actionLayerIndex);
        }

        /// <summary>
        /// Starts an attack action and cross-fades the action override layer to its attack state.
        /// </summary>
        public void PlayTargetAttackActionAnimation(
            AttackType attackType,
            WeaponItem weapon = null,
            bool shouldApplyRootMotion = true)
        {
            if (m_animator == null || m_characterManager == null)
            {
                return;
            }

            if (weapon != null && !UpdateAnimatorController(weapon))
            {
                return;
            }

            int actionLayerIndex = m_animator.GetLayerIndex(k_ActionOverrideLayerName);
            if (actionLayerIndex < 0)
            {
                Debug.LogError(
                    $"Animator {m_animator.name} is missing the {k_ActionOverrideLayerName} layer.",
                    m_animator);
                return;
            }

            int attackStateHash = GetAttackStateHash(attackType);
            if (!m_animator.HasState(actionLayerIndex, attackStateHash))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} does not contain attack {attackType}.",
                    m_animator);
                return;
            }

            const bool k_IsPerformingAction = true;
            bool canRotate = IsMovingAttack(attackType);
            const bool k_CanMove = false;
            m_characterManager.SetActionState(
                k_IsPerformingAction,
                shouldApplyRootMotion,
                canRotate,
                k_CanMove);
            m_animator.CrossFade(
                attackStateHash,
                k_ActionTransitionDuration,
                actionLayerIndex);
        }

        /// <summary>
        /// Applies an action state and immediately enters its animation without a blend.
        /// </summary>
        public void PlayTargetActionAnimationInstantly(
            CharacterActionAnimation targetAnimation,
            bool isPerformingAction,
            bool shouldApplyRootMotion = false,
            bool canRotate = false,
            bool canMove = false)
        {
            if (m_animator == null || m_characterManager == null)
            {
                return;
            }

            int actionLayerIndex = m_animator.GetLayerIndex(
                k_ActionOverrideLayerName);
            if (actionLayerIndex < 0 ||
                !TryGetActionStateHash(targetAnimation, out int actionStateHash) ||
                !m_animator.HasState(actionLayerIndex, actionStateHash))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} does not contain instant action " +
                    $"{targetAnimation}.",
                    m_animator);
                return;
            }

            m_characterManager.SetActionState(
                isPerformingAction,
                shouldApplyRootMotion,
                canRotate,
                canMove);
            m_animator.Play(actionStateHash, actionLayerIndex, 0f);
        }

        /// <summary>Updates the death branch used by Critical victim states.</summary>
        public void SetDeadState(bool isDead)
        {
            m_animator?.SetBool(s_isDeadParameter, isDead);
        }

        /// <summary>Animation Event: settles pending Critical damage on this character.</summary>
        public void ApplyCriticalDamage()
        {
            m_characterManager?.CharacterCombatManager?.ApplyCriticalDamage();
        }

        /// <summary>Animation Event: opens the owner's active Parry window.</summary>
        public void EnableIsParrying()
        {
            m_characterManager?.CharacterCombatManager?.EnableIsParrying();
        }

        /// <summary>Animation Event: closes the owner's active Parry window.</summary>
        public void DisableIsParrying()
        {
            m_characterManager?.CharacterCombatManager?.DisableIsParrying();
        }

        /// <summary>Animation Event: marks the owner's current attack as Parryable.</summary>
        public void EnableIsParryable()
        {
            m_characterManager?.CharacterCombatManager?.EnableIsParryable();
        }

        /// <summary>Animation Event: closes the owner's Parryable attack window.</summary>
        public void DisableIsParryable()
        {
            m_characterManager?.CharacterCombatManager?.DisableIsParryable();
        }

        /// <summary>Animation Event: opens the owner's finite Riposte window.</summary>
        public void EnableIsRipostable()
        {
            m_characterManager?.CharacterCombatManager?.EnableIsRipostable();
        }

        /// <summary>
        /// Updates the charge parameter and enters the authored charge pose when charging begins.
        /// </summary>
        public void SetChargingAttackState(bool isChargingAttack)
        {
            if (m_animator == null)
            {
                return;
            }

            m_animator.SetBool(s_isChargingAttackParameter, isChargingAttack);
            if (!isChargingAttack || m_characterManager == null)
            {
                return;
            }

            int actionLayerIndex = m_animator.GetLayerIndex(k_ActionOverrideLayerName);
            int chargingState = IsTwoHandingWeapon()
                ? s_twoHandChargingAttackState
                : s_chargingAttackState;
            if (actionLayerIndex < 0 ||
                !m_animator.HasState(actionLayerIndex, chargingState))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} does not contain Attack_Charge_01.",
                    m_animator);
                return;
            }

            m_characterManager.SetActionState(true, false, true, false);
            m_animator.CrossFade(
                chargingState,
                k_ActionTransitionDuration,
                actionLayerIndex);
        }

        /// <summary>
        /// Clears the death Animator condition and returns the action layer to its neutral state.
        /// </summary>
        public void PlayEmptyActionAnimation()
        {
            if (m_animator == null)
            {
                return;
            }

            int actionLayerIndex = m_animator.GetLayerIndex(k_ActionOverrideLayerName);
            if (actionLayerIndex < 0 || !m_animator.HasState(actionLayerIndex, s_emptyActionState))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} does not contain Action Override.Empty.",
                    m_animator);
                return;
            }

            m_animator.SetBool(s_isDeadParameter, false);
            m_animator.CrossFade(
                s_emptyActionState,
                k_ActionTransitionDuration,
                actionLayerIndex);
        }

        /// <summary>
        /// Plays a hand-specific swap on the upper-body layer without changing movement flags.
        /// </summary>
        public void PlayWeaponSwapAnimation(WeaponModelSlot weaponSlot)
        {
            if (m_animator == null)
            {
                return;
            }

            int layerIndex = m_animator.GetLayerIndex(k_UpperBodyOverrideLayerName);
            int stateHash = weaponSlot == WeaponModelSlot.RightHandSlot
                ? s_swapRightWeaponState
                : s_swapLeftWeaponState;
            if (layerIndex < 0 || !m_animator.HasState(layerIndex, stateHash))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} is missing the {weaponSlot} swap state.",
                    m_animator);
                return;
            }

            m_animator.CrossFade(stateHash, k_ActionTransitionDuration, layerIndex);
        }

        /// <summary>
        /// Plays a random reaction for the side that received damage.
        /// </summary>
        /// <returns>The selected reaction clip, or null when no valid reaction is available.</returns>
        public AnimationClip PlayDirectionalDamageAnimation(DamageDirection damageDirection)
        {
            if (m_animator == null || m_characterManager == null)
            {
                return null;
            }

            int actionLayerIndex = m_animator.GetLayerIndex(k_ActionOverrideLayerName);
            if (actionLayerIndex < 0)
            {
                Debug.LogError(
                    $"Animator {m_animator.name} is missing the {k_ActionOverrideLayerName} layer.",
                    m_animator);
                return null;
            }

            AnimationClip damageAnimation = GetRandomDamageAnimation(
                GetDamageAnimations(damageDirection));
            if (damageAnimation == null)
            {
                Debug.LogError(
                    $"Animator {name} has no {damageDirection} damage reaction animations.",
                    this);
                return null;
            }

            int damageStateHash = Animator.StringToHash(
                $"{k_ActionOverrideLayerName}.{damageAnimation.name}");
            if (!m_animator.HasState(actionLayerIndex, damageStateHash))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} does not contain damage state " +
                    $"{damageAnimation.name}.",
                    m_animator);
                return null;
            }

            m_lastDamageAnimationPlayed = damageAnimation;
            m_characterManager.SetActionState(true, false, false, false);
            m_animator.CrossFade(
                damageStateHash,
                k_ActionTransitionDuration,
                actionLayerIndex);
            return damageAnimation;
        }

        /// <summary>
        /// Plays a directional upper-body flinch without changing gameplay action flags.
        /// </summary>
        /// <returns>The selected Ping clip, or null when the pool or layer is invalid.</returns>
        public AnimationClip PlayDirectionalPingDamageAnimation(
            DamageDirection damageDirection)
        {
            if (m_animator == null)
            {
                return null;
            }

            int pingLayerIndex = m_animator.GetLayerIndex(k_PingDamageOverrideLayerName);
            if (pingLayerIndex < 0)
            {
                Debug.LogError(
                    $"Animator {m_animator.name} is missing the " +
                    $"{k_PingDamageOverrideLayerName} layer.",
                    m_animator);
                return null;
            }

            AnimationClip pingAnimation = GetRandomPingDamageAnimation(
                GetPingDamageAnimations(damageDirection));
            if (pingAnimation == null)
            {
                Debug.LogError(
                    $"Animator {name} has no {damageDirection} Ping animations.",
                    this);
                return null;
            }

            int pingStateHash = Animator.StringToHash(
                $"{k_PingDamageOverrideLayerName}.{pingAnimation.name}");
            if (!m_animator.HasState(pingLayerIndex, pingStateHash))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} does not contain Ping state " +
                    $"{pingAnimation.name}.",
                    m_animator);
                return null;
            }

            m_lastPingDamageAnimationPlayed = pingAnimation;
            m_animator.CrossFade(
                pingStateHash,
                k_ActionTransitionDuration,
                pingLayerIndex);
            return pingAnimation;
        }

        /// <summary>
        /// Returns whether the action animation has a supported network identifier.
        /// </summary>
        internal static bool IsSupportedActionAnimation(CharacterActionAnimation targetAnimation)
        {
            return targetAnimation == CharacterActionAnimation.RollForward ||
                targetAnimation == CharacterActionAnimation.BackStep ||
                targetAnimation == CharacterActionAnimation.Death ||
                targetAnimation == CharacterActionAnimation.PassThroughFog ||
                targetAnimation == CharacterActionAnimation.RestAtSiteOfGrace ||
                targetAnimation == CharacterActionAnimation.GuardBreak ||
                targetAnimation == CharacterActionAnimation.StanceBreak ||
                targetAnimation == CharacterActionAnimation.Riposte ||
                targetAnimation == CharacterActionAnimation.Riposted ||
                targetAnimation == CharacterActionAnimation.Backstab ||
                targetAnimation == CharacterActionAnimation.Backstabbed ||
                targetAnimation == CharacterActionAnimation.ParryFast ||
                targetAnimation == CharacterActionAnimation.ParryMedium ||
                targetAnimation == CharacterActionAnimation.ParrySlow ||
                targetAnimation == CharacterActionAnimation.ParryLand ||
                targetAnimation == CharacterActionAnimation.Parried ||
                targetAnimation == CharacterActionAnimation.PickupItem ||
                targetAnimation == CharacterActionAnimation.ChargeSpellRight ||
                targetAnimation == CharacterActionAnimation.ChargeSpellLeft ||
                targetAnimation == CharacterActionAnimation.ReleaseSpellRight ||
                targetAnimation == CharacterActionAnimation.ReleaseSpellLeft ||
                targetAnimation ==
                    CharacterActionAnimation.ReleaseFullChargeSpellRight ||
                targetAnimation ==
                    CharacterActionAnimation.ReleaseFullChargeSpellLeft;
        }

        /// <summary>Returns whether an action is approved for zero-blend network playback.</summary>
        internal static bool IsSupportedInstantActionAnimation(
            CharacterActionAnimation targetAnimation)
        {
            return targetAnimation == CharacterActionAnimation.StanceBreak ||
                targetAnimation == CharacterActionAnimation.Riposte ||
                targetAnimation == CharacterActionAnimation.Riposted ||
                targetAnimation == CharacterActionAnimation.Backstab ||
                targetAnimation == CharacterActionAnimation.Backstabbed ||
                targetAnimation == CharacterActionAnimation.ParryLand ||
                targetAnimation == CharacterActionAnimation.Parried;
        }

        private void PlayUpperBodyState(int stateHash)
        {
            if (m_animator == null)
            {
                return;
            }

            int layerIndex = m_animator.GetLayerIndex(k_UpperBodyOverrideLayerName);
            if (layerIndex < 0 || !m_animator.HasState(layerIndex, stateHash))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} is missing a flask upper-body state.",
                    m_animator);
                return;
            }

            m_animator.CrossFade(stateHash, k_ActionTransitionDuration, layerIndex);
        }

        private int GetAttackStateHash(AttackType attackType)
        {
            if (IsTwoHandingWeapon())
            {
                return GetTwoHandAttackStateHash(attackType);
            }

            switch (attackType)
            {
                case AttackType.LightAttack02:
                    return s_lightAttack02State;
                case AttackType.LightAttack03:
                    return s_lightAttack03State;
                case AttackType.HeavyAttack01:
                    return s_heavyAttack01State;
                case AttackType.HeavyAttack02:
                    return s_heavyAttack02State;
                case AttackType.ChargedAttack01:
                    return s_chargedAttack01State;
                case AttackType.RunningAttack01:
                    return s_runningAttack01State;
                case AttackType.RollAttack01:
                    return s_rollAttack01State;
                case AttackType.BackStepAttack01:
                    return s_backStepAttack01State;
                case AttackType.LightJumpingAttack01:
                    return s_lightJumpingAttack01State;
                case AttackType.HeavyJumpingAttack01:
                    return s_heavyJumpingAttack01State;
                case AttackType.DualAttack01:
                    return s_dualAttack01State;
                case AttackType.DualAttack02:
                    return s_dualAttack02State;
                case AttackType.DualJumpAttack:
                    return s_dualJumpAttackState;
                case AttackType.DualRunAttack:
                    return s_dualRunAttackState;
                case AttackType.DualRollAttack:
                    return s_dualRollAttackState;
                case AttackType.DualBackstepAttack:
                    return s_dualBackstepAttackState;
                default:
                    return s_lightAttack01State;
            }
        }

        private static int GetTwoHandAttackStateHash(AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.LightAttack02:
                    return s_twoHandLightAttack02State;
                case AttackType.LightAttack03:
                    return s_twoHandLightAttack03State;
                case AttackType.HeavyAttack01:
                    return s_twoHandHeavyAttack01State;
                case AttackType.HeavyAttack02:
                    return s_twoHandHeavyAttack02State;
                case AttackType.ChargedAttack01:
                    return s_twoHandChargedAttack01State;
                case AttackType.RunningAttack01:
                    return s_twoHandRunningAttack01State;
                case AttackType.RollAttack01:
                    return s_twoHandRollAttack01State;
                case AttackType.BackStepAttack01:
                    return s_twoHandBackStepAttack01State;
                case AttackType.LightJumpingAttack01:
                    return s_twoHandLightJumpingAttack01State;
                case AttackType.HeavyJumpingAttack01:
                    return s_twoHandHeavyJumpingAttack01State;
                default:
                    return s_twoHandLightAttack01State;
            }
        }

        private bool IsTwoHandingWeapon()
        {
            return m_characterManager is PlayerManager player &&
                player.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true;
        }

        private static bool IsMovingAttack(AttackType attackType)
        {
            return attackType == AttackType.RunningAttack01 ||
                attackType == AttackType.RollAttack01 ||
                attackType == AttackType.BackStepAttack01 ||
                attackType == AttackType.DualRunAttack ||
                attackType == AttackType.DualRollAttack ||
                attackType == AttackType.DualBackstepAttack;
        }

        private IReadOnlyList<AnimationClip> GetDamageAnimations(
            DamageDirection damageDirection)
        {
            switch (damageDirection)
            {
                case DamageDirection.Front:
                    return m_hitForwardAnimations;
                case DamageDirection.Back:
                    return m_hitBackwardAnimations;
                case DamageDirection.Left:
                    return m_hitLeftAnimations;
                case DamageDirection.Right:
                    return m_hitRightAnimations;
                default:
                    return m_hitForwardAnimations;
            }
        }

        private IReadOnlyList<AnimationClip> GetPingDamageAnimations(
            DamageDirection damageDirection)
        {
            switch (damageDirection)
            {
                case DamageDirection.Front:
                    return m_pingForwardAnimations;
                case DamageDirection.Back:
                    return m_pingBackwardAnimations;
                case DamageDirection.Left:
                    return m_pingLeftAnimations;
                case DamageDirection.Right:
                    return m_pingRightAnimations;
                default:
                    return m_pingForwardAnimations;
            }
        }

        private AnimationClip GetRandomDamageAnimation(
            IReadOnlyList<AnimationClip> damageAnimations)
        {
            if (damageAnimations == null || damageAnimations.Count == 0)
            {
                return null;
            }

            List<AnimationClip> candidates = new List<AnimationClip>(
                damageAnimations.Count);
            for (int animationIndex = 0;
                animationIndex < damageAnimations.Count;
                animationIndex++)
            {
                AnimationClip damageAnimation = damageAnimations[animationIndex];
                if (damageAnimation != null)
                {
                    candidates.Add(damageAnimation);
                }
            }

            if (candidates.Count > 1)
            {
                candidates.Remove(m_lastDamageAnimationPlayed);
            }

            return candidates.Count > 0
                ? candidates[Random.Range(0, candidates.Count)]
                : null;
        }

        private AnimationClip GetRandomPingDamageAnimation(
            IReadOnlyList<AnimationClip> damageAnimations)
        {
            if (damageAnimations == null || damageAnimations.Count == 0)
            {
                return null;
            }

            List<AnimationClip> candidates = new List<AnimationClip>(
                damageAnimations.Count);
            for (int animationIndex = 0;
                animationIndex < damageAnimations.Count;
                animationIndex++)
            {
                AnimationClip damageAnimation = damageAnimations[animationIndex];
                if (damageAnimation != null)
                {
                    candidates.Add(damageAnimation);
                }
            }

            if (candidates.Count > 1)
            {
                candidates.Remove(m_lastPingDamageAnimationPlayed);
            }

            return candidates.Count > 0
                ? candidates[Random.Range(0, candidates.Count)]
                : null;
        }

        private static bool TryGetActionStateHash(
            CharacterActionAnimation targetAnimation,
            out int actionStateHash)
        {
            switch (targetAnimation)
            {
                case CharacterActionAnimation.RollForward:
                    actionStateHash = s_rollForwardState;
                    return true;
                case CharacterActionAnimation.BackStep:
                    actionStateHash = s_backStepState;
                    return true;
                case CharacterActionAnimation.Death:
                    actionStateHash = s_deathState;
                    return true;
                case CharacterActionAnimation.PassThroughFog:
                    actionStateHash = s_passThroughFogState;
                    return true;
                case CharacterActionAnimation.RestAtSiteOfGrace:
                    actionStateHash = s_restAtSiteOfGraceState;
                    return true;
                case CharacterActionAnimation.GuardBreak:
                    actionStateHash = s_guardBreakState;
                    return true;
                case CharacterActionAnimation.StanceBreak:
                    actionStateHash = s_stanceBreakState;
                    return true;
                case CharacterActionAnimation.Riposte:
                    actionStateHash = s_riposteState;
                    return true;
                case CharacterActionAnimation.Riposted:
                    actionStateHash = s_ripostedState;
                    return true;
                case CharacterActionAnimation.Backstab:
                    actionStateHash = s_backstabState;
                    return true;
                case CharacterActionAnimation.Backstabbed:
                    actionStateHash = s_backstabbedState;
                    return true;
                case CharacterActionAnimation.ParryFast:
                    actionStateHash = s_parryFastState;
                    return true;
                case CharacterActionAnimation.ParryMedium:
                    actionStateHash = s_parryMediumState;
                    return true;
                case CharacterActionAnimation.ParrySlow:
                    actionStateHash = s_parrySlowState;
                    return true;
                case CharacterActionAnimation.ParryLand:
                    actionStateHash = s_parryLandState;
                    return true;
                case CharacterActionAnimation.Parried:
                    actionStateHash = s_parriedState;
                    return true;
                case CharacterActionAnimation.PickupItem:
                    actionStateHash = s_pickupItemState;
                    return true;
                case CharacterActionAnimation.ChargeSpellRight:
                    actionStateHash = s_chargeSpellRightState;
                    return true;
                case CharacterActionAnimation.ChargeSpellLeft:
                    actionStateHash = s_chargeSpellLeftState;
                    return true;
                case CharacterActionAnimation.ReleaseSpellRight:
                    actionStateHash = s_releaseSpellRightState;
                    return true;
                case CharacterActionAnimation.ReleaseSpellLeft:
                    actionStateHash = s_releaseSpellLeftState;
                    return true;
                case CharacterActionAnimation.ReleaseFullChargeSpellRight:
                    actionStateHash = s_releaseFullChargeSpellRightState;
                    return true;
                case CharacterActionAnimation.ReleaseFullChargeSpellLeft:
                    actionStateHash = s_releaseFullChargeSpellLeftState;
                    return true;
                case CharacterActionAnimation.BowDraw:
                    actionStateHash = s_bowDrawState;
                    return true;
                case CharacterActionAnimation.BowOutOfAmmo:
                    actionStateHash = s_bowOutOfAmmoState;
                    return true;
                default:
                    actionStateHash = 0;
                    return false;
            }
        }
    }
}
