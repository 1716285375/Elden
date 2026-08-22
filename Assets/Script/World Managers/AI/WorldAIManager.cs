using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Spawns server-owned enemies and tracks their network lifecycle in the active world.
    /// </summary>
    [DefaultExecutionOrder(-8000)]
    public class WorldAIManager : MonoBehaviour
    {
        private static WorldAIManager s_instance;

        private readonly List<AICharacterSpawner> m_characterSpawners = new();
        private readonly List<AICharacterManager> m_spawnedCharacters = new();

        private bool m_hasSpawnedCharacters;

        /// <summary>Gets the World AI Manager in the currently loaded gameplay scene.</summary>
        public static WorldAIManager Instance => s_instance;

        /// <summary>Gets every currently spawned AI registered on this peer.</summary>
        public IReadOnlyList<AICharacterManager> SpawnedCharacters =>
            m_spawnedCharacters;

        /// <summary>Gets every registered spawn point in this gameplay scene.</summary>
        public IReadOnlyList<AICharacterSpawner> CharacterSpawners =>
            m_characterSpawners;

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

        /// <summary>Registers one scene-authored spawner without duplicates.</summary>
        public void RegisterSpawner(AICharacterSpawner characterSpawner)
        {
            if (characterSpawner == null ||
                m_characterSpawners.Contains(characterSpawner))
            {
                return;
            }

            m_characterSpawners.Add(characterSpawner);
            if (m_hasSpawnedCharacters && IsServerReady())
            {
                characterSpawner.AttemptToSpawnCharacter();
            }
        }

        /// <summary>Removes a destroyed scene spawner from the world registry.</summary>
        public void UnregisterSpawner(AICharacterSpawner characterSpawner)
        {
            m_characterSpawners.Remove(characterSpawner);
        }

        private IEnumerator SpawnWhenServerIsReady()
        {
            while (!m_hasSpawnedCharacters)
            {
                if (IsServerReady())
                {
                    SpawnAICharacters();
                    yield break;
                }

                yield return null;
            }
        }

        private void SpawnAICharacters()
        {
            if (m_hasSpawnedCharacters)
            {
                return;
            }

            AICharacterSpawner[] orderedSpawners = m_characterSpawners
                .Where(spawner => spawner != null)
                .OrderBy(spawner => spawner.transform.GetSiblingIndex())
                .ToArray();
            foreach (AICharacterSpawner characterSpawner in orderedSpawners)
            {
                characterSpawner.AttemptToSpawnCharacter();
            }

            m_hasSpawnedCharacters = true;
        }

        private static bool IsServerReady()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return networkManager != null &&
                networkManager.IsListening &&
                networkManager.IsServer;
        }
    }
}
