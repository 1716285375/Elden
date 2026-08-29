using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    public class UICharacterSaveSlot : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private CharacterSlot m_characterSlot = CharacterSlot.NoSlot;
        [SerializeField] private TMP_Text m_characterNameText;
        [SerializeField] private TMP_Text m_timePlayedText;
        [SerializeField] private TitleScreenManager m_titleScreenManager;
        [SerializeField] private Button m_button;

        public CharacterSlot CharacterSlot => m_characterSlot;

        private void Awake()
        {
            m_button ??= GetComponent<Button>();
            m_titleScreenManager ??= GetComponentInParent<TitleScreenManager>(true);
        }

        private void OnEnable()
        {
            LoadSaveSlot();
        }

        /// <summary>
        /// Refreshes this slot from the World save cache and hides missing characters.
        /// </summary>
        public void LoadSaveSlot()
        {
            CharacterSaveData characterData =
                WorldSaveGameManager.Instance?.GetCharacterDataForSlot(m_characterSlot);
            if (characterData == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (m_characterNameText != null)
            {
                m_characterNameText.text = characterData.CharacterName;
            }

            if (m_timePlayedText != null)
            {
                m_timePlayedText.text = FormatTimePlayed(characterData.SecondsPlayed);
            }
        }

        /// <summary>
        /// Selects this cached slot and begins loading its saved Scene.
        /// </summary>
        public void LoadGameFromCharacterSlot()
        {
            if (WorldSaveGameManager.Instance == null || m_characterSlot == CharacterSlot.NoSlot)
            {
                return;
            }

            WorldSaveGameManager.Instance.SelectCharacterSlot(m_characterSlot);
            WorldSaveGameManager.Instance.LoadGame();
        }

        /// <summary>
        /// Moves EventSystem focus to this slot's Button.
        /// </summary>
        public void Select()
        {
            m_button?.Select();
        }

        /// <summary>
        /// Records this slot as the title screen's current deletion target.
        /// </summary>
        public void OnSelect(BaseEventData eventData)
        {
            m_titleScreenManager?.SelectCurrentSlot(m_characterSlot);
        }

        private static string FormatTimePlayed(float secondsPlayed)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(secondsPlayed));
            int totalHours = totalSeconds / 3600;
            int minutes = totalSeconds % 3600 / 60;
            int seconds = totalSeconds % 60;
            return $"{totalHours:00}:{minutes:00}:{seconds:00}";
        }
    }
}
