using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Native equipped-weapon selection, material requirements and explicit upgrade confirmation.</summary>
    public sealed class WeaponUpgradeView : MonoBehaviour
    {
        private UIDocument m_document;
        private PanelSettings m_panel;
        private PlayerUIWeaponUpgradeManager m_owner;
        private VisualElement m_root;
        private string m_selectedHand = "right";
        private bool m_isConfirmationOpen;

        public bool IsConfirmationOpen => m_isConfirmationOpen;

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            m_selectedHand = "right";
            Bind("right", () => SelectHand("right", m_owner.SelectRightHandWeapon));
            Bind("left", () => SelectHand("left", m_owner.SelectLeftHandWeapon));
            Bind("upgrade", m_owner.AttemptToUpgradeWeaponFromUI);
            Bind("confirm", m_owner.UpgradeWeaponFromUI);
            Bind("cancel", m_owner.CancelUpgradeWeapon);
            Bind("back", m_owner.CloseMenu);
            HideConfirmation(false);
        }

        private void OnDisable()
        {
            m_isConfirmationOpen = false;
            m_root = null;
        }

        private void OnDestroy()
        {
            if (m_panel == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(m_panel);
            }
            else
            {
                DestroyImmediate(m_panel);
            }
        }

        /// <summary>Attaches a native document beneath the existing serialized menu lifecycle root.</summary>
        public static WeaponUpgradeView Create(Transform parent, PlayerUIWeaponUpgradeManager owner)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/WeaponUpgrade");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Weapon upgrade Toolkit assets are missing.");
            }
            GameObject instance = new GameObject("Weapon Upgrade Toolkit");
            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 500;
            UIDocument document = instance.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;
            document.gameObject.AddComponent<MenuAudioFeedback>();
            WeaponUpgradeView view = instance.AddComponent<WeaponUpgradeView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            instance.SetActive(true);
            return view;
        }

        /// <summary>Refreshes both equipped instances without copying or mutating inventory objects.</summary>
        public void RefreshWeapons(WeaponItem right, WeaponItem left)
        {
            if (m_root == null)
            {
                return;
            }
            UpdateHand("right", right);
            UpdateHand("left", left);
        }

        /// <summary>Shows the exact catalog material and count validated by the existing service.</summary>
        public void ShowDetails(WeaponItem weapon, UpgradeMaterial cost, int ownedAmount, bool canUpgrade)
        {
            if (m_root == null)
            {
                return;
            }
            bool hasWeapon = weapon != null && !weapon.IsUnarmed;
            m_root.Q<Image>("weapon-icon").sprite = hasWeapon ? weapon.ItemIcon : null;
            m_root.Q<Label>("weapon-name").text = hasWeapon ? weapon.ItemName : "当前手未装备武器";
            m_root.Q<Label>("upgrade-level").text = !hasWeapon ? "请选择已装备的武器" :
                weapon.IsFullyUpgraded ? $"+{(int)weapon.UpgradeLevel} · 已达最高强化等级" :
                    $"+{(int)weapon.UpgradeLevel}  →  +{(int)weapon.UpgradeLevel + 1}";
            m_root.Q<Label>("material-name").text = cost != null ? cost.ItemName : "无需材料";
            m_root.Q<Label>("material-count").text = cost != null ?
                $"持有 {ownedAmount}  /  需要 {cost.CurrentItemAmount}" : "—";
            m_root.Q("material-count").EnableInClassList("upgrade-menu__cost--missing", cost != null && !canUpgrade);
            m_root.Q<Label>("upgrade-hint").text = !hasWeapon ? "徒手无法强化。" :
                weapon.IsFullyUpgraded ? "这把武器已完成全部强化。" : canUpgrade ?
                    "材料充足。强化将提升当前武器，并在确认后消耗材料。" : "强化材料不足，暂时无法强化。";
            m_root.Q<Button>("upgrade").SetEnabled(canUpgrade);
            m_root.Q("right").EnableInClassList("menu-tab--selected", m_selectedHand == "right");
            m_root.Q("left").EnableInClassList("menu-tab--selected", m_selectedHand == "left");
        }

        /// <summary>Locks underlying commands and focuses Cancel before any resources are consumed.</summary>
        public void ShowConfirmation()
        {
            m_isConfirmationOpen = true;
            m_root.Q("upgrade-content").SetEnabled(false);
            m_root.Q("upgrade-confirmation").style.display = DisplayStyle.Flex;
            WeaponItem weapon = m_owner.CurrentSelectedWeapon;
            UpgradeMaterial cost = m_owner.CurrentUpgradeCost;
            m_root.Q<Label>("confirmation-description").text =
                $"将 {weapon.ItemName} 强化至 +{(int)weapon.UpgradeLevel + 1}\n" +
                $"消耗 {cost.ItemName} × {cost.CurrentItemAmount}，确认后无法撤销。";
            Focus("cancel");
        }

        /// <summary>Closes the confirmation and optionally restores the selected weapon command.</summary>
        public void HideConfirmation(bool restoreSelection)
        {
            m_isConfirmationOpen = false;
            if (m_root == null)
            {
                return;
            }
            m_root.Q("upgrade-content").SetEnabled(true);
            m_root.Q("upgrade-confirmation").style.display = DisplayStyle.None;
            if (restoreSelection)
            {
                FocusSelection();
            }
        }

        /// <summary>Restores keyboard or controller focus to the chosen hand.</summary>
        public void FocusSelection()
        {
            Focus(m_selectedHand);
        }

        private void SelectHand(string hand, Action select)
        {
            m_selectedHand = hand;
            select();
        }

        private void UpdateHand(string hand, WeaponItem weapon)
        {
            m_root.Q<Label>(hand + "-name").text = weapon != null && !weapon.IsUnarmed ?
                $"{weapon.ItemName} +{(int)weapon.UpgradeLevel}" : "未装备";
            m_root.Q<Image>(hand + "-icon").sprite = weapon != null && !weapon.IsUnarmed ? weapon.ItemIcon : null;
        }

        private void Bind(string name, Action action)
        {
            m_root.Q<Button>(name).clicked += action;
        }

        private void Focus(string name)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            VisualElement root = m_root;
            root?.schedule.Execute(() =>
            {
                if (m_root == root && isActiveAndEnabled)
                {
                    root.Q(name)?.Focus();
                }
            });
        }
    }
}
