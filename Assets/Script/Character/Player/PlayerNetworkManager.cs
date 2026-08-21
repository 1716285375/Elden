using Unity.Collections;
using Unity.Netcode;

namespace ZZ
{
    public class PlayerNetworkManager : CharacterNetworkManager
    {
        private readonly NetworkVariable<FixedString64Bytes> m_characterName =
            new NetworkVariable<FixedString64Bytes>(
                new FixedString64Bytes("Unnamed"),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Gets the owner-written character name replicated to every client.
        /// </summary>
        public NetworkVariable<FixedString64Bytes> CharacterName => m_characterName;

        public NetworkVariable<bool> IsSprinting = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResetOwnedSprintState();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            ResetOwnedSprintState();
        }

        private void ResetOwnedSprintState()
        {
            if (IsOwner && IsSpawned)
            {
                IsSprinting.Value = false;
            }
        }
    }
}
