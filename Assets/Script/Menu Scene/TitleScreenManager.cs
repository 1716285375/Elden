using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    public class TitleScreenManager : MonoBehaviour
    {
        public void StartNetworkAsHost()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("A NetworkManager is required before starting a host.");
                return;
            }

            if (networkManager.IsListening)
            {
                return;
            }

            if (!networkManager.StartHost())
            {
                Debug.LogError("Failed to start the network host.");
            }
        }

        public void StartNewGame()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                Debug.LogError(
                    "Cannot start a new game because the network host is not running. " +
                    "Resolve the transport error and try again.");
                return;
            }

            if (WorldSaveGameManager.Instance == null)
            {
                Debug.LogError("WorldSaveGameManager is not available.");
                return;
            }

            StartCoroutine(WorldSaveGameManager.Instance.LoadNewGame());
        }
    }
}
