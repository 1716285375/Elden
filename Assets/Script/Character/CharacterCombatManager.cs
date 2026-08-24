using System.Collections;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Holds shared combat state and replicates attack presentation for any character.
    /// </summary>
    [RequireComponent(typeof(CharacterManager))]
    public class CharacterCombatManager : MonoBehaviour
    {
        private const float k_RiposteAlignmentDuration = 0.5f;
        private const float k_BackstabAlignmentDuration = 0.15f;
        private const float k_MinimumBackstabAngle = 145f;

        [SerializeField] private AttackType m_currentAttackType;
        [SerializeField, Min(0f)] private float m_previousPoiseDamageTaken;

        [Header("Critical Attacks")]
        [SerializeField, Min(0f)] private float m_criticalAttackDistanceCheck = 0.7f;
        [SerializeField, Range(0f, 180f)] private float m_criticalAttackHalfAngle = 60f;
        [SerializeField] private LayerMask m_characterLayers = 1 << 10;
        [SerializeField, Min(0)] private int m_pendingCriticalDamage;
        [SerializeField] private bool m_canBeBackstabbed = true;

        private Transform m_riposteReceiverTransform;
        private Transform m_backstabReceiverTransform;
        private CharacterManager m_pendingCriticalDamageSource;

        protected CharacterManager Character { get; private set; }

        /// <summary>Gets or sets the attack type of the current attack action.</summary>
        public AttackType CurrentAttackType
        {
            get => m_currentAttackType;
            set => m_currentAttackType = value;
        }

        /// <summary>Gets the poise damage delivered by the most recently processed hit.</summary>
        public float PreviousPoiseDamageTaken => m_previousPoiseDamageTaken;

        /// <summary>Gets damage waiting for the critical animation's authored hit frame.</summary>
        public int PendingCriticalDamage => m_pendingCriticalDamage;

        /// <summary>Gets whether this character accepts a Critical Attack from behind.</summary>
        public bool CanBeBackstabbed => m_canBeBackstabbed;

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

        /// <summary>Animation Event: opens this owner's active Parry frames.</summary>
        public void EnableIsParrying()
        {
            Character?.CharacterNetworkManager?.SetParryingState(true);
        }

        /// <summary>Animation Event: closes this owner's active Parry frames.</summary>
        public void DisableIsParrying()
        {
            Character?.CharacterNetworkManager?.SetParryingState(false);
        }

        /// <summary>Animation Event: marks the owner's current attack as Parryable.</summary>
        public void EnableIsParryable()
        {
            Character?.CharacterNetworkManager?.SetParryableState(true);
        }

        /// <summary>Animation Event: closes the owner's Parryable attack frames.</summary>
        public void DisableIsParryable()
        {
            Character?.CharacterNetworkManager?.SetParryableState(false);
        }

        /// <summary>
        /// Searches directly in front of the owner, preferring Riposte over Backstab.
        /// </summary>
        public virtual bool AttemptCriticalAttack()
        {
            CharacterNetworkManager networkManager =
                Character?.CharacterNetworkManager;
            if (Character == null ||
                !Character.IsOwner ||
                Character.IsPerformingAction ||
                networkManager == null ||
                networkManager.CurrentStamina.Value <= 0f)
            {
                return false;
            }

            Transform rayOrigin = Character.LockOnTransform;
            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin.position,
                Character.transform.forward,
                m_criticalAttackDistanceCheck,
                m_characterLayers,
                QueryTriggerInteraction.Collide);
            CharacterManager nearestRiposteTarget = null;
            float nearestRiposteDistance = float.PositiveInfinity;
            RaycastHit nearestBackstabHit = default;
            bool hasBackstabHit = false;
            float nearestBackstabDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                CharacterManager candidate =
                    hit.collider.GetComponentInParent<CharacterManager>();
                CharacterNetworkManager candidateNetworkManager =
                    candidate?.CharacterNetworkManager;
                CharacterCombatManager candidateCombatManager =
                    candidate?.CharacterCombatManager;
                Vector3 directionToCandidate = candidate != null
                    ? candidate.transform.position - Character.transform.position
                    : Vector3.zero;
                Vector3 directionToAttacker = -directionToCandidate;
                if (!WorldUtilityManager.CanDamageCharacter(
                        Character,
                        candidate) ||
                    candidateNetworkManager == null ||
                    candidateNetworkManager.IsBeingCriticallyDamaged.Value ||
                    !IsWithinCriticalAttackAngle(
                        Character.transform.forward,
                        directionToCandidate,
                        m_criticalAttackHalfAngle))
                {
                    continue;
                }

                bool isValidRiposte =
                    candidateNetworkManager.IsRipostable.Value &&
                    IsWithinCriticalAttackAngle(
                        candidate.transform.forward,
                        directionToAttacker,
                        m_criticalAttackHalfAngle);
                if (isValidRiposte && hit.distance < nearestRiposteDistance)
                {
                    nearestRiposteTarget = candidate;
                    nearestRiposteDistance = hit.distance;
                }

                bool isValidBackstab =
                    candidateCombatManager?.CanBeBackstabbed == true &&
                    IsWithinBackstabAngle(
                        candidate.transform.forward,
                        directionToAttacker,
                        k_MinimumBackstabAngle);
                if (isValidBackstab && hit.distance < nearestBackstabDistance)
                {
                    nearestBackstabHit = hit;
                    hasBackstabHit = true;
                    nearestBackstabDistance = hit.distance;
                }
            }

            if (nearestRiposteTarget != null &&
                AttemptRiposte(nearestRiposteTarget))
            {
                return true;
            }

            return hasBackstabHit && AttemptBackstab(nearestBackstabHit);
        }

        /// <summary>Allows character-specific equipment logic to start a Riposte.</summary>
        public virtual bool AttemptRiposte(CharacterManager targetCharacter)
        {
            return false;
        }

        /// <summary>Allows character-specific equipment logic to start a Backstab.</summary>
        public virtual bool AttemptBackstab(RaycastHit hit)
        {
            return false;
        }

        /// <summary>Changes whether this character accepts Backstab attempts.</summary>
        public void SetCanBeBackstabbed(bool canBeBackstabbed)
        {
            m_canBeBackstabbed = canBeBackstabbed;
        }

        /// <summary>Stores resolved Critical damage until its animation event.</summary>
        public void SetPendingCriticalDamage(
            int pendingCriticalDamage,
            CharacterManager damageSource)
        {
            m_pendingCriticalDamage = Mathf.Max(0, pendingCriticalDamage);
            m_pendingCriticalDamageSource = damageSource;
        }

        /// <summary>
        /// Presents and applies pending Critical damage once at the authored hit frame.
        /// </summary>
        public void ApplyCriticalDamage()
        {
            if (Character == null || m_pendingCriticalDamage <= 0)
            {
                return;
            }

            int damage = m_pendingCriticalDamage;
            m_pendingCriticalDamage = 0;
            Vector3 hitDirection = m_pendingCriticalDamageSource != null
                ? (Character.transform.position -
                    m_pendingCriticalDamageSource.transform.position).normalized
                : transform.forward;
            m_pendingCriticalDamageSource = null;
            Character.CharacterEffectsManager?.PlayCriticalBloodSplatterVFX(
                Character.LockOnTransform.position,
                hitDirection);
            Character.CharacterSoundFXManager
                ?.PlayCriticalStrikeSoundEffect();
            if (!Character.IsOwner)
            {
                return;
            }

            CharacterNetworkManager networkManager =
                Character.CharacterNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            float maximumHealth = Mathf.Max(0f, networkManager.MaxHealth.Value);
            float currentHealth = Mathf.Clamp(
                networkManager.CurrentHealth.Value,
                0f,
                maximumHealth);
            networkManager.CurrentHealth.Value = Mathf.Max(
                0f,
                currentHealth - damage);
        }

        /// <summary>
        /// Resolves the replicated Critical payload and starts both synchronized animations.
        /// </summary>
        public void ProcessRiposteFromServer(
            CharacterManager targetCharacter,
            MeleeWeaponItem riposteWeapon,
            CharacterActionAnimation criticalDamageAnimation,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage)
        {
            if (!PrepareCriticalAttack(
                    targetCharacter,
                    riposteWeapon,
                    CharacterActionAnimation.Riposte,
                    criticalDamageAnimation,
                    physicalDamage,
                    magicDamage,
                    fireDamage,
                    lightningDamage,
                    holyDamage,
                    poiseDamage))
            {
                return;
            }

            if (Character.IsOwner)
            {
                StartCoroutine(
                    ForceMoveEnemyCharacterToRipostePosition(
                        targetCharacter,
                        riposteWeapon.WeaponClass));
            }
        }

        /// <summary>Starts the replicated Backstab pair and target-owner alignment.</summary>
        public void ProcessBackstabFromServer(
            CharacterManager targetCharacter,
            MeleeWeaponItem backstabWeapon,
            CharacterActionAnimation criticalDamageAnimation,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage)
        {
            if (!PrepareCriticalAttack(
                    targetCharacter,
                    backstabWeapon,
                    CharacterActionAnimation.Backstab,
                    criticalDamageAnimation,
                    physicalDamage,
                    magicDamage,
                    fireDamage,
                    lightningDamage,
                    holyDamage,
                    poiseDamage))
            {
                return;
            }

            if (targetCharacter.IsOwner)
            {
                StartCoroutine(ForceMoveCharacterToBackstabPosition(
                    targetCharacter,
                    backstabWeapon.WeaponClass));
            }
        }

        /// <summary>
        /// Interrupts this character's attack and presents both sides of a validated Parry.
        /// </summary>
        public void ProcessParryFromServer(CharacterManager parryingCharacter)
        {
            if (Character == null || parryingCharacter == null)
            {
                return;
            }

            CloseAllDamageColliders();
            if (Character is AICharacterManager aiCharacter)
            {
                aiCharacter.StopMoving();
            }

            if (Character.IsOwner)
            {
                CharacterNetworkManager networkManager =
                    Character.CharacterNetworkManager;
                networkManager?.SetAttackingState(false);
                networkManager?.SetParryableState(false);
                if (networkManager != null && networkManager.IsSpawned)
                {
                    networkManager.IsRipostable.Value = false;
                }
            }

            if (parryingCharacter.IsOwner)
            {
                parryingCharacter.CharacterNetworkManager
                    ?.SetParryingState(false);
            }

            parryingCharacter.CharacterAnimatorManager
                ?.PlayTargetActionAnimationInstantly(
                    CharacterActionAnimation.ParryLand,
                    true);
            Character.CharacterAnimatorManager
                ?.PlayTargetActionAnimationInstantly(
                    CharacterActionAnimation.Parried,
                    true);
        }

        /// <summary>
        /// Closes every attack collider owned by this character type.
        /// </summary>
        public virtual void CloseAllDamageColliders()
        {
        }

        /// <summary>Moves the attacking owner into the target-authored receiver pose.</summary>
        public IEnumerator ForceMoveEnemyCharacterToRipostePosition(
            CharacterManager targetCharacter,
            WeaponClass weaponClass)
        {
            if (Character == null || targetCharacter == null)
            {
                yield break;
            }

            Transform receiver = targetCharacter.CharacterCombatManager
                ?.GetRiposteReceiverTransform(weaponClass);
            if (receiver == null)
            {
                yield break;
            }

            Vector3 startingPosition = Character.transform.position;
            Quaternion startingRotation = Character.transform.rotation;
            float elapsedTime = 0f;
            while (elapsedTime < k_RiposteAlignmentDuration &&
                Character != null &&
                targetCharacter != null)
            {
                elapsedTime += Time.deltaTime;
                float interpolation = Mathf.Clamp01(
                    elapsedTime / k_RiposteAlignmentDuration);
                Quaternion targetRotation = Quaternion.LookRotation(
                    -targetCharacter.transform.forward,
                    Vector3.up);
                Character.transform.SetPositionAndRotation(
                    Vector3.Lerp(
                        startingPosition,
                        receiver.position,
                        interpolation),
                    Quaternion.Slerp(
                        startingRotation,
                        targetRotation,
                        interpolation));
                yield return null;
            }

            if (Character != null && targetCharacter != null)
            {
                Character.transform.SetPositionAndRotation(
                    receiver.position,
                    Quaternion.LookRotation(
                        -targetCharacter.transform.forward,
                        Vector3.up));
            }
        }

        /// <summary>Moves the target owner into the attacker's Backstab receiver pose.</summary>
        public IEnumerator ForceMoveCharacterToBackstabPosition(
            CharacterManager targetCharacter,
            WeaponClass weaponClass)
        {
            if (Character == null || targetCharacter == null)
            {
                yield break;
            }

            Transform receiver = GetBackstabReceiverTransform(weaponClass);
            Vector3 startingPosition = targetCharacter.transform.position;
            Quaternion startingRotation = targetCharacter.transform.rotation;
            float elapsedTime = 0f;
            while (elapsedTime < k_BackstabAlignmentDuration &&
                Character != null &&
                targetCharacter != null)
            {
                elapsedTime += Time.deltaTime;
                float interpolation = Mathf.Clamp01(
                    elapsedTime / k_BackstabAlignmentDuration);
                Quaternion targetRotation = Quaternion.LookRotation(
                    Character.transform.forward,
                    Vector3.up);
                targetCharacter.transform.SetPositionAndRotation(
                    Vector3.Lerp(
                        startingPosition,
                        receiver.position,
                        interpolation),
                    Quaternion.Slerp(
                        startingRotation,
                        targetRotation,
                        interpolation));
                yield return null;
            }

            if (Character != null && targetCharacter != null)
            {
                targetCharacter.transform.SetPositionAndRotation(
                    receiver.position,
                    Quaternion.LookRotation(
                        Character.transform.forward,
                        Vector3.up));
            }
        }

        /// <summary>Tests a horizontal target direction against a symmetric facing cone.</summary>
        public static bool IsWithinCriticalAttackAngle(
            Vector3 attackerForward,
            Vector3 directionToTarget,
            float halfAngle)
        {
            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                attackerForward,
                Vector3.up);
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(
                directionToTarget,
                Vector3.up);
            if (horizontalForward.sqrMagnitude <= Mathf.Epsilon ||
                horizontalDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            float signedAngle = Vector3.SignedAngle(
                horizontalForward.normalized,
                horizontalDirection.normalized,
                Vector3.up);
            return Mathf.Abs(signedAngle) <= Mathf.Clamp(halfAngle, 0f, 180f);
        }

        /// <summary>Tests whether an attacker direction lies inside a target's rear cone.</summary>
        public static bool IsWithinBackstabAngle(
            Vector3 targetForward,
            Vector3 directionFromTargetToAttacker,
            float minimumRearAngle)
        {
            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                targetForward,
                Vector3.up);
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(
                directionFromTargetToAttacker,
                Vector3.up);
            if (horizontalForward.sqrMagnitude <= Mathf.Epsilon ||
                horizontalDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            float signedAngle = Vector3.SignedAngle(
                horizontalForward.normalized,
                horizontalDirection.normalized,
                Vector3.up);
            return Mathf.Abs(signedAngle) >=
                Mathf.Clamp(minimumRearAngle, 0f, 180f);
        }

        /// <summary>Clears transient combat windows when the action layer returns to neutral.</summary>
        public virtual void ResetActionState()
        {
            Character?.CharacterNetworkManager?.SetAttackingState(false);
            Character?.CharacterNetworkManager?.SetParryingState(false);
            Character?.CharacterNetworkManager?.SetParryableState(false);
            Character?.SetInvulnerable(false);
            CloseAllDamageColliders();
            m_pendingCriticalDamage = 0;
            m_pendingCriticalDamageSource = null;
            CharacterNetworkManager networkManager =
                Character?.CharacterNetworkManager;
            if (Character?.IsSpawned == true && Character.IsOwner && networkManager != null)
            {
                networkManager.IsRipostable.Value = false;
                networkManager.IsBeingCriticallyDamaged.Value = false;
            }
        }

        private bool PrepareCriticalAttack(
            CharacterManager targetCharacter,
            MeleeWeaponItem criticalWeapon,
            CharacterActionAnimation attackerAnimation,
            CharacterActionAnimation targetAnimation,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage)
        {
            if (Character == null ||
                targetCharacter == null ||
                criticalWeapon == null)
            {
                return false;
            }

            TakeCriticalDamageEffect criticalTemplate =
                WorldCharacterEffectsManager.Instance?.TakeCriticalDamageEffect;
            if (criticalTemplate == null)
            {
                Debug.LogWarning(
                    "WorldCharacterEffectsManager is missing Critical damage.",
                    this);
                return false;
            }

            TakeCriticalDamageEffect runtimeEffect = criticalTemplate
                .CreateRuntimeCriticalDamageEffect(
                    Character,
                    physicalDamage,
                    magicDamage,
                    fireDamage,
                    lightningDamage,
                    holyDamage,
                    targetCharacter.LockOnTransform.position,
                    poiseDamage);
            CharacterEffectsManager targetEffects =
                targetCharacter.CharacterEffectsManager;
            if (targetEffects == null)
            {
                Destroy(runtimeEffect);
                return false;
            }

            targetEffects.ProcessRuntimeInstantEffect(runtimeEffect);
            Character.SetInvulnerable(true);
            Character.CharacterAnimatorManager?.UpdateAnimatorController(
                criticalWeapon);
            if (targetCharacter is AICharacterManager aiTarget)
            {
                aiTarget.CloseAttackDamageColliders();
                aiTarget.StopMoving();
            }

            Character.CharacterAnimatorManager
                ?.PlayTargetActionAnimationInstantly(
                    attackerAnimation,
                    true,
                    true);
            targetCharacter.CharacterAnimatorManager
                ?.PlayTargetActionAnimationInstantly(
                    targetAnimation,
                    true);
            return true;
        }

        private Transform GetRiposteReceiverTransform(WeaponClass weaponClass)
        {
            if (m_riposteReceiverTransform == null)
            {
                Transform existingReceiver = transform.Find("Riposte Transform");
                if (existingReceiver != null)
                {
                    m_riposteReceiverTransform = existingReceiver;
                }
                else
                {
                    GameObject receiver = new GameObject("Riposte Transform");
                    m_riposteReceiverTransform = receiver.transform;
                    m_riposteReceiverTransform.SetParent(transform, false);
                }
            }

            m_riposteReceiverTransform.localPosition =
                WorldUtilityManager.GetRipostingPositionBasedOnWeaponClass(
                    weaponClass);
            m_riposteReceiverTransform.localRotation = Quaternion.identity;
            return m_riposteReceiverTransform;
        }

        private Transform GetBackstabReceiverTransform(WeaponClass weaponClass)
        {
            if (m_backstabReceiverTransform == null)
            {
                Transform existingReceiver = transform.Find(
                    "Backstab Receiver Transform");
                if (existingReceiver != null)
                {
                    m_backstabReceiverTransform = existingReceiver;
                }
                else
                {
                    GameObject receiver = new GameObject(
                        "Backstab Receiver Transform");
                    m_backstabReceiverTransform = receiver.transform;
                    m_backstabReceiverTransform.SetParent(transform, false);
                }
            }

            m_backstabReceiverTransform.localPosition =
                WorldUtilityManager.GetBackstabPositionBasedOnWeaponClass(
                    weaponClass);
            m_backstabReceiverTransform.localRotation = Quaternion.identity;
            return m_backstabReceiverTransform;
        }
    }
}
