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

        [Header("HOST REVIVAL")]
        [SerializeField, Min(0f)] private float m_hostReviveDelaySeconds = 5f;

        private Coroutine m_revivalCoroutine;

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
            StopRevivalCoroutine();
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
            if (NetworkManager.Singleton?.IsServer == true)
            {
                WorldSceneSubSceneManager.Instance
                    ?.RegisterPlayerAtDefaultLocation(player);
            }
        }

        /// <summary>
        /// Removes a despawned or disconnected player.
        /// </summary>
        public void RemovePlayer(PlayerManager player)
        {
            if (NetworkManager.Singleton?.IsServer == true)
            {
                WorldSceneSubSceneManager.Instance?.RemovePlayer(player);
            }

            m_players.Remove(player);
        }

        /// <summary>Starts one delayed revival for the locally owned Host player.</summary>
        public void ReviveHost()
        {
            ReviveHost(FindHostPlayer());
        }

        /// <summary>Restarts the delayed revival for the supplied Host-owned player.</summary>
        public void ReviveHost(PlayerManager hostPlayer)
        {
            if (NetworkManager.Singleton?.IsHost != true ||
                hostPlayer == null ||
                !hostPlayer.IsOwner ||
                !hostPlayer.IsServer ||
                !hostPlayer.IsDead)
            {
                return;
            }

            StopRevivalCoroutine();
            m_revivalCoroutine = StartCoroutine(
                ReviveHostCoroutine(hostPlayer));
        }

        private IEnumerator ReviveHostCoroutine(PlayerManager hostPlayer)
        {
            yield return new WaitForSecondsRealtime(m_hostReviveDelaySeconds);

            PlayerUILoadingScreenManager loadingScreen =
                PlayerUIManager.Instance?.PlayerUILoadingScreenManager;
            loadingScreen?.ActivateLoadingScreen();
            yield return null;

            if (hostPlayer != null && hostPlayer.IsOwner && hostPlayer.IsDead)
            {
                hostPlayer.ReviveCharacter();
                CharacterSaveData saveData =
                    WorldSaveGameManager.Instance?.CurrentCharacterData;
                SiteOfGraceInteractable respawnSite =
                    WorldObjectManager.Instance?.GetRespawnSiteOfGrace(
                        saveData?.LastSiteOfGraceRestedAt ?? 0);
                respawnSite?.TeleportLocalPlayer();

                WorldAIManager worldAIManager = WorldAIManager.Instance;
                worldAIManager?.ResetAllCharacters();
                while (worldAIManager?.IsPerformingLoadingOperation == true)
                {
                    yield return null;
                }

                WorldSaveGameManager saveGameManager =
                    WorldSaveGameManager.Instance;
                if (saveGameManager?.CanSaveGame == true)
                {
                    saveGameManager.SaveGame();
                }
            }

            loadingScreen?.DeactivateLoadingScreen();
            m_revivalCoroutine = null;
        }

        private PlayerManager FindHostPlayer()
        {
            RemoveNullPlayers();
            return m_players.Find(player =>
                player.IsOwner && player.IsServer);
        }

        private void StopRevivalCoroutine()
        {
            if (m_revivalCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_revivalCoroutine);
            m_revivalCoroutine = null;
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
