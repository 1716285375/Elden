using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Defines the shared validation, presentation, and release lifecycle for a spell item.
    /// </summary>
    public abstract class SpellItem : Item
    {
        [Header("Spell Classification")]
        [SerializeField] private SpellClass m_spellClass = SpellClass.Incantation;
        [SerializeField, Min(1)] private int m_spellSlotsUsed = 1;

        [Header("Charge")]
        [SerializeField, Min(1f)] private float m_fullChargeModifier = 1.4f;

        [Header("Action Effects")]
        [SerializeField] private GameObject m_spellWarmUpEffect;
        [SerializeField] private GameObject m_spellReleaseEffect;
        [SerializeField] private GameObject m_spellFullyChargedEffect;

        /// <summary>Gets the catalyst class required to cast this spell.</summary>
        public SpellClass SpellClass => m_spellClass;

        /// <summary>Gets the future attunement-slot cost reserved by this spell.</summary>
        public int SpellSlotsUsed => m_spellSlotsUsed;

        /// <summary>Gets the damage multiplier applied after a complete charge.</summary>
        public float FullChargeModifier => m_fullChargeModifier;

        /// <summary>Gets the locally instantiated warm-up presentation prefab.</summary>
        public GameObject SpellWarmUpEffect => m_spellWarmUpEffect;

        /// <summary>Gets the locally instantiated release presentation prefab.</summary>
        public GameObject SpellReleaseEffect => m_spellReleaseEffect;

        /// <summary>Gets the locally instantiated full-charge presentation prefab.</summary>
        public GameObject SpellFullyChargedEffect => m_spellFullyChargedEffect;

        /// <summary>Validates the owner, action state, spell, and equipped catalyst.</summary>
        public virtual bool CanICastThisSpell(
            PlayerManager player,
            CasterWeaponItem casterWeapon)
        {
            return player != null &&
                player.IsOwner &&
                !player.IsDead &&
                !player.IsPerformingAction &&
                player.IsGrounded &&
                player.InventoryManager?.CurrentSpell == this &&
                casterWeapon != null &&
                casterWeapon.SpellClass == m_spellClass;
        }

        /// <summary>Requests the owner-side combat manager to begin charging this spell.</summary>
        public virtual void AttemptToCastSpell(
            PlayerManager player,
            CasterWeaponItem casterWeapon,
            bool isRightHand)
        {
            if (!CanICastThisSpell(player, casterWeapon))
            {
                return;
            }

            player.PlayerCombatManager?.BeginChargingSpell(
                this,
                casterWeapon,
                isRightHand);
        }

        /// <summary>Starts the normal release animation for this spell.</summary>
        public virtual void SuccessfullyCastSpell(
            PlayerManager player,
            CasterWeaponItem casterWeapon,
            bool isRightHand)
        {
            player?.PlayerCombatManager?.ReplicateSpellRelease(
                isRightHand,
                false);
        }

        /// <summary>Starts the fully charged release animation for this spell.</summary>
        public virtual void SuccessfullyCastSpellFullCharge(
            PlayerManager player,
            CasterWeaponItem casterWeapon,
            bool isRightHand)
        {
            player?.PlayerCombatManager?.ReplicateSpellRelease(
                isRightHand,
                true);
        }

        /// <summary>Creates the one-shot full-charge presentation on this peer.</summary>
        public virtual void SuccessfullyChargeSpell(
            PlayerManager player,
            CasterWeaponItem casterWeapon,
            bool isRightHand)
        {
            InstantiateActionEffect(
                player,
                m_spellFullyChargedEffect,
                isRightHand);
        }

        /// <summary>Creates the charging presentation on this peer's catalyst anchor.</summary>
        public virtual void InstantiateSpellWarmUpEffects(
            PlayerManager player,
            CasterWeaponItem casterWeapon,
            bool isRightHand)
        {
            InstantiateActionEffect(player, m_spellWarmUpEffect, isRightHand);
        }

        /// <summary>
        /// Instantiates the released gameplay spell. Derived spell types supply its behavior.
        /// </summary>
        public abstract void InstantiateSpell(
            PlayerManager player,
            CasterWeaponItem casterWeapon,
            bool isRightHand,
            bool isFullyCharged);

        /// <summary>Creates an untracked one-shot release effect at the catalyst anchor.</summary>
        protected void InstantiateReleaseEffect(PlayerManager player, bool isRightHand)
        {
            Transform anchor = player?.PlayerCombatManager
                ?.GetSpellInstantiationTransform(isRightHand);
            if (anchor != null && m_spellReleaseEffect != null)
            {
                Instantiate(
                    m_spellReleaseEffect,
                    anchor.position,
                    anchor.rotation);
            }
        }

        private static void InstantiateActionEffect(
            PlayerManager player,
            GameObject effectPrefab,
            bool isRightHand)
        {
            Transform anchor = player?.PlayerCombatManager
                ?.GetSpellInstantiationTransform(isRightHand);
            if (anchor == null || effectPrefab == null)
            {
                return;
            }

            GameObject effect = Instantiate(effectPrefab, anchor);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.identity;
            player.CharacterEffectsManager?.RegisterCurrentActionEffect(effect);
        }
    }
}
