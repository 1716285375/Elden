using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Shares a navigable settings panel between the title and character menus.</summary>
    public sealed class GameSettingsPanel : MonoBehaviour
    {
        private Button m_volumeButton;
        private TMP_Text m_volumeLabel;
        private TMP_Text m_lookLabel;
        private TMP_Text m_invertLabel;
        private TMP_Text m_qualityLabel;
        private TMP_Text m_fullscreenLabel;
        private TMP_Text m_frameRateLabel;
        private TMP_Text m_shakeLabel;
        private TMP_Text m_hapticsLabel;
        private TMP_Text m_hitStopLabel;
        private Action m_closed;

        /// <summary>Creates one responsive overlay beneath the owning menu's lifecycle.</summary>
        public static GameSettingsPanel Create(Transform parent)
        {
            GameObject root = new("Settings", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 900;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.GetComponent<Image>().color = new Color(0.035f, 0.04f, 0.035f, 0.98f);
            GameSettingsPanel panel = root.AddComponent<GameSettingsPanel>();
            panel.Build();
            root.SetActive(false);
            return panel;
        }

        /// <summary>Opens settings and places keyboard/controller focus on the first control.</summary>
        public void Open(Action closed)
        {
            m_closed = closed;
            gameObject.SetActive(true);
            RefreshLabels();
            m_volumeButton.Select();
        }

        /// <summary>Closes the overlay and restores the owning menu's selection.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
            Action closed = m_closed;
            m_closed = null;
            closed?.Invoke();
        }

        /// <summary>Creates a styled controller-navigable command.</summary>
        public static Button CreateButton(Transform parent, string label, UnityAction clicked)
        {
            GameObject root = new(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = new Color(0.19f, 0.18f, 0.14f, 1f);
            root.GetComponent<LayoutElement>().preferredHeight = 52f;
            Button button = root.GetComponent<Button>();
            button.onClick.AddListener(clicked);
            ColorBlock colors = button.colors;
            colors.selectedColor = new Color(1f, 0.85f, 0.5f);
            colors.highlightedColor = colors.selectedColor;
            button.colors = colors;
            TMP_Text text = CreateLabel(root.transform, label);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 0f);
            rect.offsetMax = new Vector2(-12f, 0f);
            return button;
        }

        private void Build()
        {
            GameObject content = new("Settings Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            content.transform.SetParent(transform, false);
            RectTransform rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.22f, 0.1f);
            rect.anchorMax = new Vector2(0.78f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            CreateLabel(content.transform, "SETTINGS");
            m_volumeButton = CreateButton(content.transform, "Volume", () =>
            {
                GameSettings.SetVolume(GameSettings.Volume >= 0.99f ? 0f : GameSettings.Volume + 0.1f);
                RefreshLabels();
            });
            m_volumeLabel = m_volumeButton.GetComponentInChildren<TMP_Text>();
            m_lookLabel = AddOption(content.transform, "Camera", () =>
                GameSettings.SetLook(GameSettings.LookSensitivity >= 3f ? 0.25f :
                    GameSettings.LookSensitivity + 0.25f, GameSettings.InvertLook));
            m_invertLabel = AddOption(content.transform, "Invert", () =>
                GameSettings.SetLook(GameSettings.LookSensitivity, !GameSettings.InvertLook));
            m_qualityLabel = AddOption(content.transform, "Quality", () =>
                GameSettings.SetDisplay((GameSettings.Quality + 1) % Mathf.Max(1, QualitySettings.names.Length),
                    GameSettings.Fullscreen, GameSettings.FrameRate));
            m_fullscreenLabel = AddOption(content.transform, "Display", () =>
                GameSettings.SetDisplay(GameSettings.Quality, !GameSettings.Fullscreen, GameSettings.FrameRate));
            m_frameRateLabel = AddOption(content.transform, "Frame Limit", () =>
            {
                int next = GameSettings.FrameRate switch { 30 => 60, 60 => 120, 120 => -1, _ => 30 };
                GameSettings.SetDisplay(GameSettings.Quality, GameSettings.Fullscreen, next);
            });
            m_shakeLabel = AddOption(content.transform, "Camera Shake", () =>
                GameSettings.SetImpact(GameSettings.ShakeIntensity >= 1f ? 0f : GameSettings.ShakeIntensity + 0.25f,
                    GameSettings.HapticsEnabled, GameSettings.HitStopEnabled));
            m_hapticsLabel = AddOption(content.transform, "Vibration", () =>
                GameSettings.SetImpact(GameSettings.ShakeIntensity, !GameSettings.HapticsEnabled,
                    GameSettings.HitStopEnabled));
            m_hitStopLabel = AddOption(content.transform, "Impact Pause", () =>
                GameSettings.SetImpact(GameSettings.ShakeIntensity, GameSettings.HapticsEnabled,
                    !GameSettings.HitStopEnabled));
            CreateLabel(content.transform, "Select to cycle • Changes save automatically");
            CreateButton(content.transform, "Back", Close);
            Button[] buttons = content.GetComponentsInChildren<Button>();
            for (int index = 0; index < buttons.Length; index++)
            {
                buttons[index].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[(index + buttons.Length - 1) % buttons.Length],
                    selectOnDown = buttons[(index + 1) % buttons.Length]
                };
            }
        }

        private TMP_Text AddOption(Transform parent, string label, Action change)
        {
            return CreateButton(parent, label, () =>
            {
                change();
                RefreshLabels();
            }).GetComponentInChildren<TMP_Text>();
        }

        private void RefreshLabels()
        {
            m_volumeLabel.text = $"Master Volume: {Mathf.RoundToInt(GameSettings.Volume * 100f)}%";
            m_lookLabel.text = $"Camera Sensitivity: {GameSettings.LookSensitivity:0.00}x";
            m_invertLabel.text = $"Invert Camera Y: {(GameSettings.InvertLook ? "On" : "Off")}";
            string[] names = QualitySettings.names;
            m_qualityLabel.text = $"Quality: {(names.Length > 0 ? names[GameSettings.Quality] : "Default")}";
            m_fullscreenLabel.text = $"Display: {(GameSettings.Fullscreen ? "Borderless Fullscreen" : "Windowed")}";
            m_frameRateLabel.text = $"Frame Limit: {(GameSettings.FrameRate < 0 ? "Unlimited" : GameSettings.FrameRate.ToString())}";
            m_shakeLabel.text = $"Camera Shake: {Mathf.RoundToInt(GameSettings.ShakeIntensity * 100f)}%";
            m_hapticsLabel.text = $"Controller Vibration: {(GameSettings.HapticsEnabled ? "On" : "Off")}";
            m_hitStopLabel.text = $"Impact Animation Pause: {(GameSettings.HitStopEnabled ? "On" : "Off")}";
        }

        private static TMP_Text CreateLabel(Transform parent, string label)
        {
            GameObject root = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            TMP_Text text = root.GetComponent<TMP_Text>();
            text.text = label;
            text.fontSize = 28f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.94f, 0.9f, 0.78f);
            text.raycastTarget = false;
            root.GetComponent<LayoutElement>().preferredHeight = 50f;
            return text;
        }
    }
}
