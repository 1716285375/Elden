using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Collects ordered child transforms into one reusable scene patrol route.</summary>
    public class AIPatrolPath : MonoBehaviour
    {
        [SerializeField, Min(1)] private int m_patrolPathID = 1;
        [SerializeField] private List<Vector3> m_patrolPoints = new();

        /// <summary>Gets the stable identifier used by scene AI spawners.</summary>
        public int PatrolPathID => m_patrolPathID;

        /// <summary>Gets the world-space patrol points in hierarchy order.</summary>
        public IReadOnlyList<Vector3> PatrolPoints => m_patrolPoints;

        private void Awake()
        {
            RefreshPatrolPoints();
        }

        private void OnEnable()
        {
            WorldAIManager.Instance?.AddPatrolPathToList(this);
        }

        private void Start()
        {
            WorldAIManager.Instance?.AddPatrolPathToList(this);
        }

        private void OnDisable()
        {
            WorldAIManager.Instance?.RemovePatrolPathFromList(this);
        }

        private void OnValidate()
        {
            m_patrolPathID = Mathf.Max(1, m_patrolPathID);
            RefreshPatrolPoints();
        }

        /// <summary>Rebuilds the route from direct child transforms in hierarchy order.</summary>
        public void RefreshPatrolPoints()
        {
            m_patrolPoints.Clear();
            foreach (Transform patrolPoint in transform)
            {
                m_patrolPoints.Add(patrolPoint.position);
            }
        }

        /// <summary>Returns the route index closest to the supplied world position.</summary>
        public int GetClosestPatrolPointIndex(Vector3 worldPosition)
        {
            int closestIndex = -1;
            float closestDistanceSquared = float.PositiveInfinity;
            for (int pointIndex = 0; pointIndex < m_patrolPoints.Count; pointIndex++)
            {
                float distanceSquared =
                    (m_patrolPoints[pointIndex] - worldPosition).sqrMagnitude;
                if (distanceSquared < closestDistanceSquared)
                {
                    closestIndex = pointIndex;
                    closestDistanceSquared = distanceSquared;
                }
            }

            return closestIndex;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.65f, 0.1f, 0.9f);
            for (int pointIndex = 0; pointIndex < m_patrolPoints.Count; pointIndex++)
            {
                Vector3 point = m_patrolPoints[pointIndex];
                Gizmos.DrawWireSphere(point, 0.35f);
                if (pointIndex + 1 < m_patrolPoints.Count)
                {
                    Gizmos.DrawLine(point, m_patrolPoints[pointIndex + 1]);
                }
            }
        }
    }
}
