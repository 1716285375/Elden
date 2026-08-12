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

        protected override void Update()
        {
            base.Update();

            if (!IsOwner)
            {
                return;
            }

            LocomotionManager.HandleAllMovement();
        }
    }
}
