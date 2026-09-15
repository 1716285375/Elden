using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Samples the weapon volume between animated poses. Costs stay bounded: one sweep at a capped
    /// sample count against the authored HurtBox layers, instead of unlimited all-layer queries.
    /// </summary>
    public sealed class WeaponTrace
    {
        private const int k_InitialContactCapacity = 32;
        private const int k_MaximumContactCapacity = 128;
        private const float k_MinimumTravel = 0.005f;
        private const float k_MinimumAngle = 0.5f;

        private readonly DamageCollider m_detector;
        private readonly BoxCollider m_box;
        private Collider[] m_contacts = new Collider[k_InitialContactCapacity];
        private Vector3 m_previousCenter;
        private Quaternion m_previousRotation;
        private bool m_hasPreviousPose;

        public WeaponTrace(DamageCollider detector, BoxCollider box)
        {
            m_detector = detector;
            m_box = box;
        }

        public void Reset() => m_hasPreviousPose = false;

        public void Sample()
        {
            if (m_box == null || !m_box.enabled)
            {
                Reset();
                return;
            }

            Transform pose = m_box.transform;
            Vector3 center = pose.TransformPoint(m_box.center);
            Quaternion rotation = pose.rotation;
            Vector3 scale = pose.lossyScale;
            Vector3 halfSize = Vector3.Scale(m_box.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            // A teleport must not hit every character between the two locations.
            if (!m_hasPreviousPose || Vector3.Distance(m_previousCenter, center) > 8f)
            {
                m_previousCenter = center;
                m_previousRotation = rotation;
            }

            float travel = Vector3.Distance(m_previousCenter, center);
            float angle = Quaternion.Angle(m_previousRotation, rotation);
            int samples = 1;
            if (travel > k_MinimumTravel || angle > k_MinimumAngle)
            {
                float cornerTravel = travel + angle * Mathf.Deg2Rad * halfSize.magnitude;
                // Spacing follows the blade's thinnest half-extent so consecutive samples overlap.
                // The clamp is a performance safety valve only, never the basis for correctness.
                float allowedGap = Mathf.Max(0.04f, Mathf.Min(halfSize.x, Mathf.Min(halfSize.y, halfSize.z)));
                samples = Mathf.Clamp(Mathf.CeilToInt(cornerTravel / allowedGap), 1,
                    m_detector.MaximumTraceSamples);
            }

            PhysicsScene physics = m_detector.gameObject.scene.GetPhysicsScene();
            int layerMask = m_detector.HurtBoxLayers;
            for (int sample = 0; sample < samples; sample++)
            {
                float fraction = samples <= 1 ? 1f : sample / (float)(samples - 1);
                Vector3 position = Vector3.Lerp(m_previousCenter, center, fraction);
                Quaternion orientation = Quaternion.Slerp(m_previousRotation, rotation, fraction);
                int count = physics.OverlapBox(position, halfSize, m_contacts, orientation,
                    layerMask, QueryTriggerInteraction.Collide);
                if (count >= m_contacts.Length && m_contacts.Length < k_MaximumContactCapacity)
                {
                    m_contacts = new Collider[Mathf.Min(k_MaximumContactCapacity, m_contacts.Length * 2)];
                    count = physics.OverlapBox(position, halfSize, m_contacts, orientation,
                        layerMask, QueryTriggerInteraction.Collide);
                }

                for (int contact = 0; contact < count; contact++)
                {
                    m_detector.TryProcessHit(m_contacts[contact]);
                    m_contacts[contact] = null;
                }
            }

            m_previousCenter = center;
            m_previousRotation = rotation;
            m_hasPreviousPose = true;
        }
    }
}
