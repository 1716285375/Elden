using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using System;

namespace ZZ
{
    /// <summary>Native manual-save presentation; writes remain owned by the existing save manager.</summary>
    public sealed class SaveGameView : MonoBehaviour
    {
        private UIDocument m_document;
        private PanelSettings m_panel;
        private PlayerUISaveGameManager m_owner;
        private VisualElement m_root;

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            m_root.Q<Button>("save").clicked += m_owner.SaveCurrentGame;
            m_root.Q<Button>("back").clicked += m_owner.ReturnToCharacterMenu;
        }

        private void OnDisable()
        {
            m_root = null;
        }

        private void OnDestroy()
        {
            if (m_panel != null)
            {
                Destroy(m_panel);
            }
        }

        /// <summary>Creates a native menu beneath the existing grace lifecycle root.</summary>
        public static SaveGameView Create(Transform parent, PlayerUISaveGameManager owner)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/SaveGame");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Save Game Toolkit assets are missing.");
            }
            GameObject instance = new GameObject("Save Game Toolkit");
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
            SaveGameView view = instance.AddComponent<SaveGameView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            instance.SetActive(true);
            return view;
        }


        /// <summary>Displays the current character and slot without modifying save data.</summary>
        public void Refresh(string characterName, string details, bool canSave, bool resetFeedback)
        {
            if (m_root == null)
            {
                return;
            }
            m_root.Q<Label>("character-name").text = characterName;
            m_root.Q<Label>("save-details").text = details;
            m_root.Q<Button>("save").SetEnabled(canSave);
            if (resetFeedback)
            {
                SetFeedback(canSave ? "保存角色当前状态、位置与游玩时间。" : "当前角色暂时无法保存。", false);
                Focus(canSave ? "save" : "back");
            }
        }

        /// <summary>Reports the actual write result; success moves focus away from the disabled command.</summary>
        public void SetFeedback(string message, bool wasSaved)
        {
            if (m_root == null)
            {
                return;
            }
            m_root.Q<Label>("save-feedback").text = message;
            m_root.Q<Label>("save-status").text = wasSaved ? "保存完成" : "手动保存";
            if (wasSaved)
            {
                Focus("back");
            }
        }

        private void Focus(string name)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            VisualElement root = m_root;
            root?.schedule.Execute(() =>
            {
                if (m_root == root && isActiveAndEnabled)
                {
                    root.Q(name)?.Focus();
                }
            });
        }
    }
}
