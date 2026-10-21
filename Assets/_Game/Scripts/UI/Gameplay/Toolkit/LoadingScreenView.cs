using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Topmost native transition cover with an indeterminate, unscaled activity indicator.</summary>
    public sealed class LoadingScreenView : MonoBehaviour
    {
        private static readonly string[] s_art = { "MENU_Load_00014", "MENU_Load_00000", "MENU_Load_00002" };
        private static readonly string[] s_titles = { "异常状态：出血", "赐福", "战斗：精力" };
        private static readonly string[] s_tips =
        {
            "出血累积量表全满时，会受到与最大生命值相关的大量伤害。\n健壮度越高，出血量表越难累积。",
            "在赐福休息，可以恢复生命值、专注值与圣杯瓶使用次数。\n休息也会让大部分敌人重新出现。",
            "攻击、防御与翻滚都会消耗精力。\n持续举盾会减慢精力恢复；在攻防之间留出恢复的机会。"
        };
        private static int s_artIndex;
        private int m_tipIndex;
        private UIDocument m_document;
        private PanelSettings m_panel;
        private VisualElement m_root;
        private IVisualElementScheduledItem m_animation;
        private VisualElement m_previousFocus;

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement.Q("loading-overlay");
            float highestOrder = float.MinValue;
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (document == m_document || document.panelSettings == null ||
                    document.panelSettings.sortingOrder < highestOrder)
                {
                    continue;
                }
                VisualElement focused = document.rootVisualElement?.panel?.focusController?.focusedElement as VisualElement;
                if (focused != null && focused.enabledInHierarchy)
                {
                    m_previousFocus = focused;
                    highestOrder = document.panelSettings.sortingOrder;
                }
            }
            Texture2D art = Resources.Load<Texture2D>("UI/EldenRing/Loading/" + s_art[s_artIndex++ % s_art.Length]);
            if (art != null)
            {
                m_root.style.backgroundImage = new StyleBackground(art);
            }
            m_tipIndex = 0;
            RefreshTip();
            m_root.Q<Button>("loading-next").clicked += NextTip;
            m_root.RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                NextTip();
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            m_root.RegisterCallback<NavigationCancelEvent>(evt => evt.StopImmediatePropagation());
            m_root.RegisterCallback<NavigationMoveEvent>(evt => evt.StopImmediatePropagation());
            m_root.RegisterCallback<KeyDownEvent>(evt => evt.StopImmediatePropagation());
            VisualElement pulse = m_root.Q("loading-pulse");
            m_animation = m_root.schedule.Execute(() =>
                pulse.style.marginLeft = Length.Percent(Mathf.PingPong(Time.unscaledTime * 45f, 74f))).Every(33);
            SetOpacity(1f);
        }

        private void NextTip()
        {
            m_tipIndex = (m_tipIndex + 1) % s_titles.Length;
            RefreshTip();
        }

        private void RefreshTip()
        {
            m_root.Q<Label>("loading-tip-title").text = s_titles[m_tipIndex];
            m_root.Q<Label>("loading-tip-description").text = s_tips[m_tipIndex];
        }

        private void OnDisable()
        {
            m_animation?.Pause();
            m_animation = null;
            m_root = null;
            if (m_previousFocus?.panel != null && m_previousFocus.enabledInHierarchy)
            {
                m_previousFocus.Focus();
            }
            m_previousFocus = null;
        }

        private void OnDestroy()
        {
            if (m_panel != null)
            {
                Destroy(m_panel);
            }
        }

        /// <summary>Creates an overlay above gameplay menus and settings, retaining the existing loading lifecycle.</summary>
        public static LoadingScreenView Create(Transform parent)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/LoadingScreen");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Loading screen Toolkit assets are missing.");
            }
            GameObject instance = new GameObject("Loading Screen Toolkit");
            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 2000;
            UIDocument document = instance.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;
            LoadingScreenView view = instance.AddComponent<LoadingScreenView>();
            view.m_document = document;
            view.m_panel = panel;
            instance.SetActive(true);
            return view;
        }

        /// <summary>Updates the entire cover opacity using the manager's unscaled fade.</summary>
        public void SetOpacity(float opacity)
        {
            if (m_root != null)
            {
                m_root.style.opacity = Mathf.Clamp01(opacity);
            }
        }

        /// <summary>Removes the previous menu selection and owns navigation until loading ends.</summary>
        public void Focus()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            VisualElement root = m_root;
            root?.Focus();
            root?.schedule.Execute(() =>
            {
                if (m_root == root && isActiveAndEnabled)
                {
                    root.Focus();
                }
            });
        }
    }
}
