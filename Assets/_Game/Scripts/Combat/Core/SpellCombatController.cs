using UnityEngine;

namespace ZZ
{
    /// <summary>Owns SpellCombatController runtime state independently of the combat facade.</summary>
    public sealed class SpellCombatController
    {
        private readonly PlayerManager m_player;
        private SpellItem m_currentCastingSpell;
        private CasterWeaponItem m_currentCasterWeapon;
        private float m_spellChargeStartTime;
        private bool m_isCastingRightHandSpell;
        private bool m_hasReachedFullSpellCharge;
        private bool m_hasReleasedSpell;
        private bool m_hasInstantiatedSpell;

        public SpellCombatController(PlayerManager player)
        {
            m_player = player;
        }

        public bool IsChargingSpell => m_currentCastingSpell != null && !m_hasReleasedSpell;
        public bool IsHoldingSpellInput => IsChargingSpell &&
            (m_player?.PlayerNetworkManager?.IsChargingRightSpell.Value == true ||
                m_player?.PlayerNetworkManager?.IsChargingLeftSpell.Value == true);

        public void Tick(float fullChargeDuration)
        {
            if (IsChargingSpell && !m_hasReachedFullSpellCharge && m_player != null && m_player.IsOwner &&
                Time.time - m_spellChargeStartTime >= fullChargeDuration)
            {
                m_hasReachedFullSpellCharge = true;
                m_player.PlayerNetworkManager?.SetSpellFullyChargedState(true);
            }
        }

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

            m_player.PlayerCombatManager.SetBlocking(false);
            m_player.PlayerCombatManager.CancelChargingAttack();
            m_currentCastingSpell = spell;
            m_currentCasterWeapon = casterWeapon;
            m_isCastingRightHandSpell = isRightHand;
            m_spellChargeStartTime = Time.time;
            m_hasReachedFullSpellCharge = false;
            m_hasReleasedSpell = false;
            m_player.PlayerNetworkManager?.SetCharacterActionHand(isRightHand);
            m_player.PlayerNetworkManager?.SetSpellFullyChargedState(false);
            m_player.PlayerNetworkManager?.SetChargingSpellState(isRightHand, true);
            CharacterActionAnimation animation = isRightHand
                ? CharacterActionAnimation.ChargeSpellRight
                : CharacterActionAnimation.ChargeSpellLeft;
            ReplicateSpellAction(animation);
        }

        public void ReleaseChargingSpell(bool isRightHand)
        {
            if (!IsChargingSpell || m_isCastingRightHandSpell != isRightHand)
            {
                return;
            }

            SpellItem spell = m_currentCastingSpell;
            CasterWeaponItem casterWeapon = m_currentCasterWeapon;
            bool isFullyCharged = m_hasReachedFullSpellCharge;
            if (m_player == null || m_player.IsDead || !spell.CanAffordCast(m_player, isFullyCharged))
            {
                CancelChargingSpell();
                return;
            }

            m_hasReleasedSpell = true;
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

        public void InstantiateSpellWarmUpEffects()
        {
            bool isRightHand = ResolveCurrentSpellHand();
            CasterWeaponItem casterWeapon = ResolveCasterWeapon(isRightHand);
            ResolveCurrentSpell()?.InstantiateSpellWarmUpEffects(
                m_player,
                casterWeapon,
                isRightHand);
        }

        public void InstantiateCurrentSpell()
        {
            // Stale or duplicate animation events must not create another gameplay projectile.
            if (m_player?.IsOwner == true &&
                (m_currentCastingSpell == null || !m_hasReleasedSpell || m_hasInstantiatedSpell))
            {
                return;
            }

            bool isRightHand = ResolveCurrentSpellHand();
            CasterWeaponItem casterWeapon = ResolveCasterWeapon(isRightHand);
            SpellItem spell = ResolveCurrentSpell();
            if (spell == null || casterWeapon == null)
            {
                return;
            }

            bool isFullyCharged =
                m_player.PlayerNetworkManager?.IsSpellFullyCharged.Value == true;
            m_hasInstantiatedSpell = true;
            spell.InstantiateSpell(
                m_player,
                casterWeapon,
                isRightHand,
                isFullyCharged);
        }

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

        public Transform GetSpellInstantiationTransform(bool isRightHand)
        {
            WeaponManager weaponManager = isRightHand
                ? m_player?.EquipmentManager?.CurrentRightHandWeaponManager
                : m_player?.EquipmentManager?.CurrentLeftHandWeaponManager;
            return weaponManager?.SpellInstantiationLocation
                ?.InstantiationTransform;
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
            if (m_currentCastingSpell != null)
            {
                return m_currentCasterWeapon;
            }

            return (isRightHand
                ? m_player?.InventoryManager?.CurrentRightHandWeapon
                : m_player?.InventoryManager?.CurrentLeftHandWeapon) as
                    CasterWeaponItem;
        }

        private SpellItem ResolveCurrentSpell()
        {
            return m_currentCastingSpell != null
                ? m_currentCastingSpell
                : m_player?.InventoryManager?.CurrentSpell;
        }

        private void ClearLocalSpellState()
        {
            m_currentCastingSpell = null;
            m_currentCasterWeapon = null;
            m_spellChargeStartTime = 0f;
            m_isCastingRightHandSpell = false;
            m_hasReachedFullSpellCharge = false;
            m_hasReleasedSpell = false;
            m_hasInstantiatedSpell = false;
        }
    }
}
