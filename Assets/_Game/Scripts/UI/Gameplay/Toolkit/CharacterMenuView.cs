using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Native character-menu commands with explicit confirmation before leaving gameplay.</summary>
    public sealed class CharacterMenuView : MonoBehaviour
    {
        private UIDocument m_document;
        private PanelSettings m_panel;
        private PlayerUICharacterMenuManager m_owner;
        private VisualElement m_root;
        private Action m_exit;
        private string m_exitSource;

        public bool IsConfirmationOpen => m_exit != null;
        public bool IsStatusOpen { get; private set; }

        private void OnEnable()
        {
            if (m_document == null)
            {
                return;
            }
            m_root = m_document.rootVisualElement;
            foreach (string command in new[] { "weapons", "equipment", "inventory", "status", "settings" })
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("character-menu__icon");
                icon.AddToClassList("character-menu__icon--" + command);
                m_root.Q<Button>(command).Add(icon);
            }
            Bind("resume", m_owner.CloseAllMenuWindowsAfterFixedUpdate);
            Bind("equipment", () => PlayerUIManager.Instance.PlayerUIEquipmentManager.OpenEquipmentManagerMenu());
            Bind("weapons", () => PlayerUIManager.Instance.PlayerUIEquipmentManager.OpenWeaponMenu());
            Bind("inventory", () => PlayerUIManager.Instance.PlayerUIInventoryManager.OpenInventory());
            Bind("status", OpenStatus);
            Bind("status-back", CloseStatus);
            Bind("save", () => PlayerUIManager.Instance.PlayerUISaveGameManager.OpenSaveGameMenu());
            Bind("settings", m_owner.OpenSettings);
            Bind("title", () => ConfirmExit("title", m_owner.ReturnToMainMenu));
            Bind("quit", () => ConfirmExit("quit", m_owner.QuitGame));
            Bind("cancel-exit", CancelExit);
            Bind("confirm-exit", () =>
            {
                Action exit = m_exit;
                m_exit = null;
                exit?.Invoke();
            });
            m_root.Q("exit-confirmation").style.display = DisplayStyle.None;
            IsStatusOpen = false;
            m_root.Q("status-panel").style.display = DisplayStyle.None;
            m_root.Q(className: "character-menu").RemoveFromClassList("character-menu--status");
            string characterName = WorldSaveGameManager.Instance?.CurrentCharacterData?.CharacterName;
            m_root.Q<Label>("character-name").text = string.IsNullOrWhiteSpace(characterName) ? "旅人" : characterName;
            int runes = PlayerUIManager.Instance?.LocalPlayer?.PlayerStatsManager.Runes ?? 0;
            m_root.Q<Label>("character-currency").text = $"持有卢恩  {runes:N0}";
            Focus("resume");
        }

        private void OnDisable()
        {
            m_exit = null;
            IsStatusOpen = false;
            m_root = null;
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

        public static CharacterMenuView Create(Transform parent, PlayerUICharacterMenuManager owner)
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/CharacterMenu");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/MenuPanelTheme");
            if (layout == null || theme == null)
            {
                throw new InvalidOperationException("Character menu Toolkit assets are missing.");
            }
            GameObject instance = new GameObject("Character Menu Toolkit");
            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100;
            UIDocument document = instance.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = layout;
            document.gameObject.AddComponent<MenuAudioFeedback>();
            CharacterMenuView view = instance.AddComponent<CharacterMenuView>();
            view.m_document = document;
            view.m_panel = settings;
            view.m_owner = owner;
            instance.SetActive(true);
            return view;
        }

        public void Focus(string command)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            VisualElement root = m_root;
            root?.schedule.Execute(() =>
            {
                if (m_root == root && gameObject.activeInHierarchy)
                {
                    root.Q<Button>(command).Focus();
                }
            });
        }

        public void CancelExit()
        {
            m_exit = null;
            m_root.Q("exit-confirmation").style.display = DisplayStyle.None;
            m_root.Q(className: "character-menu__card").SetEnabled(true);
            Focus(m_exitSource ?? "resume");
        }

        /// <summary>Returns from status without releasing the parent menu's input ownership.</summary>
        public void CloseStatus()
        {
            IsStatusOpen = false;
            m_root.Q("status-panel").style.display = DisplayStyle.None;
            m_root.Q(className: "character-menu__card").style.display = DisplayStyle.Flex;
            m_root.Q(className: "character-menu").RemoveFromClassList("character-menu--status");
            Focus("status");
        }

        private void OpenStatus()
        {
            PlayerManager player = PlayerUIManager.Instance?.LocalPlayer;
            if (player == null)
            {
                return;
            }
            CharacterStatusPresentation.Populate(m_root, player);
            IsStatusOpen = true;
            m_root.Q(className: "character-menu__card").style.display = DisplayStyle.None;
            m_root.Q("status-panel").style.display = DisplayStyle.Flex;
            m_root.Q(className: "character-menu").AddToClassList("character-menu--status");
            Focus("status-back");
        }

        private void Bind(string name, Action action)
        {
            m_root.Q<Button>(name).clicked += action;
        }

        private void ConfirmExit(string source, Action exit)
        {
            m_exit = exit;
            m_exitSource = source;
            m_root.Q<Label>("exit-description").text = WorldSaveGameManager.Instance?.CanSaveGame == true ?
                "尚未保存的进度将丢失。如需保留，请先返回菜单保存。" : "当前没有可写入的存档槽，本次临时进度不会保存。";
            m_root.Q(className: "character-menu__card").SetEnabled(false);
            m_root.Q("exit-confirmation").style.display = DisplayStyle.Flex;
            Focus("cancel-exit");
        }
    }
}
