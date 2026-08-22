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

        [Header("State")]
        public NetworkVariable<bool> IsJumping = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
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

        [FormerlySerializedAs("networkPositionSmoothTime")]
        [SerializeField, Min(0.001f)] private float m_networkPositionSmoothTime = 0.1f;
        [FormerlySerializedAs("networkRotationSmoothTime")]
        [SerializeField, Min(0.001f)] private float m_networkRotationSmoothTime = 0.1f;

        private CharacterAnimatorManager m_characterAnimatorManager;
        private CharacterManager m_characterManager;
        private CharacterCombatManager m_characterCombatManager;
        private Vector3 m_networkPositionVelocity;

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
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            CurrentHealth.OnValueChanged += OnCurrentHealthChanged;
            IsDead.OnValueChanged += OnIsDeadChanged;
            IsChargingAttack.OnValueChanged += OnIsChargingAttackChanged;

            if (IsOwner)
            {
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                IsJumping.Value = false;
                IsChargingAttack.Value = false;
            }

            ApplyChargingAttackState(IsChargingAttack.Value);
            CheckHP();
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= OnCurrentHealthChanged;
            IsDead.OnValueChanged -= OnIsDeadChanged;
            IsChargingAttack.OnValueChanged -= OnIsChargingAttackChanged;
            ApplyChargingAttackState(false);
            base.OnNetworkDespawn();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            SetChargingAttackState(false);
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
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    NetworkPosition.Value,
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
            if (shouldProcessDeath && !m_characterManager.IsDeathEventRunning)
            {
                StartCoroutine(m_characterManager.ProcessDeathEvent());
            }
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
        /// Requests character damage from the attacker's owner, authorized and relayed by the server.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestCharacterDamageServerRpc(
            ulong targetNetworkObjectId,
            ulong attackerNetworkObjectId,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage,
            Vector3 contactPoint,
            bool wasBlocked,
            ServerRpcParams serverRpcParams = default)
        {
            if (serverRpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            ApplyCharacterDamageClientRpc(
                targetNetworkObjectId,
                attackerNetworkObjectId,
                physicalDamage,
                magicDamage,
                fireDamage,
                lightningDamage,
                holyDamage,
                poiseDamage,
                contactPoint,
                wasBlocked);
        }

        [ClientRpc]
        private void ApplyCharacterDamageClientRpc(
            ulong targetNetworkObjectId,
            ulong attackerNetworkObjectId,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage,
            Vector3 contactPoint,
            bool wasBlocked)
        {
            CharacterManager target = ResolveCharacter(targetNetworkObjectId);
            if (target == null)
            {
                return;
            }

            InstantCharacterEffect runtimeEffect = CreateRuntimeDamageEffect(
                target,
                attackerNetworkObjectId,
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

            target.CharacterEffectsManager?.ProcessRuntimeInstantEffect(runtimeEffect);
        }

        private InstantCharacterEffect CreateRuntimeDamageEffect(
            CharacterManager target,
            ulong attackerNetworkObjectId,
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
            CharacterManager attacker = ResolveCharacter(attackerNetworkObjectId);
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
                    statsManager.BlockingHolyAbsorption);
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

        private void OnIsDeadChanged(bool wasDead, bool isDead)
        {
            if (isDead)
            {
                CheckHP();
                return;
            }

            if (wasDead && !IsOwner)
            {
                m_characterManager?.ReviveCharacter();
            }
        }

        private void OnIsChargingAttackChanged(
            bool wasChargingAttack,
            bool isChargingAttack)
        {
            ApplyChargingAttackState(isChargingAttack);
        }

        private void ApplyChargingAttackState(bool isChargingAttack)
        {
            m_characterAnimatorManager ??=
                GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterAnimatorManager?.SetChargingAttackState(isChargingAttack);
        }
    }
}
