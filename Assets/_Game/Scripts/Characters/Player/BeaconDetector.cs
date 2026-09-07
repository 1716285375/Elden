using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Detects server-side AI activation boundaries around one replicated player.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class BeaconDetector : MonoBehaviour
    {
        [SerializeField] private PlayerManager m_player;

        private SphereCollider m_detectorCollider;

        /// <summary>Gets the Inspector-assigned player represented by this detector.</summary>
        public PlayerManager Player => m_player;

        private void Awake()
        {
            m_detectorCollider = GetComponent<SphereCollider>();
            m_player ??= GetComponentInParent<PlayerManager>();
        }

        private void Update()
        {
            if (m_detectorCollider != null)
            {
                m_detectorCollider.enabled = CanProcessServerTrigger();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanProcessServerTrigger())
            {
                return;
            }

            AICharacterManager aiCharacter =
                other.GetComponentInParent<AICharacterManager>();
            aiCharacter?.ActivateCharacter(m_player);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!CanProcessServerTrigger())
            {
                return;
            }

            AICharacterManager aiCharacter =
                other.GetComponentInParent<AICharacterManager>();
            aiCharacter?.DeactivateCharacter(m_player);
        }

        private bool CanProcessServerTrigger()
        {
            return m_player != null && m_player.IsSpawned && m_player.IsServer;
        }
    }
}
