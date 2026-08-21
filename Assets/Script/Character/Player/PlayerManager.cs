using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    [RequireComponent(typeof(PlayerLocomotionManager))]
    [RequireComponent(typeof(PlayerNetworkManager))]
    [RequireComponent(typeof(PlayerStatsManager))]
    public class PlayerManager : CharacterManager
    {
        private const string k_StartingGameplaySceneName = "Scene_World_01";
        private const string k_SpawnPointName = "Player Spawn Point";

        [SerializeField] private PlayerAnimatorManager m_playerAnimatorManager;

        public PlayerAnimatorManager PlayerAnimatorManager => m_playerAnimatorManager;
        public PlayerNetworkManager PlayerNetworkManager { get; private set; }
        public PlayerStatsManager PlayerStatsManager { get; private set; }
        public PlayerLocomotionManager LocomotionManager { get; private set; }
        public bool IsInGameplayScene => SceneManager.GetActiveScene().buildIndex > 0;

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
            RegisterLocalPlayerForSaveData();
            TryPlaceAtSpawnPoint(SceneManager.GetActiveScene());
            WorldSaveGameManager.Instance?.TryApplyLoadedCharacterData(this);
            BindLocalPlayerSystems();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            RegisterLocalPlayerForSaveData();
            BindLocalPlayerSystems();
        }

        public override void OnLostOwnership()
        {
            WorldSaveGameManager.Instance?.UnregisterPlayer(this);
            PlayerCamera.Instance?.ClearPlayer(this);
            PlayerInputManager.Instance?.ClearPlayer(this);
            PlayerStatsManager?.UnbindLocalHUD();
            base.OnLostOwnership();
        }

        public override void OnNetworkDespawn()
        {
            WorldSaveGameManager.Instance?.UnregisterPlayer(this);
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

        /// <summary>
        /// Copies the locally owned player's runtime name, position, and Scene into current save data.
        /// </summary>
        public void SaveGameDataToCurrentCharacterData()
        {
            CharacterSaveData currentData = WorldSaveGameManager.Instance?.CurrentCharacterData;
            if (!IsOwner || currentData == null)
            {
                Debug.LogWarning("Only a locally owned player with current character data can be saved.");
                return;
            }

            Vector3 position = transform.position;
            currentData.CharacterName = PlayerNetworkManager.CharacterName.Value.ToString();
            currentData.XPosition = position.x;
            currentData.YPosition = position.y;
            currentData.ZPosition = position.z;
            currentData.SceneIndex = SceneManager.GetActiveScene().buildIndex;
        }

        /// <summary>
        /// Applies current save data to the locally owned player's name and position.
        /// </summary>
        public void LoadGameDataFromCurrentCharacterData()
        {
            CharacterSaveData currentData = WorldSaveGameManager.Instance?.CurrentCharacterData;
            if (!IsOwner || currentData == null)
            {
                Debug.LogWarning("Only a locally owned player with current character data can be loaded.");
                return;
            }

            PlayerNetworkManager.CharacterName.Value =
                new FixedString64Bytes(currentData.CharacterName);
            Vector3 savedPosition = new Vector3(
                currentData.XPosition,
                currentData.YPosition,
                currentData.ZPosition);
            LocomotionManager.WarpTo(savedPosition, transform.rotation);
            CharacterNetworkManager.NetworkPosition.Value = transform.position;
            CharacterNetworkManager.NetworkRotation.Value = transform.rotation;
            PlayerCamera.Instance?.SnapToPlayerAndResetRotation(this);
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

        private void RegisterLocalPlayerForSaveData()
        {
            if (IsOwner)
            {
                WorldSaveGameManager.Instance?.RegisterPlayer(this);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            TryPlaceAtSpawnPoint(scene);
            WorldSaveGameManager.Instance?.TryApplyLoadedCharacterData(this);
        }

        private void TryPlaceAtSpawnPoint(Scene scene)
        {
            if (!IsSpawned || !IsOwner || scene.name != k_StartingGameplaySceneName)
            {
                return;
            }

            Transform spawnPoint = FindSpawnPoint(scene);
            if (spawnPoint == null)
            {
                Debug.LogError(
                    $"Could not find '{k_SpawnPointName}' in {k_StartingGameplaySceneName}.");
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
