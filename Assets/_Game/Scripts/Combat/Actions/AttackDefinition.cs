using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// The single source of truth for one authored attack. <see cref="AttackType"/> survives only as
    /// the legacy animation and network adapter; gameplay decisions read this asset directly.
    /// </summary>
    [CreateAssetMenu(menuName = "ZZ/Combat/Attack Definition")]
    public sealed class AttackDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private int m_defenseRulesVersion;

        [Header("Identity")]
        [Tooltip("Persistent, globally unique id. Assigned once by the Combat Id migration tool and never changed afterwards.")]
        [SerializeField, HideInInspector] private uint m_attackId;
        [Tooltip("Legacy animation / network key. Only used where an animation state is still keyed by it.")]
        [SerializeField] private AttackType m_attackType;
        [Tooltip("Command that opens with this attack when a moveset is entered from neutral.")]
        [SerializeField] private CombatCommand m_entryCommand = CombatCommand.LightAttack;

        [Header("Cost")]
        [SerializeField, Min(0f)] private float m_staminaCost;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float m_damageMultiplier = 1f;
        [SerializeField, Min(0f)] private float m_poiseDamage;
        [SerializeField, Min(0f)] private float m_stanceDamage;

        [Header("AI Base Damage")]
        [SerializeField, Min(0f)] private float m_physicalDamage;
        [SerializeField, Min(0f)] private float m_magicDamage;
        [SerializeField, Min(0f)] private float m_fireDamage;
        [SerializeField, Min(0f)] private float m_lightningDamage;
        [SerializeField, Min(0f)] private float m_holyDamage;

        [Header("Defense Rules")]
        [Tooltip("0 is unparryable; larger levels require stronger parries.")]
        [SerializeField, Min(0)] private int m_parryLevel = 1;
        [SerializeField] private bool m_isBlockable = true;
        [SerializeField, Min(0f)] private float m_hyperArmorPoise;
        [Tooltip("Normalized window during which hyper armor is active. Independent of the hit window.")]
        [SerializeField] private ActionWindow m_hyperArmorWindow;

        [Header("Phase Windows (normalized)")]
        [Tooltip("Authored active span. Drives ActionPhase independently of every combat window.")]
        [SerializeField] private ActionWindow m_activePhase = new ActionWindow(0.2f, 0.6f);
        [SerializeField] private ActionWindow m_recoveryPhase = new ActionWindow(0.6f, 1f);

        [Header("Profiles")]
        [SerializeField] private TrackingProfile m_tracking;
        [SerializeField] private MotionProfile m_motion;
        [SerializeField] private ReactionProfile m_reaction;

        [Header("Presentation")]
        [Tooltip("Optional full Animator state path. Empty preserves the weapon's existing hand-specific mapping.")]
        [SerializeField] private string m_animationState;

        [Header("Transitions")]
        [SerializeField] private AttackTransition[] m_transitions = Array.Empty<AttackTransition>();
        [Tooltip("Legacy follow-ups kept for movesets authored before CombatCommand transitions.")]
        [SerializeField] private AttackDefinition m_nextLightAttack;
        [SerializeField] private AttackDefinition m_nextHeavyAttack;

        public float PhysicalDamage => Mathf.Max(0f, m_physicalDamage) * DamageMultiplier;
        public float MagicDamage => Mathf.Max(0f, m_magicDamage) * DamageMultiplier;
        public float FireDamage => Mathf.Max(0f, m_fireDamage) * DamageMultiplier;
        public float LightningDamage => Mathf.Max(0f, m_lightningDamage) * DamageMultiplier;
        public float HolyDamage => Mathf.Max(0f, m_holyDamage) * DamageMultiplier;

        public float GetChannelBaseDamage(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Physical => PhysicalDamage,
                DamageType.Magic => MagicDamage,
                DamageType.Fire => FireDamage,
                DamageType.Lightning => LightningDamage,
                DamageType.Holy => HolyDamage,
                _ => 0f
            };
        }

        /// <summary>
        /// Whether this attack authors its own channel damage instead of deriving it from a weapon.
        /// </summary>
        public bool HasAuthoredChannelDamage =>
            Mathf.Max(0f, m_physicalDamage) > 0f ||
            Mathf.Max(0f, m_magicDamage) > 0f ||
            Mathf.Max(0f, m_fireDamage) > 0f ||
            Mathf.Max(0f, m_lightningDamage) > 0f ||
            Mathf.Max(0f, m_holyDamage) > 0f;

        public float HyperArmorPoise => Mathf.Max(0f, m_hyperArmorPoise);
        public int ParryLevel => Mathf.Max(0, m_parryLevel);
        public bool IsBlockable => m_isBlockable;
        public ActionWindow HyperArmorWindow => m_hyperArmorWindow;
        public ActionWindow ActivePhase => m_activePhase;
        public ActionWindow RecoveryPhase => m_recoveryPhase;

        /// <summary>Whether the authored phase windows can drive <see cref="CombatActionPhase"/>.</summary>
        public bool HasAuthoredPhases => m_activePhase.IsOpen;

        public TrackingProfile Tracking => m_tracking;
        public MotionProfile Motion => m_motion;
        public ReactionProfile Reaction => m_reaction;

        /// <summary>
        /// The persistent id used by networking, hit identity and the debugger. It is authored once
        /// and never derived from the asset name, so renaming or moving an asset cannot change it.
        /// </summary>
        public uint AttackId => m_attackId;

        /// <summary>Whether a persistent id has been assigned. Networked attacks require this.</summary>
        public bool HasStableAttackId => m_attackId != 0u;

        public CombatCommand EntryCommand => m_entryCommand;
        public AttackType AttackType => m_attackType;
        public float StaminaCost => Mathf.Max(0f, m_staminaCost);
        public float DamageMultiplier => Mathf.Max(0f, m_damageMultiplier);
        public float PoiseDamage => Mathf.Max(0f, m_poiseDamage);
        public float StanceDamage => Mathf.Max(0f, m_stanceDamage);
        public string AnimationState => m_animationState;

        public bool HasFollowUp => HasFollowUpFor(CombatCommand.LightAttack) ||
            HasFollowUpFor(CombatCommand.HeavyAttack);

        /// <summary>Resolves a command edge without mutating the shared attack asset.</summary>
        public AttackDefinition GetFollowUp(CombatCommand command)
        {
            if (m_transitions != null)
            {
                foreach (AttackTransition transition in m_transitions)
                {
                    if (transition.IsValid && transition.Command == command)
                    {
                        return transition.NextAttack;
                    }
                }
            }

            return command switch
            {
                CombatCommand.LightAttack => m_nextLightAttack,
                CombatCommand.HeavyAttack => m_nextHeavyAttack,
                _ => null
            };
        }

        public bool HasFollowUpFor(CombatCommand command)
        {
            return GetFollowUp(command) != null;
        }

        /// <summary>Legacy adapter: maps the old attack enum onto the command-driven transition table.</summary>
        public AttackDefinition GetFollowUp(AttackType input)
        {
            return GetFollowUp(ToCommand(input));
        }

        /// <summary>Legacy adapter: the command this attack's animation key was authored against.</summary>
        public static CombatCommand ToCommand(AttackType attackType)
        {
            return attackType switch
            {
                AttackType.HeavyAttack01 => CombatCommand.HeavyAttack,
                AttackType.HeavyAttack02 => CombatCommand.HeavyAttack,
                AttackType.ChargedAttack01 => CombatCommand.HeavyAttack,
                AttackType.HeavyJumpingAttack01 => CombatCommand.HeavyAttack,
                AttackType.RollAttack01 => CombatCommand.LightAttack,
                AttackType.BackStepAttack01 => CombatCommand.LightAttack,
                AttackType.RunningAttack01 => CombatCommand.LightAttack,
                AttackType.LightJumpingAttack01 => CombatCommand.LightAttack,
                _ => CombatCommand.LightAttack
            };
        }
    }
}
