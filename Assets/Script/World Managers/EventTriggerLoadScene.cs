using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>Moves server-owned players into a configured world streaming region.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EventTriggerLoadScene : MonoBehaviour
    {
        [SerializeField] private WorldSceneLocation m_area =
            WorldSceneLocation.Area01SubArea00;

        /// <summary>Gets the region entered through this trigger.</summary>
        public WorldSceneLocation Area => m_area;

        private void Reset()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnValidate()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (NetworkManager.Singleton?.IsServer != true)
            {
                return;
            }

            PlayerManager player = other.GetComponentInParent<PlayerManager>();
            if (player == null || !player.IsSpawned)
            {
                return;
            }

            WorldSceneSubSceneManager.Instance?.LoadAreaBasedOnCurrentArea(
                m_area,
                player);
        }
    }
}
