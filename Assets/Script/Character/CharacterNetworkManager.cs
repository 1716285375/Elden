using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    public class CharacterNetworkManager : NetworkBehaviour
    {
        [Header("Position")]
        public NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("Rotation")]
        public NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [SerializeField, Min(0.001f)] private float networkPositionSmoothTime = 0.1f;
        [SerializeField, Min(0.001f)] private float networkRotationSmoothTime = 0.1f;

        private Vector3 networkPositionVelocity;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                networkPosition.Value = transform.position;
                networkRotation.Value = transform.rotation;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    networkPosition.Value,
                    ref networkPositionVelocity,
                    networkPositionSmoothTime);

                float rotationInterpolation = 1f - Mathf.Exp(-Time.deltaTime / networkRotationSmoothTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    networkRotation.Value,
                    rotationInterpolation);
            }
        }
    }
}
