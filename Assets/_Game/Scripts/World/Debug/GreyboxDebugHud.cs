using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Draws the LV01 greybox traversal overlay used while manually play testing the
    /// blockout: where the player is, how fast they are moving, and which streaming
    /// slices are actually resident.
    /// </summary>
    public class GreyboxDebugHud : MonoBehaviour
    {
        private const string k_ScenePrefix = "SCN_LV01_R";

        [SerializeField] private LV01GreyboxLayout m_layout;
        [SerializeField] private bool m_visible = true;
        [SerializeField] private KeyCode m_toggleKey = KeyCode.F1;
        [SerializeField] private Vector2 m_panelSize = new(360f, 300f);

        private readonly StringBuilder m_text = new();
        private readonly List<string> m_loadedScenes = new();

        private PlayerManager m_player;
        private Vector3 m_previousPosition;
        private float m_speed;
        private GUIStyle m_panelStyle;
        private GUIStyle m_labelStyle;

        private void Update()
        {
            if (Input.GetKeyDown(m_toggleKey))
            {
                m_visible = !m_visible;
            }

            if (m_player == null)
            {
                m_player = FindFirstObjectByType<PlayerManager>(FindObjectsInactive.Include);
                if (m_player != null)
                {
                    m_previousPosition = m_player.transform.position;
                }

                return;
            }

            Vector3 position = m_player.transform.position;
            float deltaTime = Time.deltaTime;
            if (deltaTime > 0f)
            {
                Vector3 horizontalDelta = position - m_previousPosition;
                horizontalDelta.y = 0f;
                m_speed = horizontalDelta.magnitude / deltaTime;
            }

            m_previousPosition = position;
        }

        private void OnGUI()
        {
            if (!m_visible)
            {
                return;
            }

            EnsureStyles();
            GUI.Box(new Rect(10f, 10f, m_panelSize.x, m_panelSize.y),
                "LV01 Greybox", m_panelStyle);
            GUILayout.BeginArea(new Rect(20f, 34f, m_panelSize.x - 20f, m_panelSize.y - 34f));
            GUILayout.Label(BuildText(), m_labelStyle);
            GUILayout.EndArea();
        }

        private string BuildText()
        {
            m_text.Clear();
            m_text.Append($"<b><color=#FFD24A>[{m_toggleKey}] LV01 Greybox Traversal</color></b>\n");

            if (m_layout == null)
            {
                m_text.Append("\n<color=#FF6B6B>No layout asset assigned.</color>");
                return m_text.ToString();
            }

            if (m_player == null)
            {
                m_text.Append("\n<color=#FF6B6B>No PlayerManager in the loaded scenes.</color>");
                return m_text.ToString();
            }

            Vector3 position = m_player.transform.position;
            m_layout.TryGetAreaAt(position, out int regionIndex, out string area);

            m_text.Append("\n<b>Location</b>\n");
            m_text.Append("  Region  ").Append(regionIndex >= 0
                ? WorldScenePathLayout.GetRegionFolderName(regionIndex)
                : "<color=#FF6B6B>outside all Areas</color>").Append('\n');
            m_text.Append("  Area    ").Append(area ?? "-").Append('\n');

            m_text.Append("\n<b>Player</b>\n");
            m_text.Append("  Position  ").Append(FormatVector(position)).Append('\n');
            m_text.Append("  Speed     ").Append(m_speed.ToString("0.00")).Append(" m/s\n");

            m_text.Append("\n<b>Scale basis</b>\n");
            m_text.Append("  Factor    x").Append(m_layout.ScaleFactor.ToString("0.####")).Append('\n');
            m_text.Append("  Capsule   h ").Append(m_layout.PlayerHeight.ToString("0.00"))
                .Append("  r ").Append(m_layout.PlayerRadius.ToString("0.00")).Append('\n');
            m_text.Append("  Camera    ").Append(m_layout.CameraPivotHeight.ToString("0.00"))
                .Append(" up / ").Append(m_layout.CameraDistance.ToString("0.00")).Append(" back\n");

            m_text.Append("\n<b>Loaded slices</b>\n");
            AppendLoadedSlices();
            return m_text.ToString();
        }

        private void AppendLoadedSlices()
        {
            m_loadedScenes.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name.StartsWith(k_ScenePrefix))
                {
                    m_loadedScenes.Add(scene.name);
                }
            }

            if (m_loadedScenes.Count == 0)
            {
                m_text.Append("  <color=#FF6B6B>none</color>\n");
                return;
            }

            string[] slices = { "Base", "Props", "Effects", "Spawners" };
            for (int region = 0; region < WorldScenePathLayout.RegionCount; region++)
            {
                for (int area = 0; area < WorldScenePathLayout.GetAreaCount(region); area++)
                {
                    bool anyLoaded = false;
                    for (int slice = 0; slice < slices.Length; slice++)
                    {
                        if (m_loadedScenes.Contains(WorldScenePathLayout.GetSceneID(
                                region, area, slice)))
                        {
                            anyLoaded = true;
                            break;
                        }
                    }

                    if (!anyLoaded)
                    {
                        continue;
                    }

                    m_text.Append("  R").Append((region + 1).ToString("00"))
                        .Append(" A").Append((area + 1).ToString("00")).Append(' ');
                    for (int slice = 0; slice < slices.Length; slice++)
                    {
                        bool loaded = m_loadedScenes.Contains(
                            WorldScenePathLayout.GetSceneID(region, area, slice));
                        m_text.Append(loaded
                            ? $"<color=#7CFF7C>{slices[slice]}</color> "
                            : $"<color=#7A7A7A>{slices[slice]}</color> ");
                    }

                    m_text.Append('\n');
                }
            }
        }

        private static string FormatVector(Vector3 value) =>
            $"({value.x:0.0}, {value.y:0.0}, {value.z:0.0})";

        private void EnsureStyles()
        {
            if (m_panelStyle != null)
            {
                return;
            }

            m_panelStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };

            m_labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = true,
                alignment = TextAnchor.UpperLeft
            };
            m_labelStyle.normal.textColor = Color.white;
        }
    }
}
