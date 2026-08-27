using Unity.Collections;
using Unity.Netcode;

namespace ZZ
{
    /// <summary>
    /// Replicates server-owned AI state and discrete pivot presentation.
    /// </summary>
    public class AICharacterNetworkManager : CharacterNetworkManager
    {
        /// <summary>Replicates whether the complete AI root is enabled on every peer.</summary>
        public NetworkVariable<bool> IsActive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>Replicates the server-selected behavior state.</summary>
        public NetworkVariable<AICharacterStateId> CurrentAIState =
            new NetworkVariable<AICharacterStateId>(
                AICharacterStateId.Idle,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>Replicates whether this AI has left its sleeping state.</summary>
        public NetworkVariable<bool> IsAwake = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>Replicates the authored persistent sleeping state name.</summary>
        public NetworkVariable<FixedString64Bytes> SleepingAnimation =
            new NetworkVariable<FixedString64Bytes>(
                new FixedString64Bytes("Sleep_01"),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>Replicates the authored waking transition state name.</summary>
        public NetworkVariable<FixedString64Bytes> WakingAnimation =
            new NetworkVariable<FixedString64Bytes>(
                new FixedString64Bytes("Wake_01"),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <inheritdoc />
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            IsActive.OnValueChanged += OnActiveStateChanged;
            IsAwake.OnValueChanged += OnAwakeStateChanged;
            if (!IsAwake.Value)
            {
                PlaySleepingAnimation();
            }

            ApplyActiveState(IsActive.Value);
        }

        /// <inheritdoc />
        public override void OnNetworkDespawn()
        {
            IsActive.OnValueChanged -= OnActiveStateChanged;
            IsAwake.OnValueChanged -= OnAwakeStateChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>Publishes one server-authoritative AI activation transition.</summary>
        public bool SetActiveState(bool isActive)
        {
            if (!IsSpawned || !IsServer)
            {
                return false;
            }

            if (IsActive.Value != isActive)
            {
                IsActive.Value = isActive;
            }
            else
            {
                ApplyActiveState(isActive);
            }

            return true;
        }

        /// <summary>Publishes an AI state transition from the server.</summary>
        public void SetAIState(AICharacterStateId stateId)
        {
            if (IsSpawned && IsServer && CurrentAIState.Value != stateId)
            {
                CurrentAIState.Value = stateId;
            }
        }

        /// <summary>Publishes authored sleep data and a server-selected awake transition.</summary>
        public void SetAwakeState(
            bool isAwake,
            string sleepingAnimation,
            string wakingAnimation)
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(sleepingAnimation))
            {
                SleepingAnimation.Value = new FixedString64Bytes(
                    sleepingAnimation);
            }

            if (!string.IsNullOrWhiteSpace(wakingAnimation))
            {
                WakingAnimation.Value = new FixedString64Bytes(
                    wakingAnimation);
            }

            if (IsAwake.Value != isAwake)
            {
                IsAwake.Value = isAwake;
            }
            else if (!isAwake)
            {
                PlaySleepingAnimation();
            }
        }

        /// <summary>Applies the current sleeping animation without waiting for a value change.</summary>
        public void PlaySleepingAnimation()
        {
            GetComponentInChildren<AICharacterAnimatorManager>(true)
                ?.PlaySleepingAnimation(SleepingAnimation.Value.ToString());
        }

        /// <inheritdoc />
        protected override void OnIsDeadChanged(bool wasDead, bool isDead)
        {
            base.OnIsDeadChanged(wasDead, isDead);
            AICharacterInventoryManager inventoryManager =
                GetComponent<AICharacterInventoryManager>();
            if (!isDead)
            {
                if (wasDead && IsServer)
                {
                    inventoryManager?.ResetDropState();
                    GetComponent<AICharacterCombatManager>()
                        ?.ClearRuneRewardCandidate();
                }

                return;
            }

            if (!wasDead && IsServer)
            {
                inventoryManager?.DropItem();
                AwardRunesToKiller();
            }
        }

        private void AwardRunesToKiller()
        {
            AICharacterCombatManager combatManager =
                GetComponent<AICharacterCombatManager>();
            PlayerManager player = combatManager?.RuneRewardCandidate;
            if (player == null)
            {
                return;
            }

            int reward = GetComponent<CharacterStatsManager>()
                ?.RunesDroppedOnDeath ?? 0;
            if (player.OwnerClientId == NetworkManager.LocalClientId)
            {
                combatManager.AwardRunesOnDeath(player, reward);
            }
            else
            {
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { player.OwnerClientId }
                    }
                };
                AwardRunesClientRpc(
                    player.NetworkObjectId,
                    reward,
                    clientRpcParams);
            }

            combatManager.ClearRuneRewardCandidate();
        }

        private void OnAwakeStateChanged(bool wasAwake, bool isAwake)
        {
            AICharacterAnimatorManager animatorManager =
                GetComponentInChildren<AICharacterAnimatorManager>(true);
            if (isAwake)
            {
                animatorManager?.PlayWakingAnimation(
                    WakingAnimation.Value.ToString());
            }
            else
            {
                animatorManager?.PlaySleepingAnimation(
                    SleepingAnimation.Value.ToString());
            }
        }

        private void OnActiveStateChanged(bool wasActive, bool isActive)
        {
            ApplyActiveState(isActive);
        }

        private void ApplyActiveState(bool isActive)
        {
            if (gameObject.activeSelf != isActive)
            {
                gameObject.SetActive(isActive);
            }
        }

        [ClientRpc]
        private void AwardRunesClientRpc(
            ulong playerNetworkObjectId,
            int reward,
            ClientRpcParams clientRpcParams = default)
        {
            if (NetworkManager == null ||
                !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    playerNetworkObjectId,
                    out NetworkObject playerNetworkObject))
            {
                return;
            }

            PlayerManager player = playerNetworkObject.GetComponent<PlayerManager>();
            GetComponent<AICharacterCombatManager>()
                ?.AwardRunesOnDeath(player, reward);
        }

        /// <summary>Plays one server-selected pivot on every peer.</summary>
        public void ReplicatePivot(bool turnLeft)
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            GetComponentInChildren<AICharacterAnimatorManager>(true)
                ?.PlayPivotTurn(turnLeft);
            PlayPivotClientRpc(turnLeft);
        }

        [ClientRpc]
        private void PlayPivotClientRpc(bool turnLeft)
        {
            if (IsServer)
            {
                return;
            }

            GetComponentInChildren<AICharacterAnimatorManager>(true)
                ?.PlayPivotTurn(turnLeft);
        }
    }
}
