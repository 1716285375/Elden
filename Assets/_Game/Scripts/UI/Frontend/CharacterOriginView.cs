using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Owns origin selection and body-type confirmation before appearance editing.</summary>
    public sealed class CharacterOriginView : MonoBehaviour
    {
        private UIDocument m_document;
        private PanelSettings m_panel;
        private TitleScreenCharacterCreationManager m_owner;
        private CharacterClass[] m_classes;
        private VisualElement m_root;
        private int m_selected;
        private bool m_selectingBody;
        private bool m_isShowingStats;
        private InputAction m_statusAction;

        public static CharacterOriginView Create(Transform parent, TitleScreenCharacterCreationManager owner)
        {
            var root = new GameObject("Character Origin Toolkit");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.sortingOrder = 710;
            var document = root.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/CharacterOrigin");
            document.gameObject.AddComponent<MenuAudioFeedback>();
            var view = root.AddComponent<CharacterOriginView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            view.m_statusAction = new InputAction("Origin status", InputActionType.Button);
            view.m_statusAction.AddBinding("<Keyboard>/f");
            view.m_statusAction.AddBinding("<Gamepad>/buttonNorth");
            view.m_statusAction.performed += view.OnStatusPerformed;
            return view;
        }

        public void Show(CharacterClass[] classes, int selected)
        {
            gameObject.SetActive(true);
            m_root = m_document.rootVisualElement;
            m_classes = classes;
            m_selected = Mathf.Clamp(selected, 0, classes.Length - 1);
            m_selectingBody = false;
            m_isShowingStats = false;
            m_statusAction.Enable();
            m_root.style.display = DisplayStyle.Flex;
            m_root.Q<Button>("origin-back").clicked -= Back;
            m_root.Q<Button>("origin-back").clicked += Back;
            m_root.Q<Button>("origin-stats").clicked -= ToggleStats;
            m_root.Q<Button>("origin-stats").clicked += ToggleStats;
            m_root.UnregisterCallback<NavigationMoveEvent>(OnMove);
            m_root.UnregisterCallback<NavigationCancelEvent>(OnCancel);
            m_root.RegisterCallback<NavigationMoveEvent>(OnMove);
            m_root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            Rebuild();
        }

        public void Hide()
        {
            m_statusAction?.Disable();
            if (m_root != null)
            {
                m_root.style.display = DisplayStyle.None;
            }
        }

        private void Rebuild()
        {
            m_root.Q<Label>("origin-heading").text = m_selectingBody ? "选择体型" : "选择角色出身";
            m_root.Q<Label>("origin-help").text = m_selectingBody ? "选择角色的体型。" : "选择创建角色时的出身。";
            VisualElement row = m_root.Q("origin-cards");
            row.Clear();
            if (m_selectingBody)
            {
                for (int body = 0; body < 2; body++)
                {
                    bool male = body == 0;
                    var button = new Button(() => m_owner.ConfirmOrigin(m_selected, male));
                    button.AddToClassList("origin-card");
                    button.AddToClassList("origin-card--body");
                    button.Add(Portrait(m_classes[m_selected], male));
                    button.Add(new Label(male ? "体型 A（男性）" : "体型 B（女性）"));
                    row.Add(button);
                }
            }
            else
            {
                int start = Mathf.Clamp(m_selected - 1, 0, Mathf.Max(0, m_classes.Length - 3));
                for (int index = start; index < Mathf.Min(start + 3, m_classes.Length); index++)
                {
                    int selected = index;
                    var button = new Button(() => { m_selected = selected; m_selectingBody = true; Rebuild(); })
                    {
                        name = "origin-" + index
                    };
                    button.AddToClassList("origin-card");
                    button.EnableInClassList("origin-card--selected", index == m_selected);
                    var pair = new VisualElement();
                    pair.AddToClassList("origin-pair");
                    pair.Add(Portrait(m_classes[index], true));
                    pair.Add(Portrait(m_classes[index], false));
                    button.Add(pair);
                    var bodyLabels = new VisualElement();
                    bodyLabels.AddToClassList("origin-body-labels");
                    bodyLabels.Add(new Label("A"));
                    bodyLabels.Add(new Label("B"));
                    button.Add(bodyLabels);
                    button.Add(new Label(m_classes[index].ClassName));
                    if (m_isShowingStats)
                    {
                        CharacterClass stats = m_classes[index];
                        var values = new Label($"等级　{stats.Level}\n生命力　{stats.Vitality}\n集中力　{stats.Mind}\n" +
                            $"耐力　{stats.Endurance}\n力气　{stats.Strength}\n灵巧　{stats.Dexterity}\n" +
                            $"智力　{stats.Intelligence}\n信仰　{stats.Faith}\n感应　{stats.Arcane}");
                        values.name = "origin-stats-" + stats.OriginId;
                        values.AddToClassList("origin-card__stats");
                        button.Add(values);
                    }
                    button.RegisterCallback<FocusInEvent>(_ => SelectCard(selected));
                    row.Add(button);
                }
            }
            CharacterClass origin = m_classes[m_selected];
            m_root.Q<Label>("origin-values").text = $"{origin.ClassName}　等级 {origin.Level}\n" +
                $"生命力 {origin.Vitality}　集中力 {origin.Mind}　耐力 {origin.Endurance}　力气 {origin.Strength}\n" +
                $"灵巧 {origin.Dexterity}　智力 {origin.Intelligence}　信仰 {origin.Faith}　感应 {origin.Arcane}";
            m_root.Q("origin-values").style.display = DisplayStyle.None;
            m_root.Q<Button>("origin-stats").text = m_isShowingStats ? "隐藏能力值" : "显示能力值";
            m_root.Q<Button>("origin-stats").style.display = m_selectingBody ? DisplayStyle.None : DisplayStyle.Flex;
            row.Query<Button>().ToList().FirstOrDefault(button => m_selectingBody ||
                button.ClassListContains("origin-card--selected"))?.Focus();
        }

        private VisualElement Portrait(CharacterClass origin, bool male)
        {
            var image = new Image
            {
                image = Resources.Load<Texture2D>("UI/Origins/Origin" + origin.OriginId + (male ? "A" : "B")),
                scaleMode = ScaleMode.ScaleToFit
            };
            image.AddToClassList("origin-portrait");
            return image;
        }

        private void SelectCard(int index)
        {
            m_selected = index;
            m_owner.SelectClass(index);
            foreach (Button card in m_root.Q("origin-cards").Query<Button>().ToList())
            {
                card.EnableInClassList("origin-card--selected", card.name == "origin-" + index);
            }
        }

        private void OnMove(NavigationMoveEvent evt)
        {
            if (m_selectingBody || (evt.direction != NavigationMoveEvent.Direction.Left &&
                evt.direction != NavigationMoveEvent.Direction.Right))
            {
                return;
            }
            m_selected = (m_selected + (evt.direction == NavigationMoveEvent.Direction.Right ? 1 : -1) +
                m_classes.Length) % m_classes.Length;
            evt.StopImmediatePropagation();
            Rebuild();
        }

        private void OnCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            Back();
        }

        private void Back()
        {
            if (m_selectingBody)
            {
                m_selectingBody = false;
                Rebuild();
            }
            else
            {
                m_owner.CancelCharacterCreation();
            }
        }

        private void ToggleStats() { m_isShowingStats = !m_isShowingStats; Rebuild(); }

        private void OnStatusPerformed(InputAction.CallbackContext context)
        {
            if (!m_selectingBody && m_root != null && m_root.style.display.value != DisplayStyle.None)
            {
                ToggleStats();
            }
        }

        private void OnDisable()
        {
            m_statusAction?.Disable();
        }

        private void OnDestroy()
        {
            m_statusAction?.Dispose();
            if (m_panel != null)
            {
                Destroy(m_panel);
            }
        }
    }
}
