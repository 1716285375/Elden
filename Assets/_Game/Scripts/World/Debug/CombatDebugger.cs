using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ
{
    /// <summary>
    /// Development overlay and gizmos for the combat framework. Purely additive: it reads combat
    /// state but never feeds it, so it can be removed without touching gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatDebugger : MonoBehaviour
    {
        [SerializeField] private Key m_toggleKey = Key.F3;
        [SerializeField] private bool m_drawGizmos = true;
        [SerializeField] private bool m_isVisible;
        [SerializeField] private Color m_hurtBoxColor = new Color(0.2f, 0.9f, 0.4f, 0.5f);
        [SerializeField] private Color m_hitBoxColor = new Color(1f, 0.3f, 0.2f, 0.6f);
        [SerializeField] private Color m_guardConeColor = new Color(0.3f, 0.6f, 1f, 0.35f);

        private readonly StringBuilder m_report = new();
        private PlayerManager m_player;
        private GUIStyle m_style;
        private string m_lastReport = string.Empty;
        private float m_nextRefresh;

        public static CombatDebugger Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[m_toggleKey].wasPressedThisFrame)
            {
                m_isVisible = !m_isVisible;
            }

            m_player = FindCachedPlayer();
        }

        /// <summary>
        /// FindFirstObjectByType is expensive, so it runs at most once every few dozen frames and
        /// the result is cached for the lifetime of the overlay.
        /// </summary>
        private PlayerManager FindCachedPlayer()
        {
            if (m_player != null)
            {
                return m_player;
            }

            return Time.frameCount % 30 == 0 ? FindFirstObjectByType<PlayerManager>() : null;
        }

        private void OnGUI()
        {
            if (!m_isVisible || m_player == null)
            {
                return;
            }

            m_style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = false,
                normal = { textColor = Color.white }
            };

            if (Time.unscaledTime >= m_nextRefresh)
            {
                m_nextRefresh = Time.unscaledTime + 0.1f;
                m_lastReport = BuildReport();
            }

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(new Rect(10f, 10f, 320f, 300f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(18f, 14f, 310f, 290f), m_lastReport, m_style);
        }

        private string BuildReport()
        {
            CharacterCombatManager combat = m_player.CharacterCombatManager;
            CombatActionInstance action = combat?.ActionRunner.Current;
            m_report.Clear();
            m_report.AppendLine("== Combat ==");
            m_report.Append("Action   ").AppendLine(action != null && action.IsRunning
                ? $"{action.Definition.name} ({action.AttackId})"
                : "None");
            m_report.Append("Sequence ").AppendLine(action != null ? action.Sequence.ToString() : "-");
            m_report.Append("Phase    ").AppendLine(action != null ? action.Phase.ToString() : "-");
            m_report.Append("Contacts ").AppendLine(action != null ? action.Hits.TotalContacts.ToString() : "-");
            m_report.Append("Snapshot ").AppendLine(action != null && action.Snapshot.IsValid
                ? "captured"
                : "live weapon / legacy");
            m_report.Append("Dodge    ").AppendLine(
                m_player.CharacterAnimatorManager?.UsesFallbackDodgeWindows == true
                    ? "FALLBACK (assign DodgeDefinition)"
                    : "authored");
            m_report.AppendLine();
            m_report.AppendLine("== Windows ==");
            AppendWindow(m_report, combat, CombatWindowType.Hit, "Hit");
            AppendWindow(m_report, combat, CombatWindowType.Combo, "Combo");
            AppendWindow(m_report, combat, CombatWindowType.HyperArmor, "Hyper");
            AppendWindow(m_report, combat, CombatWindowType.Parryable, "Parryable");
            AppendWindow(m_report, combat, CombatWindowType.Invulnerability, "IFrame");
            AppendWindow(m_report, combat, CombatWindowType.RollCancel, "RollCancel");
            AppendWindow(m_report, combat, CombatWindowType.MoveCancel, "MoveCancel");
            m_report.AppendLine();

            CombatCommandBuffer buffer = m_player.PlayerCombatManager?.CommandBuffer;
            m_report.AppendLine("== Buffer ==");
            if (buffer == null || buffer.Count == 0)
            {
                m_report.AppendLine("Empty");
            }
            else
            {
                foreach (BufferedCommand pending in buffer.Pending)
                {
                    m_report.Append("  ").Append(pending.Command)
                        .Append("  ").Append((Time.time - pending.Timestamp).ToString("0.00"))
                        .AppendLine("s");
                }
            }

            m_report.AppendLine();
            m_report.AppendLine("== Resources ==");
            CharacterNetworkManager network = m_player.CharacterNetworkManager;
            if (network != null)
            {
                m_report.Append("HP      ").Append(network.CurrentHealth.Value.ToString("0"))
                    .Append(" / ").AppendLine(network.MaxHealth.Value.ToString("0"));
                m_report.Append("Stamina ").AppendLine(network.CurrentStamina.Value.ToString("0"));
            }

            CharacterStatsManager stats = m_player.CharacterStatsManager;
            if (stats != null)
            {
                m_report.Append("Poise   ").AppendLine(stats.TotalPoiseDamage.ToString("0.0"));
            }

            m_report.Append("LastHit ").AppendLine(combat != null
                ? combat.PreviousPoiseDamageTaken.ToString("0.0") + " poise"
                : "-");
            return m_report.ToString();
        }

        private static void AppendWindow(StringBuilder report, CharacterCombatManager combat,
            CombatWindowType window, string label)
        {
            bool isOpen = combat?.ActionRunner.Windows.IsOpen(window) == true;
            report.Append("  ").Append(label.PadRight(11)).AppendLine(isOpen ? "ON" : "off");
        }

        private void OnDrawGizmos()
        {
            if (!m_drawGizmos)
            {
                return;
            }

            DrawHurtBoxes();
            DrawDamageColliders();
            DrawGuardCone();
        }

        private void DrawHurtBoxes()
        {
            Gizmos.color = m_hurtBoxColor;
            foreach (HurtBox hurtBox in
                     FindObjectsByType<HurtBox>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (hurtBox == null || !hurtBox.isActiveAndEnabled)
                {
                    continue;
                }

                if (hurtBox.TryGetComponent(out Collider collider))
                {
                    Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
                }
            }
        }

        private void DrawDamageColliders()
        {
            Gizmos.color = m_hitBoxColor;
            foreach (DamageCollider detector in
                     FindObjectsByType<DamageCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (detector == null || !detector.isActiveAndEnabled ||
                    !detector.TryGetComponent(out Collider collider) || !collider.enabled)
                {
                    continue;
                }

                Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
            }
        }

        private void DrawGuardCone()
        {
            if (m_player == null)
            {
                return;
            }

            GuardProfile guard = m_player.CharacterStatsManager?.CurrentGuardProfile;
            float halfAngle = guard != null ? guard.TotalGuardArcDegrees * 0.5f : 60f;
            Gizmos.color = m_guardConeColor;
            Vector3 origin = m_player.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(m_player.transform.forward, Vector3.up).normalized;
            Vector3 previous = origin + Quaternion.Euler(0f, -halfAngle, 0f) * forward * 1.5f;
            for (int step = 1; step <= 12; step++)
            {
                float angle = -halfAngle + step * halfAngle * 2f / 12f;
                Vector3 point = origin + Quaternion.Euler(0f, angle, 0f) * forward * 1.5f;
                Gizmos.DrawLine(origin, point);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
