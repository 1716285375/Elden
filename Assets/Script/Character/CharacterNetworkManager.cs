using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    public class CharacterNetworkManager : NetworkBehaviour
    {
        [Header("Position")]
        public NetworkVariable<Vector3> NetworkPosition = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("Rotation")]
        public NetworkVariable<Quaternion> NetworkRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("Animation")]
        public NetworkVariable<float> HorizontalMovement = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> VerticalMovement = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> MoveAmount = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [FormerlySerializedAs("networkPositionSmoothTime")]
        [SerializeField, Min(0.001f)] private float m_networkPositionSmoothTime = 0.1f;
        [FormerlySerializedAs("networkRotationSmoothTime")]
        [SerializeField, Min(0.001f)] private float m_networkRotationSmoothTime = 0.1f;

        private Vector3 m_networkPositionVelocity;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            NetworkPosition.Value = transform.position;
            NetworkRotation.Value = transform.rotation;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    NetworkPosition.Value,
                    ref m_networkPositionVelocity,
                    m_networkPositionSmoothTime);

                float rotationInterpolation = 1f - Mathf.Exp(-Time.deltaTime / m_networkRotationSmoothTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    NetworkRotation.Value,
                    rotationInterpolation);
            }
        }
    }
}
