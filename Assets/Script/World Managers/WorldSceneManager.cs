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
        /// <summary>The world Scene that remains loaded for the whole gameplay session.</summary>
        public const string PersistentWorldSceneID = "Scene_World_01";

        private static WorldSceneManager s_instance;

        private readonly List<Scene> m_loadedScenes = new();
        private readonly Queue<string> m_queuedSceneIDs = new();
        private readonly Queue<string> m_queuedUnloadSceneIDs = new();
        private readonly HashSet<string> m_pendingLoadSceneIDs = new();
        private readonly HashSet<string> m_pendingUnloadSceneIDs = new();

        private Coroutine m_sceneQueueCoroutine;
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

        private void OnDestroy()
        {
            UnsubscribeFromSceneEvents();
            if (s_instance == this)
            {
                s_instance = null;
            }
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
                    m_pendingLoadSceneIDs.Remove(sceneID);
                    if (!IsSceneLoaded(sceneID) && ShouldLoadScene(sceneID))
                    {
                        yield return LoadQueuedScene(sceneID);
                        WorldSceneSubSceneManager.Instance?.ReconcileLoadedScenes();
                    }

                    continue;
                }

                string unloadSceneID = m_queuedUnloadSceneIDs.Dequeue();
                m_pendingUnloadSceneIDs.Remove(unloadSceneID);
                if (CanUnloadScene(unloadSceneID))
                {
                    yield return UnloadQueuedScene(unloadSceneID);
                }
            }

            m_sceneQueueCoroutine = null;
        }

        private IEnumerator LoadQueuedScene(string sceneID)
        {
            m_sceneIsLoading = true;
            SceneEventProgressStatus status = NetworkManager.SceneManager.LoadScene(
                sceneID,
                LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started)
            {
                m_sceneIsLoading = false;
                Debug.LogError($"Could not load additive Scene {sceneID}: {status}.");
                yield break;
            }

            while (m_sceneIsLoading)
            {
                yield return null;
            }
        }

        private IEnumerator UnloadQueuedScene(string sceneID)
        {
            Scene scene = SceneManager.GetSceneByName(sceneID);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield break;
            }

            m_sceneIsUnloading = true;
            SceneEventProgressStatus status = NetworkManager.SceneManager.UnloadScene(scene);
            if (status != SceneEventProgressStatus.Started)
            {
                m_sceneIsUnloading = false;
                Debug.LogError($"Could not unload additive Scene {sceneID}: {status}.");
                yield break;
            }

            while (m_sceneIsUnloading)
            {
                yield return null;
            }
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
                        if (!IsServer)
                        {
                            m_sceneIsLoading = false;
                        }
                    }
                    break;
                case SceneEventType.LoadEventCompleted:
                    AddLoadedScene(sceneEvent.SceneName, sceneEvent.Scene);
                    m_sceneIsLoading = false;
                    break;
                case SceneEventType.Unload:
                    m_sceneIsUnloading = true;
                    break;
                case SceneEventType.UnloadComplete:
                    if (sceneEvent.ClientId == NetworkManager.LocalClientId)
                    {
                        RemoveLoadedScene(sceneEvent.SceneName);
                        if (!IsServer)
                        {
                            m_sceneIsUnloading = false;
                        }
                    }
                    break;
                case SceneEventType.UnloadEventCompleted:
                    RemoveLoadedScene(sceneEvent.SceneName);
                    m_sceneIsUnloading = false;
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
