using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    public class TitleScreenManager : MonoBehaviour
    {
        [Header("MENUS")]
        [SerializeField] private GameObject m_pressStartMenu;
        [SerializeField] private GameObject m_mainMenu;
        [SerializeField] private GameObject m_loadGameMenu;
        [SerializeField] private TitleScreenCharacterCreationManager
            m_characterCreationManager;

        [Header("CHARACTER CLASSES")]
        [SerializeField] private CharacterClass[] m_startingClasses;

        [Header("POPUPS")]
        [SerializeField] private GameObject m_noFreeCharacterSlotsPopup;
        [SerializeField] private GameObject m_deleteCharacterSlotPopup;

        [Header("SELECTION")]
        [SerializeField] private Button m_newGameButton;
        [SerializeField] private Button m_loadGameButton;
        [SerializeField] private Button m_loadGameReturnButton;
        [SerializeField] private Button m_noFreeSlotsCloseButton;
        [SerializeField] private Button m_confirmDeleteButton;
        [SerializeField] private UICharacterSaveSlot[] m_characterSaveSlots;

        private CharacterSlot m_currentSelectedSlot = CharacterSlot.NoSlot;

        /// <summary>Gets the authored classes displayed by character creation.</summary>
        public CharacterClass[] StartingClasses
        {
            get
            {
                EnsureDefaultStartingClasses();
                return m_startingClasses;
            }
        }

        private void Awake()
        {
            if (m_characterCreationManager == null)
            {
                m_characterCreationManager =
                    GetComponent<TitleScreenCharacterCreationManager>();
            }

            if (m_characterCreationManager == null)
            {
                m_characterCreationManager =
                    gameObject.AddComponent<TitleScreenCharacterCreationManager>();
            }

            m_characterCreationManager.ConfigureRuntime(this, m_newGameButton);
        }

        /// <summary>
        /// Starts the host used by the title screen without changing menu state.
        /// </summary>
        public void StartNetworkAsHost()
        {
            TryStartNetworkAsHost();
        }

        /// <summary>
        /// Starts the host, opens the main menu, and establishes controller focus.
        /// </summary>
        public void PressStart()
        {
            if (!TryStartNetworkAsHost())
            {
                return;
            }

            m_pressStartMenu?.SetActive(false);
            m_mainMenu?.SetActive(true);
            m_newGameButton?.Select();
        }

        /// <summary>
        /// Opens character creation when a slot is free or displays the capacity warning.
        /// </summary>
        public void StartNewGame()
        {
            if (!IsNetworkHostReady())
            {
                return;
            }

            if (!WorldSaveGameManager.Instance.HasFreeCharacterSlot())
            {
                DisplayNoFreeCharacterSlotsPopup();
                return;
            }

            m_mainMenu?.SetActive(false);
            m_characterCreationManager?.OpenCharacterCreation();
        }

        /// <summary>Closes character creation and restores title-menu controller focus.</summary>
        public void ReturnFromCharacterCreation()
        {
            m_characterCreationManager?.CloseCharacterCreation();
            m_mainMenu?.SetActive(true);
            m_newGameButton?.Select();
        }

        /// <summary>
        /// Opens the cached character list and moves focus to its return button.
        /// </summary>
        public void OpenLoadGameMenu()
        {
            if (!IsNetworkHostReady())
            {
                return;
            }

            m_currentSelectedSlot = CharacterSlot.NoSlot;
            m_mainMenu?.SetActive(false);
            PrepareSaveSlotsForRefresh();
            m_loadGameMenu?.SetActive(true);
            m_loadGameReturnButton?.Select();
        }

        /// <summary>
        /// Closes the character list and restores main-menu controller focus.
        /// </summary>
        public void CloseLoadGameMenu()
        {
            m_currentSelectedSlot = CharacterSlot.NoSlot;
            m_deleteCharacterSlotPopup?.SetActive(false);
            m_loadGameMenu?.SetActive(false);
            m_mainMenu?.SetActive(true);
            m_loadGameButton?.Select();
        }

        /// <summary>
        /// Displays the fixed-slot capacity warning.
        /// </summary>
        public void DisplayNoFreeCharacterSlotsPopup()
        {
            m_noFreeCharacterSlotsPopup?.SetActive(true);
            m_noFreeSlotsCloseButton?.Select();
        }

        /// <summary>
        /// Closes the fixed-slot capacity warning.
        /// </summary>
        public void CloseNoFreeCharacterSlotsPopup()
        {
            m_noFreeCharacterSlotsPopup?.SetActive(false);
            m_newGameButton?.Select();
        }

        /// <summary>
        /// Records the save slot most recently selected by the EventSystem.
        /// </summary>
        public void SelectCurrentSlot(CharacterSlot characterSlot)
        {
            m_currentSelectedSlot = characterSlot;
        }

        /// <summary>
        /// Clears selection when focus leaves the character slot list.
        /// </summary>
        public void SelectNoSlot()
        {
            m_currentSelectedSlot = CharacterSlot.NoSlot;
        }

        /// <summary>
        /// Opens delete confirmation only when a real character slot is selected.
        /// </summary>
        public void AttemptToDeleteCharacterSlot()
        {
            if (m_currentSelectedSlot == CharacterSlot.NoSlot ||
                (m_deleteCharacterSlotPopup != null && m_deleteCharacterSlotPopup.activeSelf))
            {
                return;
            }

            m_deleteCharacterSlotPopup?.SetActive(true);
            m_confirmDeleteButton?.Select();
        }

        /// <summary>
        /// Confirms deletion, clears the slot cache, and refreshes the list.
        /// </summary>
        public void DeleteCharacterSlot()
        {
            if (m_currentSelectedSlot == CharacterSlot.NoSlot ||
                WorldSaveGameManager.Instance == null)
            {
                return;
            }

            WorldSaveGameManager.Instance.DeleteGame(m_currentSelectedSlot);
            m_currentSelectedSlot = CharacterSlot.NoSlot;
            m_deleteCharacterSlotPopup?.SetActive(false);
            PrepareSaveSlotsForRefresh();
            RefreshVisibleSaveSlots();
            m_loadGameReturnButton?.Select();
        }

        /// <summary>
        /// Cancels deletion and restores focus to the selected character slot.
        /// </summary>
        public void CloseDeleteCharacterPopup()
        {
            m_deleteCharacterSlotPopup?.SetActive(false);
            RestoreLoadMenuSelection();
        }

        private bool TryStartNetworkAsHost()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("A NetworkManager is required before starting a host.");
                return false;
            }

            if (networkManager.IsListening)
            {
                return networkManager.IsServer;
            }

            if (!networkManager.StartHost())
            {
                Debug.LogError("Failed to start the network host.");
                return false;
            }

            return true;
        }

        private bool IsNetworkHostReady()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                Debug.LogError(
                    "Cannot continue because the network host is not running. " +
                    "Resolve the transport error and try again.");
                return false;
            }

            if (WorldSaveGameManager.Instance == null)
            {
                Debug.LogError("WorldSaveGameManager is not available.");
                return false;
            }

            return true;
        }

        private void PrepareSaveSlotsForRefresh()
        {
            if (m_characterSaveSlots == null)
            {
                return;
            }

            foreach (UICharacterSaveSlot saveSlot in m_characterSaveSlots)
            {
                saveSlot?.gameObject.SetActive(true);
            }
        }

        private void RefreshVisibleSaveSlots()
        {
            if (m_characterSaveSlots == null)
            {
                return;
            }

            foreach (UICharacterSaveSlot saveSlot in m_characterSaveSlots)
            {
                if (saveSlot != null && saveSlot.gameObject.activeSelf)
                {
                    saveSlot.LoadSaveSlot();
                }
            }
        }

        private void RestoreLoadMenuSelection()
        {
            if (m_characterSaveSlots != null)
            {
                foreach (UICharacterSaveSlot saveSlot in m_characterSaveSlots)
                {
                    if (saveSlot != null &&
                        saveSlot.CharacterSlot == m_currentSelectedSlot &&
                        saveSlot.gameObject.activeInHierarchy)
                    {
                        saveSlot.Select();
                        return;
                    }
                }
            }

            m_currentSelectedSlot = CharacterSlot.NoSlot;
            m_loadGameReturnButton?.Select();
        }

        private void EnsureDefaultStartingClasses()
        {
            if (m_startingClasses != null && m_startingClasses.Length > 0)
            {
                return;
            }

            WorldItemDatabase database = WorldItemDatabase.Instance;
            if (database == null)
            {
                return;
            }

            WeaponItem unarmed = database.GetWeaponByID(0);
            QuickSlotItem crimsonFlask = database.GetQuickSlotItemByID(14);
            QuickSlotItem ceruleanFlask = database.GetQuickSlotItemByID(15);
            m_startingClasses = new[]
            {
                new CharacterClass(
                    "Knight",
                    12,
                    11,
                    10,
                    14,
                    10,
                    8,
                    9,
                    new[]
                    {
                        database.GetWeaponByID(1),
                        database.GetWeaponByID(2),
                        unarmed
                    },
                    new[]
                    {
                        database.GetWeaponByID(3),
                        unarmed,
                        unarmed
                    },
                    database.GetHeadEquipmentByID(4),
                    database.GetBodyEquipmentByID(5),
                    database.GetHandEquipmentByID(6),
                    database.GetLegEquipmentByID(7),
                    new[] { crimsonFlask, ceruleanFlask, null },
                    new[] { 3, 1, 0 }),
                new CharacterClass(
                    "Ranger",
                    10,
                    12,
                    11,
                    10,
                    15,
                    9,
                    8,
                    new[]
                    {
                        database.GetWeaponByID(11),
                        database.GetWeaponByID(1),
                        unarmed
                    },
                    new[] { unarmed, unarmed, unarmed },
                    null,
                    database.GetBodyEquipmentByID(5),
                    database.GetHandEquipmentByID(6),
                    database.GetLegEquipmentByID(7),
                    new[] { crimsonFlask, ceruleanFlask, null },
                    new[] { 3, 1, 0 })
            };
        }
    }
}
