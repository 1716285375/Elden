using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Resolves the locally owned player's equipped weapon action and executes combat logic.
    /// </summary>
    [RequireComponent(typeof(PlayerManager))]
    public class PlayerCombatManager : CharacterCombatManager
    {
        [SerializeField, Min(0f)] private float m_fullyChargedDuration = 0.8f;
        [SerializeField, Min(0f)] private float m_fullyChargedSpellDuration = 1.5f;
        [SerializeField] private bool m_canBlock = true;

        private PlayerManager m_player;
        private WeaponItem m_chargingWeapon;
        private float m_chargeStartTime;
        private bool m_canComboWithMainHandWeapon;
        private bool m_canPerformOffHandCombo;
        private bool m_canQueueNextAttack;
        private bool m_canPerformCommittedAttack;
        private AttackType m_committedAttackType;
        private SpellItem m_currentCastingSpell;
        private CasterWeaponItem m_currentCasterWeapon;
        private float m_spellChargeStartTime;
        private bool m_isCastingRightHandSpell;
        private bool m_hasReachedFullSpellCharge;
        private RangedWeaponItem m_currentRangedWeapon;
        private RangedProjectileItem m_currentProjectileBeingUsed;
        private ProjectileSlot m_currentProjectileSlot;
        private QuickSlotItem m_currentQuickSlotItem;
        private bool m_isUsingItem;
        private Coroutine m_restoreDeadSpotRoutine;
        private PickupRunesInteractable m_activeDeadSpot;

        /// <summary>Gets the weapon currently selected by the player's action hand.</summary>
        public WeaponItem CurrentWeaponBeingUsed => ResolveCurrentWeapon();

        /// <summary>Gets whether the locally owned player is holding a heavy attack.</summary>
        public bool IsChargingAttack => m_chargingWeapon != null;

        /// <summary>Gets whether the current main-hand attack accepts its next combo input.</summary>
        public bool CanComboWithMainHandWeapon => m_canComboWithMainHandWeapon;

        /// <summary>Gets whether the active dual attack accepts another off-hand input.</summary>
        public bool CanPerformOffHandCombo => m_canPerformOffHandCombo;

        /// <summary>Gets whether the current animation accepts a buffered attack input.</summary>
        public bool CanQueueNextAttack => m_canQueueNextAttack;

        /// <summary>Gets whether gameplay currently permits a new block.</summary>
        public bool CanBlock => m_canBlock;

        /// <summary>Gets whether this owner currently holds either spell-casting input.</summary>
        public bool IsChargingSpell => m_currentCastingSpell != null;

        /// <summary>Gets whether replicated input still holds the active spell charge.</summary>
        public bool IsHoldingSpellInput => IsChargingSpell &&
            (m_player?.PlayerNetworkManager?.IsChargingRightSpell.Value == true ||
                m_player?.PlayerNetworkManager?.IsChargingLeftSpell.Value == true);

        /// <summary>Gets whether a replicated drawn arrow currently belongs to this action.</summary>
        public bool HasArrowNotched =>
            m_player?.PlayerNetworkManager?.HasArrowNotched.Value == true;

        /// <summary>Gets the ammunition copy committed when the current shot began.</summary>
        public RangedProjectileItem CurrentProjectileBeingUsed =>
            m_currentProjectileBeingUsed;

        /// <summary>Gets the ammunition slot committed when the current shot began.</summary>
        public ProjectileSlot CurrentProjectileSlot => m_currentProjectileSlot;

        /// <summary>Gets whether an upper-body quick-slot action currently owns the right hand.</summary>
        public bool IsUsingItem => m_isUsingItem;

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (m_restoreDeadSpotRoutine != null)
            {
                StopCoroutine(m_restoreDeadSpotRoutine);
                m_restoreDeadSpotRoutine = null;
            }
        }

        private void Update()
        {
            if (!IsChargingSpell ||
                m_hasReachedFullSpellCharge ||
                m_player == null ||
                !m_player.IsOwner)
            {
                return;
            }

            if (Time.time - m_spellChargeStartTime < m_fullyChargedSpellDuration)
            {
                return;
            }

            m_hasReachedFullSpellCharge = true;
            m_player.PlayerNetworkManager?.SetSpellFullyChargedState(true);
        }

        /// <summary>Creates one Host-owned Rune recovery point at a death position.</summary>
        public bool CreateDeadSpot(
            Vector3 position,
            int runeCount,
            bool removePlayersRunes = true)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager?.IsHost != true ||
                m_player == null ||
                !m_player.IsOwner ||
                runeCount <= 0)
            {
                return false;
            }

            GameObject deadSpotPrefab =
                WorldCharacterEffectsManager.Instance?.DeadSpotVFX;
            if (deadSpotPrefab == null)
            {
                Debug.LogError(
                    "WorldCharacterEffectsManager is missing the Dead Spot prefab.",
                    this);
                return false;
            }

            GameObject deadSpotObject = Instantiate(
                deadSpotPrefab,
                position,
                Quaternion.identity);
            NetworkObject networkObject =
                deadSpotObject.GetComponent<NetworkObject>();
            PickupRunesInteractable deadSpot =
                deadSpotObject.GetComponent<PickupRunesInteractable>();
            if (networkObject == null || deadSpot == null)
            {
                Debug.LogError(
                    "The Dead Spot prefab requires NetworkObject and PickupRunesInteractable.",
                    deadSpotObject);
                Destroy(deadSpotObject);
                return false;
            }

            networkObject.Spawn(true);
            if (!deadSpot.InitializeDeadSpot(runeCount, m_player.OwnerClientId))
            {
                networkObject.Despawn(true);
                return false;
            }

            m_activeDeadSpot = deadSpot;
            if (!removePlayersRunes)
            {
                return true;
            }

            m_player.PlayerStatsManager?.AddRunes(-runeCount);
            WorldSaveGameManager saveGameManager = WorldSaveGameManager.Instance;
            saveGameManager?.RecordDeadSpot(position, runeCount, false);
            if (saveGameManager?.CanSaveGame == true)
            {
                saveGameManager.SaveGame();
            }

            return true;
        }

        /// <summary>Restores a saved Dead Spot after the gameplay Scene becomes active.</summary>
        public void RestoreDeadSpotFromSaveIfNeeded()
        {
            if (m_restoreDeadSpotRoutine == null)
            {
                m_restoreDeadSpotRoutine = StartCoroutine(
                    RestoreDeadSpotAfterSceneChange());
            }
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
        {
            if (activeScene.buildIndex > 0)
            {
                RestoreDeadSpotFromSaveIfNeeded();
            }
        }

        private IEnumerator RestoreDeadSpotAfterSceneChange()
        {
            yield return null;

            m_restoreDeadSpotRoutine = null;
            CharacterSaveData saveData =
                WorldSaveGameManager.Instance?.CurrentCharacterData;
            if (NetworkManager.Singleton?.IsHost != true ||
                m_player == null ||
                !m_player.IsOwner ||
                SceneManager.GetActiveScene().buildIndex <= 0 ||
                m_activeDeadSpot != null ||
                saveData?.HasDeadSpot != true ||
                saveData.DeadSpotRuneCount <= 0)
            {
                yield break;
            }

            Vector3 deadSpotPosition = new Vector3(
                saveData.DeadSpotPositionX,
                saveData.DeadSpotPositionY,
                saveData.DeadSpotPositionZ);
            CreateDeadSpot(
                deadSpotPosition,
                saveData.DeadSpotRuneCount,
                false);
        }

        /// <summary>
        /// Executes the supplied weapon action against the supplied weapon.
        /// </summary>
        public void PerformWeaponBasedAction(WeaponItemBasedAction weaponAction, WeaponItem weapon)
        {
            if (m_isUsingItem || weaponAction == null || weapon == null)
            {
                return;
            }

            weaponAction.AttemptToPerformAction(m_player, weapon);
        }

        /// <summary>Begins a flask action or requests one additional authored sip.</summary>
        public void AttemptToUseFlask(FlaskItem flask)
        {
            if (flask == null || m_player == null || !m_player.IsOwner)
            {
                return;
            }

            if (m_isUsingItem)
            {
                RequestAdditionalFlaskUse(flask);
                return;
            }

            BeginQuickSlotItemUse(flask, true);
        }

        /// <summary>Reconstructs one remote player's quick-slot action from its stable item ID.</summary>
        public void PerformQuickSlotItemActionFromRpc(QuickSlotItem quickSlotItem)
        {
            if (quickSlotItem != null && m_player?.IsOwner != true)
            {
                BeginQuickSlotItemUse(quickSlotItem, false);
            }
        }

        /// <summary>Animation Event: resolves the currently presented item's success frame.</summary>
        public void SuccessfullyUseQuickSlotItem()
        {
            if (!m_isUsingItem || m_currentQuickSlotItem == null)
            {
                return;
            }

            m_currentQuickSlotItem.SuccessfullyUseItem(m_player);
        }

        /// <summary>Consumes the continuation flag when Drink 01 or Drink 02 begins.</summary>
        public void HandleFlaskDrinkStateEntered()
        {
            if (!m_isUsingItem || m_currentQuickSlotItem is not FlaskItem flask)
            {
                return;
            }

            if (m_player?.IsOwner == true)
            {
                m_player.PlayerNetworkManager?.SetChuggingState(false);
            }

            if (GetRemainingFlaskCount(flask) <= 0)
            {
                PresentQuickSlotItemModel(flask.EmptyFlaskItemModel);
                m_player?.CharacterSoundFXManager?.PlayFlaskSound(true);
                m_player?.PlayerAnimatorManager?.PlayQuickSlotItemAnimation(true);
            }
        }

        /// <summary>Refreshes the held presentation after a replicated flask count changes.</summary>
        public void HandleRemainingFlasksChanged(
            bool healthFlask,
            int previousFlaskCount,
            int currentFlaskCount)
        {
            if (!m_isUsingItem ||
                m_currentQuickSlotItem is not FlaskItem flask ||
                flask.RestoresHealth != healthFlask)
            {
                return;
            }

            if (currentFlaskCount < previousFlaskCount &&
                m_player?.IsOwner != true)
            {
                flask.PlaySuccessfulUseFeedback(m_player);
            }

            if (currentFlaskCount <= 0)
            {
                PresentQuickSlotItemModel(flask.EmptyFlaskItemModel);
                m_player?.PlayerNetworkManager?.SetChuggingState(false);
                m_player?.PlayerAnimatorManager?.PlayQuickSlotItemAnimation(true);
            }
        }

        /// <summary>Ends the upper-body action and restores all gameplay permissions.</summary>
        public void ResetQuickSlotItemUse()
        {
            if (!m_isUsingItem)
            {
                return;
            }

            m_isUsingItem = false;
            m_currentQuickSlotItem = null;
            m_player?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            m_player?.LocomotionManager?.SetCanRun(true);
            m_player?.LocomotionManager?.SetCanRoll(true);
            m_player?.EquipmentManager?.SetWeaponsHidden(false);
            m_player?.PlayerAnimatorManager?.SetFlaskChuggingState(false);
            if (m_player?.IsOwner == true)
            {
                m_player.PlayerNetworkManager?.SetChuggingState(false);
                if (m_player.PlayerNetworkManager?.IsSpawned == true)
                {
                    m_player.PlayerNetworkManager.SetWeaponsHiddenServerRpc(false);
                }
            }
        }

        /// <summary>Interrupts a quick-slot action when a full-body action or modal state takes over.</summary>
        public void CancelQuickSlotItemUse()
        {
            if (!m_isUsingItem)
            {
                return;
            }

            m_player?.PlayerAnimatorManager?.PlayEmptyUpperBodyAnimation();
            ResetQuickSlotItemUse();
        }

        /// <summary>Validates, two-hands, and presents one held ammunition slot.</summary>
        public void BeginNotchingProjectile(
            RangedWeaponItem rangedWeapon,
            ProjectileSlot projectileSlot)
        {
            RangedProjectileItem projectile =
                m_player?.InventoryManager?.GetProjectile(projectileSlot);
            float currentStamina = m_player?.CharacterNetworkManager != null
                ? m_player.CharacterNetworkManager.CurrentStamina.Value
                : 0f;
            bool isCompatible = rangedWeapon?.CanFireProjectile(projectile) == true;
            if (!CanNotchProjectile(
                    m_player?.IsOwner == true,
                    m_player?.IsPerformingAction == true,
                    currentStamina,
                    isCompatible,
                    projectile?.CurrentAmmoAmount ?? 0))
            {
                if (m_player?.IsOwner == true &&
                    rangedWeapon != null &&
                    isCompatible &&
                    projectile?.CurrentAmmoAmount <= 0)
                {
                    PlayOutOfAmmoAnimation();
                }

                return;
            }

            bool isRightHandWeapon =
                m_player.InventoryManager.CurrentRightHandWeapon == rangedWeapon ||
                (m_player.InventoryManager.CurrentTwoHandWeapon == rangedWeapon &&
                    m_player.PlayerNetworkManager
                        .IsTwoHandingRightWeapon.Value);
            if (m_player.PlayerNetworkManager?.EnsureTwoHandWeapon(
                    isRightHandWeapon) != true)
            {
                return;
            }

            SetBlocking(false);
            CancelChargingAttack();
            CancelChargingSpell();
            m_currentRangedWeapon = rangedWeapon;
            m_currentProjectileBeingUsed = projectile;
            m_currentProjectileSlot = projectileSlot;
            m_player.PlayerNetworkManager.SetCharacterActionHand(
                isRightHandWeapon);
            m_player.PlayerNetworkManager.SetNotchedProjectileState(
                projectile.ItemID,
                projectileSlot,
                true,
                true);
            m_player.LocomotionManager?.StopSprinting();
            m_player.LocomotionManager?.SetCanRun(false);
            PresentNotchedProjectile(rangedWeapon, projectile);
            m_player.PlayerNetworkManager
                .NotifyServerOfNotchProjectileServerRpc(
                    projectile.ItemID,
                    projectileSlot);
        }

        /// <summary>Reconstructs a remote player's drawn projectile from one notch event.</summary>
        public void PerformNotchingProjectileFromRpc(
            int projectileID,
            ProjectileSlot projectileSlot)
        {
            RangedProjectileItem projectile = WorldItemDatabase.Instance
                ?.GetProjectileByID(projectileID);
            RangedWeaponItem rangedWeapon = ResolveCurrentWeapon() as
                RangedWeaponItem;
            if (projectile == null ||
                rangedWeapon == null ||
                !rangedWeapon.CanFireProjectile(projectile))
            {
                return;
            }

            m_currentRangedWeapon = rangedWeapon;
            m_currentProjectileBeingUsed = projectile;
            m_currentProjectileSlot = projectileSlot;
            PresentNotchedProjectile(rangedWeapon, projectile);
        }

        /// <summary>Records held-input release and lets the Bow Fire event choose launch time.</summary>
        public void ReleaseHeldProjectileInput()
        {
            if (m_player?.IsOwner == true && HasArrowNotched)
            {
                m_player.PlayerNetworkManager?.SetHoldingArrowState(false);
            }
        }

        /// <summary>Animation Event: consumes ammunition and releases the owner projectile.</summary>
        public void ReleaseArrow()
        {
            if (m_player == null ||
                !m_player.IsOwner ||
                !HasArrowNotched ||
                m_player.PlayerNetworkManager?.IsHoldingArrow.Value == true)
            {
                return;
            }

            int projectileID =
                m_player.PlayerNetworkManager.CurrentProjectileID.Value;
            ProjectileSlot projectileSlot =
                m_player.PlayerNetworkManager.CurrentProjectileSlot.Value;
            RangedProjectileItem projectile =
                m_player.InventoryManager?.GetProjectile(projectileSlot);
            RangedWeaponItem rangedWeapon = m_currentRangedWeapon ??
                ResolveCurrentWeapon() as RangedWeaponItem;
            if (projectile == null ||
                projectile.ItemID != projectileID ||
                rangedWeapon == null ||
                !rangedWeapon.CanFireProjectile(projectile) ||
                m_player.PlayerStatsManager?.TryConsumeStamina(
                    rangedWeapon.BaseStaminaCost) != true)
            {
                CancelNotchedProjectile(true);
                return;
            }

            if (!projectile.TryConsumeAmmo())
            {
                CancelNotchedProjectile(true);
                return;
            }

            m_player.InventoryManager?.NotifyProjectileAmountChanged(
                projectileSlot);

            Vector3 releaseDirection = ResolveProjectileReleaseDirection();
            float characterYRotation = m_player.transform.eulerAngles.y;
            SpawnProjectile(projectile, releaseDirection, true);
            m_player.CharacterSoundFXManager?.PlayRangedWeaponSound(
                rangedWeapon,
                true);
            m_player.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            m_player.PlayerNetworkManager.SetNotchedProjectileState(
                projectileID,
                projectileSlot,
                false,
                false);
            m_player.PlayerNetworkManager
                .NotifyServerOfReleaseProjectileServerRpc(
                    projectileID,
                    releaseDirection,
                    characterYRotation);
            ClearLocalProjectileState();
        }

        /// <summary>Reconstructs a remote presentation projectile from a fire snapshot.</summary>
        public void PerformReleaseProjectileFromRpc(
            int projectileID,
            Vector3 aimDirection,
            float characterYRotation)
        {
            RangedProjectileItem projectile = WorldItemDatabase.Instance
                ?.GetProjectileByID(projectileID);
            if (projectile == null)
            {
                return;
            }

            Vector3 releaseDirection = ResolveReplicatedProjectileDirection(
                aimDirection,
                characterYRotation);
            SpawnProjectile(projectile, releaseDirection, false);
            RangedWeaponItem rangedWeapon = ResolveCurrentWeapon() as
                RangedWeaponItem;
            m_player?.CharacterSoundFXManager?.PlayRangedWeaponSound(
                rangedWeapon,
                true);
            m_player?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            ClearLocalProjectileState();
        }

        /// <summary>Cancels a drawn arrow for interruption or a roll without firing it.</summary>
        public void CancelNotchedProjectile(bool resetActionFlags)
        {
            if (m_player == null ||
                (!HasArrowNotched && m_currentProjectileBeingUsed == null))
            {
                return;
            }

            if (m_player.IsOwner && m_player.PlayerNetworkManager != null)
            {
                m_player.PlayerNetworkManager.SetNotchedProjectileState(
                    m_player.PlayerNetworkManager.CurrentProjectileID.Value,
                    m_player.PlayerNetworkManager.CurrentProjectileSlot.Value,
                    false,
                    false);
            }

            m_player.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            m_player.EquipmentManager?.SetRangedWeaponState(false, false);
            m_player.LocomotionManager?.SetCanRun(true);
            ClearLocalProjectileState();
            if (resetActionFlags)
            {
                m_player.ResetActionFlags();
            }
        }

        /// <summary>Pure validation seam for projectile action eligibility.</summary>
        internal static bool CanNotchProjectile(
            bool isOwner,
            bool isPerformingAction,
            float currentStamina,
            bool isCompatible,
            int currentAmmoAmount)
        {
            return isOwner &&
                !isPerformingAction &&
                currentStamina > 0f &&
                isCompatible &&
                currentAmmoAmount > 0;
        }

        /// <summary>Uses the transmitted direction or the exact fire-frame yaw fallback.</summary>
        public static Vector3 ResolveReplicatedProjectileDirection(
            Vector3 aimDirection,
            float characterYRotation)
        {
            return aimDirection.sqrMagnitude > Mathf.Epsilon
                ? aimDirection.normalized
                : Quaternion.Euler(0f, characterYRotation, 0f) * Vector3.forward;
        }

        /// <summary>Begins one owner-authoritative spell charge and replicates its animation.</summary>
        public void BeginChargingSpell(
            SpellItem spell,
            CasterWeaponItem casterWeapon,
            bool isRightHand)
        {
            if (spell == null ||
                casterWeapon == null ||
                IsChargingSpell ||
                !spell.CanICastThisSpell(m_player, casterWeapon))
            {
                return;
            }

            SetBlocking(false);
            CancelChargingAttack();
            m_currentCastingSpell = spell;
            m_currentCasterWeapon = casterWeapon;
            m_isCastingRightHandSpell = isRightHand;
            m_spellChargeStartTime = Time.time;
            m_hasReachedFullSpellCharge = false;
            m_player.PlayerNetworkManager?.SetCharacterActionHand(isRightHand);
            m_player.PlayerNetworkManager?.SetSpellFullyChargedState(false);
            m_player.PlayerNetworkManager?.SetChargingSpellState(isRightHand, true);
            CharacterActionAnimation animation = isRightHand
                ? CharacterActionAnimation.ChargeSpellRight
                : CharacterActionAnimation.ChargeSpellLeft;
            ReplicateSpellAction(animation);
        }

        /// <summary>Releases the active spell only for the hand that began its charge.</summary>
        public void ReleaseChargingSpell(bool isRightHand)
        {
            if (!IsChargingSpell || m_isCastingRightHandSpell != isRightHand)
            {
                return;
            }

            SpellItem spell = m_currentCastingSpell;
            CasterWeaponItem casterWeapon = m_currentCasterWeapon;
            bool isFullyCharged = m_hasReachedFullSpellCharge;
            m_player.PlayerNetworkManager?.SetChargingSpellState(isRightHand, false);
            if (isFullyCharged)
            {
                spell.SuccessfullyCastSpellFullCharge(
                    m_player,
                    casterWeapon,
                    isRightHand);
            }
            else
            {
                spell.SuccessfullyCastSpell(m_player, casterWeapon, isRightHand);
            }
        }

        /// <summary>Aborts a held spell without releasing a projectile.</summary>
        public void CancelChargingSpell()
        {
            if (m_player?.PlayerNetworkManager != null)
            {
                m_player.PlayerNetworkManager.SetChargingSpellState(
                    m_isCastingRightHandSpell,
                    false);
                m_player.PlayerNetworkManager.SetSpellFullyChargedState(false);
            }

            ClearLocalSpellState();
            m_player?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
        }

        /// <summary>Replicates the correct normal or full-charge hand release.</summary>
        public void ReplicateSpellRelease(bool isRightHand, bool isFullyCharged)
        {
            CharacterActionAnimation animation = (isRightHand, isFullyCharged) switch
            {
                (true, true) =>
                    CharacterActionAnimation.ReleaseFullChargeSpellRight,
                (false, true) =>
                    CharacterActionAnimation.ReleaseFullChargeSpellLeft,
                (true, false) => CharacterActionAnimation.ReleaseSpellRight,
                _ => CharacterActionAnimation.ReleaseSpellLeft
            };
            ReplicateSpellAction(animation);
        }

        /// <summary>Animation Event: creates warm-up effects from replicated spell data.</summary>
        public void InstantiateSpellWarmUpEffects()
        {
            bool isRightHand = ResolveCurrentSpellHand();
            CasterWeaponItem casterWeapon = ResolveCasterWeapon(isRightHand);
            m_player?.InventoryManager?.CurrentSpell?.InstantiateSpellWarmUpEffects(
                m_player,
                casterWeapon,
                isRightHand);
        }

        /// <summary>Animation Event: instantiates the released projectile on this peer.</summary>
        public void InstantiateCurrentSpell()
        {
            bool isRightHand = ResolveCurrentSpellHand();
            CasterWeaponItem casterWeapon = ResolveCasterWeapon(isRightHand);
            SpellItem spell = m_player?.InventoryManager?.CurrentSpell;
            if (spell == null || casterWeapon == null)
            {
                return;
            }

            bool isFullyCharged =
                m_player.PlayerNetworkManager?.IsSpellFullyCharged.Value == true;
            spell.InstantiateSpell(
                m_player,
                casterWeapon,
                isRightHand,
                isFullyCharged);
        }

        /// <summary>Ends local spell presentation and clears owner-written charge state.</summary>
        public void CompleteSpellCast()
        {
            m_player?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            if (m_player?.IsOwner == true)
            {
                m_player.PlayerNetworkManager?.SetChargingSpellState(
                    m_isCastingRightHandSpell,
                    false);
                m_player.PlayerNetworkManager?.SetSpellFullyChargedState(false);
            }

            ClearLocalSpellState();
        }

        /// <summary>Returns the loaded catalyst anchor for one casting hand.</summary>
        public Transform GetSpellInstantiationTransform(bool isRightHand)
        {
            WeaponManager weaponManager = isRightHand
                ? m_player?.EquipmentManager?.CurrentRightHandWeaponManager
                : m_player?.EquipmentManager?.CurrentLeftHandWeaponManager;
            return weaponManager?.SpellInstantiationLocation
                ?.InstantiationTransform;
        }

        /// <summary>Executes the Ash of War selected for the current hand state.</summary>
        public void AttemptToPerformAshOfWar()
        {
            if (m_isUsingItem)
            {
                return;
            }

            WeaponItem weapon = SelectWeaponToPerformAshOfWar();
            weapon?.AshOfWarAction?.AttemptToPerformAction(m_player);
        }

        /// <summary>
        /// Selects the left-hand weapon for EP74 while preserving one extension boundary.
        /// </summary>
        public WeaponItem SelectWeaponToPerformAshOfWar()
        {
            return m_player?.InventoryManager?.CurrentLeftHandWeapon;
        }

        /// <summary>Starts or ends owner-authoritative blocking with the off-hand weapon.</summary>
        public bool SetBlocking(bool isBlocking, WeaponItem blockingWeapon = null)
        {
            CharacterNetworkManager networkManager =
                m_player?.CharacterNetworkManager;
            if (m_player == null ||
                !m_player.IsOwner ||
                networkManager == null ||
                !networkManager.IsSpawned)
            {
                return false;
            }

            if (!isBlocking)
            {
                networkManager.SetBlockingState(false);
                return true;
            }

            if (!m_canBlock ||
                m_player.IsDead ||
                m_player.IsPerformingAction ||
                networkManager.IsAttacking.Value ||
                networkManager.IsBlocking.Value ||
                blockingWeapon == null)
            {
                return false;
            }

            networkManager.SetSneakingState(false);
            m_player.PlayerStatsManager?.SetBlockingStats(blockingWeapon);
            bool isRightHandBlock =
                m_player.PlayerNetworkManager?.IsTwoHandingRightWeapon.Value == true;
            m_player.PlayerNetworkManager?.SetCharacterActionHand(isRightHandBlock);
            networkManager.SetBlockingState(true);
            return true;
        }

        /// <summary>Allows authored action windows to enable or disable blocking.</summary>
        public void SetCanBlock(bool canBlock)
        {
            m_canBlock = canBlock;
            if (!m_canBlock)
            {
                SetBlocking(false);
            }
        }

        /// <summary>
        /// Returns whether the equipped hand pair can replace blocking with Power Stance.
        /// </summary>
        public bool CanUsePowerStance()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            return CanUsePowerStance(
                inventory?.CurrentRightHandWeapon,
                inventory?.CurrentLeftHandWeapon,
                m_player?.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true);
        }

        /// <summary>Evaluates Power Stance from weapon class rather than item identity.</summary>
        public static bool CanUsePowerStance(
            WeaponItem mainHandWeapon,
            WeaponItem offHandWeapon,
            bool isTwoHandingWeapon)
        {
            return !isTwoHandingWeapon &&
                mainHandWeapon != null &&
                offHandWeapon != null &&
                mainHandWeapon.WeaponClass == offHandWeapon.WeaponClass;
        }

        /// <summary>
        /// Resolves and performs the off-hand Power Stance attack for the current movement state.
        /// </summary>
        public bool PerformPowerStanceLeftHandAction(WeaponItem offHandWeapon)
        {
            CharacterNetworkManager networkManager =
                m_player?.CharacterNetworkManager;
            if (m_player == null ||
                !m_player.IsOwner ||
                offHandWeapon == null ||
                networkManager == null ||
                networkManager.CurrentStamina.Value <= 0f ||
                !CanUsePowerStance())
            {
                return false;
            }

            if (!m_player.IsGrounded && m_player.IsPerformingAction)
            {
                return false;
            }

            bool canPerformRollAttack = CanPerformRollingAttack();
            bool canPerformBackstepAttack = CanPerformBackstepAttack();
            if (m_player.IsPerformingAction &&
                !canPerformRollAttack &&
                !canPerformBackstepAttack &&
                !m_canPerformOffHandCombo)
            {
                return false;
            }

            AttackType attackType = ResolvePowerStanceAttackType(
                m_player.IsGrounded,
                m_player.IsPerformingAction,
                canPerformRollAttack,
                canPerformBackstepAttack,
                m_player.LocomotionManager?.IsSprinting == true,
                CurrentAttackType);
            DisableCanPerformCommittedAttack();
            DisableCanCombo();
            m_player.PlayerNetworkManager?.SetCharacterActionHand(false);
            ReplicateAttack(attackType, offHandWeapon);
            networkManager.NotifyServerOfAttackActionServerRpc(attackType);
            return true;
        }

        /// <summary>Applies EP125 movement-state priority to a validated dual attack.</summary>
        public static AttackType ResolvePowerStanceAttackType(
            bool isGrounded,
            bool isPerformingAction,
            bool canPerformRollAttack,
            bool canPerformBackstepAttack,
            bool isSprinting,
            AttackType previousAttack)
        {
            if (!isGrounded && !isPerformingAction)
            {
                return AttackType.DualJumpAttack;
            }

            if (canPerformRollAttack)
            {
                return AttackType.DualRollAttack;
            }

            if (canPerformBackstepAttack)
            {
                return AttackType.DualBackstepAttack;
            }

            if (isSprinting && !isPerformingAction)
            {
                return AttackType.DualRunAttack;
            }

            return previousAttack == AttackType.DualAttack01
                ? AttackType.DualAttack02
                : AttackType.DualAttack01;
        }

        /// <summary>Switches between the off-hand blocking and right-hand action sets.</summary>
        public void ApplyBlockingAnimatorController(bool isBlockingController)
        {
            WeaponItem weapon = m_player?.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true
                ? m_player.InventoryManager?.CurrentTwoHandWeapon
                : isBlockingController
                    ? m_player?.InventoryManager?.CurrentLeftHandWeapon
                    : m_player?.InventoryManager?.CurrentRightHandWeapon;
            m_player?.PlayerAnimatorManager?.UpdateAnimatorController(weapon);
        }

        /// <summary>
        /// Begins an owner-controlled heavy attack charge using the equipped right-hand weapon.
        /// </summary>
        public void BeginChargingHeavyAttack()
        {
            if (m_isUsingItem)
            {
                return;
            }

            WeaponItem weapon = ResolveCurrentWeapon();
            WeaponItemBasedAction heavyAction =
                m_player?.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true
                    ? weapon?.TwoHandRightHeavyAction
                    : weapon?.RightHandHeavyAction;
            if (m_player?.IsPerformingAction == true)
            {
                heavyAction?.AttemptToPerformAction(m_player, weapon);
                return;
            }

            if (IsChargingAttack ||
                heavyAction == null ||
                !CanBeginChargingAttack())
            {
                return;
            }

            SetBlocking(false);
            m_chargingWeapon = weapon;
            m_chargeStartTime = Time.time;
            SetCurrentWeaponActionHand();
            m_player.CharacterNetworkManager?.SetChargingAttackState(true);
        }

        /// <summary>
        /// Releases a short hold as a heavy attack and a completed hold as a charged attack.
        /// </summary>
        public void ReleaseChargingHeavyAttack()
        {
            if (!IsChargingAttack)
            {
                return;
            }

            WeaponItem weapon = m_chargingWeapon;
            float chargeDuration = Mathf.Max(0f, Time.time - m_chargeStartTime);
            WeaponItemBasedAction weaponAction =
                ShouldUseChargedAttack(chargeDuration, m_fullyChargedDuration) &&
                weapon.RightHandChargedAction != null
                    ? weapon.RightHandChargedAction
                    : m_player.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true
                        ? weapon.TwoHandRightHeavyAction
                        : weapon.RightHandHeavyAction;
            ClearChargingState();
            m_player.ResetActionFlags();
            PerformWeaponBasedAction(weaponAction, weapon);
        }

        /// <summary>Aborts an active charge without releasing an attack.</summary>
        public void CancelChargingAttack()
        {
            if (!IsChargingAttack)
            {
                return;
            }

            ClearChargingState();
            m_player?.ResetActionFlags();
        }

        /// <summary>
        /// Opens the authored main-hand combo window when the current attack has a follow-up.
        /// Called by an attack animation event.
        /// </summary>
        public void EnableCanCombo()
        {
            bool isOffHandAction =
                m_player?.PlayerNetworkManager?.IsUsingLeftHand.Value == true &&
                m_player.PlayerNetworkManager.IsTwoHandingWeapon.Value == false;
            m_canPerformOffHandCombo =
                m_player != null &&
                m_player.IsOwner &&
                m_player.IsPerformingAction &&
                isOffHandAction &&
                IsDualComboAttack(CurrentAttackType);
            m_canQueueNextAttack =
                m_player != null &&
                m_player.IsOwner &&
                m_player.IsPerformingAction &&
                m_player.PlayerNetworkManager != null &&
                (m_player.PlayerNetworkManager.IsUsingRightHand.Value ||
                    m_player.PlayerNetworkManager.IsTwoHandingWeapon.Value) &&
                HasNextMainHandComboAttack(CurrentAttackType);
            m_canComboWithMainHandWeapon =
                m_canQueueNextAttack;
        }

        /// <summary>Closes the current main-hand combo window.</summary>
        public void DisableCanCombo()
        {
            m_canComboWithMainHandWeapon = false;
            m_canPerformOffHandCombo = false;
            m_canQueueNextAttack = false;
        }

        /// <summary>
        /// Closes the authored queue window and consumes its oldest valid attack intent.
        /// </summary>
        public void CloseAttackInputQueueWindow()
        {
            if (!m_canQueueNextAttack || !TryConsumeQueuedAttackInput())
            {
                DisableCanCombo();
            }
        }

        /// <summary>
        /// Consumes a valid combo window and immediately replicates the next authored attack.
        /// </summary>
        public bool TryPerformMainHandCombo(AttackType requestedOpeningAttack)
        {
            if (!m_canComboWithMainHandWeapon ||
                m_player == null ||
                !m_player.IsOwner ||
                !m_player.IsPerformingAction ||
                m_player.CharacterNetworkManager == null ||
                m_player.CharacterNetworkManager.CurrentStamina.Value <= 0f ||
                !TryGetNextMainHandComboAttack(
                    CurrentAttackType,
                    requestedOpeningAttack,
                    out AttackType nextAttack))
            {
                return false;
            }

            DisableCanCombo();
            SetCurrentWeaponActionHand();
            ReplicateAttack(nextAttack, CurrentWeaponBeingUsed);
            m_player.CharacterNetworkManager.NotifyServerOfAttackActionServerRpc(
                nextAttack);
            return true;
        }

        /// <summary>Executes the running attack before normal light-attack resolution.</summary>
        public bool TryPerformRunningAttack(WeaponItem weapon)
        {
            if (weapon == null ||
                m_player == null ||
                !m_player.IsOwner ||
                m_player.IsPerformingAction ||
                !m_player.IsGrounded ||
                m_player.LocomotionManager == null ||
                !m_player.LocomotionManager.IsSprinting ||
                m_player.CharacterNetworkManager == null ||
                m_player.CharacterNetworkManager.CurrentStamina.Value <= 0f)
            {
                return false;
            }

            m_player.LocomotionManager.StopSprinting();
            PerformMovingAttack(AttackType.RunningAttack01);
            return true;
        }

        /// <summary>Consumes the active roll or backstep recovery window as a moving attack.</summary>
        public bool TryPerformCommittedAttack(WeaponItem weapon)
        {
            if (weapon == null ||
                !m_canPerformCommittedAttack ||
                m_player == null ||
                !m_player.IsOwner ||
                !m_player.IsPerformingAction ||
                !m_player.IsGrounded ||
                m_player.CharacterNetworkManager == null ||
                m_player.CharacterNetworkManager.CurrentStamina.Value <= 0f)
            {
                return false;
            }

            AttackType attackType = m_committedAttackType;
            DisableCanPerformCommittedAttack();
            PerformMovingAttack(attackType);
            return true;
        }

        /// <summary>Opens the authored roll-attack recovery window on the local owner.</summary>
        public void EnableCanPerformRollAttack()
        {
            EnableCommittedAttack(AttackType.RollAttack01);
        }

        /// <summary>Opens the authored backstep-attack recovery window on the local owner.</summary>
        public void EnableCanPerformBackStepAttack()
        {
            EnableCommittedAttack(AttackType.BackStepAttack01);
        }

        /// <summary>Returns whether the active dodge recovery accepts a dual roll attack.</summary>
        public bool CanPerformRollingAttack()
        {
            return m_canPerformCommittedAttack &&
                m_committedAttackType == AttackType.RollAttack01;
        }

        /// <summary>Returns whether the active dodge recovery accepts a dual backstep attack.</summary>
        public bool CanPerformBackstepAttack()
        {
            return m_canPerformCommittedAttack &&
                m_committedAttackType == AttackType.BackStepAttack01;
        }

        /// <summary>Closes any unconsumed committed-action attack window.</summary>
        public void DisableCanPerformCommittedAttack()
        {
            m_canPerformCommittedAttack = false;
            m_committedAttackType = default;
        }

        /// <inheritdoc />
        public override void ResetActionState()
        {
            base.ResetActionState();
            CancelQuickSlotItemUse();
            if (HasArrowNotched)
            {
                CancelNotchedProjectile(false);
            }

            CompleteSpellCast();
            DisableCanCombo();
            DisableCanPerformCommittedAttack();
            PlayerInputManager.Instance?.ClearAttackInputQueue();
        }

        /// <inheritdoc />
        public override void CloseAllDamageColliders()
        {
            PlayerEquipmentManager equipmentManager =
                m_player?.EquipmentManager;
            equipmentManager?.CurrentRightHandWeaponManager
                ?.CloseDamageCollider();
            equipmentManager?.CurrentLeftHandWeaponManager
                ?.CloseDamageCollider();
            equipmentManager?.CurrentTwoHandWeaponManager
                ?.CloseDamageCollider();
        }

        /// <inheritdoc />
        public override bool AttemptRiposte(CharacterManager targetCharacter)
        {
            CharacterNetworkManager targetNetworkManager =
                targetCharacter?.CharacterNetworkManager;
            if (targetCharacter == null ||
                targetNetworkManager == null ||
                !targetNetworkManager.IsRipostable.Value ||
                targetNetworkManager.IsBeingCriticallyDamaged.Value ||
                !TryGetCriticalAttackWeapon(
                    out MeleeWeaponItem riposteWeapon,
                    out WeaponManager weaponManager) ||
                weaponManager.MeleeDamageCollider == null)
            {
                return false;
            }

            SetCriticalAttackActionHand();
            m_player.CharacterNetworkManager?.SetSneakingState(false);
            float damageModifier = riposteWeapon.RiposteAttack01Modifier;
            CharacterNetworkManager attackerNetworkManager =
                m_player.CharacterNetworkManager;
            if (attackerNetworkManager == null ||
                !attackerNetworkManager.IsSpawned ||
                !targetNetworkManager.IsSpawned)
            {
                ProcessRiposteFromServer(
                    targetCharacter,
                    riposteWeapon,
                    CharacterActionAnimation.Riposted,
                    riposteWeapon.PhysicalDamage * damageModifier,
                    riposteWeapon.MagicDamage * damageModifier,
                    riposteWeapon.FireDamage * damageModifier,
                    riposteWeapon.LightningDamage * damageModifier,
                    riposteWeapon.HolyDamage * damageModifier,
                    0f);
                return true;
            }

            attackerNetworkManager.NotifyServerOfRiposteServerRpc(
                targetCharacter.NetworkObjectId,
                m_player.NetworkObjectId,
                riposteWeapon.ItemID,
                CharacterActionAnimation.Riposted,
                riposteWeapon.PhysicalDamage * damageModifier,
                riposteWeapon.MagicDamage * damageModifier,
                riposteWeapon.FireDamage * damageModifier,
                riposteWeapon.LightningDamage * damageModifier,
                riposteWeapon.HolyDamage * damageModifier,
                0f);
            return true;
        }

        /// <inheritdoc />
        public override bool AttemptBackstab(RaycastHit hit)
        {
            CharacterManager targetCharacter =
                hit.collider?.GetComponentInParent<CharacterManager>();
            CharacterNetworkManager targetNetworkManager =
                targetCharacter?.CharacterNetworkManager;
            if (targetCharacter == null ||
                targetCharacter.CharacterCombatManager?.CanBeBackstabbed != true ||
                targetNetworkManager == null ||
                targetNetworkManager.IsBeingCriticallyDamaged.Value ||
                !TryGetCriticalAttackWeapon(
                    out MeleeWeaponItem backstabWeapon,
                    out WeaponManager weaponManager) ||
                weaponManager.MeleeDamageCollider == null)
            {
                return false;
            }

            SetCriticalAttackActionHand();
            m_player.CharacterNetworkManager?.SetSneakingState(false);
            float damageModifier = backstabWeapon.BackstabAttack01Modifier;
            CharacterNetworkManager attackerNetworkManager =
                m_player.CharacterNetworkManager;
            if (attackerNetworkManager == null ||
                !attackerNetworkManager.IsSpawned ||
                !targetNetworkManager.IsSpawned)
            {
                ProcessBackstabFromServer(
                    targetCharacter,
                    backstabWeapon,
                    CharacterActionAnimation.Backstabbed,
                    backstabWeapon.PhysicalDamage * damageModifier,
                    backstabWeapon.MagicDamage * damageModifier,
                    backstabWeapon.FireDamage * damageModifier,
                    backstabWeapon.LightningDamage * damageModifier,
                    backstabWeapon.HolyDamage * damageModifier,
                    0f);
                return true;
            }

            attackerNetworkManager.NotifyTheServerOfBackstabServerRpc(
                targetCharacter.NetworkObjectId,
                m_player.NetworkObjectId,
                backstabWeapon.ItemID,
                CharacterActionAnimation.Backstabbed,
                backstabWeapon.PhysicalDamage * damageModifier,
                backstabWeapon.MagicDamage * damageModifier,
                backstabWeapon.FireDamage * damageModifier,
                backstabWeapon.LightningDamage * damageModifier,
                backstabWeapon.HolyDamage * damageModifier,
                0f);
            return true;
        }

        /// <summary>
        /// Consumes the stamina cost of the current attack on the locally owned player.
        /// Called from an attack animation event.
        /// </summary>
        public void DrainStaminaBasedOnAttack()
        {
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }

            WeaponItem weapon = CurrentWeaponBeingUsed;
            if (weapon == null)
            {
                return;
            }

            float staminaCost = weapon.BaseStaminaCost *
                weapon.GetStaminaCostMultiplier(CurrentAttackType);
            m_player.PlayerStatsManager?.TryConsumeStamina(staminaCost);
        }

        private void BeginQuickSlotItemUse(
            QuickSlotItem quickSlotItem,
            bool notifyNetwork)
        {
            if (quickSlotItem == null || m_player == null || m_isUsingItem)
            {
                return;
            }

            m_isUsingItem = true;
            m_currentQuickSlotItem = quickSlotItem;
            m_player.LocomotionManager?.StopSprinting();
            m_player.LocomotionManager?.SetCanRun(false);
            m_player.LocomotionManager?.SetCanRoll(false);
            m_player.EquipmentManager?.SetWeaponsHidden(true);

            bool isEmpty = quickSlotItem is FlaskItem flask &&
                GetRemainingFlaskCount(flask) <= 0;
            GameObject model = isEmpty && quickSlotItem is FlaskItem emptyFlask
                ? emptyFlask.EmptyFlaskItemModel
                : quickSlotItem.ItemModel;
            PresentQuickSlotItemModel(model);
            m_player.PlayerAnimatorManager?.SetFlaskChuggingState(false);
            m_player.PlayerAnimatorManager?.PlayQuickSlotItemAnimation(isEmpty);
            if (isEmpty)
            {
                m_player.CharacterSoundFXManager?.PlayFlaskSound(true);
            }

            if (!notifyNetwork || !m_player.IsOwner ||
                m_player.PlayerNetworkManager?.IsSpawned != true)
            {
                return;
            }

            m_player.PlayerNetworkManager.SetChuggingState(false);
            m_player.PlayerNetworkManager.SetWeaponsHiddenServerRpc(true);
            m_player.PlayerNetworkManager
                .NotifyServerOfQuickSlotItemActionServerRpc(quickSlotItem.ItemID);
        }

        private void RequestAdditionalFlaskUse(FlaskItem requestedFlask)
        {
            if (m_currentQuickSlotItem != requestedFlask ||
                m_player?.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.IsChugging.Value)
            {
                return;
            }

            if (GetRemainingFlaskCount(requestedFlask) <= 0)
            {
                PresentQuickSlotItemModel(requestedFlask.EmptyFlaskItemModel);
                m_player.CharacterSoundFXManager?.PlayFlaskSound(true);
                m_player.PlayerAnimatorManager?.PlayQuickSlotItemAnimation(true);
                return;
            }

            m_player.PlayerNetworkManager.SetChuggingState(true);
        }

        private int GetRemainingFlaskCount(FlaskItem flask)
        {
            return flask != null && m_player?.PlayerNetworkManager != null
                ? m_player.PlayerNetworkManager.GetRemainingFlaskCount(
                    flask.RestoresHealth)
                : 0;
        }

        private void PresentQuickSlotItemModel(GameObject model)
        {
            m_player?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            Transform itemParent = m_player?.EquipmentManager?.QuickSlotItemParent;
            if (model == null || itemParent == null)
            {
                return;
            }

            GameObject itemModel = Instantiate(model, itemParent);
            itemModel.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            m_player.CharacterEffectsManager?.RegisterCurrentActionEffect(itemModel);
        }

        private WeaponItem ResolveCurrentWeapon()
        {
            if (m_player == null || m_player.InventoryManager == null)
            {
                return null;
            }

            if (m_player.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true)
            {
                return m_player.InventoryManager.CurrentTwoHandWeapon;
            }

            bool isUsingRightHand = m_player.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.IsUsingRightHand.Value;
            return isUsingRightHand
                ? m_player.InventoryManager.CurrentRightHandWeapon
                : m_player.InventoryManager.CurrentLeftHandWeapon;
        }

        private void PresentNotchedProjectile(
            RangedWeaponItem rangedWeapon,
            RangedProjectileItem projectile)
        {
            if (m_player == null || rangedWeapon == null || projectile == null)
            {
                return;
            }

            const bool k_IsPerformingAction = true;
            const bool k_ShouldApplyRootMotion = false;
            const bool k_CanRotate = true;
            const bool k_CanMove = true;
            m_player.PlayerAnimatorManager?.SetRangedWeaponState(
                true,
                true,
                m_player.PlayerNetworkManager?.IsAiming.Value == true);
            m_player.PlayerAnimatorManager?.PlayTargetActionAnimation(
                CharacterActionAnimation.BowDraw,
                k_IsPerformingAction,
                k_ShouldApplyRootMotion,
                k_CanRotate,
                k_CanMove);
            m_player.EquipmentManager?.SetRangedWeaponState(true, true);
            m_player.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            Transform projectilePivot = FindProjectilePivot();
            if (projectile.DrawProjectileModel != null && projectilePivot != null)
            {
                GameObject drawnProjectile = Instantiate(
                    projectile.DrawProjectileModel,
                    projectilePivot);
                drawnProjectile.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                m_player.CharacterEffectsManager?.RegisterCurrentActionEffect(
                    drawnProjectile);
            }

            m_player.CharacterSoundFXManager?.PlayRangedWeaponSound(
                rangedWeapon,
                false);
        }

        private void PlayOutOfAmmoAnimation()
        {
            const bool k_IsPerformingAction = true;
            const bool k_ShouldApplyRootMotion = false;
            const bool k_CanRotate = true;
            const bool k_CanMove = false;
            m_player.PlayerAnimatorManager?.PlayTargetActionAnimation(
                CharacterActionAnimation.BowOutOfAmmo,
                k_IsPerformingAction,
                k_ShouldApplyRootMotion,
                k_CanRotate,
                k_CanMove);
            m_player.CharacterNetworkManager
                ?.NotifyServerOfActionAnimationServerRpc(
                    CharacterActionAnimation.BowOutOfAmmo,
                    k_IsPerformingAction,
                    k_ShouldApplyRootMotion,
                    k_CanRotate,
                    k_CanMove);
        }

        private Vector3 ResolveProjectileReleaseDirection()
        {
            Transform releaseOrigin = m_player?.LockOnTransform;
            if (releaseOrigin == null)
            {
                return Vector3.forward;
            }

            Vector3 aimDirection;
            if (m_player.PlayerNetworkManager?.IsAiming.Value == true &&
                PlayerCamera.Instance != null)
            {
                aimDirection = PlayerCamera.Instance.AimDirection;
            }
            else if (m_player.LockOnManager?.CurrentTarget != null)
            {
                aimDirection =
                    m_player.LockOnManager.CurrentTarget.LockOnTransform.position -
                    releaseOrigin.position;
            }
            else
            {
                aimDirection = m_player.transform.forward;
            }

            Vector3 normalizedDirection = aimDirection.sqrMagnitude > Mathf.Epsilon
                ? aimDirection.normalized
                : m_player.transform.forward;
            Vector3 farPoint = releaseOrigin.position +
                normalizedDirection * 5000f;
            return (farPoint - releaseOrigin.position).normalized;
        }

        private void SpawnProjectile(
            RangedProjectileItem projectile,
            Vector3 releaseDirection,
            bool canApplyDamage)
        {
            if (m_player == null ||
                projectile?.ReleaseProjectileModel == null)
            {
                return;
            }

            Vector3 direction = releaseDirection.sqrMagnitude > Mathf.Epsilon
                ? releaseDirection.normalized
                : m_player.transform.forward;
            Transform releaseOrigin = m_player.LockOnTransform;
            Vector3 farPoint = releaseOrigin.position + direction * 5000f;
            Quaternion rotation = Quaternion.LookRotation(
                (farPoint - releaseOrigin.position).normalized,
                Vector3.up);
            RangedProjectileManager projectileManager = Instantiate(
                projectile.ReleaseProjectileModel,
                releaseOrigin.position,
                rotation);
            projectileManager.Initialize(
                m_player,
                projectile,
                direction,
                canApplyDamage);
        }

        private Transform FindProjectilePivot()
        {
            if (m_player == null)
            {
                return null;
            }

            foreach (Transform candidate in
                     m_player.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "Projectile Pivot")
                {
                    return candidate;
                }
            }

            return m_player.LockOnTransform;
        }

        private void ClearLocalProjectileState()
        {
            m_currentRangedWeapon = null;
            m_currentProjectileBeingUsed = null;
            m_currentProjectileSlot = ProjectileSlot.Main;
            m_player?.LocomotionManager?.SetCanRun(true);
        }

        private bool TryGetCriticalAttackWeapon(
            out MeleeWeaponItem criticalWeapon,
            out WeaponManager weaponManager)
        {
            bool usesLeftWeapon =
                m_player?.PlayerNetworkManager
                    ?.IsTwoHandingLeftWeapon.Value == true;
            criticalWeapon = (usesLeftWeapon
                ? m_player?.InventoryManager?.CurrentLeftHandWeapon
                : m_player?.InventoryManager?.CurrentRightHandWeapon) as
                    MeleeWeaponItem;
            weaponManager = usesLeftWeapon
                ? m_player?.EquipmentManager?.CurrentLeftHandWeaponManager
                : m_player?.EquipmentManager?.CurrentRightHandWeaponManager;
            return criticalWeapon != null && weaponManager != null;
        }

        private void SetCriticalAttackActionHand()
        {
            bool usesLeftWeapon =
                m_player?.PlayerNetworkManager
                    ?.IsTwoHandingLeftWeapon.Value == true;
            m_player?.PlayerNetworkManager?.SetCharacterActionHand(
                !usesLeftWeapon);
        }

        private bool CanBeginChargingAttack()
        {
            return m_player != null &&
                m_player.IsOwner &&
                !m_player.IsPerformingAction &&
                m_player.IsGrounded &&
                m_player.CharacterNetworkManager != null &&
                m_player.CharacterNetworkManager.CurrentStamina.Value > 0f;
        }

        private void ClearChargingState()
        {
            m_chargingWeapon = null;
            m_chargeStartTime = 0f;
            m_player?.CharacterNetworkManager?.SetChargingAttackState(false);
        }

        private void ReplicateSpellAction(CharacterActionAnimation animation)
        {
            const bool k_IsPerformingAction = true;
            const bool k_ShouldApplyRootMotion = false;
            const bool k_CanRotate = true;
            const bool k_CanMove = false;
            m_player?.PlayerAnimatorManager?.PlayTargetActionAnimation(
                animation,
                k_IsPerformingAction,
                k_ShouldApplyRootMotion,
                k_CanRotate,
                k_CanMove);
            m_player?.CharacterNetworkManager
                ?.NotifyServerOfActionAnimationServerRpc(
                    animation,
                    k_IsPerformingAction,
                    k_ShouldApplyRootMotion,
                    k_CanRotate,
                    k_CanMove);
        }

        private bool ResolveCurrentSpellHand()
        {
            if (m_currentCastingSpell != null)
            {
                return m_isCastingRightHandSpell;
            }

            return m_player?.PlayerNetworkManager?.IsUsingLeftHand.Value != true;
        }

        private CasterWeaponItem ResolveCasterWeapon(bool isRightHand)
        {
            return (isRightHand
                ? m_player?.InventoryManager?.CurrentRightHandWeapon
                : m_player?.InventoryManager?.CurrentLeftHandWeapon) as
                    CasterWeaponItem;
        }

        private void ClearLocalSpellState()
        {
            m_currentCastingSpell = null;
            m_currentCasterWeapon = null;
            m_spellChargeStartTime = 0f;
            m_isCastingRightHandSpell = false;
            m_hasReachedFullSpellCharge = false;
        }

        private void EnableCommittedAttack(AttackType attackType)
        {
            if (m_player == null || !m_player.IsOwner || !m_player.IsPerformingAction)
            {
                return;
            }

            m_committedAttackType = attackType;
            m_canPerformCommittedAttack = true;
        }

        private void PerformMovingAttack(AttackType attackType)
        {
            DisableCanCombo();
            SetCurrentWeaponActionHand();
            ReplicateAttack(attackType, CurrentWeaponBeingUsed);
            m_player.CharacterNetworkManager?.NotifyServerOfAttackActionServerRpc(
                attackType);
        }

        private bool TryConsumeQueuedAttackInput()
        {
            PlayerInputManager inputManager = PlayerInputManager.Instance;
            if (inputManager == null ||
                !inputManager.TryDequeueAttackInput(out AttackInput attackInput))
            {
                return false;
            }

            AttackType requestedOpeningAttack =
                attackInput.InputType == AttackInputType.Heavy
                    ? AttackType.HeavyAttack01
                    : AttackType.LightAttack01;
            if (TryPerformMainHandCombo(requestedOpeningAttack))
            {
                return true;
            }

            if (m_player == null ||
                !m_player.IsOwner ||
                !m_player.IsPerformingAction ||
                m_player.CharacterNetworkManager == null ||
                m_player.CharacterNetworkManager.CurrentStamina.Value <= 0f)
            {
                return false;
            }

            DisableCanCombo();
            SetCurrentWeaponActionHand();
            ReplicateAttack(requestedOpeningAttack, CurrentWeaponBeingUsed);
            m_player.CharacterNetworkManager.NotifyServerOfAttackActionServerRpc(
                requestedOpeningAttack);
            return true;
        }

        private static bool ShouldUseChargedAttack(
            float chargeDuration,
            float fullyChargedDuration)
        {
            return chargeDuration >= Mathf.Max(0f, fullyChargedDuration);
        }

        private void SetCurrentWeaponActionHand()
        {
            bool isRightHandAction =
                m_player?.PlayerNetworkManager?.IsTwoHandingLeftWeapon.Value != true;
            m_player?.PlayerNetworkManager?.SetCharacterActionHand(isRightHandAction);
        }

        private static bool HasNextMainHandComboAttack(AttackType currentAttack)
        {
            return currentAttack == AttackType.LightAttack01 ||
                currentAttack == AttackType.LightAttack02 ||
                currentAttack == AttackType.HeavyAttack01 ||
                currentAttack == AttackType.ChargedAttack01;
        }

        private static bool IsDualComboAttack(AttackType currentAttack)
        {
            return currentAttack == AttackType.DualAttack01 ||
                currentAttack == AttackType.DualAttack02;
        }

        private static bool TryGetNextMainHandComboAttack(
            AttackType currentAttack,
            AttackType requestedOpeningAttack,
            out AttackType nextAttack)
        {
            nextAttack = default;
            if (requestedOpeningAttack == AttackType.LightAttack01)
            {
                if (currentAttack == AttackType.LightAttack01)
                {
                    nextAttack = AttackType.LightAttack02;
                    return true;
                }

                if (currentAttack == AttackType.LightAttack02)
                {
                    nextAttack = AttackType.LightAttack03;
                    return true;
                }

                return false;
            }

            if (requestedOpeningAttack == AttackType.HeavyAttack01 &&
                (currentAttack == AttackType.HeavyAttack01 ||
                    currentAttack == AttackType.ChargedAttack01))
            {
                nextAttack = AttackType.HeavyAttack02;
                return true;
            }

            return false;
        }
    }
}
