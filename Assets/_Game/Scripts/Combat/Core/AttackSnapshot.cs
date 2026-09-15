using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// The immutable numbers one committed attack will use. Captured when the action begins so a
    /// mid-swing weapon swap, buff expiry or stat change cannot alter a swing that already left
    /// the hand.
    /// </summary>
    public readonly struct AttackSnapshot
    {
        public readonly uint AttackId;
        public readonly int WeaponId;
        public readonly float Physical;
        public readonly float Magic;
        public readonly float Fire;
        public readonly float Lightning;
        public readonly float Holy;
        public readonly float PoiseDamage;
        public readonly float StanceDamage;
        public readonly float Poison;
        public readonly float Bleed;
        public readonly float Frost;

        private AttackSnapshot(uint attackId, int weaponId, float physical, float magic, float fire,
            float lightning, float holy, float poiseDamage, float stanceDamage, float poison,
            float bleed, float frost)
        {
            AttackId = attackId;
            WeaponId = weaponId;
            Physical = physical;
            Magic = magic;
            Fire = fire;
            Lightning = lightning;
            Holy = holy;
            PoiseDamage = poiseDamage;
            StanceDamage = stanceDamage;
            Poison = poison;
            Bleed = bleed;
            Frost = frost;
        }

        /// <summary>Whether this snapshot carries at least one non-zero offensive value.</summary>
        public bool IsValid => Physical > 0f || Magic > 0f || Fire > 0f || Lightning > 0f ||
            Holy > 0f || PoiseDamage > 0f || StanceDamage > 0f ||
            Poison > 0f || Bleed > 0f || Frost > 0f;

        /// <summary>
        /// Freezes the numbers for one action. Definition-authored channel damage wins; otherwise
        /// the weapon's attack power at commit time is used.
        /// </summary>
        public static AttackSnapshot Capture(AttackDefinition definition, CharacterManager attacker,
            WeaponItem weapon, AttackType attackType)
        {
            if (definition == null && weapon == null)
            {
                return default;
            }

            float physical = 0f;
            float magic = 0f;
            float fire = 0f;
            float lightning = 0f;
            float holy = 0f;
            if (definition != null && definition.HasAuthoredChannelDamage)
            {
                physical = definition.GetChannelBaseDamage(DamageType.Physical);
                magic = definition.GetChannelBaseDamage(DamageType.Magic);
                fire = definition.GetChannelBaseDamage(DamageType.Fire);
                lightning = definition.GetChannelBaseDamage(DamageType.Lightning);
                holy = definition.GetChannelBaseDamage(DamageType.Holy);
            }
            else if (weapon != null)
            {
                float modifier = definition != null
                    ? definition.DamageMultiplier
                    : weapon.GetAttackDamageModifier(attackType);
                physical = weapon.GetAttackPower(weapon.PhysicalDamage, attacker, true, false) * modifier;
                magic = weapon.GetAttackPower(weapon.MagicDamage, attacker, false, true) * modifier;
                fire = weapon.GetAttackPower(weapon.FireDamage, attacker, false, false) * modifier;
                lightning = weapon.GetAttackPower(weapon.LightningDamage, attacker, false, false) *
                    modifier;
                holy = weapon.GetAttackPower(weapon.HolyDamage, attacker, false, false) * modifier;
            }

            float poiseDamage = definition != null
                ? definition.PoiseDamage
                : weapon != null ? weapon.BasePoiseDamage : 0f;
            float stanceDamage = definition != null
                ? definition.StanceDamage
                : weapon != null ? weapon.BasePoiseDamage : 0f;
            return new AttackSnapshot(
                definition != null ? definition.AttackId : 0u,
                weapon != null ? weapon.ItemID : 0,
                Mathf.Max(0f, physical),
                Mathf.Max(0f, magic),
                Mathf.Max(0f, fire),
                Mathf.Max(0f, lightning),
                Mathf.Max(0f, holy),
                Mathf.Max(0f, poiseDamage),
                Mathf.Max(0f, stanceDamage),
                weapon != null ? weapon.PoisonBuildup : 0f,
                weapon != null ? weapon.BleedBuildup : 0f,
                weapon != null ? weapon.FrostBuildup : 0f);
        }

        /// <summary>Builds the payload for one struck body part without re-reading live state.</summary>
        public DamagePayload CreatePayload(HurtBox hurtBox)
        {
            float damageMultiplier = hurtBox != null ? hurtBox.DamageMultiplier : 1f;
            float poiseMultiplier = hurtBox != null ? hurtBox.PoiseMultiplier : 1f;
            return new DamagePayload(Physical * damageMultiplier, Magic * damageMultiplier,
                Fire * damageMultiplier, Lightning * damageMultiplier, Holy * damageMultiplier,
                PoiseDamage * poiseMultiplier, StanceDamage, Poison, Bleed, Frost);
        }
    }
}
