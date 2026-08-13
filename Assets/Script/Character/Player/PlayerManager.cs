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
            BindLocalCamera();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            BindLocalCamera();
        }

        public override void OnLostOwnership()
        {
            PlayerCamera.Instance?.ClearPlayer(this);
            base.OnLostOwnership();
        }

        public override void OnNetworkDespawn()
        {
            PlayerCamera.Instance?.ClearPlayer(this);
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

            BindLocalCamera();
            PlayerCamera.Instance?.HandleAllCameraActions();
        }

        private void BindLocalCamera()
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerCamera.Instance?.BindPlayer(this);
        }
    }
}
