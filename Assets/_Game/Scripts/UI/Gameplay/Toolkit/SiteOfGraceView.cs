using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Native grace services with capability-aware travel and inventory commands.</summary>
    public sealed class SiteOfGraceView : MonoBehaviour
    {
        private UIDocument m_document;
        private PanelSettings m_panel;
        private PlayerUISiteOfGraceManager m_owner;
        private VisualElement m_root;
        private MemorizeSpellsView m_memory;

        public bool IsMemoryOpen => m_memory?.IsOpen == true;

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            m_memory = new MemorizeSpellsView(m_root, PlayerUIManager.Instance.LocalPlayer.InventoryManager);
            Bind("memorize", m_memory.Open);
            Bind("level-up", m_owner.OpenLevelUpMenu);
            Bind("storage", m_owner.OpenStorageMenu);
            Bind("travel", m_owner.OpenTeleportLocationMenu);
            Bind("leave", m_owner.CloseSiteOfGraceMenu);
        }

        private void OnDisable()
        {
            m_memory?.Dispose();
            m_memory = null;
            m_root = null;
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

        /// <summary>Creates a native menu beneath the existing grace lifecycle root.</summary>
        public static SiteOfGraceView Create(Transform parent, PlayerUISiteOfGraceManager owner)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/SiteOfGrace");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Site of Grace Toolkit assets are missing.");
            }
            GameObject instance = new GameObject("Site Of Grace Toolkit");
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
            SiteOfGraceView view = instance.AddComponent<SiteOfGraceView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            instance.SetActive(true);
            return view;
        }

        /// <summary>Refreshes available services each time the player returns to grace.</summary>
        public void Refresh()
        {
            if (m_root == null)
            {
                return;
            }
            PlayerUIManager ui = PlayerUIManager.Instance;
            bool canTravel = ui?.PlayerUITeleportLocationManager?.CanFastTravel == true;
            m_root.Q<Button>("travel").SetEnabled(canTravel);
            m_root.Q<Button>("storage").SetEnabled(ui?.LocalPlayer?.InventoryManager != null);
            m_root.Q<Label>("travel-hint").text = canTravel ? "前往已经解锁的赐福。" : "快速旅行仅在单人会话中可用。";
            m_root.Q<Label>("grace-runes").text = $"持有卢恩  {ui?.LocalPlayer?.PlayerStatsManager?.Runes ?? 0:N0}";
            Focus("level-up");
        }

        /// <summary>Returns from the spell page while grace retains input ownership.</summary>
        public void CloseMemory()
        {
            m_memory?.Close();
        }

        private void Bind(string name, Action action)
        {
            m_root.Q<Button>(name).clicked += action;
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
