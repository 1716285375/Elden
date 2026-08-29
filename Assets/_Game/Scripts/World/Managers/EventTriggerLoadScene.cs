using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>Moves server-owned players into a data-driven world location.</summary>
    [DisallowMultipleComponent]
    public class EventTriggerLoadScene : MonoBehaviour
    {
        [SerializeField] private WorldLocationSceneSet m_worldLocation;
        [SerializeField] private WorldSceneLocation m_area =
            WorldSceneLocation.Area01SubArea00;

        /// <summary>Gets the configured data-driven location.</summary>
        public WorldLocationSceneSet WorldLocation => m_worldLocation;

        /// <summary>Gets the legacy region used only by existing Scene instances.</summary>
        public WorldSceneLocation Area => m_area;

        private void Reset()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                if (triggerCollider is MeshCollider meshCollider)
                {
                    meshCollider.convex = true;
                }

                triggerCollider.isTrigger = true;
            }
        }

        private void OnValidate()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                if (triggerCollider is MeshCollider meshCollider)
                {
                    meshCollider.convex = true;
                }

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

            WorldSceneSubSceneManager subSceneManager =
                WorldSceneSubSceneManager.Instance;
            WorldLocationSceneSet worldLocation = m_worldLocation != null
                ? m_worldLocation
                : subSceneManager?.ResolveWorldLocation(m_area);
            if (worldLocation != null)
            {
                subSceneManager.LoadAreaBasedOnCurrentArea(
                    worldLocation,
                    player);
            }
        }
    }
}
