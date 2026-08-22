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

        [Header("Attribute Requirements")]
        [SerializeField, Min(0)] private int m_strengthRequirement;
        [SerializeField, Min(0)] private int m_dexterityRequirement;
        [SerializeField, Min(0)] private int m_intelligenceRequirement;
        [SerializeField, Min(0)] private int m_faithRequirement;

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
        [SerializeField] private OffHandMeleeAction m_leftHandAction;

        [Header("Attack Modifiers")]
        [SerializeField, Min(0f)] private float m_lightAttack01DamageModifier = 1f;
        [SerializeField, Min(0f)] private float m_heavyAttack01DamageModifier = 1f;
        [SerializeField, Min(0f)] private float m_chargedAttack01DamageModifier = 1.75f;
        [SerializeField, Min(0f)] private float m_runningAttack01DamageModifier = 1.2f;
        [SerializeField, Min(0f)] private float m_rollAttack01DamageModifier = 1.1f;
        [SerializeField, Min(0f)] private float m_backStepAttack01DamageModifier = 1.15f;
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

        /// <summary>Gets the reserved strength requirement.</summary>
        public int StrengthRequirement => m_strengthRequirement;

        /// <summary>Gets the reserved dexterity requirement.</summary>
        public int DexterityRequirement => m_dexterityRequirement;

        /// <summary>Gets the reserved intelligence requirement.</summary>
        public int IntelligenceRequirement => m_intelligenceRequirement;

        /// <summary>Gets the reserved faith requirement.</summary>
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
        public OffHandMeleeAction LeftHandAction => m_leftHandAction;

        /// <summary>
        /// Returns the damage multiplier applied to the supplied attack type.
        /// </summary>
        public float GetAttackDamageModifier(AttackType attackType)
        {
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
                default:
                    return m_lightAttack01StaminaCostMultiplier;
            }
        }
    }
}
