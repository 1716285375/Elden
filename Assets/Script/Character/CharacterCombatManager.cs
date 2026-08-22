using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Holds shared combat state and replicates attack presentation for any character.
    /// </summary>
    [RequireComponent(typeof(CharacterManager))]
    public class CharacterCombatManager : MonoBehaviour
    {
        [SerializeField] private AttackType m_currentAttackType;

        protected CharacterManager Character { get; private set; }

        /// <summary>Gets or sets the attack type of the current attack action.</summary>
        public AttackType CurrentAttackType
        {
            get => m_currentAttackType;
            set => m_currentAttackType = value;
        }

        protected virtual void Awake()
        {
            Character = GetComponent<CharacterManager>();
        }

        /// <summary>
        /// Records the attack type and plays its animation for local and replicated presentation.
        /// </summary>
        public void ReplicateAttack(AttackType attackType)
        {
            CurrentAttackType = attackType;
            Character?.CharacterAnimatorManager?.PlayTargetAttackActionAnimation(attackType);
        }

        /// <summary>Clears transient combat windows when the action layer returns to neutral.</summary>
        public virtual void ResetActionState()
        {
        }
    }
}
