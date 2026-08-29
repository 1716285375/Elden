using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Keeps a lightweight server-side trigger alive while its owning AI is disabled.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class AIActivationBeacon : MonoBehaviour
    {
        [SerializeField] private AICharacterManager m_beaconOwner;

        /// <summary>Gets the AI that will be reactivated by this beacon.</summary>
        public AICharacterManager BeaconOwner => m_beaconOwner;

        /// <summary>Assigns the runtime-spawned AI represented by this beacon.</summary>
        public void SetOwnerOfBeacon(AICharacterManager beaconOwner)
        {
            m_beaconOwner = beaconOwner;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_beaconOwner == null || !m_beaconOwner.IsServer)
            {
                return;
            }

            BeaconDetector detector = other.GetComponent<BeaconDetector>();
            if (detector?.Player == null ||
                !m_beaconOwner.ReactivateAICharacter())
            {
                return;
            }

            m_beaconOwner.ActivateCharacter(detector.Player);
        }
    }
}
