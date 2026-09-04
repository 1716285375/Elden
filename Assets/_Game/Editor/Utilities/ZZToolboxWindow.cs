using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Editor
{
    /// <summary>
    /// Provides the single menu entry for project-specific editor commands.
    /// </summary>
    internal sealed class ZZToolboxWindow : EditorWindow
    {
        private const string k_MenuPath = "ZZ/工具面板";
        private const string k_WindowTitle = "ZZ 工具面板";
        private const float k_WindowMinWidth = 520f;
        private const float k_WindowMinHeight = 480f;

        private readonly List<ToolEntry> m_tools = new();
        private VisualElement m_contentRoot;
        private Label m_summaryLabel;
        private Label m_statusLabel;
        private string m_searchText = string.Empty;

        [MenuItem(k_MenuPath, priority = 0)]
        public static void OpenWindow()
        {
            ZZToolboxWindow window = GetWindow<ZZToolboxWindow>();
            window.titleContent = new GUIContent(k_WindowTitle);
            window.minSize = new Vector2(k_WindowMinWidth, k_WindowMinHeight);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            BuildToolRegistry();
            BuildHeader();

            ScrollView scrollView = new(ScrollViewMode.Vertical);
            scrollView.AddToClassList("zz-toolbox__scroll-view");
            scrollView.style.flexGrow = 1f;
            scrollView.style.paddingLeft = 14f;
            scrollView.style.paddingRight = 14f;
            scrollView.style.paddingBottom = 14f;
            rootVisualElement.Add(scrollView);
            m_contentRoot = scrollView.contentContainer;

            m_statusLabel = new Label("就绪");
            m_statusLabel.AddToClassList("zz-toolbox__status");
            m_statusLabel.style.paddingLeft = 14f;
            m_statusLabel.style.paddingRight = 14f;
            m_statusLabel.style.paddingTop = 8f;
            m_statusLabel.style.paddingBottom = 8f;
            m_statusLabel.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            rootVisualElement.Add(m_statusLabel);

            RebuildToolList();
        }

        private void BuildHeader()
        {
            VisualElement header = new();
            header.AddToClassList("zz-toolbox__header");
            header.style.paddingLeft = 14f;
            header.style.paddingRight = 14f;
            header.style.paddingTop = 12f;
            header.style.paddingBottom = 10f;
            header.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);

            Label titleLabel = new(k_WindowTitle);
            titleLabel.AddToClassList("zz-toolbox__title");
            titleLabel.style.fontSize = 20f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(titleLabel);

            m_summaryLabel = new Label();
            m_summaryLabel.AddToClassList("zz-toolbox__summary");
            m_summaryLabel.style.marginTop = 3f;
            m_summaryLabel.style.marginBottom = 9f;
            m_summaryLabel.style.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            header.Add(m_summaryLabel);

            VisualElement toolbar = new();
            toolbar.AddToClassList("zz-toolbox__toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;

            ToolbarSearchField searchField = new();
            searchField.AddToClassList("zz-toolbox__search");
            searchField.style.flexGrow = 1f;
            searchField.RegisterValueChangedCallback(evt =>
            {
                m_searchText = evt.newValue?.Trim() ?? string.Empty;
                RebuildToolList();
            });
            toolbar.Add(searchField);

            Button refreshButton = new(() =>
            {
                BuildToolRegistry();
                RebuildToolList();
                SetStatus("工具列表已刷新", false);
            })
            {
                text = "刷新"
            };
            refreshButton.AddToClassList("zz-toolbox__refresh");
            refreshButton.style.marginLeft = 8f;
            toolbar.Add(refreshButton);

            header.Add(toolbar);
            rootVisualElement.Add(header);
        }

        private void BuildToolRegistry()
        {
            m_tools.Clear();

            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<ZZToolAttribute>())
            {
                ZZToolAttribute attribute = method.GetCustomAttribute<ZZToolAttribute>();
                if (attribute == null || !IsSupportedToolMethod(method))
                {
                    Debug.LogError(
                        $"[ZZToolbox] {method.DeclaringType?.FullName}.{method.Name} " +
                        "必须是无参数、返回 void 的静态方法。");
                    continue;
                }

                m_tools.Add(new ToolEntry(method, attribute));
            }

            m_tools.Sort((left, right) =>
            {
                int orderComparison = left.Order.CompareTo(right.Order);
                return orderComparison != 0
                    ? orderComparison
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            });
        }

        private void RebuildToolList()
        {
            if (m_contentRoot == null)
            {
                return;
            }

            m_contentRoot.Clear();
            IEnumerable<ToolEntry> visibleTools = m_tools.Where(MatchesSearch);
            List<IGrouping<string, ToolEntry>> groups = visibleTools
                .GroupBy(tool => tool.Category)
                .ToList();

            m_summaryLabel.text = $"{m_tools.Count} 个项目工具 · 单一入口 · 支持搜索";

            if (groups.Count == 0)
            {
                Label emptyLabel = new("没有匹配的工具");
                emptyLabel.AddToClassList("zz-toolbox__empty");
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.marginTop = 40f;
                m_contentRoot.Add(emptyLabel);
                return;
            }

            foreach (IGrouping<string, ToolEntry> group in groups)
            {
                m_contentRoot.Add(CreateToolGroup(group.Key, group));
            }
        }

        private VisualElement CreateToolGroup(string category, IEnumerable<ToolEntry> tools)
        {
            VisualElement card = new();
            card.AddToClassList("zz-toolbox__category");
            card.style.marginTop = 12f;
            card.style.paddingLeft = 10f;
            card.style.paddingRight = 10f;
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 10f;
            card.style.backgroundColor = new Color(0.20f, 0.20f, 0.20f, 1f);
            card.style.borderBottomLeftRadius = 5f;
            card.style.borderBottomRightRadius = 5f;
            card.style.borderTopLeftRadius = 5f;
            card.style.borderTopRightRadius = 5f;

            Label categoryLabel = new(category);
            categoryLabel.AddToClassList("zz-toolbox__category-title");
            categoryLabel.style.fontSize = 14f;
            categoryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            categoryLabel.style.marginBottom = 6f;
            card.Add(categoryLabel);

            foreach (ToolEntry tool in tools)
            {
                Button button = new(() => RunTool(tool))
                {
                    text = tool.DisplayName,
                    tooltip = tool.Tooltip
                };
                button.AddToClassList("zz-toolbox__tool-button");
                button.style.height = 28f;
                button.style.marginTop = 2f;
                button.style.marginBottom = 2f;

                if (tool.RequiresConfirmation)
                {
                    button.AddToClassList("zz-toolbox__tool-button--destructive");
                    button.style.color = new Color(1f, 0.72f, 0.63f, 1f);
                }

                card.Add(button);
            }

            return card;
        }

        private void RunTool(ToolEntry tool)
        {
            if (tool.RequiresConfirmation && !EditorUtility.DisplayDialog(
                    "确认操作",
                    tool.ConfirmationMessage,
                    "继续",
                    "取消"))
            {
                SetStatus($"已取消：{tool.DisplayName}", false);
                return;
            }

            try
            {
                tool.Method.Invoke(null, null);
                SetStatus($"已执行：{tool.DisplayName}", false);
            }
            catch (TargetInvocationException exception)
            {
                Exception cause = exception.InnerException ?? exception;
                Debug.LogException(cause);
                SetStatus($"执行失败：{tool.DisplayName}。详情见 Console。", true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus($"执行失败：{tool.DisplayName}。详情见 Console。", true);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            if (m_statusLabel == null)
            {
                return;
            }

            m_statusLabel.text = message;
            m_statusLabel.style.color = isError
                ? new Color(1f, 0.48f, 0.42f, 1f)
                : new Color(0.78f, 0.86f, 0.78f, 1f);
        }

        private bool MatchesSearch(ToolEntry tool)
        {
            if (string.IsNullOrEmpty(m_searchText))
            {
                return true;
            }

            return tool.Category.IndexOf(m_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tool.DisplayName.IndexOf(m_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSupportedToolMethod(MethodInfo method)
        {
            return method.IsStatic &&
                   method.ReturnType == typeof(void) &&
                   method.GetParameters().Length == 0;
        }

        private sealed class ToolEntry
        {
            public ToolEntry(MethodInfo method, ZZToolAttribute attribute)
            {
                Method = method;
                Category = attribute.Category;
                DisplayName = attribute.DisplayName;
                Order = attribute.Order;
                ConfirmationMessage = attribute.ConfirmationMessage;
            }

            public MethodInfo Method { get; }

            public string Category { get; }

            public string DisplayName { get; }

            public int Order { get; }

            public string ConfirmationMessage { get; }

            public bool RequiresConfirmation => !string.IsNullOrWhiteSpace(ConfirmationMessage);

            public string Tooltip => $"{Method.DeclaringType?.Name}.{Method.Name}";
        }
    }
}
