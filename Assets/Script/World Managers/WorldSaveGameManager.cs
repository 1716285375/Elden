using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    public class WorldSaveGameManager : MonoBehaviour
    {
        public static WorldSaveGameManager instance;
        public static WorldSaveGameManager Instance => instance;

        [SerializeField] private string worldSceneName = "Scene_World_01";

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
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
                    worldSceneName,
                    LoadSceneMode.Single);

                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError($"Could not load {worldSceneName}: {status}.");
                }

                yield break;
            }

            yield return SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Single);
        }

        public int GetWorldSceneIndex()
        {
            return SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{worldSceneName}.unity");
        }
    }
}
