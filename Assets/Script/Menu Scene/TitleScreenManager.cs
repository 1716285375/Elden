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
            if (WorldSaveGameManager.instance == null)
            {
                Debug.LogError("WorldSaveGameManager is not available.");
                return;
            }

            StartCoroutine(WorldSaveGameManager.instance.LoadNewGame());
        }
    }
}
