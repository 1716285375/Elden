using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>Assigns one entering player as the stimulus for nearby sleeping AI.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public class EventTriggerWakeNearbyCharacters : MonoBehaviour
    {
        private const string k_CharacterLayerName = "Damageable Character";
        private const int k_MaximumCharacterColliders = 64;

        [SerializeField, Min(0.1f)] private float m_triggerRadius = 1f;
        [SerializeField, Min(0.1f)] private float m_awakenRadius = 20f;

        private readonly Collider[] m_characterColliders =
            new Collider[k_MaximumCharacterColliders];
        private readonly HashSet<AICharacterManager> m_creaturesToWake = new();

        private SphereCollider m_triggerCollider;

        /// <summary>Gets how near a player must be to fire the event.</summary>
        public float TriggerRadius => m_triggerRadius;

        /// <summary>Gets how far the event searches for sleeping AI.</summary>
        public float AwakenRadius => m_awakenRadius;

        private void Awake()
        {
            ConfigureTriggerCollider();
        }

        private void OnValidate()
        {
            m_triggerRadius = Mathf.Max(0.1f, m_triggerRadius);
            m_awakenRadius = Mathf.Max(m_triggerRadius, m_awakenRadius);
            ConfigureTriggerCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            PlayerManager player = other != null
                ? other.GetComponentInParent<PlayerManager>()
                : null;
            if (networkManager == null ||
                !networkManager.IsListening ||
                !networkManager.IsServer ||
                player == null ||
                player.IsDead)
            {
                return;
            }

            int characterLayerMask = LayerMask.GetMask(k_CharacterLayerName);
            int colliderCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                m_awakenRadius,
                m_characterColliders,
                characterLayerMask,
                QueryTriggerInteraction.Collide);
            m_creaturesToWake.Clear();
            for (int colliderIndex = 0;
                colliderIndex < colliderCount;
                colliderIndex++)
            {
                Collider characterCollider = m_characterColliders[colliderIndex];
                AICharacterManager aiCharacter = characterCollider != null
                    ? characterCollider.GetComponentInParent<AICharacterManager>()
                    : null;
                if (aiCharacter == null ||
                    aiCharacter.IsDead ||
                    aiCharacter.IsAwake ||
                    !m_creaturesToWake.Add(aiCharacter))
                {
                    continue;
                }

                aiCharacter.SetTarget(player);
            }
        }

        private void ConfigureTriggerCollider()
        {
            m_triggerCollider ??= GetComponent<SphereCollider>();
            if (m_triggerCollider != null)
            {
                m_triggerCollider.isTrigger = true;
                m_triggerCollider.radius = m_triggerRadius;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, m_triggerRadius);
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, m_awakenRadius);
        }
    }
}
