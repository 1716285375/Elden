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
        [SerializeField, Min(0f)] private float m_previousPoiseDamageTaken;

        protected CharacterManager Character { get; private set; }

        /// <summary>Gets or sets the attack type of the current attack action.</summary>
        public AttackType CurrentAttackType
        {
            get => m_currentAttackType;
            set => m_currentAttackType = value;
        }

        /// <summary>Gets the poise damage delivered by the most recently processed hit.</summary>
        public float PreviousPoiseDamageTaken => m_previousPoiseDamageTaken;

        protected virtual void Awake()
        {
            Character = GetComponent<CharacterManager>();
        }

        /// <summary>
        /// Records the attack type and plays its animation for local and replicated presentation.
        /// </summary>
        public void ReplicateAttack(AttackType attackType, WeaponItem weapon = null)
        {
            if (Character is PlayerManager blockingPlayer)
            {
                blockingPlayer.PlayerCombatManager?.SetBlocking(false);
            }

            Character?.CharacterNetworkManager?.SetAttackingState(true);
            CurrentAttackType = attackType;
            WeaponItem animatorWeapon = weapon;
            if (animatorWeapon == null && Character is PlayerManager player)
            {
                animatorWeapon = player.PlayerCombatManager?.CurrentWeaponBeingUsed;
            }

            Character?.CharacterAnimatorManager?.PlayTargetAttackActionAnimation(
                attackType,
                animatorWeapon);
        }

        /// <summary>Stores the latest hit intensity for follow-up combat decisions.</summary>
        public void RecordPoiseDamageTaken(float poiseDamage)
        {
            m_previousPoiseDamageTaken = Mathf.Max(0f, poiseDamage);
        }

        /// <summary>Opens the owner's finite Riposte opportunity window.</summary>
        public void EnableIsRipostable()
        {
            CharacterNetworkManager networkManager =
                Character?.CharacterNetworkManager;
            if (Character == null ||
                !Character.IsSpawned ||
                !Character.IsOwner ||
                networkManager == null)
            {
                return;
            }

            networkManager.IsRipostable.Value = true;
        }

        /// <summary>Clears transient combat windows when the action layer returns to neutral.</summary>
        public virtual void ResetActionState()
        {
            Character?.CharacterNetworkManager?.SetAttackingState(false);
            CharacterNetworkManager networkManager =
                Character?.CharacterNetworkManager;
            if (Character?.IsSpawned == true && Character.IsOwner && networkManager != null)
            {
                networkManager.IsRipostable.Value = false;
                networkManager.IsBeingCriticallyDamaged.Value = false;
            }
        }
    }
}
