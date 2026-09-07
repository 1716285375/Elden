using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ZZ
{
    /// <summary>Renders semantic action tokens as the supplied input-button artwork.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class InputHintText : MonoBehaviour
    {
        private static readonly Regex s_tokenPattern = new(@"\{([^{}]+)\}");
        [SerializeField, TextArea] private string m_template;
        [SerializeField] private InputActionAsset m_actions;
        [SerializeField] private TMP_SpriteAsset m_icons;
        private TMP_Text m_text;

        public string Template => m_template;

        private void OnEnable()
        {
            m_text = GetComponent<TMP_Text>();
            InputHintDevice.DeviceChanged += Refresh;
            InputSystem.onActionChange += HandleActionChange;
            Refresh();
        }

        private void OnDisable()
        {
            InputHintDevice.DeviceChanged -= Refresh;
            InputSystem.onActionChange -= HandleActionChange;
        }

        /// <summary>Sets dynamic hint content, preserving device switching while the panel is open.</summary>
        public static void SetTemplate(TMP_Text text, string template)
        {
            if (!text.TryGetComponent(out InputHintText hint))
            {
                hint = text.gameObject.AddComponent<InputHintText>();
            }
            hint.m_template = template;
            hint.Refresh();
        }

        /// <summary>Updates icons only when content, bindings, or the active device changes.</summary>
        public void Refresh()
        {
            m_text ??= GetComponent<TMP_Text>();
            if (m_icons == null || m_actions == null)
            {
                InputHintCatalog catalog = Resources.Load<InputHintCatalog>("InputHints/Catalog");
                m_icons = catalog?.Icons;
                m_actions = catalog?.Actions;
            }
            if (m_text == null || m_icons == null || string.IsNullOrEmpty(m_template))
            {
                return;
            }
            m_text.spriteAsset = m_icons;
            m_text.richText = true;
            m_text.raycastTarget = false;
            m_text.text = s_tokenPattern.Replace(m_template, match => ResolveToken(match.Groups[1].Value));
        }

        private void HandleActionChange(object source, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
            {
                Refresh();
            }
        }

        private string ResolveToken(string token)
        {
            bool isGamepad = InputHintDevice.IsGamepad;
            string path = null;
            var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            InputAction action = token == "Submit" ? module?.submit?.action :
                token == "Cancel" ? module?.cancel?.action : m_actions?.FindAction(token, false);
            if (action != null)
            {
                foreach (InputBinding binding in action.bindings)
                {
                    if (!binding.isComposite && !binding.isPartOfComposite &&
                        !string.IsNullOrEmpty(binding.effectivePath) &&
                        (isGamepad ? binding.effectivePath.StartsWith("<Gamepad>") :
                            binding.effectivePath.StartsWith("<Keyboard>") ||
                            binding.effectivePath.StartsWith("<Mouse>")))
                    {
                        path = binding.effectivePath;
                        break;
                    }
                }
            }
            path ??= token switch
            {
                "Submit" => isGamepad ? "<Gamepad>/buttonSouth" : "<Keyboard>/enter",
                "Cancel" => isGamepad ? "<Gamepad>/buttonEast" : "<Keyboard>/escape",
                "Any" => isGamepad ? "<Gamepad>/buttonSouth" : "<Keyboard>/any",
                _ => null
            };
            string icon = GetIconName(path);
            return icon != null && m_icons.GetSpriteIndexFromName(icon) >= 0
                ? $"<size=180%><sprite name=\"{icon}\" tint=0></size>" : "?";
        }

        /// <summary>Maps Input System control paths to names in the authored icon collection.</summary>
        public static string GetIconName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            if (path.StartsWith("<Keyboard>/"))
            {
                return ("keyboard_" + path.Substring(11).ToLowerInvariant()) switch
                {
                    "keyboard_leftshift" or "keyboard_rightshift" => "keyboard_shift",
                    "keyboard_leftctrl" or "keyboard_rightctrl" => "keyboard_ctrl",
                    "keyboard_uparrow" => "keyboard_arrow_up",
                    "keyboard_downarrow" => "keyboard_arrow_down",
                    "keyboard_leftarrow" => "keyboard_arrow_left",
                    "keyboard_rightarrow" => "keyboard_arrow_right",
                    var name => name
                };
            }
            return path switch
            {
                "<Gamepad>/buttonSouth" => "xbox_button_a",
                "<Gamepad>/buttonEast" => "xbox_button_b",
                "<Gamepad>/buttonWest" => "xbox_button_x",
                "<Gamepad>/buttonNorth" => "xbox_button_y",
                "<Gamepad>/start" => "xbox_button_menu",
                "<Gamepad>/leftShoulder" => "xbox_lb",
                "<Gamepad>/rightShoulder" => "xbox_rb",
                "<Gamepad>/leftTrigger" => "xbox_lt",
                "<Gamepad>/rightTrigger" => "xbox_rt",
                "<Gamepad>/leftStickPress" => "xbox_ls",
                "<Gamepad>/rightStickPress" => "xbox_rs",
                "<Mouse>/leftButton" => "mouse_left",
                "<Mouse>/rightButton" => "mouse_right",
                _ => null
            };
        }
    }
}
