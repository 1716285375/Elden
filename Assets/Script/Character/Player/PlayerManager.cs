using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(PlayerLocomotionManager))]
    [RequireComponent(typeof(PlayerNetworkManager))]
    public class PlayerManager : CharacterManager
    {
        public PlayerLocomotionManager LocomotionManager { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            LocomotionManager = GetComponent<PlayerLocomotionManager>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner && PlayerCamera.Instance != null)
            {
                PlayerCamera.Instance.BindPlayer(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && PlayerCamera.Instance != null)
            {
                PlayerCamera.Instance.ClearPlayer(this);
            }

            base.OnNetworkDespawn();
        }

        protected override void Update()
        {
            base.Update();

            if (!IsOwner)
            {
                return;
            }

            LocomotionManager.HandleAllMovement();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (!IsOwner)
            {
                return;
            }

            PlayerCamera.Instance?.HandleAllCameraActions();
        }
    }
}
