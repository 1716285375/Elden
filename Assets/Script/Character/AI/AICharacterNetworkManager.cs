using Unity.Netcode;

namespace ZZ
{
    /// <summary>
    /// Replicates server-owned AI state and discrete pivot presentation.
    /// </summary>
    public class AICharacterNetworkManager : CharacterNetworkManager
    {
        public NetworkVariable<AICharacterStateId> CurrentAIState =
            new NetworkVariable<AICharacterStateId>(
                AICharacterStateId.Idle,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>Publishes an AI state transition from the server.</summary>
        public void SetAIState(AICharacterStateId stateId)
        {
            if (IsSpawned && IsServer && CurrentAIState.Value != stateId)
            {
                CurrentAIState.Value = stateId;
            }
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
