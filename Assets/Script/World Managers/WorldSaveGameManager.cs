using System;
using System.Collections;
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

        [FormerlySerializedAs("worldSceneName")]
        [SerializeField] private string m_worldSceneName = "Scene_World_01";
        [SerializeField] private int m_startingSceneIndex = 1;

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
            m_currentCharacterSlotBeingUsed != CharacterSlot.NoSlot &&
            m_currentCharacterData != null &&
            m_player != null &&
            m_player.IsOwner;

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
            if (m_currentCharacterData == null ||
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

        /// <summary>
        /// Chooses the first empty fixed slot and begins loading the starting scene.
        /// </summary>
        public bool AttemptToCreateNewGame()
        {
            for (int slotNumber = 1; slotNumber <= 10; slotNumber++)
            {
                CharacterSlot slot = (CharacterSlot)slotNumber;
                SaveFileDataWriter writer = CreateWriter(slot);
                if (writer.CheckToSeeIfFileExists())
                {
                    continue;
                }

                m_currentCharacterSlotBeingUsed = slot;
                m_currentCharacterData = new CharacterSaveData
                {
                    CharacterName = "Unnamed",
                    SceneIndex = m_startingSceneIndex
                };
                m_shouldApplyLoadedCharacterData = false;
                StartCoroutine(LoadScene(m_startingSceneIndex));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Writes the locally owned player's runtime state to the current slot.
        /// </summary>
        public bool SaveGame()
        {
            if (!CanSaveGame)
            {
                Debug.LogError(
                    "A current character slot, character data, and locally owned player are required to save.");
                return false;
            }

            try
            {
                m_player.SaveGameDataToCurrentCharacterData();
                CreateWriter(m_currentCharacterSlotBeingUsed).SaveFile(m_currentCharacterData);
                SetCharacterDataForSlot(m_currentCharacterSlotBeingUsed, m_currentCharacterData);
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

                m_currentCharacterData = loadedData;
                SetCharacterDataForSlot(m_currentCharacterSlotBeingUsed, loadedData);
                m_shouldApplyLoadedCharacterData = true;
                StartCoroutine(LoadScene(loadedData.SceneIndex));
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
            m_currentCharacterSlotBeingUsed = characterSlot;
        }

        /// <summary>
        /// Deletes one slot from disk and clears its in-memory cache.
        /// </summary>
        public void DeleteGame(CharacterSlot characterSlot)
        {
            if (characterSlot == CharacterSlot.NoSlot)
            {
                return;
            }

            try
            {
                CreateWriter(characterSlot).DeleteSaveFile();
                SetCharacterDataForSlot(characterSlot, null);
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
            yield return LoadScene(m_startingSceneIndex);
        }

        /// <summary>
        /// Returns the Build Settings index used to enable gameplay input.
        /// </summary>
        public int GetWorldSceneIndex()
        {
            return SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{m_worldSceneName}.unity");
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

        private IEnumerator LoadScene(int sceneIndex)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError($"Scene build index {sceneIndex} is not available in Build Settings.");
                yield break;
            }

            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                if (!networkManager.IsServer)
                {
                    Debug.LogError("Only the server can load a saved Scene.");
                    yield break;
                }

                SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(
                    sceneName,
                    LoadSceneMode.Single);
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError($"Could not load {sceneName}: {status}.");
                }

                yield break;
            }

            yield return SceneManager.LoadSceneAsync(sceneIndex, LoadSceneMode.Single);
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
