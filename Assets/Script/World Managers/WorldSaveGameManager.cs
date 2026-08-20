using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace ZZ
{
    public class WorldSaveGameManager : MonoBehaviour
    {
        private static WorldSaveGameManager s_instance;
        public static WorldSaveGameManager Instance => s_instance;

        [FormerlySerializedAs("worldSceneName")]
        [SerializeField] private string m_worldSceneName = "Scene_World_01";

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                return;
            }

            Destroy(gameObject);
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        public IEnumerator LoadNewGame()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                if (!networkManager.IsServer)
                {
                    Debug.LogError("Only the server can load the World Scene.");
                    yield break;
                }

                SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(
                    m_worldSceneName,
                    LoadSceneMode.Single);

                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError($"Could not load {m_worldSceneName}: {status}.");
                }

                yield break;
            }

            yield return SceneManager.LoadSceneAsync(m_worldSceneName, LoadSceneMode.Single);
        }

        public int GetWorldSceneIndex()
        {
            return SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{m_worldSceneName}.unity");
        }
    }
}
