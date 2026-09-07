using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Owns weapon selection, validation, confirmation, and upgrade commit UI.</summary>
    public sealed class PlayerUIWeaponUpgradeManager : PlayerUIMenu
    {
        private const string k_FullyUpgradedMessage =
            "Weapon Is Fully Upgraded";

        [Header("WEAPON SLOTS")]
        [SerializeField] private Button m_rightWeaponButton;
        [SerializeField] private Button m_leftWeaponButton;
        [SerializeField] private Image m_rightWeaponIcon;
        [SerializeField] private Image m_leftWeaponIcon;

        [Header("UPGRADE DETAILS")]
        [SerializeField] private TMP_Text m_weaponNameText;
        [SerializeField] private TMP_Text m_upgradeLevelText;
        [SerializeField] private TMP_Text m_currentMaterialsText;
        [SerializeField] private TMP_Text m_materialsRequiredText;
        [SerializeField] private Button m_upgradeButton;
        [SerializeField] private Button m_returnButton;

        [Header("CONFIRMATION")]
        [SerializeField] private GameObject m_confirmationPopup;
        [SerializeField] private TMP_Text m_confirmationText;
        [SerializeField] private Button m_confirmButton;
        [SerializeField] private Button m_cancelButton;

        [Header("FEEDBACK")]
        [SerializeField] private Color m_normalColor = Color.white;
        [SerializeField] private Color m_unavailableColor =
            new(0.85f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color m_fullyUpgradedColor =
            new(0.35f, 0.8f, 0.45f, 1f);
        [SerializeField, Range(0.1f, 1f)] private float m_unavailableIconAlpha =
            0.35f;

        private WeaponItem m_currentSelectedWeapon;
        private UpgradeMaterial m_currentUpgradeCost;
        private Button m_lastSelectedWeaponButton;
        private Coroutine m_delayedMenuRoutine;
        private bool m_canCommitCurrentSelection;

        public WeaponItem CurrentSelectedWeapon => m_currentSelectedWeapon;
        public UpgradeMaterial CurrentUpgradeCost => m_currentUpgradeCost;
        public bool IsConfirmationOpen =>
            m_confirmationPopup?.activeSelf == true;

        /// <summary>Opens the shared upgrade service and selects the right-hand weapon.</summary>
        public void OpenWeaponUpgradeMenu()
        {
            CancelDelayedMenuRoutine();
            PlayerUIManager.Instance?.PlayerUIPopUpManager
                ?.CloseAllPopUpWindows();
            OpenMenu();
            if (!IsMenuOpen)
            {
                return;
            }

            CloseConfirmationPopup(false);
            RefreshWeaponIcons();
            SelectRightHandWeapon();
            m_rightWeaponButton?.Select();
        }

        /// <summary>Defers service opening past dialogue HUD restoration.</summary>
        public void OpenMenuAfterFixedFrame()
        {
            CancelDelayedMenuRoutine();
            m_delayedMenuRoutine = StartCoroutine(OpenMenuAfterFixedFrameRoutine());
        }

        /// <summary>Defers service closing so a trigger exit cannot fight dialogue cleanup.</summary>
        public void CloseMenuAfterFixedFrame()
        {
            CancelDelayedMenuRoutine();
            m_delayedMenuRoutine = StartCoroutine(CloseMenuAfterFixedFrameRoutine());
        }

        /// <inheritdoc />
        public override void CloseMenu()
        {
            CancelDelayedMenuRoutine();
            CloseConfirmationPopup(false);
            ReleaseUpgradeCost();
            m_currentSelectedWeapon = null;
            m_lastSelectedWeaponButton = null;
            base.CloseMenu();
        }

        /// <summary>Selects the locally equipped right-hand runtime weapon.</summary>
        public void SelectRightHandWeapon()
        {
            SelectWeapon(
                ResolveLocalInventory()?.CurrentRightHandWeapon,
                m_rightWeaponButton);
        }

        /// <summary>Selects the locally equipped left-hand runtime weapon.</summary>
        public void SelectLeftHandWeapon()
        {
            SelectWeapon(
                ResolveLocalInventory()?.CurrentLeftHandWeapon,
                m_leftWeaponButton);
        }

        /// <summary>Validates the selected weapon before exposing the commit action.</summary>
        public bool AttemptToUpgradeWeapon()
        {
            RefreshUpgradeDetails();
            if (!CanUpgradeCurrentSelection())
            {
                PlayerUIManager.Instance?.PlayUnableToContinueSound();
                return false;
            }

            m_canCommitCurrentSelection = true;
            SetWeaponButtonsInteractable(false);
            if (m_confirmationText != null)
            {
                m_confirmationText.text =
                    $"Strengthen {m_currentSelectedWeapon.ItemName} to " +
                    $"+{(int)m_currentSelectedWeapon.UpgradeLevel + 1}?";
            }

            m_confirmationPopup?.transform.SetAsLastSibling();
            m_confirmationPopup?.SetActive(true);
            m_confirmButton?.Select();
            m_confirmButton?.OnSelect(null);
            return true;
        }

        /// <summary>UnityEvent bridge for the validation step.</summary>
        public void AttemptToUpgradeWeaponFromUI()
        {
            AttemptToUpgradeWeapon();
        }

        /// <summary>Consumes the validated stack and advances exactly one weapon level.</summary>
        public bool UpgradeWeapon()
        {
            PlayerInventoryManager inventory = ResolveLocalInventory();
            if (!m_canCommitCurrentSelection ||
                inventory == null ||
                !CanUpgradeCurrentSelection() ||
                !inventory.RemoveItemFromInventory(m_currentUpgradeCost) ||
                !m_currentSelectedWeapon.TryUpgrade())
            {
                PlayerUIManager.Instance?.PlayUnableToContinueSound();
                CloseConfirmationPopup(true);
                RefreshUpgradeDetails();
                return false;
            }

            RefreshEquippedWeaponDamage();
            WorldSaveGameManager saveGameManager = WorldSaveGameManager.Instance;
            if (saveGameManager?.CanSaveGame == true)
            {
                saveGameManager.SaveGame();
            }

            PlayerUIManager.Instance?.PlayMenuConfirmSound();
            CloseConfirmationPopup(true);
            RefreshWeaponIcons();
            RefreshUpgradeDetails();
            return true;
        }

        /// <summary>UnityEvent bridge for the confirmed commit step.</summary>
        public void UpgradeWeaponFromUI()
        {
            UpgradeWeapon();
        }

        /// <summary>Cancels the pending commit and restores weapon selection.</summary>
        public void CancelUpgradeWeapon()
        {
            CloseConfirmationPopup(true);
        }

        /// <summary>Creates a catalog-backed isolated cost for the selected weapon.</summary>
        public UpgradeMaterial DetermineUpgradeCostBasedOnWeapon(
            WeaponItem weapon)
        {
            ReleaseUpgradeCost();
            if (weapon == null ||
                weapon.IsUnarmed ||
                !WeaponUpgradeRules.TryGetUpgradeCost(
                    weapon.UpgradeLevel,
                    out UpgradeStone upgradeStone,
                    out int requiredAmount))
            {
                return null;
            }

            m_currentUpgradeCost = WorldItemDatabase.Instance
                ?.CreateUpgradeMaterialCost(upgradeStone, requiredAmount);
            return m_currentUpgradeCost;
        }

        /// <summary>Checks both material identity and required stack amount.</summary>
        public bool PlayerHasUpgradeCost()
        {
            PlayerInventoryManager inventory = ResolveLocalInventory();
            return inventory != null &&
                m_currentUpgradeCost != null &&
                inventory.GetItemAmount(m_currentUpgradeCost.ItemID) >=
                    m_currentUpgradeCost.CurrentItemAmount;
        }

        private IEnumerator OpenMenuAfterFixedFrameRoutine()
        {
            yield return new WaitForFixedUpdate();
            m_delayedMenuRoutine = null;
            OpenWeaponUpgradeMenu();
        }

        private IEnumerator CloseMenuAfterFixedFrameRoutine()
        {
            yield return new WaitForFixedUpdate();
            m_delayedMenuRoutine = null;
            CloseMenu();
        }

        private void SelectWeapon(WeaponItem weapon, Button weaponButton)
        {
            if (!IsMenuOpen || IsConfirmationOpen)
            {
                return;
            }

            m_currentSelectedWeapon = weapon;
            m_lastSelectedWeaponButton = weaponButton;
            RefreshUpgradeDetails();
        }

        private void RefreshWeaponIcons()
        {
            PlayerInventoryManager inventory = ResolveLocalInventory();
            ApplyWeaponIcon(m_rightWeaponIcon, inventory?.CurrentRightHandWeapon);
            ApplyWeaponIcon(m_leftWeaponIcon, inventory?.CurrentLeftHandWeapon);
        }

        private void RefreshUpgradeDetails()
        {
            UpgradeMaterial upgradeCost =
                DetermineUpgradeCostBasedOnWeapon(m_currentSelectedWeapon);
            bool isFullyUpgraded =
                m_currentSelectedWeapon?.IsFullyUpgraded == true;
            bool hasCost = PlayerHasUpgradeCost();
            int ownedAmount = upgradeCost != null
                ? ResolveLocalInventory()?.GetItemAmount(upgradeCost.ItemID) ?? 0
                : 0;

            if (m_weaponNameText != null)
            {
                m_weaponNameText.text = m_currentSelectedWeapon != null
                    ? m_currentSelectedWeapon.ItemName
                    : "No Weapon Selected";
            }

            if (m_upgradeLevelText != null)
            {
                m_upgradeLevelText.text = m_currentSelectedWeapon != null
                    ? $"Current Level: +{(int)m_currentSelectedWeapon.UpgradeLevel}"
                    : "Current Level: N/A";
            }

            if (m_currentMaterialsText != null)
            {
                m_currentMaterialsText.text = isFullyUpgraded
                    ? "Current Materials: N/A"
                    : upgradeCost != null
                        ? $"Current Materials: {ownedAmount}"
                        : "Current Materials: N/A";
                m_currentMaterialsText.color = isFullyUpgraded
                    ? m_fullyUpgradedColor
                    : hasCost
                        ? m_normalColor
                        : m_unavailableColor;
            }

            if (m_materialsRequiredText != null)
            {
                m_materialsRequiredText.text = isFullyUpgraded
                    ? k_FullyUpgradedMessage
                    : upgradeCost != null
                        ? $"Materials Required: " +
                            $"{upgradeCost.ItemName} x" +
                            $"{upgradeCost.CurrentItemAmount}"
                        : "Materials Required: N/A";
                m_materialsRequiredText.color = isFullyUpgraded
                    ? m_fullyUpgradedColor
                    : hasCost
                        ? m_normalColor
                        : m_unavailableColor;
            }

            ResetWeaponIconAlpha();
            ApplySelectedIconAvailability(hasCost && !isFullyUpgraded);
        }

        private bool CanUpgradeCurrentSelection()
        {
            return m_currentSelectedWeapon != null &&
                !m_currentSelectedWeapon.IsUnarmed &&
                !m_currentSelectedWeapon.IsFullyUpgraded &&
                m_currentUpgradeCost != null &&
                PlayerHasUpgradeCost();
        }

        private void CloseConfirmationPopup(bool restoreSelection)
        {
            m_canCommitCurrentSelection = false;
            m_confirmationPopup?.SetActive(false);
            SetWeaponButtonsInteractable(true);
            if (restoreSelection &&
                m_lastSelectedWeaponButton != null &&
                m_lastSelectedWeaponButton.IsInteractable())
            {
                m_lastSelectedWeaponButton.Select();
                m_lastSelectedWeaponButton.OnSelect(null);
            }
        }

        private void SetWeaponButtonsInteractable(bool isInteractable)
        {
            if (m_returnButton != null)
            {
                m_returnButton.interactable = isInteractable;
            }
            if (m_rightWeaponButton != null)
            {
                m_rightWeaponButton.interactable = isInteractable;
            }

            if (m_leftWeaponButton != null)
            {
                m_leftWeaponButton.interactable = isInteractable;
            }

            if (m_upgradeButton != null)
            {
                m_upgradeButton.interactable = isInteractable;
            }
        }

        private void ApplySelectedIconAvailability(bool isAvailable)
        {
            Image selectedIcon = m_lastSelectedWeaponButton == m_leftWeaponButton
                ? m_leftWeaponIcon
                : m_rightWeaponIcon;
            if (selectedIcon == null)
            {
                return;
            }

            Color iconColor = selectedIcon.color;
            iconColor.a = isAvailable ? 1f : m_unavailableIconAlpha;
            selectedIcon.color = iconColor;
        }

        private void ResetWeaponIconAlpha()
        {
            ResetIconAlpha(m_rightWeaponIcon);
            ResetIconAlpha(m_leftWeaponIcon);
        }

        private static void ResetIconAlpha(Image icon)
        {
            if (icon == null)
            {
                return;
            }

            Color iconColor = icon.color;
            iconColor.a = 1f;
            icon.color = iconColor;
        }

        private static void ApplyWeaponIcon(Image icon, WeaponItem weapon)
        {
            if (icon == null)
            {
                return;
            }

            bool shouldShow = weapon != null &&
                !weapon.IsUnarmed &&
                weapon.ItemIcon != null;
            icon.sprite = shouldShow ? weapon.ItemIcon : null;
            icon.enabled = shouldShow;
            Color iconColor = icon.color;
            iconColor.a = 1f;
            icon.color = iconColor;
        }

        private void RefreshEquippedWeaponDamage()
        {
            PlayerEquipmentManager equipmentManager = PlayerUIManager.Instance
                ?.LocalPlayer?.EquipmentManager;
            equipmentManager?.CurrentRightHandWeaponManager?.SetWeaponDamage();
            equipmentManager?.CurrentLeftHandWeaponManager?.SetWeaponDamage();
            equipmentManager?.CurrentTwoHandWeaponManager?.SetWeaponDamage();
        }

        private void ReleaseUpgradeCost()
        {
            if (m_currentUpgradeCost != null)
            {
                Destroy(m_currentUpgradeCost);
                m_currentUpgradeCost = null;
            }
        }

        private void CancelDelayedMenuRoutine()
        {
            if (m_delayedMenuRoutine == null)
            {
                return;
            }

            StopCoroutine(m_delayedMenuRoutine);
            m_delayedMenuRoutine = null;
        }

        private static PlayerInventoryManager ResolveLocalInventory()
        {
            return PlayerUIManager.Instance?.LocalPlayer?.InventoryManager;
        }
    }
}
