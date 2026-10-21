using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using ToolkitButton = UnityEngine.UIElements.Button;
using Slider = UnityEngine.UIElements.Slider;
using Toggle = UnityEngine.UIElements.Toggle;

namespace ZZ
{
    /// <summary>Native Toolkit settings shared by the title and in-game menus.</summary>
    public sealed class GameSettingsPanel : MonoBehaviour
    {
        private static readonly string[] s_categories = { "audio", "controls", "display", "impact" };
        private static readonly int[] s_frameRates = { 30, 60, 120, -1 };
        private static int s_closedFrame = -1;
        private UIDocument m_document;
        private PanelSettings m_panel;
        private VisualElement m_root;
        private Action m_closed;
        private string m_category = "audio";
        private IVisualElementScheduledItem m_pendingCommit;

        /// <summary>Prevents the same cancel event closing the parent after this overlay.</summary>
        public static bool ClosedThisFrame => Application.isPlaying && s_closedFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_closedFrame = -1;
        }

        private void OnEnable()
        {
            if (m_document != null)
            {
                BindDocument();
            }
        }

        private void OnDisable()
        {
            FlushPreferences();
            m_root = null;
            m_closed = null;
        }

        private void OnDestroy()
        {
            if (m_panel != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(m_panel);
                }
                else
                {
                    DestroyImmediate(m_panel);
                }
            }
        }

        /// <summary>Creates a disabled document without a Canvas or legacy controls.</summary>
        public static GameSettingsPanel Create(Transform parent)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/SettingsPanel");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Settings Toolkit layout or theme is missing.");
            }
            GameObject instance = new GameObject("Settings");
            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 900;
            UIDocument document = instance.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = layout;
            document.gameObject.AddComponent<MenuAudioFeedback>();
            GameSettingsPanel panel = instance.AddComponent<GameSettingsPanel>();
            panel.m_document = document;
            panel.m_panel = settings;
            return panel;
        }

        /// <summary>Opens the current category and gives Toolkit exclusive selection.</summary>
        public void Open(Action closed)
        {
            m_closed = closed;
            if (!gameObject.activeSelf)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                gameObject.SetActive(true);
            }
        }

        /// <summary>Closes once and returns selection to the owner's callback.</summary>
        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }
            Action closed = m_closed;
            s_closedFrame = Time.frameCount;
            gameObject.SetActive(false);
            closed?.Invoke();
        }

        private void BindDocument()
        {
            m_root = m_document.rootVisualElement;
            foreach (string category in s_categories)
            {
                string selectedCategory = category;
                m_root.Q<ToolkitButton>("tab-" + category).clicked += () => SelectCategory(selectedCategory);
            }
            m_root.Q<ToolkitButton>("back").clicked += Close;
            m_root.RegisterCallback<NavigationCancelEvent>(evt =>
            {
                evt.StopPropagation();
                Close();
            });
            m_root.RegisterCallback<GeometryChangedEvent>(evt => m_root?.Q(className: "settings-panel")
                .EnableInClassList("settings-panel--compact", evt.newRect.width < 1200f));
            BindPreferences();
            BindRowFeedback();
            RefreshValues();
            SelectCategory(m_category);
            VisualElement root = m_root;
            root.schedule.Execute(() =>
            {
                if (m_root == root && gameObject.activeInHierarchy)
                {
                    root.Q<ToolkitButton>("tab-" + m_category).Focus();
                }
            });
        }

        private void BindPreferences()
        {
            m_root.Q<Slider>("volume").RegisterValueChangedCallback(evt =>
                Preview(() => GameSettings.PreviewVolume(evt.newValue / 100f)));
            m_root.Q<Slider>("look").RegisterValueChangedCallback(evt =>
                Preview(() => GameSettings.PreviewLook(evt.newValue, GameSettings.InvertLook)));
            m_root.Q<Toggle>("invert").RegisterValueChangedCallback(evt =>
                Change(() => GameSettings.SetLook(GameSettings.LookSensitivity, evt.newValue)));
            DropdownField quality = m_root.Q<DropdownField>("quality");
            quality.choices = new List<string>(QualitySettings.names);
            if (quality.choices.Count == 0)
            {
                quality.choices.Add("默认");
                quality.SetEnabled(false);
            }
            quality.RegisterValueChangedCallback(_ => Change(() => GameSettings.SetDisplay(
                quality.index, GameSettings.Fullscreen, GameSettings.FrameRate)));
            DropdownField display = m_root.Q<DropdownField>("display");
            display.choices = new List<string> { "窗口", "无边框全屏" };
            display.RegisterValueChangedCallback(_ => Change(() => GameSettings.SetDisplay(
                GameSettings.Quality, display.index == 1, GameSettings.FrameRate)));
            DropdownField frameRate = m_root.Q<DropdownField>("frame-rate");
            frameRate.choices = new List<string> { "30 FPS", "60 FPS", "120 FPS", "不限帧率" };
            frameRate.RegisterValueChangedCallback(_ => Change(() => GameSettings.SetDisplay(
                GameSettings.Quality, GameSettings.Fullscreen, s_frameRates[frameRate.index])));
            m_root.Q<Slider>("shake").RegisterValueChangedCallback(evt => Preview(() => GameSettings.PreviewImpact(
                evt.newValue / 100f, GameSettings.HapticsEnabled, GameSettings.HitStopEnabled)));
            m_root.Q<Toggle>("haptics").RegisterValueChangedCallback(evt => Change(() => GameSettings.SetImpact(
                GameSettings.ShakeIntensity, evt.newValue, GameSettings.HitStopEnabled)));
            m_root.Q<Toggle>("hit-stop").RegisterValueChangedCallback(evt => Change(() => GameSettings.SetImpact(
                GameSettings.ShakeIntensity, GameSettings.HapticsEnabled, evt.newValue)));
        }

        private void SelectCategory(string category)
        {
            m_category = category;
            foreach (string candidate in s_categories)
            {
                bool isSelected = candidate == category;
                m_root.Q("page-" + candidate).style.display = isSelected ? DisplayStyle.Flex : DisplayStyle.None;
                m_root.Q<ToolkitButton>("tab-" + candidate)
                    .EnableInClassList("menu-tab--selected", isSelected);
            }
            m_root.Q<ScrollView>("settings-scroll").scrollOffset = Vector2.zero;
            m_root.Q<Label>("category-heading").text = m_root.Q<ToolkitButton>("tab-" + category).tooltip;
            m_root.Q<Label>("settings-help").text = "选择项目进行调整。";
        }

        private void BindRowFeedback()
        {
            m_root.Query<VisualElement>(className: "settings-row").ForEach(row =>
            {
                row.RegisterCallback<FocusInEvent>(_ => SetRowFeedback(row));
                row.RegisterCallback<FocusOutEvent>(_ => row.RemoveFromClassList("settings-row--focused"));
                row.RegisterCallback<PointerEnterEvent>(_ => SetRowFeedback(row));
                row.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (row.panel?.focusController.focusedElement is not VisualElement focused || !row.Contains(focused))
                    {
                        row.RemoveFromClassList("settings-row--focused");
                    }
                });
            });
        }

        private void SetRowFeedback(VisualElement row)
        {
            row.AddToClassList("settings-row--focused");
            m_root.Q<Label>("settings-help").text = row.Q<Label>(className: "menu-description").text;
        }

        private void Change(Action change)
        {
            m_pendingCommit?.Pause();
            m_pendingCommit = null;
            change();
            RefreshValues();
            m_root.Q<Label>("save-status").text = "设置已保存";
        }

        private void Preview(Action preview)
        {
            preview();
            RefreshValues();
            m_root.Q<Label>("save-status").text = "调整中…";
            m_pendingCommit?.Pause();
            m_pendingCommit = m_root.schedule.Execute(() =>
            {
                FlushPreferences();
                m_root.Q<Label>("save-status").text = "设置已保存";
            }).StartingIn(200);
        }

        private void FlushPreferences()
        {
            if (m_pendingCommit == null)
            {
                return;
            }
            m_pendingCommit.Pause();
            m_pendingCommit = null;
            GameSettings.CommitPreferences();
        }

        private void RefreshValues()
        {
            m_root.Q<Slider>("volume").SetValueWithoutNotify(GameSettings.Volume * 100f);
            m_root.Q<Label>("volume-value").text = $"{Mathf.RoundToInt(GameSettings.Volume * 100f)}%";
            m_root.Q<Slider>("look").SetValueWithoutNotify(GameSettings.LookSensitivity);
            m_root.Q<Label>("look-value").text = $"{GameSettings.LookSensitivity:0.00}×";
            RefreshToggle("invert", GameSettings.InvertLook);
            DropdownField quality = m_root.Q<DropdownField>("quality");
            quality.SetValueWithoutNotify(quality.choices[Mathf.Clamp(GameSettings.Quality, 0,
                quality.choices.Count - 1)]);
            DropdownField display = m_root.Q<DropdownField>("display");
            display.SetValueWithoutNotify(display.choices[GameSettings.Fullscreen ? 1 : 0]);
            DropdownField frameRate = m_root.Q<DropdownField>("frame-rate");
            int frameIndex = Mathf.Max(0, Array.IndexOf(s_frameRates, GameSettings.FrameRate));
            frameRate.SetValueWithoutNotify(frameRate.choices[frameIndex]);
            m_root.Q<Slider>("shake").SetValueWithoutNotify(GameSettings.ShakeIntensity * 100f);
            m_root.Q<Label>("shake-value").text = $"{Mathf.RoundToInt(GameSettings.ShakeIntensity * 100f)}%";
            RefreshToggle("haptics", GameSettings.HapticsEnabled);
            RefreshToggle("hit-stop", GameSettings.HitStopEnabled);
        }

        private void RefreshToggle(string name, bool value)
        {
            Toggle toggle = m_root.Q<Toggle>(name);
            toggle.SetValueWithoutNotify(value);
            toggle.text = value ? "开启" : "关闭";
        }

    }
}
