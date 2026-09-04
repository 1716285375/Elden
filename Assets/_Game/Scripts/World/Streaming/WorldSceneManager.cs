using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Serializes server-authoritative additive Scene operations and mirrors their
    /// completed local state on every peer.
    /// </summary>
    [DefaultExecutionOrder(-8500)]
    [RequireComponent(typeof(NetworkObject))]
    public class WorldSceneManager : NetworkBehaviour
    {
        private const float k_SceneEventRetryTimeoutSeconds = 30f;

        /// <summary>The world Scene that remains loaded for the whole gameplay session.</summary>
        public const string PersistentWorldSceneID =
            WorldScenePathLayout.MasterSceneName;

        private static WorldSceneManager s_instance;

        private readonly List<Scene> m_loadedScenes = new();
        private readonly Queue<string> m_queuedSceneIDs = new();
        private readonly Queue<string> m_queuedUnloadSceneIDs = new();
        private readonly HashSet<string> m_pendingLoadSceneIDs = new();
        private readonly HashSet<string> m_pendingUnloadSceneIDs = new();

        [SerializeField, Min(0f)] private float m_loadOperationInterval = 0.1f;
        [SerializeField, Min(0f)] private float m_unloadOperationInterval = 0.5f;

        private Coroutine m_sceneQueueCoroutine;
        private Coroutine m_requiredRenderersCoroutine;
        private bool m_sceneIsLoading;
        private bool m_sceneIsUnloading;

        /// <summary>Gets the persistent world Scene manager.</summary>
        public static WorldSceneManager Instance => s_instance;

        /// <summary>Gets Scenes that have completed loading on this peer.</summary>
        public IReadOnlyList<Scene> LoadedScenes => m_loadedScenes;

        /// <summary>Gets whether this peer is processing a Scene load event.</summary>
        public bool SceneIsLoading => m_sceneIsLoading;

        /// <summary>Gets whether this peer is processing a Scene unload event.</summary>
        public bool SceneIsUnloading => m_sceneIsUnloading;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            RefreshLoadedScenes();
        }

        /// <inheritdoc />
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            DontDestroyOnLoad(gameObject);
            RefreshLoadedScenes();
            if (NetworkManager?.SceneManager != null)
            {
                NetworkManager.SceneManager.OnSceneEvent += HandleNetworkSceneEvent;
            }
        }

        /// <inheritdoc />
        public override void OnNetworkDespawn()
        {
            UnsubscribeFromSceneEvents();
            StopSceneQueue();
            StartCoroutine(UnloadAllAdditiveScenesNonNetwork());
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            UnsubscribeFromSceneEvents();
            if (s_instance == this)
            {
                s_instance = null;
            }

            base.OnDestroy();
        }

        /// <summary>Returns whether one Scene has completed loading on this peer.</summary>
        public bool IsSceneLoaded(string sceneID)
        {
            if (string.IsNullOrWhiteSpace(sceneID))
            {
                return false;
            }

            return m_loadedScenes.Any(scene =>
                scene.IsValid() &&
                scene.isLoaded &&
                scene.name == sceneID);
        }

        /// <summary>Queues one additive Scene load on the server.</summary>
        public void LoadAdditiveScene(string sceneID)
        {
            if (!CanServerManageScenes() ||
                string.IsNullOrWhiteSpace(sceneID) ||
                IsSceneLoaded(sceneID) ||
                !m_pendingLoadSceneIDs.Add(sceneID))
            {
                return;
            }

            m_pendingUnloadSceneIDs.Remove(sceneID);
            m_queuedSceneIDs.Enqueue(sceneID);
            StartSceneQueueIfNeeded();
        }

        /// <summary>Adds multiple Scene loads to the existing queue.</summary>
        public void LoadAdditiveScenes(IEnumerable<string> sceneIDs)
        {
            if (sceneIDs == null)
            {
                return;
            }

            foreach (string sceneID in sceneIDs)
            {
                LoadAdditiveScene(sceneID);
            }
        }

        /// <summary>Queues one additive Scene unload on the server.</summary>
        public void UnloadAdditiveScene(string sceneID)
        {
            if (!CanServerManageScenes() ||
                string.IsNullOrWhiteSpace(sceneID) ||
                sceneID == PersistentWorldSceneID ||
                !IsSceneLoaded(sceneID) ||
                !m_pendingUnloadSceneIDs.Add(sceneID))
            {
                return;
            }

            m_queuedUnloadSceneIDs.Enqueue(sceneID);
            StartSceneQueueIfNeeded();
        }

        /// <summary>Adds multiple Scene unloads to the existing queue.</summary>
        public void UnloadAdditiveScenes(IEnumerable<string> sceneIDs)
        {
            if (sceneIDs == null)
            {
                return;
            }

            foreach (string sceneID in sceneIDs)
            {
                UnloadAdditiveScene(sceneID);
            }
        }

        /// <summary>Queues every loaded area Scene not present in the protected set.</summary>
        public void UnloadAllExcept(IEnumerable<string> protectedSceneIDs)
        {
            HashSet<string> protectedScenes = protectedSceneIDs != null
                ? new HashSet<string>(protectedSceneIDs)
                : new HashSet<string>();
            protectedScenes.Add(PersistentWorldSceneID);

            foreach (Scene scene in m_loadedScenes.ToArray())
            {
                if (scene.IsValid() &&
                    scene.isLoaded &&
                    !protectedScenes.Contains(scene.name))
                {
                    UnloadAdditiveScene(scene.name);
                }
            }
        }

        /// <summary>
        /// Restarts the local Renderer calculation using the latest owned-player
        /// location. Scene loading remains controlled by the server-side union.
        /// </summary>
        public void CheckForRequiredRenderers()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (m_requiredRenderersCoroutine != null)
            {
                StopCoroutine(m_requiredRenderersCoroutine);
            }

            m_requiredRenderersCoroutine = StartCoroutine(
                CheckForRequiredRenderersAfterSceneRegistration());
        }

        /// <summary>Maps a configured Scene name or path to its Build Index.</summary>
        public static int GetBuildIndexFromSceneID(string sceneID)
        {
            if (string.IsNullOrWhiteSpace(sceneID))
            {
                return -1;
            }

            string normalizedSceneID = sceneID.Trim();
            int directBuildIndex = SceneUtility.GetBuildIndexByScenePath(
                normalizedSceneID);
            if (directBuildIndex >= 0)
            {
                return directBuildIndex;
            }

            for (int buildIndex = 0;
                buildIndex < SceneManager.sceneCountInBuildSettings;
                buildIndex++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(
                    buildIndex);
                if (scenePath.EndsWith(
                        $"/{normalizedSceneID}.unity",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return SceneUtility.GetBuildIndexByScenePath(scenePath);
                }
            }

            return -1;
        }

        private bool CanServerManageScenes()
        {
            return IsSpawned &&
                IsServer &&
                NetworkManager != null &&
                NetworkManager.IsListening &&
                NetworkManager.SceneManager != null;
        }

        private void StartSceneQueueIfNeeded()
        {
            if (m_sceneQueueCoroutine == null)
            {
                m_sceneQueueCoroutine = StartCoroutine(ProcessSceneQueues());
            }
        }

        private IEnumerator ProcessSceneQueues()
        {
            while (m_queuedSceneIDs.Count > 0 || m_queuedUnloadSceneIDs.Count > 0)
            {
                while (m_sceneIsLoading || m_sceneIsUnloading)
                {
                    yield return null;
                }

                if (m_queuedSceneIDs.Count > 0)
                {
                    string sceneID = m_queuedSceneIDs.Dequeue();
                    if (!IsSceneLoaded(sceneID) && ShouldLoadScene(sceneID))
                    {
                        yield return LoadQueuedScene(sceneID);
                        WorldSceneSubSceneManager.Instance?.ReconcileLoadedScenes();
                        yield return WaitForQueueInterval(
                            m_loadOperationInterval);
                    }

                    m_pendingLoadSceneIDs.Remove(sceneID);
                    continue;
                }

                string unloadSceneID = m_queuedUnloadSceneIDs.Dequeue();
                if (CanUnloadScene(unloadSceneID))
                {
                    yield return UnloadQueuedScene(unloadSceneID);
                    yield return WaitForQueueInterval(
                        m_unloadOperationInterval);
                }

                m_pendingUnloadSceneIDs.Remove(unloadSceneID);
            }

            m_sceneQueueCoroutine = null;
        }

        private IEnumerator LoadQueuedScene(string sceneID)
        {
            float retryUntil = Time.realtimeSinceStartup +
                k_SceneEventRetryTimeoutSeconds;
            while (Time.realtimeSinceStartup < retryUntil)
            {
                SceneEventProgressStatus status =
                    NetworkManager.SceneManager.LoadScene(
                        sceneID,
                        LoadSceneMode.Additive);
                if (status == SceneEventProgressStatus.SceneEventInProgress)
                {
                    yield return null;
                    continue;
                }

                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError(
                        $"Could not load additive Scene {sceneID}: {status}.");
                    yield break;
                }

                m_sceneIsLoading = true;
                while (m_sceneIsLoading)
                {
                    yield return null;
                }

                yield break;
            }

            Debug.LogError(
                $"Timed out waiting to load additive Scene {sceneID}.");
        }

        private IEnumerator UnloadQueuedScene(string sceneID)
        {
            Scene scene = SceneManager.GetSceneByName(sceneID);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield break;
            }

            float retryUntil = Time.realtimeSinceStartup +
                k_SceneEventRetryTimeoutSeconds;
            while (Time.realtimeSinceStartup < retryUntil)
            {
                SceneEventProgressStatus status =
                    NetworkManager.SceneManager.UnloadScene(scene);
                if (status == SceneEventProgressStatus.SceneEventInProgress)
                {
                    yield return null;
                    continue;
                }

                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError(
                        $"Could not unload additive Scene {sceneID}: {status}.");
                    yield break;
                }

                m_sceneIsUnloading = true;
                while (m_sceneIsUnloading)
                {
                    yield return null;
                }

                yield break;
            }

            Debug.LogError(
                $"Timed out waiting to unload additive Scene {sceneID}.");
        }

        private static IEnumerator WaitForQueueInterval(float waitTime)
        {
            if (waitTime > 0f && !LoadingScreenIsActive())
            {
                yield return new WaitForSecondsRealtime(waitTime);
            }
        }

        private static bool LoadingScreenIsActive()
        {
            return PlayerUIManager.Instance?.PlayerUILoadingScreenManager
                ?.IsLoadingScreenActive == true;
        }

        private bool CanUnloadScene(string sceneID)
        {
            return sceneID != PersistentWorldSceneID &&
                IsSceneLoaded(sceneID) &&
                WorldSceneSubSceneManager.Instance?.IsSceneProtected(sceneID) != true;
        }

        private static bool ShouldLoadScene(string sceneID)
        {
            WorldSceneSubSceneManager subSceneManager =
                WorldSceneSubSceneManager.Instance;
            return subSceneManager == null ||
                subSceneManager.IsSceneProtected(sceneID);
        }

        private void HandleNetworkSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                    m_sceneIsLoading = true;
                    break;
                case SceneEventType.LoadComplete:
                    if (sceneEvent.ClientId == NetworkManager.LocalClientId)
                    {
                        AddLoadedScene(sceneEvent.SceneName, sceneEvent.Scene);
                        CheckForRequiredRenderers();
                        if (!IsServer)
                        {
                            m_sceneIsLoading = false;
                        }
                    }
                    break;
                case SceneEventType.LoadEventCompleted:
                    AddLoadedScene(sceneEvent.SceneName, sceneEvent.Scene);
                    m_sceneIsLoading = false;
                    CheckForRequiredRenderers();
                    break;
                case SceneEventType.Unload:
                    m_sceneIsUnloading = true;
                    break;
                case SceneEventType.UnloadComplete:
                    if (sceneEvent.ClientId == NetworkManager.LocalClientId)
                    {
                        RemoveLoadedScene(sceneEvent.SceneName);
                        CheckForRequiredRenderers();
                        if (!IsServer)
                        {
                            m_sceneIsUnloading = false;
                        }
                    }
                    break;
                case SceneEventType.UnloadEventCompleted:
                    RemoveLoadedScene(sceneEvent.SceneName);
                    m_sceneIsUnloading = false;
                    CheckForRequiredRenderers();
                    break;
            }
        }

        private void AddLoadedScene(string sceneID, Scene scene)
        {
            Scene loadedScene = scene.IsValid()
                ? scene
                : SceneManager.GetSceneByName(sceneID);
            if (!loadedScene.IsValid() ||
                !loadedScene.isLoaded ||
                IsSceneLoaded(loadedScene.name))
            {
                return;
            }

            m_loadedScenes.Add(loadedScene);
        }

        private void RemoveLoadedScene(string sceneID)
        {
            m_loadedScenes.RemoveAll(scene =>
                !scene.IsValid() ||
                !scene.isLoaded ||
                scene.name == sceneID);
        }

        private void RefreshLoadedScenes()
        {
            m_loadedScenes.Clear();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.IsValid() && scene.isLoaded)
                {
                    m_loadedScenes.Add(scene);
                }
            }
        }

        private IEnumerator CheckForRequiredRenderersAfterSceneRegistration()
        {
            yield return null;

            PlayerManager localPlayer = PlayerUIManager.Instance?.LocalPlayer ??
                WorldGameSessionManager.Instance?.Players.FirstOrDefault(
                    player => player != null && player.IsOwner);
            WorldLocationSceneSet currentLocation =
                localPlayer?.AreaCurrentlyIn;
            WorldLocationManager locationManager = WorldLocationManager.Instance;
            if (currentLocation == null || locationManager == null)
            {
                m_requiredRenderersCoroutine = null;
                yield break;
            }

            HashSet<int> requiredSceneBuildIndexes = currentLocation
                .GetRequiredSceneIDsForWorldLocation()
                .Select(GetBuildIndexFromSceneID)
                .Where(buildIndex => buildIndex >= 0)
                .ToHashSet();
            foreach (WorldLocationRendererManager rendererManager in
                locationManager.WorldLocationRenderers.ToArray())
            {
                if (rendererManager == null)
                {
                    continue;
                }

                rendererManager.EnableRootObjectsForRuntime();
                rendererManager.ToggleAllMeshRenderers(
                    requiredSceneBuildIndexes.Contains(
                        rendererManager.RendererSceneID));
            }

            m_requiredRenderersCoroutine = null;
        }

        private void UnsubscribeFromSceneEvents()
        {
            if (NetworkManager?.SceneManager != null)
            {
                NetworkManager.SceneManager.OnSceneEvent -= HandleNetworkSceneEvent;
            }
        }

        private void StopSceneQueue()
        {
            if (m_sceneQueueCoroutine != null)
            {
                StopCoroutine(m_sceneQueueCoroutine);
                m_sceneQueueCoroutine = null;
            }

            m_queuedSceneIDs.Clear();
            m_queuedUnloadSceneIDs.Clear();
            m_pendingLoadSceneIDs.Clear();
            m_pendingUnloadSceneIDs.Clear();
            m_sceneIsLoading = false;
            m_sceneIsUnloading = false;
            if (m_requiredRenderersCoroutine != null)
            {
                StopCoroutine(m_requiredRenderersCoroutine);
                m_requiredRenderersCoroutine = null;
            }
        }

        private IEnumerator UnloadAllAdditiveScenesNonNetwork()
        {
            Scene[] scenesToUnload = m_loadedScenes
                .Where(scene =>
                    scene.IsValid() &&
                    scene.isLoaded &&
                    scene.name != PersistentWorldSceneID)
                .ToArray();
            m_loadedScenes.RemoveAll(scene =>
                scenesToUnload.Any(candidate => candidate.handle == scene.handle));

            foreach (Scene scene in scenesToUnload)
            {
                AsyncOperation unloadOperation =
                    SceneManager.UnloadSceneAsync(scene);
                if (unloadOperation != null)
                {
                    yield return unloadOperation;
                }
            }
        }
    }
}
