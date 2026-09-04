using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    public class CharacterNetworkManager : NetworkBehaviour
    {
        private const float k_ResourceNetworkUpdateInterval = 0.1f;

        [Header("Position")]
        public NetworkVariable<Vector3> NetworkPosition = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("Rotation")]
        public NetworkVariable<Quaternion> NetworkRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("Animation")]
        public NetworkVariable<float> HorizontalMovement = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> VerticalMovement = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> MoveAmount = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("Stats")]
        public NetworkVariable<int> Vitality = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> Endurance = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> Mind = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        /// <summary>Gets owner-written Strength replicated to every peer.</summary>
        public NetworkVariable<int> Strength = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        /// <summary>Gets owner-written Dexterity replicated to every peer.</summary>
        public NetworkVariable<int> Dexterity = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        /// <summary>Gets owner-written Intelligence replicated to every peer.</summary>
        public NetworkVariable<int> Intelligence = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        /// <summary>Gets owner-written Faith replicated to every peer.</summary>
        public NetworkVariable<int> Faith = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> MaxHealth = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> CurrentStamina = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> MaxStamina = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> CurrentFocusPoints = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> MaxFocusPoints = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("Damage Prediction")]
        public NetworkVariable<float> ArmorPhysicalAbsorption =
            CreateOwnerFloatVariable();
        public NetworkVariable<float> ArmorMagicAbsorption =
            CreateOwnerFloatVariable();
        public NetworkVariable<float> ArmorFireAbsorption =
            CreateOwnerFloatVariable();
        public NetworkVariable<float> ArmorLightningAbsorption =
            CreateOwnerFloatVariable();
        public NetworkVariable<float> ArmorHolyAbsorption =
            CreateOwnerFloatVariable();
        public NetworkVariable<float> BasePoiseDefense =
            CreateOwnerFloatVariable();
        public NetworkVariable<float> ArmorPoiseDefense =
            CreateOwnerFloatVariable();
        public NetworkVariable<float> OffensivePoiseBonus =
            CreateOwnerFloatVariable();
        public NetworkVariable<float> TotalPoiseDamage =
            CreateOwnerFloatVariable();
        public NetworkVariable<int> CurrentStance = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> CanBeKnockedOffLadder =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        [Header("Status Buildup")]
        public NetworkVariable<float> PoisonBuildup = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> BleedBuildup = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> FrostBuildup = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> BuildupCapacity = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("Status Effects")]
        public NetworkVariable<bool> IsPoisoned = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsFrostbitten = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsFrozen = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("State")]
        public NetworkVariable<bool> IsJumping = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsRolling = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsSneaking = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsHidden = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsChargingAttack = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsBlocking = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsAttacking = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsParrying = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsParryable = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsRipostable = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsBeingCriticallyDamaged =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_hasArrowNotched =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isHoldingArrow =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        /// <summary>Gets whether a drawn projectile is attached to this character.</summary>
        public NetworkVariable<bool> HasArrowNotched => m_hasArrowNotched;

        /// <summary>Gets whether this character is still holding its ranged attack.</summary>
        public NetworkVariable<bool> IsHoldingArrow => m_isHoldingArrow;

        [FormerlySerializedAs("networkPositionSmoothTime")]
        [SerializeField, Min(0.001f)] private float m_networkPositionSmoothTime = 0.1f;
        [FormerlySerializedAs("networkRotationSmoothTime")]
        [SerializeField, Min(0.001f)] private float m_networkRotationSmoothTime = 0.1f;

        private CharacterAnimatorManager m_characterAnimatorManager;
        private CharacterManager m_characterManager;
        private CharacterCombatManager m_characterCombatManager;
        private Vector3 m_networkPositionVelocity;
        private bool m_hasResolvedCurrentParry;

        private static NetworkVariable<float> CreateOwnerFloatVariable()
        {
            return new NetworkVariable<float>(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        }

        private void Awake()
        {
            m_characterManager = GetComponent<CharacterManager>();
            m_characterAnimatorManager = GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterCombatManager = GetComponent<CharacterCombatManager>();
            CurrentHealth.SetUpdateTraits(new NetworkVariableUpdateTraits
            {
                MinSecondsBetweenUpdates = k_ResourceNetworkUpdateInterval
            });
            CurrentStamina.SetUpdateTraits(new NetworkVariableUpdateTraits
            {
                MinSecondsBetweenUpdates = k_ResourceNetworkUpdateInterval
            });
            CurrentFocusPoints.SetUpdateTraits(new NetworkVariableUpdateTraits
            {
                MinSecondsBetweenUpdates = k_ResourceNetworkUpdateInterval
            });
            PoisonBuildup.SetUpdateTraits(new NetworkVariableUpdateTraits
            {
                MinSecondsBetweenUpdates = k_ResourceNetworkUpdateInterval
            });
            BleedBuildup.SetUpdateTraits(new NetworkVariableUpdateTraits
            {
                MinSecondsBetweenUpdates = k_ResourceNetworkUpdateInterval
            });
            FrostBuildup.SetUpdateTraits(new NetworkVariableUpdateTraits
            {
                MinSecondsBetweenUpdates = k_ResourceNetworkUpdateInterval
            });
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_hasResolvedCurrentParry = false;
            CurrentHealth.OnValueChanged += OnCurrentHealthChanged;
            IsDead.OnValueChanged += OnIsDeadChanged;
            IsPoisoned.OnValueChanged += OnIsPoisonedChanged;
            IsFrostbitten.OnValueChanged += OnIsFrostbittenChanged;
            IsFrozen.OnValueChanged += OnIsFrozenChanged;
            IsChargingAttack.OnValueChanged += OnIsChargingAttackChanged;
            IsBlocking.OnValueChanged += OnIsBlockingChanged;
            IsParrying.OnValueChanged += OnIsParryingChanged;
            IsSneaking.OnValueChanged += OnIsSneakingChanged;
            m_hasArrowNotched.OnValueChanged += OnRangedStateChanged;
            m_isHoldingArrow.OnValueChanged += OnRangedStateChanged;

            if (IsOwner)
            {
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                IsJumping.Value = false;
                IsRolling.Value = false;
                IsSneaking.Value = false;
                IsChargingAttack.Value = false;
                IsBlocking.Value = false;
                IsAttacking.Value = false;
                IsParrying.Value = false;
                IsParryable.Value = false;
                IsRipostable.Value = false;
                IsBeingCriticallyDamaged.Value = false;
                m_hasArrowNotched.Value = false;
                m_isHoldingArrow.Value = false;
            }

            if (IsServer)
            {
                IsHidden.Value = m_characterCombatManager?.IsHidden ?? true;
            }

            ApplyChargingAttackState(IsChargingAttack.Value);
            ApplyBlockingState(IsBlocking.Value);
            ApplySneakingState(IsSneaking.Value);
            ApplyRangedPresentation();
            m_characterAnimatorManager?.SetDeadState(IsDead.Value);
            OnIsPoisonedChanged(false, IsPoisoned.Value);
            OnIsFrostbittenChanged(false, IsFrostbitten.Value);
            OnIsFrozenChanged(false, IsFrozen.Value);
            CheckHP();
        }

        public override void OnNetworkDespawn()
        {
            m_hasResolvedCurrentParry = false;
            CurrentHealth.OnValueChanged -= OnCurrentHealthChanged;
            IsDead.OnValueChanged -= OnIsDeadChanged;
            IsPoisoned.OnValueChanged -= OnIsPoisonedChanged;
            IsFrostbitten.OnValueChanged -= OnIsFrostbittenChanged;
            IsFrozen.OnValueChanged -= OnIsFrozenChanged;
            IsChargingAttack.OnValueChanged -= OnIsChargingAttackChanged;
            IsBlocking.OnValueChanged -= OnIsBlockingChanged;
            IsParrying.OnValueChanged -= OnIsParryingChanged;
            IsSneaking.OnValueChanged -= OnIsSneakingChanged;
            m_hasArrowNotched.OnValueChanged -= OnRangedStateChanged;
            m_isHoldingArrow.OnValueChanged -= OnRangedStateChanged;
            ApplyChargingAttackState(false);
            ApplyBlockingState(false);
            ApplySneakingState(false);
            m_characterAnimatorManager?.SetRangedWeaponState(false, false, false);
            m_characterManager?.CharacterEffectsManager?.SetFrostbittenState(
                false);
            m_characterManager?.SetFrozenState(false);
            base.OnNetworkDespawn();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            SetRollingState(false);
            SetSneakingState(false);
            SetChargingAttackState(false);
            SetBlockingState(false);
            SetAttackingState(false);
            SetParryingState(false);
            SetParryableState(false);
            IsRipostable.Value = false;
            IsBeingCriticallyDamaged.Value = false;
            m_hasArrowNotched.Value = false;
            m_isHoldingArrow.Value = false;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
            }
            else
            {
                Vector3 networkPosition = NetworkPosition.Value;
                if (m_characterManager?.CharacterLocomotionManager
                    ?.IsRidingLift == true &&
                    m_characterManager.IsJumping == false)
                {
                    networkPosition.y = transform.position.y;
                }

                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    networkPosition,
                    ref m_networkPositionVelocity,
                    m_networkPositionSmoothTime);

                float rotationInterpolation = 1f - Mathf.Exp(-Time.deltaTime / m_networkRotationSmoothTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    NetworkRotation.Value,
                    rotationInterpolation);
            }
        }

        /// <summary>
        /// Clamps owner Health to its maximum and starts death presentation on every peer.
        /// </summary>
        public void CheckHP()
        {
            if (!IsSpawned || m_characterManager == null)
            {
                return;
            }

            float maximumHealth = Mathf.Max(0f, MaxHealth.Value);
            if (IsOwner && maximumHealth > 0f && CurrentHealth.Value > maximumHealth)
            {
                CurrentHealth.Value = maximumHealth;
                return;
            }

            bool hasInitializedHealth = maximumHealth > 0f;
            bool shouldProcessDeath = IsDead.Value ||
                hasInitializedHealth && CurrentHealth.Value <= 0f;
            m_characterManager.ReconcilePredictedDeath(
                shouldProcessDeath,
                CurrentHealth.Value);
            if (shouldProcessDeath && !m_characterManager.IsDeathEventRunning)
            {
                StartCoroutine(m_characterManager.ProcessDeathEvent(
                    IsBeingCriticallyDamaged.Value));
            }
        }

        /// <summary>
        /// Writes whether the owner is currently inside a rolling action.
        /// </summary>
        public void SetRollingState(bool isRolling)
        {
            if (!IsSpawned || !IsOwner || IsRolling.Value == isRolling)
            {
                return;
            }

            IsRolling.Value = isRolling;
        }

        /// <summary>
        /// Writes the owner's charging intent for synchronized remote presentation.
        /// </summary>
        public void SetChargingAttackState(bool isChargingAttack)
        {
            if (!IsSpawned || !IsOwner || IsChargingAttack.Value == isChargingAttack)
            {
                return;
            }

            IsChargingAttack.Value = isChargingAttack;
        }

        /// <summary>Writes the owner's sustained blocking state.</summary>
        public void SetBlockingState(bool isBlocking)
        {
            if (!IsSpawned || !IsOwner || IsBlocking.Value == isBlocking)
            {
                return;
            }

            IsBlocking.Value = isBlocking;
        }

        /// <summary>Writes the owner's deliberate low-profile locomotion state.</summary>
        public void SetSneakingState(bool isSneaking)
        {
            if (!IsSpawned || !IsOwner || IsSneaking.Value == isSneaking)
            {
                return;
            }

            IsSneaking.Value = isSneaking;
        }

        /// <summary>Writes server-authoritative target visibility for every peer.</summary>
        public void SetHiddenState(bool isHidden)
        {
            if (!IsSpawned || !IsServer || IsHidden.Value == isHidden)
            {
                return;
            }

            IsHidden.Value = isHidden;
        }

        /// <summary>Writes whether the owner is inside an attack action.</summary>
        public void SetAttackingState(bool isAttacking)
        {
            if (!IsSpawned || !IsOwner || IsAttacking.Value == isAttacking)
            {
                return;
            }

            IsAttacking.Value = isAttacking;
        }

        /// <summary>Writes the owner's replicated projectile notch and hold state.</summary>
        public void SetNotchedProjectileState(
            bool hasArrowNotched,
            bool isHoldingArrow)
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            m_hasArrowNotched.Value = hasArrowNotched;
            m_isHoldingArrow.Value = hasArrowNotched && isHoldingArrow;
        }

        /// <summary>Releases held input while retaining the arrow until its fire event.</summary>
        public void SetHoldingArrowState(bool isHoldingArrow)
        {
            if (IsSpawned &&
                IsOwner &&
                m_hasArrowNotched.Value &&
                m_isHoldingArrow.Value != isHoldingArrow)
            {
                m_isHoldingArrow.Value = isHoldingArrow;
            }
        }

        /// <summary>Writes whether the owner is currently inside Parry active frames.</summary>
        public void SetParryingState(bool isParrying)
        {
            if (!IsSpawned ||
                !IsOwner ||
                IsParrying.Value == isParrying)
            {
                return;
            }

            IsParrying.Value = isParrying;
        }

        /// <summary>Writes whether the owner's active attack accepts a Parry.</summary>
        public void SetParryableState(bool isParryable)
        {
            if (!IsSpawned ||
                !IsOwner ||
                IsParryable.Value == isParryable)
            {
                return;
            }

            IsParryable.Value = isParryable;
        }

        /// <summary>Gets the replicated value for one status accumulation channel.</summary>
        public float GetBuildup(Buildup buildupType)
        {
            return buildupType switch
            {
                Buildup.Poison => PoisonBuildup.Value,
                Buildup.Bleed => BleedBuildup.Value,
                Buildup.Frost => FrostBuildup.Value,
                _ => 0f
            };
        }

        /// <summary>Writes one owner-authoritative buildup value within its valid range.</summary>
        public bool TrySetBuildup(Buildup buildupType, float buildupAmount)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            float maximumBuildup = BuildupCapacity.Value > 0f
                ? BuildupCapacity.Value
                : float.MaxValue;
            float sanitizedAmount = Mathf.Clamp(
                buildupAmount,
                0f,
                maximumBuildup);
            NetworkVariable<float> buildupVariable = buildupType switch
            {
                Buildup.Poison => PoisonBuildup,
                Buildup.Bleed => BleedBuildup,
                Buildup.Frost => FrostBuildup,
                _ => null
            };
            if (buildupVariable == null)
            {
                return false;
            }

            buildupVariable.Value = sanitizedAmount;
            return true;
        }

        /// <summary>Writes the owner's replicated Poison state.</summary>
        public bool TrySetPoisoned(bool isPoisoned)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            IsPoisoned.Value = isPoisoned;
            return true;
        }

        /// <summary>Writes the owner's replicated Frostbite state.</summary>
        public bool TrySetFrostbitten(bool isFrostbitten)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            IsFrostbitten.Value = isFrostbitten;
            return true;
        }

        /// <summary>Writes the owner's independent replicated Freeze state.</summary>
        public bool TrySetFrozen(bool isFrozen)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            IsFrozen.Value = isFrozen;
            return true;
        }

        /// <summary>Reapplies late-join blocking data after replicated equipment is ready.</summary>
        public void RefreshBlockingPresentation()
        {
            ApplyBlockingState(IsBlocking.Value);
        }

        /// <summary>
        /// Sends an owner-predicted action animation to the server for replication to remote clients.
        /// </summary>
        [ServerRpc]
        public void NotifyServerOfActionAnimationServerRpc(
            CharacterActionAnimation targetAnimation,
            bool isPerformingAction,
            bool shouldApplyRootMotion,
            bool canRotate,
            bool canMove,
            ServerRpcParams serverRpcParams = default)
        {
            if (!CharacterAnimatorManager.IsSupportedActionAnimation(targetAnimation))
            {
                Debug.LogWarning($"Rejected unsupported character action {targetAnimation}.", this);
                return;
            }

            PlayActionAnimationForAllClientsClientRpc(
                targetAnimation,
                isPerformingAction,
                shouldApplyRootMotion,
                canRotate,
                canMove,
                serverRpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void PlayActionAnimationForAllClientsClientRpc(
            CharacterActionAnimation targetAnimation,
            bool isPerformingAction,
            bool shouldApplyRootMotion,
            bool canRotate,
            bool canMove,
            ulong senderClientId)
        {
            if (NetworkManager.Singleton != null &&
                senderClientId == NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

            m_characterAnimatorManager ??= GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterAnimatorManager?.PlayTargetActionAnimation(
                targetAnimation,
                isPerformingAction,
                shouldApplyRootMotion,
                canRotate,
                canMove);
        }

        /// <summary>
        /// Sends an owner-played instant action to the server for remote presentation.
        /// </summary>
        [ServerRpc]
        public void NotifyServerOfInstantActionAnimationServerRpc(
            CharacterActionAnimation targetAnimation,
            bool isPerformingAction,
            bool shouldApplyRootMotion,
            bool canRotate,
            bool canMove,
            ServerRpcParams serverRpcParams = default)
        {
            if (!CharacterAnimatorManager.IsSupportedInstantActionAnimation(
                    targetAnimation))
            {
                Debug.LogWarning(
                    $"Rejected unsupported instant action {targetAnimation}.",
                    this);
                return;
            }

            PlayInstantActionAnimationForAllClientsClientRpc(
                targetAnimation,
                isPerformingAction,
                shouldApplyRootMotion,
                canRotate,
                canMove,
                serverRpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void PlayInstantActionAnimationForAllClientsClientRpc(
            CharacterActionAnimation targetAnimation,
            bool isPerformingAction,
            bool shouldApplyRootMotion,
            bool canRotate,
            bool canMove,
            ulong senderClientId)
        {
            if (NetworkManager.Singleton != null &&
                senderClientId == NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

            m_characterAnimatorManager ??=
                GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterAnimatorManager?.PlayTargetActionAnimationInstantly(
                targetAnimation,
                isPerformingAction,
                shouldApplyRootMotion,
                canRotate,
                canMove);
        }

        /// <summary>
        /// Sends an owner-predicted attack animation to the server for replication to remote clients.
        /// </summary>
        [ServerRpc]
        public void NotifyServerOfAttackActionServerRpc(
            AttackType attackType,
            ServerRpcParams serverRpcParams = default)
        {
            PlayAttackActionForAllClientsClientRpc(
                attackType,
                serverRpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void PlayAttackActionForAllClientsClientRpc(
            AttackType attackType,
            ulong senderClientId)
        {
            if (NetworkManager.Singleton != null &&
                senderClientId == NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

            m_characterCombatManager ??= GetComponent<CharacterCombatManager>();
            m_characterCombatManager?.ReplicateAttack(attackType);
        }

        /// <summary>
        /// Routes a collision-confirmed Parry through the server authority.
        /// Server-owned AI colliders resolve immediately; client callers use the RPC.
        /// </summary>
        public void RequestParry(ulong parriedCharacterNetworkObjectId)
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                ProcessParryRequest(parriedCharacterNetworkObjectId);
                return;
            }

            NotifyServerOfParryServerRpc(parriedCharacterNetworkObjectId);
        }

        /// <summary>Asks the server to validate one collision-confirmed Parry.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void NotifyServerOfParryServerRpc(
            ulong parriedCharacterNetworkObjectId,
            RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            ProcessParryRequest(parriedCharacterNetworkObjectId);
        }

        private void ProcessParryRequest(
            ulong parriedCharacterNetworkObjectId)
        {
            CharacterManager parryingCharacter = m_characterManager;
            CharacterManager parriedCharacter = ResolveCharacter(
                parriedCharacterNetworkObjectId);
            CharacterNetworkManager parriedNetworkManager =
                parriedCharacter?.CharacterNetworkManager;
            if (m_hasResolvedCurrentParry ||
                parryingCharacter == null ||
                parriedCharacter == null ||
                parriedNetworkManager == null ||
                parryingCharacter.IsDead ||
                parriedCharacter.IsDead ||
                parryingCharacter.IsInvulnerable ||
                !IsParrying.Value ||
                !parriedNetworkManager.IsParryable.Value ||
                parriedNetworkManager.IsBeingCriticallyDamaged.Value ||
                !WorldUtilityManager.CanDamageCharacter(
                    parriedCharacter,
                    parryingCharacter) ||
                (parriedCharacter.transform.position -
                    parryingCharacter.transform.position).sqrMagnitude > 16f)
            {
                return;
            }

            m_hasResolvedCurrentParry = true;
            parriedCharacter.CharacterCombatManager
                ?.CloseAllDamageColliders();
            parriedNetworkManager.SetParryableState(false);
            ProcessParryClientRpc(
                parriedCharacterNetworkObjectId,
                NetworkObjectId);
        }

        [ClientRpc]
        private void ProcessParryClientRpc(
            ulong parriedCharacterNetworkObjectId,
            ulong parryingCharacterNetworkObjectId)
        {
            CharacterManager parriedCharacter = ResolveCharacter(
                parriedCharacterNetworkObjectId);
            CharacterManager parryingCharacter = ResolveCharacter(
                parryingCharacterNetworkObjectId);
            parriedCharacter?.CharacterCombatManager
                ?.ProcessParryFromServer(parryingCharacter);
        }

        /// <summary>
        /// Reserves one Riposte target on the server and relays its ordered payload.
        /// </summary>
        [ServerRpc]
        public void NotifyServerOfRiposteServerRpc(
            ulong targetNetworkObjectId,
            ulong attackerNetworkObjectId,
            int weaponID,
            CharacterActionAnimation criticalDamageAnimation,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage,
            ServerRpcParams serverRpcParams = default)
        {
            CharacterManager attacker = ResolveCharacter(
                attackerNetworkObjectId);
            CharacterManager target = ResolveCharacter(targetNetworkObjectId);
            CharacterNetworkManager targetNetworkManager =
                target?.CharacterNetworkManager;
            MeleeWeaponItem weapon =
                WorldItemDatabase.Instance?.GetWeaponByID(weaponID) as
                    MeleeWeaponItem;
            Vector3 directionToTarget = target != null && attacker != null
                ? target.transform.position - attacker.transform.position
                : Vector3.zero;
            Vector3 directionToAttacker = -directionToTarget;
            if (serverRpcParams.Receive.SenderClientId != OwnerClientId ||
                attackerNetworkObjectId != NetworkObjectId ||
                attacker == null ||
                target == null ||
                weapon == null ||
                attacker.IsDead ||
                attacker.IsPerformingAction ||
                attacker.CharacterNetworkManager.CurrentStamina.Value <= 0f ||
                !WorldUtilityManager.CanDamageCharacter(attacker, target) ||
                targetNetworkManager == null ||
                !targetNetworkManager.IsRipostable.Value ||
                targetNetworkManager.IsBeingCriticallyDamaged.Value ||
                criticalDamageAnimation != CharacterActionAnimation.Riposted ||
                directionToTarget.sqrMagnitude > 2.25f ||
                !CharacterCombatManager.IsWithinCriticalAttackAngle(
                    attacker.transform.forward,
                    directionToTarget,
                    75f) ||
                !CharacterCombatManager.IsWithinCriticalAttackAngle(
                    target.transform.forward,
                    directionToAttacker,
                    75f))
            {
                return;
            }

            targetNetworkManager.IsBeingCriticallyDamaged.Value = true;
            targetNetworkManager.IsRipostable.Value = false;
            float damageModifier = weapon.RiposteAttack01Modifier;
            NotifyServerOfRiposteClientRpc(
                targetNetworkObjectId,
                attackerNetworkObjectId,
                weaponID,
                criticalDamageAnimation,
                Mathf.Clamp(
                    physicalDamage,
                    0f,
                    weapon.PhysicalDamage * damageModifier),
                Mathf.Clamp(
                    magicDamage,
                    0f,
                    weapon.MagicDamage * damageModifier),
                Mathf.Clamp(
                    fireDamage,
                    0f,
                    weapon.FireDamage * damageModifier),
                Mathf.Clamp(
                    lightningDamage,
                    0f,
                    weapon.LightningDamage * damageModifier),
                Mathf.Clamp(
                    holyDamage,
                    0f,
                    weapon.HolyDamage * damageModifier),
                0f);
        }

        [ClientRpc]
        private void NotifyServerOfRiposteClientRpc(
            ulong targetNetworkObjectId,
            ulong attackerNetworkObjectId,
            int weaponID,
            CharacterActionAnimation criticalDamageAnimation,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage)
        {
            CharacterManager attacker = ResolveCharacter(
                attackerNetworkObjectId);
            CharacterManager target = ResolveCharacter(targetNetworkObjectId);
            MeleeWeaponItem weapon =
                WorldItemDatabase.Instance?.GetWeaponByID(weaponID) as
                    MeleeWeaponItem;
            if (attacker == null || target == null || weapon == null)
            {
                return;
            }

            CharacterCombatManager attackerCombatManager =
                attacker.CharacterCombatManager;
            attackerCombatManager?.ProcessRiposteFromServer(
                target,
                weapon,
                criticalDamageAnimation,
                physicalDamage,
                magicDamage,
                fireDamage,
                lightningDamage,
                holyDamage,
                poiseDamage);
        }

        /// <summary>
        /// Reserves one rear Critical target and relays the ordered Backstab payload.
        /// </summary>
        [ServerRpc]
        public void NotifyTheServerOfBackstabServerRpc(
            ulong targetNetworkObjectId,
            ulong attackerNetworkObjectId,
            int weaponID,
            CharacterActionAnimation criticalDamageAnimation,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage,
            ServerRpcParams serverRpcParams = default)
        {
            CharacterManager attacker = ResolveCharacter(
                attackerNetworkObjectId);
            CharacterManager target = ResolveCharacter(targetNetworkObjectId);
            CharacterNetworkManager targetNetworkManager =
                target?.CharacterNetworkManager;
            CharacterCombatManager targetCombatManager =
                target?.CharacterCombatManager;
            MeleeWeaponItem weapon =
                WorldItemDatabase.Instance?.GetWeaponByID(weaponID) as
                    MeleeWeaponItem;
            Vector3 directionToTarget = target != null && attacker != null
                ? target.transform.position - attacker.transform.position
                : Vector3.zero;
            Vector3 directionToAttacker = -directionToTarget;
            if (serverRpcParams.Receive.SenderClientId != OwnerClientId ||
                attackerNetworkObjectId != NetworkObjectId ||
                attacker == null ||
                target == null ||
                weapon == null ||
                attacker.IsDead ||
                attacker.IsPerformingAction ||
                attacker.CharacterNetworkManager.CurrentStamina.Value <= 0f ||
                !WorldUtilityManager.CanDamageCharacter(attacker, target) ||
                targetNetworkManager == null ||
                targetCombatManager?.CanBeBackstabbed != true ||
                targetNetworkManager.IsBeingCriticallyDamaged.Value ||
                criticalDamageAnimation !=
                    CharacterActionAnimation.Backstabbed ||
                directionToTarget.sqrMagnitude > 2.25f ||
                !CharacterCombatManager.IsWithinCriticalAttackAngle(
                    attacker.transform.forward,
                    directionToTarget,
                    75f) ||
                !CharacterCombatManager.IsWithinBackstabAngle(
                    target.transform.forward,
                    directionToAttacker,
                    145f))
            {
                return;
            }

            targetNetworkManager.IsBeingCriticallyDamaged.Value = true;
            targetNetworkManager.IsRipostable.Value = false;
            float damageModifier = weapon.BackstabAttack01Modifier;
            NotifyTheServerOfBackstabClientRpc(
                targetNetworkObjectId,
                attackerNetworkObjectId,
                weaponID,
                criticalDamageAnimation,
                Mathf.Clamp(
                    physicalDamage,
                    0f,
                    weapon.PhysicalDamage * damageModifier),
                Mathf.Clamp(
                    magicDamage,
                    0f,
                    weapon.MagicDamage * damageModifier),
                Mathf.Clamp(
                    fireDamage,
                    0f,
                    weapon.FireDamage * damageModifier),
                Mathf.Clamp(
                    lightningDamage,
                    0f,
                    weapon.LightningDamage * damageModifier),
                Mathf.Clamp(
                    holyDamage,
                    0f,
                    weapon.HolyDamage * damageModifier),
                0f);
        }

        [ClientRpc]
        private void NotifyTheServerOfBackstabClientRpc(
            ulong targetNetworkObjectId,
            ulong attackerNetworkObjectId,
            int weaponID,
            CharacterActionAnimation criticalDamageAnimation,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage)
        {
            CharacterManager attacker = ResolveCharacter(
                attackerNetworkObjectId);
            CharacterManager target = ResolveCharacter(targetNetworkObjectId);
            MeleeWeaponItem weapon =
                WorldItemDatabase.Instance?.GetWeaponByID(weaponID) as
                    MeleeWeaponItem;
            if (attacker == null || target == null || weapon == null)
            {
                return;
            }

            attacker.CharacterCombatManager?.ProcessBackstabFromServer(
                target,
                weapon,
                criticalDamageAnimation,
                physicalDamage,
                magicDamage,
                fireDamage,
                lightningDamage,
                holyDamage,
                poiseDamage);
        }

        /// <summary>
        /// Requests character damage from the attacker's owner, authorized and relayed by the server.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestCharacterDamageServerRpc(
            ulong targetNetworkObjectId,
            ulong attackerNetworkObjectId,
            bool attackerIsPresent,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage,
            float poisonBuildup,
            float bleedBuildup,
            float frostBuildup,
            Vector3 contactPoint,
            bool wasBlocked,
            RpcParams rpcParams = default)
        {
            CharacterManager target = ResolveCharacter(targetNetworkObjectId);
            CharacterManager attacker = attackerIsPresent
                ? ResolveCharacter(attackerNetworkObjectId)
                : null;
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (!IsAuthorizedDamageSender(
                    senderClientId,
                    target,
                    attacker,
                    attackerIsPresent))
            {
                return;
            }

            if (attacker?.CharacterCombatManager is
                    AICharacterCombatManager aiCombatManager &&
                aiCombatManager.TryRegisterDamageTarget(target))
            {
                aiCombatManager.RecordSuccessfulHit(target);
            }

            ApplyCharacterDamageClientRpc(
                targetNetworkObjectId,
                attackerNetworkObjectId,
                attackerIsPresent,
                senderClientId,
                physicalDamage,
                magicDamage,
                fireDamage,
                lightningDamage,
                holyDamage,
                poiseDamage,
                poisonBuildup,
                bleedBuildup,
                frostBuildup,
                contactPoint,
                wasBlocked);
        }

        [ClientRpc]
        private void ApplyCharacterDamageClientRpc(
            ulong targetNetworkObjectId,
            ulong attackerNetworkObjectId,
            bool attackerIsPresent,
            ulong initiatingClientId,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage,
            float poisonBuildup,
            float bleedBuildup,
            float frostBuildup,
            Vector3 contactPoint,
            bool wasBlocked)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == initiatingClientId)
            {
                return;
            }

            CharacterManager target = ResolveCharacter(targetNetworkObjectId);
            if (target == null)
            {
                return;
            }

            DamageEffect runtimeEffect = CreateRuntimeDamageEffect(
                target,
                attackerNetworkObjectId,
                attackerIsPresent,
                physicalDamage,
                magicDamage,
                fireDamage,
                lightningDamage,
                holyDamage,
                poiseDamage,
                contactPoint,
                wasBlocked);
            if (runtimeEffect == null)
            {
                return;
            }

            DamageProcessingMode processingMode = target.IsOwner
                ? DamageProcessingMode.Authoritative
                : DamageProcessingMode.ReplicatedPresentation;
            target.CharacterEffectsManager?.ProcessRuntimeDamageEffect(
                runtimeEffect,
                processingMode);
            if (!wasBlocked &&
                processingMode == DamageProcessingMode.Authoritative)
            {
                target.CharacterEffectsManager?.ProcessBuildupEffects(
                    poisonBuildup,
                    bleedBuildup,
                    frostBuildup);
            }
        }

        private DamageEffect CreateRuntimeDamageEffect(
            CharacterManager target,
            ulong attackerNetworkObjectId,
            bool attackerIsPresent,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage,
            Vector3 contactPoint,
            bool wasBlocked)
        {
            WorldCharacterEffectsManager effectsManager =
                WorldCharacterEffectsManager.Instance;
            CharacterManager attacker = attackerIsPresent
                ? ResolveCharacter(attackerNetworkObjectId)
                : null;
            if (wasBlocked)
            {
                TakeBlockedDamageEffect blockedTemplate =
                    effectsManager?.TakeBlockedDamageEffect;
                CharacterStatsManager statsManager = target.CharacterStatsManager;
                if (blockedTemplate == null || statsManager == null)
                {
                    Debug.LogWarning(
                        "Blocked damage requires its effect template and target stats.",
                        this);
                    return null;
                }

                return blockedTemplate.CreateRuntimeBlockedDamageEffect(
                    attacker,
                    physicalDamage,
                    magicDamage,
                    fireDamage,
                    lightningDamage,
                    holyDamage,
                    contactPoint,
                    poiseDamage,
                    statsManager.BlockingPhysicalAbsorption,
                    statsManager.BlockingMagicAbsorption,
                    statsManager.BlockingFireAbsorption,
                    statsManager.BlockingLightningAbsorption,
                    statsManager.BlockingHolyAbsorption,
                    statsManager.BlockingStability);
            }

            TakeDamageEffect damageTemplate = effectsManager?.TakeDamageEffect;
            if (damageTemplate == null)
            {
                Debug.LogWarning(
                    "WorldCharacterEffectsManager is missing the TakeDamageEffect template.",
                    this);
                return null;
            }

            return damageTemplate.CreateRuntimeDamageEffect(
                attacker,
                physicalDamage,
                magicDamage,
                fireDamage,
                lightningDamage,
                holyDamage,
                contactPoint,
                poiseDamage);
        }

        private static bool IsAuthorizedDamageSender(
            ulong senderClientId,
            CharacterManager target,
            CharacterManager attacker,
            bool attackerIsPresent)
        {
            if (target == null || target.IsInvulnerable)
            {
                return false;
            }

            if (!attackerIsPresent)
            {
                return senderClientId == target.OwnerClientId;
            }

            if (attacker == null ||
                !WorldUtilityManager.CanDamageCharacter(attacker, target))
            {
                return false;
            }

            ulong authorizedClientId = attacker is AICharacterManager &&
                target is PlayerManager
                    ? target.OwnerClientId
                    : attacker.OwnerClientId;
            return senderClientId == authorizedClientId;
        }

        private static CharacterManager ResolveCharacter(ulong networkObjectId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId,
                    out NetworkObject networkObject))
            {
                return null;
            }

            return networkObject.GetComponent<CharacterManager>();
        }

        private void OnCurrentHealthChanged(float previousHealth, float currentHealth)
        {
            CheckHP();
        }

        protected virtual void OnIsDeadChanged(bool wasDead, bool isDead)
        {
            m_characterAnimatorManager?.SetDeadState(isDead);
            if (isDead)
            {
                if (IsOwner)
                {
                    SetSneakingState(false);
                }

                if (IsOwner && IsPoisoned.Value)
                {
                    TrySetPoisoned(false);
                }

                if (IsOwner && IsFrostbitten.Value)
                {
                    TrySetFrostbitten(false);
                }

                if (IsOwner && IsFrozen.Value)
                {
                    TrySetFrozen(false);
                }

                CheckHP();
                return;
            }

            if (wasDead && !IsOwner)
            {
                m_characterManager?.ReviveCharacter();
            }
        }

        private void OnIsPoisonedChanged(bool wasPoisoned, bool isPoisoned)
        {
            m_characterManager?.CharacterEffectsManager?.SetPoisonedState(
                isPoisoned);
            m_characterManager?.CharacterUIManager?.SetPoisonedState(
                isPoisoned);

            if (m_characterManager is not PlayerManager player || !player.IsOwner)
            {
                return;
            }

            PlayerUIManager playerUI = PlayerUIManager.Instance;
            if (!wasPoisoned && isPoisoned)
            {
                playerUI?.PlayerUIPopUpManager?.SendStatusEffectPopup(
                    Buildup.Poison);
            }
        }

        private void OnIsFrostbittenChanged(
            bool wasFrostbitten,
            bool isFrostbitten)
        {
            m_characterManager?.CharacterEffectsManager?.SetFrostbittenState(
                isFrostbitten);
            if (m_characterManager is PlayerManager player &&
                player.IsOwner &&
                !wasFrostbitten &&
                isFrostbitten)
            {
                PlayerUIManager.Instance?.PlayerUIPopUpManager
                    ?.SendStatusEffectPopup(Buildup.Frost);
            }
        }

        private void OnIsFrozenChanged(bool wasFrozen, bool isFrozen)
        {
            m_characterManager?.SetFrozenState(isFrozen);
        }

        private void OnIsChargingAttackChanged(
            bool wasChargingAttack,
            bool isChargingAttack)
        {
            ApplyChargingAttackState(isChargingAttack);
        }

        private void OnIsBlockingChanged(bool wasBlocking, bool isBlocking)
        {
            ApplyBlockingState(isBlocking);
        }

        private void OnIsSneakingChanged(bool wasSneaking, bool isSneaking)
        {
            ApplySneakingState(isSneaking);
        }

        private void OnRangedStateChanged(bool previousValue, bool currentValue)
        {
            ApplyRangedPresentation();
        }

        private void OnIsParryingChanged(bool wasParrying, bool isParrying)
        {
            if (!isParrying)
            {
                m_hasResolvedCurrentParry = false;
            }
        }

        private void ApplyChargingAttackState(bool isChargingAttack)
        {
            m_characterAnimatorManager ??=
                GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterAnimatorManager?.SetChargingAttackState(isChargingAttack);
        }

        private void ApplyBlockingState(bool isBlocking)
        {
            m_characterAnimatorManager ??=
                GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterAnimatorManager?.SetBlockingState(isBlocking);
            if (isBlocking && m_characterManager is PlayerManager player)
            {
                player.PlayerStatsManager?.SetBlockingStats(
                    player.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true
                        ? player.InventoryManager?.CurrentTwoHandWeapon
                        : player.InventoryManager?.CurrentLeftHandWeapon);
            }
        }

        private void ApplySneakingState(bool isSneaking)
        {
            m_characterAnimatorManager ??=
                GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterAnimatorManager?.SetSneakingState(isSneaking);
        }

        /// <summary>Applies replicated ranged state for this character type.</summary>
        protected virtual void ApplyRangedPresentation()
        {
            m_characterAnimatorManager ??=
                GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterAnimatorManager?.SetRangedWeaponState(
                m_hasArrowNotched.Value,
                m_isHoldingArrow.Value,
                false);
            if (!m_hasArrowNotched.Value)
            {
                m_characterManager?.CharacterEffectsManager
                    ?.DestroyAllCurrentActionEffects();
            }
        }
    }
}
