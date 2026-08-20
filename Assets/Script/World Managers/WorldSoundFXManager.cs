using UnityEngine;

namespace ZZ
{
    public class WorldSoundFXManager : MonoBehaviour
    {
        private static WorldSoundFXManager s_instance;

        [SerializeField] private AudioClip m_rollingSoundFX;

        public static WorldSoundFXManager Instance => s_instance;
        public AudioClip RollingSoundFX => m_rollingSoundFX;

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

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
