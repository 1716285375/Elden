using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Virtualized unlocked destinations and explicit fast-travel confirmation.</summary>
    public sealed class TravelMenuView : MonoBehaviour
    {
        private readonly List<SiteOfGraceInteractable> m_locations = new();
        private UIDocument m_document;
        private PanelSettings m_panel;
        private PlayerUITeleportLocationManager m_owner;
        private VisualElement m_root;
        private ListView m_list;
        private SiteOfGraceInteractable m_selected;

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            Bind("back", m_owner.ReturnToSiteOfGrace);
            Bind("refresh", RefreshLocations);
            Bind("travel", () =>
            {
                if (m_selected != null)
                {
                    m_owner.RequestTravel(m_selected.SiteOfGraceID);
                }
            });
            Bind("confirm", m_owner.ConfirmTravel);
            Bind("cancel", m_owner.CancelTravel);
            m_list = m_root.Q<ListView>("destinations");
            m_list.itemsSource = m_locations;
            m_list.fixedItemHeight = 72;
            m_list.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            m_list.selectionType = SelectionType.Single;
            m_list.makeItem = () =>
            {
                Label label = new Label();
                label.AddToClassList("travel-menu__location");
                return label;
            };
            m_list.bindItem = (element, index) => ((Label)element).text = m_locations[index].DisplayName;
            m_list.selectionChanged += selection =>
            {
                m_selected = null;
                foreach (object item in selection)
                {
                    m_selected = item as SiteOfGraceInteractable;
                    break;
                }
                ShowDetails();
            };
            m_list.itemsChosen += _ => Focus("travel");
            HideConfirmation();
            Focus("destinations");
        }

        private void OnDisable()
        {
            m_root = null;
            m_selected = null;
            m_list = null;
            m_locations.Clear();
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

        /// <summary>Creates the native travel document beneath its existing menu lifecycle root.</summary>
        public static TravelMenuView Create(Transform parent, PlayerUITeleportLocationManager owner)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/TravelMenu");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Travel menu Toolkit assets are missing.");
            }
            GameObject instance = new GameObject("Travel Menu Toolkit");
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
            TravelMenuView view = instance.AddComponent<TravelMenuView>();
            view.m_document = document;
            view.m_panel = panel;
            view.m_owner = owner;
            instance.SetActive(true);
            return view;
        }

        /// <summary>Reads registered, unlocked destinations; old prefab button counts do not limit the list.</summary>
        public void RefreshLocations()
        {
            if (m_root == null)
            {
                return;
            }
            m_locations.Clear();
            IReadOnlyList<SiteOfGraceInteractable> sites = WorldObjectManager.Instance?.SitesOfGrace;
            if (sites != null)
            {
                foreach (SiteOfGraceInteractable site in sites)
                {
                    if (PlayerUITeleportLocationManager.IsLocationUnlocked(site) && site.TeleportTransform != null)
                    {
                        m_locations.Add(site);
                    }
                }
            }
            m_locations.Sort((first, second) => first.SiteOfGraceID.CompareTo(second.SiteOfGraceID));
            int selectedIndex = m_selected != null ? m_locations.IndexOf(m_selected) : -1;
            if (selectedIndex < 0 && m_locations.Count > 0)
            {
                selectedIndex = 0;
            }
            m_selected = selectedIndex >= 0 ? m_locations[selectedIndex] : null;
            m_list.SetSelectionWithoutNotify(selectedIndex >= 0 ? new[] { selectedIndex } : Array.Empty<int>());
            m_list.RefreshItems();
            m_root.Q<Label>("destination-count").text = $"已解锁 {m_locations.Count} 处赐福";
            m_root.Q("empty-destinations").style.display = m_locations.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            ShowDetails();
        }

        /// <summary>Locks the destination list and focuses Cancel before teleporting.</summary>
        public void ShowConfirmation(string destinationName)
        {
            m_root.Q("travel-content").SetEnabled(false);
            m_root.Q("travel-confirmation").style.display = DisplayStyle.Flex;
            m_root.Q<Label>("confirmation-description").text = $"传送至「{destinationName}」？";
            Focus("cancel");
        }

        /// <summary>Closes the pending travel prompt without selecting a different destination.</summary>
        public void HideConfirmation()
        {
            if (m_root == null)
            {
                return;
            }
            bool wasOpen = m_root.Q("travel-confirmation").style.display.value == DisplayStyle.Flex;
            m_root.Q("travel-content").SetEnabled(true);
            m_root.Q("travel-confirmation").style.display = DisplayStyle.None;
            if (wasOpen)
            {
                Focus("travel");
            }
        }

        /// <summary>Displays an actionable travel failure without closing the menu.</summary>
        public void SetFeedback(string message)
        {
            if (m_root != null)
            {
                m_root.Q<Label>("travel-feedback").text = message;
            }
        }

        private void ShowDetails()
        {
            m_root.Q<Label>("destination-name").text = m_selected != null ? m_selected.DisplayName : "暂无可用目的地";
            m_root.Q<Button>("travel").SetEnabled(m_selected != null && m_owner.CanFastTravel);
            SetFeedback(m_owner.CanFastTravel ? "选择赐福后确认传送。取消不会改变当前位置。" :
                "当前会话不满足单人快速旅行条件。");
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
