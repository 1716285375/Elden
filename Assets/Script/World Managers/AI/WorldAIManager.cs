using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ZZ
{
    /// <summary>
    /// Spawns server-owned enemies and tracks their network lifecycle in the active world.
    /// </summary>
    [DefaultExecutionOrder(-8000)]
    public class WorldAIManager : MonoBehaviour
    {
        private const float k_SpawnSampleDistance = 4f;

        private static WorldAIManager s_instance;

        [SerializeField] private GameObject m_aiCharacterPrefab;

        private readonly List<AICharacterManager> m_spawnedCharacters = new();

        private bool m_hasSpawnedCharacters;

        /// <summary>Gets the World AI Manager in the currently loaded gameplay scene.</summary>
        public static WorldAIManager Instance => s_instance;

        /// <summary>Gets every currently spawned AI registered on this peer.</summary>
        public IReadOnlyList<AICharacterManager> SpawnedCharacters =>
            m_spawnedCharacters;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
        }

        private void Start()
        {
            StartCoroutine(SpawnWhenServerIsReady());
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>Registers an AI spawned on this peer without duplicates.</summary>
        public void RegisterAI(AICharacterManager aiCharacter)
        {
            if (aiCharacter != null && !m_spawnedCharacters.Contains(aiCharacter))
            {
                m_spawnedCharacters.Add(aiCharacter);
            }
        }

        /// <summary>Removes a despawned AI from this peer's world list.</summary>
        public void UnregisterAI(AICharacterManager aiCharacter)
        {
            m_spawnedCharacters.Remove(aiCharacter);
        }

        private IEnumerator SpawnWhenServerIsReady()
        {
            while (!m_hasSpawnedCharacters)
            {
                NetworkManager networkManager = NetworkManager.Singleton;
                if (networkManager != null &&
                    networkManager.IsListening &&
                    networkManager.IsServer)
                {
                    SpawnAICharacters();
                    yield break;
                }

                yield return null;
            }
        }

        private void SpawnAICharacters()
        {
            if (m_hasSpawnedCharacters || m_aiCharacterPrefab == null)
            {
                return;
            }

            NetworkObject prefabNetworkObject =
                m_aiCharacterPrefab.GetComponent<NetworkObject>();
            if (prefabNetworkObject == null)
            {
                Debug.LogError("The AI prefab must contain a NetworkObject.", this);
                return;
            }

            AISpawnPoint[] spawnPoints = GetComponentsInChildren<AISpawnPoint>(true)
                .OrderBy(spawnPoint => spawnPoint.transform.GetSiblingIndex())
                .ToArray();
            foreach (AISpawnPoint spawnPoint in spawnPoints)
            {
                if (!TryResolveSpawnPosition(spawnPoint.transform.position, out Vector3 position))
                {
                    Debug.LogWarning(
                        $"No NavMesh was found near AI spawn point {spawnPoint.name}.",
                        spawnPoint);
                    continue;
                }

                GameObject instance = Instantiate(
                    m_aiCharacterPrefab,
                    position,
                    spawnPoint.transform.rotation);
                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                networkObject.Spawn(true);
            }

            m_hasSpawnedCharacters = true;
        }

        private static bool TryResolveSpawnPosition(
            Vector3 sourcePosition,
            out Vector3 spawnPosition)
        {
            if (NavMesh.SamplePosition(
                    sourcePosition,
                    out NavMeshHit hit,
                    k_SpawnSampleDistance,
                    NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
                return true;
            }

            spawnPosition = sourcePosition;
            return false;
        }
    }
}
