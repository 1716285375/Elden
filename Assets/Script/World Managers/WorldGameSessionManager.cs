using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Tracks every currently spawned player so late-joining clients can rebuild
    /// already-synchronized equipment presentation.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public class WorldGameSessionManager : MonoBehaviour
    {
        private const float k_ClientRefreshDelaySeconds = 0.5f;

        private static WorldGameSessionManager s_instance;

        private readonly List<PlayerManager> m_players = new();

        /// <summary>Gets the persistent active-player session instance.</summary>
        public static WorldGameSessionManager Instance => s_instance;

        /// <summary>Gets the currently spawned players in registration order.</summary>
        public IReadOnlyList<PlayerManager> Players => m_players;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// Registers a spawned player without allowing duplicates.
        /// </summary>
        public void AddPlayer(PlayerManager player)
        {
            if (player == null || m_players.Contains(player))
            {
                return;
            }

            m_players.Add(player);
        }

        /// <summary>
        /// Removes a despawned or disconnected player.
        /// </summary>
        public void RemovePlayer(PlayerManager player)
        {
            m_players.Remove(player);
        }

        private void OnClientConnected(ulong clientId)
        {
            StartCoroutine(RefreshOtherPlayerCharacters());
        }

        private IEnumerator RefreshOtherPlayerCharacters()
        {
            yield return new WaitForSeconds(k_ClientRefreshDelaySeconds);
            RemoveNullPlayers();
            foreach (PlayerManager player in m_players)
            {
                player?.LoadOtherPlayerCharacter();
            }
        }

        private void RemoveNullPlayers()
        {
            m_players.RemoveAll(player => player == null);
        }
    }
}
