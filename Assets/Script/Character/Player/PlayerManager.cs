using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    [RequireComponent(typeof(PlayerLocomotionManager))]
    [RequireComponent(typeof(PlayerNetworkManager))]
    [RequireComponent(typeof(PlayerStatsManager))]
    public class PlayerManager : CharacterManager
    {
        private const string k_GameplaySceneName = "Scene_World_01";
        private const string k_SpawnPointName = "Player Spawn Point";

        [SerializeField] private PlayerAnimatorManager m_playerAnimatorManager;

        public PlayerAnimatorManager PlayerAnimatorManager => m_playerAnimatorManager;
        public PlayerNetworkManager PlayerNetworkManager { get; private set; }
        public PlayerStatsManager PlayerStatsManager { get; private set; }
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
            m_playerAnimatorManager = GetComponentInChildren<PlayerAnimatorManager>(true);
            PlayerNetworkManager = GetComponent<PlayerNetworkManager>();
            PlayerStatsManager = GetComponent<PlayerStatsManager>();
            LocomotionManager = GetComponent<PlayerLocomotionManager>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            TryPlaceAtSpawnPoint(SceneManager.GetActiveScene());
            BindLocalPlayerSystems();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            BindLocalPlayerSystems();
        }

        public override void OnLostOwnership()
        {
            PlayerCamera.Instance?.ClearPlayer(this);
            PlayerInputManager.Instance?.ClearPlayer(this);
            PlayerStatsManager?.UnbindLocalHUD();
            base.OnLostOwnership();
        }

        public override void OnNetworkDespawn()
        {
            PlayerCamera.Instance?.ClearPlayer(this);
            PlayerInputManager.Instance?.ClearPlayer(this);
            PlayerStatsManager?.UnbindLocalHUD();
            base.OnNetworkDespawn();
        }

        private void LateUpdate()
        {
            if (!IsOwner)
            {
                return;
            }

            BindLocalPlayerSystems();
            PlayerCamera.Instance?.HandleAllCameraActions();
        }

        private void BindLocalPlayerSystems()
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerCamera.Instance?.BindPlayer(this);
            PlayerInputManager.Instance?.BindPlayer(this);
            PlayerStatsManager?.BindLocalHUD();
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
