using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Owns the item-browser display commands without changing inventory or menu ownership.</summary>
    internal sealed class ItemDetailDisplay : IDisposable
    {
        private readonly VisualElement m_root;
        private readonly VisualElement m_surface;
        private readonly Button m_middle;
        private readonly Button m_right;
        private readonly InputAction m_middleAction;
        private readonly InputAction m_rightAction;

        public ItemDetailDisplay(VisualElement root)
        {
            m_root = root;
            m_surface = root.Q(className: "menu-overlay");
            m_surface.RemoveFromClassList("menu--description");
            m_surface.RemoveFromClassList("menu--simple");
            m_middle = root.Q<Button>("switch-display");
            m_right = root.Q<Button>("switch-view");
            m_middle.clicked += ToggleDescription;
            m_right.clicked += ToggleStatus;
            m_middleAction = new InputAction("Item middle display", InputActionType.Button, "<Keyboard>/r");
            m_middleAction.AddBinding("<Gamepad>/buttonWest");
            m_rightAction = new InputAction("Item right display", InputActionType.Button, "<Keyboard>/f");
            m_rightAction.AddBinding("<Gamepad>/buttonNorth");
            m_middleAction.performed += OnMiddleDisplay;
            m_rightAction.performed += OnRightDisplay;
            if (Application.isPlaying)
            {
                m_middleAction.Enable();
                m_rightAction.Enable();
            }
            InputHintDevice.DeviceChanged += RefreshHints;
            RefreshHints();
        }

        public void Dispose()
        {
            InputHintDevice.DeviceChanged -= RefreshHints;
            m_middle.clicked -= ToggleDescription;
            m_right.clicked -= ToggleStatus;
            m_middleAction.performed -= OnMiddleDisplay;
            m_rightAction.performed -= OnRightDisplay;
            m_middleAction.Dispose();
            m_rightAction.Dispose();
        }

        public void ToggleDescription()
        {
            bool description = !m_surface.ClassListContains("menu--description");
            m_surface.EnableInClassList("menu--description", description);
            Label heading = m_root.Q<Label>("detail-effect-heading");
            if (heading != null)
            {
                heading.text = description ? "道具说明" : "道具效果";
            }
        }

        public void ToggleStatus()
        {
            m_surface.EnableInClassList("menu--simple", !m_surface.ClassListContains("menu--simple"));
        }

        private void OnMiddleDisplay(InputAction.CallbackContext context)
        {
            if (m_surface.ClassListContains("equipment-menu") &&
                !m_surface.ClassListContains("equipment-menu--browsing"))
            {
                return;
            }
            if (m_root.panel?.focusController.focusedElement is not TextField)
            {
                ToggleDescription();
            }
        }

        private void OnRightDisplay(InputAction.CallbackContext context)
        {
            if (m_root.panel?.focusController.focusedElement is not TextField)
            {
                ToggleStatus();
            }
        }

        private void RefreshHints()
        {
            bool gamepad = InputHintDevice.IsGamepad;
            string prefix = GetGamepadPrefix();
            SetHint(m_middle, "切换显示（中）", gamepad ? prefix + "R_L" : "Key_R");
            SetHint(m_right, "切换显示（右）", gamepad ? prefix + "R_U" : "Key_F");
            Button back = m_root.Q<Button>("back");
            SetHint(back, "返回", gamepad ? prefix + "R_R" : "Key_Esc");
            Button unequip = m_root.Q<Button>("unequip");
            if (unequip != null)
            {
                SetHint(unequip, "卸下", gamepad ? prefix + "R_L" : "Key_X");
                SetHint(m_root.Q<Button>("equip"), "装备", gamepad ? prefix + "R_D" : "Key_Enter");
            }
        }

        public static string GetGamepadPrefix()
        {
            string layout = InputHintDevice.ActiveGamepad?.layout ?? string.Empty;
            return layout.Contains("DualSense") ? "PS5_" :
                layout.Contains("DualShock") ? "PS4_" : "XboxSeries_";
        }

        public static void SetHint(Button button, string label, string iconName)
        {
            button.text = string.Empty;
            Label copy = button.Q<Label>(className: "menu-key-label");
            if (copy == null)
            {
                copy = new Label { pickingMode = PickingMode.Ignore };
                copy.AddToClassList("menu-key-label");
                button.Add(copy);
            }
            copy.text = label;
            Image icon = button.Q<Image>(className: "menu-key-icon");
            if (icon == null)
            {
                icon = new Image { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("menu-key-icon");
                button.Insert(0, icon);
            }
            icon.image = Resources.Load<Texture2D>("UI/EldenRing/Elements/KG_" + iconName);
        }
    }
}
