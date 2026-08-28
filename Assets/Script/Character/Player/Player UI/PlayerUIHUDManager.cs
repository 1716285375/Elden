using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    public class PlayerUIHUDManager : MonoBehaviour
    {
        private const float k_RuneMergeDelay = 2.5f;

        [SerializeField] private UIStatBar m_healthBar;
        [SerializeField] private UIStatBar m_staminaBar;
        [SerializeField] private UIStatBar m_focusPointsBar;
        [SerializeField] private UIBuildupBar[] m_buildupBars =
            System.Array.Empty<UIBuildupBar>();
        [SerializeField] private UIQuickSlot m_leftWeaponQuickSlot;
        [SerializeField] private UIQuickSlot m_rightWeaponQuickSlot;
        [SerializeField] private UIQuickSlot m_spellQuickSlot;
        [SerializeField] private UIQuickSlot m_itemQuickSlot;
        [SerializeField] private UIQuickSlot m_mainProjectileQuickSlot;
        [SerializeField] private UIQuickSlot m_secondaryProjectileQuickSlot;
        [SerializeField] private GameObject m_projectileQuickSlotsGameObject;
        [SerializeField] private GameObject m_crosshair;
        [SerializeField] private TMP_Text m_runesCountText;
        [SerializeField] private TMP_Text m_runesToAddText;
        [SerializeField] private CanvasGroup[] m_hudCanvasGroups =
            System.Array.Empty<CanvasGroup>();

        private CharacterNetworkManager m_boundNetworkManager;
        private PlayerInventoryManager m_boundInventoryManager;
        private PlayerNetworkManager m_boundPlayerNetworkManager;
        private Coroutine m_waitThenAddRunesCoroutine;
        private int m_pendingRunesToAdd;
        private int m_displayedRunes;

        /// <summary>Gets the Rune amount waiting to merge into the displayed balance.</summary>
        public int PendingRunesToAdd => m_pendingRunesToAdd;

        private void OnDisable()
        {
            StopRuneMergeCoroutine();
            m_pendingRunesToAdd = 0;
            SetPendingRuneText(0);
        }

        /// <summary>
        /// Updates the local Health presentation from shared character state.
        /// </summary>
        public void SetNewHealthValue(float currentHealth)
        {
            m_healthBar?.SetStat(currentHealth);
        }

        /// <summary>
        /// Updates the local Health range from shared character state.
        /// </summary>
        public void SetMaxHealthValue(float maximumHealth)
        {
            m_healthBar?.SetMaxStat(maximumHealth);
            RefreshHUD();
        }

        /// <summary>Applies Poison coloring to the locally owned Health bar.</summary>
        public void SetHealthBarPoisoned(bool isPoisoned)
        {
            m_healthBar?.SetPoisonedColor(isPoisoned);
        }

        /// <summary>
        /// Updates the local Stamina presentation from shared character state.
        /// </summary>
        public void SetNewStaminaValue(float currentStamina)
        {
            m_staminaBar?.SetStat(currentStamina);
        }

        /// <summary>
        /// Updates the local Stamina range from shared character state.
        /// </summary>
        public void SetMaxStaminaValue(float maximumStamina)
        {
            m_staminaBar?.SetMaxStat(maximumStamina);
            RefreshHUD();
        }

        /// <summary>Updates the local Focus Point presentation.</summary>
        public void SetNewFocusPointsValue(float currentFocusPoints)
        {
            m_focusPointsBar?.SetStat(currentFocusPoints);
        }

        /// <summary>Updates the local Focus Point range.</summary>
        public void SetMaxFocusPointsValue(float maximumFocusPoints)
        {
            m_focusPointsBar?.SetMaxStat(maximumFocusPoints);
            RefreshHUD();
        }

        /// <summary>Updates the shared maximum used by every authored buildup bar.</summary>
        public void SetMaxBuildupValue(float maximumBuildup)
        {
            foreach (UIBuildupBar buildupBar in
                m_buildupBars ?? System.Array.Empty<UIBuildupBar>())
            {
                buildupBar?.SetMaxBuildupValue(maximumBuildup);
            }
        }

        /// <summary>Updates and toggles the bar matching one accumulation channel.</summary>
        public void SetBuildupAmount(Buildup buildupType, float buildupAmount)
        {
            foreach (UIBuildupBar buildupBar in
                m_buildupBars ?? System.Array.Empty<UIBuildupBar>())
            {
                if (buildupBar != null && buildupBar.BuildupType == buildupType)
                {
                    buildupBar.SetBuildupAmount(buildupAmount);
                    return;
                }
            }
        }

        /// <summary>Shows one signed Rune change and restarts the merge delay.</summary>
        public void SetRunesCount(int runesToAdd)
        {
            if (runesToAdd == 0)
            {
                return;
            }

            m_pendingRunesToAdd = CalculatePendingRuneTotal(
                m_pendingRunesToAdd,
                runesToAdd);
            SetPendingRuneText(m_pendingRunesToAdd);
            StopRuneMergeCoroutine();
            if (isActiveAndEnabled)
            {
                m_waitThenAddRunesCoroutine = StartCoroutine(
                    WaitThenUpdateRuneCount());
            }
        }

        /// <summary>Sets the visible Rune balance without showing an earned amount.</summary>
        public void SetRuneCountImmediately(int runeCount)
        {
            StopRuneMergeCoroutine();
            m_pendingRunesToAdd = 0;
            m_displayedRunes = Mathf.Max(0, runeCount);
            SetRuneCountText(m_displayedRunes);
            SetPendingRuneText(0);
        }

        /// <summary>Returns an overflow-safe pending total for signed Rune changes.</summary>
        public static int CalculatePendingRuneTotal(
            int pendingRunes,
            int runesToAdd)
        {
            long total = (long)pendingRunes + runesToAdd;
            return total <= int.MinValue
                ? int.MinValue
                : total >= int.MaxValue
                    ? int.MaxValue
                    : (int)total;
        }

        /// <summary>Formats a signed pending Rune change without producing a double sign.</summary>
        public static string FormatRuneChange(int runeChange)
        {
            if (runeChange == 0)
            {
                return string.Empty;
            }

            string sign = runeChange > 0 ? "+" : "-";
            return $"{sign} {System.Math.Abs((long)runeChange)}";
        }

        /// <summary>
        /// Subscribes the HUD to one locally owned character and initializes its resources.
        /// </summary>
        public void BindStats(CharacterNetworkManager networkManager)
        {
            if (networkManager == null)
            {
                return;
            }

            if (m_boundNetworkManager != networkManager)
            {
                UnbindCurrentStats();
                m_boundNetworkManager = networkManager;
                m_boundNetworkManager.CurrentHealth.OnValueChanged += OnCurrentHealthChanged;
                m_boundNetworkManager.MaxHealth.OnValueChanged += OnMaxHealthChanged;
                m_boundNetworkManager.CurrentStamina.OnValueChanged += OnCurrentStaminaChanged;
                m_boundNetworkManager.MaxStamina.OnValueChanged += OnMaxStaminaChanged;
                m_boundNetworkManager.CurrentFocusPoints.OnValueChanged +=
                    OnCurrentFocusPointsChanged;
                m_boundNetworkManager.MaxFocusPoints.OnValueChanged +=
                    OnMaxFocusPointsChanged;
                m_boundNetworkManager.PoisonBuildup.OnValueChanged +=
                    OnPoisonBuildupChanged;
                m_boundNetworkManager.BleedBuildup.OnValueChanged +=
                    OnBleedBuildupChanged;
                m_boundNetworkManager.FrostBuildup.OnValueChanged +=
                    OnFrostBuildupChanged;
                m_boundNetworkManager.BuildupCapacity.OnValueChanged +=
                    OnBuildupCapacityChanged;
                m_boundNetworkManager.IsPoisoned.OnValueChanged +=
                    OnIsPoisonedChanged;
            }

            gameObject.SetActive(true);
            RefreshStatBars();
        }

        /// <summary>
        /// Removes the binding only when the supplied character still owns this HUD connection.
        /// </summary>
        public void UnbindStats(CharacterNetworkManager networkManager)
        {
            if (m_boundNetworkManager != networkManager)
            {
                return;
            }

            UnbindCurrentStats();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Binds the reusable quick slots to the locally owned player's equipped weapons.
        /// </summary>
        public void BindQuickSlots(PlayerInventoryManager inventoryManager)
        {
            if (inventoryManager == null)
            {
                return;
            }

            if (m_boundInventoryManager == inventoryManager)
            {
                return;
            }

            UnbindCurrentQuickSlots();
            m_boundInventoryManager = inventoryManager;
            m_boundInventoryManager.RightHandWeaponChanged +=
                OnRightHandWeaponChanged;
            m_boundInventoryManager.LeftHandWeaponChanged +=
                OnLeftHandWeaponChanged;
            m_boundInventoryManager.CurrentSpellChanged += OnCurrentSpellChanged;
            m_boundInventoryManager.CurrentQuickSlotItemChanged +=
                OnCurrentQuickSlotItemChanged;
            m_boundInventoryManager.MainProjectileChanged +=
                OnMainProjectileChanged;
            m_boundInventoryManager.SecondaryProjectileChanged +=
                OnSecondaryProjectileChanged;
            m_boundPlayerNetworkManager = inventoryManager
                .GetComponent<PlayerNetworkManager>();
            if (m_boundPlayerNetworkManager != null)
            {
                m_boundPlayerNetworkManager.RemainingHealthFlasks.OnValueChanged +=
                    OnQuickSlotItemAmountChanged;
                m_boundPlayerNetworkManager.RemainingFocusPointFlasks
                    .OnValueChanged += OnQuickSlotItemAmountChanged;
            }

            RefreshQuickSlots();
        }

        /// <summary>
        /// Releases the quick-slot binding only when it still represents the supplied inventory.
        /// </summary>
        public void UnbindQuickSlots(PlayerInventoryManager inventoryManager)
        {
            if (m_boundInventoryManager != inventoryManager)
            {
                return;
            }

            UnbindCurrentQuickSlots();
            m_leftWeaponQuickSlot?.SetItem(null);
            m_rightWeaponQuickSlot?.SetItem(null);
            m_spellQuickSlot?.SetItem(null);
            m_itemQuickSlot?.SetQuickSlotItem(null, null);
            m_mainProjectileQuickSlot?.SetProjectile(null);
            m_secondaryProjectileQuickSlot?.SetProjectile(null);
            ToggleProjectileQuickSlotsVisibility(false);
        }

        /// <summary>
        /// Forces the status-bar layout to react to stat-driven width changes.
        /// </summary>
        public void RefreshHUD()
        {
            RefreshStatBarLayout(m_healthBar);
            RefreshStatBarLayout(m_staminaBar);
            RefreshStatBarLayout(m_focusPointsBar);

            if (transform is RectTransform rectTransform)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }

        /// <summary>Hides gameplay HUD groups while preserving their runtime bindings.</summary>
        public void HideHUD()
        {
            SetHUDVisibility(false);
        }

        /// <summary>Restores gameplay HUD groups after every modal menu closes.</summary>
        public void ShowHUD()
        {
            SetHUDVisibility(true);
        }

        /// <summary>Shows the center-screen reticle only during first-person aiming.</summary>
        public void SetCrosshairVisible(bool isVisible)
        {
            m_crosshair?.SetActive(isVisible);
        }

        /// <summary>Updates the gameplay item quick-slot icon from synchronized inventory.</summary>
        public void SetQuickSlotItemQuickSlotIcon(QuickSlotItem quickSlotItem)
        {
            PlayerManager player = m_boundInventoryManager
                ?.GetComponent<PlayerManager>();
            m_itemQuickSlot?.SetQuickSlotItem(quickSlotItem, player);
        }

        /// <summary>Updates the primary ammunition icon and its real-time count.</summary>
        public void SetMainProjectileQuickSlotIcon(
            RangedProjectileItem projectileItem)
        {
            m_mainProjectileQuickSlot?.SetProjectile(projectileItem);
        }

        /// <summary>Updates the secondary ammunition icon and its real-time count.</summary>
        public void SetSecondaryProjectileQuickSlotIcon(
            RangedProjectileItem projectileItem)
        {
            m_secondaryProjectileQuickSlot?.SetProjectile(projectileItem);
        }

        /// <summary>Shows ammunition context only while either hand equips a Bow.</summary>
        public void ToggleProjectileQuickSlotsVisibility(bool isVisible)
        {
            m_projectileQuickSlotsGameObject?.SetActive(isVisible);
        }

        /// <summary>Returns whether the local weapon context requires ammunition HUD.</summary>
        public static bool ShouldShowProjectileQuickSlots(
            WeaponItem rightHandWeapon,
            WeaponItem leftHandWeapon)
        {
            return rightHandWeapon?.WeaponClass == WeaponClass.Bow ||
                leftHandWeapon?.WeaponClass == WeaponClass.Bow;
        }

        private void OnCurrentHealthChanged(float previousHealth, float currentHealth)
        {
            SetNewHealthValue(currentHealth);
        }

        private void OnMaxHealthChanged(float previousHealth, float maximumHealth)
        {
            SetMaxHealthValue(maximumHealth);
            if (m_boundNetworkManager != null)
            {
                SetNewHealthValue(m_boundNetworkManager.CurrentHealth.Value);
            }
        }

        private void OnCurrentStaminaChanged(float previousStamina, float currentStamina)
        {
            SetNewStaminaValue(currentStamina);
        }

        private void OnMaxStaminaChanged(float previousStamina, float maximumStamina)
        {
            SetMaxStaminaValue(maximumStamina);
            if (m_boundNetworkManager != null)
            {
                SetNewStaminaValue(m_boundNetworkManager.CurrentStamina.Value);
            }
        }

        private void OnCurrentFocusPointsChanged(
            float previousFocusPoints,
            float currentFocusPoints)
        {
            SetNewFocusPointsValue(currentFocusPoints);
        }

        private void OnMaxFocusPointsChanged(
            float previousFocusPoints,
            float maximumFocusPoints)
        {
            SetMaxFocusPointsValue(maximumFocusPoints);
            if (m_boundNetworkManager != null)
            {
                SetNewFocusPointsValue(
                    m_boundNetworkManager.CurrentFocusPoints.Value);
            }
        }

        private void OnPoisonBuildupChanged(
            float previousBuildup,
            float currentBuildup)
        {
            SetBuildupAmount(Buildup.Poison, currentBuildup);
        }

        private void OnBleedBuildupChanged(
            float previousBuildup,
            float currentBuildup)
        {
            SetBuildupAmount(Buildup.Bleed, currentBuildup);
        }

        private void OnFrostBuildupChanged(
            float previousBuildup,
            float currentBuildup)
        {
            SetBuildupAmount(Buildup.Frost, currentBuildup);
        }

        private void OnBuildupCapacityChanged(
            float previousCapacity,
            float currentCapacity)
        {
            SetMaxBuildupValue(currentCapacity);
            RefreshBuildupBars();
        }

        private void OnIsPoisonedChanged(bool wasPoisoned, bool isPoisoned)
        {
            SetHealthBarPoisoned(isPoisoned);
        }

        private void OnRightHandWeaponChanged(WeaponItem weapon)
        {
            m_rightWeaponQuickSlot?.SetItem(weapon);
            RefreshProjectileQuickSlotVisibility();
        }

        private void OnLeftHandWeaponChanged(WeaponItem weapon)
        {
            m_leftWeaponQuickSlot?.SetItem(weapon);
            RefreshProjectileQuickSlotVisibility();
        }

        private void OnCurrentSpellChanged(SpellItem spell)
        {
            m_spellQuickSlot?.SetItem(spell);
        }

        private void OnCurrentQuickSlotItemChanged(QuickSlotItem quickSlotItem)
        {
            SetQuickSlotItemQuickSlotIcon(quickSlotItem);
        }

        private void OnQuickSlotItemAmountChanged(
            int previousAmount,
            int currentAmount)
        {
            SetQuickSlotItemQuickSlotIcon(
                m_boundInventoryManager?.CurrentQuickSlotItem);
        }

        private void OnMainProjectileChanged(RangedProjectileItem projectileItem)
        {
            SetMainProjectileQuickSlotIcon(projectileItem);
        }

        private void OnSecondaryProjectileChanged(
            RangedProjectileItem projectileItem)
        {
            SetSecondaryProjectileQuickSlotIcon(projectileItem);
        }

        private void RefreshStatBars()
        {
            SetMaxHealthValue(m_boundNetworkManager.MaxHealth.Value);
            SetNewHealthValue(m_boundNetworkManager.CurrentHealth.Value);
            SetMaxStaminaValue(m_boundNetworkManager.MaxStamina.Value);
            SetNewStaminaValue(m_boundNetworkManager.CurrentStamina.Value);
            SetMaxFocusPointsValue(m_boundNetworkManager.MaxFocusPoints.Value);
            SetNewFocusPointsValue(
                m_boundNetworkManager.CurrentFocusPoints.Value);
            SetHealthBarPoisoned(m_boundNetworkManager.IsPoisoned.Value);
            RefreshBuildupBars();
        }

        private void RefreshBuildupBars()
        {
            if (m_boundNetworkManager == null)
            {
                return;
            }

            SetMaxBuildupValue(m_boundNetworkManager.BuildupCapacity.Value);
            SetBuildupAmount(
                Buildup.Poison,
                m_boundNetworkManager.PoisonBuildup.Value);
            SetBuildupAmount(
                Buildup.Bleed,
                m_boundNetworkManager.BleedBuildup.Value);
            SetBuildupAmount(
                Buildup.Frost,
                m_boundNetworkManager.FrostBuildup.Value);
        }

        private void RefreshQuickSlots()
        {
            m_leftWeaponQuickSlot?.SetItem(
                m_boundInventoryManager.CurrentLeftHandWeapon);
            m_rightWeaponQuickSlot?.SetItem(
                m_boundInventoryManager.CurrentRightHandWeapon);
            m_spellQuickSlot?.SetItem(m_boundInventoryManager.CurrentSpell);
            SetQuickSlotItemQuickSlotIcon(
                m_boundInventoryManager.CurrentQuickSlotItem);
            SetMainProjectileQuickSlotIcon(
                m_boundInventoryManager.MainProjectile);
            SetSecondaryProjectileQuickSlotIcon(
                m_boundInventoryManager.SecondaryProjectile);
            RefreshProjectileQuickSlotVisibility();
        }

        private void UnbindCurrentStats()
        {
            if (m_boundNetworkManager == null)
            {
                return;
            }

            m_boundNetworkManager.CurrentHealth.OnValueChanged -= OnCurrentHealthChanged;
            m_boundNetworkManager.MaxHealth.OnValueChanged -= OnMaxHealthChanged;
            m_boundNetworkManager.CurrentStamina.OnValueChanged -= OnCurrentStaminaChanged;
            m_boundNetworkManager.MaxStamina.OnValueChanged -= OnMaxStaminaChanged;
            m_boundNetworkManager.CurrentFocusPoints.OnValueChanged -=
                OnCurrentFocusPointsChanged;
            m_boundNetworkManager.MaxFocusPoints.OnValueChanged -=
                OnMaxFocusPointsChanged;
            m_boundNetworkManager.PoisonBuildup.OnValueChanged -=
                OnPoisonBuildupChanged;
            m_boundNetworkManager.BleedBuildup.OnValueChanged -=
                OnBleedBuildupChanged;
            m_boundNetworkManager.FrostBuildup.OnValueChanged -=
                OnFrostBuildupChanged;
            m_boundNetworkManager.BuildupCapacity.OnValueChanged -=
                OnBuildupCapacityChanged;
            m_boundNetworkManager.IsPoisoned.OnValueChanged -=
                OnIsPoisonedChanged;
            m_boundNetworkManager = null;
        }

        private void UnbindCurrentQuickSlots()
        {
            if (m_boundInventoryManager == null)
            {
                return;
            }

            m_boundInventoryManager.RightHandWeaponChanged -=
                OnRightHandWeaponChanged;
            m_boundInventoryManager.LeftHandWeaponChanged -=
                OnLeftHandWeaponChanged;
            m_boundInventoryManager.CurrentSpellChanged -= OnCurrentSpellChanged;
            m_boundInventoryManager.CurrentQuickSlotItemChanged -=
                OnCurrentQuickSlotItemChanged;
            m_boundInventoryManager.MainProjectileChanged -=
                OnMainProjectileChanged;
            m_boundInventoryManager.SecondaryProjectileChanged -=
                OnSecondaryProjectileChanged;
            if (m_boundPlayerNetworkManager != null)
            {
                m_boundPlayerNetworkManager.RemainingHealthFlasks.OnValueChanged -=
                    OnQuickSlotItemAmountChanged;
                m_boundPlayerNetworkManager.RemainingFocusPointFlasks
                    .OnValueChanged -= OnQuickSlotItemAmountChanged;
                m_boundPlayerNetworkManager = null;
            }

            m_boundInventoryManager = null;
        }

        private void RefreshProjectileQuickSlotVisibility()
        {
            ToggleProjectileQuickSlotsVisibility(
                ShouldShowProjectileQuickSlots(
                    m_boundInventoryManager?.CurrentRightHandWeapon,
                    m_boundInventoryManager?.CurrentLeftHandWeapon));
        }

        private static void RefreshStatBarLayout(UIStatBar statBar)
        {
            if (statBar == null)
            {
                return;
            }

            GameObject statBarObject = statBar.gameObject;
            bool wasActive = statBarObject.activeSelf;
            statBarObject.SetActive(false);
            statBarObject.SetActive(wasActive);
        }

        private void SetHUDVisibility(bool isVisible)
        {
            if (m_hudCanvasGroups == null)
            {
                return;
            }

            foreach (CanvasGroup canvasGroup in m_hudCanvasGroups)
            {
                if (canvasGroup == null)
                {
                    continue;
                }

                canvasGroup.alpha = isVisible ? 1f : 0f;
                canvasGroup.interactable = isVisible;
                canvasGroup.blocksRaycasts = isVisible;
            }
        }

        private IEnumerator WaitThenUpdateRuneCount()
        {
            yield return new WaitForSecondsRealtime(k_RuneMergeDelay);

            PlayerManager localPlayer = PlayerUIManager.Instance?.LocalPlayer;
            int currentRunes = localPlayer?.PlayerStatsManager != null
                ? localPlayer.PlayerStatsManager.Runes
                : CalculatePendingRuneTotal(
                    m_displayedRunes,
                    m_pendingRunesToAdd);
            m_displayedRunes = currentRunes;
            SetRuneCountText(currentRunes);
            m_pendingRunesToAdd = 0;
            SetPendingRuneText(0);
            m_waitThenAddRunesCoroutine = null;
        }

        private void StopRuneMergeCoroutine()
        {
            if (m_waitThenAddRunesCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_waitThenAddRunesCoroutine);
            m_waitThenAddRunesCoroutine = null;
        }

        private void SetRuneCountText(int runeCount)
        {
            if (m_runesCountText != null)
            {
                m_runesCountText.text = Mathf.Max(0, runeCount).ToString();
            }
        }

        private void SetPendingRuneText(int pendingRunes)
        {
            if (m_runesToAddText == null)
            {
                return;
            }

            m_runesToAddText.text = FormatRuneChange(pendingRunes);
            m_runesToAddText.gameObject.SetActive(pendingRunes != 0);
        }
    }
}
