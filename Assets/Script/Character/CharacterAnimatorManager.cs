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

        private static readonly int s_horizontalParameter = Animator.StringToHash("Horizontal");
        private static readonly int s_verticalParameter = Animator.StringToHash("Vertical");
        private static readonly int s_isGroundedParameter = Animator.StringToHash("isGrounded");
        private static readonly int s_inAirTimerParameter = Animator.StringToHash("inAirTimer");
        private static readonly int s_isDeadParameter = Animator.StringToHash("isDead");
        private static readonly int s_isChargingAttackParameter =
            Animator.StringToHash("isChargingAttack");
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
        private static readonly int s_swapRightWeaponState =
            Animator.StringToHash("Upper Body Override.Swap_Right_Weapon_01");
        private static readonly int s_swapLeftWeaponState =
            Animator.StringToHash("Upper Body Override.Swap_Left_Weapon_01");
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

        [SerializeField] private Animator m_animator;

        [Header("Damage Reactions")]
        [SerializeField] private List<AnimationClip> m_hitForwardAnimations = new();
        [SerializeField] private List<AnimationClip> m_hitBackwardAnimations = new();
        [SerializeField] private List<AnimationClip> m_hitLeftAnimations = new();
        [SerializeField] private List<AnimationClip> m_hitRightAnimations = new();

        private CharacterManager m_characterManager;
        private AnimationClip m_lastDamageAnimationPlayed;

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
            bool shouldApplyRootMotion = true)
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
            if (actionLayerIndex < 0 ||
                !m_animator.HasState(actionLayerIndex, s_chargingAttackState))
            {
                Debug.LogError(
                    $"Animator {m_animator.name} does not contain Attack_Charge_01.",
                    m_animator);
                return;
            }

            m_characterManager.SetActionState(true, false, true, false);
            m_animator.CrossFade(
                s_chargingAttackState,
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
        /// Returns whether the action animation has a supported network identifier.
        /// </summary>
        internal static bool IsSupportedActionAnimation(CharacterActionAnimation targetAnimation)
        {
            return targetAnimation == CharacterActionAnimation.RollForward ||
                targetAnimation == CharacterActionAnimation.BackStep ||
                targetAnimation == CharacterActionAnimation.Death ||
                targetAnimation == CharacterActionAnimation.PassThroughFog;
        }

        private static int GetAttackStateHash(AttackType attackType)
        {
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
                default:
                    return s_lightAttack01State;
            }
        }

        private static bool IsMovingAttack(AttackType attackType)
        {
            return attackType == AttackType.RunningAttack01 ||
                attackType == AttackType.RollAttack01 ||
                attackType == AttackType.BackStepAttack01;
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
                default:
                    actionStateHash = 0;
                    return false;
            }
        }
    }
}
