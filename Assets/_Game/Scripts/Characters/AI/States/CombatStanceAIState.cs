using UnityEngine;

namespace ZZ
{
    internal sealed class CombatStanceAIState : AICharacterState
    {
        private const int k_MinimumRoll = 0;
        private const int k_MaximumRollExclusive = 100;

        private readonly AttackAIState m_attackState;

        private bool m_hasChosenPath;
        private bool m_hasRolledForBlockChance;
        private bool m_hasRolledForComboChance;
        private bool m_hasRolledForEvasionChance;
        private bool m_hasEvaded;
        private bool m_willBlockDuringThisCombatRotation;
        private bool m_willEvadeDuringThisCombatRotation;
        private float m_strafeAmount;

        internal CombatStanceAIState(AttackAIState attackState)
        {
            m_attackState = attackState;
        }

        internal override AICharacterStateId StateId =>
            AICharacterStateId.CombatStance;

        internal override void Enter(AICharacterManager character)
        {
            character.StopAtCurrentPosition();
            ChooseStrafePath(character);
            RollForBlocking(character);
            RollForCombo(character);
            RollForEvasion(character);
        }

        internal override AICharacterStateId Tick(
            AICharacterManager character,
            float deltaTime)
        {
            if (character.CurrentTarget?.IsDead == true)
            {
                character.ClearTarget();
            }

            if (!character.HasValidTarget || character.IsTargetBeyondLoseDistance)
            {
                character.ClearTarget();
                return AICharacterStateId.Idle;
            }

            if (character.ShouldResumePursuit)
            {
                return AICharacterStateId.PursueTarget;
            }

            TryPerformEvasion(character);
            if (character.IsPerformingAction)
            {
                return StateId;
            }

            if (character.WillCircleTarget && m_hasChosenPath)
            {
                character.MoveAroundTarget(
                    m_strafeAmount,
                    deltaTime,
                    character.GetPursuitMode(StateId));
            }
            else
            {
                character.StopAtCurrentPosition();
                character.FaceTarget();
            }

            return character.CanStartAttack
                ? AICharacterStateId.Attack
                : StateId;
        }

        internal override void Exit(AICharacterManager character)
        {
            character.SetBlockingState(false);
            character.SetMovementAnimationParameters(0f, 0f);
            m_hasChosenPath = false;
            m_hasRolledForBlockChance = false;
            m_hasRolledForComboChance = false;
            m_hasRolledForEvasionChance = false;
            m_hasEvaded = false;
            m_willBlockDuringThisCombatRotation = false;
            m_willEvadeDuringThisCombatRotation = false;
            m_strafeAmount = 0f;
        }

        internal static bool RollForOutcomeChance(float percentage, int roll)
        {
            return roll < Mathf.Clamp(percentage, 0f, 100f);
        }

        internal static float SelectStrafeAmount(int roll, float strafeAmount)
        {
            float amount = Mathf.Abs(strafeAmount);
            return roll >= 50 ? -amount : amount;
        }

        private void ChooseStrafePath(AICharacterManager character)
        {
            if (!character.WillCircleTarget || m_hasChosenPath)
            {
                return;
            }

            int roll = Random.Range(k_MinimumRoll, k_MaximumRollExclusive);
            m_strafeAmount = SelectStrafeAmount(
                roll,
                character.CombatStrafeAnimationAmount);
            m_hasChosenPath = true;
        }

        private void RollForBlocking(AICharacterManager character)
        {
            if (!character.CanBlock || m_hasRolledForBlockChance)
            {
                return;
            }

            m_hasRolledForBlockChance = true;
            m_willBlockDuringThisCombatRotation = RollForOutcomeChance(
                character.PercentageOfTimeWillBlock,
                Random.Range(k_MinimumRoll, k_MaximumRollExclusive));
            character.SetBlockingState(m_willBlockDuringThisCombatRotation);
        }

        private void RollForCombo(AICharacterManager character)
        {
            if (!character.CanPerformCombo || m_hasRolledForComboChance)
            {
                m_attackState.ConfigureComboDecision(false, false);
                return;
            }

            m_hasRolledForComboChance = true;
            bool willPerformCombo = RollForOutcomeChance(
                character.ChanceToPerformCombo,
                Random.Range(k_MinimumRoll, k_MaximumRollExclusive));
            m_attackState.ConfigureComboDecision(
                willPerformCombo,
                character.OnlyPerformComboIfInitialAttackHits);
        }

        private void RollForEvasion(AICharacterManager character)
        {
            if (!character.CanEvade || m_hasRolledForEvasionChance)
            {
                return;
            }

            m_hasRolledForEvasionChance = true;
            m_willEvadeDuringThisCombatRotation = RollForOutcomeChance(
                character.PercentageOfTimeWillEvade,
                Random.Range(k_MinimumRoll, k_MaximumRollExclusive));
        }

        private void TryPerformEvasion(AICharacterManager character)
        {
            if (!m_willEvadeDuringThisCombatRotation ||
                m_hasEvaded ||
                !character.IsCurrentTargetAttacking)
            {
                return;
            }

            m_hasEvaded = character.TryPerformEvasion();
        }
    }
}
