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
    /// Tracks every player's current region and protects the union of all current
    /// and adjacent area Scenes.
    /// </summary>
    [DefaultExecutionOrder(-8400)]
    [RequireComponent(typeof(NetworkObject))]
    public class WorldSceneSubSceneManager : NetworkBehaviour
    {
        private const float k_ActiveSceneTimeoutSeconds = 30f;

        private static readonly WorldSceneLocation[] s_streamableLocations =
        {
            WorldSceneLocation.Area01SubArea00,
            WorldSceneLocation.Area01SubArea01,
            WorldSceneLocation.Area01SubArea02,
            WorldSceneLocation.Area01SubArea03,
            WorldSceneLocation.Area01SubArea04
        };

        private static WorldSceneSubSceneManager s_instance;

        private readonly List<PlayerManager> m_area00Players = new();
        private readonly List<PlayerManager> m_area01Players = new();
        private readonly List<PlayerManager> m_area02Players = new();
        private readonly List<PlayerManager> m_area03Players = new();
        private readonly List<PlayerManager> m_area04Players = new();

        /// <summary>Gets the world region decision manager.</summary>
        public static WorldSceneSubSceneManager Instance => s_instance;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(this);
                return;
            }

            s_instance = this;
        }

        /// <inheritdoc />
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
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

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>Registers a newly spawned player in the world spawn region.</summary>
        public void RegisterPlayerAtDefaultLocation(PlayerManager player)
        {
            if (IsServer && player != null)
            {
                LoadAreaBasedOnCurrentArea(
                    WorldSceneLocation.Area01SubArea00,
                    player);
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
        /// Moves one player between regions, queues required loads, then queues only
        /// unprotected Scenes for unloading.
        /// </summary>
        public bool LoadAreaBasedOnCurrentArea(
            WorldSceneLocation area,
            PlayerManager player)
        {
            if (!IsServer ||
                player == null ||
                !IsStreamableLocation(area) ||
                GetPlayersAtLocation(area).Contains(player))
            {
                return false;
            }

            RemovePlayerFromPreviousLocation(player);
            GetPlayersAtLocation(area).Add(player);

            WorldSceneManager sceneManager = WorldSceneManager.Instance;
            sceneManager?.LoadAdditiveScenes(GetScenesToLoadForLocation(area));
            ReconcileLoadedScenes();
            SetActiveAreaClientRpc(
                area,
                CreateTargetClientRpcParams(player.OwnerClientId));
            return true;
        }

        /// <summary>Returns the persistent Scene ID or exact additive Scene asset name.</summary>
        public static string GetSceneIDFromWorldSceneLocation(
            WorldSceneLocation location)
        {
            return location switch
            {
                WorldSceneLocation.PersistentWorld =>
                    WorldSceneManager.PersistentWorldSceneID,
                WorldSceneLocation.Area01SubArea00 => "Area_01_Sub_Area_00",
                WorldSceneLocation.Area01SubArea01 => "Area_01_Sub_Area_01",
                WorldSceneLocation.Area01SubArea02 => "Area_01_Sub_Area_02",
                WorldSceneLocation.Area01SubArea03 => "Area_01_Sub_Area_03",
                WorldSceneLocation.Area01SubArea04 => "Area_01_Sub_Area_04",
                _ => string.Empty
            };
        }

        /// <summary>Returns the current region plus every directly adjacent region.</summary>
        public static IReadOnlyList<string> GetScenesToLoadForLocation(
            WorldSceneLocation location)
        {
            WorldSceneLocation[] locations = location switch
            {
                WorldSceneLocation.Area01SubArea00 =>
                    new[]
                    {
                        WorldSceneLocation.Area01SubArea00,
                        WorldSceneLocation.Area01SubArea01
                    },
                WorldSceneLocation.Area01SubArea01 =>
                    new[]
                    {
                        WorldSceneLocation.Area01SubArea00,
                        WorldSceneLocation.Area01SubArea01,
                        WorldSceneLocation.Area01SubArea02
                    },
                WorldSceneLocation.Area01SubArea02 =>
                    new[]
                    {
                        WorldSceneLocation.Area01SubArea01,
                        WorldSceneLocation.Area01SubArea02,
                        WorldSceneLocation.Area01SubArea03
                    },
                WorldSceneLocation.Area01SubArea03 =>
                    new[]
                    {
                        WorldSceneLocation.Area01SubArea02,
                        WorldSceneLocation.Area01SubArea03,
                        WorldSceneLocation.Area01SubArea04
                    },
                WorldSceneLocation.Area01SubArea04 =>
                    new[]
                    {
                        WorldSceneLocation.Area01SubArea03,
                        WorldSceneLocation.Area01SubArea04
                    },
                _ => System.Array.Empty<WorldSceneLocation>()
            };

            return locations
                .Select(GetSceneIDFromWorldSceneLocation)
                .Where(sceneID => !string.IsNullOrEmpty(sceneID))
                .ToArray();
        }

        /// <summary>Builds the multiplayer union of Scenes that must remain loaded.</summary>
        public IReadOnlyCollection<string> BuildDoNotUnloadSceneIDs()
        {
            RemoveNullPlayers();
            HashSet<string> protectedScenes = new()
            {
                WorldSceneManager.PersistentWorldSceneID
            };

            foreach (WorldSceneLocation location in s_streamableLocations)
            {
                if (GetPlayersAtLocation(location).Count == 0)
                {
                    continue;
                }

                protectedScenes.UnionWith(GetScenesToLoadForLocation(location));
            }

            return protectedScenes;
        }

        /// <summary>Returns whether current player locations protect one Scene.</summary>
        public bool IsSceneProtected(string sceneID)
        {
            return BuildDoNotUnloadSceneIDs().Contains(sceneID);
        }

        private static bool IsStreamableLocation(WorldSceneLocation location)
        {
            return location != WorldSceneLocation.PersistentWorld &&
                !string.IsNullOrEmpty(GetSceneIDFromWorldSceneLocation(location));
        }

        private List<PlayerManager> GetPlayersAtLocation(
            WorldSceneLocation location)
        {
            return location switch
            {
                WorldSceneLocation.Area01SubArea00 => m_area00Players,
                WorldSceneLocation.Area01SubArea01 => m_area01Players,
                WorldSceneLocation.Area01SubArea02 => m_area02Players,
                WorldSceneLocation.Area01SubArea03 => m_area03Players,
                WorldSceneLocation.Area01SubArea04 => m_area04Players,
                _ => throw new System.ArgumentOutOfRangeException(
                    nameof(location),
                    location,
                    "The persistent world cannot own a regional player list.")
            };
        }

        private void RemovePlayerFromPreviousLocation(PlayerManager player)
        {
            foreach (WorldSceneLocation location in s_streamableLocations)
            {
                GetPlayersAtLocation(location).Remove(player);
            }
        }

        /// <summary>Queues every loaded Scene outside the current multiplayer protection union.</summary>
        public void ReconcileLoadedScenes()
        {
            WorldSceneManager.Instance?.UnloadAllExcept(
                BuildDoNotUnloadSceneIDs());
        }

        private void RemoveNullPlayers()
        {
            foreach (WorldSceneLocation location in s_streamableLocations)
            {
                GetPlayersAtLocation(location).RemoveAll(player => player == null);
            }
        }

        private void ClearPlayerLists()
        {
            foreach (WorldSceneLocation location in s_streamableLocations)
            {
                GetPlayersAtLocation(location).Clear();
            }
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
            WorldSceneLocation area,
            ClientRpcParams clientRpcParams = default)
        {
            StartCoroutine(WaitThenSetActiveScene(area));
        }

        private IEnumerator WaitThenSetActiveScene(WorldSceneLocation area)
        {
            string sceneID = GetSceneIDFromWorldSceneLocation(area);
            float timeoutAt = Time.realtimeSinceStartup + k_ActiveSceneTimeoutSeconds;
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                PlayerManager localPlayer = WorldGameSessionManager.Instance?.Players
                    .FirstOrDefault(player => player != null && player.IsOwner);
                if (localPlayer != null &&
                    localPlayer.IsOwner &&
                    WorldSceneManager.Instance?.IsSceneLoaded(sceneID) == true)
                {
                    Scene scene = SceneManager.GetSceneByName(sceneID);
                    if (scene.IsValid() && scene.isLoaded)
                    {
                        ProbeReferenceVolume.instance?.SetActiveScene(scene);
                    }

                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                $"Timed out waiting to activate APV lighting for {sceneID}.");
        }
    }
}
