using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Tracks every player's logical world location and protects the union of all
    /// physical Scenes required by those locations.
    /// </summary>
    [DefaultExecutionOrder(-8400)]
    [RequireComponent(typeof(NetworkObject))]
    public class WorldSceneSubSceneManager : NetworkBehaviour
    {
        private const float k_ActiveSceneTimeoutSeconds = 30f;
        private const string k_WorldLocationResourcesPath = "World Locations";

        private static WorldSceneSubSceneManager s_instance;

        [SerializeField] private WorldLocationSceneSet m_defaultWorldLocation;
        [SerializeField] private List<WorldLocationSceneSet> m_worldLocations =
            new();

        private readonly Dictionary<
            WorldLocationSceneSet,
            List<PlayerManager>> m_playersInLocation = new();
        private bool m_isLocationRegistryInitialized;

        /// <summary>Gets the world location decision manager.</summary>
        public static WorldSceneSubSceneManager Instance => s_instance;

        /// <summary>Gets every registered logical location and its current players.</summary>
        public IReadOnlyDictionary<WorldLocationSceneSet, List<PlayerManager>>
            PlayersInLocation => m_playersInLocation;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(this);
                return;
            }

            s_instance = this;
            EnsureLocationRegistry();
        }

        /// <inheritdoc />
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            EnsureLocationRegistry();
            if (!IsServer || WorldGameSessionManager.Instance == null)
            {
                return;
            }

            foreach (PlayerManager player in WorldGameSessionManager.Instance.Players)
            {
                RegisterPlayerAtDefaultLocation(player);
            }
        }

        /// <inheritdoc />
        public override void OnNetworkDespawn()
        {
            ClearPlayerLists();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }

            base.OnDestroy();
        }

        /// <summary>Registers a newly spawned player in the configured spawn location.</summary>
        public void RegisterPlayerAtDefaultLocation(PlayerManager player)
        {
            if (!IsServer || player == null)
            {
                return;
            }

            EnsureLocationRegistry();
            if (m_defaultWorldLocation != null)
            {
                LoadAreaBasedOnCurrentArea(m_defaultWorldLocation, player);
            }
        }

        /// <summary>Removes a disconnected player and recalculates protected Scenes.</summary>
        public void RemovePlayer(PlayerManager player)
        {
            if (!IsServer || player == null)
            {
                return;
            }

            RemovePlayerFromPreviousLocation(player);
            ReconcileLoadedScenes();
        }

        /// <summary>
        /// Moves one player to a data-driven location, queues all required loads,
        /// then queues only unprotected Scenes for unloading.
        /// </summary>
        public bool LoadAreaBasedOnCurrentArea(
            WorldLocationSceneSet worldLocation,
            PlayerManager player)
        {
            if (!IsServer || player == null || worldLocation == null)
            {
                return false;
            }

            EnsureLocationRegistered(worldLocation);
            List<PlayerManager> players = m_playersInLocation[worldLocation];
            RemoveNullPlayers();
            if (players.Contains(player))
            {
                return false;
            }

            RemovePlayerFromPreviousLocation(player);
            players.Add(player);
            player.SetAreaCurrentlyIn(worldLocation);

            WorldSceneManager.Instance?.LoadAdditiveScenes(
                worldLocation.GetRequiredSceneIDsForWorldLocation());
            ReconcileLoadedScenes();
            string activeSceneID = worldLocation.PrimarySceneID;
            if (!string.IsNullOrEmpty(activeSceneID))
            {
                SetActiveAreaClientRpc(
                    activeSceneID,
                    worldLocation.LocationID,
                    CreateTargetClientRpcParams(player.OwnerClientId));
            }

            return true;
        }

        /// <summary>Moves one legacy trigger through its matching Scene Set asset.</summary>
        public bool LoadAreaBasedOnCurrentArea(
            WorldSceneLocation legacyLocation,
            PlayerManager player)
        {
            return LoadAreaBasedOnCurrentArea(
                ResolveWorldLocation(legacyLocation),
                player);
        }

        /// <summary>Finds the data asset associated with one migrated enum value.</summary>
        public WorldLocationSceneSet ResolveWorldLocation(
            WorldSceneLocation legacyLocation)
        {
            EnsureLocationRegistry();
            return m_worldLocations.FirstOrDefault(location =>
                location != null &&
                location.LegacyLocation == legacyLocation);
        }

        /// <summary>Finds the data asset with one stable location identifier.</summary>
        public WorldLocationSceneSet ResolveWorldLocation(string locationID)
        {
            if (string.IsNullOrWhiteSpace(locationID))
            {
                return null;
            }

            EnsureLocationRegistry();
            string normalizedLocationID = locationID.Trim();
            return m_worldLocations.FirstOrDefault(location =>
                location != null &&
                location.LocationID == normalizedLocationID);
        }

        /// <summary>Returns the persistent Scene ID or a legacy location's primary Scene.</summary>
        public static string GetSceneIDFromWorldSceneLocation(
            WorldSceneLocation legacyLocation)
        {
            if (legacyLocation == WorldSceneLocation.PersistentWorld)
            {
                return WorldSceneManager.PersistentWorldSceneID;
            }

            return FindLocationAsset(legacyLocation)?.PrimarySceneID ??
                string.Empty;
        }

        /// <summary>Returns all Scenes configured by one migrated location asset.</summary>
        public static IReadOnlyList<string> GetScenesToLoadForLocation(
            WorldSceneLocation legacyLocation)
        {
            WorldLocationSceneSet location = FindLocationAsset(legacyLocation);
            return location != null
                ? location.GetRequiredSceneIDsForWorldLocation()
                : System.Array.Empty<string>();
        }

        /// <summary>Builds the multiplayer union of Scenes that must remain loaded.</summary>
        public IReadOnlyCollection<string> BuildDoNotUnloadSceneIDs()
        {
            EnsureLocationRegistry();
            RemoveNullPlayers();
            HashSet<string> protectedScenes = new()
            {
                WorldSceneManager.PersistentWorldSceneID
            };

            foreach (KeyValuePair<WorldLocationSceneSet, List<PlayerManager>>
                locationPlayers in m_playersInLocation)
            {
                if (locationPlayers.Key == null ||
                    locationPlayers.Value.Count == 0)
                {
                    continue;
                }

                protectedScenes.UnionWith(
                    locationPlayers.Key.GetRequiredSceneIDsForWorldLocation());
            }

            return protectedScenes;
        }

        /// <summary>Returns whether current player locations protect one Scene.</summary>
        public bool IsSceneProtected(string sceneID)
        {
            return BuildDoNotUnloadSceneIDs().Contains(sceneID);
        }

        /// <summary>Queues every loaded Scene outside the multiplayer protection union.</summary>
        public void ReconcileLoadedScenes()
        {
            WorldSceneManager.Instance?.UnloadAllExcept(
                BuildDoNotUnloadSceneIDs());
        }

        private void EnsureLocationRegistry()
        {
            if (m_isLocationRegistryInitialized)
            {
                return;
            }

            foreach (WorldLocationSceneSet location in
                Resources.LoadAll<WorldLocationSceneSet>(
                    k_WorldLocationResourcesPath))
            {
                if (location != null && !m_worldLocations.Contains(location))
                {
                    m_worldLocations.Add(location);
                }
            }

            m_worldLocations.RemoveAll(location => location == null);
            foreach (WorldLocationSceneSet location in m_worldLocations)
            {
                EnsureLocationRegistered(location);
            }

            if (m_defaultWorldLocation == null)
            {
                m_defaultWorldLocation = m_worldLocations.FirstOrDefault(
                    location => location.LegacyLocation ==
                        WorldSceneLocation.Area01SubArea00) ??
                    m_worldLocations.FirstOrDefault();
            }

            m_isLocationRegistryInitialized = true;
        }

        private void EnsureLocationRegistered(WorldLocationSceneSet location)
        {
            if (location != null && !m_playersInLocation.ContainsKey(location))
            {
                m_playersInLocation.Add(location, new List<PlayerManager>());
            }
        }

        private void RemovePlayerFromPreviousLocation(PlayerManager player)
        {
            foreach (List<PlayerManager> players in m_playersInLocation.Values)
            {
                players.Remove(player);
            }

            player.SetAreaCurrentlyIn(null);
        }

        private void RemoveNullPlayers()
        {
            foreach (List<PlayerManager> players in m_playersInLocation.Values)
            {
                players.RemoveAll(player => player == null);
            }
        }

        private void ClearPlayerLists()
        {
            foreach (List<PlayerManager> players in m_playersInLocation.Values)
            {
                foreach (PlayerManager player in players)
                {
                    if (player != null)
                    {
                        player.SetAreaCurrentlyIn(null);
                    }
                }

                players.Clear();
            }
        }

        private static WorldLocationSceneSet FindLocationAsset(
            WorldSceneLocation legacyLocation)
        {
            if (s_instance != null)
            {
                return s_instance.ResolveWorldLocation(legacyLocation);
            }

            return Resources.LoadAll<WorldLocationSceneSet>(
                    k_WorldLocationResourcesPath)
                .FirstOrDefault(location =>
                    location != null &&
                    location.LegacyLocation == legacyLocation);
        }

        private static ClientRpcParams CreateTargetClientRpcParams(ulong clientID)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientID }
                }
            };
        }

        [ClientRpc]
        private void SetActiveAreaClientRpc(
            string activeSceneID,
            string worldLocationID,
            ClientRpcParams clientRpcParams = default)
        {
            StartCoroutine(WaitThenSetActiveScene(
                activeSceneID,
                worldLocationID));
        }

        private IEnumerator WaitThenSetActiveScene(
            string activeSceneID,
            string worldLocationID)
        {
            float timeoutAt = Time.realtimeSinceStartup +
                k_ActiveSceneTimeoutSeconds;
            bool localLocationAssigned = false;
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                PlayerManager localPlayer = WorldGameSessionManager.Instance?.Players
                    .FirstOrDefault(player => player != null && player.IsOwner);
                if (localPlayer != null && !localLocationAssigned)
                {
                    WorldLocationSceneSet localLocation =
                        ResolveWorldLocation(worldLocationID);
                    if (localLocation == null)
                    {
                        Debug.LogError(
                            $"Could not resolve local world location " +
                            $"{worldLocationID}.");
                        yield break;
                    }

                    localPlayer.SetAreaCurrentlyIn(localLocation);
                    localLocationAssigned = true;
                    WorldSceneManager.Instance?.CheckForRequiredRenderers();
                }

                if (localLocationAssigned &&
                    WorldSceneManager.Instance?.IsSceneLoaded(activeSceneID) ==
                        true)
                {
                    Scene scene = SceneManager.GetSceneByName(activeSceneID);
                    if (scene.IsValid() && scene.isLoaded)
                    {
                        ProbeReferenceVolume.instance?.SetActiveScene(scene);
                    }

                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                $"Timed out waiting to activate APV lighting for " +
                $"{activeSceneID}.");
        }
    }
}
