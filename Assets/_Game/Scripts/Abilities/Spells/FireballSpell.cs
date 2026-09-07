using UnityEngine;

namespace ZZ
{
    /// <summary>Creates a homing fire projectile with an authored fire-damage payload.</summary>
    [GameAsset(FileName = "Fireball", MenuName = "ZZ/Items/Spells/Fireball")]
    public class FireballSpell : SpellItem
    {
        [Header("Fireball")]
        [SerializeField] private FireballManager m_fireballPrefab;
        [SerializeField, Min(0f)] private float m_fireDamage = 150f;
        [SerializeField, Min(0f)] private float m_poiseDamage = 10f;

        /// <summary>Gets the projectile presentation instantiated on every peer.</summary>
        public FireballManager FireballPrefab => m_fireballPrefab;

        /// <summary>Gets the base fire damage before the full-charge modifier.</summary>
        public float FireDamage => m_fireDamage;

        /// <inheritdoc />
        public override void InstantiateSpell(
            PlayerManager player,
            CasterWeaponItem casterWeapon,
            bool isRightHand,
            bool isFullyCharged)
        {
            Transform anchor = player?.PlayerCombatManager
                ?.GetSpellInstantiationTransform(isRightHand);
            if (anchor == null || m_fireballPrefab == null)
            {
                return;
            }

            player.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            InstantiateReleaseEffect(player, isRightHand);
            Transform target = player.LockOnManager?.CurrentTarget?.LockOnTransform;
            Vector3 aimDirection = target != null
                ? target.position - anchor.position
                : player.transform.forward;
            Quaternion rotation = aimDirection.sqrMagnitude > Mathf.Epsilon
                ? Quaternion.LookRotation(aimDirection.normalized)
                : anchor.rotation;
            FireballManager fireball = Instantiate(
                m_fireballPrefab,
                anchor.position,
                rotation);
            float damageModifier = isFullyCharged ? FullChargeModifier : 1f;
            fireball.Initialize(
                player,
                target,
                m_fireDamage * damageModifier,
                m_poiseDamage,
                isFullyCharged);
        }
    }
}
