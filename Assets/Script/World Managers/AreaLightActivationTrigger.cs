using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Enables a bounded area's realtime lights only while the local player occupies it.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class AreaLightActivationTrigger : MonoBehaviour
    {
        [Header("AREA LIGHTS")]
        [SerializeField] private Light[] m_areaLights = System.Array.Empty<Light>();
        [SerializeField] private bool m_disableWhenEmpty = true;

        private readonly Dictionary<PlayerManager, int> m_playerColliderCounts = new();

        /// <summary>Gets the lights managed by this trigger.</summary>
        public IReadOnlyList<Light> AreaLights => m_areaLights;

        /// <summary>Configures the trigger without requiring per-frame discovery calls.</summary>
        public void Configure(Light[] areaLights, bool disableWhenEmpty = true)
        {
            m_areaLights = areaLights ?? System.Array.Empty<Light>();
            m_disableWhenEmpty = disableWhenEmpty;
        }

        /// <summary>Returns whether a network player represents the local presentation owner.</summary>
        public static bool ShouldTrackPlayer(bool isSpawned, bool isOwner)
        {
            return !isSpawned || isOwner;
        }

        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            SetLightsActive(!m_disableWhenEmpty);
        }

        private void OnDisable()
        {
            m_playerColliderCounts.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerManager player = other.GetComponentInParent<PlayerManager>();
            if (player == null || !ShouldTrackPlayer(player.IsSpawned, player.IsOwner))
            {
                return;
            }

            m_playerColliderCounts.TryGetValue(player, out int colliderCount);
            m_playerColliderCounts[player] = colliderCount + 1;
            SetLightsActive(true);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerManager player = other.GetComponentInParent<PlayerManager>();
            if (player == null || !m_playerColliderCounts.TryGetValue(player, out int colliderCount))
            {
                return;
            }

            if (colliderCount <= 1)
            {
                m_playerColliderCounts.Remove(player);
            }
            else
            {
                m_playerColliderCounts[player] = colliderCount - 1;
            }

            if (m_playerColliderCounts.Count == 0 && m_disableWhenEmpty)
            {
                SetLightsActive(false);
            }
        }

        private void OnValidate()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void SetLightsActive(bool isActive)
        {
            foreach (Light areaLight in m_areaLights)
            {
                if (areaLight != null)
                {
                    areaLight.enabled = isActive;
                }
            }
        }
    }
}
