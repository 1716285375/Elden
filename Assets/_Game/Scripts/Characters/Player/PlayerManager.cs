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
        private const string k_StartingGameplaySceneName =
            WorldScenePathLayout.GameplaySceneName;
        private const string k_SpawnPointName = "Player Spawn Point";
        private const float k_UninitializedPositionSqrMagnitude = 0.0001f;
        private const float k_BelowTerrainTolerance = 0.5f;
        private int m_originId;

        [SerializeField] private PlayerAnimatorManager m_playerAnimatorManager;
        [SerializeField] private WorldLocationSceneSet m_areaCurrentlyIn;

        [Header("DEBUG")]
        [SerializeField] private bool m_respawnCharacter;

        private bool m_isDeathInputBlocked;
        private bool m_isSpawnCameraPresentationPending;

        public PlayerAnimatorManager PlayerAnimatorManager => m_playerAnimatorManager;
        public PlayerNetworkManager PlayerNetworkManager { get; private set; }
        public PlayerStatsManager PlayerStatsManager { get; private set; }
        public PlayerLocomotionManager LocomotionManager { get; private set; }
        public PlayerLockOnManager LockOnManager { get; private set; }
        public PlayerInteractionManager InteractionManager { get; private set; }

        /// <summary>Gets the server-tracked logical world location.</summary>
        public WorldLocationSceneSet AreaCurrentlyIn => m_areaCurrentlyIn;

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

        /// <summary>Updates the server-owned logical world location reference.</summary>
        internal void SetAreaCurrentlyIn(WorldLocationSceneSet worldLocation)
        {
            m_areaCurrentlyIn = worldLocation;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            RegisterLocalPlayerForSaveData();
            WorldGameSessionManager.Instance?.AddPlayer(this);
            bool placedAtStartingSpawn =
                TryPlaceAtSpawnPoint(SceneManager.GetActiveScene());
            RestoreSpawnedClientState();
            PlayerCombatManager?.RestoreDeadSpotFromSaveIfNeeded();
            m_isSpawnCameraPresentationPending =
                IsOwner && placedAtStartingSpawn;
            BindLocalPlayerSystems();
            TryBeginSpawnCameraPresentation();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            RegisterLocalPlayerForSaveData();
            m_isSpawnCameraPresentationPending = IsInGameplayScene;
            BindLocalPlayerSystems();
            TryBeginSpawnCameraPresentation();
        }

        public override void OnLostOwnership()
        {
            ResetLocalDeathPresentation();
            m_isSpawnCameraPresentationPending = false;
            LockOnManager?.ClearLockOn();
            WorldSaveGameManager.Instance?.UnregisterPlayer(this);
            PlayerCamera.Instance?.ClearPlayer(this);
            PlayerInputManager.Instance?.ClearPlayer(this);
            PlayerUIManager.Instance?.UnbindLocalPlayer(this);
            InteractionManager?.ClearInteractions();
            PlayerUIManager.Instance?.PlayerUIHUDManager?.UnbindQuickSlots(
                InventoryManager);
            PlayerStatsManager?.UnbindLocalHUD();
            base.OnLostOwnership();
        }

        public override void OnNetworkDespawn()
        {
            ResetLocalDeathPresentation();
            m_isSpawnCameraPresentationPending = false;
            LockOnManager?.ClearLockOn();
            WorldSaveGameManager.Instance?.UnregisterPlayer(this);
            WorldGameSessionManager.Instance?.RemovePlayer(this);
            PlayerCamera.Instance?.ClearPlayer(this);
            PlayerInputManager.Instance?.ClearPlayer(this);
            PlayerUIManager.Instance?.UnbindLocalPlayer(this);
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
            TryBeginSpawnCameraPresentation();
            PlayerCamera camera = PlayerCamera.Instance;
            camera?.HandleAllCameraActions();
            LockOnManager?.PresentTarget(camera?.CameraObject);
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
                    PlayerNetworkManager.CurrentFocusPoints.Value =
                        Mathf.Max(0f, PlayerNetworkManager.MaxFocusPoints.Value);
                    PlayerNetworkManager.RefillFlasks();
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
            InventoryManager.InitializeMainProjectileFromID(
                PlayerNetworkManager.MainProjectileID.Value);
            InventoryManager.InitializeSecondaryProjectileFromID(
                PlayerNetworkManager.SecondaryProjectileID.Value);
            PlayerNetworkManager.RefreshTwoHandingPresentation();
            PlayerNetworkManager.RefreshArmorPresentation();
            if (!IsOwner && PlayerNetworkManager.HasArrowNotched.Value)
            {
                PlayerCombatManager?.PerformNotchingProjectileFromRpc(
                    PlayerNetworkManager.CurrentProjectileID.Value,
                    PlayerNetworkManager.CurrentProjectileSlot.Value);
            }
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

            PopulateCharacterSaveData(currentData);
        }

        /// <summary>Builds an independent save snapshot for the title-screen creation flow.</summary>
        public CharacterSaveData CreateCharacterSaveData()
        {
            if (!IsOwner)
            {
                return null;
            }

            CharacterSaveData characterSaveData = new();
            PopulateCharacterSaveData(characterSaveData);
            return characterSaveData;
        }

        /// <summary>Applies one starting class to owner attributes and equipment.</summary>
        public void ApplyCharacterClass(CharacterClass characterClass)
        {
            if (!IsOwner || characterClass == null)
            {
                return;
            }

            CharacterNetworkManager.Vitality.Value = characterClass.Vitality;
            CharacterNetworkManager.Endurance.Value = characterClass.Endurance;
            CharacterNetworkManager.Mind.Value = characterClass.Mind;
            CharacterNetworkManager.Strength.Value = characterClass.Strength;
            CharacterNetworkManager.Dexterity.Value = characterClass.Dexterity;
            CharacterNetworkManager.Intelligence.Value = characterClass.Intelligence;
            CharacterNetworkManager.Faith.Value = characterClass.Faith;
            CharacterNetworkManager.Arcane.Value = characterClass.Arcane;
            m_originId = characterClass.OriginId;
            PlayerNetworkManager.CurrentSpellID.Value = -1;
            InventoryManager.InitializeCurrentSpellFromID(-1);
            PlayerStatsManager.SetNewMaxHealthValue();
            PlayerStatsManager.SetNewMaxStaminaValue();
            PlayerStatsManager.SetNewMaxFocusPointsValue();
            InventoryManager.ApplyCharacterClassEquipment(characterClass);
            ApplyStartingFlaskAmounts(characterClass);
        }

        private void PopulateCharacterSaveData(CharacterSaveData currentData)
        {

            Vector3 position = transform.position;
            currentData.CharacterName = PlayerNetworkManager.CharacterName.Value.ToString();
            currentData.XPosition = position.x;
            currentData.YPosition = position.y;
            currentData.ZPosition = position.z;
            currentData.SceneIndex = SceneManager.GetActiveScene().buildIndex;
            currentData.Vitality = CharacterNetworkManager.Vitality.Value;
            currentData.Endurance = CharacterNetworkManager.Endurance.Value;
            currentData.Mind = CharacterNetworkManager.Mind.Value;
            currentData.Strength = CharacterNetworkManager.Strength.Value;
            currentData.Dexterity = CharacterNetworkManager.Dexterity.Value;
            currentData.Intelligence = CharacterNetworkManager.Intelligence.Value;
            currentData.Faith = CharacterNetworkManager.Faith.Value;
            currentData.Arcane = CharacterNetworkManager.Arcane.Value;
            currentData.OriginId = m_originId;
            currentData.Runes = PlayerStatsManager.Runes;
            currentData.CurrentHealth = CharacterNetworkManager.CurrentHealth.Value;
            currentData.CurrentStamina = CharacterNetworkManager.CurrentStamina.Value;
            currentData.CurrentFocusPoints =
                CharacterNetworkManager.CurrentFocusPoints.Value;
            currentData.CurrentHealthFlasksRemaining =
                PlayerNetworkManager.RemainingHealthFlasks.Value;
            currentData.HealthFlaskCapacity = PlayerNetworkManager.HealthFlaskCapacity;
            currentData.FocusPointFlaskCapacity = PlayerNetworkManager.FocusPointFlaskCapacity;
            currentData.CurrentFocusPointFlasksRemaining =
                PlayerNetworkManager.RemainingFocusPointFlasks.Value;
            currentData.CurrentSpellID = InventoryManager.CurrentSpell?.ItemID ?? -1;
            currentData.MainProjectile = WorldSaveGameManager
                .GetSerializableProjectileFromProjectileItem(
                    InventoryManager.MainProjectile);
            currentData.SecondaryProjectile = WorldSaveGameManager
                .GetSerializableProjectileFromProjectileItem(
                    InventoryManager.SecondaryProjectile);
            currentData.HeadEquipmentID = PlayerNetworkManager.CurrentHeadEquipmentID.Value;
            currentData.BodyEquipmentID = PlayerNetworkManager.CurrentBodyEquipmentID.Value;
            currentData.HandEquipmentID = PlayerNetworkManager.CurrentHandEquipmentID.Value;
            currentData.LegEquipmentID = PlayerNetworkManager.CurrentLegEquipmentID.Value;
            currentData.RightHandWeaponSlot01 = WorldSaveGameManager
                .GetSerializableWeaponFromWeaponItem(
                    InventoryManager.GetRightHandQuickSlotItem(0));
            currentData.RightHandWeaponSlot02 = WorldSaveGameManager
                .GetSerializableWeaponFromWeaponItem(
                    InventoryManager.GetRightHandQuickSlotItem(1));
            currentData.RightHandWeaponSlot03 = WorldSaveGameManager
                .GetSerializableWeaponFromWeaponItem(
                    InventoryManager.GetRightHandQuickSlotItem(2));
            currentData.LeftHandWeaponSlot01 = WorldSaveGameManager
                .GetSerializableWeaponFromWeaponItem(
                    InventoryManager.GetLeftHandQuickSlotItem(0));
            currentData.LeftHandWeaponSlot02 = WorldSaveGameManager
                .GetSerializableWeaponFromWeaponItem(
                    InventoryManager.GetLeftHandQuickSlotItem(1));
            currentData.LeftHandWeaponSlot03 = WorldSaveGameManager
                .GetSerializableWeaponFromWeaponItem(
                    InventoryManager.GetLeftHandQuickSlotItem(2));
            currentData.RightHandWeaponIndex = InventoryManager.RightHandWeaponIndex;
            currentData.LeftHandWeaponIndex = InventoryManager.LeftHandWeaponIndex;
            currentData.QuickSlotItem01 = WorldSaveGameManager
                .GetSerializableQuickSlotItemFromQuickSlotItem(
                    InventoryManager.GetQuickSlotItem(0),
                    this);
            currentData.QuickSlotItem02 = WorldSaveGameManager
                .GetSerializableQuickSlotItemFromQuickSlotItem(
                    InventoryManager.GetQuickSlotItem(1),
                    this);
            currentData.QuickSlotItem03 = WorldSaveGameManager
                .GetSerializableQuickSlotItemFromQuickSlotItem(
                    InventoryManager.GetQuickSlotItem(2),
                    this);
            currentData.QuickSlotItemIndex = InventoryManager.QuickSlotItemIndex;
            SaveInventoryData(currentData);
            SaveStorageData(currentData);
            currentData.IsMale = PlayerNetworkManager.IsMale.Value;
            currentData.Appearance = PlayerNetworkManager.Appearance.Value.ToData();
            currentData.HairstyleID = currentData.Appearance.Hairstyle;
            currentData.HairColorRed = currentData.Appearance.HairColor.r;
            currentData.HairColorGreen = currentData.Appearance.HairColor.g;
            currentData.HairColorBlue = currentData.Appearance.HairColor.b;
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
            CharacterNetworkManager.Strength.Value = currentData.Strength;
            CharacterNetworkManager.Dexterity.Value = currentData.Dexterity;
            CharacterNetworkManager.Intelligence.Value = currentData.Intelligence;
            CharacterNetworkManager.Faith.Value = currentData.Faith;
            CharacterNetworkManager.Arcane.Value = currentData.Arcane;
            m_originId = currentData.OriginId;
            PlayerStatsManager.SetRunes(currentData.Runes);
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
            PlayerNetworkManager.Appearance.Value = CharacterAppearanceState.FromData(currentData.Appearance);
            InventoryManager.RestoreInventory(currentData);
            InventoryManager.RestoreStorage(currentData);
            PlayerNetworkManager.ConfigureFlaskCapacity(
                currentData.HealthFlaskCapacity, currentData.FocusPointFlaskCapacity);
            PlayerNetworkManager.RemainingHealthFlasks.Value =
                currentData.CurrentHealthFlasksRemaining;
            PlayerNetworkManager.RemainingFocusPointFlasks.Value =
                currentData.CurrentFocusPointFlasksRemaining;
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
                    currentData.RightHandWeaponSlot01,
                    currentData.RightHandWeaponSlot02,
                    currentData.RightHandWeaponSlot03
                },
                new[]
                {
                    currentData.LeftHandWeaponSlot01,
                    currentData.LeftHandWeaponSlot02,
                    currentData.LeftHandWeaponSlot03
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
            InventoryManager.RestoreProjectileLoadout(
                currentData.MainProjectile,
                currentData.SecondaryProjectile);
            InventoryManager.RestoreQuickSlotLoadout(
                new[]
                {
                    currentData.QuickSlotItem01,
                    currentData.QuickSlotItem02,
                    currentData.QuickSlotItem03
                },
                currentData.QuickSlotItemIndex);
            PlayerUIHUDManager hudManager = PlayerUIManager.Instance
                ?.PlayerUIHUDManager;
            hudManager?.SetQuickSlotItemQuickSlotIcon(
                InventoryManager.CurrentQuickSlotItem);
            hudManager?.SetMainProjectileQuickSlotIcon(
                InventoryManager.MainProjectile);
            hudManager?.SetSecondaryProjectileQuickSlotIcon(
                InventoryManager.SecondaryProjectile);
            Vector3 savedPosition = new Vector3(
                currentData.XPosition,
                currentData.YPosition,
                currentData.ZPosition);
            Vector3 loadPosition = ResolveSafeLoadPosition(savedPosition);
            LocomotionManager.WarpTo(loadPosition, transform.rotation);
            CharacterNetworkManager.NetworkPosition.Value = transform.position;
            CharacterNetworkManager.NetworkRotation.Value = transform.rotation;
            PlayerCamera.Instance?.SnapToPlayerAndResetRotation(this);
        }

        private void ApplyStartingFlaskAmounts(CharacterClass characterClass)
        {
            int healthFlasks = 0;
            int focusPointFlasks = 0;
            QuickSlotItem[] quickSlotItems = characterClass.QuickSlotItems;
            if (quickSlotItems != null)
            {
                for (int slotIndex = 0; slotIndex < quickSlotItems.Length; slotIndex++)
                {
                    if (quickSlotItems[slotIndex] is not FlaskItem flaskItem)
                    {
                        continue;
                    }

                    if (flaskItem.RestoresHealth)
                    {
                        healthFlasks += characterClass.GetQuickSlotItemAmount(slotIndex);
                    }
                    else
                    {
                        focusPointFlasks += characterClass.GetQuickSlotItemAmount(slotIndex);
                    }
                }
            }

            PlayerNetworkManager.ConfigureFlaskCapacity(healthFlasks, focusPointFlasks);
            PlayerNetworkManager.RefillFlasks();
        }

        private void SaveInventoryData(CharacterSaveData currentData)
        {
            currentData.ClearInventoryData();
            foreach (Item item in InventoryManager.ItemsInInventory)
            {
                switch (item)
                {
                    case WeaponItem weapon:
                        currentData.WeaponsInInventory.Add(WorldSaveGameManager
                            .GetSerializableWeaponFromWeaponItem(weapon));
                        break;
                    case RangedProjectileItem projectile:
                        currentData.ProjectilesInInventory.Add(
                            WorldSaveGameManager
                                .GetSerializableProjectileFromProjectileItem(
                                    projectile));
                        break;
                    case QuickSlotItem quickSlotItem:
                        currentData.QuickSlotItemsInInventory.Add(
                            WorldSaveGameManager
                                .GetSerializableQuickSlotItemFromQuickSlotItem(
                                    quickSlotItem,
                                    this));
                        break;
                    case Item stackableItem when stackableItem.IsStackable:
                        currentData.StackableItemsInInventory.Add(
                            WorldSaveGameManager
                                .GetSerializableItemStackFromItem(
                                    stackableItem));
                        break;
                    case HeadEquipmentItem headEquipment:
                        currentData.HeadEquipmentInInventory.Add(
                            headEquipment.ItemID);
                        break;
                    case BodyEquipmentItem bodyEquipment:
                        currentData.BodyEquipmentInInventory.Add(
                            bodyEquipment.ItemID);
                        break;
                    case HandEquipmentItem handEquipment:
                        currentData.HandEquipmentInInventory.Add(
                            handEquipment.ItemID);
                        break;
                    case LegEquipmentItem legEquipment:
                        currentData.LegEquipmentInInventory.Add(
                            legEquipment.ItemID);
                        break;
                }
            }
        }

        private void SaveStorageData(CharacterSaveData currentData)
        {
            SerializableInventoryData storage = currentData.StorageInventory;
            storage.Clear();
            foreach (Item item in InventoryManager.ItemsInStorage)
            {
                switch (item)
                {
                    case WeaponItem weapon:
                        storage.Weapons.Add(WorldSaveGameManager
                            .GetSerializableWeaponFromWeaponItem(weapon));
                        break;
                    case RangedProjectileItem projectile:
                        storage.Projectiles.Add(WorldSaveGameManager
                            .GetSerializableProjectileFromProjectileItem(
                                projectile));
                        break;
                    case QuickSlotItem quickSlotItem:
                        storage.QuickSlotItems.Add(WorldSaveGameManager
                            .GetSerializableQuickSlotItemFromQuickSlotItem(
                                quickSlotItem,
                                this));
                        break;
                    case Item stackableItem when stackableItem.IsStackable:
                        storage.StackableItems.Add(WorldSaveGameManager
                            .GetSerializableItemStackFromItem(stackableItem));
                        break;
                    case HeadEquipmentItem headEquipment:
                        storage.HeadEquipment.Add(headEquipment.ItemID);
                        break;
                    case BodyEquipmentItem bodyEquipment:
                        storage.BodyEquipment.Add(bodyEquipment.ItemID);
                        break;
                    case HandEquipmentItem handEquipment:
                        storage.HandEquipment.Add(handEquipment.ItemID);
                        break;
                    case LegEquipmentItem legEquipment:
                        storage.LegEquipment.Add(legEquipment.ItemID);
                        break;
                }
            }
        }

        private void BindLocalPlayerSystems()
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerCamera.Instance?.BindPlayer(this);
            PlayerInputManager.Instance?.BindPlayer(this);
            PlayerUIManager.Instance?.BindLocalPlayer(this);
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
            if (!TryPlaceAtSpawnPoint(scene))
            {
                return;
            }

            WorldSaveGameManager.Instance?.TryApplyLoadedCharacterData(this);
            PlayerCombatManager?.RestoreDeadSpotFromSaveIfNeeded();
            m_isSpawnCameraPresentationPending = IsOwner;
            BindLocalPlayerSystems();
            TryBeginSpawnCameraPresentation();
        }

        private bool TryPlaceAtSpawnPoint(Scene scene)
        {
            if (!IsSpawned || !IsOwner || scene.name != k_StartingGameplaySceneName)
            {
                return false;
            }

            Transform spawnPoint = FindSpawnPoint(scene);
            if (spawnPoint == null)
            {
                Debug.LogError(
                    $"Could not find '{k_SpawnPointName}' in {k_StartingGameplaySceneName}.");
                return false;
            }

            LocomotionManager.WarpTo(spawnPoint.position, spawnPoint.rotation);
            if (CharacterNetworkManager != null)
            {
                CharacterNetworkManager.NetworkPosition.Value = transform.position;
                CharacterNetworkManager.NetworkRotation.Value = transform.rotation;
            }

            return true;
        }

        private void TryBeginSpawnCameraPresentation()
        {
            if (!m_isSpawnCameraPresentationPending || !IsOwner)
            {
                return;
            }

            PlayerCamera playerCamera = PlayerCamera.Instance;
            if (playerCamera == null)
            {
                return;
            }

            playerCamera.BeginSpawnIntroduction(this);
            m_isSpawnCameraPresentationPending = false;
        }

        private static Transform FindSpawnPoint(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

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

        private Vector3 ResolveSafeLoadPosition(Vector3 savedPosition)
        {
            if (IsFinite(savedPosition) &&
                savedPosition.sqrMagnitude > k_UninitializedPositionSqrMagnitude &&
                IsSupportedByGround(savedPosition))
            {
                return savedPosition;
            }

            Scene gameplayScene = SceneManager.GetSceneByName(k_StartingGameplaySceneName);
            Transform spawnPoint = FindSpawnPoint(gameplayScene);
            Vector3 fallbackPosition = spawnPoint != null
                ? spawnPoint.position
                : transform.position;
            Debug.LogWarning(
                $"Saved player position {savedPosition} has no nearby ground. " +
                $"Using spawn point {fallbackPosition} instead.");
            return fallbackPosition;
        }

        private static bool IsSupportedByGround(Vector3 position)
        {
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * k_BelowTerrainTolerance,
                Vector3.down, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject.scene.name == k_StartingGameplaySceneName && hit.normal.y > 0.5f &&
                    hit.collider.GetComponentInParent<CharacterManager>() == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFinite(Vector3 position)
        {
            return !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
                !float.IsNaN(position.y) && !float.IsInfinity(position.y) &&
                !float.IsNaN(position.z) && !float.IsInfinity(position.z);
        }
    }
}
