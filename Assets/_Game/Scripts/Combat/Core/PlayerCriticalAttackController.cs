using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns the player side of Riposte and Backstab: weapon selection, network delivery and the
    /// offline server fallback. Split out of the combat facade so it only holds critical concerns.
    /// </summary>
    public sealed class PlayerCriticalAttackController
    {
        private readonly PlayerManager m_player;

        public PlayerCriticalAttackController(PlayerManager player)
        {
            m_player = player;
        }

        /// <summary>Performs a Riposte on a ripostable target using the authored weapon modifier.</summary>
        public bool AttemptRiposte(CharacterManager targetCharacter)
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
                m_player.CharacterCombatManager.ProcessRiposteFromServer(
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

        /// <summary>Performs a Backstab on a target struck from behind.</summary>
        public bool AttemptBackstab(RaycastHit hit)
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
                m_player.CharacterCombatManager.ProcessBackstabFromServer(
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
    }
}
