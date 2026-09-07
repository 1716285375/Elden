using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Editor
{
    /// <summary>Creates registered project data assets from a searchable, categorized window.</summary>
    internal sealed class GameAssetCreationWindow : EditorWindow
    {
        private readonly List<Type> m_assetTypes = new();
        private ScrollView m_list;
        private Label m_summary;
        private string m_searchText = string.Empty;

        [ZZTool("资源管理", "资源创建中心", 10)]
        public static void OpenWindow()
        {
            var window = GetWindow<GameAssetCreationWindow>();
            window.titleContent = new GUIContent("资源创建中心");
            window.minSize = new Vector2(520f, 400f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12;
            rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12;

            m_summary = new Label();
            m_summary.AddToClassList("game-assets__summary");
            rootVisualElement.Add(m_summary);

            var help = new HelpBox("搜索资源名称或类型，点击创建后选择保存位置。", HelpBoxMessageType.Info);
            help.AddToClassList("game-assets__help");
            rootVisualElement.Add(help);

            var search = new ToolbarSearchField();
            search.AddToClassList("game-assets__search");
            search.SetValueWithoutNotify(m_searchText);
            search.RegisterValueChangedCallback(evt =>
            {
                m_searchText = evt.newValue.Trim();
                RebuildList();
            });
            rootVisualElement.Add(search);

            m_list = new ScrollView();
            m_list.AddToClassList("game-assets__list");
            m_list.style.flexGrow = 1;
            rootVisualElement.Add(m_list);
            m_assetTypes.Clear();
            m_assetTypes.AddRange(TypeCache.GetTypesWithAttribute<GameAssetAttribute>()
                .Where(type => typeof(ScriptableObject).IsAssignableFrom(type) &&
                    !type.IsAbstract && !type.ContainsGenericParameters)
                .OrderBy(GetMenuPath, StringComparer.Ordinal));
            RebuildList();
        }

        private void RebuildList()
        {
            m_list.Clear();
            var visibleTypes = m_assetTypes.Where(type =>
                GetMenuPath(type).IndexOf(m_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.Name.IndexOf(m_searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            m_summary.text = $"资源创建中心 · {visibleTypes.Count} / {m_assetTypes.Count} 种资源";

            foreach (var group in visibleTypes.GroupBy(type => GetMenuPath(type).Split('/')[0]))
            {
                var category = new Foldout { text = group.Key, value = true };
                category.AddToClassList("game-assets__category");
                category.style.marginTop = 10;
                foreach (Type type in group)
                {
                    var row = new VisualElement();
                    row.AddToClassList("game-assets__row");
                    row.style.flexDirection = FlexDirection.Row;
                    var label = new Label(GetMenuPath(type)) { tooltip = type.FullName };
                    label.AddToClassList("game-assets__name");
                    label.style.flexGrow = 1;
                    row.Add(label);
                    var create = new Button(() => CreateAsset(type)) { text = "创建" };
                    create.AddToClassList("game-assets__create");
                    row.Add(create);
                    category.Add(row);
                }
                m_list.Add(category);
            }

            if (visibleTypes.Count == 0)
            {
                var empty = new Label("没有匹配的资源类型");
                empty.AddToClassList("game-assets__empty");
                m_list.Add(empty);
            }
        }

        private static string GetMenuPath(Type type)
        {
            string path = type.GetCustomAttribute<GameAssetAttribute>().MenuName;
            if (string.IsNullOrWhiteSpace(path))
            {
                return type.Name;
            }
            return path.StartsWith("ZZ/", StringComparison.Ordinal) ? path.Substring(3) : path;
        }

        private static void CreateAsset(Type type)
        {
            string fileName = type.GetCustomAttribute<GameAssetAttribute>().FileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "New " + type.Name;
            }
            string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            }
            if (string.IsNullOrEmpty(folder) ||
                !(folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                folder = "Assets/_Game/Data";
            }
            string path = EditorUtility.SaveFilePanelInProject(
                "创建 " + type.Name, fileName, "asset", "选择资源保存位置", folder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("无法创建", "请选择 Assets 文件夹内的保存位置。", "确定");
                return;
            }

            // A selected existing filename must never replace an authored asset.
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            ScriptableObject asset = null;
            try
            {
                asset = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("创建失败", "资源未能创建，详细原因见 Console。", "确定");
            }
            finally
            {
                if (asset != null && !EditorUtility.IsPersistent(asset))
                {
                    DestroyImmediate(asset);
                }
            }
        }
    }
}
