using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns the authored menu presentation layer of the title Scene: the menu
    /// camera composition, the placeholder environment, and the hero placeholder.
    /// Camera <b>handover</b> between the menu presentation and the persistent
    /// gameplay rig is the job of <see cref="TitleScreenCameraCoordinator"/>;
    /// this controller only configures composition and runs cheap idle motion.
    /// </summary>
    public class TitleScreenPresentationController : MonoBehaviour
    {
        [Header("Composition")]
        [SerializeField] private Camera m_menuCamera;
        [SerializeField, Range(20f, 90f)] private float m_compositionFieldOfView = 42f;
        [SerializeField] private Vector3 m_compositionPosition = new(0f, 2.2f, -7.5f);

        [Header("Presentation")]
        [SerializeField] private Transform m_heroRoot;
        [SerializeField, Min(0f)] private float m_heroIdleAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float m_heroIdleSpeed = 0.6f;

        private Vector3 m_heroBasePosition;
        private Quaternion m_heroBaseRotation;
        private bool m_hasHero;

        private void Start()
        {
            ApplyComposition();
            CaptureHeroPose();
        }

        private void Update()
        {
            AnimateHeroIdle();
        }

        private void ApplyComposition()
        {
            if (m_menuCamera == null)
            {
                m_menuCamera = GetComponentInChildren<Camera>();
            }

            if (m_menuCamera == null)
            {
                return;
            }

            m_menuCamera.fieldOfView = m_compositionFieldOfView;
            m_menuCamera.transform.position = m_compositionPosition;
        }

        private void CaptureHeroPose()
        {
            if (m_heroRoot == null)
            {
                return;
            }

            m_heroBasePosition = m_heroRoot.position;
            m_heroBaseRotation = m_heroRoot.rotation;
            m_hasHero = true;
        }

        private void AnimateHeroIdle()
        {
            if (!m_hasHero || Mathf.Approximately(m_heroIdleAmplitude, 0f))
            {
                return;
            }

            float phase = Time.unscaledTime * m_heroIdleSpeed;
            float bob = Mathf.Sin(phase) * m_heroIdleAmplitude;
            float sway = Mathf.Sin(phase * 0.5f) * m_heroIdleAmplitude * 0.5f;
            m_heroRoot.position = m_heroBasePosition + Vector3.up * bob;
            m_heroRoot.rotation = m_heroBaseRotation * Quaternion.Euler(0f, sway, 0f);
        }
    }
}
