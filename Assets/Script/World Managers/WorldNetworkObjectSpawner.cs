using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>Spawns one authored persistent network prefab at this scene anchor.</summary>
    public sealed class WorldNetworkObjectSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject m_networkPrefab;

        private NetworkObject m_spawnedInstance;

        public NetworkObject NetworkPrefab => m_networkPrefab;
        public NetworkObject SpawnedInstance => m_spawnedInstance;

        private IEnumerator Start()
        {
            while (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsListening)
            {
                yield return null;
            }

            if (!NetworkManager.Singleton.IsServer || m_networkPrefab == null)
            {
                yield break;
            }

            m_spawnedInstance = Instantiate(
                m_networkPrefab,
                transform.position,
                transform.rotation);
            m_spawnedInstance.Spawn(true);
        }
    }
}
