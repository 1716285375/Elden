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

            TryFindBoss();
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
                FindObjectsSortMode.None);
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
                return;
            }

            PlayerUIManager.Instance?.PlayerUIBossHealthBar?.UnbindBoss(m_boundBoss);
        }

        private void SetArenaLocked(bool isLocked)
        {
            if (m_fogWallRoot != null)
            {
                m_fogWallRoot.SetActive(isLocked);
            }
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
