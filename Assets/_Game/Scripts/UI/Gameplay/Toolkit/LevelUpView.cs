using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Native attribute planning and explicit currency confirmation.</summary>
    public sealed class LevelUpView : MonoBehaviour
    {
        private static readonly string[] s_names = { "生命力", "集中力", "耐力", "力气", "灵巧", "智力", "信仰", "感应" };
        private UIDocument m_document;
        private PanelSettings m_panel;
        private PlayerUILevelUpManager m_owner;
        private VisualElement m_root;
        private readonly SliderInt[] m_sliders = new SliderInt[8];
        private bool m_isRendering;

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            VisualTreeAsset row = Resources.Load<VisualTreeAsset>("UI/LevelUpAttributeRow");
            for (int index = 0; index < s_names.Length; index++)
            {
                int attribute = index;
                VisualElement element = row.CloneTree();
                element.name = $"attribute-{index}";
                element.Q<Label>("attribute-name").text = s_names[index];
                SliderInt slider = element.Q<SliderInt>("attribute-slider");
                m_sliders[index] = slider;
                slider.RegisterValueChangedCallback(evt =>
                {
                    if (!m_isRendering)
                    {
                        m_owner.SetProjectedAttribute((CharacterAttribute)attribute, evt.newValue);
                    }
                });
                element.Q<Button>("decrease").clicked += () => slider.value--;
                element.Q<Button>("increase").clicked += () => slider.value++;
                m_root.Q("attributes").Add(element);
            }
            Bind("reset", m_owner.InitializeLevelUpMenu);
            Bind("review", m_owner.RequestConfirmation);
            Bind("confirm", m_owner.ConfirmLevels);
            Bind("cancel", m_owner.CancelConfirmation);
            Bind("back", m_owner.ReturnToSiteOfGrace);
            HideConfirmation();
        }

        private void OnDisable()
        {
            m_root = null;
            Array.Clear(m_sliders, 0, m_sliders.Length);
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

        /// <summary>Creates a document inside the existing menu lifecycle root.</summary>
        public static LevelUpView Create(Transform parent, PlayerUILevelUpManager owner)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/LevelUp");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Level up Toolkit assets are missing.");
            }
            GameObject instance = new GameObject("Level Up Toolkit");
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
            LevelUpView view = instance.AddComponent<LevelUpView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            instance.SetActive(true);
            return view;
        }

        /// <summary>Renders a model snapshot without emitting additional attribute changes.</summary>
        public void Refresh(int[] current, int[] projected, int currentLevel, int projectedLevel,
            int runes, int cost, bool canAfford)
        {
            if (m_root == null)
            {
                return;
            }
            m_isRendering = true;
            for (int index = 0; index < s_names.Length; index++)
            {
                SliderInt slider = m_sliders[index];
                slider.lowValue = current[index];
                slider.highValue = Mathf.Max(99, current[index]);
                slider.SetValueWithoutNotify(projected[index]);
                VisualElement row = m_root.Q($"attribute-{index}");
                row.Q<Label>("attribute-value").text = $"{current[index]} → {projected[index]}";
                row.EnableInClassList("level-attribute--changed", projected[index] > current[index]);
                row.Q<Button>("decrease").SetEnabled(projected[index] > current[index]);
                row.Q<Button>("increase").SetEnabled(projected[index] < slider.highValue);
            }
            m_isRendering = false;
            m_root.Q<Label>("level-preview").text = $"{currentLevel} → {projectedLevel}";
            m_root.Q<Label>("runes-held").text = $"{runes:N0}";
            m_root.Q<Label>("runes-cost").text = cost == int.MaxValue ? "达到等级上限" : $"{cost:N0}";
            m_root.Q<Label>("runes-remaining").text = cost == int.MaxValue ? "—" : $"{(long)runes - cost:N0}";
            m_root.Q("runes-cost").EnableInClassList("level-menu__value--invalid", !canAfford);
            m_root.Q<Button>("review").SetEnabled(canAfford && projectedLevel > currentLevel);
            m_root.Q<Button>("reset").SetEnabled(projectedLevel > currentLevel);
            SetFeedback(cost == int.MaxValue ? "本次方案超过角色等级上限。" : !canAfford ?
                "卢恩不足。可以减少加点，或重置本次方案。" : "确认前不会消耗卢恩；返回赐福会放弃本次预览。");
        }

        /// <summary>Shows the total cost and resulting level, with Cancel initially focused.</summary>
        public void ShowConfirmation(int cost, int level)
        {
            m_root.Q("level-content").SetEnabled(false);
            m_root.Q("level-confirmation").style.display = DisplayStyle.Flex;
            m_root.Q<Label>("confirmation-description").text =
                $"消耗 {cost:N0} 卢恩，将角色提升至 {level} 级。\n确认后属性立即生效，本次加点无法撤销。";
            Focus(m_root.Q<Button>("cancel"));
        }

        /// <summary>Dismisses the modal without changing the attribute plan.</summary>
        public void HideConfirmation()
        {
            if (m_root == null)
            {
                return;
            }
            bool wasOpen = m_root.Q("level-confirmation").style.display.value == DisplayStyle.Flex;
            m_root.Q("level-content").SetEnabled(true);
            m_root.Q("level-confirmation").style.display = DisplayStyle.None;
            if (wasOpen)
            {
                Focus(m_root.Q<Button>("review"));
            }
        }

        /// <summary>Focuses the first attribute after opening or resetting.</summary>
        public void FocusFirstAttribute()
        {
            Focus(m_sliders[0]);
        }

        /// <summary>Shows a validation or completion result.</summary>
        public void SetFeedback(string message)
        {
            if (m_root != null)
            {
                m_root.Q<Label>("level-feedback").text = message;
            }
        }

        private void Bind(string name, Action action)
        {
            m_root.Q<Button>(name).clicked += action;
        }

        private void Focus(VisualElement element)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            VisualElement root = m_root;
            root?.schedule.Execute(() =>
            {
                if (m_root == root && isActiveAndEnabled)
                {
                    element?.Focus();
                }
            });
        }
    }
}
