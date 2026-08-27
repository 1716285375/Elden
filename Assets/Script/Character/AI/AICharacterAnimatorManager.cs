using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Receives authored undead animation events and forwards them to AI gameplay state.
    /// </summary>
    public class AICharacterAnimatorManager : CharacterAnimatorManager
    {
        private const string k_ActionLayerName = "Action Override";

        private static readonly int s_pivotLeftTrigger =
            Animator.StringToHash("PivotLeft");
        private static readonly int s_pivotRightTrigger =
            Animator.StringToHash("PivotRight");
        private static readonly int s_bossPhaseTransitionState =
            Animator.StringToHash("Action Override.Boss_Phase_Transition");
        private static readonly int s_emptyActionState =
            Animator.StringToHash("Action Override.Empty");

        private AICharacterManager m_aiCharacter;
        private AICharacterCombatManager m_combatManager;

        protected override void Awake()
        {
            base.Awake();
            m_aiCharacter = GetComponentInParent<AICharacterManager>();
            m_combatManager = GetComponentInParent<AICharacterCombatManager>();
            if (CharacterAnimator != null)
            {
                CharacterAnimator.keepAnimatorStateOnDisable = true;
            }
        }

        /// <summary>Immediately presents the configured persistent sleeping state.</summary>
        public void PlaySleepingAnimation(string stateName)
        {
            PlayIdleBehaviorAnimation(stateName, 0f);
        }

        /// <summary>Blends through the configured waking state before locomotion resumes.</summary>
        public void PlayWakingAnimation(string stateName)
        {
            PlayIdleBehaviorAnimation(stateName, 0.1f);
        }

        /// <summary>Skips a consumed awakening cinematic and resumes locomotion immediately.</summary>
        public void PlayAwakeIdleAnimation()
        {
            if (CharacterAnimator == null || m_aiCharacter == null)
            {
                return;
            }

            int actionLayerIndex = CharacterAnimator.GetLayerIndex(
                k_ActionLayerName);
            if (actionLayerIndex >= 0 &&
                CharacterAnimator.HasState(actionLayerIndex, s_emptyActionState))
            {
                CharacterAnimator.CrossFade(
                    s_emptyActionState,
                    0.1f,
                    actionLayerIndex);
            }

            m_aiCharacter.ResetActionFlags();
        }

        /// <summary>Triggers a locally presented pivot selected by the server.</summary>
        public void PlayPivotTurn(bool turnLeft)
        {
            if (CharacterAnimator == null)
            {
                return;
            }

            CharacterAnimator.ResetTrigger(
                turnLeft ? s_pivotRightTrigger : s_pivotLeftTrigger);
            CharacterAnimator.SetTrigger(
                turnLeft ? s_pivotLeftTrigger : s_pivotRightTrigger);
        }

        /// <summary>Plays the authored phase transition while pausing movement and attacks.</summary>
        public void PlayBossPhaseTransition()
        {
            if (CharacterAnimator == null || m_aiCharacter == null)
            {
                return;
            }

            int actionLayerIndex = CharacterAnimator.GetLayerIndex("Action Override");
            if (actionLayerIndex < 0 ||
                !CharacterAnimator.HasState(actionLayerIndex, s_bossPhaseTransitionState))
            {
                return;
            }

            m_aiCharacter.SetActionState(true, false, false, false);
            CharacterAnimator.CrossFade(
                s_bossPhaseTransitionState,
                0.2f,
                actionLayerIndex);
        }

        /// <summary>Refreshes the current swipe attack's damage payload.</summary>
        public void SetSwipeAttackDamage()
        {
            m_combatManager?.PrepareAttackDamage();
        }

        /// <summary>Opens the server-authoritative left-hand hit window.</summary>
        public void OpenLeftHandDamageCollider()
        {
            m_combatManager?.OpenLeftHandDamageCollider();
        }

        /// <summary>Closes the left-hand hit window.</summary>
        public void CloseLeftHandDamageCollider()
        {
            m_combatManager?.CloseLeftHandDamageCollider();
        }

        /// <summary>Opens the server-authoritative right-hand hit window.</summary>
        public void OpenRightHandDamageCollider()
        {
            m_combatManager?.OpenRightHandDamageCollider();
        }

        /// <summary>Closes every hit window at the end of the right-hand swipe.</summary>
        public void CloseRightHandDamageCollider()
        {
            m_combatManager?.CloseDamageColliders();
        }

        /// <summary>Allows the AI state machine to rotate during authored frames.</summary>
        public void EnableCanRotate()
        {
            m_aiCharacter?.SetCanRotate(true);
        }

        /// <summary>Prevents the AI state machine from rotating during authored frames.</summary>
        public void DisableCanRotate()
        {
            m_aiCharacter?.SetCanRotate(false);
        }

        /// <summary>Animation Event: plays the replicated Stance Break impact.</summary>
        public void PlayStanceBrokenSoundEffect()
        {
            m_aiCharacter?.CharacterSoundFXManager
                ?.PlayStanceBrokenSoundEffect();
        }

        /// <summary>Animation Event: opens the server-authoritative AI combo window.</summary>
        public void EnableCanDoCombo()
        {
            m_combatManager?.EnableCanDoCombo();
        }

        /// <summary>Animation Event: closes the AI combo window and clears hit confirmation.</summary>
        public void DisableCanDoCombo()
        {
            m_combatManager?.DisableCanDoCombo();
        }

        private void PlayIdleBehaviorAnimation(
            string stateName,
            float transitionDuration)
        {
            if (CharacterAnimator == null ||
                m_aiCharacter == null ||
                string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            int actionLayerIndex = CharacterAnimator.GetLayerIndex(
                k_ActionLayerName);
            int stateHash = Animator.StringToHash(
                $"{k_ActionLayerName}.{stateName}");
            if (actionLayerIndex < 0 ||
                !CharacterAnimator.HasState(actionLayerIndex, stateHash))
            {
                Debug.LogWarning(
                    $"Animator {CharacterAnimator.name} is missing AI state {stateName}.",
                    CharacterAnimator);
                return;
            }

            m_aiCharacter.SetActionState(true, false, false, false);
            CharacterAnimator.CrossFade(
                stateHash,
                transitionDuration,
                actionLayerIndex);
        }
    }
}
