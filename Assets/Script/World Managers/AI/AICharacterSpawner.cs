using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ZZ
{
    /// <summary>
    /// Owns one authored AI spawn location and its optional persistent boss identity.
    /// </summary>
    public class AICharacterSpawner : MonoBehaviour
    {
        private const float k_NavMeshSampleDistance = 4f;

        [Header("Character")]
        [SerializeField] private GameObject m_characterGameObject;

        [Header("Idle Behavior")]
        [SerializeField, Min(0)] private int m_patrolPathID;
        [SerializeField] private bool m_repeatPatrol;
        [SerializeField] private bool m_isSleeping;
        [SerializeField] private bool m_willInvestigateSound = true;

        [Header("Boss Persistence")]
        [SerializeField, Min(0)] private int m_bossID;

        private AICharacterManager m_instantiatedCharacter;
        private bool m_hasResolvedSpawn;

        /// <summary>Gets the server-spawned AI instance owned by this point.</summary>
        public AICharacterManager InstantiatedCharacter => m_instantiatedCharacter;

        /// <summary>Gets the stable boss identifier, or zero for a normal enemy.</summary>
        public int BossID => m_bossID;

        /// <summary>Gets whether this spawn point represents a persistent boss.</summary>
        public bool IsBoss => m_bossID > 0;

        /// <summary>Gets the scene patrol route requested by this spawn point.</summary>
        public int PatrolPathID => m_patrolPathID;

        /// <summary>Gets whether the spawned AI begins in its sleeping behavior.</summary>
        public bool IsSleeping => m_isSleeping;

        /// <summary>Gets whether the spawned AI responds to sound stimuli.</summary>
        public bool WillInvestigateSound => m_willInvestigateSound;

        private void Awake()
        {
            WorldAIManager.Instance?.RegisterSpawner(this);
        }

        private void Start()
        {
            WorldAIManager.Instance?.RegisterSpawner(this);
        }

        private void OnDestroy()
        {
            WorldAIManager.Instance?.UnregisterSpawner(this);
        }

        /// <summary>
        /// Instantiates and network-spawns this character when called by the server.
        /// </summary>
        public AICharacterManager AttemptToSpawnCharacter()
        {
            if (m_hasResolvedSpawn || m_instantiatedCharacter != null)
            {
                return m_instantiatedCharacter;
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.IsListening ||
                !networkManager.IsServer)
            {
                return null;
            }

            if (m_characterGameObject == null ||
                m_characterGameObject.GetComponent<NetworkObject>() == null ||
                m_characterGameObject.GetComponent<AICharacterManager>() == null)
            {
                Debug.LogError(
                    $"{name} requires a networked AI character prefab.",
                    this);
                return null;
            }

            if (IsBoss &&
                WorldSaveGameManager.Instance?.GetBossProgress(m_bossID) ==
                BossProgressState.Defeated)
            {
                m_hasResolvedSpawn = true;
                SetSpawnMarkerVisible(false);
                return null;
            }

            if (!NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit hit,
                    k_NavMeshSampleDistance,
                    NavMesh.AllAreas))
            {
                Debug.LogWarning(
                    $"No NavMesh was found near AI spawner {name}.",
                    this);
                return null;
            }

            GameObject instance = Instantiate(
                m_characterGameObject,
                hit.position,
                transform.rotation);
            AICharacterManager character = instance.GetComponent<AICharacterManager>();
            character.SetOriginSpawner(this);
            AIPatrolPath patrolPath = m_patrolPathID > 0
                ? WorldAIManager.Instance?.GetAIPatrolPathByID(m_patrolPathID)
                : null;
            character.ConfigureIdleBehavior(
                patrolPath,
                m_repeatPatrol,
                m_isSleeping,
                m_willInvestigateSound);
            instance.GetComponent<NetworkObject>().Spawn(true);
            character.InitializeAsInactive();

            m_instantiatedCharacter = character;
            m_hasResolvedSpawn = true;
            if (IsBoss)
            {
                WorldSaveGameManager.Instance?.RecordBossProgress(
                    m_bossID,
                    BossProgressState.Dormant,
                    false);
            }

            SetSpawnMarkerVisible(false);
            return m_instantiatedCharacter;
        }

        /// <summary>Clears this spawner's transient resolution so a living enemy can respawn.</summary>
        public void ResetSpawnState()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.IsListening ||
                !networkManager.IsServer)
            {
                return;
            }

            m_instantiatedCharacter = null;
            m_hasResolvedSpawn = false;
            SetSpawnMarkerVisible(true);
        }

        /// <summary>Restores the cached reusable AI to this authored spawn point.</summary>
        public bool ResetCharacter()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.IsListening ||
                !networkManager.IsServer ||
                m_instantiatedCharacter == null ||
                !m_instantiatedCharacter.IsSpawned)
            {
                return false;
            }

            if (IsBoss &&
                WorldSaveGameManager.Instance?.GetBossProgress(m_bossID) ==
                    BossProgressState.Defeated)
            {
                return false;
            }

            Vector3 spawnPosition = transform.position;
            if (NavMesh.SamplePosition(
                    spawnPosition,
                    out NavMeshHit hit,
                    k_NavMeshSampleDistance,
                    NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
            }

            return m_instantiatedCharacter.ResetAtSpawnPoint(
                spawnPosition,
                transform.rotation);
        }

        /// <summary>Persists the first transition into an active boss encounter.</summary>
        public void MarkBossAwakened()
        {
            if (!IsBoss)
            {
                return;
            }

            WorldSaveGameManager.Instance?.RecordBossProgress(
                m_bossID,
                BossProgressState.Awakened,
                true);
        }

        /// <summary>Persists boss defeat so future scene loads suppress this spawn.</summary>
        public void MarkBossDefeated()
        {
            if (!IsBoss)
            {
                return;
            }

            WorldSaveGameManager.Instance?.RecordBossProgress(
                m_bossID,
                BossProgressState.Defeated,
                true);
        }

        internal void NotifyCharacterDespawned(AICharacterManager character)
        {
            if (m_instantiatedCharacter == character)
            {
                m_instantiatedCharacter = null;
            }
        }

        private void SetSpawnMarkerVisible(bool isVisible)
        {
            foreach (Renderer markerRenderer in
                GetComponentsInChildren<Renderer>(true))
            {
                markerRenderer.enabled = isVisible;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsBoss
                ? new Color(0.65f, 0.1f, 0.85f, 0.9f)
                : new Color(0.85f, 0.15f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawLine(
                transform.position,
                transform.position + transform.forward * 1.5f);
        }
    }
}
