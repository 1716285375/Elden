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

        [Header("Casting Costs")]
        [SerializeField, Min(0f)] private float m_staminaCost = 25f;
        [SerializeField, Min(0)] private int m_focusPointsCost = 25;

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

        /// <summary>Gets the base Stamina consumed by a successful release.</summary>
        public float StaminaCost => Mathf.Max(0f, m_staminaCost);

        /// <summary>Gets the base Focus Points consumed by a successful release.</summary>
        public int FocusPointsCost => Mathf.Max(0, m_focusPointsCost);

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
            CharacterNetworkManager networkManager =
                player?.CharacterNetworkManager;
            return player != null &&
                player.IsOwner &&
                !player.IsDead &&
                !player.IsPerformingAction &&
                player.IsGrounded &&
                networkManager != null &&
                networkManager.CurrentStamina.Value > 0f &&
                networkManager.CurrentFocusPoints.Value >= FocusPointsCost &&
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
            ConsumeCastingResources(player, false);
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
            ConsumeCastingResources(player, true);
            player?.PlayerCombatManager?.ReplicateSpellRelease(
                isRightHand,
                true);
        }

        /// <summary>Returns the rounded Focus Point cost for one release type.</summary>
        public int GetFocusPointsCost(bool isFullyCharged)
        {
            float modifier = isFullyCharged ? FullChargeModifier : 1f;
            return Mathf.RoundToInt(FocusPointsCost * modifier);
        }

        /// <summary>Returns the Stamina cost for one release type.</summary>
        public float GetStaminaCost(bool isFullyCharged)
        {
            return StaminaCost * (isFullyCharged ? FullChargeModifier : 1f);
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

        private void ConsumeCastingResources(
            PlayerManager player,
            bool isFullyCharged)
        {
            if (player?.IsOwner != true || player.PlayerStatsManager == null)
            {
                return;
            }

            float staminaCost = GetStaminaCost(isFullyCharged);
            if (staminaCost > 0f)
            {
                player.PlayerStatsManager.TryConsumeStamina(staminaCost);
            }

            int focusPointsCost = GetFocusPointsCost(isFullyCharged);
            if (focusPointsCost > 0)
            {
                player.PlayerStatsManager.TryConsumeFocusPoints(focusPointsCost);
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
