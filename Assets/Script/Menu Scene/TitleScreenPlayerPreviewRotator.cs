using UnityEngine;

namespace ZZ
{
    /// <summary>Rotates the creation preview from keyboard or gamepad camera input.</summary>
    public class TitleScreenPlayerPreviewRotator : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float m_rotationSpeed = 90f;

        private Transform m_target;

        private void Update()
        {
            float horizontalInput = PlayerInputManager.Instance?.CameraInput.x ?? 0f;
            if (m_target == null || Mathf.Approximately(horizontalInput, 0f))
            {
                return;
            }

            m_target.Rotate(
                Vector3.up,
                -horizontalInput * m_rotationSpeed * Time.unscaledDeltaTime,
                Space.World);
        }

        /// <summary>Assigns the locally owned preview player to rotate.</summary>
        public void BindTarget(Transform target)
        {
            m_target = target;
        }
    }
}
