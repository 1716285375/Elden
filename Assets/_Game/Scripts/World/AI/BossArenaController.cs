using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Activates one server-owned Boss when a player enters and presents its arena lock on every peer.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class BossArenaController : MonoBehaviour
    {
        [SerializeField, Min(1)] private int m_bossID = 1;
        [SerializeField] private GameObject m_fogWallRoot;

        [Header("Music")]
        [SerializeField] private AudioSource m_bossMusicSource;
        [SerializeField] private AudioClip m_bossMusic;

        private BossCharacterManager m_boundBoss;

        private void Awake()
        {
            BoxCollider arenaTrigger = GetComponent<BoxCollider>();
            arenaTrigger.isTrigger = true;
            SetArenaLocked(false);
        }

        private void Start()
        {
            if (WorldAIManager.Instance != null)
            {
                WorldAIManager.Instance.AIRegistered += OnAIRegistered;
            }

            TryFindBoss();
        }

        private void OnDestroy()
        {
            if (WorldAIManager.Instance != null)
            {
                WorldAIManager.Instance.AIRegistered -= OnAIRegistered;
            }

            UnbindBoss();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryBeginEncounter(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // Streaming can spawn the Boss after the player has already entered the arena.
            TryBeginEncounter(other);
        }

        private void TryBeginEncounter(Collider other)
        {
            if (m_boundBoss != null && m_boundBoss.IsEncounterActive)
            {
                return;
            }
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            PlayerManager enteringPlayer = other.GetComponentInParent<PlayerManager>();
            if (enteringPlayer == null || enteringPlayer.IsDead)
            {
                return;
            }

            if (m_boundBoss == null || !m_boundBoss.IsSpawned)
            {
                TryFindBoss();
            }
            m_boundBoss?.BeginEncounter(enteringPlayer);
        }

        private void OnAIRegistered(AICharacterManager aiCharacter)
        {
            BossCharacterManager boss = aiCharacter != null
                ? aiCharacter.GetComponent<BossCharacterManager>()
                : null;
            if (boss != null && boss.BossID == m_bossID)
            {
                BindBoss(boss);
            }
        }

        private void TryFindBoss()
        {
            BossCharacterManager[] bosses = FindObjectsByType<BossCharacterManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (BossCharacterManager boss in bosses)
            {
                if (boss != null && boss.IsSpawned && boss.BossID == m_bossID)
                {
                    BindBoss(boss);
                    return;
                }
            }
        }

        private void BindBoss(BossCharacterManager boss)
        {
            if (m_boundBoss == boss)
            {
                return;
            }

            UnbindBoss();
            m_boundBoss = boss;
            m_boundBoss.EncounterStateChanged += OnEncounterStateChanged;
            PresentEncounter(m_boundBoss.IsEncounterActive);
        }

        private void UnbindBoss()
        {
            if (m_boundBoss != null)
            {
                m_boundBoss.EncounterStateChanged -= OnEncounterStateChanged;
            }

            PlayerUIManager.Instance?.PlayerUIBossHealthBar?.UnbindBoss(m_boundBoss);
            SetBossMusicActive(false);
            m_boundBoss = null;
        }

        private void OnEncounterStateChanged(
            BossCharacterManager boss,
            bool isEncounterActive)
        {
            PresentEncounter(isEncounterActive);
        }

        private void PresentEncounter(bool isEncounterActive)
        {
            SetArenaLocked(isEncounterActive);
            if (isEncounterActive)
            {
                PlayerUIManager.Instance?.PlayerUIBossHealthBar?.BindBoss(m_boundBoss);
                SetBossMusicActive(true);
                return;
            }

            PlayerUIManager.Instance?.PlayerUIBossHealthBar?.RemoveHPBar(m_boundBoss);
            SetBossMusicActive(false);
        }

        private void SetBossMusicActive(bool isActive)
        {
            if (m_bossMusicSource == null)
            {
                return;
            }

            if (!isActive)
            {
                m_bossMusicSource.Stop();
                return;
            }

            if (m_bossMusic != null)
            {
                m_bossMusicSource.clip = m_bossMusic;
            }

            m_bossMusicSource.loop = true;
            if (m_bossMusicSource.clip != null && !m_bossMusicSource.isPlaying)
            {
                m_bossMusicSource.Play();
            }
        }

        private void SetArenaLocked(bool isLocked)
        {
            if (m_fogWallRoot == null)
            {
                return;
            }

            FogWallInteractable fogWall =
                m_fogWallRoot.GetComponent<FogWallInteractable>();
            if (fogWall != null)
            {
                fogWall.SetFogWallActive(isLocked);
                return;
            }

            m_fogWallRoot.SetActive(isLocked);
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider arenaTrigger = GetComponent<BoxCollider>();
            if (arenaTrigger == null)
            {
                return;
            }

            Gizmos.color = new Color(0.55f, 0.1f, 0.75f, 0.35f);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(arenaTrigger.center, arenaTrigger.size);
            Gizmos.matrix = previousMatrix;
        }
    }
}
