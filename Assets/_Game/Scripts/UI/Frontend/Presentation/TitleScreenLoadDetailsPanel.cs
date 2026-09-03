using TMPro;
using UnityEngine;

namespace ZZ
{
    /// <summary>Shows details for the save slot currently selected on the title screen.</summary>
    public sealed class TitleScreenLoadDetailsPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_slotLabel;
        [SerializeField] private TMP_Text m_characterName;
        [SerializeField] private TMP_Text m_playtime;
        [SerializeField] private TMP_Text m_progress;
        [SerializeField] private TMP_Text m_attributes;

        private void OnDisable()
        {
            Clear();
        }

        /// <summary>Displays cached save data for one character slot.</summary>
        public void Display(CharacterSlot characterSlot)
        {
            CharacterSaveData data = WorldSaveGameManager.Instance?
                .GetCharacterDataForSlot(characterSlot);
            if (data == null)
            {
                Clear();
                return;
            }

            SetText(m_slotLabel, $"SAVE SLOT {(int)characterSlot:00}");
            SetText(m_characterName, data.CharacterName.ToUpperInvariant());
            SetText(m_playtime, $"PLAYTIME  {FormatTime(data.SecondsPlayed)}");
            SetText(
                m_progress,
                $"REGION    AREA {data.SceneIndex:00}\n" +
                $"RUNES     {data.Runes:N0}");
            SetText(
                m_attributes,
                $"VIT {data.Vitality:00}     END {data.Endurance:00}\n" +
                $"STR {data.Strength:00}     DEX {data.Dexterity:00}\n" +
                $"INT {data.Intelligence:00}     FAI {data.Faith:00}");
        }

        /// <summary>Restores the panel's no-selection presentation.</summary>
        public void Clear()
        {
            SetText(m_slotLabel, "SAVE DATA");
            SetText(m_characterName, "SELECT A SLOT");
            SetText(m_playtime, "PLAYTIME  --:--:--");
            SetText(m_progress, "REGION    --\nRUNES     --");
            SetText(m_attributes, "VIT --     END --\nSTR --     DEX --\nINT --     FAI --");
        }

        private static string FormatTime(float secondsPlayed)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(secondsPlayed));
            int hours = totalSeconds / 3600;
            int minutes = totalSeconds % 3600 / 60;
            int seconds = totalSeconds % 60;
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
