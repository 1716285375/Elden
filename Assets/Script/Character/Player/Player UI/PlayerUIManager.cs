using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    public class PlayerUIManager : MonoBehaviour
    {
        private static PlayerUIManager s_instance;

        [Header("NETWORK JOIN")]
        [FormerlySerializedAs("startGameAsClient")]
        [SerializeField] private bool m_shouldStartAsClient;

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (m_shouldStartAsClient)
            {
                m_shouldStartAsClient = false;
                NetworkManager.Singleton.Shutdown();

                NetworkManager.Singleton.StartClient();
            }
        }
    }
}
