using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Toolkit presentation for the creation draft; contains no save or network writes.</summary>
    public sealed class CharacterCreationView : MonoBehaviour
    {
        private UIDocument m_document;
        private PanelSettings m_panel;
        private TitleScreenCharacterCreationManager m_owner;
        private VisualElement m_root;
        private CharacterCreationPreview m_preview;
        private readonly Dictionary<string, VisualElement> m_sections = new();
        private readonly Dictionary<int, SliderInt> m_sliders = new();
        private string m_name = "";
        private bool m_isDragging;
        private bool m_fullBody;
        private Vector2 m_lastPointer;
        private string m_section = "基本资料";

        public string CharacterName => m_root?.Q<TextField>("character-name")?.value ?? m_name;

        public static CharacterCreationView Create(Transform parent, TitleScreenCharacterCreationManager owner)
        {
            var instance = new GameObject("Character Creation Toolkit");
            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 700;
            var document = instance.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/CharacterCreation");
            document.gameObject.AddComponent<MenuAudioFeedback>();
            var view = instance.AddComponent<CharacterCreationView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            return view;
        }

        private void OnEnable()
        {
            m_root = m_document.rootVisualElement;
            m_root.Q<Button>("create").clicked += ShowConfirmation;
            m_root.Q<Button>("confirm-back").clicked += HideConfirmation;
            m_root.Q<Button>("confirm-create").clicked += m_owner.StartGame;
            m_root.Q<Button>("cancel").clicked += NavigateBack;
            m_root.Q<Button>("reset-face").clicked += m_owner.ResetFace;
            m_root.Q<Button>("random-face").clicked += m_owner.RandomizeFace;
            m_root.Q<Button>("preview-mode").clicked += TogglePreviewMode;
            m_root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            VisualElement preview = m_root.Q("character-preview");
            preview.RegisterCallback<PointerDownEvent>(OnPointerDown);
            preview.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            preview.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnDisable()
        {
            m_isDragging = false;
            m_name = CharacterName;
            m_root = null;
            m_sections.Clear();
            m_sliders.Clear();
        }

        private void OnDestroy()
        {
            if (m_panel != null)
            {
                Destroy(m_panel);
            }
        }

        public void Bind(CharacterCreationPreview preview, CharacterClass[] classes, CharacterAppearanceData data)
        {
            m_preview = preview;
            m_root.Q("character-preview").style.backgroundImage = Background.FromRenderTexture(preview.Texture);
            var name = new TextField("名称") { name = "character-name", value = "", maxLength = 60 };
            name.AddToClassList("creation-field");
            name.RegisterValueChangedCallback(evt => m_name = evt.newValue);
            VisualElement basics = Section("基本资料");
            basics.AddToClassList("creation-basics");
            basics.Add(name);
            var classField = new DropdownField("出身", classes.Select(item => item.ClassName == "Knight" ?
                "骑士" : item.ClassName == "Ranger" ? "游侠" : item.ClassName).ToList(), 0);
            classField.AddToClassList("menu-dropdown");
            classField.RegisterValueChangedCallback(_ => m_owner.SelectClass(classField.index));
            basics.Add(classField);
            var bodyType = new DropdownField("体型", new List<string> { "体型 A", "体型 B" }, 0);
            bodyType.name = "body-type";
            bodyType.AddToClassList("menu-dropdown");
            bodyType.RegisterValueChangedCallback(_ => m_owner.SelectBodyType(bodyType.index == 0));
            basics.Add(bodyType);
            classField.SetEnabled(false);
            var origin = new Button(m_owner.OpenOriginSelection) { text = "重新选择出身" };
            origin.AddToClassList("creation-directory-row");
            basics.Add(origin);
            for (int index = 0; index < preview.Definition.Count; index++)
            {
                FaceMorphDefinition.Control control = preview.Definition.GetControl(index);
                int shapeIndex = index;
                var slider = new SliderInt(control.Label, 0, 255) { name = "face-" + index, showInputField = true };
                slider.AddToClassList("creation-slider");
                slider.AddToClassList("creation-face-slider");
                slider.AddToClassList("menu-slider");
                slider.RegisterValueChangedCallback(evt => m_owner.SetMorph(shapeIndex,
                    (evt.newValue - 128f) / (evt.newValue < 128 ? 128f : 127f)));
                Section(control.Group).Add(slider);
                m_sliders.Add(index, slider);
            }
            AddDirectoryRow(basics, "详细外貌", "详细外貌");
            VisualElement appearance = Section("发型与肤色");
            var hairstyle = new DropdownField("发型", Enumerable.Range(0, 9)
                .Select(index => index == 0 ? "无发" : "发型 " + index).ToList(), data.Hairstyle);
            hairstyle.name = "hairstyle";
            hairstyle.AddToClassList("menu-dropdown");
            hairstyle.RegisterValueChangedCallback(_ => m_owner.SelectHairstyle(hairstyle.index));
            appearance.Add(hairstyle);
            var hairGrid = new VisualElement { name = "hairstyle-grid" };
            hairGrid.AddToClassList("creation-style-grid");
            var hairButtons = new List<Button>();
            for (int index = 0; index < 9; index++)
            {
                int style = index;
                var button = new Button(() => hairstyle.index = style)
                    { text = index.ToString(), tooltip = index == 0 ? "无发" : "发型 " + index };
                button.AddToClassList("creation-style");
                button.EnableInClassList("creation-style--selected", index == data.Hairstyle);
                Texture2D thumbnail = Resources.Load<Texture2D>("UI/Hairstyles/Hair" + index.ToString("D2"));
                if (thumbnail != null) { button.style.backgroundImage = Background.FromTexture2D(thumbnail); }
                hairButtons.Add(button);
                hairGrid.Add(button);
            }
            hairstyle.RegisterValueChangedCallback(_ =>
            {
                for (int index = 0; index < hairButtons.Count; index++)
                {
                    hairButtons[index].EnableInClassList("creation-style--selected", index == hairstyle.index);
                }
            });
            appearance.Add(hairGrid);
            var eyebrow = new DropdownField("眉毛", new List<string> { "无", "眉型 1", "眉型 2", "眉型 3", "眉型 4" }, data.Eyebrow);
            eyebrow.name = "eyebrow";
            eyebrow.AddToClassList("menu-dropdown");
            eyebrow.RegisterValueChangedCallback(_ => m_owner.SetEyebrow(eyebrow.index));
            Section("眉毛").Add(eyebrow);
            var beard = new DropdownField("胡须", new List<string> { "无", "胡须 1", "胡须 2", "胡须 3", "胡须 4" }, data.Beard);
            beard.AddToClassList("menu-dropdown");
            beard.RegisterValueChangedCallback(_ => m_owner.SetBeard(beard.index));
            Section("胡须").Add(beard);
            VisualElement eyes = Section("眼睛");
            var irisSize = new Slider("虹膜大小", 0.7f, 1.3f) { name = "iris-size", value = data.IrisSize, showInputField = true };
            irisSize.AddToClassList("creation-slider");
            irisSize.RegisterValueChangedCallback(evt => m_owner.SetIrisSize(evt.newValue));
            eyes.Add(irisSize);
            eyes.Add(new Label("虹膜颜色"));
            var eyeSwatches = new VisualElement();
            eyeSwatches.AddToClassList("creation-swatches");
            foreach (Color32 color in new Color32[] { new(91,69,42,255), new(48,75,85,255),
                new(60,81,49,255), new(125,100,55,255), new(83,80,76,255), new(37,29,24,255) })
            {
                Color32 selected = color;
                var swatch = new Button(() => m_owner.SetEyeColor(selected));
                swatch.AddToClassList("creation-swatch");
                swatch.style.backgroundColor = (Color)selected;
                swatch.tooltip = $"虹膜颜色 {selected.r}, {selected.g}, {selected.b}";
                eyeSwatches.Add(swatch);
            }
            eyes.Add(eyeSwatches);
            VisualElement skin = Section("肤色");
            skin.Add(new Label("预设颜色") { enableRichText = false });
            VisualElement skinSwatches = new VisualElement();
            skinSwatches.AddToClassList("creation-swatches");
            var skinChannels = new SliderInt[3];
            Color32[] paletteColumns = { new(238,195,167,255), new(235,178,160,255), new(215,174,141,255),
                new(197,152,118,255), new(165,121,91,255), new(134,101,82,255), new(179,165,138,255),
                new(172,151,134,255), new(146,160,155,255), new(135,147,163,255), new(170,150,165,255),
                new(255,255,255,255) };
            for (int row = 0; row < 8; row++)
            {
                var paletteRow = new VisualElement();
                paletteRow.AddToClassList("creation-palette-row");
                skinSwatches.Add(paletteRow);
                for (int column = 0; column < paletteColumns.Length; column++)
                {
                    Color baseColor = paletteColumns[column];
                    Color32 selected = row < 2 ? Color.Lerp(baseColor, Color.white, (2f - row) * 0.2f) :
                        Color.Lerp(baseColor, Color.black, (row - 2f) / 6f);
                    var swatch = new Button(() =>
                    {
                        m_owner.SetSkinColor(selected);
                        skinChannels[0].SetValueWithoutNotify(selected.r);
                        skinChannels[1].SetValueWithoutNotify(selected.g);
                        skinChannels[2].SetValueWithoutNotify(selected.b);
                    });
                    swatch.AddToClassList("creation-swatch");
                    swatch.style.backgroundColor = (Color)selected;
                    swatch.tooltip = $"肤色 {selected.r}, {selected.g}, {selected.b}";
                    paletteRow.Add(swatch);
                }
            }
            skin.Add(skinSwatches);
            for (int index = 0; index < skinChannels.Length; index++)
            {
                int channel = index;
                Color32 initial = data.SkinColor;
                var slider = new SliderInt(new[] { "红", "绿", "蓝" }[index], 0, 255)
                { showInputField = true, value = index == 0 ? initial.r : index == 1 ? initial.g : initial.b };
                slider.AddToClassList("creation-slider");
                slider.RegisterValueChangedCallback(evt =>
                {
                    Color32 color = m_owner.Draft.SkinColor;
                    if (channel == 0) { color.r = (byte)evt.newValue; }
                    else if (channel == 1) { color.g = (byte)evt.newValue; }
                    else { color.b = (byte)evt.newValue; }
                    m_owner.SetSkinColor(color);
                });
                skinChannels[index] = slider;
                skin.Add(slider);
            }
            Color32 hair = data.HairColor;
            foreach (string channel in new[] { "红", "绿", "蓝" })
            {
                string selected = channel;
                var slider = new SliderInt("发色 · " + channel, 0, 255) { showInputField = true,
                    value = channel == "红" ? hair.r : channel == "绿" ? hair.g : hair.b };
                slider.AddToClassList("creation-slider");
                slider.RegisterValueChangedCallback(evt =>
                {
                    if (selected == "红") { hair.r = (byte)evt.newValue; }
                    else if (selected == "绿") { hair.g = (byte)evt.newValue; }
                    else { hair.b = (byte)evt.newValue; }
                    m_owner.SelectHairColor(hair);
                });
                appearance.Add(slider);
            }
            VisualElement details = Section("详细外貌");
            AddDirectoryRow(details, "改变肤色", "肤色");
            AddDirectoryRow(details, "调整面部结构", "面部结构");
            AddDirectoryRow(details, "头发", "发型与肤色");
            AddDirectoryRow(details, "眉毛", "眉毛");
            AddDirectoryRow(details, "胡须", "胡须");
            AddDirectoryRow(details, "眼睛", "眼睛");
            VisualElement structure = Section("面部结构");
            foreach (string group in new[] { "脸部轮廓", "眼与眉", "鼻部", "口部", "下颌", "不对称" })
            {
                AddDirectoryRow(structure, group, group);
            }
            foreach (VisualElement row in m_root.Query<VisualElement>().ToList().Where(element =>
                element.ClassListContains("creation-slider") || element.ClassListContains("creation-field") ||
                element.ClassListContains("menu-dropdown")))
            {
                row.RegisterCallback<FocusInEvent>(_ => row.AddToClassList("creation-row--focused"));
                row.RegisterCallback<FocusOutEvent>(_ => row.RemoveFromClassList("creation-row--focused"));
            }
            m_fullBody = true;
            ShowSection("基本资料");
            RefreshMorphValues(data);
            name.Focus();
        }

        private void AddDirectoryRow(VisualElement parent, string label, string section)
        {
            var button = new Button(() => ShowSection(section)) { text = label + "　›" };
            button.AddToClassList("menu-command");
            button.AddToClassList("creation-directory-row");
            button.name = "section-" + section;
            parent.Add(button);
        }

        public void SetBusy(bool busy, string message)
        {
            m_root?.Q("creation-content")?.SetEnabled(!busy);
            m_root?.Q<Button>("create")?.SetEnabled(!busy);
            m_root?.Q<Button>("confirm-create")?.SetEnabled(!busy);
            if (m_root != null)
            {
                m_root.Q<Label>("creation-status").text = message;
            }
        }

        public void SetClassDetails(CharacterClass data)
        {
            Label label = m_root?.Q<Label>("class-details");
            if (label != null)
            {
                label.text = $"等级　　{data.Level}\n生命力　{data.Vitality}\n集中力　{data.Mind}\n耐力　　{data.Endurance}\n力气　　{data.Strength}\n" +
                    $"灵巧　　{data.Dexterity}\n智力　　{data.Intelligence}\n信仰　　{data.Faith}\n感应　　{data.Arcane}";
                m_root.Q<DropdownField>(className: "menu-dropdown")?.SetValueWithoutNotify(data.ClassName);
            }
        }

        public void SetCharacterName(string value)
        {
            m_name = value;
            m_root?.Q<TextField>("character-name")?.SetValueWithoutNotify(value);
        }

        public void SetVisible(bool visible)
        {
            m_root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetBodyType(bool isMale)
        {
            m_root.Q<DropdownField>("body-type").SetValueWithoutNotify(isMale ? "体型 A" : "体型 B");
        }

        public void ShowSection(string name)
        {
            if (!m_sections.ContainsKey(name))
            {
                throw new ArgumentException("Unknown character creation page.", nameof(name));
            }
            m_section = name;
            foreach (var entry in m_sections)
            {
                entry.Value.style.display = entry.Key == name ? DisplayStyle.Flex : DisplayStyle.None;
            }
            bool overview = name == "基本资料";
            bool directory = name == "详细外貌" || name == "面部结构";
            m_root.Q("creation-root").EnableInClassList("creation-screen--overview", overview);
            m_root.Q("creation-root").EnableInClassList("creation-screen--directory", directory);
            m_root.Q<Label>("section-title").text = name;
            m_root.Q<Label>("creation-breadcrumb").text = overview ? "" : name == "详细外貌" ? "详细外貌" :
                name == "面部结构" ? "详细外貌 ＞ 面部结构" : name == "发型与肤色" ? "详细外貌 ＞ 头发" :
                m_sliders.Values.Any(slider => slider.parent == m_sections[name]) ?
                    "详细外貌 ＞ 面部结构 ＞ " + name : "详细外貌 ＞ " + name;
            m_root.Q<Label>("section-help").text = overview ? "决定角色的名称、体型与出身。" :
                directory ? "选择需要调整的项目。" : name == "肤色" ? "选择角色的肤色。" :
                name == "发型与肤色" ? "选择发型并调整毛发颜色。" :
                name == "眉毛" || name == "胡须" ? "选择毛发样式。颜色与头发保持一致。" :
                name == "眼睛" ? "调整虹膜大小及眼睛颜色。" : "调整角色面部。128 为基础面容的数值。";
            m_root.Q<Label>("creation-status").text = m_root.Q<Label>("section-help").text;
            m_root.Q("creation-tools").style.display = !overview && !directory && m_sliders.Values
                .Any(slider => slider.parent == m_sections[name]) ? DisplayStyle.Flex : DisplayStyle.None;
            m_preview?.SetEquipmentPreview(false);
            m_fullBody = overview || name == "详细外貌" || name == "肤色";
            m_preview?.SetCloseUp(!m_fullBody);
            m_root.Q<Button>("preview-mode").text = m_fullBody ? "面部特写" : "全身预览";
            m_root.Q<ScrollView>("creation-controls").scrollOffset = Vector2.zero;
            VisualElement first = m_sections[name].Children().FirstOrDefault(element => element.focusable);
            first?.Focus();
        }

        public void RefreshMorphValues(CharacterAppearanceData data)
        {
            foreach (var entry in m_sliders)
            {
                float value = data.GetShape(entry.Key);
                entry.Value.SetValueWithoutNotify(Mathf.RoundToInt(128f + value * (value < 0f ? 128f : 127f)));
            }
        }

        private VisualElement Section(string name)
        {
            if (!m_sections.TryGetValue(name, out VisualElement section))
            {
                section = new VisualElement { name = "page-" + name };
                section.style.display = DisplayStyle.None;
                m_root.Q<ScrollView>("creation-controls").Add(section);
                m_sections.Add(name, section);
            }
            return section;
        }

        private void TogglePreviewMode()
        {
            m_fullBody = !m_fullBody;
            m_preview?.SetCloseUp(!m_fullBody);
            m_root.Q<Button>("preview-mode").text = m_fullBody ? "面部特写" : "全身预览";
        }

        private void ShowConfirmation()
        {
            if (!m_owner.IsReady)
            {
                return;
            }
            m_root.Q<Label>("confirmation-summary").text = TitleScreenCharacterCreationManager.SanitizeCharacterName(CharacterName);
            m_root.Q("creation-content").SetEnabled(false);
            m_root.Q("creation-footer").SetEnabled(false);
            m_root.Q("creation-confirmation").style.display = DisplayStyle.Flex;
            m_root.Q<Button>("confirm-back").Focus();
        }

        private void HideConfirmation()
        {
            m_root.Q("creation-confirmation").style.display = DisplayStyle.None;
            m_root.Q("creation-content").SetEnabled(true);
            m_root.Q("creation-footer").SetEnabled(true);
            m_root.Q<Button>("create").Focus();
        }

        private void OnCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            NavigateBack();
        }

        public void NavigateBack()
        {
            if (m_root.Q("creation-confirmation").resolvedStyle.display != DisplayStyle.None)
            {
                HideConfirmation();
            }
            else if (m_section != "基本资料")
            {
                ShowSection(m_section == "详细外貌" ? "基本资料" :
                    m_section == "面部结构" || m_section == "肤色" || m_section == "发型与肤色" ||
                    m_section == "眉毛" || m_section == "胡须" || m_section == "眼睛" ?
                    "详细外貌" : "面部结构");
            }
            else
            {
                m_owner.OpenOriginSelection();
            }
        }
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) { return; }
            m_isDragging = true;
            m_lastPointer = evt.position;
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
        }
        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!m_isDragging) { return; }
            m_preview?.Rotate((evt.position.x - m_lastPointer.x) * 0.4f);
            m_lastPointer = evt.position;
        }
        private void OnPointerUp(PointerUpEvent evt)
        {
            m_isDragging = false;
            ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
        }
    }
}
