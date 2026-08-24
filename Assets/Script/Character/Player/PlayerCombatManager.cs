using UnityEngine;

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
        private bool m_canQueueNextAttack;
        private bool m_canPerformCommittedAttack;
        private AttackType m_committedAttackType;
        private SpellItem m_currentCastingSpell;
        private CasterWeaponItem m_currentCasterWeapon;
        private float m_spellChargeStartTime;
        private bool m_isCastingRightHandSpell;
        private bool m_hasReachedFullSpellCharge;

        /// <summary>Gets the weapon currently selected by the player's action hand.</summary>
        public WeaponItem CurrentWeaponBeingUsed => ResolveCurrentWeapon();

        /// <summary>Gets whether the locally owned player is holding a heavy attack.</summary>
        public bool IsChargingAttack => m_chargingWeapon != null;

        /// <summary>Gets whether the current main-hand attack accepts its next combo input.</summary>
        public bool CanComboWithMainHandWeapon => m_canComboWithMainHandWeapon;

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

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
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

        /// <summary>
        /// Executes the supplied weapon action against the supplied weapon.
        /// </summary>
        public void PerformWeaponBasedAction(WeaponItemBasedAction weaponAction, WeaponItem weapon)
        {
            if (weaponAction == null || weapon == null)
            {
                return;
            }

            weaponAction.AttemptToPerformAction(m_player, weapon);
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
