using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace ZZ
{
    /// <summary>Moves server-owned players into a data-driven world location.</summary>
    [DisallowMultipleComponent]
    public class EventTriggerLoadScene : MonoBehaviour
    {
        private static readonly HashSet<EventTriggerLoadScene> s_areaVolumes = new();

        [SerializeField] private WorldLocationSceneSet m_worldLocation;
        [SerializeField] private bool m_requirePlayerInsideBounds;
        [SerializeField] private int m_locationPriority;
        [SerializeField] private WorldSceneLocation m_area =
            WorldSceneLocation.Area01SubArea00;

        /// <summary>Gets the configured data-driven location.</summary>
        public WorldLocationSceneSet WorldLocation => m_worldLocation;

        /// <summary>Gets the legacy region used only by existing Scene instances.</summary>
        public WorldSceneLocation Area => m_area;

        private void Awake()
        {
            Reset();
        }

        private void OnEnable()
        {
            if (m_requirePlayerInsideBounds)
            {
                s_areaVolumes.Add(this);
            }
        }

        private void OnDisable()
        {
            s_areaVolumes.Remove(this);
        }

        private bool ContainsPosition(Vector3 position)
        {
            BoxCollider volume = GetComponent<BoxCollider>();
            if (volume == null || !volume.enabled)
            {
                return false;
            }
            Vector3 localPosition = transform.InverseTransformPoint(position) - volume.center;
            Vector3 halfSize = volume.size * 0.5f;
            return localPosition.x >= -halfSize.x && localPosition.x < halfSize.x &&
                localPosition.z >= -halfSize.z && localPosition.z < halfSize.z &&
                Mathf.Abs(localPosition.y) <= halfSize.y;
        }

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
            TryUpdatePlayerLocation(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // Recover an overlap that began before player or scene-manager network initialization.
            TryUpdatePlayerLocation(other);
        }

        private void TryUpdatePlayerLocation(Collider other)
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

            if (m_requirePlayerInsideBounds)
            {
                if (!ContainsPosition(player.transform.position))
                {
                    return;
                }
                foreach (EventTriggerLoadScene otherVolume in s_areaVolumes)
                {
                    if (otherVolume != null && otherVolume != this &&
                        otherVolume.m_locationPriority > m_locationPriority &&
                        otherVolume.ContainsPosition(player.transform.position))
                    {
                        return;
                    }
                }
            }

            WorldSceneSubSceneManager subSceneManager =
                WorldSceneSubSceneManager.Instance;
            WorldLocationSceneSet worldLocation = m_worldLocation != null
                ? m_worldLocation
                : subSceneManager?.ResolveWorldLocation(m_area);
            if (subSceneManager != null && worldLocation != null && player.AreaCurrentlyIn != worldLocation)
            {
                subSceneManager.LoadAreaBasedOnCurrentArea(
                    worldLocation,
                    player);
            }
        }
    }
}
