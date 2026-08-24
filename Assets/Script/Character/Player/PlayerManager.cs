using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    [RequireComponent(typeof(PlayerLocomotionManager))]
    [RequireComponent(typeof(PlayerNetworkManager))]
    [RequireComponent(typeof(PlayerStatsManager))]
    [RequireComponent(typeof(PlayerInventoryManager))]
    [RequireComponent(typeof(PlayerEquipmentManager))]
    [RequireComponent(typeof(PlayerBodyManager))]
    [RequireComponent(typeof(PlayerCombatManager))]
    [RequireComponent(typeof(PlayerLockOnManager))]
    [RequireComponent(typeof(PlayerInteractionManager))]
    public class PlayerManager : CharacterManager
    {
        private const string k_StartingGameplaySceneName = "Scene_World_01";
        private const string k_SpawnPointName = "Player Spawn Point";

        [SerializeField] private PlayerAnimatorManager m_playerAnimatorManager;

        [Header("DEBUG")]
        [SerializeField] private bool m_respawnCharacter;

        private bool m_isDeathInputBlocked;

        public PlayerAnimatorManager PlayerAnimatorManager => m_playerAnimatorManager;
        public PlayerNetworkManager PlayerNetworkManager { get; private set; }
        public PlayerStatsManager PlayerStatsManager { get; private set; }
        public PlayerLocomotionManager LocomotionManager { get; private set; }
        public PlayerLockOnManager LockOnManager { get; private set; }
        public PlayerInteractionManager InteractionManager { get; private set; }

        /// <summary>Gets the player's quick-slot and runtime item state.</summary>
        public PlayerInventoryManager InventoryManager { get; private set; }

        /// <summary>Gets the player's hand-model presentation manager.</summary>
        public PlayerEquipmentManager EquipmentManager { get; private set; }

        /// <summary>Gets the modular body and gender presentation manager.</summary>
        public PlayerBodyManager BodyManager { get; private set; }

        /// <summary>Gets the player's weapon-action combat manager.</summary>
        public PlayerCombatManager PlayerCombatManager { get; private set; }
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
            InventoryManager = GetComponent<PlayerInventoryManager>();
            EquipmentManager = GetComponent<PlayerEquipmentManager>();
            BodyManager = GetComponent<PlayerBodyManager>();
            PlayerCombatManager = GetComponent<PlayerCombatManager>();
            LockOnManager = GetComponent<PlayerLockOnManager>();
            InteractionManager = GetComponent<PlayerInteractionManager>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            RegisterLocalPlayerForSaveData();
            WorldGameSessionManager.Instance?.AddPlayer(this);
            TryPlaceAtSpawnPoint(SceneManager.GetActiveScene());
            RestoreSpawnedClientState();
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
            ResetLocalDeathPresentation();
            LockOnManager?.ClearLockOn();
            WorldSaveGameManager.Instance?.UnregisterPlayer(this);
            PlayerCamera.Instance?.ClearPlayer(this);
            PlayerInputManager.Instance?.ClearPlayer(this);
            InteractionManager?.ClearInteractions();
            PlayerUIManager.Instance?.PlayerUIHUDManager?.UnbindQuickSlots(
                InventoryManager);
            PlayerStatsManager?.UnbindLocalHUD();
            base.OnLostOwnership();
        }

        public override void OnNetworkDespawn()
        {
            ResetLocalDeathPresentation();
            LockOnManager?.ClearLockOn();
            WorldSaveGameManager.Instance?.UnregisterPlayer(this);
            WorldGameSessionManager.Instance?.RemovePlayer(this);
            PlayerCamera.Instance?.ClearPlayer(this);
            PlayerInputManager.Instance?.ClearPlayer(this);
            InteractionManager?.ClearInteractions();
            PlayerUIManager.Instance?.PlayerUIHUDManager?.UnbindQuickSlots(
                InventoryManager);
            PlayerStatsManager?.UnbindLocalHUD();
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!m_respawnCharacter)
            {
                return;
            }

            m_respawnCharacter = false;
            if (IsOwner && IsDead)
            {
                ReviveCharacter();
            }
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

        /// <inheritdoc />
        public override IEnumerator ProcessDeathEvent(
            bool manuallySelectDeathAnimation = false)
        {
            if (!BeginDeathEvent(manuallySelectDeathAnimation))
            {
                yield break;
            }

            if (IsOwner)
            {
                if (IsSpawned && PlayerNetworkManager != null)
                {
                    PlayerNetworkManager.IsSprinting.Value = false;
                }

                PlayerUIManager playerUIManager = PlayerUIManager.Instance;
                playerUIManager?.SetMenuInputBlocked(true);
                playerUIManager?.PlayerUISaveGameManager?.SetDeathInputBlocked(true);
                PlayerInputManager.Instance?.BlockGameplayInput();
                m_isDeathInputBlocked = true;
                playerUIManager?.PlayerUIPopUpManager?.SendYouDiedPopup();
            }

            yield return WaitForRevive();
        }

        /// <inheritdoc />
        public override void ReviveCharacter()
        {
            if (IsOwner && IsSpawned && CharacterNetworkManager != null)
            {
                CharacterNetworkManager.CurrentHealth.Value =
                    Mathf.Max(0f, CharacterNetworkManager.MaxHealth.Value);
                CharacterNetworkManager.CurrentStamina.Value =
                    Mathf.Max(0f, CharacterNetworkManager.MaxStamina.Value);
                if (PlayerNetworkManager != null)
                {
                    PlayerNetworkManager.IsSprinting.Value = false;
                }

                base.ReviveCharacter();
                CharacterNetworkManager.IsDead.Value = false;
                ResetLocalDeathPresentation();
                return;
            }

            base.ReviveCharacter();
        }

        /// <summary>
        /// Refreshes equipment presentation from already-synchronized network identifiers.
        /// Called for late-joining clients once existing players have spawned.
        /// </summary>
        public void LoadOtherPlayerCharacter()
        {
            if (InventoryManager == null || PlayerNetworkManager == null)
            {
                return;
            }

            InventoryManager.InitializeRightWeaponFromID(
                PlayerNetworkManager.CurrentRightHandWeaponID.Value);
            InventoryManager.InitializeLeftWeaponFromID(
                PlayerNetworkManager.CurrentLeftHandWeaponID.Value);
            PlayerNetworkManager.RefreshTwoHandingPresentation();
            PlayerNetworkManager.RefreshArmorPresentation();
        }

        /// <summary>
        /// Copies the locally owned player's identity, position, attributes, and resources into save data.
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
            currentData.Vitality = CharacterNetworkManager.Vitality.Value;
            currentData.Endurance = CharacterNetworkManager.Endurance.Value;
            currentData.Mind = CharacterNetworkManager.Mind.Value;
            currentData.CurrentHealth = CharacterNetworkManager.CurrentHealth.Value;
            currentData.CurrentStamina = CharacterNetworkManager.CurrentStamina.Value;
            currentData.CurrentFocusPoints =
                CharacterNetworkManager.CurrentFocusPoints.Value;
            currentData.CurrentSpellID = InventoryManager.CurrentSpell?.ItemID ?? -1;
            currentData.HeadEquipmentID = PlayerNetworkManager.CurrentHeadEquipmentID.Value;
            currentData.BodyEquipmentID = PlayerNetworkManager.CurrentBodyEquipmentID.Value;
            currentData.HandEquipmentID = PlayerNetworkManager.CurrentHandEquipmentID.Value;
            currentData.LegEquipmentID = PlayerNetworkManager.CurrentLegEquipmentID.Value;
            currentData.RightHandWeaponSlot01ID =
                InventoryManager.GetRightHandQuickSlotItemID(0);
            currentData.RightHandWeaponSlot02ID =
                InventoryManager.GetRightHandQuickSlotItemID(1);
            currentData.RightHandWeaponSlot03ID =
                InventoryManager.GetRightHandQuickSlotItemID(2);
            currentData.LeftHandWeaponSlot01ID =
                InventoryManager.GetLeftHandQuickSlotItemID(0);
            currentData.LeftHandWeaponSlot02ID =
                InventoryManager.GetLeftHandQuickSlotItemID(1);
            currentData.LeftHandWeaponSlot03ID =
                InventoryManager.GetLeftHandQuickSlotItemID(2);
            currentData.RightHandWeaponIndex = InventoryManager.RightHandWeaponIndex;
            currentData.LeftHandWeaponIndex = InventoryManager.LeftHandWeaponIndex;
            currentData.IsMale = PlayerNetworkManager.IsMale.Value;
        }

        /// <summary>
        /// Restores attributes, resource maxima, current resources, identity, and position in order.
        /// </summary>
        public void LoadGameDataFromCurrentCharacterData()
        {
            CharacterSaveData currentData = WorldSaveGameManager.Instance?.CurrentCharacterData;
            if (!IsOwner || currentData == null)
            {
                Debug.LogWarning("Only a locally owned player with current character data can be loaded.");
                return;
            }

            CharacterNetworkManager.Vitality.Value = currentData.Vitality;
            CharacterNetworkManager.Endurance.Value = currentData.Endurance;
            CharacterNetworkManager.Mind.Value = currentData.Mind;
            PlayerStatsManager.SetNewMaxHealthValue();
            PlayerStatsManager.SetNewMaxStaminaValue();
            PlayerStatsManager.SetNewMaxFocusPointsValue();
            CharacterNetworkManager.CurrentHealth.Value = Mathf.Clamp(
                currentData.CurrentHealth,
                0f,
                CharacterNetworkManager.MaxHealth.Value);
            CharacterNetworkManager.CurrentStamina.Value = Mathf.Clamp(
                currentData.CurrentStamina,
                0f,
                CharacterNetworkManager.MaxStamina.Value);
            CharacterNetworkManager.CurrentFocusPoints.Value = Mathf.Clamp(
                currentData.CurrentFocusPoints,
                0f,
                CharacterNetworkManager.MaxFocusPoints.Value);
            PlayerNetworkManager.CharacterName.Value =
                new FixedString64Bytes(currentData.CharacterName);
            PlayerNetworkManager.IsMale.Value = currentData.IsMale;
            PlayerNetworkManager.CurrentHeadEquipmentID.Value =
                currentData.HeadEquipmentID;
            PlayerNetworkManager.CurrentBodyEquipmentID.Value =
                currentData.BodyEquipmentID;
            PlayerNetworkManager.CurrentHandEquipmentID.Value =
                currentData.HandEquipmentID;
            PlayerNetworkManager.CurrentLegEquipmentID.Value =
                currentData.LegEquipmentID;
            InventoryManager.RestoreWeaponLoadout(
                new[]
                {
                    currentData.RightHandWeaponSlot01ID,
                    currentData.RightHandWeaponSlot02ID,
                    currentData.RightHandWeaponSlot03ID
                },
                new[]
                {
                    currentData.LeftHandWeaponSlot01ID,
                    currentData.LeftHandWeaponSlot02ID,
                    currentData.LeftHandWeaponSlot03ID
                },
                currentData.RightHandWeaponIndex,
                currentData.LeftHandWeaponIndex);
            SpellItem savedSpell = WorldItemDatabase.Instance?.GetSpellByID(
                currentData.CurrentSpellID);
            int resolvedSpellID = savedSpell != null
                ? savedSpell.ItemID
                : -1;
            PlayerNetworkManager.CurrentSpellID.Value = resolvedSpellID;
            InventoryManager.InitializeCurrentSpellFromID(resolvedSpellID);
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
            PlayerUIManager.Instance?.PlayerUIHUDManager?.BindQuickSlots(
                InventoryManager);
            PlayerStatsManager?.BindLocalHUD();
        }

        private void ResetLocalDeathPresentation()
        {
            if (!m_isDeathInputBlocked)
            {
                return;
            }

            PlayerUIManager playerUIManager = PlayerUIManager.Instance;
            playerUIManager?.SetMenuInputBlocked(false);
            playerUIManager?.PlayerUISaveGameManager?.SetDeathInputBlocked(false);
            playerUIManager?.PlayerUIPopUpManager?.HideYouDiedPopup();
            PlayerInputManager.Instance?.UnblockGameplayInput();
            m_isDeathInputBlocked = false;
        }

        private void RegisterLocalPlayerForSaveData()
        {
            if (IsOwner)
            {
                WorldSaveGameManager.Instance?.RegisterPlayer(this);
            }
        }

        private void RestoreSpawnedClientState()
        {
            WorldSaveGameManager saveGameManager = WorldSaveGameManager.Instance;
            if (saveGameManager == null)
            {
                return;
            }

            bool appliedPendingData = saveGameManager.TryApplyLoadedCharacterData(this);
            if (!appliedPendingData && IsOwner && !IsServer)
            {
                LoadGameDataFromCurrentCharacterData();
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
