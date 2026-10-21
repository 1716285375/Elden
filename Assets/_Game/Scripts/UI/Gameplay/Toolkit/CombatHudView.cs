using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Event-driven Toolkit presentation; gameplay state stays in the existing HUD manager.</summary>
    public sealed class CombatHudView : MonoBehaviour
    {
        private UIDocument m_document;
        private PanelSettings m_panel;
        private VisualElement m_root;
        private VisualElement m_lockOnReticle;
        private HudResourceMeter m_health;
        private HudResourceMeter m_stamina;
        private HudResourceMeter m_focus;
        private string m_prompt;
        private bool m_visible = true;
        private Rect m_safeArea;
        private int m_screenWidth;
        private int m_screenHeight;

        public bool HasPrompt => !string.IsNullOrWhiteSpace(m_prompt);

        /// <summary>Creates a self-contained overlay only when its authored assets are available.</summary>
        public static CombatHudView Create(Transform parent)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/CombatHud");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/CombatTheme");
            if (layout == null || theme == null)
            {
                return null;
            }

            GameObject instance = new GameObject("Combat HUD Toolkit");
            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 20;
            UIDocument document = instance.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;
            CombatHudView view = instance.AddComponent<CombatHudView>();
            view.m_document = document;
            view.m_panel = panel;
            instance.SetActive(true);
            return view;
        }

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            m_lockOnReticle = m_root.Q("lock-on-reticle");
            m_root.pickingMode = PickingMode.Ignore;
            m_root.Query<VisualElement>().ForEach(element =>
            {
                element.pickingMode = PickingMode.Ignore;
                element.focusable = false;
            });
            m_health = new HudResourceMeter(m_root, "health");
            m_stamina = new HudResourceMeter(m_root, "stamina");
            m_focus = new HudResourceMeter(m_root, "focus");
            InputHintDevice.DeviceChanged += RefreshHint;
            InputSystem.onActionChange += OnActionChange;
            SetVisible(m_visible);
            SetPrompt(m_prompt);
            RefreshSafeArea();
            GetComponentInParent<PlayerUIHUDManager>()?.RefreshToolkit();
        }

        private void OnDisable()
        {
            InputHintDevice.DeviceChanged -= RefreshHint;
            InputSystem.onActionChange -= OnActionChange;
            m_health?.CancelTrail();
            m_stamina?.CancelTrail();
            m_focus?.CancelTrail();
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

        private void Update()
        {
            // Screen geometry is the only polled state; character values arrive through existing events.
            if (m_safeArea != Screen.safeArea || m_screenWidth != Screen.width ||
                m_screenHeight != Screen.height)
            {
                RefreshSafeArea();
            }
        }

        public void SetHealth(float current, float maximum)
        {
            m_health?.SetValue(current, maximum);
            RefreshWarning();
        }

        public void SetStamina(float current, float maximum)
        {
            m_stamina?.SetValue(current, maximum);
            RefreshWarning();
        }

        public void SetFocus(float current, float maximum) => m_focus?.SetValue(current, maximum);

        /// <summary>Projects the selected enemy's anchor onto the HUD without intercepting input.</summary>
        public void SetLockOnTarget(Camera camera, Vector3? point)
        {
            if (m_lockOnReticle == null || m_root.panel == null)
            {
                return;
            }
            bool visible = camera != null && point.HasValue &&
                camera.WorldToViewportPoint(point.Value).z > 0f;
            m_lockOnReticle.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                Vector2 position = RuntimePanelUtils.CameraTransformWorldToPanel(m_root.panel, point.Value, camera);
                m_lockOnReticle.style.left = position.x - 12f;
                m_lockOnReticle.style.top = position.y - 12f;
            }
        }

        public void SetPoisoned(bool value)
        {
            m_root?.Q("health")?.EnableInClassList("resource--poisoned", value);
        }

        public void SetCurrency(int value, string change)
        {
            if (m_root == null)
            {
                return;
            }
            m_root.Q<Label>("currency-value").text = Mathf.Max(0, value).ToString("N0");
            m_root.Q<Label>("currency-change").text = change;
        }

        public void SetVisible(bool visible)
        {
            m_visible = visible;
            if (m_root != null)
            {
                m_root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void SetPrompt(string text)
        {
            m_prompt = text;
            if (m_root == null)
            {
                return;
            }
            m_root.Q("interaction").EnableInClassList("interaction--hidden", !HasPrompt);
            Label label = m_root.Q<Label>("interaction-text");
            label.enableRichText = false;
            label.text = text ?? string.Empty;
            RefreshHint();
        }

        private void RefreshHint()
        {
            if (m_root == null)
            {
                return;
            }
            InputAction action = Resources.Load<InputHintCatalog>("InputHints/Catalog")
                ?.Actions?.FindAction("Interact", false);
            string hint = "—";
            if (action != null)
            {
                for (int index = 0; index < action.bindings.Count; index++)
                {
                    InputBinding binding = action.bindings[index];
                    string path = binding.effectivePath;
                    if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(path))
                    {
                        continue;
                    }
                    bool matches = InputHintDevice.IsGamepad ? path.StartsWith("<Gamepad>") :
                        path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>");
                    if (matches)
                    {
                        hint = action.GetBindingDisplayString(index);
                        break;
                    }
                }
            }
            m_root.Q<Label>("interaction-key").text = hint;
        }

        private void OnActionChange(object source, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
            {
                RefreshHint();
            }
        }

        private void RefreshWarning()
        {
            if (m_root == null)
            {
                return;
            }
            m_root.Q<Label>("resource-warning").text = m_health?.IsLow == true ? "生命垂危" :
                m_stamina?.IsLow == true ? "精力不足 · 留意恢复时机" : string.Empty;
        }

        private void RefreshSafeArea()
        {
            m_safeArea = Screen.safeArea;
            m_screenWidth = Mathf.Max(1, Screen.width);
            m_screenHeight = Mathf.Max(1, Screen.height);
            VisualElement hud = m_root?.Q("combat-hud");
            if (hud == null)
            {
                return;
            }
            hud.style.left = Length.Percent(m_safeArea.xMin / m_screenWidth * 100f);
            hud.style.right = Length.Percent((m_screenWidth - m_safeArea.xMax) / m_screenWidth * 100f);
            hud.style.top = Length.Percent((m_screenHeight - m_safeArea.yMax) / m_screenHeight * 100f);
            hud.style.bottom = Length.Percent(m_safeArea.yMin / m_screenHeight * 100f);
        }
    }
}
