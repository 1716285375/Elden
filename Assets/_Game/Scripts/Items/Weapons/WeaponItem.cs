using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Defines the authored model, damage, requirements, and costs shared by weapons.
    /// </summary>
    public abstract class WeaponItem : EquipmentItem
    {
        [Header("Weapon Model")]
        [SerializeField] private GameObject m_weaponModel;
        [SerializeField] private bool m_isUnarmed;
        [SerializeField] private WeaponModelType m_weaponModelType;
        [SerializeField] private WeaponClass m_weaponClass;
        [SerializeField] private AnimatorOverrideController m_weaponAnimator;
        [SerializeField] private WeaponMoveset m_moveset;

        [Header("Weapon Pivot")]
        [SerializeField] private Vector3 m_weaponPivotPosition;
        [SerializeField] private Vector3 m_weaponPivotRotation;
        [SerializeField] private Vector3 m_weaponPivotScale = Vector3.one;

        [Header("Base Damage")]
        [SerializeField, Min(0f)] private float m_physicalDamage;
        [SerializeField, Min(0f)] private float m_magicDamage;
        [SerializeField, Min(0f)] private float m_fireDamage;
        [SerializeField, Min(0f)] private float m_lightningDamage;
        [SerializeField, Min(0f)] private float m_holyDamage;

        [Header("Status Buildup")]
        [SerializeField, Min(0f)] private float m_poisonBuildup;
        [SerializeField, Min(0f)] private float m_bleedBuildup;
        [SerializeField, Min(0f)] private float m_frostBuildup;

        [Header("Upgrade")]
        [SerializeField] private UpgradeLevel m_upgradeLevel;

        [Header("Damage Channels")]
        [Tooltip("Optional per-channel overrides. Leave Base Damage at 0 to reuse the values above.")]
        [SerializeField] private DamageChannel m_physicalChannel;
        [SerializeField] private DamageChannel m_magicChannel;
        [SerializeField] private DamageChannel m_fireChannel;
        [SerializeField] private DamageChannel m_lightningChannel;
        [SerializeField] private DamageChannel m_holyChannel;

        [Header("Attribute Requirements")]
        [SerializeField, Min(0)] private int m_strengthRequirement;
        [SerializeField, Min(0)] private int m_dexterityRequirement;
        [SerializeField, Min(0)] private int m_intelligenceRequirement;
        [SerializeField, Min(0)] private int m_faithRequirement;

        [Header("Attribute Scaling")]
        [SerializeField, Range(0f, 2f)] private float m_strengthScaling = 0.35f;
        [SerializeField, Range(0f, 2f)] private float m_dexterityScaling = 0.25f;
        [SerializeField, Range(0f, 2f)] private float m_intelligenceScaling = 0.5f;
        [SerializeField, Range(0f, 2f)] private float m_faithScaling = 0.5f;

        [Header("Action Costs")]
        [SerializeField, Min(0f)] private float m_baseStaminaCost;
        [SerializeField, Min(0f)] private float m_basePoiseDamage;

        [Header("Blocking")]
        [SerializeField, Range(0f, 100f)] private float m_blockingPhysicalAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_blockingMagicAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_blockingFireAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_blockingLightningAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_blockingHolyAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_blockingStability;

        [Header("Guard")]
        [SerializeField] private GuardProfile m_guardProfile;

        [Header("Sound Effects")]
        [SerializeField] private AudioClip[] m_whooshes = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] m_blockingSoundEffects =
            System.Array.Empty<AudioClip>();

        [Header("Weapon Actions")]
        [SerializeField] private WeaponItemBasedAction m_rightHandAction;
        [SerializeField] private WeaponItemBasedAction m_rightHandHeavyAction;
        [SerializeField] private WeaponItemBasedAction m_rightHandChargedAction;
        [SerializeField] private WeaponItemBasedAction m_twoHandRightAction;
        [SerializeField] private WeaponItemBasedAction m_twoHandRightHeavyAction;
        [SerializeField] private WeaponItemBasedAction m_leftHandAction;
        [SerializeField] private AshOfWar m_ashOfWarAction;

        [Header("Attack Modifiers")]
        [SerializeField, Min(0f)] private float m_lightAttack01DamageModifier = 1f;
        [SerializeField, Min(0f)] private float m_heavyAttack01DamageModifier = 1f;
        [SerializeField, Min(0f)] private float m_chargedAttack01DamageModifier = 1.75f;
        [SerializeField, Min(0f)] private float m_runningAttack01DamageModifier = 1.2f;
        [SerializeField, Min(0f)] private float m_rollAttack01DamageModifier = 1.1f;
        [SerializeField, Min(0f)] private float m_backStepAttack01DamageModifier = 1.15f;
        [SerializeField, Min(0f)] private float m_lightJumpingAttack01DamageModifier = 1f;
        [SerializeField, Min(0f)] private float m_heavyJumpingAttack01DamageModifier = 1.8f;
        [SerializeField, Min(0f)] private float m_dualAttack01DamageModifier = 0.77f;
        [SerializeField, Min(0f)] private float m_dualAttack02DamageModifier = 0.77f;
        [SerializeField, Min(0f)] private float m_dualJumpAttackDamageModifier = 0.77f;
        [SerializeField, Min(0f)] private float m_dualRunAttackDamageModifier = 0.77f;
        [SerializeField, Min(0f)] private float m_dualRollAttackDamageModifier = 0.77f;
        [SerializeField, Min(0f)] private float m_dualBackstepAttackDamageModifier = 0.77f;
        [SerializeField, Min(0f)] private float m_lightAttack01StaminaCostMultiplier = 1f;
        [SerializeField, Min(0f)] private float m_heavyAttack01StaminaCostMultiplier = 1f;
        [SerializeField, Min(0f)] private float m_chargedAttack01StaminaCostMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float m_runningAttack01StaminaCostMultiplier = 1.25f;
        [SerializeField, Min(0f)] private float m_rollAttack01StaminaCostMultiplier = 1.15f;
        [SerializeField, Min(0f)] private float m_backStepAttack01StaminaCostMultiplier = 1.2f;

        /// <summary>Gets the prefab instantiated by an equipment slot.</summary>
        public GameObject WeaponModel => m_weaponModel;

        /// <summary>Gets whether this item represents the non-null unarmed fallback.</summary>
        public bool IsUnarmed => m_isUnarmed;

        /// <summary>Gets whether this model uses a hand-weapon or shield attachment.</summary>
        public WeaponModelType WeaponModelType => m_weaponModelType;

        /// <summary>Gets the class used to select a back or hip storage slot.</summary>
        public WeaponClass WeaponClass => m_weaponClass;

        /// <summary>Gets the complete animation override set used by this weapon.</summary>
        public AnimatorOverrideController WeaponAnimator => m_weaponAnimator;

        public WeaponMoveset Moveset => m_moveset;

        /// <summary>Checks effective Strength and all other requirements before paying an action cost.</summary>
        public bool MeetsRequirements(CharacterManager character)
        {
            if (character == null)
            {
                return false;
            }

            CharacterNetworkManager network = character.CharacterNetworkManager;
            if (network == null)
            {
                return false;
            }

            int strength = character.CharacterStatsManager != null
                ? character.CharacterStatsManager.TotalStrength : network.Strength.Value;
            bool twoHanded = character is PlayerManager player &&
                player.PlayerNetworkManager.IsTwoHandingWeapon.Value;
            return WeaponAttackRating.MeetsRequirements(strength, network.Dexterity.Value,
                network.Intelligence.Value, network.Faith.Value, twoHanded,
                StrengthRequirement, DexterityRequirement, IntelligenceRequirement, FaithRequirement);
        }

        /// <summary>Resolves reinforced and attribute-scaled attack power for one authored channel.</summary>
        public float GetAttackPower(float baseDamage, CharacterManager character, bool isPhysical, bool isMagic)
        {
            float reinforced = WeaponManager.GetUpgradedDamage(baseDamage, UpgradeLevel);
            CharacterNetworkManager network = character != null ? character.CharacterNetworkManager : null;
            if (network == null || reinforced <= 0f)
            {
                return reinforced;
            }

            int strength = character.CharacterStatsManager != null
                ? character.CharacterStatsManager.TotalStrength : network.Strength.Value;
            bool twoHanded = character is PlayerManager player &&
                player.PlayerNetworkManager.IsTwoHandingWeapon.Value;
            float scaling = isPhysical
                ? WeaponAttackRating.AttributeBonus(twoHanded ? Mathf.FloorToInt(strength * 1.5f) : strength,
                    m_strengthScaling) + WeaponAttackRating.AttributeBonus(network.Dexterity.Value, m_dexterityScaling)
                : WeaponAttackRating.AttributeBonus(isMagic ? network.Intelligence.Value : network.Faith.Value,
                    isMagic ? m_intelligenceScaling : m_faithScaling);
            return reinforced * (1f + scaling);
        }


        /// <summary>Gets the authored guard arc and stability used while blocking with this item.</summary>
        public GuardProfile GuardProfile => m_guardProfile;

        /// <summary>Gets the authored scaling for one damage channel.</summary>
        public DamageChannel GetDamageChannel(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Physical => m_physicalChannel,
                DamageType.Magic => m_magicChannel,
                DamageType.Fire => m_fireChannel,
                DamageType.Lightning => m_lightningChannel,
                DamageType.Holy => m_holyChannel,
                _ => default
            };
        }

        /// <summary>
        /// Resolves attack power for one channel using that channel's own attribute scaling, so
        /// Fire can scale with Faith while Lightning scales with Dexterity on the same weapon.
        /// </summary>
        public float GetChannelAttackPower(DamageType damageType, CharacterManager character)
        {
            DamageChannel channel = GetDamageChannel(damageType);
            float baseDamage = channel.BaseDamage > 0f ? channel.BaseDamage : GetBaseDamage(damageType);
            float reinforced = WeaponManager.GetUpgradedDamage(baseDamage, UpgradeLevel);
            CharacterNetworkManager network = character != null ? character.CharacterNetworkManager : null;
            if (network == null || reinforced <= 0f)
            {
                return reinforced;
            }

            int strength = character.CharacterStatsManager != null
                ? character.CharacterStatsManager.TotalStrength : network.Strength.Value;
            bool twoHanded = character is PlayerManager player &&
                player.PlayerNetworkManager.IsTwoHandingWeapon.Value;
            return reinforced * (1f + WeaponAttackRating.ResolveChannel(channel,
                twoHanded ? Mathf.FloorToInt(strength * 1.5f) : strength,
                network.Dexterity.Value, network.Intelligence.Value, network.Faith.Value));
        }

        private float GetBaseDamage(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Physical => m_physicalDamage,
                DamageType.Magic => m_magicDamage,
                DamageType.Fire => m_fireDamage,
                DamageType.Lightning => m_lightningDamage,
                DamageType.Holy => m_holyDamage,
                _ => 0f
            };
        }

        public AttackDefinition GetAttackDefinition(AttackType attackType)
        {
            return m_moveset != null ? m_moveset.GetAttack(attackType) : null;
        }

        public float GetAttackStaminaCost(AttackType attackType)
        {
            AttackDefinition attack = GetAttackDefinition(attackType);
            return attack != null ? attack.StaminaCost : BaseStaminaCost * GetStaminaCostMultiplier(attackType);
        }

        /// <summary>Gets the local hand-slot position adjustment for this weapon model.</summary>
        public Vector3 WeaponPivotPosition => m_weaponPivotPosition;

        /// <summary>Gets the local hand-slot Euler rotation adjustment.</summary>
        public Vector3 WeaponPivotRotation => m_weaponPivotRotation;

        /// <summary>Gets the local hand-slot scale adjustment.</summary>
        public Vector3 WeaponPivotScale => m_weaponPivotScale;

        /// <summary>Gets the weapon's base physical damage.</summary>
        public float PhysicalDamage => m_physicalDamage;

        /// <summary>Gets the weapon's base magic damage.</summary>
        public float MagicDamage => m_magicDamage;

        /// <summary>Gets the weapon's base fire damage.</summary>
        public float FireDamage => m_fireDamage;

        /// <summary>Gets the weapon's base lightning damage.</summary>
        public float LightningDamage => m_lightningDamage;

        /// <summary>Gets the weapon's base holy damage.</summary>
        public float HolyDamage => m_holyDamage;

        /// <summary>Gets Poison accumulation applied by an unblocked hit.</summary>
        public float PoisonBuildup => Mathf.Max(0f, m_poisonBuildup);

        /// <summary>Gets Bleed accumulation applied by an unblocked hit.</summary>
        public float BleedBuildup => Mathf.Max(0f, m_bleedBuildup);

        /// <summary>Gets Frost accumulation applied by an unblocked hit.</summary>
        public float FrostBuildup => Mathf.Max(0f, m_frostBuildup);

        /// <summary>Gets this runtime weapon instance's reinforcement level.</summary>
        public UpgradeLevel UpgradeLevel => m_upgradeLevel;

        /// <summary>Gets whether this weapon has reached the supported reinforcement cap.</summary>
        public bool IsFullyUpgraded => m_upgradeLevel >= UpgradeLevel.Level10;

        /// <summary>Gets the minimum effective Strength needed to attack.</summary>
        public int StrengthRequirement => m_strengthRequirement;

        /// <summary>Gets the minimum Dexterity needed to attack.</summary>
        public int DexterityRequirement => m_dexterityRequirement;

        /// <summary>Gets the minimum Intelligence needed to attack.</summary>
        public int IntelligenceRequirement => m_intelligenceRequirement;

        /// <summary>Gets the minimum Faith needed to attack.</summary>
        public int FaithRequirement => m_faithRequirement;

        /// <summary>Gets the base stamina cost reserved for weapon actions.</summary>
        public float BaseStaminaCost => m_baseStaminaCost;

        /// <summary>Gets the poise damage forwarded to the damage effect.</summary>
        public float BasePoiseDamage => m_basePoiseDamage;

        /// <summary>Gets Physical damage absorption supplied while blocking.</summary>
        public float BlockingPhysicalAbsorption => m_blockingPhysicalAbsorption;

        /// <summary>Gets Magic damage absorption supplied while blocking.</summary>
        public float BlockingMagicAbsorption => m_blockingMagicAbsorption;

        /// <summary>Gets Fire damage absorption supplied while blocking.</summary>
        public float BlockingFireAbsorption => m_blockingFireAbsorption;

        /// <summary>Gets Lightning damage absorption supplied while blocking.</summary>
        public float BlockingLightningAbsorption => m_blockingLightningAbsorption;

        /// <summary>Gets Holy damage absorption supplied while blocking.</summary>
        public float BlockingHolyAbsorption => m_blockingHolyAbsorption;

        /// <summary>Gets the percentage of incoming guard stamina damage prevented.</summary>
        public float BlockingStability => m_blockingStability;

        /// <summary>Gets the weapon-specific clips used when a damage window opens.</summary>
        public AudioClip[] Whooshes => m_whooshes;

        /// <summary>Gets impact sounds played while this weapon blocks.</summary>
        public AudioClip[] BlockingSoundEffects => m_blockingSoundEffects;

        /// <summary>Gets the action bound to the one-handed right-bumper slot.</summary>
        public WeaponItemBasedAction RightHandAction => m_rightHandAction;

        /// <summary>Gets the action bound to the one-handed right-trigger slot.</summary>
        public WeaponItemBasedAction RightHandHeavyAction => m_rightHandHeavyAction;

        /// <summary>Gets the action released after a fully charged right-trigger hold.</summary>
        public WeaponItemBasedAction RightHandChargedAction => m_rightHandChargedAction;

        /// <summary>Gets the action bound to the two-handed right-bumper slot.</summary>
        public WeaponItemBasedAction TwoHandRightAction => m_twoHandRightAction;

        /// <summary>Gets the action bound to the two-handed right-trigger slot.</summary>
        public WeaponItemBasedAction TwoHandRightHeavyAction => m_twoHandRightHeavyAction;

        /// <summary>Gets the sustained action bound to the left bumper.</summary>
        public WeaponItemBasedAction LeftHandAction => m_leftHandAction;

        /// <summary>Gets the special action equipped on this weapon.</summary>
        public AshOfWar AshOfWarAction => m_ashOfWarAction;

        /// <summary>
        /// Applies per-instance saved Ash of War state to a runtime weapon copy.
        /// </summary>
        public void SetAshOfWar(AshOfWar ashOfWar)
        {
            m_ashOfWarAction = ashOfWar;
        }

        /// <summary>Restores a clamped per-instance reinforcement level.</summary>
        public void SetUpgradeLevel(UpgradeLevel upgradeLevel)
        {
            m_upgradeLevel = (UpgradeLevel)Mathf.Clamp(
                (int)upgradeLevel,
                (int)UpgradeLevel.Level0,
                (int)UpgradeLevel.Level10);
        }

        /// <summary>Advances this runtime weapon by one level when it is eligible.</summary>
        public bool TryUpgrade()
        {
            if (m_isUnarmed || IsFullyUpgraded)
            {
                return false;
            }

            SetUpgradeLevel((UpgradeLevel)((int)m_upgradeLevel + 1));
            return true;
        }

        /// <summary>
        /// Returns the damage multiplier applied to the supplied attack type.
        /// </summary>
        public float GetAttackDamageModifier(AttackType attackType)
        {
            AttackDefinition attack = GetAttackDefinition(attackType);
            if (attack != null)
            {
                return attack.DamageMultiplier;
            }

            switch (attackType)
            {
                case AttackType.HeavyAttack01:
                case AttackType.HeavyAttack02:
                    return m_heavyAttack01DamageModifier;
                case AttackType.ChargedAttack01:
                    return m_chargedAttack01DamageModifier;
                case AttackType.RunningAttack01:
                    return m_runningAttack01DamageModifier;
                case AttackType.RollAttack01:
                    return m_rollAttack01DamageModifier;
                case AttackType.BackStepAttack01:
                    return m_backStepAttack01DamageModifier;
                case AttackType.LightJumpingAttack01:
                    return m_lightJumpingAttack01DamageModifier;
                case AttackType.HeavyJumpingAttack01:
                    return m_heavyJumpingAttack01DamageModifier;
                case AttackType.DualAttack01:
                    return m_dualAttack01DamageModifier;
                case AttackType.DualAttack02:
                    return m_dualAttack02DamageModifier;
                case AttackType.DualJumpAttack:
                    return m_dualJumpAttackDamageModifier;
                case AttackType.DualRunAttack:
                    return m_dualRunAttackDamageModifier;
                case AttackType.DualRollAttack:
                    return m_dualRollAttackDamageModifier;
                case AttackType.DualBackstepAttack:
                    return m_dualBackstepAttackDamageModifier;
                default:
                    return m_lightAttack01DamageModifier;
            }
        }

        /// <summary>
        /// Returns the stamina cost multiplier applied to the supplied attack type.
        /// </summary>
        public float GetStaminaCostMultiplier(AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.HeavyAttack01:
                case AttackType.HeavyAttack02:
                    return m_heavyAttack01StaminaCostMultiplier;
                case AttackType.ChargedAttack01:
                    return m_chargedAttack01StaminaCostMultiplier;
                case AttackType.RunningAttack01:
                    return m_runningAttack01StaminaCostMultiplier;
                case AttackType.RollAttack01:
                    return m_rollAttack01StaminaCostMultiplier;
                case AttackType.BackStepAttack01:
                    return m_backStepAttack01StaminaCostMultiplier;
                case AttackType.HeavyJumpingAttack01:
                    return m_heavyAttack01StaminaCostMultiplier;
                case AttackType.LightJumpingAttack01:
                    return m_lightAttack01StaminaCostMultiplier;
                case AttackType.DualRunAttack:
                    return m_runningAttack01StaminaCostMultiplier;
                case AttackType.DualRollAttack:
                    return m_rollAttack01StaminaCostMultiplier;
                case AttackType.DualBackstepAttack:
                    return m_backStepAttack01StaminaCostMultiplier;
                case AttackType.DualAttack01:
                case AttackType.DualAttack02:
                case AttackType.DualJumpAttack:
                    return m_lightAttack01StaminaCostMultiplier;
                default:
                    return m_lightAttack01StaminaCostMultiplier;
            }
        }
    }
}
