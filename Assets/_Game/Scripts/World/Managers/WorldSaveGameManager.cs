using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace ZZ
{
    public class WorldSaveGameManager : MonoBehaviour
    {
        private static WorldSaveGameManager s_instance;

        private const string k_LastPlayedCharacterSlotKey =
            "ZZ.LastPlayedCharacterSlot";
        private const float k_SceneEventRetryTimeoutSeconds = 30f;

        [FormerlySerializedAs("worldSceneName")]
        [SerializeField] private string m_worldSceneName =
            WorldScenePathLayout.MasterSceneName;
        [SerializeField] private int m_startingSceneIndex = 1;

        [Header("Dialogue")]
        [SerializeField] private List<CharacterDialogue>
            m_namelessKnightDialogues = new();
        [SerializeField] private List<CharacterDialogue>
            m_blacksmithDialogues = new();

        private PlayerManager m_player;
        private CharacterSaveData m_characterSlot01;
        private CharacterSaveData m_characterSlot02;
        private CharacterSaveData m_characterSlot03;
        private CharacterSaveData m_characterSlot04;
        private CharacterSaveData m_characterSlot05;
        private CharacterSaveData m_characterSlot06;
        private CharacterSaveData m_characterSlot07;
        private CharacterSaveData m_characterSlot08;
        private CharacterSaveData m_characterSlot09;
        private CharacterSaveData m_characterSlot10;
        private CharacterSlot m_currentCharacterSlotBeingUsed = CharacterSlot.NoSlot;
        private CharacterSaveData m_currentCharacterData;
        private bool m_shouldApplyLoadedCharacterData;
        private bool m_sceneLoadIsInProgress;
        private int m_namelessKnightStageID;
        private int m_blacksmithStageID;

        public static WorldSaveGameManager Instance => s_instance;

        /// <summary>
        /// Gets the save data currently associated with the locally owned player.
        /// </summary>
        public CharacterSaveData CurrentCharacterData => m_currentCharacterData;

        /// <summary>
        /// Gets the fixed slot currently selected for the active character.
        /// </summary>
        public CharacterSlot CurrentCharacterSlot => m_currentCharacterSlotBeingUsed;

        /// <summary>
        /// Gets whether the locally owned player has a writable active save slot.
        /// </summary>
        public bool CanSaveGame =>
            !m_sceneLoadIsInProgress &&
            !m_shouldApplyLoadedCharacterData &&
            m_currentCharacterSlotBeingUsed != CharacterSlot.NoSlot &&
            m_currentCharacterData != null &&
            m_player != null &&
            m_player.IsOwner;

        /// <summary>Gets whether a saved Scene transition is already running.</summary>
        public bool IsSceneLoadInProgress => m_sceneLoadIsInProgress;

        /// <summary>Gets one NPC's loaded dialogue Stage.</summary>
        public int GetStageOfDialogue(CharacterDialogueID characterDialogueID)
        {
            return characterDialogueID switch
            {
                CharacterDialogueID.NamelessKnight =>
                    m_namelessKnightStageID,
                CharacterDialogueID.Blacksmith =>
                    m_blacksmithStageID,
                _ => 0
            };
        }

        /// <summary>Returns a runtime dialogue copy for the NPC's currently loaded Stage.</summary>
        public CharacterDialogue GetCurrentDialogue(
            CharacterDialogueID characterDialogueID)
        {
            IReadOnlyList<CharacterDialogue> dialogueList =
                characterDialogueID switch
                {
                    CharacterDialogueID.NamelessKnight =>
                        m_namelessKnightDialogues,
                    CharacterDialogueID.Blacksmith =>
                        m_blacksmithDialogues,
                    _ => null
                };
            return FindDialogueByStageID(
                GetStageOfDialogue(characterDialogueID),
                dialogueList);
        }

        /// <summary>
        /// Finds one authored Stage and returns an isolated runtime copy for mutable playback.
        /// </summary>
        public CharacterDialogue FindDialogueByStageID(
            int stageID,
            IReadOnlyList<CharacterDialogue> dialogueList)
        {
            if (dialogueList == null)
            {
                return null;
            }

            foreach (CharacterDialogue dialogueAsset in dialogueList)
            {
                if (dialogueAsset != null &&
                    dialogueAsset.RequiredStageID == stageID)
                {
                    return dialogueAsset.CreateRuntimeCopy();
                }
            }

            return null;
        }

        /// <summary>Updates one Stage, mirrors it into save data, and optionally saves.</summary>
        public bool SetStageOfDialogue(
            CharacterDialogueID characterDialogueID,
            int stageID,
            bool saveImmediately)
        {
            if (m_currentCharacterData == null ||
                characterDialogueID == CharacterDialogueID.NoDialogue)
            {
                return false;
            }

            int sanitizedStageID = Mathf.Max(0, stageID);
            bool didChange;
            switch (characterDialogueID)
            {
                case CharacterDialogueID.NamelessKnight:
                    didChange =
                        m_namelessKnightStageID != sanitizedStageID;
                    m_namelessKnightStageID = sanitizedStageID;
                    m_currentCharacterData.NamelessKnightStageID =
                        sanitizedStageID;
                    break;
                case CharacterDialogueID.Blacksmith:
                    didChange = m_blacksmithStageID != sanitizedStageID;
                    m_blacksmithStageID = sanitizedStageID;
                    m_currentCharacterData.BlacksmithStageID =
                        sanitizedStageID;
                    break;
                default:
                    return false;
            }

            if (didChange && saveImmediately && CanSaveGame)
            {
                SaveGame();
            }

            return didChange;
        }

        /// <summary>Copies Dialogue Stage values only after current save data is assigned.</summary>
        public void GetStageIDsOnLoad()
        {
            m_namelessKnightStageID =
                m_currentCharacterData?.NamelessKnightStageID ?? 0;
            m_blacksmithStageID =
                m_currentCharacterData?.BlacksmithStageID ?? 0;
        }

        /// <summary>Gets the active slot's saved lifecycle state for one boss.</summary>
        public BossProgressState GetBossProgress(int bossID)
        {
            return m_currentCharacterData?.GetBossProgress(bossID) ??
                BossProgressState.Dormant;
        }

        /// <summary>Gets the active slot's saved activation state for one Site of Grace.</summary>
        public bool IsSiteOfGraceActivated(int siteOfGraceID)
        {
            return m_currentCharacterData?.IsSiteOfGraceActivated(siteOfGraceID) ??
                false;
        }

        /// <summary>Gets whether the active save permanently opened one world door.</summary>
        public bool IsDoorOpened(string doorID)
        {
            return m_currentCharacterData?.IsDoorOpened(doorID) ?? false;
        }

        /// <summary>Records one opened door and optionally writes the active slot.</summary>
        public bool RecordOpenedDoor(string doorID, bool saveImmediately)
        {
            if (m_currentCharacterData == null)
            {
                return false;
            }

            bool didChange = m_currentCharacterData.RecordOpenedDoor(doorID);
            if (didChange && saveImmediately && CanSaveGame)
            {
                SaveGame();
            }

            return didChange;
        }

        /// <summary>Updates one Site of Grace and optionally writes the active save immediately.</summary>
        public bool RecordSiteOfGraceActivation(
            int siteOfGraceID,
            bool isActivated,
            bool saveImmediately)
        {
            if (m_currentCharacterData == null)
            {
                return false;
            }

            bool didChange = m_currentCharacterData.SetSiteOfGraceActivated(
                siteOfGraceID,
                isActivated);
            if (didChange && saveImmediately && CanSaveGame)
            {
                SaveGame();
            }

            return didChange;
        }

        /// <summary>Stores the Site of Grace that should receive the next revival.</summary>
        public bool RecordLastSiteOfGraceRestedAt(
            int siteOfGraceID,
            bool saveImmediately)
        {
            if (m_currentCharacterData == null || siteOfGraceID < 0)
            {
                return false;
            }

            bool didChange =
                m_currentCharacterData.LastSiteOfGraceRestedAt != siteOfGraceID;
            m_currentCharacterData.LastSiteOfGraceRestedAt = siteOfGraceID;
            if (didChange && saveImmediately && CanSaveGame)
            {
                SaveGame();
            }

            return didChange;
        }

        /// <summary>Transfers a Rune balance into a persistent world recovery point.</summary>
        public bool RecordDeadSpot(
            Vector3 position,
            int runeCount,
            bool saveImmediately)
        {
            if (m_currentCharacterData == null || runeCount <= 0)
            {
                return false;
            }

            m_currentCharacterData.HasDeadSpot = true;
            m_currentCharacterData.DeadSpotPositionX = position.x;
            m_currentCharacterData.DeadSpotPositionY = position.y;
            m_currentCharacterData.DeadSpotPositionZ = position.z;
            m_currentCharacterData.DeadSpotRuneCount = runeCount;
            if (saveImmediately && CanSaveGame)
            {
                SaveGame();
            }

            return true;
        }

        /// <summary>Clears the saved recovery point after its Runes are reclaimed.</summary>
        public bool ClearDeadSpot(bool saveImmediately)
        {
            if (m_currentCharacterData == null)
            {
                return false;
            }

            bool didChange = m_currentCharacterData.HasDeadSpot ||
                m_currentCharacterData.DeadSpotRuneCount > 0;
            m_currentCharacterData.HasDeadSpot = false;
            m_currentCharacterData.DeadSpotPositionX = 0f;
            m_currentCharacterData.DeadSpotPositionY = 0f;
            m_currentCharacterData.DeadSpotPositionZ = 0f;
            m_currentCharacterData.DeadSpotRuneCount = 0;
            if (didChange && saveImmediately && CanSaveGame)
            {
                SaveGame();
            }

            return didChange;
        }

        /// <summary>
        /// Advances one boss state and optionally writes the active save immediately.
        /// </summary>
        public bool RecordBossProgress(
            int bossID,
            BossProgressState progress,
            bool saveImmediately)
        {
            if (m_currentCharacterData == null)
            {
                return false;
            }

            bool didChange = m_currentCharacterData.SetBossProgress(
                bossID,
                progress);
            if (didChange && saveImmediately && CanSaveGame)
            {
                SaveGame();
            }

            return didChange;
        }

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                return;
            }

            Destroy(gameObject);
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            LoadAllCharacterSlots();
        }

        private void Update()
        {
            if (m_sceneLoadIsInProgress ||
                m_shouldApplyLoadedCharacterData ||
                m_currentCharacterData == null ||
                m_currentCharacterSlotBeingUsed == CharacterSlot.NoSlot ||
                m_player == null ||
                !m_player.IsOwner ||
                SceneManager.GetActiveScene().buildIndex <= 0)
            {
                return;
            }

            m_currentCharacterData.SecondsPlayed += Time.unscaledDeltaTime;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// Registers the locally owned player as the runtime source for save data.
        /// </summary>
        public void RegisterPlayer(PlayerManager localPlayer)
        {
            if (localPlayer == null || !localPlayer.IsOwner)
            {
                return;
            }

            m_player = localPlayer;
        }

        /// <summary>
        /// Clears the local player without disturbing a newer ownership registration.
        /// </summary>
        public void UnregisterPlayer(PlayerManager localPlayer)
        {
            if (m_player == localPlayer)
            {
                m_player = null;
            }
        }

        /// <summary>Converts one runtime weapon instance into persistent state.</summary>
        public static SerializableWeapon GetSerializableWeaponFromWeaponItem(
            WeaponItem weapon)
        {
            return new SerializableWeapon(
                weapon?.ItemID ?? -1,
                weapon?.AshOfWarAction?.ItemID ?? -1,
                (int)(weapon?.UpgradeLevel ?? UpgradeLevel.Level0));
        }

        /// <summary>Converts one generic runtime stack into persistent state.</summary>
        public static SerializableItemStack GetSerializableItemStackFromItem(
            Item item)
        {
            return new SerializableItemStack(
                item?.ItemID ?? -1,
                item?.CurrentItemAmount ?? 0);
        }

        /// <summary>Converts one runtime ammunition stack into persistent state.</summary>
        public static SerializableRangeProjectile
            GetSerializableProjectileFromProjectileItem(
                RangedProjectileItem projectile)
        {
            return new SerializableRangeProjectile(
                projectile?.ItemID ?? -1,
                projectile?.CurrentAmmoAmount ?? 0);
        }

        /// <summary>Converts one runtime quick-slot item into persistent state.</summary>
        public static SerializableQuickSlotItem
            GetSerializableQuickSlotItemFromQuickSlotItem(
                QuickSlotItem quickSlotItem,
                PlayerManager player)
        {
            return new SerializableQuickSlotItem(
                quickSlotItem?.ItemID ?? -1,
                quickSlotItem?.GetCurrentAmount(player) ?? 0);
        }

        /// <summary>
        /// Preserves the existing title-screen entry point for creating a new character.
        /// </summary>
        public bool AttemptToCreateNewGame()
        {
            return NewGame();
        }

        /// <summary>Returns whether at least one of the ten fixed character slots is free.</summary>
        public bool HasFreeCharacterSlot()
        {
            return TryFindFreeCharacterSlot(out _, out _);
        }

        /// <summary>
        /// Returns the last slot played by this machine, when it still holds a valid
        /// save. Recency is tracked separately from <see cref="CharacterSaveData.SecondsPlayed"/>
        /// (an accumulated total, not a "last played" marker).
        /// </summary>
        public bool TryGetContinueSlot(out CharacterSlot continueSlot)
        {
            continueSlot = (CharacterSlot)PlayerPrefs.GetInt(
                k_LastPlayedCharacterSlotKey,
                (int)CharacterSlot.NoSlot);
            if (continueSlot == CharacterSlot.NoSlot)
            {
                return false;
            }

            if (GetCharacterDataForSlot(continueSlot) == null)
            {
                continueSlot = CharacterSlot.NoSlot;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Marks a slot as the most recently played one. Called after successful
        /// create, load, and save operations so Continue always lands on the last
        /// character this machine actually played.
        /// </summary>
        private void SetLastPlayedCharacterSlot(CharacterSlot characterSlot)
        {
            if (characterSlot == CharacterSlot.NoSlot)
            {
                return;
            }

            PlayerPrefs.SetInt(k_LastPlayedCharacterSlotKey, (int)characterSlot);
            PlayerPrefs.Save();
        }

        /// <summary>Clears the last-played marker when its slot is deleted.</summary>
        private void ClearLastPlayedCharacterSlotIfMatches(CharacterSlot characterSlot)
        {
            if (characterSlot == CharacterSlot.NoSlot)
            {
                return;
            }

            if ((CharacterSlot)PlayerPrefs.GetInt(
                    k_LastPlayedCharacterSlotKey,
                    (int)CharacterSlot.NoSlot) == characterSlot)
            {
                PlayerPrefs.DeleteKey(k_LastPlayedCharacterSlotKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Creates starting character data in the first free slot and loads the world.
        /// </summary>
        public bool NewGame()
        {
            return NewGame(new CharacterSaveData
            {
                CharacterName = "Unnamed",
                SceneIndex = m_startingSceneIndex
            });
        }

        /// <summary>
        /// Reserves the first free slot with a complete creation snapshot before loading the world.
        /// </summary>
        public bool NewGame(CharacterSaveData startingCharacterData)
        {
            if (m_sceneLoadIsInProgress ||
                m_shouldApplyLoadedCharacterData ||
                startingCharacterData == null ||
                !CanLoadScene(m_startingSceneIndex) ||
                !TryFindFreeCharacterSlot(
                    out CharacterSlot freeSlot,
                    out SaveFileDataWriter writer))
            {
                return false;
            }

            startingCharacterData.CharacterName = string.IsNullOrWhiteSpace(
                startingCharacterData.CharacterName)
                    ? "Unnamed"
                    : startingCharacterData.CharacterName.Trim();
            startingCharacterData.SceneIndex = m_startingSceneIndex;

            try
            {
                writer.SaveFile(startingCharacterData);
            }
            catch (IOException exception)
            {
                Debug.LogError($"Could not create {freeSlot}: {exception.Message}");
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogError($"Could not create {freeSlot}: {exception.Message}");
                return false;
            }

            m_currentCharacterSlotBeingUsed = freeSlot;
            m_currentCharacterData = startingCharacterData;
            GetStageIDsOnLoad();
            SetCharacterDataForSlot(freeSlot, startingCharacterData);
            SetLastPlayedCharacterSlot(freeSlot);
            m_shouldApplyLoadedCharacterData = true;
            return TryBeginSceneLoad(m_startingSceneIndex);
        }

        /// <summary>
        /// Writes the locally owned player's runtime state to the current slot.
        /// </summary>
        public bool SaveGame()
        {
            if (!CanSaveGame)
            {
                Debug.LogError(
                    "Saving requires an active character and locally owned player after scene restoration completes.");
                return false;
            }

            try
            {
                m_player.SaveGameDataToCurrentCharacterData();
                CreateWriter(m_currentCharacterSlotBeingUsed).SaveFile(m_currentCharacterData);
                SetCharacterDataForSlot(m_currentCharacterSlotBeingUsed, m_currentCharacterData);
                SetLastPlayedCharacterSlot(m_currentCharacterSlotBeingUsed);
                return true;
            }
            catch (IOException exception)
            {
                Debug.LogError($"Could not save {m_currentCharacterSlotBeingUsed}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogError($"Could not save {m_currentCharacterSlotBeingUsed}: {exception.Message}");
            }

            return false;
        }

        /// <summary>
        /// Loads the current slot from disk, then begins loading its saved scene.
        /// </summary>
        public void LoadGame()
        {
            if (m_sceneLoadIsInProgress || m_shouldApplyLoadedCharacterData)
            {
                return;
            }

            if (m_currentCharacterSlotBeingUsed == CharacterSlot.NoSlot)
            {
                Debug.LogError("A character slot must be selected before loading a game.");
                return;
            }

            try
            {
                CharacterSaveData loadedData =
                    CreateWriter(m_currentCharacterSlotBeingUsed).LoadSaveFile();
                if (loadedData == null)
                {
                    Debug.LogError($"No save file exists for {m_currentCharacterSlotBeingUsed}.");
                    return;
                }

                if (!CanLoadScene(loadedData.SceneIndex))
                {
                    return;
                }

                m_currentCharacterData = loadedData;
                GetStageIDsOnLoad();
                SetCharacterDataForSlot(m_currentCharacterSlotBeingUsed, loadedData);
                SetLastPlayedCharacterSlot(m_currentCharacterSlotBeingUsed);
                m_shouldApplyLoadedCharacterData = true;
                TryBeginSceneLoad(loadedData.SceneIndex);
            }
            catch (IOException exception)
            {
                Debug.LogError($"Could not load {m_currentCharacterSlotBeingUsed}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogError($"Could not load {m_currentCharacterSlotBeingUsed}: {exception.Message}");
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"Could not parse {m_currentCharacterSlotBeingUsed}: {exception.Message}");
            }
        }

        /// <summary>
        /// Selects a cached character slot without reading the file from UI code.
        /// </summary>
        public void SelectCharacterSlot(CharacterSlot characterSlot)
        {
            if (!m_sceneLoadIsInProgress && !m_shouldApplyLoadedCharacterData)
            {
                m_currentCharacterSlotBeingUsed = characterSlot;
            }
        }

        /// <summary>
        /// Deletes one slot from disk and clears its in-memory cache.
        /// </summary>
        public void DeleteGame(CharacterSlot characterSlot)
        {
            if (m_sceneLoadIsInProgress ||
                m_shouldApplyLoadedCharacterData ||
                characterSlot == CharacterSlot.NoSlot)
            {
                return;
            }

            try
            {
                CreateWriter(characterSlot).DeleteSaveFile();
                SetCharacterDataForSlot(characterSlot, null);
                ClearLastPlayedCharacterSlotIfMatches(characterSlot);
                if (m_currentCharacterSlotBeingUsed == characterSlot)
                {
                    m_currentCharacterSlotBeingUsed = CharacterSlot.NoSlot;
                    m_currentCharacterData = null;
                    m_shouldApplyLoadedCharacterData = false;
                }
            }
            catch (IOException exception)
            {
                Debug.LogError($"Could not delete {characterSlot}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogError($"Could not delete {characterSlot}: {exception.Message}");
            }
        }

        /// <summary>
        /// Scans all ten fixed slots and refreshes the in-memory save cache.
        /// </summary>
        public void LoadAllCharacterSlots()
        {
            for (int slotNumber = 1; slotNumber <= 10; slotNumber++)
            {
                CharacterSlot slot = (CharacterSlot)slotNumber;
                try
                {
                    SetCharacterDataForSlot(slot, CreateWriter(slot).LoadSaveFile());
                }
                catch (IOException exception)
                {
                    SetCharacterDataForSlot(slot, null);
                    Debug.LogError($"Could not scan {slot}: {exception.Message}");
                }
                catch (UnauthorizedAccessException exception)
                {
                    SetCharacterDataForSlot(slot, null);
                    Debug.LogError($"Could not scan {slot}: {exception.Message}");
                }
                catch (ArgumentException exception)
                {
                    SetCharacterDataForSlot(slot, null);
                    Debug.LogError($"Could not parse {slot}: {exception.Message}");
                }
            }
        }

        /// <summary>
        /// Returns the cached save data associated with one fixed slot.
        /// </summary>
        public CharacterSaveData GetCharacterDataForSlot(CharacterSlot characterSlot)
        {
            return characterSlot switch
            {
                CharacterSlot.CharacterSlot01 => m_characterSlot01,
                CharacterSlot.CharacterSlot02 => m_characterSlot02,
                CharacterSlot.CharacterSlot03 => m_characterSlot03,
                CharacterSlot.CharacterSlot04 => m_characterSlot04,
                CharacterSlot.CharacterSlot05 => m_characterSlot05,
                CharacterSlot.CharacterSlot06 => m_characterSlot06,
                CharacterSlot.CharacterSlot07 => m_characterSlot07,
                CharacterSlot.CharacterSlot08 => m_characterSlot08,
                CharacterSlot.CharacterSlot09 => m_characterSlot09,
                CharacterSlot.CharacterSlot10 => m_characterSlot10,
                _ => null
            };
        }

        /// <summary>
        /// Provides the single filename definition for every fixed character slot.
        /// </summary>
        public string DecideCharacterFileNameBasedOnCharacterSlot(CharacterSlot characterSlot)
        {
            return characterSlot switch
            {
                CharacterSlot.CharacterSlot01 => "CharacterSlot01",
                CharacterSlot.CharacterSlot02 => "CharacterSlot02",
                CharacterSlot.CharacterSlot03 => "CharacterSlot03",
                CharacterSlot.CharacterSlot04 => "CharacterSlot04",
                CharacterSlot.CharacterSlot05 => "CharacterSlot05",
                CharacterSlot.CharacterSlot06 => "CharacterSlot06",
                CharacterSlot.CharacterSlot07 => "CharacterSlot07",
                CharacterSlot.CharacterSlot08 => "CharacterSlot08",
                CharacterSlot.CharacterSlot09 => "CharacterSlot09",
                CharacterSlot.CharacterSlot10 => "CharacterSlot10",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Applies pending loaded data after the locally owned player is ready in the new scene.
        /// </summary>
        public bool TryApplyLoadedCharacterData(PlayerManager localPlayer)
        {
            if (!m_shouldApplyLoadedCharacterData ||
                m_currentCharacterData == null ||
                localPlayer == null ||
                localPlayer != m_player ||
                !localPlayer.IsOwner)
            {
                return false;
            }

            localPlayer.LoadGameDataFromCurrentCharacterData();
            m_shouldApplyLoadedCharacterData = false;
            return true;
        }

        /// <summary>
        /// Loads the configured starting Scene without assigning a character slot.
        /// </summary>
        public IEnumerator LoadNewGame()
        {
            if (!TryBeginSceneLoad(m_startingSceneIndex))
            {
                yield break;
            }

            while (m_sceneLoadIsInProgress)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Loads the front-end Scene without saving the active character.
        /// </summary>
        public void ReturnToMainMenu()
        {
            int mainMenuBuildIndex = SceneUtility.GetBuildIndexByScenePath(
                WorldScenePathLayout.MainMenuScenePath);
            if (mainMenuBuildIndex < 0)
            {
                HandleSceneLoadFailure(
                    $"The main menu Scene at {WorldScenePathLayout.MainMenuScenePath} " +
                    "is not in Build Settings.");
                return;
            }

            if (m_sceneLoadIsInProgress)
            {
                return;
            }
            m_shouldApplyLoadedCharacterData = false;
            m_sceneLoadIsInProgress = true;
            StartCoroutine(ReturnToMainMenuRoutine(mainMenuBuildIndex));
        }

        private IEnumerator ReturnToMainMenuRoutine(int sceneIndex)
        {
            try
            {
                PlayerUIManager.Instance?.PlayerUILoadingScreenManager?.ActivateLoadingScreen();
                NetworkManager networkManager = NetworkManager.Singleton;
                if (networkManager != null && networkManager.IsListening)
                {
                    // Area scenes are streamed locally; leave the session before unloading the world.
                    networkManager.Shutdown();
                    while (networkManager != null && networkManager.ShutdownInProgress)
                    {
                        yield return null;
                    }
                }
                yield return SceneManager.LoadSceneAsync(sceneIndex, LoadSceneMode.Single);
            }
            finally
            {
                m_sceneLoadIsInProgress = false;
                PlayerUIManager.Instance?.PlayerUILoadingScreenManager?.DeactivateLoadingScreen(0f);
            }
        }

        /// <summary>
        /// Returns the Build Settings index used to enable gameplay input.
        /// </summary>
        public int GetWorldSceneIndex()
        {
            return SceneUtility.GetBuildIndexByScenePath(
                $"{WorldScenePathLayout.LevelFolder}/{m_worldSceneName}.unity");
        }

        private SaveFileDataWriter CreateWriter(CharacterSlot characterSlot)
        {
            string fileName = DecideCharacterFileNameBasedOnCharacterSlot(characterSlot);
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(
                    $"{characterSlot} is not a writable character slot.",
                    nameof(characterSlot));
            }

            return new SaveFileDataWriter(Application.persistentDataPath, fileName);
        }

        private bool TryFindFreeCharacterSlot(
            out CharacterSlot freeSlot,
            out SaveFileDataWriter writer)
        {
            for (int slotNumber = 1; slotNumber <= 10; slotNumber++)
            {
                CharacterSlot candidateSlot = (CharacterSlot)slotNumber;
                SaveFileDataWriter candidateWriter = CreateWriter(candidateSlot);
                if (candidateWriter.CheckToSeeIfFileExists())
                {
                    continue;
                }

                freeSlot = candidateSlot;
                writer = candidateWriter;
                return true;
            }

            freeSlot = CharacterSlot.NoSlot;
            writer = null;
            return false;
        }

        private IEnumerator LoadScene(int sceneIndex)
        {
            try
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
                if (string.IsNullOrEmpty(scenePath))
                {
                    HandleSceneLoadFailure(
                        $"Scene build index {sceneIndex} is not available in Build Settings.");
                    yield break;
                }

                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                NetworkManager networkManager = NetworkManager.Singleton;
                if (networkManager != null &&
                    networkManager.IsListening &&
                    !networkManager.IsServer)
                {
                    HandleSceneLoadFailure(
                        "Only the server can load a saved Scene.");
                    yield break;
                }

                PlayerUIManager.Instance?.PlayerUILoadingScreenManager
                    ?.ActivateLoadingScreen();
                if (networkManager != null && networkManager.IsListening)
                {
                    yield return LoadNetworkScene(networkManager, sceneName);
                    yield break;
                }

                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                    sceneIndex,
                    LoadSceneMode.Single);
                if (loadOperation == null)
                {
                    HandleSceneLoadFailure(
                        $"Could not start loading {sceneName}.");
                    yield break;
                }

                yield return loadOperation;
            }
            finally
            {
                m_sceneLoadIsInProgress = false;
            }
        }

        private IEnumerator LoadNetworkScene(
            NetworkManager networkManager,
            string sceneName)
        {
            NetworkSceneManager networkSceneManager = networkManager.SceneManager;
            if (networkSceneManager == null)
            {
                HandleSceneLoadFailure(
                    $"Could not load {sceneName}: NetworkSceneManager is unavailable.");
                yield break;
            }

            bool sceneLoadCompleted = false;
            void HandleSceneEvent(SceneEvent sceneEvent)
            {
                if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
                    sceneEvent.SceneName == sceneName)
                {
                    sceneLoadCompleted = true;
                }
            }

            networkSceneManager.OnSceneEvent += HandleSceneEvent;
            try
            {
                float retryUntil = Time.realtimeSinceStartup +
                    k_SceneEventRetryTimeoutSeconds;
                while (Time.realtimeSinceStartup < retryUntil)
                {
                    if (!networkManager.IsListening || !networkManager.IsServer)
                    {
                        HandleSceneLoadFailure(
                            $"Could not load {sceneName}: the network host stopped.");
                        yield break;
                    }

                    sceneLoadCompleted = false;
                    SceneEventProgressStatus status = networkSceneManager.LoadScene(
                        sceneName,
                        LoadSceneMode.Single);
                    if (ShouldRetrySceneLoad(status))
                    {
                        yield return null;
                        continue;
                    }

                    if (status != SceneEventProgressStatus.Started)
                    {
                        HandleSceneLoadFailure(
                            $"Could not load {sceneName}: {status}.");
                        yield break;
                    }

                    float completeUntil = Time.realtimeSinceStartup +
                        k_SceneEventRetryTimeoutSeconds;
                    while (!sceneLoadCompleted &&
                        Time.realtimeSinceStartup < completeUntil &&
                        networkManager.IsListening)
                    {
                        yield return null;
                    }

                    if (!sceneLoadCompleted)
                    {
                        string reason = networkManager.IsListening
                            ? "the Netcode load event did not complete in time"
                            : "the network host stopped";
                        HandleSceneLoadFailure(
                            $"Could not load {sceneName}: {reason}.");
                    }

                    yield break;
                }

                HandleSceneLoadFailure(
                    $"Timed out waiting to start loading {sceneName}.");
            }
            finally
            {
                networkSceneManager.OnSceneEvent -= HandleSceneEvent;
            }
        }

        private bool TryBeginSceneLoad(int sceneIndex)
        {
            if (m_sceneLoadIsInProgress || !CanLoadScene(sceneIndex))
            {
                return false;
            }

            m_sceneLoadIsInProgress = true;
            StartCoroutine(LoadScene(sceneIndex));
            return true;
        }

        private bool CanLoadScene(int sceneIndex)
        {
            if (sceneIndex < 0 ||
                sceneIndex >= SceneManager.sceneCountInBuildSettings ||
                string.IsNullOrEmpty(SceneUtility.GetScenePathByBuildIndex(sceneIndex)))
            {
                Debug.LogError($"Scene build index {sceneIndex} is not available in Build Settings.");
                return false;
            }

            if (!isActiveAndEnabled)
            {
                Debug.LogError("The save manager must be active to load a Scene.");
                return false;
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
            {
                Debug.LogError("Only the server can load a saved Scene.");
                return false;
            }

            return true;
        }

        private static bool ShouldRetrySceneLoad(
            SceneEventProgressStatus status)
        {
            return status == SceneEventProgressStatus.SceneEventInProgress;
        }

        private void HandleSceneLoadFailure(string message)
        {
            m_shouldApplyLoadedCharacterData = false;
            Debug.LogError(message);
            PlayerUIManager.Instance?.PlayerUILoadingScreenManager
                ?.DeactivateLoadingScreen(0f);
        }

        private void SetCharacterDataForSlot(
            CharacterSlot characterSlot,
            CharacterSaveData characterSaveData)
        {
            switch (characterSlot)
            {
                case CharacterSlot.CharacterSlot01:
                    m_characterSlot01 = characterSaveData;
                    break;
                case CharacterSlot.CharacterSlot02:
                    m_characterSlot02 = characterSaveData;
                    break;
                case CharacterSlot.CharacterSlot03:
                    m_characterSlot03 = characterSaveData;
                    break;
                case CharacterSlot.CharacterSlot04:
                    m_characterSlot04 = characterSaveData;
                    break;
                case CharacterSlot.CharacterSlot05:
                    m_characterSlot05 = characterSaveData;
                    break;
                case CharacterSlot.CharacterSlot06:
                    m_characterSlot06 = characterSaveData;
                    break;
                case CharacterSlot.CharacterSlot07:
                    m_characterSlot07 = characterSaveData;
                    break;
                case CharacterSlot.CharacterSlot08:
                    m_characterSlot08 = characterSaveData;
                    break;
                case CharacterSlot.CharacterSlot09:
                    m_characterSlot09 = characterSaveData;
                    break;
                case CharacterSlot.CharacterSlot10:
                    m_characterSlot10 = characterSaveData;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(characterSlot),
                        characterSlot,
                        "Only fixed character slots can be cached.");
            }
        }
    }
}
