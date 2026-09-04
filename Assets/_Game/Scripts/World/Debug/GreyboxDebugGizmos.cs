using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Draws LV01 greybox diagnostics in the Scene view: Area bounds, the authored
    /// route between Areas, and every gameplay marker the level actually contains.
    /// </summary>
    public class GreyboxDebugGizmos : MonoBehaviour
    {
        private const float k_AreaBoundsColorAlpha = 0.35f;
        private const float k_MarkerRadius = 0.5f;

        [SerializeField] private LV01GreyboxLayout m_layout;
        [SerializeField] private bool m_drawAreaBounds = true;
        [SerializeField] private bool m_drawRoute = true;
        [SerializeField] private bool m_drawMarkers = true;
        [SerializeField] private float m_markerRefreshInterval = 1f;
        [SerializeField] private List<GreyboxAreaLink> m_areaLinks = new();

        private readonly List<Vector3> m_spawnPoints = new();
        private readonly List<Vector3> m_enemyPoints = new();
        private readonly List<Vector3> m_checkpointPoints = new();
        private readonly List<Vector3> m_interactablePoints = new();

        private float m_nextMarkerRefreshTime;

        private void OnDrawGizmos()
        {
            if (m_layout == null)
            {
                return;
            }

            if (m_drawMarkers && Time.realtimeSinceStartup >= m_nextMarkerRefreshTime)
            {
                RefreshMarkers();
                m_nextMarkerRefreshTime = Time.realtimeSinceStartup + m_markerRefreshInterval;
            }

            if (m_drawAreaBounds)
            {
                DrawAreaBounds();
            }

            if (m_drawRoute)
            {
                DrawRoute();
            }

            if (m_drawMarkers)
            {
                DrawMarkers();
            }
        }

        private void DrawAreaBounds()
        {
            foreach ((int regionIndex, string area, Bounds bounds) in m_layout.GetAreaBounds())
            {
                Gizmos.color = new Color(1f, 0.85f, 0.2f, k_AreaBoundsColorAlpha);
                Gizmos.DrawWireCube(bounds.center, bounds.size);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    bounds.center + Vector3.up * (bounds.size.y * 0.5f + 1f),
                    $"R{regionIndex + 1:00}/{area}");
#endif
            }
        }

        private void DrawRoute()
        {
            Gizmos.color = new Color(0f, 1f, 0.9f, 0.9f);
            foreach (GreyboxAreaLink link in m_areaLinks)
            {
                if (!m_layout.TryGetAreaBounds(
                        link.FromRegionIndex, link.FromArea, out Bounds from))
                {
                    continue;
                }

                if (!m_layout.TryGetAreaBounds(link.ToRegionIndex, link.ToArea, out Bounds to))
                {
                    continue;
                }

                Gizmos.DrawLine(from.center, to.center);
            }
        }

        private void DrawMarkers()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.3f);
            foreach (Vector3 spawnPoint in m_spawnPoints)
            {
                Gizmos.DrawSphere(spawnPoint, k_MarkerRadius);
                Gizmos.DrawLine(spawnPoint, spawnPoint + Vector3.up * 2f);
            }

            Gizmos.color = new Color(1f, 0.2f, 0.25f);
            foreach (Vector3 enemyPoint in m_enemyPoints)
            {
                Gizmos.DrawWireSphere(enemyPoint, k_MarkerRadius);
            }

            Gizmos.color = new Color(0.3f, 0.6f, 1f);
            foreach (Vector3 checkpointPoint in m_checkpointPoints)
            {
                Gizmos.DrawWireCube(
                    checkpointPoint + Vector3.up, new Vector3(1f, 2f, 1f));
            }

            Gizmos.color = new Color(1f, 0.8f, 0.1f);
            foreach (Vector3 interactablePoint in m_interactablePoints)
            {
                Gizmos.DrawWireSphere(interactablePoint, k_MarkerRadius * 0.7f);
            }
        }

        private void RefreshMarkers()
        {
            m_spawnPoints.Clear();
            m_enemyPoints.Clear();
            m_checkpointPoints.Clear();
            m_interactablePoints.Clear();

            AICharacterSpawner[] spawners =
                FindObjectsByType<AICharacterSpawner>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (AICharacterSpawner spawner in spawners)
            {
                m_enemyPoints.Add(spawner.transform.position);
            }

            SiteOfGraceInteractable[] graces =
                FindObjectsByType<SiteOfGraceInteractable>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (SiteOfGraceInteractable grace in graces)
            {
                m_checkpointPoints.Add(grace.transform.position);
            }

            Interactable[] interactables =
                FindObjectsByType<Interactable>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (Interactable interactable in interactables)
            {
                if (interactable is SiteOfGraceInteractable)
                {
                    continue;
                }

                m_interactablePoints.Add(interactable.transform.position);
            }

            GameObject playerSpawn = GameObject.Find("Player Spawn Point");
            if (playerSpawn != null)
            {
                m_spawnPoints.Add(playerSpawn.transform.position);
            }
        }

        /// <summary>An authored connection between two Areas, drawn as a route line.</summary>
        [Serializable]
        public sealed class GreyboxAreaLink
        {
            [SerializeField] private int m_fromRegionIndex;
            [SerializeField] private string m_fromArea;
            [SerializeField] private int m_toRegionIndex;
            [SerializeField] private string m_toArea;

            public int FromRegionIndex => m_fromRegionIndex;

            public string FromArea => m_fromArea;

            public int ToRegionIndex => m_toRegionIndex;

            public string ToArea => m_toArea;
        }
    }
}
