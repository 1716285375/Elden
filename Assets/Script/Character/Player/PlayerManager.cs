using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    [RequireComponent(typeof(PlayerLocomotionManager))]
    [RequireComponent(typeof(PlayerNetworkManager))]
    [RequireComponent(typeof(PlayerAnimatorManager))]
    public class PlayerManager : CharacterManager
    {
        private const string k_GameplaySceneName = "Scene_World_01";
        private const string k_SpawnPointName = "Player Spawn Point";

        [SerializeField] private PlayerAnimatorManager m_playerAnimatorManager;

        public PlayerAnimatorManager PlayerAnimatorManager => m_playerAnimatorManager;
        public PlayerLocomotionManager LocomotionManager { get; private set; }
        public bool IsInGameplayScene =>
            SceneManager.GetActiveScene().name == k_GameplaySceneName;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        protected override void Awake()
        {
            base.Awake();
            m_playerAnimatorManager = GetComponent<PlayerAnimatorManager>();
            LocomotionManager = GetComponent<PlayerLocomotionManager>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            TryPlaceAtSpawnPoint(SceneManager.GetActiveScene());
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

        private void LateUpdate()
        {
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

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            TryPlaceAtSpawnPoint(scene);
        }

        private void TryPlaceAtSpawnPoint(Scene scene)
        {
            if (!IsSpawned || !IsOwner || scene.name != k_GameplaySceneName)
            {
                return;
            }

            Transform spawnPoint = FindSpawnPoint(scene);
            if (spawnPoint == null)
            {
                Debug.LogError($"Could not find '{k_SpawnPointName}' in {k_GameplaySceneName}.");
                return;
            }

            LocomotionManager.WarpTo(spawnPoint.position, spawnPoint.rotation);
            if (CharacterNetworkManager != null)
            {
                CharacterNetworkManager.NetworkPosition.Value = transform.position;
                CharacterNetworkManager.NetworkRotation.Value = transform.rotation;
            }

            PlayerCamera.Instance?.SnapToPlayerAndResetRotation(this);
        }

        private static Transform FindSpawnPoint(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (Transform candidate in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == k_SpawnPointName)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }
}
